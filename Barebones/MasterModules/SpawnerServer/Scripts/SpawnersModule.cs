using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Logging;
using Barebones.MasterServer.Ui;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Module, which handles registrations of spawner servers,
    ///     and transfering user request to appropriate spawner servers
    /// </summary>
    public partial class SpawnersModule : MasterModule
    {
        public delegate GameServerSpawnRequestPacket SpawnRequestFactory(SpawnTask task);

        public delegate SpawnTask SpawnTaskFactory(int taskId, ISession creator,
            SpawnerLink spawner, Dictionary<string, string> properties);

        private int _spawnIdGenerator = 1652;
        private int _spawnerIdGenerator;

        public bool AllowGuestsCreatingRooms = false;
        public int DefaultFpsLimit = 30;
        public string DefaultScene = "MasterTut";
        protected GamesModule GamesModule;

        protected IMaster Master;
        public bool OnlyAdminsCanCreateRooms = false;

        /// <summary>
        ///     All the handlers that handle messages from game servers
        /// </summary>
        protected Dictionary<int, IPacketHandler> SpawnerHandlers;

        /// <summary>
        ///     Collection of all game server peers
        /// </summary>
        protected Dictionary<int, IPeer> SpawnerPeers;

        protected Dictionary<int, SpawnerLink> Spawners;

        /// <summary>
        ///     Port, to which spawner servers will need to connect
        /// </summary>
        public int SpawnersPort = 5002;

        protected IServerSocket SpawnersSocket;
        protected Dictionary<int, SpawnTask> SpawnTasks;

        public LogLevel LogLevel = LogLevel.Warn;
        public BmLogger Logger = LogManager.GetLogger(typeof(SpawnersModule).ToString());

        /// <summary>
        ///     Invoked when spawner successfully registers to master server
        /// </summary>
        public event Action<SpawnerLink> OnSpawnerRegistered;

        /// <summary>
        /// Maximum number of spawn requests that can be queued on a single spawner server.
        /// If queue reaches this number, it will not allow new requests to get into queue.
        /// This is to protect users from waiting too long
        /// </summary>
        public int SpawnerQueueLength = 10;

        [Header("Spawner Inspector")]
        [Tooltip("If true, only admin can request inspector data")]
        public bool OnlyAdminCanViewInspector = false;

        [Tooltip("If true, only admin can kill servers")]
        public bool OnlyAdminCanKillServers = true;

        public virtual void Awake()
        {
            Logger.LogLevel = LogLevel;

            AddDependency<GamesModule>();

            Spawners = new Dictionary<int, SpawnerLink>();
            SpawnTasks = new Dictionary<int, SpawnTask>();
            SpawnerHandlers = new Dictionary<int, IPacketHandler>();
            SpawnerPeers = new Dictionary<int, IPeer>();
            SpawnersSocket = Connections.CreateServerSocket();
        }

        protected virtual void Start()
        {
        }

        /// <summary>
        ///     Called by master server, when module should be started
        /// </summary>
        /// <param name="master"></param>
        /// <param name="dependencies"></param>
        public override void Initialize(IMaster master)
        {
            Master = master;

            GamesModule = master.GetModule<GamesModule>();

            GamesModule.SetGameServerHandler(new PacketHandler(BmOpCodes.UnityProcessStarted, HandleUnityProcessStarted));
            GamesModule.OnGameServerRegistered += OnGameServerRegistered;

            // Handlers for messages from clients
            master.SetClientHandler(BmOpCodes.CreateGameServer, HandleCreateGameRequest);
            master.SetClientHandler(BmOpCodes.AbortSpawn, HandleAbortMessage);
            master.SetClientHandler(BmOpCodes.SpawnerInspectorDataRequest, HandleInspectorDataRequest);
            master.SetClientHandler(BmOpCodes.CreatedGameAccessRequest, HandleCreatedGameAccessRequest);
            master.SetClientHandler(BmOpCodes.KillProcess, HandleKillSpawnedProcess);

            // Handlers for messages from spawners
            SetSpawnerHandler(BmOpCodes.RegisterSpawner, HandleRegisterSpawnerMessage);
            SetSpawnerHandler(BmOpCodes.SpawnerUpdate, HandleSpawnerUpdateMessage);
            SetSpawnerHandler(BmOpCodes.GameProcessCreated, HandleGameProcessCreated);
            SetSpawnerHandler(BmOpCodes.GameProcessKilled, HandleGameProcessKilled);

            SetSpawnerHandler(BmOpCodes.SpawnerGameClosed,
                message => OnGameServerClosed(message.AsInt()));

            SpawnersSocket.OnConnected += OnServerConnected;
            SpawnersSocket.OnDisconnected += OnServerDisconnected;

            SpawnersSocket.Listen(SpawnersPort);

            Logger.Info("Spawners module started");

            if (!OnlyAdminCanKillServers)
                Logger.Warn("You have enabled regular players to kill servers");
        }
        
        /// <summary>
        ///     Called when spawner server connects to master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnServerConnected(IPeer peer)
        {
            peer.OnMessage += HandleMessage;

            if (SpawnerPeers.ContainsKey(peer.Id))
                return;

            SpawnerPeers.Add(peer.Id, peer);

            Logger.Info("Spawner server connected. Peer id: " + peer.Id);
        }

        /// <summary>
        ///     Called when spawner server disconnects from master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnServerDisconnected(IPeer peer)
        {
            peer.OnMessage -= HandleMessage;

            SpawnerPeers.Remove(peer.Id);

            Logger.Info("Spawner server disconnected. Peer id: " + peer.Id);
        }

        /// <summary>
        /// Invoked, when one of the game servers is registered
        /// </summary>
        /// <param name="serverLink"></param>
        protected virtual void OnGameServerRegistered(IRegisteredGameServer serverLink)
        {
            var idObject = serverLink.ServerPeer.GetProperty(BmPropCodes.SpawnId);

            // Ignore, if this game server has no spawn id.
            // It's most likely not spawned by a spawner
            if (idObject == null)
                return;

            var spawnId = (int) idObject;

            SpawnTask task;
            SpawnTasks.TryGetValue(spawnId, out task);

            if (task == null)
            {
                Logger.Warn("Game server is ready, but spawner task no longer exists " +
                           "so we can't notify user about a new server ");
                return;
            }

            task.OnServerRegistered(spawnId, serverLink);
        }

        [Obsolete("Use SetSpawnerHandler")]
        public IPacketHandler AddSpawnerHandler(IPacketHandler handler, bool overrideCurrent = true)
        {
            SetSpawnerHandler(handler, overrideCurrent);
            return handler;
        }

        /// <summary>
        /// Sets a message handler, which handles messages from spawner server
        /// </summary>
        /// <param name="handler"></param>
        /// <param name="overrideCurrent"></param>
        public IPacketHandler SetSpawnerHandler(IPacketHandler handler, bool overrideCurrent = true)
        {
            if (SpawnerHandlers.ContainsKey(handler.OpCode) && overrideCurrent)
            {
                SpawnerHandlers[handler.OpCode] = handler;
                return handler;
            }

            SpawnerHandlers.Add(handler.OpCode, handler);
            return handler;
        }

        /// <summary>
        /// Sets a message handler, which handles messages from spawner server
        /// </summary>
        public IPacketHandler SetSpawnerHandler(short opCode, Action<IIncommingMessage> handlerMethod, bool overrideCurrent = true)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            return SetSpawnerHandler(handler);
        }

        /// <summary>
        ///     Returns true if player is allowed to create a game server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public virtual bool IsAuthorizedToCreateRoom(ISession session, Dictionary<string, string> properties)
        {
            if ((session == null) || (session.Account == null))
                return false;

            if (!session.Account.IsAdmin && !OnlyAdminsCanCreateRooms)
                return false;

            if (session.Account.IsGuest && !AllowGuestsCreatingRooms)
                return false;

            return true;
        }

        /// <summary>
        ///     Finds an appropriate spawner and requests to spawn a game server.
        ///     If any of the servers accept the request, request object will be returned.
        ///     Otherwise - null
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="customArgs">Custom arguments that will be added when spawning a game server</param>
        /// <returns></returns>
        public virtual SpawnTask Spawn(Dictionary<string, string> properties, string customArgs = "")
        {
            Logger.Trace("Looking for an appropriate spawner");

            var spawners = GetSpawners(properties).ToArray();

            if (spawners.Length <= 0)
                return null;

            var ordered = spawners.OrderByDescending(s => s.GetFreeSlotsCount());

            // Get a promise from the least busy spawner
            SpawnTask task = null;
            foreach (var spawnerLink in ordered)
            {
                task = spawnerLink.OrderSpawn(properties, customArgs);
                if (task != null)
                    break;
            }

            if (task != null)
            {
                Logger.Trace("Found an appropriate spawner");
            }

            return task;
        }

        /// <summary>
        ///     Creates a spawner link
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual SpawnerLink CreateLink(IPeer peer, SpawnerRegisterPacket data)
        {
            var link = new SpawnerLink(_spawnerIdGenerator++, data, this, peer);

            return link;
        }

        public virtual IEnumerable<SpawnerLink> GetSpawners()
        {
            return Spawners.Values.ToList();
        }

        /// <summary>
        ///     Default implementation finds spawners in the same region (if provided in properties),
        ///     or all spawners, if no filters applied
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        public virtual IEnumerable<SpawnerLink> GetSpawners(Dictionary<string, string> properties)
        {
            var region = properties.ContainsKey("region") ? properties["region"].ToLower() : null;

            // If region filter was not used
            if (region == null)
                return Spawners.Values;

            // Return all spawners in a given region
            return Spawners.Values
                .Where(s => s.Properties.ContainsKey("region"))
                .Where(s => s.Properties["region"].ToLower() == region);
        }

        public int GenerateSpawnId()
        {
            return _spawnIdGenerator++;
        }

        /// <summary>
        ///     Default implementation of <see cref="SpawnTaskFactory" />. Creates a spawn task.
        ///     If you're using custom extensions of <see cref="SpawnTask" />,
        ///     you probably want to override this method, or change the factory method property <see cref="TaskFactory" />
        /// </summary>
        /// <returns></returns>
        public virtual SpawnTask CreateSpawnTask(int spawnId, SpawnerLink spawner, 
            Dictionary<string, string> properties, string customArgs = "")
        {
            return new SpawnTask(spawnId, spawner, properties, customArgs);
        }

        /// <summary>
        /// Adds a task to dictionary of tasks. When an instance of unity is spawned,
        /// this task will be taken from this dictionary and its 
        /// <see cref="SpawnTask.OnProcessStarted"/> will be called
        /// </summary>
        /// <param name="task"></param>
        public virtual void RegisterSpawnTask(SpawnTask task)
        {
            SpawnTasks.Add(task.SpawnId, task);

            Logger.Trace("Spawn Task registered");
        }

        /// <summary>
        ///     This packet will be sent to spawner server
        ///     If you want to implement some custom logic, you'll need to override this method
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public virtual GameServerSpawnRequestPacket CreateSpawnRequestPacket(SpawnTask task)
        {
            // Get the name of the scene from properties
            var sceneToOpen = task.Properties.ContainsKey(GameProperty.SceneName)
                ? task.Properties[GameProperty.SceneName]
                : null;

            return new GameServerSpawnRequestPacket
            {
                SpawnId = task.SpawnId,
                MasterKey = Master.MasterKey,
                CustomArgs = task.CustomArgs,
                SceneName = sceneToOpen ?? DefaultScene,
                FpsLimit = DefaultFpsLimit
            };
        }

        protected virtual void OnGameServerClosed(int taskId)
        {
            SpawnTask task;
            SpawnTasks.TryGetValue(taskId, out task);

            SpawnTasks.Remove(taskId);

            if (task != null)
                task.Abort("Game closed");
        }

        /// <summary>
        /// Sends requests to all the spawner servers, and retrieves info about
        /// all the game processes they are running
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        private IEnumerator GetProcessesOfAllSpawners(Action<Dictionary<int, List<GameProcessInfoPacket>>> callback)
        {
            var timeoutAt = Time.time + 20;
            var spawners = Spawners.ToDictionary(p => p.Key, p => p.Value);

            var data = new Dictionary<int, List<GameProcessInfoPacket>>();
            var hashSet = new HashSet<int>(spawners.Values.Select(s => s.Id));

            foreach (var spawner in spawners.Values)
            {
                var spawnerRef = spawner;
                spawner.RequestProcessesInfo(list =>
                {
                    data[spawnerRef.Id] = list;
                });
            }

            // While not all data is returned
            while (hashSet.Count != data.Count)
            {
                yield return null;

                if (Time.time > timeoutAt)
                {
                    Logger.Warn("Failed to get all processes of all spawners - Time out");
                    callback.Invoke(data);
                    yield break;
                }
            }

            callback.Invoke(data);
        }

        #region Client message handlers 

        /// <summary>
        ///     Handles users request to abort spawning of game server
        /// </summary>
        protected virtual void HandleAbortMessage(IIncommingMessage message)
        {
            var request = message.Peer.GetProperty(BmPropCodes.SpawnRequest) as SpawnTask;

            if (request != null)
                request.Abort("Aborted by user");

            if (message.IsExpectingResponse)
                message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        ///     Handles an access request from user, who created a room.
        ///     It doesn't check if game is open, so that it would be possible to open the game
        ///     after first player joins
        /// </summary>
        public virtual void HandleCreatedGameAccessRequest(IIncommingMessage message)
        {
            var request = message.Peer.GetProperty(BmPropCodes.SpawnRequest) as SpawnTask;
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (request == null)
            {
                message.Respond("You are not spawning a server".ToBytes(),
                    AckResponseStatus.Failed);
                return;
            }

            if (request.GameServer == null)
            {
                message.Respond("Game server is not ready yet".ToBytes(),
                    AckResponseStatus.Failed);
                return;
            }

            // Send a request to game server, so that it generates a pass
            request.GameServer.RequestPlayerAccess(session, (access, error) =>
            {
                if (access == null)
                {
                    // Failure
                    message.Respond((error ?? "Invalid Request").ToBytes(), AckResponseStatus.Failed);
                    return;
                }

                // Success
                message.Respond(access.ToBytes(), AckResponseStatus.Success);
            });
        }

        /// <summary>
        /// Handles a request from user to create a game server
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleCreateGameRequest(IIncommingMessage message)
        {
            var data = new Dictionary<string, string>().FromBytes(message.AsBytes());
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            Logger.Debug("Module got request to spawn a server");

            if (IsAuthorizedToCreateRoom(session, data))
            {
                // creator is not authorized to create a room
                message.Respond("Unauthorized".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            var oldPromise = message.Peer.GetProperty(BmPropCodes.SpawnRequest) as SpawnTask;

            if ((oldPromise != null) && !oldPromise.IsDone())
            {
                message.Respond("You already have an active request".ToBytes(), AckResponseStatus.Failed);
                return;
            }

            // Starts game server creation
            var task = Spawn(data);

            if (task != null)
            {
                message.Peer.SetProperty(BmPropCodes.SpawnRequest, task);

                task.OnStatusChange += status =>
                {
                    // Push the status update to the creator of game room
                    message.Peer.SendMessage(MessageHelper.Create(BmOpCodes.CreateGameStatus,
                        (int)status), DeliveryMethod.ReliableSequenced);
                };
            }

            if (task == null)
            {
                message.Respond("All the servers are busy. Try again later".ToBytes(), AckResponseStatus.Failed);
                return;
            }

            message.Respond(MessageHelper.Create(message.OpCode, task.SpawnId), AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from user to retrieve information about all of the spawners
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleInspectorDataRequest(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (OnlyAdminCanViewInspector && (session.Account == null || !session.Account.IsAdmin))
            {
                // Ignore if this is not an administrator
                Logger.Warn("User tried to get spawners data, but he's not authorized to do so");
                message.Respond(AckResponseStatus.Unauthorized);
                return;
            }

            var allProcesses = Spawners.Values.ToDictionary(s => s.Id, s => s.GetSpawnedProcesses());

            // All processes received
            var data = new SpawnersInspectorPacket();
            var spawners = Spawners.Values;

            data.Spawners = new List<SpawnersInspectorPacket.SISpawnerData>();

            foreach (var spawner in spawners)
            {
                var spawnerData = new SpawnersInspectorPacket.SISpawnerData()
                {
                    Ip = spawner.Data.PublicIp,
                    MaxGameServers = spawner.MaxGames,
                    Region = spawner.Data.Region,
                    SpawnerId = spawner.Id,
                    GameServers = new List<SpawnersInspectorPacket.SIGameServerData>()
                };

                List<GameProcessInfoPacket> processes;
                allProcesses.TryGetValue(spawner.Id, out processes);

                // Create a new list for "smoother flow"
                if (processes == null)
                    processes = new List<GameProcessInfoPacket>();

                foreach (var processInfo in processes)
                {
                    var gameRoom = spawner.GetGameServerBySpawnId(processInfo.SpawnId);

                    var gameInfo = new SpawnersInspectorPacket.SIGameServerData()
                    {
                        CmdArgs = processInfo.CmdArgs,
                        SpawnId = processInfo.SpawnId,
                        CurrentPlayers = gameRoom != null ? gameRoom.OnlinePlayers : -1,
                        GameId = gameRoom != null ? gameRoom.GameId : -1,
                        MaxPlayers = gameRoom != null ? gameRoom.MaxPlayers : -1,
                        Name = gameRoom != null ? gameRoom.Name : "N/A"
                    };

                    spawnerData.GameServers.Add(gameInfo);
                }
                data.Spawners.Add(spawnerData);
            }

            // Done processing, send data
            message.Respond(data, AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from client to kill a game server
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleKillSpawnedProcess(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as Session;

            if (OnlyAdminCanKillServers && (session.Account == null || !session.Account.IsAdmin))
            {
                message.Respond("Unauthorized", AckResponseStatus.Unauthorized);
                return;
            }

            var spawnId = message.AsInt();

            var spawner = Spawners.Values.FirstOrDefault(s => s.ContainsProcess(spawnId));

            if (spawner == null)
            {
                message.Respond("Couldn't find a spawner responsible for spawning this game server", AckResponseStatus.Failed);
                return;
            }

            Logger.Trace("Got request to kill a game server with spawn id: " + spawnId);

            spawner.RequestProcessKill(spawnId, isKilled =>
            {
                message.Respond(isKilled ? AckResponseStatus.Success : AckResponseStatus.Failed);
            });
        }

        #endregion

        #region Spawner message handlers

        /// <summary>
        ///     Handles a message, received from spawner server.
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleMessage(IIncommingMessage message)
        {
            try
            {
                SpawnerHandlers[message.OpCode].Handle(message);
            }
            catch (Exception e)
            {

#if UNITY_EDITOR
                throw e;

#else
                if (LogLevel <= LogLevel.Error)
                {
                    Logs.Error("Error while handling a message from Spawner server. OpCode: " + message.OpCode);
                    Logs.Error(e);
                }

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

        /// <summary>
        ///     Handles an update message from spawner
        /// </summary>
        protected virtual void HandleSpawnerUpdateMessage(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new SpawnerUpdatePacket());

            var link = message.Peer.GetProperty(BmPropCodes.SpawnerLink) as SpawnerLink;

            if (link == null)
                return;

            link.UpdateState(data);
        }

        /// <summary>
        ///     Handles a registration message from spawner
        ///     Registers given peer as a spawner, with specified parameters
        /// </summary>
        protected virtual void HandleRegisterSpawnerMessage(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new SpawnerRegisterPacket());

            var link = CreateLink(message.Peer, data);
            message.Peer.SetProperty(BmPropCodes.SpawnerLink, link);

            Spawners.Add(link.Id, link);

            Logger.Info("Spawner registered (" + link.Peer.IsConnected + ")");

            // Invoke the event
            if (OnSpawnerRegistered != null)
                OnSpawnerRegistered.Invoke(link);

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a notification from spawner, which says that
        /// spawner has spawned a new game process
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGameProcessCreated(IIncommingMessage message)
        {
            var spawner = message.Peer.GetProperty(BmPropCodes.SpawnerLink) as SpawnerLink;

            if (spawner == null)
                return;

            var processInfo = message.DeserializeMessage<GameProcessInfoPacket>();
            spawner.AddGameProcess(processInfo);
        }

        /// <summary>
        /// Handles a notification from spawner, which says that spawner
        /// has killed process
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGameProcessKilled(IIncommingMessage message)
        {
            var spawner = message.Peer.GetProperty(BmPropCodes.SpawnerLink) as SpawnerLink;

            if (spawner == null)
                return;

            var spawnId = message.AsInt();
            spawner.RemoveGameProcess(spawnId);
        }

        #endregion

        #region Game Server message handlers

        /// <summary>
        /// Handles a message from game server (soon to be), which notifies that 
        /// unity process was started successfully
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleUnityProcessStarted(IIncommingMessage message)
        {
            var spawnId = message.AsInt();

            SpawnTask task;
            SpawnTasks.TryGetValue(spawnId, out task);

            if (task == null)
            {
                // Already timed out
                Logger.Error("Game Sever instance started, but no callback waiting");

                message.Peer.Disconnect("Timed out");
                return;
            }

            // Attach instance id for later identification
            // (for example, to know which instance got its server ready)
            message.Peer.SetProperty(BmPropCodes.SpawnId, spawnId);

            task.OnProcessStarted(message.Peer);

            // Respond with user properties
            var propertiesData = task.GetProperties().ToBytes();

            message.Respond(propertiesData, AckResponseStatus.Success);
        }
        
        #endregion
    }
}