using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Barebones.Logging;
#if UNITY_STANDALONE || UNITY_EDITOR
using System.Threading;

#endif

using Barebones.Networking;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a server that is responsible for spawning
    ///     game server instances on user request (not directly, but through master server).
    /// </summary>
    public class SpawnerServer : MonoBehaviour
    {
        public static SpawnerServer Instance;
        public bool IsStarted { get; protected set; }

#if UNITY_STANDALONE || UNITY_EDITOR

        private readonly object _lock = new object();

        private readonly object _updateLock = new object();

        private List<Action> _executeInMainThread;

        /// <summary>
        ///     Collection of running servers.
        ///     key - server instance id / creation task id
        /// </summary>
        private Dictionary<int, Process> _runningServers;

        public BmLogger Logger = LogManager.GetLogger(typeof(SpawnerServer).ToString());
        public LogLevel LogLevel = LogLevel.Debug;

        public string ExePath = "";

        /// <summary>
        ///     Queue of available ports (not yet assigned to game server)
        /// </summary>
        protected Queue<int> FreePorts;

        /// <summary>
        ///     Client socket, which is connected to master server
        /// </summary>
        protected IClientSocket MasterConnection;

        public int MasterGamesPort = 5001;

        public string MasterIp = "127.0.0.1";
        public int MasterSpawnersPort = 5002;
        public int MaxPort = 2000;

        /// <summary>
        /// A max number of game servers this spawner server can spawn
        /// </summary>
        public int MaxSpawns = 5;
        public int MinPort = 1500;

        /// <summary>
        /// Public IP address of the machine, on which this server is running.
        /// This will be passed to game servers, so that they know their public IP, and can send it to players
        /// </summary>
        public string MachineIp = "127.0.0.1";

        public string Region = "International";

        public bool RunInBatchMode = true;
        public bool UseWebsockets = false;

        [Header("Development Settings (editor only)")]
        public bool AutoStart = true;
        public string OverrideExePath = "";
        public bool WaitForMaster = true;

        public int RunningServersCount
        {
            get
            {
                lock (_lock)
                {
                    return _runningServers.Count;
                }
            }
        }

        protected object ThisLock
        {
            get
            {
                return _lock;
            }
        }

        protected virtual void Awake()
        {
            if (GamesModule.IsClient)
            {
                Destroy(gameObject);
                return;
            }

#if !UNITY_EDITOR
            if (!BmArgs.StartSpawner)
            {
                gameObject.SetActive(false);
                return;
            }
#endif

            Logger.LogLevel = LogLevel;

            // Ensure that logs are initialized
            LogController.Instance.InitializeLogs();

            Instance = this;
            //DontDestroyOnLoad(gameObject);
            _executeInMainThread = new List<Action>();

            _runningServers = new Dictionary<int, Process>();
            MasterConnection = Connections.CreateClientSocket();
            MasterConnection.OnConnected += OnConnectedToMaster;
            MasterConnection.OnDisconnected += OnDisconnectedFromMaster;
            
            MasterConnection.SetHandler(BmOpCodes.SpawnGameServer, HandleSpawnServer);
            MasterConnection.SetHandler(BmOpCodes.KillProcess, HandleKillProcess);
            MasterConnection.SetHandler(BmOpCodes.GetGameProcesses, HandleGetGameProcesses);

            // Fill in available ports
            FreePorts = new Queue<int>();
            for (var i = MinPort; i <= MaxPort; i++)
                FreePorts.Enqueue(i);
        }

        protected virtual void Start()
        {
#if UNITY_EDITOR
            // If necessary, override executable path
            if (!string.IsNullOrEmpty(OverrideExePath))
                ExePath = OverrideExePath;

            if (AutoStart)
            {
                if (WaitForMaster && !Master.IsStarted)
                    Master.OnStarted += StartServer;
                else
                    StartServer();
                return;
            }
#endif

            if (BmArgs.StartSpawner)
            {
                ExtractCmdArgs();
                if (BmArgs.StartMaster && !Master.IsStarted)
                    Master.OnStarted += StartServer;
                else
                    StartServer();
            }
        }

        protected virtual void Update()
        {
            lock (_updateLock)
            {
                foreach (var action in _executeInMainThread)
                    action.Invoke();

                _executeInMainThread.Clear();
            }
        }

        protected virtual void ExecuteOnUpdate(Action action)
        {
            lock (_updateLock)
            {
                _executeInMainThread.Add(action);
            }
        }

        /// <summary>
        ///     Called, when spawner is disconnected from master.
        ///     Default implementation shuts spawner and all it's game servers down
        /// </summary>
        protected virtual void OnDisconnectedFromMaster()
        {
            Shutdown();
        }

        /// <summary>
        ///     Called, when connected to master server.
        ///     Default implementation sends a registration request
        /// </summary>
        protected virtual void OnConnectedToMaster()
        {
            Logger.Info("Spawner server connected to master");

            var msg = MessageHelper.Create(BmOpCodes.RegisterSpawner, CreateRegisterPacket().ToBytes());
            MasterConnection.Peer.SendMessage(msg, (status, message) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    Logger.Error("Spawner failed to register");
                }
            });
        }

        /// <summary>
        /// Returns a path to executable, which should start a server.
        /// Override this to implement custom logic for selecting which executable to run.
        /// </summary>
        /// <returns></returns>
        protected virtual string GetExecutablePath()
        {
            // If a path is provided
            if (!string.IsNullOrEmpty(ExePath))
                return ExePath;

            // If no path is provided, we'll try to get path to executable which
            // is running the server (this code) 

            // If cmd args contain a path
            if (File.Exists(Environment.GetCommandLineArgs()[0]))
                return Environment.GetCommandLineArgs()[0];

            return Process.GetCurrentProcess().MainModule.FileName;
        }

        /// <summary>
        ///     Creates a packet, which is sent to master server as registration data
        /// </summary>
        /// <returns></returns>
        protected virtual SpawnerRegisterPacket CreateRegisterPacket()
        {
            return new SpawnerRegisterPacket
            {
                PublicIp = MachineIp,
                MaxServers = MaxSpawns,
                Properties = new Dictionary<string, string>(),
                Region = Region
            };
        }

        /// <summary>
        ///     Connects to master server
        /// </summary>
        public void StartServer()
        {
            MasterConnection.Connect(MasterIp, MasterSpawnersPort);

            IsStarted = true;
            Logger.Info("Spawner Server Started : " + MasterIp + ":" + MasterSpawnersPort);
        }

        /// <summary>
        ///     Kills all of the game server processes, and terminates itself
        /// </summary>
        public virtual void Shutdown()
        {
            lock (ThisLock)
            {
                foreach (var process in _runningServers.Values)
                    process.Kill();
            }

#if !UNITY_EDITOR
            Environment.Exit(0);
#endif
        }

        /// <summary>
        ///     Kills game server process
        /// </summary>
        /// <param name="spawnId"></param>
        public virtual void KillProcess(int spawnId)
        {
            try
            {
                Process process;

                lock (ThisLock)
                {
                    _runningServers.TryGetValue(spawnId, out process);
                    _runningServers.Remove(spawnId);
                }

                if (process != null)
                    process.Kill();
            }
            catch (Exception e)
            {
                Logger.Error("Got error while killing a spawned process");
                Logger.Error(e);
            }
        }

        protected virtual void ExtractCmdArgs()
        {
            MasterIp = BmArgs.MasterIp;
            MasterGamesPort = BmArgs.MasterGamesPort;
            MasterSpawnersPort = BmArgs.MasterSpawnersPort;
            ExePath = BmArgs.ExecutablePath;
            MinPort = BmArgs.MinPort;
            MaxPort = BmArgs.MaxPort;
            RunInBatchMode = BmArgs.SpawnInBatchMode;
            MaxSpawns = BmArgs.MaxGames;
            MachineIp = BmArgs.MachineIpAddress;
            Region = string.IsNullOrEmpty(BmArgs.Region) ? Region : BmArgs.Region;

            if (BmArgs.UseWebsockets)
                UseWebsockets = true;
        }

        /// <summary>
        ///     Notifies Master that one of the tasks is killed
        /// </summary>
        /// <param name="taskId"></param>
        protected virtual void NotifyAbortedTask(int taskId)
        {
            var msg = MessageHelper.Create(BmOpCodes.AbortSpawn, taskId);
            MasterConnection.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a notification to master server about new process
        /// </summary>
        /// <param name="spawnId"></param>
        public virtual void NotifyMasterAboutNewProcess(int spawnId, int processId, string args)
        {
            var info = new GameProcessInfoPacket()
            {
                CmdArgs = args,
                ProcessId = processId,
                SpawnId = spawnId
            };

            var msg = MessageHelper.Create(BmOpCodes.GameProcessCreated, info);
            MasterConnection.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        /// <summary>
        /// Sends a notification to master server about killed process
        /// </summary>
        /// <param name="spawnId"></param>
        public virtual void NotifyMasterAboutKilledProcess(int spawnId)
        {
            var msg = MessageHelper.Create(BmOpCodes.GameProcessKilled, spawnId);
            MasterConnection.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        /// <summary>
        ///     Sends info to master server about current state
        /// </summary>
        protected virtual void SendUpdate()
        {
            int serversCount;

            lock (ThisLock)
            {
                serversCount = _runningServers.Count;
            }

            var data = new SpawnerUpdatePacket
            {
                RunningGameServersCount = serversCount
            };
            var msg = MessageHelper.Create(BmOpCodes.SpawnerUpdate, data.ToBytes());
            MasterConnection.Peer.SendMessage(msg, DeliveryMethod.ReliableSequenced);
        }

        public virtual void OnDestroy()
        {
            if (BmArgs.StartSpawner)
                Shutdown();
        }


        #region Handlers

        /// <summary>
        ///     Spawns a game server instance
        /// </summary>
        public virtual void HandleSpawnServer(IIncommingMessage message)
        {
            Logger.Trace("Got spawn server request ");

            var data = MessageHelper.Deserialize(message.AsBytes(), new GameServerSpawnRequestPacket());

            int port;

            // Retrieve a free port
            lock (ThisLock)
            {
                port = FreePorts.Dequeue();
            }

            Logger.Trace("About to start process on path: " + ExePath);

            var startInfo = new ProcessStartInfo(GetExecutablePath())
            {
                CreateNoWindow = false,
                UseShellExecute = false,
                Arguments = " " +
                            (RunInBatchMode ? "-batchmode " : "") +
                            "-bmStartSpawned " +
                            string.Format("-bmFps {0} ", data.FpsLimit) +
                            string.Format("-bmSpawnId {0} ", data.SpawnId) +
                            string.Format("-bmMasterKey \"{0}\" ", data.MasterKey) +
                            string.Format("-bmGamePort {0} ", port) +
                            string.Format("-bmIp {0} ", MachineIp) +
                            string.Format("-bmMasterIp {0} ", MasterIp) +
                            string.Format("-bmGamesPort {0} ", MasterGamesPort) +
                            string.Format("-bmScene \"{0}\" ", data.SceneName) +
                            (BmArgs.DestroyObjects ? "-bmDestroy " : "") +
                            (UseWebsockets ? "-bmWebsockets " : "") +
                            data.CustomArgs

            };

            Logger.Trace("Starting process with arguments: " + startInfo.Arguments);

            var isStarted = false;

            try
            {
                new Thread(() =>
                {
                    try
                    {
                        Logger.Trace("New thread started");

                        using (var process = Process.Start(startInfo))
                        {
                            Logger.Trace("Process started");
                            var processId = process.Id;
                            var args = process.StartInfo.Arguments;
                            ExecuteOnUpdate(() => { NotifyMasterAboutNewProcess(data.SpawnId, processId, args); });

                            isStarted = true;

                            // Notify that we've "started starting" the server
                            ExecuteOnUpdate(() => { message.Respond(AckResponseStatus.Success); });

                            lock (ThisLock)
                            {
                                _runningServers.Add(data.SpawnId, process);
                            }

                            ExecuteOnUpdate(SendUpdate);

                            process.WaitForExit();
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!isStarted)
                            ExecuteOnUpdate(() => { message.Respond(AckResponseStatus.Failed); });

                        Logger.Error("Make sure that your Spawner Server has a correct path to your build");
                        Logger.Error(ex);
                    }
                    finally
                    {
                        ExecuteOnUpdate(() => { NotifyMasterAboutKilledProcess(data.SpawnId); });

                        lock (_runningServers)
                        {
                            _runningServers.Remove(data.SpawnId);
                            FreePorts.Enqueue(port);
                        }
                        ExecuteOnUpdate(SendUpdate);
                        ExecuteOnUpdate(() =>
                        {
                            // Notify master that the game is closed
                            MasterConnection.Peer.SendMessage(
                                MessageHelper.Create(BmOpCodes.SpawnerGameClosed, data.SpawnId),
                                DeliveryMethod.Reliable);
                        });
                    }
                }).Start();
            }
            catch (Exception e)
            {
                message.Respond(AckResponseStatus.Failed);
                Logs.Error(e);
            }
        }

        /// <summary>
        /// Handles a request from master server to kill a process
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleKillProcess(IIncommingMessage message)
        {
            var spawnId = message.AsInt();

            Logger.Warn("Received kill request for spawn id:  " + spawnId);

            KillProcess(spawnId);

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request to return a list of spawned processes
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleGetGameProcesses(IIncommingMessage message)
        {
            List<KeyValuePair<int, Process>> processes;

            lock (ThisLock)
            {
                processes = _runningServers.ToList();
            }

            var list = processes
                .Select(pair => (ISerializablePacket) new GameProcessInfoPacket()
                {
                    CmdArgs = pair.Value.StartInfo.Arguments,
                    ProcessId = pair.Value.Id,
                    SpawnId = pair.Key
                })
                .ToList();

            message.Respond(list.ToBytes(), AckResponseStatus.Success);
        }

        #endregion

#endif
    }
}