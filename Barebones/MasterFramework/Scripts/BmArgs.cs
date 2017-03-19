using System;
using System.Linq;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Handles barebones cmd args
    /// </summary>
    public class BmArgs
    {
        private static readonly string[] _args;

        static BmArgs()
        {
            _args = Environment.GetCommandLineArgs();

            if (_args == null)
                _args = new string[0];

            StartSpawned = ArgExists("-bmStartSpawned");
            StartManual = ArgExists("-bmStartManual");
            SpawnId = ExtractValueInt("-bmSpawnId");
            IsPrivate = ArgExists("-bmPrivate");
            QuitOnFailure = ArgExists("-bmQuitOnFailure");
            SceneName = ExtractValue("-bmScene");
            MasterIp = ExtractValue("-bmMasterIp", "127.0.0.1");
            MachineIpAddress = ExtractValue("-bmIp", "127.0.0.1");
            GamePort = ExtractValueInt("-bmGamePort", 7777);
            GameName = ExtractValue("-bmGameName", "Manual Server");
            MaxPlayers = ExtractValueInt("-bmMaxPlayers", 10);
            FpsLimit = ExtractValueInt("-bmFps", 30);
            MasterKey = ExtractValue("-bmMasterKey", "");
            Password = ExtractValue("-bmPassword", "");
            UseWebsockets = ArgExists("-bmWebsockets") || ArgExists("-bmWebsocket");
            DestroyObjects = ArgExists("-bmDestroy");
            Region = ExtractValue("-bmRegion");

            StartMaster = ArgExists("-bmMaster");
            MasterClientsPort = ExtractValueInt("-bmClientsPort", 5000);
            MasterGamesPort = ExtractValueInt("-bmGamesPort", 5001);
            MasterSpawnersPort = ExtractValueInt("-bmSpawnersPort", 5002);

            StartSpawner = ArgExists("-bmSpawner");
            MinPort = ExtractValueInt("-bmMinPort", 1500);
            MaxPort = ExtractValueInt("-bmMaxPort", 2000);
            MaxGames = ExtractValueInt("-bmMaxGames", 5);
            ExecutablePath = ExtractValue("-bmExe", "");
            SpawnInBatchMode = ArgExists("-bmBatchmode");

            LobbyId = ExtractValueInt("-bmLobbyId");

            ApplyFpsLimit();
            Application.runInBackground = true;
        }

        /// <summary>
        ///     If true, it's considered that we will start a user created game server
        /// </summary>
        public static bool StartSpawned { get; set; }

        /// <summary>
        ///     If true, it's considered we'll be starting a server manually
        /// </summary>
        public static bool StartManual { get; set; }

        /// <summary>
        ///     If true, server will try to exit the application, if it
        ///     fails to start
        /// </summary>
        public static bool QuitOnFailure { get; set; }

        /// <summary>
        ///     If true, server won't appear in listings
        /// </summary>
        public static bool IsPrivate { get; set; }

        /// <summary>
        ///     Namo of the scene, where game server should be started
        /// </summary>
        public static string SceneName { get; set; }

        /// <summary>
        ///     ServerAddress to the master server, through which game server will try to connect.
        ///     Should contain ip and port (ip:port)
        /// </summary>
        public static string MasterIp { get; set; }

        /// <summary>
        ///     Ip ServerAddress, to which game server will bind.
        ///     It will be given to connecting clients
        /// </summary>
        public static string MachineIpAddress { get; set; }

        /// <summary>
        ///     Port, the game server will listen to
        /// </summary>
        public static int GamePort { get; set; }

        /// <summary>
        ///     Name of the game room
        /// </summary>
        public static string GameName { get; set; }

        /// <summary>
        ///     Maximum number of players, allowed to join
        ///     this game server
        /// </summary>
        public static int MaxPlayers { get; set; }

        /// <summary>
        ///     Key, required to register to master server
        /// </summary>
        public static string MasterKey { get; set; }

        /// <summary>
        ///     Game password
        /// </summary>
        public static string Password { get; set; }

        /// <summary>
        ///     Server instance id. Used, to identify which server has started
        ///     (not the same thing as game id)
        /// </summary>
        public static int SpawnId { get; set; }

        /// <summary>
        ///     Fps limit for the game server
        /// </summary>
        public static int FpsLimit { get; set; }

        /// <summary>
        ///     Port, to which clients will connect, when
        ///     connecting to master server
        /// </summary>
        public static int MasterClientsPort { get; set; }

        /// <summary>
        ///     Port, to which game servers will connect, when
        ///     connecting to master server
        /// </summary>
        public static int MasterGamesPort { get; set; }

        /// <summary>
        ///     Port, to which spawner servers will connect, when
        ///     connecting to master server
        /// </summary>
        public static int MasterSpawnersPort { get; set; }

        /// <summary>
        ///     If true, will try to start a master server
        /// </summary>
        public static bool StartMaster { get; set; }

        /// <summary>
        ///     If true, will try to start a spawner server
        /// </summary>
        public static bool StartSpawner { get; set; }

        /// <summary>
        ///     Path to executable file, which will be used when spawning a
        ///     game server instance. If empty string, will use the same exe
        ///     as spawner
        /// </summary>
        public static string ExecutablePath { get; set; }

        /// <summary>
        ///     Lowest port number, which will be given to game servers
        /// </summary>
        public static int MinPort { get; set; }

        /// <summary>
        ///     Highest port number, which will be given to game servers
        /// </summary>
        public static int MaxPort { get; set; }

        /// <summary>
        ///     If true, spawner will start game servers in batchmode
        /// </summary>
        public static bool SpawnInBatchMode { get; set; }

        /// <summary>
        /// Region of this server
        /// </summary>
        public static string Region { get; set; }

        /// <summary>
        /// Id of the lobby, to which the game server should "attach" 
        /// </summary>
        public static int LobbyId { get; set; }

        public static bool UseDefaultUnetConfig { get; set; }

        /// <summary>
        /// If true, scripts that have object destroyer attached will be destroyed
        /// </summary>
        public static bool DestroyObjects { get; set; }

        /// <summary>
        ///     Number of games, current spawn server can spawn
        /// </summary>
        public static int MaxGames { get; set; }

        public static bool UseWebsockets { get; set; }

        public static bool IsServer { get { return StartManual || StartSpawned; } }

        public static void ApplyFpsLimit()
        {
#if !UNITY_EDITOR
            Application.targetFrameRate = FpsLimit;
            QualitySettings.vSyncCount = 0;
#endif
        }

        /// <summary>
        ///     Extracts a value for command line arguments provided
        /// </summary>
        /// <param name="argName"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public static string ExtractValue(string argName, string defaultValue = null)
        {
            if (!_args.Contains(argName))
                return defaultValue;

            var index = _args.ToList().FindIndex(0, a => a.Equals(argName));
            return _args[index + 1];
        }

        public static int ExtractValueInt(string argName, int defaultValue = -1)
        {
            var number = ExtractValue(argName, defaultValue.ToString());
            return Convert.ToInt32(number);
        }

        public static bool ArgExists(string argName)
        {
            return _args.Contains(argName);
        }
    }
}