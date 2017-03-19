using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Authentication class, which is used on client for easy
    ///     authentication management
    /// </summary>
    public class Auth
    {
        /// <summary>
        ///     Generic callback for authentication requests
        /// </summary>
        /// <param name="isSuccessful"></param>
        /// <param name="error"></param>
        public delegate void AuthCallback(bool isSuccessful, string error);

        private static bool _isLogginIn;

        public static AuthConfig Config = new AuthConfig();
 
        /// <summary>
        ///     Authenticated users data, saved after loggin in
        /// </summary>
        public static PlayerDataPacket PlayerData { get; private set; }

        public static bool IsLoggedIn
        {
            get { return PlayerData != null; }
        }

        public static bool IsAdmin
        {
            get { return (PlayerData != null) && PlayerData.IsAdmin; }
        }

        public static bool IsGuest
        {
            get { return (PlayerData != null) && PlayerData.IsGuest; }
        }

        /// <summary>
        ///     Event, invoked when user logs in
        /// </summary>
        public static event Action OnLoggedIn;

        /// <summary>
        ///     Event, invoked when user logs out
        /// </summary>
        public static event Action OnLoggedOut;

        /// <summary>
        ///     Sends a generic login request to the Master server
        /// </summary>
        /// <param name="data"></param>
        /// <param name="doneCallback">called after successful or failed login attempt</param>
        public static void LogIn(Dictionary<string, string> data, AuthCallback doneCallback)
        {
            if (_isLogginIn)
            {
                Logs.Error("You're already trying to log in");
                if (doneCallback != null)
                    doneCallback.Invoke(false, "Log in is already in progress");
                return;
            }

            if (IsLoggedIn)
            {
                Logs.Error("You're already logged in");
                if (doneCallback != null)
                    doneCallback.Invoke(false, "Already logged in");
                return;
            }
            var connection = Connections.ClientToMaster;
            _isLogginIn = true;

            // We first need to get an aes key 
            // so that we can encrypt our login data
            BmSecurity.GetAesKey(aesKey =>
            {
                if (aesKey == null)
                {
                    _isLogginIn = false;
                    if (doneCallback != null)
                    {
                        doneCallback.Invoke(false, "Login Failed due to security issues");
                    }
                    return;
                }

                var encryptedData = BmSecurity.EncryptAES(data.ToBytes(), aesKey);

                connection.Peer.SendMessage(BmOpCodes.Login, encryptedData, (status, response) =>
                {
                    _isLogginIn = false;

                    if (status != AckResponseStatus.Success)
                    {
                        if (doneCallback != null)
                            doneCallback.Invoke(false, response.HasData ? response.AsString() : "Login Failed");
                        return;
                    }

                    PlayerData = MessageHelper.Deserialize(response.AsBytes(), new PlayerDataPacket());

                    // Save new token
                    PlayerPrefs.SetString(GetTokenPrefKey(),
                        string.IsNullOrEmpty(PlayerData.Token) ? null : PlayerData.Token);

                    if (doneCallback != null)
                        doneCallback.Invoke(true, null);

                    if (OnLoggedIn != null)
                        OnLoggedIn.Invoke();
                });
            });
        }

        /// <summary>
        ///     Sends a login request with specific credentials
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="doneCallback">called after successful or failed login attempt</param>
        public static void LogIn(string username, string password, AuthCallback doneCallback = null)
        {
            LogIn(new Dictionary<string, string>
            {
                {"username", username},
                {"password", password}
            }, doneCallback);
        }

        /// <summary>
        ///     Tries to login with token
        /// </summary>
        /// <param name="token"></param>
        /// <param name="doneCallback">called after successful or failed login attempt</param>
        public static void LogIn(string token, AuthCallback doneCallback = null)
        {
            LogIn(new Dictionary<string, string>
            {
                {"token", token}
            }, doneCallback);
        }

        /// <summary>
        ///     Tries to log in as guest
        /// </summary>
        /// <param name="doneCallback">called after successful or failed login attempt</param>
        public static void LogInAsGuest(AuthCallback doneCallback = null)
        {
            LogIn(new Dictionary<string, string>
            {
                {"guest", ""}
            }, doneCallback);
        }

        /// <summary>
        /// Requests master server to send a password reset code to a provided e-mail address
        /// </summary>
        /// <param name="email"></param>
        /// <param name="callback"></param>
        public static void RequestPasswordReset(string email, AuthCallback callback )
        {
            var msg = MessageHelper.Create(BmOpCodes.PasswordResetCodeRequest, email.ToBytes());
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.HasData ? response.AsString() : "Password reset failed ");
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to master server, to resend confirmation
        /// code to e-mail
        /// </summary>
        /// <param name="callback"></param>
        public static void RequestEmailConfirmationCode(AuthCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.ConfirmEmailCodeRequest);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString());
                    return;
                }

                callback.Invoke(true, null);
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox);
            });
        }

        /// <summary>
        /// Sends an e-mail confirmation code to master server
        /// </summary>
        /// <param name="code"></param>
        /// <param name="callback"></param>
        public static void ConfirmEmail(string code, AuthCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.ConfirmEmailCodeRequest, code);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString());
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to master server, to change the password
        /// </summary>
        /// <param name="data"></param>
        /// <param name="callback"></param>
        public static void ChangePassword(PasswordChangeData data, AuthCallback callback)
        {
            var dictionary = new Dictionary<string, string>()
            {
                {"email", data.Email },
                {"code", data.Code },
                {"password", data.NewPassword }
            };

            var msg = MessageHelper.Create(BmOpCodes.PasswordChange, dictionary.ToBytes());

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.HasData ? response.AsString() : "Failed to change the password");
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to retrieve a number of players who are currently logged in
        /// </summary>
        /// <param name="callback"></param>
        public static void GetLoggedInCount(Action<int> callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.GetLoggedInCount);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                callback.Invoke(status == AckResponseStatus.Success ? response.AsInt() : -1);
            });
        }

        /// <summary>
        ///     Initiates a log out. In the process, disconnects and connects
        ///     back to the server to ensure no state data is left on the server.
        /// </summary>
        public static void LogOut()
        {
            if (!IsLoggedIn)
                return;

            var connection = Connections.ClientToMaster;

            PlayerData = null;

            DeleteToken();
            if ((connection != null) && connection.IsConnected)
                connection.Reconnect();

            if (OnLoggedOut != null)
                OnLoggedOut.Invoke();
        }

        /// <summary>
        ///     Sends registration data to the Master server
        /// </summary>
        /// <param name="data"></param>
        /// <param name="doneCallback"></param>
        public static void Register(Dictionary<string, string> data, AuthCallback doneCallback)
        {
            if (IsLoggedIn)
            {
                Logs.Error("Cannot register, because already logged in");
                doneCallback.Invoke(false, "Already registere");
                return;
            }

            var connection = Connections.ClientToMaster;

            // We first need to get an aes key 
            // so that we can encrypt our login data
            BmSecurity.GetAesKey(aesKey =>
            {
                if (aesKey == null)
                {
                    if (doneCallback != null)
                    {
                        doneCallback.Invoke(false, "Login Failed due to security issues");
                    }
                    return;
                }

                // Encrypt data
                var encryptedData = BmSecurity.EncryptAES(data.ToBytes(), aesKey);

                connection.Peer.SendMessage(BmOpCodes.Register, encryptedData, (status, response) =>
                {
                    if (status != AckResponseStatus.Success)
                    {
                        var errorMessage = response.HasData ? response.AsString() : "Unknown Error";
                        Logs.Error("Registration failed: " + errorMessage);

                        if (doneCallback != null)
                            doneCallback.Invoke(false, errorMessage);

                        return;
                    }

                    if (doneCallback != null)
                        doneCallback.Invoke(true, null);
                });
            });
        }

        public static void DeleteToken()
        {
            PlayerPrefs.DeleteKey(GetTokenPrefKey());
        }

        private static string GetTokenPrefKey()
        {
            return Config.AuthTokenPrefKey;
        }

        public class AuthConfig
        {
            public string AuthTokenPrefKey = "bbm.auth.token";
            public Connections.ConnectionId ConnectionId = Connections.ConnectionId.ClientToMaster;
        }

        public class PasswordChangeData
        {
            public string Email;
            public string Code;
            public string NewPassword;
        }
    }
}