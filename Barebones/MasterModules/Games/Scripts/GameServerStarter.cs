using System;
using System.Collections.Generic;
using Barebones.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Script, that handles starting of game server.
    ///     Most of the public properties can be edited via inspector, and overriden
    ///     with command line arguments.
    ///     Game server technology independent.
    /// </summary>
    public class GameServerStarter : MonoBehaviour
    {
        private static bool _hasStarted;

        private int _spawnId = -1;

        /// <summary>
        ///     If true, registered game server will be opened instantly
        /// </summary>
        public bool AutoOpenGame = true;

        public LogLevel LogLevel = LogLevel.Warn;
        public BmLogger Logger = LogManager.GetLogger(typeof(GameServerStarter).ToString());

        [Header("Development (Editor only)")]
        // If true, game server will be started automatically in editor
        public bool AutoStart = true;

        // If true, game server will wait for master to start (int editor)
        public bool WaitForMaster = true;

        [Header("Game info")] public string GameName = "Fake Game";

        public string GamePassword = "";

        /// <summary>
        ///     If true, this server will not appear in game lists
        /// </summary>
        public bool IsPrivate;

        public string MapName = "Unknown";

        /// <summary>
        ///     Port, which will be used when game server connects to master server
        /// </summary>
        public int MasterGamesPort = 5001;

        /// <summary>
        ///     Ip, which will be used when game server connects to master server
        /// </summary>
        public string MasterIp = "127.0.0.1";

        /// <summary>
        ///     Master key will be used when registering to master server.
        ///     If it doesn't match, master server will refuse registration
        /// </summary>
        public string MasterKey = "";

        public int MaxPlayers = 20;

        /// <summary>
        ///     Port, which will be sent to clients with access data
        /// </summary>
        public int Port = 7777;

        [Header("Game server settings")]
        // Ip, which will be sent to clients with access data
        public string PublicIp = "127.0.0.1";

        // If true, game server will be started as host
        public bool StartAsHost = true;

        /// <summary>
        ///     Only set if this is supposed to be a user created game server
        /// </summary>
        protected Dictionary<string, string> SpawnSettings;

        public bool UseWebsockets;

        protected virtual void Awake()
        {
            // TODO think of a better way
            if (GamesModule.IsClient || _hasStarted)
                Destroy(gameObject);

            Logger.LogLevel = LogLevel;

#if UNITY_WEBGL && !UNITY_EDITOR
            Destroy(gameObject);
#endif

        }

        protected virtual void Start()
        {
#if UNITY_EDITOR
            // Quick development setup
            if (AutoStart)
            {
                Logger.Trace("Auto Starting a game server");
                GamesModule.OnGameServerStarted += OnServerStarted;

                if (WaitForMaster && !Master.IsStarted)
                {
                    Logger.Trace("Waiting for master server before starting (option selected)");
                    Master.OnStarted += StartManualServer;
                }
                else
                {
                    StartManualServer();
                }
                return;
            }
#endif
            // Override start as host, if it's not the editor
            StartAsHost = false;

            // Switch scenes, if necessary
            var scene = GetRequiredSceneName();
            if (!string.IsNullOrEmpty(scene))
                if (scene != SceneManager.GetActiveScene().name)
                {
                    SceneManager.LoadScene(scene);
                    return;
                }

            if (_hasStarted)
                return;

            _hasStarted = true;

            GamesModule.OnGameServerStarted += OnServerStarted;

            if (BmArgs.StartManual)
            {
                ExtractCmdArgs();
                StartManualServer();
            }
            else if (BmArgs.StartSpawned)
            {
                ExtractCmdArgs();
                StartSpawnedServer();
            }
        }

        /// <summary>
        ///     Starts "user created" server chain - connects to master server,
        ///     notifies about a started instance, retrieves user settings, and then starts the
        ///     actual server
        /// </summary>
        protected virtual void StartSpawnedServer()
        {
            // 1. Connect to master
            var connection = Connections.GameToMaster;
            connection.Connect(MasterIp, MasterGamesPort).WaitConnection(socket =>
            {
                if (!socket.IsConnected)
                {
                    OnDisconnectedFromMaster();
                    Logs.Error("Game server (instance) failed to connect to master");
                    return;
                }

                // 2. Notify master that the instance was started
                SpawnersModule.NotifyProcessStarted(_spawnId, parameters =>
                {
                    if (parameters == null)
                    {
                        Logs.Error("Failed to notify master about started instance ");
                        return;
                    }

                    SpawnSettings = parameters;

                    ExtractUserSettings(parameters);

                    // 3. Start the server
                    StartGameServer();
                });
            });

            connection.OnDisconnected += OnDisconnectedFromMaster;
        }

        /// <summary>
        ///     Starts dedicated server chain - connects to master, and starts the actual server
        /// </summary>
        protected virtual void StartManualServer()
        {
            // 1. Connect to master
            Logger.Trace("Game server is connecting to master...");

            var connection = Connections.GameToMaster;
            connection.Connect(MasterIp, MasterGamesPort).WaitConnection(socket =>
            {
                if (!socket.IsConnected)
                {
                    OnDisconnectedFromMaster();
                    Logs.Error("Game server failed to connect to master");
                    return;
                }

                Logger.Trace("Game server connected to master");

                // 2. Start the server
                StartGameServer();
            });

            connection.OnDisconnected += OnDisconnectedFromMaster;
        }

        /// <summary>
        ///     Starts the server itself.
        ///     Fires an event <see cref="BmEvents.StartGameServer" />
        /// </summary>
        protected virtual void StartGameServer()
        {
            Logger.Trace("Sending an event to start a game server");

            // Fire an event
            if (!BmEvents.Channel.Fire(BmEvents.StartGameServer, CreateStartData(), StartAsHost))
            {
                Logger.Error("'Game Server Starter' fired a start event, but nothing handled it. " +
                                   "Most likely, you have no game server object in the scene");
            }
        }

        /// <summary>
        ///     Creates server start data, which will be used to start the game server.
        ///     Uses spawn settings dictionary <see cref="SpawnSettings" /> if the server is
        ///     user created, or if the server is manual - fills settings from public
        ///     properties (which might have been overriden by cmd args)
        /// </summary>
        /// <returns></returns>
        protected virtual StartGameServerData CreateStartData()
        {
            var values = SpawnSettings;

            if (values == null)
            {
                values = new Dictionary<string, string>();
                values.Add(GameProperty.GameName, GameName);
                values.Add(GameProperty.MaxPlayers, MaxPlayers.ToString());
                values.Add(GameProperty.Password, GamePassword);
                values.Add(GameProperty.IsPrivate, IsPrivate.ToString());
                values.Add(GameProperty.MapName, MapName);
            }

            return new StartGameServerData
            {
                GameIpAddress = PublicIp,
                GamePort = Port,
                MasterConnection = Connections.GameToMaster,
                Properties = values,
                UseWebsockets = UseWebsockets
            };
        }

        /// <summary>
        ///     Extracts values from settings which were provided from user
        /// </summary>
        /// <param name="settings"></param>
        protected virtual void ExtractUserSettings(Dictionary<string, string> settings)
        {
            if (settings.ContainsKey(GameProperty.IsPrivate))
                bool.TryParse(settings[GameProperty.IsPrivate], out IsPrivate);

            if (settings.ContainsKey(GameProperty.GameName))
                GameName = settings[GameProperty.GameName];

            if (settings.ContainsKey(GameProperty.MapName))
                MapName = settings[GameProperty.MapName];

            if (settings.ContainsKey(GameProperty.Password))
                GamePassword = settings[GameProperty.Password];

            if (settings.ContainsKey(GameProperty.MaxPlayers))
            {
                int maxPlayers;
                int.TryParse(settings[GameProperty.MaxPlayers], out maxPlayers);
                if (maxPlayers > 0)
                    MaxPlayers = maxPlayers;
            }
        }

        /// <summary>
        ///     Extracts values from command line arguments
        /// </summary>
        protected virtual void ExtractCmdArgs()
        {
            MasterIp = BmArgs.MasterIp;
            MasterGamesPort = BmArgs.MasterGamesPort;

            PublicIp = BmArgs.MachineIpAddress;
            Port = BmArgs.GamePort;

            if (BmArgs.UseWebsockets)
                UseWebsockets = true;

            if (BmArgs.StartManual)
            {
                GameName = BmArgs.GameName;
                MaxPlayers = BmArgs.MaxPlayers;
                IsPrivate = BmArgs.IsPrivate;
                GamePassword = BmArgs.Password;
                MasterKey = BmArgs.MasterKey;
            }

            if (BmArgs.StartSpawned)
            {
                _spawnId = BmArgs.SpawnId;
                MasterKey = BmArgs.MasterKey;
            }
        }

        /// <summary>
        ///     Should return a name of the scene that should be loaded.
        ///     Default implementation returns a scene name from args.
        ///     Override if you want to implement custom logics for selecting a scene to load
        ///     <see cref="BmArgs.SceneName" />
        /// </summary>
        protected string GetRequiredSceneName()
        {
            return BmArgs.SceneName;
        }

        /// <summary>
        ///     Called, when game server starts.
        ///     Applies fps limit and registers the game server
        /// </summary>
        /// <param name="server"></param>
        protected virtual void OnServerStarted(IGameServer server)
        {
            Logger.Trace("Game server started, about to register it to master");
            try
            {
                // Register Game server after it's started
                GamesModule.RegisterGame(server.CreateRegisterPacket(MasterKey), server, game =>
                {
                    if (game == null)
                    {
                        Logger.Error("Failed to register game server");

                        if (BmArgs.QuitOnFailure)
                            Application.Quit();
                        return;
                    }

                    Logger.Trace("Successfully registered game server to master");

                    if (AutoOpenGame)
                        game.Open(isSuccessfull =>
                        {
                            Logger.Trace("Game server opened: " + isSuccessfull);
                        });
                });
            }
            catch (Exception e)
            {
                Logger.Error("Got error while trying to register game server. Most likely, something " +
                               "went wrong while creating a game server registration packet");

                if (BmArgs.QuitOnFailure)
                    Application.Quit();
                throw e;
            }
        }

        protected virtual void OnDisconnectedFromMaster()
        {
            Application.Quit();
        }

        private void OnDestroy()
        {
            GamesModule.OnGameServerStarted -= OnServerStarted;
        }
    }
}