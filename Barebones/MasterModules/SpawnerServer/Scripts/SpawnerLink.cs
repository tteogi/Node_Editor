using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This represents what Master server knows about spawner server.
    ///     Instance of this object will be created on master server, 
    ///     when spawner server registers to it.
    /// </summary>
    public class SpawnerLink
    {
        /// <summary>
        ///     How often a working task check if it has requests to launch a game server
        /// </summary>
        public static int WorkerFrequencyMillis = 100;

        /// <summary>
        ///     Spawner server peer
        /// </summary>
        public readonly IPeer Peer;

        private SpawnTask _currentlySpawning;

        private int _passGenerator;

        private readonly Queue<SpawnTask> _queue;
        private readonly object _spawnLock = new object();

        protected Dictionary<int, IRegisteredGameServer> RegisteredGames;

        protected Dictionary<int, GameProcessInfoPacket> GameProcesses;

        public SpawnerLink(int id, SpawnerRegisterPacket data, SpawnersModule module, IPeer serverPeer)
        {
            Module = module;
            Id = id;
            Data = data;
            Properties = data.Properties;
            Peer = serverPeer;

            GameProcesses = new Dictionary<int, GameProcessInfoPacket>();
            RegisteredGames = new Dictionary<int, IRegisteredGameServer>();
            _queue = new Queue<SpawnTask>();

            MaxGames = data.MaxServers;

            // Start worker coroutine
            Module.StartCoroutine(StartWorker());
        }

        public SpawnersModule Module { get; private set; }

        /// <summary>
        ///     Properties of the spawner
        /// </summary>
        public Dictionary<string, string> Properties { get; private set; }

        /// <summary>
        ///     Spawner id
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        ///     RegistrationPacket, which spawner server sent to register to master server
        /// </summary>
        public SpawnerRegisterPacket Data { get; private set; }

        /// <summary>
        ///     How many game instances are running in this spawner server
        /// </summary>
        public int GamesRunning { get; private set; }

        /// <summary>
        ///     Maximum number of games, allowed to run at this spawner server
        ///     simultaneously
        /// </summary>
        public int MaxGames { get; private set; }

        public IEnumerable<IRegisteredGameServer> SpawnedGameServers { get { return RegisteredGames.Values; } }

        /// <summary>
        ///     Queues a spawn request if server is not yet full
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="customArgs">Custom arguments that will be added to the process</param>
        /// <returns>null, if server is full or queue is too long</returns>
        public SpawnTask OrderSpawn(Dictionary<string, string> properties, string customArgs = "")
        {
            // Spawner is busy
            if (_queue.Count + GamesRunning >= MaxGames)
                return null;

            // Queue is full
            if (_queue.Count >= Module.SpawnerQueueLength)
                return null;

            var task = Module.CreateSpawnTask(Module.GenerateSpawnId(), this, properties, customArgs);
            _queue.Enqueue(task);
            
            Module.RegisterSpawnTask(task);

            return task;
        }

        /// <summary>
        ///     Returns a number of how many more game servers the spawner can spawn
        /// </summary>
        /// <returns></returns>
        public int GetFreeSlotsCount()
        {
            return MaxGames - _queue.Count - GamesRunning;
        }

        /// <summary>
        ///     Checks a queue every <see cref="WorkerFrequencyMillis" />, and
        ///     if the queue is not empty and there's nothing spawning at the moment,
        ///     starts spawning a new instance
        /// </summary>
        private IEnumerator StartWorker()
        {
            // Wait 10 seconds for connection
            for (int i = 0; i < 100; i++)
            {
                if (Peer.IsConnected) break;
                yield return new WaitForSeconds(0.1f);
            }

            while (Peer.IsConnected)
            {
                yield return new WaitForSeconds(WorkerFrequencyMillis/1000f);

                lock (_spawnLock)
                {
                    // Ignore if nothing's in the queue
                    if (_queue.Count == 0)
                        continue;

                    if ((_currentlySpawning != null) && !_currentlySpawning.IsStartingProcess())
                    {
                        // Previous instance is already spawned
                        _currentlySpawning = null;
                    }

                    if (_currentlySpawning == null)
                    {
                        // Nothing is being spawned, so we can start spawning a new instance
                        var task = _queue.Dequeue();

                        // If this task is already started (probably force-started without the queue)
                        if (task.Status > SpawnGameStatus.None)
                            continue;

                        _currentlySpawning = task;

                        try
                        {
                            task.Start();
                        }
                        catch (Exception e)
                        {
                            Logs.Error("Exception thrown while trying to start a spawn task");
                            Logs.Error(e);
                            task.Abort("Internal error");
                        }
                    }
                }
            }

            Module.Logger.Debug("Spawner link worker stopped");
        }

        /// <summary>
        ///     Updates the link to represent a "fresh" state of spawner server
        ///     This is called when spawner server sends an update.
        /// </summary>
        /// <param name="data"></param>
        public virtual void UpdateState(SpawnerUpdatePacket data)
        {
            GamesRunning = data.RunningGameServersCount;
        }

        /// <summary>
        /// Adds game server to a list of server that this spawner
        /// has spawned.
        /// </summary>
        public void AddRegisteredGame(int spawnId, IRegisteredGameServer gameServer)
        {
            RegisteredGames.Add(spawnId, gameServer);

            Action<IRegisteredGameServer> onDisconnected = null;
            onDisconnected = (dcedServer) =>
            {
                RegisteredGames.Remove(spawnId);
                gameServer.Disconnected -= onDisconnected;
            };
            gameServer.Disconnected += onDisconnected;
        }
        
        public void RequestProcessesInfo(Action<List<GameProcessInfoPacket>> callback)
        {
            Peer.SendMessage(MessageHelper.Create(BmOpCodes.GetGameProcesses), (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    Module.Logger.Warn("Failed to retrieve processes from spawner: " + Id);
                    callback.Invoke(new List<GameProcessInfoPacket>());
                    return;
                }

                var data = response.DeserializeList<GameProcessInfoPacket>().ToList();
                callback.Invoke(data);
            });
        }

        /// <summary>
        /// Returns a list of processes that this spawner has spawned
        /// </summary>
        /// <returns></returns>
        public List<GameProcessInfoPacket> GetSpawnedProcesses()
        {
            return GameProcesses.Values.ToList();
        }

        public IRegisteredGameServer GetGameServerBySpawnId(int spawnId)
        {
            IRegisteredGameServer server;
            RegisteredGames.TryGetValue(spawnId, out server);
            return server;
        }

        /// <summary>
        /// Adds process info to the list of processes that this
        /// spawner has spawnerd
        /// </summary>
        /// <param name="processInfo"></param>
        public void AddGameProcess(GameProcessInfoPacket processInfo)
        {
            GameProcesses[processInfo.SpawnId] = processInfo;
        }

        /// <summary>
        /// Removes a process of a given spawn id from a list of 
        /// game processes that this spawner has spawned
        /// </summary>
        /// <param name="spawnId"></param>
        public void RemoveGameProcess(int spawnId)
        {
            GameProcesses.Remove(spawnId);
        }

        /// <summary>
        /// Returns true, if this spawner is running a process with
        /// a given spawn id
        /// </summary>
        /// <param name="spawnId"></param>
        /// <returns></returns>
        public bool ContainsProcess(int spawnId)
        {
            return GameProcesses.ContainsKey(spawnId);
        }

        public void RequestProcessKill(int spawnId, Action<bool> callback)
        {
            var killMsg = MessageHelper.Create(BmOpCodes.KillProcess, spawnId);
            Peer.SendMessage(killMsg, (status, response) =>
            {
                callback.Invoke(status == AckResponseStatus.Success);
            });
        }
    }
}