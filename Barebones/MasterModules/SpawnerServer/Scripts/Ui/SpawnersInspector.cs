using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer.Ui
{
    public class SpawnersInspector : MonoBehaviour
    {
        public SISpawner SpawnerPrefab;
        public SIGameServer GameServerPrefab;

        public Text SpawnersCount;
        public Text GamesCount;
        public Text PlayersCount;

        public GenericPool<SISpawner> SpawnersPool;
        public GenericPool<SIGameServer> GamesPool;

        private Dictionary<int, SISpawner> _visibleSpawners;
        private Dictionary<int, SIGameServer> _visibleGames;

        public LayoutGroup SpawnersList;
        public GameObject StatsView;
        public GameObject NoDataView;

        void Awake()
        {
            // Remove the game server temporalily
            GameServerPrefab.transform.SetParent(SpawnersList.transform);

            GamesPool = new GenericPool<SIGameServer>(GameServerPrefab, true);
            SpawnersPool = new GenericPool<SISpawner>(SpawnerPrefab, true);

            _visibleSpawners = new Dictionary<int, SISpawner>();
            _visibleGames = new Dictionary<int, SIGameServer>();
            StatsView.gameObject.SetActive(false);
        }

        public void OnRefreshClick()
        {
            var msg = MessageHelper.Create(BmOpCodes.SpawnerInspectorDataRequest);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    DrawData(null);
                    DialogBoxView.ShowError("Failed to get inspector data: " + response.AsString());
                    return;
                }

                var data = response.DeserializePacket(new SpawnersInspectorPacket());

                DrawData(data);
            });
        }

        protected void DrawData(SpawnersInspectorPacket data)
        {
            StoreAllInPools();

            if (data == null)
            {
                StatsView.gameObject.SetActive(false);
                NoDataView.gameObject.SetActive(true);
                SpawnersCount.text = "N/A";
                GamesCount.text = "N/A";
                PlayersCount.text = "N/A";
                return;
            }

            NoDataView.gameObject.SetActive(false);
            StatsView.gameObject.SetActive(true);

            // Set the stats
            SpawnersCount.text = data.Spawners.Count.ToString();
            GamesCount.text = data.Spawners.Sum(s => s.GameServers.Count).ToString();
            PlayersCount.text = data.Spawners.Sum(s => s.GameServers.Sum(g => g.CurrentPlayers)).ToString();

            foreach (var spawnerData in data.Spawners)
            {
                // Setup spawner
                var spawner = SpawnersPool.GetResource();
                spawner.Inspector = this;
                spawner.Setup(spawnerData);
                spawner.transform.SetParent(SpawnersList.transform, false);
                spawner.gameObject.SetActive(true);
                spawner.transform.SetAsLastSibling();
                _visibleSpawners.Add(spawnerData.SpawnerId, spawner);

                foreach (var gameData in spawnerData.GameServers)
                {
                    // Setup game server
                    var game = GamesPool.GetResource();
                    game.Inspector = this;
                    game.Setup(gameData);
                    game.transform.SetParent(spawner.GamesList.transform, false);
                    game.gameObject.SetActive(true);
                    game.transform.SetAsLastSibling();
                    game.MoreInfo.SetActive(false);
                    _visibleGames.Add(gameData.SpawnId, game);
                }
            }
        }

        void OnDisable()
        {
            DrawData(null);
        }

        public void StoreAllInPools()
        {
            foreach (var val in _visibleGames.Values)
            {
                // Remove from spawners temporalily
                val.transform.SetParent(SpawnersList.transform);
                GamesPool.Store(val);
            }
            _visibleGames.Clear();

            foreach (var val in _visibleSpawners.Values)
            {
                SpawnersPool.Store(val);
            }
            _visibleSpawners.Clear();
        }

        /// <summary>
        /// Sends a request to kill a game server
        /// </summary>
        /// <param name="gameServer"></param>
        public void KillGameServer(SIGameServer gameServer)
        {
            var promise = BmEvents.Channel.FireWithPromise(BmEvents.Loading, "Requesting game server termination");

            var msg = MessageHelper.Create(BmOpCodes.KillProcess, gameServer.Data.SpawnId);
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                promise.Finish();

                if (status != AckResponseStatus.Success)
                {
                    DialogBoxView.ShowError("Failed to kill game server: " + response.AsString());
                    return;
                }

                GamesPool.Store(gameServer);
            });
        }
    }
}