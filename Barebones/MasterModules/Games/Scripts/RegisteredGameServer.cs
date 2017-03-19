using System;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This represents state of Game Server within Master
    /// </summary>
    public class RegisteredGameServer : IRegisteredGameServer
    {
        protected Dictionary<string, ISession> ConnectedSessions;

        public RegisteredGameServer(int gameId, IPeer serverPeer, RegisterGameServerPacket packet)
        {
            ConnectedSessions = new Dictionary<string, ISession>();
            GameId = gameId;
            Name = packet.Name;
            Password = packet.Password;
            RegistrationPacket = packet;
            ServerPeer = serverPeer;
            MaxPlayers = packet.MaxPlayers;

            // If peer has no spawn id, this is a manual server
            if (serverPeer.GetProperty(BmPropCodes.SpawnId) == null)
                IsManual = true;
        }

        public bool IsManual { get; private set; }

        /// <summary>
        ///     RegistrationPacket, that was used to register game server to master server
        /// </summary>
        public RegisterGameServerPacket RegistrationPacket { get; private set; }

        /// <summary>
        ///     Unique game identifier
        /// </summary>
        public int GameId { get; private set; }

        /// <summary>
        ///     Number of players who play in this game server
        /// </summary>
        public int OnlinePlayers
        {
            get { return ConnectedSessions.Count; }
        }

        /// <summary>
        ///     Name of the game
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        ///     Connection to game server
        /// </summary>
        public IPeer ServerPeer { get; private set; }

        /// <summary>
        ///     Maximum number of players who are allowed to play in this server
        /// </summary>
        public int MaxPlayers { get; set; }

        /// <summary>
        ///     If true, this game will not appear in regular listings
        /// </summary>
        public bool IsPrivate { get; set; }

        /// <summary>
        ///     If false, players are not allowed to get in with regular request
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        ///     Properties of the game
        /// </summary>
        public Dictionary<string, string> Properties
        {
            get { return RegistrationPacket.Properties; }
        }

        /// <summary>
        ///     Password of the game
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        ///     Event, that is invoked after game server opens
        /// </summary>
        public event Action OnOpened;

        /// <summary>
        /// Event, which is invoked when game server disconnects from master
        /// </summary>
        public event Action<IRegisteredGameServer> Disconnected;

        /// <summary>
        /// Event, invoked when player joined the game
        /// </summary>
        public event Action<ISession> PlayerJoined;

        /// <summary>
        /// Event, invoked when player left the game
        /// </summary>
        public event Action<ISession> PlayerLeft;

        /// <summary>
        ///     Called when game server notifies master that new player has joined
        ///     (used a pass successfully)
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public virtual bool OnPlayerJoined(ISession session)
        {
            if (ConnectedSessions.ContainsKey(session.Username))
                return false;

            ConnectedSessions.Add(session.Username, session);

            session.Peer.SetProperty(BmPropCodes.Game, this);

            session.OnDisconnect += OnPlayerDisconnectedFromMaster;

            if (PlayerJoined != null)
                PlayerJoined.Invoke(session);

            return true;
        }

        /// <summary>
        ///     Called when game server notifies master that a player has left
        /// </summary>
        public virtual ISession OnPlayerLeft(string username)
        {
            ISession session;
            ConnectedSessions.TryGetValue(username, out session);

            if (session == null)
                return null;

            ConnectedSessions.Remove(username);

            session.Peer.SetProperty(BmPropCodes.Game, null);

            if (PlayerLeft != null)
                PlayerLeft.Invoke(session);

            return session;
        }

        /// <summary>
        ///     Sends a pass request to game server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="successCallback"></param>
        /// <param name="failureCallback"></param>
        public virtual void RequestPlayerAccess(ISession session, AccessRequestCallback callback)
        {
            RequestPlayerAccess(session, null, callback);
        }

        /// <summary>
        ///     Sends a pass request to game server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="successCallback"></param>
        /// <param name="failureCallback"></param>
        public virtual void RequestPlayerAccess(ISession session, Dictionary<string, string> additionalInfo,
            AccessRequestCallback callback)
        {

            // Invalid request
            if ((session == null) || (session.Username == null))
            {
                callback.Invoke(null, "Not Authorized");
                return;
            }

            var requestData = new GameAccessRequestPacket
            {
                Username = session.Username,
                SessionId = session.Id,
                AdditionalData = additionalInfo
            };

            try
            {
                var msg = MessageHelper.Create(BmOpCodes.AccessRequest, requestData.ToBytes());

                ServerPeer.SendMessage(msg, (status, message) =>
                {
                    if (status != AckResponseStatus.Success)
                    {
                        callback.Invoke(null, message.AsString());
                        return;
                    }

                    var passData = MessageHelper.Deserialize(message.AsBytes(), new GameAccessPacket());
                    callback.Invoke(passData, null);
                });
            }
            catch (Exception e)
            {
                Logs.Error(e);
                callback.Invoke(null, "Internal Server Error");
            }
        }

        /// <summary>
        ///     Called when Game Server disconnects from Master
        /// </summary>
        public virtual void OnDisconnected()
        {
            if (Disconnected != null)
                Disconnected.Invoke(this);
        }

        public void Open()
        {
            IsOpen = true;

            if (OnOpened != null)
                OnOpened.Invoke();
        }

        /// <summary>
        ///     Sends a request to game server for it to destroy itself
        /// </summary>
        public virtual void Destroy()
        {
            ServerPeer.SendMessage(MessageHelper.Create(BmOpCodes.KillProcess), DeliveryMethod.Reliable);
        }

        /// <summary>
        ///     Called when player disconnects from master. Default implementation
        ///     sends a request to game server to disconnect a player, if he's still playing
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnPlayerDisconnectedFromMaster(ISession session)
        {
            DisconnectPlayer(session);
        }

        /// <summary>
        ///     Notifies game server that player needs to be disconnected
        /// </summary>
        /// <param name="session"></param>
        public virtual void DisconnectPlayer(ISession session)
        {
            var msg = MessageHelper.Create(BmOpCodes.DisconnectPlayer, session.Username);
            ServerPeer.SendMessage(msg, DeliveryMethod.Reliable);
        }
    }
}