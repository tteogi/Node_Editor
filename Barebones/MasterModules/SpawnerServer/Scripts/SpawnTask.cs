using System;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a request to spawn a single game server.
    ///     This object is created in Master Server
    /// </summary>
    public class SpawnTask
    {
        protected bool IsAborted;
        protected bool IsProcessStarted;
        protected bool IsServerReady;
        protected bool IsStarting;

        private SpawnGameStatus _status;

        public event Action<SpawnGameStatus> OnStatusChange;
        public event Action<IRegisteredGameServer> OnGameServerRegistered;
        public event Action<IRegisteredGameServer> OnGameServerOpened;

        protected List<Action<SpawnTask>> WhenDoneCallbacks;

        public SpawnTask(int spawnId, SpawnerLink spawner, Dictionary<string, string> properties, 
            string customArgs = "")
        {
            SpawnId = spawnId;
            Spawner = spawner;
            Properties = properties;
            CustomArgs = customArgs;
            WhenDoneCallbacks = new List<Action<SpawnTask>>();
        }

        public string CustomArgs { get; protected set; }

        public SpawnGameStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;

                if (OnStatusChange != null)
                    OnStatusChange.Invoke(_status);

                if (_status >= SpawnGameStatus.Open || _status == SpawnGameStatus.Aborted)
                    NotifyDoneListeners();
            }
        }

        public int SpawnId { get; private set; }

        public SpawnerLink Spawner { get; private set; }
        public Dictionary<string, string> Properties { get; private set; }

        public IRegisteredGameServer GameServer { get; protected set; }
        public IPeer GameServerPeer { get; protected set; }

        public void SetStatus(SpawnGameStatus status)
        {
            Status = status;
        }

        /// <summary>
        ///     Returns true if spawner server is working on this request
        /// </summary>
        /// <returns></returns>
        public bool IsStartingProcess()
        {
            return IsStarting;
        }

        /// <summary>
        ///     Returns true if request is aborted or fully ready
        /// </summary>
        /// <returns></returns>
        public bool IsDone()
        {
            return IsAborted || !IsServerReady || IsProcessStarted;
        }

        /// <summary>
        ///     Sends a request to spawner server, to actually start the
        ///     game server instance
        /// </summary>
        public void Start()
        {
            if (IsStarting)
                return;

            if (!Spawner.Peer.IsConnected)
            {
                Abort("Connection with spawner lost");
                return;
            }

            if (IsAborted)
            {
                IsStarting = false;
                return;
            }

            Status = SpawnGameStatus.StartingProcess;

            IsStarting = true;

            var data = Spawner.Module.CreateSpawnRequestPacket(this);
            var spawnMsg = MessageHelper.Create(BmOpCodes.SpawnGameServer, data.ToBytes());

            // Send request to start
            Spawner.Peer.SendMessage(spawnMsg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    // If failed
                    IsStarting = false;
                    Abort("Spawn Server request not handled. Status: " + status);
                }
            });
        }

        /// <summary>
        ///     After game instance is started, it sends a notification to master server,
        ///     and after the notification is received, this method will be called. Most of the time,
        ///     this is where you'll want to start setting up your game server.
        ///     Default implementation stores a peer
        /// </summary>
        /// <param name="peer">Game server peer</param>
        public virtual void OnProcessStarted(IPeer peer)
        {
            Status = SpawnGameStatus.WaitingToGetReady;
            IsProcessStarted = true;
            IsStarting = false;
            GameServerPeer = peer;
        }

        public virtual void OnServerRegistered(int spawnId, IRegisteredGameServer gameServer)
        {
            GameServer = gameServer;
            Status = SpawnGameStatus.Ready;
            IsServerReady = true;

            // If already open
            if (gameServer.IsOpen)
            {
                // If already opened
                OnOpen();
            }
            else
            {
                // Wait for it to be opened
                gameServer.OnOpened += OnOpen;
            }

            Spawner.AddRegisteredGame(spawnId, gameServer);

            if (OnGameServerRegistered != null)
                OnGameServerRegistered.Invoke(gameServer);
        }

        public void OnOpen()
        {
            Status = SpawnGameStatus.Open;

            if (OnGameServerOpened != null)
            {
                OnGameServerOpened.Invoke(GameServer);
            }
        }

        /// <summary>
        /// Callback will be called when spawn task is aborted or completed 
        /// (game server is opened)
        /// </summary>
        /// <param name="callback"></param>
        public SpawnTask WhenDone(Action<SpawnTask> callback)
        {
            WhenDoneCallbacks.Add(callback);
            return this;
        }

        /// <summary>
        ///     Returns a copy of properties that creator gave in the initial request
        /// </summary>
        /// <returns>Returns a copy of properties that creator gave</returns>
        [Obsolete("Use GetProperties() instead")]
        public Dictionary<string, string> GetUserProperties()
        {
            return GetProperties();
        }

        /// <summary>
        ///     Returns a copy of properties that creator gave in the initial request
        /// </summary>
        /// <returns>Returns a copy of properties that were given in the process of creating a task</returns>
        public Dictionary<string, string> GetProperties()
        {
            return Properties.ToDictionary(p => p.Key, p => p.Value);
        }

        /// <summary>
        /// Sends a message to spawner server to abort spawning or kill
        /// current unity process
        /// </summary>
        /// <param name="reason"></param>
        public void Abort(string reason)
        {
            if (Status < SpawnGameStatus.Open)
                Logs.Warn("Spawn task aborted: " + reason);

            IsAborted = true;
            try
            {
                if (IsStarting || IsProcessStarted)
                {
                    Status = SpawnGameStatus.Aborting;

                    // If spawner server started working on this, we need to notify it to stop
                    var killMsg = MessageHelper.Create(BmOpCodes.KillProcess, SpawnId);
                    Spawner.Peer.SendMessage(killMsg, (status, response) =>
                    {
                        IsStarting = false;
                        Status = SpawnGameStatus.Aborted;
                    });
                }
                else
                {
                    Status = SpawnGameStatus.Aborted;
                }
            }
            catch (Exception e)
            {
                Logs.Error("Exception while aborting a spawn task");
                Logs.Error(e);
            }
        }

        protected void NotifyDoneListeners()
        {
            foreach (var callback in WhenDoneCallbacks)
            {
                callback.Invoke(this);
            }

            WhenDoneCallbacks.Clear();
        }
    }
}