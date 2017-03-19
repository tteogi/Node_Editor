using System;
using System.Collections;
using System.Collections.Generic;
using Barebones.Logging;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Unet connector implementation.
    ///     Connects client to game server.
    ///     To allow faster development, it listens to events, fired by game server,
    ///     and reacts accordingly (automatically joins a new game server and etc.)
    /// </summary>
    public class UnetConnector : GameConnector
    {
        private GameAccessPacket _access;
        private bool _isHost;
        private EventfulNetworkManager _networkManager;

        [Header("Development: ")] 
        // If set to true, will automatically try to join a game,
        // if network manager starts server as a host
        public bool AutoJoinIfHost = true;

        /// <summary>
        ///     Whether or not client should be disconnected,
        ///     when it connects without a pass
        /// </summary>
        public bool DisconnectIfNoPass = false;

        /// <summary>
        ///     If true, connector will be disabled in editor
        /// </summary>
        public bool IgnoreInEditor = false;

        /// <summary>
        ///     Log level of connector
        /// </summary>
        public LogLevel LogLevel = LogLevel.Error;

        /// <summary>
        ///     Port address, to which client will try to connect
        /// </summary>
        public int MasterClientsPort = 80;

        /// <summary>
        ///     Master ip address, to which client will try to
        ///     connect
        /// </summary>
        public string MasterIp = "127.0.0.1";

        /// <summary>
        /// If true, destroes network manager when disconnected from game server
        /// </summary>
        public bool DestroyNetworkManagerOnDisconnect = true;
        
        /// <summary>
        ///     Name of the scene, to which connector will switch after disconnecting,
        ///     or failed connection attempt
        /// </summary>
        [Header("Scene to switch to after disconnect")]
        public string SceneName = "Main";

        [Header("If empty, logs in as guest")]
        public string Username = "";
        public string Password = "";

        public BmLogger Logger = LogManager.GetLogger(typeof(UnetConnector).ToString());

        protected override void Awake()
        {
            Logger.LogLevel = LogLevel;

            base.Awake();

#if UNITY_EDITOR
            if (IgnoreInEditor)
            {
                gameObject.SetActive(false);
                return;
            }
#endif

            _networkManager = FindObjectOfType<EventfulNetworkManager>();
            if (_networkManager != null)
            {
                _networkManager.maxConnections = BmHelper.MaxUnetConnections;
                _networkManager.OnClientConnectEvent += OnClientConnected;
                _networkManager.OnClientDisconnectEvent += OnClientDisconnected;
                _networkManager.OnStopClientEvent += OnStoppedClient;
                _networkManager.OnStartHostEvent += OnStartHost;
            }

            GamesModule.OnGameRegistered += OnGameRegistered;
        }

        private void Start()
        {
            if (AccessData != null)
                ConnectToGame(AccessData);
        }

        /// <summary>
        ///     Called, when game server is started as a host
        /// </summary>
        private void OnStartHost()
        {
            _isHost = true;
        }

        /// <summary>
        ///     Called, when game server registers a game
        /// </summary>
        /// <param name="game"></param>
        private void OnGameRegistered(RegisteredGame game)
        {
            if (!game.IsOpened)
            {
                game.OnOpened += barebonesGame =>
                {
                    if (AutoJoinIfHost && _isHost)
                        StartCoroutine(AutoJoinCoroutine(game));
                };
            }
            else
            {
                
                if (AutoJoinIfHost && _isHost)
                    StartCoroutine(AutoJoinCoroutine(game));
            }

        }

        /// <summary>
        ///     Called on client, when he connects to game server
        /// </summary>
        /// <param name="connection"></param>
        private void OnClientConnected(NetworkConnection connection)
        {
            connection.RegisterHandler(UnetMsgType.AskToDisconnect, msg =>
            {
                if (_networkManager != null)
                    _networkManager.StopClient();
            });

            // Send the pass after connecting to game server
            if (_access != null)
            {
                SendAccessToGameServer(_access);
                _access = null;
                return;
            }

            if (_isHost && AutoJoinIfHost)
                return;

            // We have no pass
            Logger.Error("Connected without a pass");

            if (DisconnectIfNoPass && !_isHost)
                _networkManager.StopClient();
        }

        /// <summary>
        ///     Automatically connects to master server as client,
        ///     logs in if necessary, retrieves a pass to enter game server,
        ///     and enters it
        /// </summary>
        /// <returns></returns>
        public IEnumerator AutoJoinCoroutine(RegisteredGame game)
        {
            //-----------------------------------
            // 1. Connect as client if we're not already connected
            var clientConnection = Connections.ClientToMaster;

            // Start connecting, if not connecting or connected
            if (!clientConnection.IsConnected && !clientConnection.IsConnecting)
                clientConnection.Connect(MasterIp, MasterClientsPort);

            // Wait while connecting
            while (clientConnection.IsConnecting && !clientConnection.IsConnected)
            {
                yield return null;
            }

            if (!clientConnection.IsConnected)
                throw new Exception("Connector failed to connect to master as client. " +
                                    string.Format("ServerAddress {0}:{1}", MasterIp, MasterClientsPort));

            //-----------------------------------
            // 2. Login if we're not already logged in
            if (!Auth.IsLoggedIn)
            {
                var loginData = new Dictionary<string, string>();

                if (string.IsNullOrEmpty(Username))
                {
                    loginData.Add("guest", "guest");
                }
                else
                {
                    loginData.Add("username", Username);
                    loginData.Add("password", Password);
                }

                Logger.Debug("Trying to log in...");

                var isLoginDone = false;
                
                Auth.LogIn(loginData, (successful, error) =>
                {
                    isLoginDone = true;
                    if (!successful || (error != null))
                    {
                        Logger.Error("Failed to login: " + error);
                    }
                        
                });

                while (!isLoginDone)
                    yield return null;

                if (!Auth.IsLoggedIn)
                    yield break;

                Logger.Debug("Login successfull ");
            }

            //-----------------------------------
            // 3. Get pass to enter game server
            var passRequest = new RoomJoinRequestDataPacket
            {
                RoomId = game.GameId,
                RoomPassword = game.Password
            };
            
            GamesModule.GetAccess(passRequest, (passData, error) =>
            {
                
                if ((passData == null) || (error != null))
                {
                    Logger.Error("Failed to get a pass: " + error);
                    return;
                }

                //-----------------------------------
                // 4. Send pass to game server
                var connection = FindObjectOfType<NetworkManager>().client.connection;
                connection.Send(UnetMsgType.GameJoin, new StringMessage(passData.AccessToken));
            });
        }

        /// <summary>
        ///     Called on client, when it is stopped
        /// </summary>
        private void OnStoppedClient()
        {
            GoToDisconnectedScene();
        }

        /// <summary>
        ///     Called on client, when it is disconnected
        /// </summary>
        /// <param name="connection"></param>
        private void OnClientDisconnected(NetworkConnection connection)
        {
            GoToDisconnectedScene();
        }

        protected void GoToDisconnectedScene()
        {
            if (DestroyNetworkManagerOnDisconnect)
            {
                var networkManager = FindObjectOfType<NetworkManager>();

                if (networkManager != null)
                {
                    Destroy(networkManager.gameObject);
                }
            }
            
            SceneManager.LoadScene(SceneName);
        }

        /// <summary>
        ///     Connects to game server with given access.
        ///     This implementation uses network manager and starts a client
        /// </summary>
        /// <param name="access"></param>
        protected override void ConnectToGame(GameAccessPacket access)
        {
            _access = access;

            if (access == null)
            {
                GoToDisconnectedScene();
                return;
            }
            
            if (access.SceneName != SceneManager.GetActiveScene().name)
            {
                // We need to change the scene first
                SceneManager.LoadScene(access.SceneName);
                return;
            }
            
            AccessData = null;

            if (!access.Address.Contains(":"))
            {
                Logger.Error("Invalid address. Should be in format ip:port (for example 127.0.0.1:80)");
                return;
            }

            // TODO Connect to Game Server
            var parts = access.Address.Split(':');

            _networkManager = _networkManager ?? FindObjectOfType<EventfulNetworkManager>();
            _networkManager.networkAddress = parts[0];
            _networkManager.networkPort = Convert.ToInt32(parts[1]);
            _networkManager.maxConnections = BmHelper.MaxUnetConnections;

            _networkManager.StartClient();
        }

        /// <summary>
        ///     Sends access to game server
        /// </summary>
        /// <param name="access"></param>
        public override void SendAccessToGameServer(GameAccessPacket access)
        {
            var connection = FindObjectOfType<NetworkManager>().client.connection;

            if (connection == null)
            {
                Logger.Error("uNET NetworkConnection is missing");
                return;
            }

            connection.Send(UnetMsgType.GameJoin, new StringMessage(access.AccessToken));
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            GamesModule.OnGameRegistered -= OnGameRegistered;

            if (_networkManager != null)
            {
                _networkManager.OnClientConnectEvent += OnClientConnected;
                _networkManager.OnClientDisconnectEvent += OnClientDisconnected;
                _networkManager.OnStartHostEvent += OnStartHost;
                _networkManager.OnStopClientEvent -= OnStoppedClient;
            }
        }
    }
}