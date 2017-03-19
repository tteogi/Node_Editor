using System;
using System.Collections;
using System.Collections.Generic;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Match;
using UnityEngine.Networking.NetworkSystem;
using UnityEngine.SceneManagement;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents uNET HLAPI specific game server, which uses network manager.
    ///     This implementation handles connection and client lookups.
    ///     Accepts access keys from clients
    /// </summary>
    public abstract class UnetGameServer : MonoBehaviour, IGameServer
    {
        // For keeping track of who passed and is ready
        private HashSet<int> _addedPlayers;

        private bool _isStopping;
        private Dictionary<int, string> _passedConnections;

        /// <summary>
        ///     Dictionary of connections that have not yet sent a Pass to join.
        ///     Kept to force disconnect users that have timed out
        /// </summary>
        private Dictionary<int, NetworkConnection> _passPendingConnections;

        // Lookup "tables"
        protected Dictionary<int, UnetClient> ClientsByConnectionId;
        protected Dictionary<string, UnetClient> ClientsByUsername;
        public EventfulNetworkManager NetworkManager;

        /// <summary>
        ///     Properties of this game server, which will be sent to master
        ///     server, when registering to it
        /// </summary>
        public Dictionary<string, string> Properties;

        /// <summary>
        ///     ServerAddress, which will be sent to users when giving out accesses
        /// </summary>
        [Header("Game Info")]
        public string PublicIpAddress;

        /// <summary>
        ///     Port, which will be sent to users, when giving out accesses
        /// </summary>
        public int Port;

        /// <summary>
        ///     Maximum time to wait for all users to disconnect when stopping
        ///     game server
        /// </summary>
        protected float WaitDisconnectUsers = 10f;

        /// <summary>
        ///     When client connects to game server, he will have a
        ///     certain amount of time to provide a pass
        /// </summary>
        public float AccessClaimTimeout = 10f;

        public bool IsRunning { get; protected set; }

        public int? LobbyId { get; protected set; }

        protected RegisteredGame Game { get; private set; }
        protected IClientSocket MasterConnection { get; private set; }

        protected virtual void Awake()
        {
            // Try to find a network manager
            NetworkManager = NetworkManager ?? FindObjectOfType<EventfulNetworkManager>();

            if (NetworkManager == null)
            {
                Debug.LogError(
                    "Couldn't find EventfulNetworkManager in the scene. Make sure your Network Manager is an extension " +
                    "of EventfulNetworkManager");
                return;
            }

            _passPendingConnections = new Dictionary<int, NetworkConnection>();
            _addedPlayers = new HashSet<int>();

            ClientsByConnectionId = new Dictionary<int, UnetClient>();
            ClientsByUsername = new Dictionary<string, UnetClient>();
            _passedConnections = new Dictionary<int, string>();

            if (!BmArgs.UseDefaultUnetConfig)
            {
                // This should help lifting the limit of uNET players for those,
                // who forgot to set it in network manager
                NetworkServer.Configure(BmHelper.CreateDefaultConnectionConfig(), BmHelper.MaxUnetConnections);
            }

            // Events from unet HLAPI network manager
            NetworkManager.OnServerConnectEvent += OnServerConnect;
            NetworkManager.OnServerDisconnectEvent += OnServerDisconnect;
            NetworkManager.OnStartServerEvent += OnStartServer;
            NetworkManager.OnStopServerEvent += OnStopServerEvent;
            NetworkManager.OnServerAddPlayerEvent += OnServerAddPlayerEvent;

            // Message handlers
            NetworkServer.RegisterHandler(UnetMsgType.GameJoin, OnClientPassReceived);
            NetworkServer.RegisterHandler(UnetMsgType.LeaveGame, OnLeaveGameReceived);

            // Subscribe to game server start event
            BmEvents.Channel.Subscribe(BmEvents.StartGameServer,
                (o, o1) => StartGameServer(o as StartGameServerData, (bool)o1));
        }

        /// <summary>
        ///     Called when game server is opened to public
        /// </summary>
        public virtual void OnOpenedToPublic()
        {
        }

        /// <summary>
        ///     Create the register packet which will be sent to master server.
        ///     Default implementation calls <see cref="FillRegistrationFromDictionary" /> after
        ///     getting some of the values from <see cref="Properties" />
        /// </summary>
        /// <returns></returns>
        public virtual RegisterGameServerPacket CreateRegisterPacket(string masterKey)
        {
            var packet = new RegisterGameServerPacket
            {
                Properties = Properties,
                ExtraData = new byte[0],
                PublicAddress = PublicIpAddress + ":" + Port,
                MasterKey = masterKey
            };

            FillRegistrationFromDictionary(packet, Properties);

            return packet;
        }

        /// <summary>
        ///     Returns a name of the scene, which will be used when client
        ///     tries to connect to game server
        /// </summary>
        /// <returns></returns>
        public virtual string GetClientConnectionScene()
        {
            return SceneManager.GetActiveScene().name;
        }

        /// <summary>
        ///     Called, when game server registers to master server
        /// </summary>
        /// <param name="masterConnection"></param>
        /// <param name="game"></param>
        public virtual void OnRegisteredToMaster(IClientSocket masterConnection, RegisteredGame game)
        {
            Game = game;
            MasterConnection = masterConnection;
        }

        /// <summary>
        ///     Disconnects player with given session id from game server
        /// </summary>
        /// <param name="username"></param>
        public virtual void DisconnectPlayer(string username)
        {
            UnetClient client;
            ClientsByUsername.TryGetValue(username, out client);

            if (client == null)
                return;

            client.Connection.Disconnect();
        }

        public void Shutdown()
        {
            StartCoroutine(StopGameServerCoroutine());
        }

        /// <summary>
        /// Checks if access should be given to the requester.
        /// Returns false, if access should not be given.
        /// This is a good place to implement custom logic for player bans and etc.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public virtual bool ValidateAccessRequest(GameAccessRequestPacket request, out string error)
        {
            error = null;
            return true;
        }

        /// <summary>
        /// This is called right before game server gives access data to a player.
        /// You can use this method to add custom properties to access data.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="accessData"></param>
        public virtual void FillAccessProperties(GameAccessRequestPacket request, GameAccessPacket accessData)
        {
        }

        /// <summary>
        ///     Invoked when user successfully claims a pass
        /// </summary>
        public event Action<string> OnUserJoined;

        /// <summary>
        ///     Called when user leaves the game
        /// </summary>
        public event Action<string> OnUserLeft;

        /// <summary>
        ///     Invoked, when server stops
        /// </summary>
        public event Action OnServerStopped;

        /// <summary>
        ///     Starts a game server
        /// </summary>
        /// <param name="startData"></param>
        public virtual void StartGameServer(StartGameServerData startData, bool startHost = false)
        {
            PublicIpAddress = startData.GameIpAddress;
            Port = startData.GamePort;
            Properties = startData.Properties;

            // Fill in the settings
            NetworkManager.networkPort = startData.GamePort;
            NetworkManager.useWebSockets = startData.UseWebsockets;
            NetworkManager.maxConnections = BmHelper.MaxUnetConnections;

            // In case this game needs to be attached to a lobby
            if (BmArgs.LobbyId >= 0)
            {
                // Save the lobby id for later
                LobbyId = BmArgs.LobbyId;

                LobbiesModule.AttachLobbyToGame(BmArgs.LobbyId, (data, error) =>
                {
                    if (data == null)
                    {
                        Debug.LogError("Failed to attach lobby ");
                        return;
                    }

                    StartGameServerWithLobby(startHost, data);
                });
                return;
            }

            if (startHost)
                NetworkManager.StartHost();
            else
                NetworkManager.StartServer();
        }

        public virtual void StartGameServerWithLobby(bool startHost, LobbyDataPacket lobbyData)
        {
            if (startHost)
                NetworkManager.StartHost();
            else
                NetworkManager.StartServer();
        }

        /// <summary>
        ///     Called when server is ready to add player.
        ///     This is the safest place after which player instances can be spawned and etc.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="controllerId"></param>
        protected void OnServerAddPlayerEvent(NetworkConnection conn, short controllerId)
        {
            _addedPlayers.Add(conn.connectionId);

            if (_passedConnections.ContainsKey(conn.connectionId))
                OnServerUserJoined(ClientsByConnectionId[conn.connectionId]);
        }

        /// <summary>
        /// This is called after client connects to game server
        /// and provides a valid access token
        /// </summary>
        /// <param name="client"></param>
        protected abstract void OnServerUserJoined(UnetClient client);

        /// <summary>
        ///     Called when client, who successfully passed, leaves the game server
        /// </summary>
        /// <param name="client"></param>
        protected abstract void OnServerUserLeft(UnetClient client);

        /// <summary>
        ///     Called when network manager triggers an event about started server.
        ///     Calls <see cref="Master.NotifyServerStarted" />
        /// </summary>
        protected virtual void OnStartServer()
        {
            GamesModule.NotifyServerStarted(this);
        }

        /// <summary>
        ///     Called when server stops
        /// </summary>
        protected virtual void OnStopServerEvent()
        {
            if (Game != null)
                Game.Unregister();

            if (OnServerStopped != null)
                OnServerStopped.Invoke();
        }

        /// <summary>
        ///     Stops the game server
        /// </summary>
        public virtual IEnumerator StopGameServerCoroutine()
        {
            if (_isStopping)
                throw new Exception("Server is already trying to stop");

            _isStopping = true;

            var disconnected = false;
            // Using Game object, because it has a convenient method
            Game.DisconnectAllPlayers(() => { disconnected = true; }, WaitDisconnectUsers);

            // Wait for users to be disconnected
            while (!disconnected)
                yield return null;

            // Stop the server
            NetworkManager.StopHost();

            // Stop the game
            if (Game != null)
                Game.Unregister();

            _isStopping = false;
        }

        /// <summary>
        ///     Helper method, which uses values from dictionary to fill registration
        ///     packet with options
        /// </summary>
        /// <param name="data"></param>
        public virtual void FillRegistrationFromDictionary(RegisterGameServerPacket packet,
            Dictionary<string, string> data)
        {
            packet.MaxPlayers = data.ContainsKey(GameProperty.MaxPlayers)
                ? Convert.ToInt32(data[GameProperty.MaxPlayers])
                : 10;
            bool.TryParse(data.ContainsKey(GameProperty.IsPrivate) ? data[GameProperty.IsPrivate] : "false",
                out packet.IsPrivate);
            packet.Name = data.ContainsKey(GameProperty.GameName) ? data[GameProperty.GameName] : "Not named";
            packet.Password = data.ContainsKey(GameProperty.Password) ? data[GameProperty.Password] : "";

            if (packet.Properties == null)
                packet.Properties = new Dictionary<string, string>();

            // Fill in the values from dictionary just in case
            foreach (var pair in data)
                if (!packet.Properties.ContainsKey(pair.Key))
                    packet.Properties.Add(pair.Key, pair.Value);
        }

        /// <summary>
        ///     Called on server, when client connects
        ///     Starts a pass wait timer <see cref="StartAccessTimeoutTimer" />
        /// </summary>
        /// <param name="conn"></param>
        protected void OnServerConnect(NetworkConnection conn)
        {
            _passPendingConnections.Add(conn.connectionId, conn);

            // Start timer to disconnect user if he doesn't provide a pass
            StartCoroutine(StartAccessTimeoutTimer(conn.connectionId));
        }

        /// <summary>
        ///     Called on server, when client disconnects
        /// </summary>
        /// <param name="conn"></param>
        protected void OnServerDisconnect(NetworkConnection conn)
        {
            _passedConnections.Remove(conn.connectionId);
            _addedPlayers.Remove(conn.connectionId);

            UnetClient client;
            ClientsByConnectionId.TryGetValue(conn.connectionId, out client);

            if (client == null)
                return;

            ClientsByConnectionId.Remove(conn.connectionId);
            ClientsByUsername.Remove(client.Username);

            OnServerUserLeft(client);

            Game.RemoveConnectedUser(client.Username);

            // Notify listeners
            if (OnUserLeft != null)
                OnUserLeft.Invoke(client.Username);
        }

        /// <summary>
        ///     Called when Game Server receives a leave request from client
        /// </summary>
        /// <param name="netMsg"></param>
        protected virtual void OnLeaveGameReceived(NetworkMessage netMsg)
        {
            UnetClient client;
            ClientsByConnectionId.TryGetValue(netMsg.conn.connectionId, out client);

            if (client != null)
                netMsg.conn.Disconnect();
        }

        /// <summary>
        ///     Called on server, when it receives client's request to join (sends a Pass)
        ///     Adds a new connection to lookup tables
        /// </summary>
        /// <param name="netMsg"></param>
        private void OnClientPassReceived(NetworkMessage netMsg)
        {
            var msg = netMsg.ReadMessage<StringMessage>();
            var passData = Game.TryClaimAccess(msg.value);

            if (passData == null)
            {
                netMsg.conn.Disconnect();
                Debug.LogError("Server received invalid GameJoinRequest with invalid pass: " + msg.value);
                return;
            }

            var client = new UnetClient(netMsg.conn, passData.Username, passData);

            // Add to lookup tables
            ClientsByConnectionId.Add(netMsg.conn.connectionId, client);
            ClientsByUsername.Add(client.Username, client);

            // Remove pending connection
            _passPendingConnections.Remove(netMsg.conn.connectionId);

            // Add a passed connection
            if (!_passedConnections.ContainsKey(netMsg.conn.connectionId))
                _passedConnections.Add(netMsg.conn.connectionId, passData.Username);
            else
                Debug.Log("For some reason, same connection id passed more than once");

            Game.AddConnectedUser(client.Username);

            // Notify listeners
            if (OnUserJoined != null)
                OnUserJoined.Invoke(client.Username);

            // Invoke if player is ready to be added
            if (_addedPlayers.Contains(client.Connection.connectionId))
                OnServerUserJoined(client);
        }

        /// <summary>
        ///     Waits for a specific time <see cref="AccessClaimTimeout" />,
        ///     after which checks if user has sent a pass, if not - force disconnects the user
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        private IEnumerator StartAccessTimeoutTimer(int connectionId)
        {
            yield return new WaitForSeconds(AccessClaimTimeout);

            if (_passPendingConnections.ContainsKey(connectionId))
            {
                Debug.Log("Player with connection id " + connectionId +
                          " failed to claim a Pass in time. Forcing disconnect");
                var connection = _passPendingConnections[connectionId];
                _passPendingConnections.Remove(connectionId);
                connection.Disconnect();
            }
        }

        public virtual void OnDestroy()
        {
            // Stop the server
            NetworkManager.StopServer();
        }

        public UnetClient GetClient(string username)
        {
            UnetClient link;
            ClientsByUsername.TryGetValue(username, out link);
            return link;
        }

        public UnetClient GetClient(NetworkConnection connection)
        {
            UnetClient link;
            ClientsByConnectionId.TryGetValue(connection.connectionId, out link);
            return link;
        }
    }

    /// <summary>
    ///     Represents a uNET client connection
    /// </summary>
    public class UnetClient
    {
        public UnetClient(NetworkConnection connection, string username, 
            GameAccessRequestPacket access)
        {
            Connection = connection;
            Username = username;
            AccessData = access;
        }

        /// <summary>
        /// uNET connection
        /// </summary>
        public NetworkConnection Connection { get; private set; }

        /// <summary>
        /// Username of the account who joined the game
        /// </summary>
        public string Username { get; private set; }

        /// <summary>
        /// Access data, which was used to access the game
        /// </summary>
        public GameAccessRequestPacket AccessData { get; private set; }

        /// <summary>
        /// Asks client to disconnect itself, and if it's not disconnected after the
        /// <see cref="forceDisconnectTimeout"/>, forcefully disconnects.
        /// </summary>
        /// <returns></returns>
        public IEnumerator Disconnect(float forceDisconnectTimeout = 5f)
        {
            if (BmArgs.UseWebsockets)
            {
                // uNET bug fix. Force disconnecting uNET client does not
                // fully disconnect it. Instead, we need to send him a request to disconnect itself

                Connection.Send(UnetMsgType.AskToDisconnect, new EmptyMessage());

                yield return new WaitForSeconds(forceDisconnectTimeout);

                if (Connection.isConnected)
                {
                    Connection.Disconnect();
                }
                yield break;
            }

            Connection.Disconnect();
        }
    }
}