using System;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Handles login request from user
    /// </summary>
    public class LoginUserHandler : IPacketHandler
    {
        private readonly AuthModule _auth;

        private int _guestId = 18210;

        public LoginUserHandler(AuthModule auth)
        {
            _auth = auth;
        }

        public short OpCode
        {
            get { return BmOpCodes.Login; }
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
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (session == null)
            {
                message.Respond("Invalid request".ToBytes(), AckResponseStatus.Unauthorized);
                Logs.Warn("Invalid session while trying to login");
                return;
            }

            // ---------------------------------------------
            // Guest Authentication
            if (data.ContainsKey("guest") && _auth.EnableGuestLogin)
            {
                var id = _guestId++;
                var guestAccount = new AccountDataGuest(_auth.GuestPrefix + id);
                _auth.FinalizeLogin(session, guestAccount);

                var guestData = new PlayerDataPacket
                {
                    IsAdmin = false,
                    IsGuest = true,
                    Username = guestAccount.Username
                };

                message.Respond(guestData.ToBytes(), AckResponseStatus.Success);
                return;
            }

            // ----------------------------------------------
            // Token Authentication
            if (data.ContainsKey("token"))
            {
                var tokenAccount = _auth.Database.GetAccountByToken(data["token"]);
                if (tokenAccount == null)
                {
                    message.Respond("Invalid Credentials".ToBytes(), AckResponseStatus.Unauthorized);
                    return;
                }

                var otherSession = _auth.GetLoggedInSession(tokenAccount.Username);
                if (otherSession != null)
                {
                    otherSession.ForceDisconnect();
                    message.Respond("This account is already logged in".ToBytes(),
                        AckResponseStatus.Unauthorized);
                    return;
                }

                var playerData = new PlayerDataPacket
                {
                    IsAdmin = tokenAccount.IsAdmin,
                    IsGuest = false,
                    Username = tokenAccount.Username,
                    Token = data["token"],
                };

                // Success
                _auth.FinalizeLogin(session, tokenAccount);
                message.Respond(playerData.ToBytes(), AckResponseStatus.Success);

                return;
            }

            // ----------------------------------------------
            // Username / RoomPassword authentication

            if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            {
                message.Respond("Invalid Credentials".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            var username = data["username"];
            var password = data["password"];

            var loggedInSession = _auth.GetLoggedInSession(username);

            if (loggedInSession != null)
            {
                loggedInSession.ForceDisconnect();
                message.Respond(MessageHelper.Create(OpCode, "This account is already logged in"),
                    AckResponseStatus.Unauthorized);
                return;
            }

            var account = _auth.Database.GetAccount(username);

            if (account == null)
            {
                // Couldn't find an account with this name
                message.Respond("Invalid Credentials".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            if (!PasswordHash.ValidatePassword(password, account.Password))
            {
                // Password is not correct
                message.Respond("Invalid Credentials".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            // Password is correct. We're in
            _auth.FinalizeLogin(session, account);

            var token = Guid.NewGuid().ToString();

            var accData = new PlayerDataPacket
            {
                IsAdmin = account.IsAdmin,
                IsGuest = false,
                Username = account.Username,
                Token = token,
                IsEmailConfirmed = account.IsEmailConfirmed
            };

            message.Respond(MessageHelper.Create(OpCode, accData.ToBytes()), AckResponseStatus.Success);

            // Save token
            _auth.Database.InsertToken(account, token);
        }
    }
}