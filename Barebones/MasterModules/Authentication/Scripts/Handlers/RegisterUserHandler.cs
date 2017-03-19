using System;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Handles a registration request from user
    /// </summary>
    public class RegisterUserHandler : IPacketHandler
    {
        private readonly AuthModule _auth;

        public RegisterUserHandler(AuthModule auth)
        {
            _auth = auth;
        }

        public short OpCode
        {
            get { return BmOpCodes.Register; }
        }

        public void Handle(IIncommingMessage message)
        {
            var encryptedData = message.AsBytes();
            var aesKey = message.Peer.GetProperty(BmPropCodes.AesKey) as string;

            if (aesKey == null)
            {
                // There's no aesKey that client and master agreed upon
                message.Respond("Insecure request".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            var decrypted = BmSecurity.DecryptAES(encryptedData, aesKey);
            var data = new Dictionary<string, string>().FromBytes(decrypted);

            if (!data.ContainsKey("username") || !data.ContainsKey("password") || !data.ContainsKey("email"))
            {
                message.Respond("Invalid registration request".ToBytes(), AckResponseStatus.Error);
                return;
            }

            var username = data["username"];
            var password = data["password"];
            var email = data["email"].ToLower();

            var usernameLower = username.ToLower();

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if ((session == null) || ((session.Account != null) && !session.Account.IsGuest))
            {
                // Fail, if session is invalid, or user is already logged in
                message.Respond("Invalid registration request".ToBytes(), AckResponseStatus.Error);
                return;
            }

            if (!IsUsernameValid(usernameLower))
            {
                message.Respond("Invalid Username".ToBytes(), AckResponseStatus.Error);
                return;
            }

            if (_auth.ModuleConfig.ForbiddenUsernames.Contains(usernameLower))
            {
                // Check if uses forbidden username
                message.Respond("Forbidden word used in username".ToBytes(), AckResponseStatus.Error);
                return;
            }

            if (_auth.ModuleConfig.ForbiddenWordsInUsernames.FirstOrDefault(usernameLower.Contains) != null)
            {
                // Check if there's a forbidden word in username
                message.Respond("Forbidden word used in username".ToBytes(), AckResponseStatus.Error);
                return;
            }

            if ((username.Length < _auth.ModuleConfig.UsernameMinChars) ||
                (username.Length > _auth.ModuleConfig.UsernameMaxChars))
            {
                // Check if username length is good
                message.Respond("Invalid usernanme length".ToBytes(), AckResponseStatus.Error);

                return;
            }

            if (!ValidateEmail(email))
            {
                // Check if email is valid
                message.Respond("Invalid Email".ToBytes(), AckResponseStatus.Error);
                return;
            }

            var account = _auth.Database.CreateAccountObject();

            account.Username = username;
            account.Email = email;
            account.Password = PasswordHash.CreateHash(password);

            try
            {
                _auth.Database.InsertNewAccount(account);

                _auth.FinalizeRegistration(account, session);

                message.Respond(MessageHelper.Create(OpCode), AckResponseStatus.Success);
            }
            catch (Exception e)
            {
                Logs.Error(e);
                message.Respond("Username or E-mail is already registered".ToBytes(), AckResponseStatus.Error);
            }
        }

        private bool IsUsernameValid(string username)
        {
            return !string.IsNullOrEmpty(username) && // If username is empty
                   username == username.Replace(" ", ""); // If username contains spaces
        }

        private bool ValidateEmail(string email)
        {
            return !string.IsNullOrEmpty(email)
                   && email.Contains("@")
                   && email.Contains(".");
        }
    }
}