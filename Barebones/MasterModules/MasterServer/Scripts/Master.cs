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
    ///     Main Master server component.
    ///     Manages modules and their dependencies
    /// </summary>
    public class Master : MonoBehaviour, IMaster
    {
        public delegate Type DependencyResolver<T>() where T : class, IMasterModule;

        private static bool _isStarted;

        private IServerSocket _clientsSocket;

        private readonly IMessage _internalServerErrorMsg = MessageHelper.Create(BmOpCodes.Error,
            "Internal Server Error");

        private Dictionary<Type, IMasterModule> _modules;
        private HashSet<Type> _initializedModules;

        /// <summary>
        /// If true, will go to root of your hierarchy when awakened.
        /// Useful, when you don't want to destroy master server when changing scenes
        /// </summary>
        public bool GoToRootHierarchy = true;

        public BmLogger Logger = LogManager.GetLogger(typeof(Master).ToString());

        public LogLevel LogLevel = LogLevel.Warn;

        [Header("Development (Editor only)")]
        // If true, will try to start master server in the editor
        public bool AutoStartInEditor = false;

        /// <summary>
        /// If true, exceptions will not be caught. Helpful when
        /// debugging, to find an exact source of exception
        /// </summary>
        public bool RethrowExceptions = true;

        /// <summary>
        ///     All the handlers that handle messages from game servers
        /// </summary>
        protected Dictionary<int, IPacketHandler> ClientHandlers;

        /// <summary>
        ///     Collection of all game server peers
        /// </summary>
        protected Dictionary<int, IPeer> ClientPeers;

        [Header("Settings")]
        // Port, to which clients will connect
        public int ClientsPort = 5000;

        /// <summary>
        ///     When game servers register, they will need to match the master key
        /// </summary>
        public string MasterKey { get; set; }

        public static BMMasterFactories Factories = new BMMasterFactories();

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static Master Instance { get; protected set; }

        /// <summary>
        /// Connected clients session registry
        /// </summary>
        public SessionRegistry<ISession> SessionRegistry { get; private set; }

        /// <summary>
        /// Event, invoked before master server starts
        /// </summary>
        public static event Action BeforeStart;

        public static bool IsStarted
        {
            get { return _isStarted; }
            protected set
            {
                _isStarted = value;
                if (OnStarted != null)
                    OnStarted.Invoke();
            }
        }

        public static event Action OnStarted;

        /// <summary>
        ///     Invoked when connection with game server is lost
        /// </summary>
        public event Action<IPeer> OnClientDisconnected;

        /// <summary>
        ///     Invoked when game server connected to master
        /// </summary>
        public event Action<IPeer> OnClientConnected;

        public void Awake()
        {
            if (ShouldBeDestroyed())
            {
                Destroy(gameObject);
                return;
            }

            // Ensure that logs are initialized
            LogController.Instance.InitializeLogs();

            MasterKey = "";

            Logger.LogLevel = LogLevel;

            // Go to root of the hierarchy 
            if (GoToRootHierarchy)
                transform.SetParent(null);

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ClientHandlers = new Dictionary<int, IPacketHandler>();
            ClientPeers = new Dictionary<int, IPeer>();
            _modules = new Dictionary<Type, IMasterModule>();
            _initializedModules = new HashSet<Type>();

            Initialize();

            _clientsSocket = Connections.CreateServerSocket();
            SessionRegistry = new SessionRegistry<ISession>(Factories.SessionFactory);

            // Add a client handler
            SetClientHandler(new AesKeyRequestHandler());

            OnAwake();
        }

        protected virtual bool ShouldBeDestroyed()
        {
            // Destroy if there's already an instance
            // or if we're actually playing as a client
            if ((Instance != null) || GamesModule.IsClient)
            {
                return true;
            }

            // Destory, if it's a webgl build
#if UNITY_WEBGL && !UNITY_EDITOR
        Destroy(gameObject);
        return true;
#endif

            return false;
        }

        public virtual void Start()
        {
#if !UNITY_WEBGL || UNITY_EDITOR


#if UNITY_EDITOR
            // Destroy if it's an editor, and we don't want to start master
            if (AutoStartInEditor)
                StartCoroutine(StartOnNextFrame());
#else
            
#endif

            // If we're not going to start a master server
            if (BmArgs.StartMaster && !IsStarted)
                StartCoroutine(StartOnNextFrame());

#endif
        }

        protected virtual IEnumerator StartOnNextFrame()
        {
            yield return null;
#if UNITY_EDITOR
            if (AutoStartInEditor)
            {
                StartServer();
                yield break;
            }
#endif

            if (BmArgs.StartMaster)
            {
                ExtractCmdArgs();
                StartServer();
            }
        }

        /// <summary>
        ///     Starts the master server and it's modules
        /// </summary>
        public virtual void StartServer()
        {
            if (BeforeStart != null)
                BeforeStart.Invoke();

            _clientsSocket.OnConnected += OnClientConnect;
            _clientsSocket.OnDisconnected += OnClientDisconnect;
            _clientsSocket.Listen(ClientsPort);

            // Find all modules
            var modules = FindObjectsOfType<MasterModule>();

            // Register modules
            foreach (var module in modules)
            {
                RegisterModule(module);
            }

            InitializeModules();

            IsStarted = true;

            Logger.Info("Master Server started");
        }

        /// <summary>
        ///     Called when client connects to master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnClientConnect(IPeer peer)
        {
            // Create and set a session
            var session = SessionRegistry.Create(peer);
            peer.SetProperty(BmPropCodes.Session, session);

            peer.OnMessage += HandleMessage;

            if (ClientPeers.ContainsKey(peer.Id))
                return;

            ClientPeers.Add(peer.Id, peer);

            if (OnClientConnected != null)
                OnClientConnected.Invoke(peer);

            Logger.Info("Client connected. Peer id: " + peer.Id);
        }

        /// <summary>
        /// Goes through all of the registered modules, and initializes the ones
        /// that haven't been initialized
        /// </summary>
        protected virtual void InitializeModules()
        {
            // Initialize modules
            while (true)
            {
                var changed = false;
                foreach (var entry in _modules)
                {
                    // Module is already initialized
                    if (_initializedModules.Contains(entry.Key))
                        continue;

                    // Not all dependencies have been initialized
                    if (!entry.Value.Dependencies.All(d => _initializedModules.Any(d.IsAssignableFrom)))
                        continue;

                    // If we got here, we can initialize our module
                    entry.Value.Initialize(this);
                    _initializedModules.Add(entry.Key);
                    changed = true;
                }

                // If we can no longer initialize anything
                if (!changed)
                {
                    var uninitialized = _modules
                        .Where(m => !_initializedModules.Contains(m.Key))
                        .ToList();

                    if (uninitialized.Count > 0)
                    {
                        var names = string.Join("\n", uninitialized.Select(u => u.Key.ToString()).ToArray());
                        Debug.LogError("Failed to initialized modules. Probably some of the dependencies were not found. Modules: \n" + names);
                    }
                    break;
                }
            }
        }

        /// <summary>
        ///     Called when spawner server disconnects from master
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnClientDisconnect(IPeer peer)
        {
            // Remove the session from registry
            var session = peer.GetProperty(BmPropCodes.Session) as ISession;
            if (session != null)
                SessionRegistry.Remove(session.Id);

            peer.OnMessage -= HandleMessage;

            ClientPeers.Remove(peer.Id);

            if (OnClientDisconnected != null)
                OnClientDisconnected.Invoke(peer);

            Logger.Info("Client disconnected. Peer id: " + peer.Id);
        }

        /// <summary>
        /// Adds a handler to the collection of client packet handlers.
        /// This handler will be invoked when master server receives a
        /// message of the specified <see cref="IPacketHandler.OpCode"/>
        /// </summary>
        /// <param name="handler"></param>
        public IPacketHandler SetClientHandler(IPacketHandler handler)
        {
            if (ClientHandlers.ContainsKey(handler.OpCode))
                ClientHandlers[handler.OpCode] = handler;
            else
                ClientHandlers.Add(handler.OpCode, handler);

            return handler;
        }

        /// <summary>
        /// Adds a handler to the collection of client packet handlers.
        /// This handler will be invoked when master server receives a
        /// message of the specified <see cref="IPacketHandler.OpCode"/>
        /// </summary>
        public IPacketHandler SetClientHandler(short opCode, Action<IIncommingMessage> handler)
        {
            var newHandler = new PacketHandler(opCode, handler);
            SetClientHandler(new PacketHandler(opCode, handler));
            return newHandler;
        }

        [Obsolete("Use SetClientHandler")]
        public IPacketHandler AddClientHandler(IPacketHandler handler)
        {
            SetClientHandler(handler);
            return handler;
        }

        /// <summary>
        ///     Handles a message, received from game server.
        /// </summary>
        /// <param name="message"></param>
        public virtual void HandleMessage(IIncommingMessage message)
        {
            try
            {
                IPacketHandler handler;
                ClientHandlers.TryGetValue(message.OpCode, out handler);

                if (handler != null)
                    handler.Handle(message);
                else if (message.IsExpectingResponse)
                {
                    Logger.Warn("Couldn't find a handler for clients message. OpCode: " + message.OpCode);
                    message.Respond(_internalServerErrorMsg, AckResponseStatus.Error);
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                if (RethrowExceptions)
                    throw;
#endif

                Logger.Error("Error while handling a message from Client. OpCode: " + message.OpCode);
                Logger.Error(e);

                if (!message.IsExpectingResponse)
                    return;

                try
                {
                    message.Respond(_internalServerErrorMsg, AckResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
            }
        }

        /// <summary>
        ///     Retrieves a module of type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : class, IMasterModule
        {
            IMasterModule module;
            _modules.TryGetValue(typeof(T), out module);

            if (module == null)
            {
                // Try to find an assignable module
                module = _modules.Values.FirstOrDefault(m => m is T);
            }

            return module as T;
        }

        /// <summary>
        ///     Adds a module to the collection of modules
        /// </summary>
        /// <param name="module"></param>
        protected void RegisterModule(IMasterModule module)
        {
            if (_modules.ContainsKey(module.GetType()))
            {
                Logger.Error("Module is already registered: " + module);
                return;
            }

            _modules.Add(module.GetType(), module);
        }

        /// <summary>
        /// Adds a module, and initializes it (if master server has already started)
        /// </summary>
        /// <param name="module"></param>
        public void AddAndInitializeModule(IMasterModule module)
        {
            RegisterModule(module);

            if (IsStarted)
            {
                InitializeModules();
            }
        }

        protected virtual void Initialize()
        {
        }

        protected virtual void OnAwake()
        {
        }

        /// <summary>
        ///     Overrides some of the settings with values from cmd arguments
        /// </summary>
        protected virtual void ExtractCmdArgs()
        {
            ClientsPort = BmArgs.MasterClientsPort;
            MasterKey = BmArgs.MasterKey;
        }

        public class BMMasterFactories
        {
            public SessionFactory SessionFactory = (id, peer) => new Session(id, peer);
        }
    }

}