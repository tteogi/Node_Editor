using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This is a quick and dirty component,
    ///     which you can use to check status of what servers are running
    /// </summary>
    public class MasterStatusHud : MonoBehaviour
    {
        private readonly string _colorText = "<color={0}>{1}</color>";
        public Text ClientStatus;

        public bool EditorOnly = false;
        public Text GameServerStatus;
        public string InfoColor = "#52BBF9FF";
        public string OfflineColor = "#525252FF";

        public string OnlineColor = "#B4E83BFF";
        public Text ServersStatus;

        public float UpdateIntervalSeconds = 0.2f;

        private void Awake()
        {
#if !UNITY_EDITOR
            if (EditorOnly) {
                gameObject.SetActive(false);
            }
#endif
        }

        // Use this for initialization
        private void OnEnable()
        {
            StartCoroutine(StartUpdating());
        }

        // Update is called once per frame
        private IEnumerator StartUpdating()
        {
            while (true)
            {
                UpdateServersStatus();
                UpdateGameServerStatus();
                UpdateClientStatus();

                yield return new WaitForSeconds(UpdateIntervalSeconds);
            }
        }

        private void UpdateServersStatus()
        {
            ServersStatus.text = string.Format(_colorText + "\n", Master.IsStarted ? OnlineColor : OfflineColor,
                "Master Server");

            var isSpawnerStarted = (SpawnerServer.Instance != null) && SpawnerServer.Instance.IsStarted;
            ServersStatus.text += string.Format(_colorText, isSpawnerStarted ? OnlineColor : OfflineColor,
                "Spawner Server");
        }

        private void UpdateGameServerStatus()
        {
            if (GamesModule.CurrentGame == null)
            {
                GameServerStatus.text = string.Format(_colorText, OfflineColor, "Not Started/Registered");
                return;
            }

            GameServerStatus.text = GetColorText(OnlineColor, "Running");
            GameServerStatus.text += GetColorText(InfoColor,
                "Players online: " + GamesModule.CurrentGame.OnlineUsersCount, false);
        }

        private string GetColorText(string color, string text, bool addNewline = true)
        {
            return string.Format(_colorText, color, text) + (addNewline ? "\n" : "");
        }

        private void UpdateClientStatus()
        {
            var isConnectedToMaster = Connections.ClientToMaster.IsConnected;

            if (!isConnectedToMaster)
            {
                ClientStatus.text = string.Format(_colorText, OfflineColor, "Not Connected to Master");
                return;
            }

            ClientStatus.text = GetColorText(OnlineColor, "Connected to Master");
            ClientStatus.text += GetColorText(Auth.IsLoggedIn ? OnlineColor : OfflineColor, "Authorized", false);
        }
    }
}