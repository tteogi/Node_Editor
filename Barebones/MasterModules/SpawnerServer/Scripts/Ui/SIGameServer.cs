using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.UI;

namespace Barebones.MasterServer.Ui
{
    public class SIGameServer : MonoBehaviour
    {
        public Text SpawnId;
        public Text Name;
        public Text Players;
        public Text CmdArgs;

        public SpawnersInspector Inspector { get; set; }

        public GameObject MoreInfo;

        public SpawnersInspectorPacket.SIGameServerData Data { get; private set; }

        public void OnKillClick()
        {
            Inspector.KillGameServer(this);
        }

        public void Setup(SpawnersInspectorPacket.SIGameServerData gameData)
        {
            Data = gameData;
            SpawnId.text = gameData.SpawnId.ToString();
            Name.text = gameData.Name + " ["+gameData.GameId+"]";
            Players.text = gameData.CurrentPlayers + "/" + gameData.MaxPlayers;
            CmdArgs.text = gameData.CmdArgs;
        }

        public void OnExpandToggleClick()
        {
            MoreInfo.gameObject.SetActive(!MoreInfo.gameObject.activeSelf);
        }
    }
}