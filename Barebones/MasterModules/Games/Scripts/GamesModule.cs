using System;
using System.Collections.Generic;
using System.Linq;
using Barebones.Logging;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Master server's module, which handles game server connections,
    ///     registering game servers, retrieving lists of available games,
    ///     and acquiring access'es to game servers
    /// </summary>
    public partial class GamesModule : MasterModule, IGamesListProvider
    {
        /// <summary>
        ///     Authentication module reference
        /// </summary>
        private AuthModule _auth;

        /// <summary>
        ///     Represents next game id
        /// </summary>
        private int _gameIdGenerator;

        /// <summary>
        ///     Master server reference
        /// </summary>
        private IMaster _master;

        /// <summary>
        ///     All the handlers that handle messages from game servers
        /// </summary>
        protected Dictionary<int, IPacketHandler> GameServerHandlers;

        /// <summary>
        ///     Collection of all game server peers
        /// </summary>
        protected Dictionary<int, IPeer> GameServerPeers;

        /// <summary>
        ///     Dictionary of all connected game servers
        /// </summary>
        protected Dictionary<int, IRegisteredGameServer> GameServers;

        /// <summary>
        ///     Port for this module to listen, and to which game servers will connect.
        /// </summary>
        public int GamesPort = 5001;

        /// <summary>
        /// If true, when player requests a list of games, he will receive all of the game properties
        /// </summary>
        [Tooltip("If true, when player requests a list of games, he will receive all of the game properties")]
        public bool SendPlayersAllGameProperties = true;

        public LogLevel LogLevel = LogLevel.Warn;
        public BmLogger Logger = LogManager.GetLogger(typeof(GamesModule).ToString());

        /// <summary>
        ///     Socket, to which game servers connect
        /// </summary>
        protected IServerSocket GamesSocket;

        /// <summary>
        ///     Event, which is invoked when game server registers
        /// </summary>
        public event Action<IRegisteredGameServer> OnGameServerRegistered;

        /// <summary>
        ///     Invoked, when game server is removed
        /// </summary>
        public event Action<IRegisteredGameServer> OnGameRemoved;

        /// <summary>
        /// Invoked, when player joins a game
        /// </summary>
        public event Action<ISession, IRegisteredGameServer> OnPlayerJoinedGame;

        /// <summary>
        /// Invoked, when player leaves a game
        /// </summary>
        public event Action<ISession, IRegisteredGameServer> OnPlayerLeftGame;

        protected virtual void Awake()
        {
            Logger.LogLevel = LogLevel;

            if (BmArgs.StartMaster)
                AnalyseCmdArgs();

            AddDependency<AuthModule>();

            GamesSocket = Connections.CreateServerSocket();
            GamesSocket.OnConnected += OnServerConnected;
            GamesSocket.OnDisconnected += OnServerDisconnected;

            GameServerHandlers = new Dictionary<int, IPacketHandler>();
            GameServers = new Dictionary<int, IRegisteredGameServer>();
            GameServerPeers = new Dictionary<int, IPeer>();
        }

        /// <summary>
        ///     Called by master server, when module should be started
        /// </summary>
        /// <param name="master"></param>
        /// <param name="dependencies"></param>
        public override void Initialize(IMaster master)
        {
            _master = master;
            _auth = master.GetModule<AuthModule>();

            // Adding handlers
            SetGameServerHandler(new PacketHandler(BmOpCodes.PlayerJoinedGame, HandlePlayerJoinedMessage));
            SetGameServerHandler(new PacketHandler(BmOpCodes.PlayerLeftGame, HandlePlayerLeftMessage));
            SetGameServerHandler(new PacketHandler(BmOpCodes.RegisterGameServer, HandleRegisterGameServerMessage));
            SetGameServerHandler(new PacketHandler(BmOpCodes.OpenGameServer, HandleOpenGameMessage));

            master.SetClientHandler(new GameAccessRequestHandler(this));

            GamesSocket.Listen(GamesPort);

            Logger.Info("Games module started");
        }

        /// <summary>
        ///     Adds a game server into the listing, and makes the module
        ///     avare of this new game server
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="data"></param>
        /// <returns>Game Server id</returns>
        public int RegisterGameServer(IPeer peer, RegisterGameServerPacket data)
        {
            var room = CreateGameServerObject(peer, data);

            // Store a game server for quick access later
            peer.SetProperty(BmPropCodes.Game, room);

            // Remove server on disconnect
            Action<IPeer> onDisconnect = null;
            onDisconnect = dcPeer =>
            {
                RemoveGameServer(room);
                room.ServerPeer.OnDisconnect -= onDisconnect;
            };
            room.ServerPeer.OnDisconnect += onDisconnect;

            // Add the server to registry
            AddGameServer(room);

            OnGameServerReady(room);

            if (OnGameServerRegistered != null)
                OnGameServerRegistered.Invoke(room);

            return room.GameId;
        }

        /// <summary>
        ///     Registers a handler that handles messages from game servers
        /// </summary>
        /// <param name="handler"></param>
        public IPacketHandler SetGameServerHandler(IPacketHandler handler)
        {
            GameServerHandlers.Add(handler.OpCode, handler);
            return handler;
        }

        /// <summary>
        ///     Registers a handler that handles messages from game servers
        /// </summary>
        /// <param name="handler"></param>
        public IPacketHandler SetGameServerHandler(short opCode, Action<IIncommingMessage> handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            SetGameServerHandler(handler);
            return handler;
        }

        [Obsolete("Use SetGameServerHandler")]
        public IPacketHandler AddGameServerHandler(IPacketHandler handler)
        {
            SetGameServerHandler(handler);
            return handler;
        }

        /// <summary>
        ///     Adds a game server to the listing
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public bool AddGameServer(IRegisteredGameServer server)
        {
            if (GameServers.ContainsKey(server.GameId))
            {
                Logger.Warn("Tried to add a server with same id: " + server.GameId);
                return false;
            }

            GameServers.Add(server.GameId, server);

            Logger.Info("Game added: " + server.GameId);

            return true;
        }

        /// <summary>
        ///     Removes game server from the listing
        /// </summary>
        /// <param name="server"></param>
        public void RemoveGameServer(IRegisteredGameServer server)
        {
            if (server == null)
                return;

            GameServers.Remove(server.GameId);

            if (OnGameRemoved != null)
                OnGameRemoved.Invoke(server);

            Logger.Info("Game removed: " + server.GameId);
        }

        /// <summary>
        ///     Creates an implementation of IGameServer
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        public virtual IRegisteredGameServer CreateGameServerObject(IPeer peer, RegisterGameServerPacket packet)
        {
            var room = new RegisteredGameServer(CreateGameServerId(), peer, packet);

            if (packet.Properties.ContainsKey(GameProperty.IsPrivate))
            {
                bool isPrivate;
                bool.TryParse(packet.Properties[GameProperty.IsPrivate], out isPrivate);
                room.IsPrivate = isPrivate;
            }

            return room;
        }

        /// <summary>
        ///     Called when game server connects to master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnServerConnected(IPeer peer)
        {
            peer.OnMessage += HandleMessage;

            if (GameServerPeers.ContainsKey(peer.Id))
                return;

            GameServerPeers.Add(peer.Id, peer);

            Logger.Info("Game server connected. Peer id: " + peer.Id);
        }

        /// <summary>
        ///     Called when game server disconnects from master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnServerDisconnected(IPeer peer)
        {
            peer.OnMessage -= HandleMessage;
            GameServerPeers.Remove(peer.Id);

            var game = peer.GetProperty(BmPropCodes.Game) as IRegisteredGameServer;
            if (game != null)
                game.OnDisconnected();

            Logger.Info("Game server disconnected. Peer id: " + peer.Id);
        }

        /// <summary>
        ///     Retrieves game servers list (immutable), but only after cross referencing options
        ///     and peer (user, who requested a list of game servers) data.
        ///     Default implementation does no filtering, so to enable it, you'll have to override this method
        /// </summary>
        /// <param name="peer">Peer, who is trying to retrieve game servers</param>
        /// <param name="options">filtering options</param>
        public virtual IEnumerable<IRegisteredGameServer> GetOpenServers(IPeer peer, Dictionary<string, string> options)
        {
            return GameServers.Values.Where(g => g.IsOpen && !g.IsPrivate);
        }

        /// <summary>
        ///     Iterates through servers, and returns a new collection of open game servers,
        ///     filtered with a provided filtering function
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public virtual IEnumerable<IRegisteredGameServer> GetOpenServers(Func<IRegisteredGameServer, bool> filter)
        {
            var rooms = GameServers.Values;
            return rooms.Where(l => l.IsOpen).Where(filter);
        }

        /// <summary>
        /// Returns all open game servers
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<IRegisteredGameServer> GetOpenServers()
        {
            var rooms = GameServers.Values;
            return rooms.Where(l => l.IsOpen);
        }

        /// <summary>
        ///     Returns all of the registered game servers
        /// </summary>
        /// <returns></returns>
        public virtual List<IRegisteredGameServer> GetRegisteredServers()
        {
            return GameServers.Values.ToList();
        }

        /// <summary>
        ///     Returns game server with specified id, or null if not found
        /// </summary>
        /// <param name="serverId"></param>
        /// <returns></returns>
        public IRegisteredGameServer GetGameServer(int serverId)
        {
            IRegisteredGameServer server;
            GameServers.TryGetValue(serverId, out server);
            return server;
        }

        /// <summary>
        ///     Generates a unique id for the game server
        /// </summary>
        /// <returns></returns>
        public virtual int CreateGameServerId()
        {
            return _gameIdGenerator++;
        }

        /// <summary>
        ///     Called when game server starts
        /// </summary>
        protected virtual void OnGameServerReady(IRegisteredGameServer gameServer)
        {
        }

        /// <summary>
        ///     Creates a <see cref="GameInfoPacket" /> for a given game server.
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public virtual GameInfoPacket CreateGameServerInfo(IRegisteredGameServer server)
        {
            var packet = new GameInfoPacket
            {
                Name = server.Name,
                Address = server.RegistrationPacket.PublicAddress,
                OnlinePlayers = server.OnlinePlayers,
                MaxPlayers = server.MaxPlayers,
                Id = server.GameId,
                IsPasswordProtected = !string.IsNullOrEmpty(server.Password),
                Properties = new Dictionary<string, string>()
            };

            // Add a custom map property
            if (server.RegistrationPacket.Properties.ContainsKey("map"))
                packet.Properties.Add("map", server.RegistrationPacket.Properties["map"]);

            if (SendPlayersAllGameProperties)
            {
                // Copy all of the properties if necessary
                foreach (var pair in server.RegistrationPacket.Properties)
                {
                    packet.Properties[pair.Key] = pair.Value;
                }
            }

            return packet;
        }

        public virtual bool CheckMasterKey(RegisterGameServerPacket data)
        {
            return _master.MasterKey == data.MasterKey;
        }

        protected virtual void AnalyseCmdArgs()
        {
            GamesPort = BmArgs.MasterGamesPort;
        }

        public virtual IEnumerable<GameInfoPacket> GetPublicGames(ISession user, Dictionary<string, string> filters)
        {
            var servers = GetOpenServers(user.Peer, filters);

            return servers.Select(s => CreateGameServerInfo(s));
        }

        #region Message handlers

        /// <summary>
        ///     Handles a message from game server, which notifies master that
        ///     user has left the game
        /// </summary>
        protected virtual void HandlePlayerLeftMessage(IIncommingMessage message)
        {
            var username = message.AsString();

            var game = message.Peer.GetProperty(BmPropCodes.Game) as IRegisteredGameServer;

            var session = game.OnPlayerLeft(username);

            if (OnPlayerJoinedGame != null)
                OnPlayerJoinedGame.Invoke(session, game);

        }

        /// <summary>
        ///     Master Server receives this message from Game Server, when game server
        ///     successfully uses an access code to allow player to join.
        /// </summary>
        protected virtual void HandlePlayerJoinedMessage(IIncommingMessage message)
        {
            var username = message.AsString();

            var playerSession = _auth.GetLoggedInSession(username);

            // Ignore invalid request
            if (playerSession == null)
                return;

            var game = message.Peer.GetProperty(BmPropCodes.Game) as IRegisteredGameServer;

            game.OnPlayerJoined(playerSession);

            if (OnPlayerJoinedGame != null)
                OnPlayerJoinedGame.Invoke(playerSession, game);
        }

        /// <summary>
        ///     Handles a request to open the game server for public.
        ///     This request should come from the game server itself
        /// </summary>
        protected virtual void HandleOpenGameMessage(IIncommingMessage message)
        {
            var game = message.Peer.GetProperty(BmPropCodes.Game) as IRegisteredGameServer;

            if (game == null)
            {
                Logger.Warn("Request to open game server received, but no game server found");
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            game.Open();

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        ///     Handles a message from game server,
        ///     which notifies master that game server is fully setup
        ///     and ready to be accessed by players
        /// </summary>
        protected virtual void HandleRegisterGameServerMessage(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new RegisterGameServerPacket());

            if (!CheckMasterKey(data))
            {
                // Wrong master key
                message.Respond(AckResponseStatus.Unauthorized);
                return;
            }

            var serverId = RegisterGameServer(message.Peer, data);

            // Response indicates to game server that it's visible and accessible to other players
            message.Respond(MessageHelper.Create(message.OpCode, serverId), AckResponseStatus.Success);
        }

        /// <summary>
        ///     Handles a message, received from game server.
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleMessage(IIncommingMessage message)
        {
            try
            {
                GameServerHandlers[message.OpCode].Handle(message);
            }
            catch (Exception e)
            {
                Logger.Error("Error while handling a message from Game server. OpCode: " + message.OpCode);
                Logger.Error(e);

#if UNITY_EDITOR
                throw e;
#else
                if (!message.IsExpectingResponse)
                    return;

                try
                {
                    message.Respond(AckResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
#endif
            }
        }

        #endregion

    }
}