using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents a basic unity behaviour, which handles lobby 
    /// messages from the master server, and invokes virtual methods,
    /// that can be overriden.
    /// Useful for developing UI for lobby
    /// </summary>
    public class LobbyListener : MonoBehaviour
    {
        /// <summary>
        /// Represents raw lobby data, which was received
        /// from the master server
        /// </summary>
        public LobbyDataPacket RawData;

        /// <summary>
        /// A list of handlers, added to the connection.
        /// This list is used to remove the handlers once the behaviour is
        /// no longer active
        /// </summary>
        protected List<IPacketHandler> AddedHandlers;

        /// <summary>
        /// Current state of the lobby
        /// </summary>
        protected LobbyState State;

        /// <summary>
        /// True, if current user is the game
        /// master
        /// </summary>
        public bool IsGameMaster { get; protected set; }

        /// <summary>
        /// Username of the current user
        /// </summary>
        public string CurrentUser { get; protected set; }

        protected virtual void Awake()
        {
            RegisterHandlers();
        }

        /// <summary>
        /// Adds a packet handler to the connection.
        /// This handler will be removed when object is destroyed
        /// </summary>
        /// <param name="handler"></param>
        protected virtual void AddHandler(IPacketHandler handler)
        {
            Connections.ClientToMaster.SetHandler(handler);
            AddedHandlers.Add(handler);
        }

        /// <summary>
        /// Registers all the necessary packet handlers
        /// </summary>
        protected virtual void RegisterHandlers()
        {
            AddedHandlers = AddedHandlers ?? new List<IPacketHandler>();

            AddHandler(new PacketHandler(BmOpCodes.LobbyMemberPropertySet, HandlePlayerPropChange));
            AddHandler(new PacketHandler(BmOpCodes.LobbySetReady, HandlePlayerReadyChange));
            AddHandler(new PacketHandler(BmOpCodes.LobbyPropertySet, HandleLobbyPropertyChange));
            AddHandler(new PacketHandler(BmOpCodes.LobbyStateChange, HandleLobbyStateChange));
            AddHandler(new PacketHandler(BmOpCodes.LobbyJoin, HandlePlayerJoined));
            AddHandler(new PacketHandler(BmOpCodes.LobbyLeave, HandlePlayerLeft));
            AddHandler(new PacketHandler(BmOpCodes.LobbyMasterChange, HandleGameMasterChanged));
            AddHandler(new PacketHandler(BmOpCodes.LobbyChatMessage, HandleChatMessage));
            AddHandler(new PacketHandler(BmOpCodes.LobbyJoinTeam, HandleJoinTeam));
            AddHandler(new PacketHandler(BmOpCodes.LobbyStatusTextChange, HandleStatusTextChange));
        }

        /// <summary>
        /// Uses the data provided to setup the listener.
        /// </summary>
        /// <param name="data"></param>
        protected virtual void Setup(LobbyDataPacket data)
        {
            RawData = data;
            CurrentUser = Auth.PlayerData.Username;
            IsGameMaster = data.GameMaster == CurrentUser;

            RegisterHandlers();
        }

        protected virtual void OnEnable()
        {
            LobbiesModule.GetLobbyInfo((info, error) =>
            {
                if (info == null)
                {
                    DialogBoxView.ShowError(error);
                    gameObject.SetActive(false);
                    return;
                }

                Setup(info);
            });
        }

        #region Virtual methods

        /// <summary>
        /// Invoked, when lobby property is changed in the Master server
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected virtual void OnLobbyPropertyChange(string key, string value)
        {
        }

        /// <summary>
        /// Invoked, when player property is changed in the master server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected virtual void OnPlayerPropertyChange(string username, string key, string value)
        {
        }

        /// <summary>
        /// Invoked, when play changes it's ready'ness status
        /// </summary>
        /// <param name="username"></param>
        /// <param name="isReady"></param>
        protected virtual void OnPlayerReadyStatusChange(string username, bool isReady)
        {
        }

        /// <summary>
        /// Invoked, when player switches teams
        /// </summary>
        /// <param name="username"></param>
        /// <param name="teamName"></param>
        protected virtual void OnPlayerSwitchedTeams(string username, string teamName)
        {
        }

        /// <summary>
        /// Invoked, when game master has changed
        /// </summary>
        /// <param name="masterUsername"></param>
        /// <param name="isCurrentUserMaster"></param>
        protected virtual void OnGameMasterChanged(string masterUsername, bool isCurrentUserMaster)
        {
        }

        /// <summary>
        /// Invoked, when lobby state changes
        /// </summary>
        /// <param name="state"></param>
        protected virtual void OnLobbyStateChange(LobbyState state)
        {
        }

        /// <summary>
        /// Invoked, when new player joins the lobby
        /// </summary>
        /// <param name="data"></param>
        protected virtual void OnPlayerJoined(LobbyMemberData data)
        {
        }

        /// <summary>
        /// Invoked, when player leaves the lobby
        /// </summary>
        /// <param name="username"></param>
        protected virtual void OnPlayerLeft(string username)
        {
        }

        /// <summary>
        /// Invoked, when a new chat message is received
        /// </summary>
        /// <param name="message"></param>
        protected virtual void OnChatMessageReceived(LobbyChatPacket message)
        {
        }

        /// <summary>
        /// Invoked, when lobby status text changes
        /// </summary>
        /// <param name="text"></param>
        public virtual void OnStatusTextChange(string text)
        {
        }
        
        /// <summary>
        /// Standard unity event, called when object is destroyed
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (AddedHandlers != null && AddedHandlers.Count > 0)
            {
                foreach (var handler in AddedHandlers)
                {
                    Connections.ClientToMaster.RemoveHandler(handler);
                }
            }
        }

        #endregion

        #region Message handlers

        public virtual void HandleLobbyPropertyChange(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new StringPairPacket());

            if (RawData != null)
                RawData.LobbyProperties[data.A] = data.B;

            OnLobbyPropertyChange(data.A, data.B);
        }

        public virtual void HandlePlayerPropChange(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new LobbyMemberPropChangePacket());

            OnPlayerPropertyChange(data.Username, data.Property, data.Value);
        }

        public virtual void HandlePlayerReadyChange(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new StringPairPacket());

            OnPlayerReadyStatusChange(data.A, bool.Parse(data.B));
        }

        public virtual void HandleLobbyStateChange(IIncommingMessage message)
        {
            var stateIndex = message.AsInt();

            OnLobbyStateChange((LobbyState)stateIndex);
        }

        public virtual void HandlePlayerJoined(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new LobbyMemberData());
            OnPlayerJoined(data);
        }

        public virtual void HandlePlayerLeft(IIncommingMessage message)
        {
            var username = message.AsString();

            OnPlayerLeft(username);
        }

        protected virtual void HandleChatMessage(IIncommingMessage message)
        {
            var msg = message.DeserializePacket(new LobbyChatPacket());

            OnChatMessageReceived(msg);
        }

        protected virtual void HandleJoinTeam(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new StringPairPacket());

            OnPlayerSwitchedTeams(data.A, data.B);
        }

        public virtual void HandleGameMasterChanged(IIncommingMessage message)
        {
            var username = message.AsString();

            IsGameMaster = CurrentUser == username;

            OnGameMasterChanged(username, IsGameMaster);
        }

        public virtual void HandleStatusTextChange(IIncommingMessage message)
        {
            var text = message.AsString();

            OnStatusTextChange(text);
        }

        #endregion

    }
}