using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Manages general game room stuff, like
    ///     creating and claiming passes for users and etc
    /// </summary>
    public class RegisteredGame
    {
        public readonly RegisterGameServerPacket RegisterPacket;

        /// <summary>
        /// Time, after which game server will try sending profile 
        /// updates to master server
        /// </summary>
        public float ProfileUpdatesInterval = 0.1f;

        // State
        protected HashSet<string> ConnectedUsers;
        protected bool IsJoinDisabled;
        
        public float PassTimeoutSeconds = 10;
        protected Dictionary<string, GameAccessRequestPacket> Permissions;

        public IGameServer Server;

        private Dictionary<string, ObservableProfile> _profiles;
        private Dictionary<string, ObservableProfile> _dirtyProfiles;

        public RegisteredGame(int gameId, IClientSocket connection, RegisterGameServerPacket registerPacket,
            IGameServer server)
        {
            _dirtyProfiles = new Dictionary<string, ObservableProfile>();
            _profiles = new Dictionary<string, ObservableProfile>();

            Server = server;
            GameId = gameId;

            PublicAddress = registerPacket.PublicAddress;
            MaxPlayers = registerPacket.MaxPlayers;
            Name = registerPacket.Name;
            Password = registerPacket.Password;

            Connection = connection;
            RegisterPacket = registerPacket;
            ConnectedUsers = new HashSet<string>();
            Permissions = new Dictionary<string, GameAccessRequestPacket>();

            Server.OnRegisteredToMaster(connection, this);

            // Add handlers
            Connection.SetHandler(new PacketHandler(BmOpCodes.AccessRequest, HandleAccessRequestPacket));
            Connection.SetHandler(new PacketHandler(BmOpCodes.DisconnectPlayer, HandlePlayerDisconnectPacket));
            Connection.SetHandler(new PacketHandler(BmOpCodes.KillProcess, HandleDestroyGameServerPacket));
        }

        public void Initialize()
        {
            // Start profile updater
            BTimer.Instance.StartCoroutine(ProfileUpdateCoroutine());
        }

        // Game Server Info
        public string Name { get; private set; }
        public int GameId { get; private set; }
        public string PublicAddress { get; private set; }
        public int MaxPlayers { get; private set; }
        public string Password { get; private set; }

        public bool IsOpened { get; protected set; }
        public bool IsStopped { get; protected set; }
        public IClientSocket Connection { get; protected set; }

        public int OnlineUsersCount
        {
            get { return ConnectedUsers.Count; }
        }

        public event Action<RegisteredGame> OnOpened;

        public event Action<string> OnPlayerAdded;
        public event Action<string> OnPlayerRemoved;

        /// <summary>
        ///     Should be called, when player joins the game server.
        /// </summary>
        /// <param name="username"></param>
        public void AddConnectedUser(string username)
        {
            OnConnectedUserAdded(username);

            if (OnPlayerAdded != null)
                OnPlayerAdded.Invoke(username);
        }

        /// <summary>
        ///     Should be called, when player leaves a game server
        /// </summary>
        /// <param name="username"></param>
        public void RemoveConnectedUser(string username)
        {
            OnConnectedUserRemoved(username);

            if (OnPlayerRemoved != null)
                OnPlayerRemoved.Invoke(username);
        }

        /// <summary>
        ///     Tries to create a new pass
        /// </summary>
        /// <param name="request">null, if access could not be created</param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public virtual GameAccessPacket TryCreateAccess(GameAccessRequestPacket request, out string errorMessage)
        {
            errorMessage = "Unknown error";

            if (IsJoinDisabled)
            {
                errorMessage = "Joining is disabled";
                return null;
            }

            // 1. Check if we have a free slot for player
            if (Permissions.Count + ConnectedUsers.Count >= MaxPlayers)
            {
                errorMessage = "Game room is full";
                return null;
            }

            // 2. Check if already in game
            if (ConnectedUsers.Contains(request.Username))
            {
                errorMessage = "You're already in the game";
                return null;
            }

            string error;
            if (!Server.ValidateAccessRequest(request, out error))
            {
                errorMessage = error != null ? error : "Game server refused request";
                return null;
            }

            // 4. Generate a pass
            var access = Guid.NewGuid().ToString();
            Permissions.Add(access, request);

            // 5. Start access expiration
            StartAccessTimeout(access);

            // 6. Return access data
            var accessData = new GameAccessPacket
            {
                AccessToken = access,
                Address = PublicAddress,
                SceneName = Server.GetClientConnectionScene(),
                Properties = new Dictionary<string, string>()
            };

            Server.FillAccessProperties(request, accessData);

            errorMessage = null;

            return accessData;
        }

        /// <summary>
        ///     After timeout, removes pass from list of permissions
        /// </summary>
        /// <param name="passKey"></param>
        /// <returns></returns>
        protected void StartAccessTimeout(string passKey)
        {
            BTimer.AfterSeconds(PassTimeoutSeconds, () =>
            {
                if (Permissions.ContainsKey(passKey))
                    Permissions.Remove(passKey);
            });
        }

        /// <summary>
        ///     Checks if this pass was actually granted to someone,
        ///     and if the timeout was not reached
        /// </summary>
        /// <param name="passKey"></param>
        /// <returns></returns>
        public virtual GameAccessRequestPacket TryClaimAccess(string passKey)
        {
            if (!Permissions.ContainsKey(passKey))
                return null;

            var userRequest = Permissions[passKey];
            Permissions.Remove(passKey);

            return userRequest;
        }

        /// <summary>
        ///     Force disconnects a player from server
        /// </summary>
        /// <param name="username"></param>
        public virtual void DisconnectPlayer(string username)
        {
            Server.DisconnectPlayer(username);
        }

        /// <summary>
        ///     Disconnects all connected players one by one, and invokes a callback when its done
        ///     or after timed out
        /// </summary>
        /// <param name="doneCallback"></param>
        /// <param name="timeoutSecs"></param>
        public virtual void DisconnectAllPlayers(Action doneCallback, float timeoutSecs = 10)
        {
            var users = ConnectedUsers.ToList();
            foreach (var user in users)
                DisconnectPlayer(user);

            // Wait until all players are disconnected
            BTimer.WaitWhile(HasConnectedUsers, successful =>
            {
                if (!successful)
                    Logs.Error("Some of the players failed to disconnect in time: " +
                                   string.Join(",", ConnectedUsers.ToArray()));
                doneCallback.Invoke();
            }, timeoutSecs);
        }

        /// <summary>
        ///     Called on server, when user provides a valid pass
        ///     Notifies master server about new user
        /// </summary>
        protected virtual void OnConnectedUserAdded(string username)
        {
            ConnectedUsers.Add(username);
            NotifyMasterAboutConnectedUser(username);
        }

        /// <summary>
        ///     Called on server, when connected user is removed
        ///     Notifies master server about removed user
        /// </summary>
        protected virtual void OnConnectedUserRemoved(string username)
        {
            ConnectedUsers.Remove(username);
            NotifyMasterAboutDisconnectedUser(username);

            if (_dirtyProfiles.ContainsKey(username))
            {
                // If user who just left had a dirty profile (^_^)
                SendProfileUpdatesToMaster();
            }

            // Remove profile
            _profiles.Remove(username);
        }

        /// <summary>
        ///     Returns true, if there's more than one player
        /// </summary>
        /// <returns></returns>
        public bool HasConnectedUsers()
        {
            return ConnectedUsers.Count > 0;
        }

        /// <summary>
        ///     Returns a list of connected users (immutable)
        /// </summary>
        /// <returns></returns>
        public List<string> GetConnectedUsers()
        {
            return ConnectedUsers.ToList();
        }

        protected virtual IEnumerator ProfileUpdateCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(ProfileUpdatesInterval);

                SendProfileUpdatesToMaster();
            }
        }

        public void GetProfile(string username, Action<ObservableProfile> callback)
        {
            ProfilesModule.GetProfile(username, Connection, profile =>
            {
                if (profile == null)
                {
                    callback.Invoke(null);
                    return;
                }

                // Save the profile
                if (!_profiles.ContainsKey(username))
                {
                    _profiles.Add(username, profile);
                }
                else
                {
                    _profiles[username] = profile;
                }

                profile.OnChanged += OnProfileChanged;

                callback.Invoke(profile);
            });
        }

        private void OnProfileChanged(ObservableProfile profile)
        {
            if (!_dirtyProfiles.ContainsKey(profile.Username))
            {
                _dirtyProfiles.Add(profile.Username, profile);
            }
        }

        protected void SendProfileUpdatesToMaster()
        {
            // Ignore, if profiles didn't change
            if (_dirtyProfiles.Count == 0)
                return;

            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    var profiles = _dirtyProfiles.Values;

                    // Write profiles count
                    writer.Write(profiles.Count);

                    foreach (var profile in profiles)
                    {
                        // Write username
                        writer.Write(profile.Username);

                        var updates = profile.GetUpdates();

                        // Write updates length
                        writer.Write(updates.Length);

                        // Write updates
                        writer.Write(updates);
                    }
                    _dirtyProfiles.Clear();
                }

                Connection.Peer.SendMessage(MessageHelper.Create(BmOpCodes.ProfileUpdate, ms.ToArray()),
                    DeliveryMethod.ReliableSequenced);
            }
        }

        private void NotifyMasterAboutConnectedUser(string username)
        {
            var approvalMsg = MessageHelper.Create(BmOpCodes.PlayerJoinedGame, username);
            Connection.Peer.SendMessage(approvalMsg, DeliveryMethod.ReliableSequenced);
        }

        private void NotifyMasterAboutDisconnectedUser(string username)
        {
            var approvalMsg = MessageHelper.Create(BmOpCodes.PlayerLeftGame, username);
            Connection.Peer.SendMessage(approvalMsg, DeliveryMethod.ReliableSequenced);
        }

        /// <summary>
        ///     Sends a request to master, to set this game server as open.
        ///     Players cannot join games that are not open
        /// </summary>
        /// <param name="callback"></param>
        public void Open(Action<bool> callback = null)
        {
            if (IsOpened)
            {
                Logs.Error("Game server is already opened");
                if (callback != null)
                    callback.Invoke(IsOpened);
                return;
            }

            var msg = MessageHelper.Create(BmOpCodes.OpenGameServer);

            Connection.Peer.SendMessage(msg, (status, response) =>
            {
                if (status == AckResponseStatus.Success)
                {
                    IsOpened = true;

                    // Invoke static event
                    if (OnOpened != null)
                        OnOpened.Invoke(this);

                    Server.OnOpenedToPublic();
                }

                if (callback != null)
                    callback.Invoke(IsOpened);
            });
        }

        /// <summary>
        ///     Stops users from using or getting new passes
        /// </summary>
        public void DisableJoining()
        {
            IsJoinDisabled = true;
        }

        protected virtual void HandleAccessRequestPacket(IIncommingMessage message)
        {
            var data = MessageHelper.Deserialize(message.AsBytes(), new GameAccessRequestPacket());
            string errorMessage;
            var passData = TryCreateAccess(data, out errorMessage);

            if (passData == null)
            {
                message.Respond(errorMessage.ToBytes(), AckResponseStatus.Failed);
                Logs.Debug("Couldn't retrieve a pass for user: " + data.Username);
                return;
            }

            message.Respond(passData.ToBytes(), AckResponseStatus.Success);
        }

        protected virtual void HandlePlayerDisconnectPacket(IIncommingMessage message)
        {
            DisconnectPlayer(message.AsString());
        }

        protected virtual void HandleDestroyGameServerPacket(IIncommingMessage message)
        {
            Server.Shutdown();
        }

        /// <summary>
        ///     Sets the state to stopped and disconnects from master server
        /// </summary>
        public void Unregister()
        {
            IsJoinDisabled = true;
            IsStopped = true;
            Connection.Disconnect();
        }
        
    }
}