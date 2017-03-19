using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Barebones.Logging;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Authentication module, which handles
    ///     most of the authentication functionality on Master Server.
    /// </summary>
    public class AuthModule : MasterModule
    {
        public delegate IAccountData AccountFactory();

        public delegate void LoginSuccessfulHandler(ISession session, IAccountData accountData);

        public List<string> ForbiddenUsernames;
        public List<string> ForbiddenWordsInUsernames;

        /// <summary>
        ///     Collection of users who are logged in
        /// </summary>
        protected Dictionary<string, ISession> LoggedInUsers;

        public int UsernameMaxChars = 25;
        public int UsernameMinChars = 3;
        public bool EnableGuestLogin = true;

        /// <summary>
        ///     Authentication database
        /// </summary>
        public IAuthDatabase Database { get; private set; }

        public AuthModuleConfig ModuleConfig { get; private set; }

        protected Master MasterServer { get; set; }

        /// <summary>
        ///     Event, invoked when user logs in
        /// </summary>
        public event LoginSuccessfulHandler OnLogin;

        public event Action<IAccountData> OnEmailConfirmed;

        public string GuestPrefix = "Guest-";

        [Header("E-mail settings")]
        public string SmtpHost = "smtp.gmail.com";
        public int SmtpPort = 587;
        public string SmtpUsername = "username@gmail.com";
        public string SmtpPassword = "password";
        public string EmailFrom = "YourGame@gmail.com";
        public string SenderDisplayName = "Awesome Game";

        [TextArea(3, 10)]
        public string ActivationForm = "<h1>Activation</h1>" +
                                       "<p>Your email activation code is: <b>{0}</b> </p>";

        [TextArea(3, 10)]
        public string PasswordResetCode = "<h1>Password Reset Code</h1>" +
                                       "<p>Your password reset code is: <b>{0}</b> </p>";

        private List<Exception> _sendMailExceptions;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        protected SmtpClient SmtpClient;

#endif
        protected BmLogger Logger = LogManager.GetLogger(typeof(AuthModule).ToString());

        private void Awake()
        {
            ExtractCmdArgs();

            _sendMailExceptions = new List<Exception>();
            SetupSmtpClient();

            GuestPrefix = GuestPrefix.ToLower();
            ForbiddenUsernames = ForbiddenUsernames.Select(u => u.ToLower()).ToList();
            ForbiddenWordsInUsernames = ForbiddenWordsInUsernames.Select(u => u.ToLower()).ToList();

            LoggedInUsers = new Dictionary<string, ISession>();
            ModuleConfig = new AuthModuleConfig
            {
                ForbiddenUsernames = ForbiddenUsernames,
                ForbiddenWordsInUsernames = ForbiddenWordsInUsernames,
                UsernameMaxChars = UsernameMaxChars,
                UsernameMinChars = UsernameMinChars
            };

            // Add guest prefix to forbidden words
            ModuleConfig.ForbiddenWordsInUsernames.Add(GuestPrefix);
        }

        public override void Initialize(IMaster master)
        {
            var dbFactory = DatabaseAccessorFactory.Instance;
            Logger.Error(dbFactory == null, "Database accessor factory not found");

            Database = dbFactory.GetAccessor<IAuthDatabase>();

            Logger.Error(Database == null, "Auth database accessor not found");

            master.SetClientHandler(new LoginUserHandler(this));
            master.SetClientHandler(new RegisterUserHandler(this));

            master.SetClientHandler(new PacketHandler(BmOpCodes.PasswordResetCodeRequest, HandleCodeRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.PasswordChange, HandlePasswordChange));
            master.SetClientHandler(new PacketHandler(BmOpCodes.ConfirmEmailCodeRequest, HandleConfirmationCodeRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.ConfirmEmail, HandleEmailConfirmation));
            master.SetClientHandler(new PacketHandler(BmOpCodes.GetLoggedInCount, HandleGetLoggedInCount));
        }

        protected void Update()
        {

            // Log errors for any exceptions that might have occured
            // when sending mail
            if (_sendMailExceptions.Count > 0)
            {
                lock (_sendMailExceptions)
                {
                    foreach (var exception in _sendMailExceptions)
                    {
                        Logs.Error(exception);
                    }

                    _sendMailExceptions.Clear();
                }
            }
        }

        protected virtual void SetupSmtpClient()
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            // Configure mail client
            SmtpClient = new SmtpClient(SmtpHost, SmtpPort);

            // set the network credentials
            SmtpClient.Credentials = new NetworkCredential(SmtpUsername, SmtpPassword) as ICredentialsByHost;
            SmtpClient.EnableSsl = true;

            SmtpClient.SendCompleted += (sender, args) =>
            {
                if (args.Error != null)
                {
                    lock (_sendMailExceptions)
                    {
                        _sendMailExceptions.Add(args.Error);
                    }
                }
            };

            ServicePointManager.ServerCertificateValidationCallback =
                delegate (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
                { return true; };

#endif
        }

        /// <summary>
        ///     Called when someone successfully logs in
        /// </summary>
        /// <param name="session"></param>
        /// <param name="accountData"></param>
        public virtual void FinalizeLogin(ISession session, IAccountData accountData)
        {
            session.Account = accountData;
            session.OnDisconnect += OnSessionDisconnect;

            // Add to lookup of logged in users
            LoggedInUsers.Add(accountData.Username.ToLower(), session);

            session.SetUsername(accountData.Username);

            // Trigger the login event
            if (OnLogin != null)
                OnLogin.Invoke(session, accountData);

            accountData.OnChange += SaveChanges;
        }

        /// <summary>
        ///     Called when user successfully registers
        /// </summary>
        public virtual void FinalizeRegistration(IAccountData account, ISession session)
        {
        }

        /// <summary>
        ///     Called when authorized session disconnects
        /// </summary>
        /// <param name="session"></param>
        private void OnSessionDisconnect(ISession session)
        {
            if (session == null)
                return;

            LoggedInUsers.Remove(session.Username.ToLower());

            if (session.Account == null)
                return;

            session.Account.OnChange -= SaveChanges;
            session.OnDisconnect -= OnSessionDisconnect;
        }

        /// <summary>
        ///     Called when account is changed in some way.
        ///     Should save changes into database
        /// </summary>
        /// <param name="data"></param>
        public void SaveChanges(IAccountData data)
        {
            if (data.IsGuest)
                return;

            Database.UpdateAccount(data);
        }

        /// <summary>
        ///     Checks if user with specific username is logged in.
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public bool IsLoggedIn(string username)
        {
            return LoggedInUsers.ContainsKey(username.ToLower());
        }

        /// <summary>
        ///     Returns a session of user that is logged in with given username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public ISession GetLoggedInSession(string username)
        {
            ISession session;
            LoggedInUsers.TryGetValue(username.ToLower(), out session);
            return session;
        }


        /// <summary>
        /// A Generic Method to send email using Gmail
        /// </summary>
        /// <param name="to">The To address to send the email to</param>
        /// <param name="subject">The Subject of email</param>
        /// <param name="body">The Body of email</param>
        /// <param name="isBodyHtml">Tell whether body of email will be html of plain text</param>
        /// <param name="mailPriority">Set the mail priority to low, medium or high</param>
        /// <returns>Returns true if email is sent successfuly</returns>
        protected virtual bool SendMail(string to, string subject, string body, MailPriority mailPriority)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            // Create the mail message (from, to, subject, body)
            MailMessage mailMessage = new MailMessage();
            mailMessage.From = new MailAddress(EmailFrom, SenderDisplayName);
            mailMessage.To.Add(to);

            mailMessage.Subject = subject;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = true;
            mailMessage.Priority = mailPriority;

            // send the mail
            SmtpClient.SendAsync(mailMessage, "");

#endif
            return true;
        }

        /// <summary>
        /// Extracts related values from command line arguments
        /// </summary>
        public virtual void ExtractCmdArgs()
        {
            SmtpUsername = BmArgs.ExtractValue("-smtpUsername") ?? SmtpUsername;
            SmtpPassword = BmArgs.ExtractValue("-smtpPassword") ?? SmtpPassword;

            SmtpHost = BmArgs.ExtractValue("-smtpHost") ?? SmtpHost;
            SmtpPort = BmArgs.ExtractValueInt("-smtpPort", SmtpPort);
        }

        #region Message handlers

        /// <summary>
        /// Handles a request to return a number of players who are currently logged in
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGetLoggedInCount(IIncommingMessage message)
        {
            var response = MessageHelper.Create(message.OpCode, LoggedInUsers.Count);
            message.Respond(response, AckResponseStatus.Success);
        } 

        /// <summary>
        /// Handles a request from user to send a confirmation code to his e-mail
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleConfirmationCodeRequest(IIncommingMessage message)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            var session = message.Peer.GetProperty(BmPropCodes.Session) as Session;

            if (session == null || session.Account == null)
            {
                message.Respond("Invalid session", AckResponseStatus.Unauthorized);
                return;
            }

            if (session.Account.IsGuest)
            {
                message.Respond("Guests cannot confirm e-mails", AckResponseStatus.Unauthorized);
                return;
            }

            var code = BmHelper.CreateRandomString(6);

            // Save the new code
            Database.SaveEmailConfirmationCode(session.Account.Email, code);

            if (!SendMail(session.Account.Email, "E-mail confirmation", string.Format(ActivationForm, code),
                MailPriority.High))
            {
                message.Respond("Couldn't send a confirmation code to your e-mail. Please contact support");
                return;
            }

            // Respond with success
            message.Respond(AckResponseStatus.Success);
#endif


        }

        /// <summary>
        /// Handles a request from user to confirm his e-mail.
        /// User should send an activation code
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleEmailConfirmation(IIncommingMessage message)
        {
            var code = message.AsString();

            var session = message.Peer.GetProperty(BmPropCodes.Session) as Session;

            if (session == null || session.Account == null)
            {
                message.Respond("Invalid session", AckResponseStatus.Unauthorized);
                return;
            }

            if (session.Account.IsGuest)
            {
                message.Respond("Guests cannot confirm e-mails", AckResponseStatus.Unauthorized);
                return;
            }

            if (session.Account.IsEmailConfirmed)
            {
                // We still need to respond with "success" in case
                // response is handled somehow on the client
                message.Respond("Your email is already confirmed",
                    AckResponseStatus.Success);
                return;
            }

            var requiredCode = Database.GetEmailConfirmationCode(session.Account.Email);

            if (requiredCode != code)
            {
                message.Respond("Invalid activation code", AckResponseStatus.Error);
                return;
            }

            // Confirm e-mail
            session.Account.IsEmailConfirmed = true;

            // Update account
            Database.UpdateAccount(session.Account);

            // Respond with success
            message.Respond(AckResponseStatus.Success);

            // Invoke the event
            if (OnEmailConfirmed != null)
                OnEmailConfirmed.Invoke(session.Account);

        }

        protected virtual void HandlePasswordChange(IIncommingMessage message)
        {
            var data = new Dictionary<string, string>().FromBytes(message.AsBytes());

            if (!data.ContainsKey("code") || !data.ContainsKey("password") || !data.ContainsKey("email"))
            {
                message.Respond("Invalid request", AckResponseStatus.Unauthorized);
                return;
            }

            var resetData = Database.GetPasswordResetData(data["email"]);

            if (resetData == null || resetData.Code == null || resetData.Code != data["code"])
            {
                message.Respond("Invalid code provided", AckResponseStatus.Unauthorized);
                return;
            }

            var account = Database.GetAccountByEmail(data["email"]);

            // Delete (overwrite) code used
            Database.SavePasswordResetCode(account, null);

            account.Password = PasswordHash.CreateHash(data["password"]);
            Database.UpdateAccount(account);

            message.Respond(AckResponseStatus.Success);
        }

        protected virtual void HandleCodeRequest(IIncommingMessage message)
        {
#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
            var email = message.AsString();
            var account = Database.GetAccountByEmail(email);

            if (account == null)
            {
                message.Respond("No such e-mail in the system", AckResponseStatus.Unauthorized);
                return;
            }

            var code = BmHelper.CreateRandomString(4);

            Database.SavePasswordResetCode(account, code);

            if (!SendMail(account.Email, "Password Reset Code", string.Format(PasswordResetCode, code),
                MailPriority.High))
            {
                message.Respond("Couldn't send an activation code to your e-mail");
                return;
            }

            message.Respond(AckResponseStatus.Success);

#endif
        }
#endregion
    }
}