using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Logging;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Module, which is responsible for enabling lobbies
    /// int the games, and handling lobby-related packets
    /// that are comming to the master server
    /// </summary>
    public partial class LobbiesModule : MasterModule, IGamesListProvider
    {
        public const string LobbyTypePropKey = "type";
        public const string TeamPropKey = "team";

        /// <summary>
        /// Collection of all of the active lobbies
        /// </summary>
        protected Dictionary<int, IGameLobby> Lobbies;

        /// <summary>
        /// Factories, responsible for generating lobbies
        /// </summary>
        protected Dictionary<string, ILobbyFactory> Factories;

        private int _nextLobbyId = 1;

        public LogLevel LogLevel = LogLevel.Warn;
        public BmLogger Logger = LogManager.GetLogger(typeof(LobbiesModule).ToString());

        #region Message texts

        [Header("Message texts")] public string MsgLobbyNotFound = "Lobby was not found";
        public string MsgInvalidLobbyType = "Invalid lobby type";
        public string MsgAlreadyInALobby = "Already in a lobby";
        public string MsgFailedToCreateLobby = "Internal server error while creating a lobby";
        public string MsgNotInALobby = "Not in lobby";
        public string MsgUserNotInLobby = "User is not in the lobby";
        public string MsgInvalidLobbyInfo = "Could not retrieve lobby info";
        public string MsgFailedSetLobbyProperty = "Failed to change lobby properties";
        public string MsgFailedSetPlayerProperty = "Failed to change player properties";
        public string MsgFailedStartingGame = "Failed to start the game";
        public string MsgFailedSwitchTeams = "Failed to switch teams";

        #endregion

        public SpawnersModule SpawnersModule { get; protected set; }
        public GamesModule GamesModule { get; protected set; }

        void Awake()
        {
            Logger.LogLevel = LogLevel;
            Lobbies = new Dictionary<int, IGameLobby>();

            AddDependency<SpawnersModule>();
            AddDependency<GamesModule>();
        }

        public override void Initialize(IMaster master)
        {
            Factories = new Dictionary<string, ILobbyFactory>();

            SpawnersModule = master.GetModule<SpawnersModule>();
            GamesModule = master.GetModule<GamesModule>();

            // Add game server handlers
            GamesModule.SetGameServerHandler(new PacketHandler(BmOpCodes.LobbyAttachToGame, HandleAttachToGameByServer));
            GamesModule.SetGameServerHandler(new PacketHandler(BmOpCodes.LobbyGetPlayerData, HandlePlayerDataRequestByServer));
            GamesModule.SetGameServerHandler(new PacketHandler(BmOpCodes.LobbyMemberPropertySet, HandlePlayerPropSetByServer));

            // Add client handlers
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyJoin, HandleJoinLobbyRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyLeave, HandleLeaveLobbyRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyCreate, HandleCreateLobbyRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyInfo, HandleLobbyInfoRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyPropertySet, HandleLobbyPropertyChanges));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyMemberPropertySet, HandlePlayerPropertyChange));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbySetReady, HandleSetReadyRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyStartGame, HandleStartGameRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyChatMessage, HandleChatMessage));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyJoinTeam, HandleJoinTeam));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyGameAccessRequest, HandleGameAccessRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LobbyIsInLobby, HandleIsInLobby));

            SetupFactories();

            Logger.Info("Lobbies Module initialized");
        }

        /// <summary>
        /// Registers a new factory to the list of available factories
        /// </summary>
        /// <param name="id"></param>
        /// <param name="factory"></param>
        public void RegisterFactory(ILobbyFactory factory)
        {
            Factories.Add(factory.Id, factory);
            factory.OnRegisteredToModule(this);
        }

        /// <summary>
        /// Generates a unique lobby id
        /// </summary>
        /// <returns></returns>
        public int GenerateLobbyId()
        {
            return _nextLobbyId++;
        }

        /// <summary>
        /// Registers the lobby and adds it to the collection
        /// of all "live" lobbies
        /// </summary>
        /// <param name="lobby"></param>
        public void RegisterLobby(IGameLobby lobby)
        {
            if (Lobbies.ContainsKey(lobby.Id))
            {
                Logger.Error("Failed to register lobby to lobbies list - " +
                                   "lobby with same id already exists");
                return;
            }
            Lobbies.Add(lobby.Id, lobby);

            lobby.OnDestroy += OnLobbyDestroyed;
        }

        /// <summary>
        /// Invoked, when lobby is destroyed
        /// </summary>
        /// <param name="lobby"></param>
        private void OnLobbyDestroyed(GameLobby lobby)
        {
            Lobbies.Remove(lobby.Id);
            lobby.OnDestroy -= OnLobbyDestroyed;
        }

        public IEnumerable<IGameLobby> GetLobbies(Func<IGameLobby, bool> condition)
        {
            return Lobbies.Values.Where(condition);
        }


        /// <summary>
        /// Tries to find a factory by the type and create a lobby. If factory type is null,
        /// will select a random factory
        /// </summary>
        /// <param name="factoryType"></param>
        /// <param name="creator"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public IGameLobby CreateLobby(Dictionary<string, string> properties, string factoryType = null)
        {
            ILobbyFactory factory;

            if (factoryType != null)
            {
                Factories.TryGetValue(factoryType, out factory);
            }
            else
            {
                factory = Factories.Values.ElementAt(UnityEngine.Random.Range(0, Factories.Count));
            }

            if (factory == null)
                return null;

            return factory.CreateLobby(properties);
        }

        #region Virtual Methods

        /// <summary>
        /// Method, which registers default factories.
        /// Override it to add yours, or to disable the ones you don't want
        /// </summary>
        protected virtual void SetupFactories()
        {
            // Add factories from anonymous functions
            RegisterFactory(new LobbyFactoryAnonymous("DEATHMATCH", DemoLobbyFactories.Deathmatch));
            RegisterFactory(new LobbyFactoryAnonymous("1 VS 1", DemoLobbyFactories.OneVsOne));
            RegisterFactory(new LobbyFactoryAnonymous("2 VS 2 VS 4", DemoLobbyFactories.TwoVsTwoVsFour));
            RegisterFactory(new LobbyFactoryAnonymous("3 VS 3 AUTO", DemoLobbyFactories.ThreeVsThreeQueue));
        }

        /// <summary>
        /// Generates a game info packet, which is used mainly when displaying
        /// a list of games
        /// </summary>
        /// <param name="session"></param>
        /// <param name="lobby"></param>
        /// <returns></returns>
        protected virtual GameInfoPacket GenerateGameInfo(ISession session, IGameLobby lobby)
        {
            return new GameInfoPacket()
            {
                Id = lobby.Id,
                Address = lobby.ServerAddress,
                IsManual = false,
                IsLobby = true,
                MaxPlayers = lobby.MaxPlayers,
                Name = lobby.Name,
                OnlinePlayers = lobby.PlayerCount,
                Properties = lobby.GetProperties(),
            };
        }

        /// <summary>
        /// Returns the list of available games. Used to
        /// display a list of available games on client
        /// </summary>
        /// <param name="user"></param>
        /// <param name="filters"></param>
        /// <returns></returns>
        public virtual IEnumerable<GameInfoPacket> GetPublicGames(ISession user, Dictionary<string, string> filters)
        {
            var publicLobbies = Lobbies.Values.Where(l => l.ShowInLists);
            return publicLobbies.Select(l => GenerateGameInfo(user, l));
        }

        #endregion

        #region Message Handlers

        /// <summary>
        /// Handles users request to join a lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleJoinLobbyRequest(IIncommingMessage message)
        {
            var lobbyId = message.AsInt();

            IGameLobby lobby;
            Lobbies.TryGetValue(lobbyId, out lobby);

            if (lobby == null)
            {
                message.Respond(MsgLobbyNotFound, AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;
            var error = lobby.AddPlayer(session);

            if (error != null)
            {
                message.Respond(error, AckResponseStatus.Failed);
                return;
            }

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles users request to leave lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleLeaveLobbyRequest(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (lobby != null)
                lobby.RemovePlayer(session);

            message.Respond(AckResponseStatus.Success);
        }
        
        /// <summary>
        /// Handles users request to create a new lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleCreateLobbyRequest(IIncommingMessage message)
        {
            Logger.Debug("Received 'Create Lobby' request");

            if (message.Peer.GetProperty(BmPropCodes.Lobby) != null)
            {
                // If the user who want's to create a lobby is already in another lobby
                message.Respond(MsgAlreadyInALobby, AckResponseStatus.Failed);
                return;
            }

            // Deserialize properties of the lobby
            var properties = new Dictionary<string, string>().FromBytes(message.AsBytes());

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (string.IsNullOrEmpty(session.Username))
            {
                message.Respond("Unauthorized to create lobbies", AckResponseStatus.Failed);
                return;
            }

            string lobbyType;
            properties.TryGetValue(LobbyTypePropKey, out lobbyType);

            if (string.IsNullOrEmpty(lobbyType))
            {
                // Invalid message type
                message.Respond(MsgInvalidLobbyType, AckResponseStatus.Failed);
                return;
            }

            ILobbyFactory factory;
            Factories.TryGetValue(lobbyType, out factory);

            if (factory == null)
            {
                // There's no factory for this type of lobby
                message.Respond(MsgInvalidLobbyType + "(2)", AckResponseStatus.Failed);

                Logger.Error("Couldn't find a factory of type: " + lobbyType);

                return;
            }

            var newLobby = factory.CreateLobby(properties);

            if (newLobby == null)
            {
                message.Respond(MsgFailedToCreateLobby, AckResponseStatus.Error);
                return;
            }

            // Add the lobby to registry
            RegisterLobby(newLobby);

            var lobbyJoinError = newLobby.AddPlayer(session);

            if (!string.IsNullOrEmpty(lobbyJoinError))
            {
                // Lobby created, but player could not be added to it
                Logs.Error("User who created a lobby was not able to join it!" +
                               string.Format(" Lobby type: '{0}',", lobbyType) +
                               string.Format(" Join error: '{0}'", lobbyJoinError));

                message.Respond(MsgFailedToCreateLobby + " (2)", AckResponseStatus.Error);

                return;
            }

            // At this point, the lobby is created and player has been added to it
            Logger.Debug("Lobby created");

            // Respond with lobby id
            var response = MessageHelper.Create(BmOpCodes.LobbyCreate, newLobby.Id);
            message.Respond(response, AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles users request to get information of a lobby he is currently in
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleLobbyInfoRequest(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond(MsgNotInALobby, AckResponseStatus.Failed);
                return;
            }

            var info = lobby.GenerateLobbyData(message.Peer);

            if (info == null)
            {
                message.Respond(MsgInvalidLobbyInfo, AckResponseStatus.Error);

                Logger.Warn("Failed to generate lobby info.");

                return;
            }

            // Send the info packet
            message.Respond(MessageHelper.Create(message.OpCode, info.ToBytes()), AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to change lobby properties
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleLobbyPropertyChanges(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond(MsgNotInALobby, AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;
            var changes = new Dictionary<string, string>().FromBytes(message.AsBytes());

            foreach (var change in changes)
            {
                // Go through each change, until one of them fails.
                if (!lobby.SetLobbyProperty(session, change.Key, change.Value))
                {
                    message.Respond(MsgFailedSetLobbyProperty, AckResponseStatus.Failed);
                    return;
                }
            }
            
            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to change it's properties in the lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandlePlayerPropertyChange(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond(MsgNotInALobby, AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;
            var changes = new Dictionary<string, string>().FromBytes(message.AsBytes());

            foreach (var change in changes)
            {
                // Go through each change, until one of them fails.
                if (!lobby.SetPlayerProperty(session, change.Key, change.Value))
                {
                    message.Respond(MsgFailedSetPlayerProperty, AckResponseStatus.Failed);
                    return;
                }
            }

            message.Respond(AckResponseStatus.Success);
            }

        /// <summary>
        /// Handles user's request to change it's "ready" state
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleSetReadyRequest(IIncommingMessage message)
        {
            var isReady = message.AsInt() > 0;
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (lobby == null)
            {
                message.Respond(MsgNotInALobby, AckResponseStatus.Failed);
                return;
            }

            lobby.SetReadyState(session, isReady);

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to start the game
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleStartGameRequest(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (!lobby.StartGameManually(session))
            {
                message.Respond(MsgFailedStartingGame, AckResponseStatus.Error);
                return;
            }

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's chat message
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleChatMessage(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;
            lobby.HandleChatMessage(message);
        }

        /// <summary>
        /// Handles user's request to join a team
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleJoinTeam(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            var team = message.AsString();

            if (!lobby.TryJoinTeam(team, session))
            {
                message.Respond(MsgFailedSwitchTeams, AckResponseStatus.Failed);
                return;
            }

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to get access to the game
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGameAccessRequest(IIncommingMessage message)
        {
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond(MsgNotInALobby, AckResponseStatus.Failed);
                return;
            }

            // Leave it up to the lobby to handle
            lobby.HandleGameAccessRequest(message);
        }

        /// <summary>
        /// Handles user's request to check if he's actually in a lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleIsInLobby(IIncommingMessage message)
        {
            var isInLobby = message.Peer.GetProperty(BmPropCodes.Lobby) != null;

            message.Respond(isInLobby ? AckResponseStatus.Success : AckResponseStatus.Failed);
        }

        #endregion

        #region Game Server message handlers

        /// <summary>
        /// Handles a request from game server, to attach the lobby to a game server
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleAttachToGameByServer(IIncommingMessage message)
        {
            var lobbyId = message.AsInt();

            IGameLobby lobby;
            Lobbies.TryGetValue(lobbyId, out lobby);

            if (lobby == null)
            {
                message.Respond(MsgLobbyNotFound, AckResponseStatus.Failed);
                return;
            }

            message.Peer.SetProperty(BmPropCodes.Lobby, lobby);

            var lobbyInfo = lobby.GenerateLobbyData(message.Peer);

            message.Respond(lobbyInfo.ToBytes(), AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from game server to retrieve information about a member
        /// of the lobby
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandlePlayerDataRequestByServer(IIncommingMessage message)
        {
            var username = message.AsString();
            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond("Not attached to lobby", AckResponseStatus.Failed);
                return;
            }

            var player = lobby.GetPlayer(username);

            if (player == null)
            {
                message.Respond(MsgUserNotInLobby, AckResponseStatus.Failed);
                return;
            }

            var playerData = player.GeneratePacket();

            message.Respond(playerData.ToBytes(), AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from game server to change player's property
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandlePlayerPropSetByServer(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new LobbyMemberPropChangePacket());

            var lobby = message.Peer.GetProperty(BmPropCodes.Lobby) as GameLobby;

            if (lobby == null)
            {
                message.Respond("Not attached to lobby", AckResponseStatus.Failed);
                return;
            }

            lobby.OverridePlayerProperty(data.Username, data.Property, data.Value);

            message.Respond(AckResponseStatus.Success);
        }

        #endregion

    }
}