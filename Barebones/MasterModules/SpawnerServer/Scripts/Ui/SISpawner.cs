using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer.Ui
{
    public class SISpawner : MonoBehaviour
    {
        public Text SpawnerId;
        public Text Ip;
        public Text Region;
        public Text GamesCount;

        public LayoutGroup GamesList;

        public SpawnersInspector Inspector { get; set; }

        public void OnKillClick()
        {
        }

        public void Setup(SpawnersInspectorPacket.SISpawnerData spawnerData)
        {
            SpawnerId.text = spawnerData.SpawnerId.ToString();
            Ip.text = spawnerData.Ip;
            Region.text = spawnerData.Region;
            GamesCount.text = spawnerData.GameServers.Count + "/" + spawnerData.MaxGameServers;
        }
    }
}