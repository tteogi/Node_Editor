using Barebones.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Displays a status of the connection
    /// </summary>
    public class ConnectionStatusView : MonoBehaviour
    {
        protected static ConnectionStatus LastStatus;

        private IClientSocket _connection;

        public Connections.ConnectionId ConnectionId;

        public Image Image;
        
        public Text Text;

        public Color UnknownColor;
        public Color OnlineColor;
        public Color ConnectingColor;
        public Color OfflineColor;

        private void Awake()
        {
            _connection = Connections.GetConnection(ConnectionId);

            _connection.OnStatusChange += UpdateStatusView;

            UpdateStatusView(_connection.Status);
        }

        protected virtual void UpdateStatusView(ConnectionStatus status)
        {
            LastStatus = status;

            switch (status)
            {
                case ConnectionStatus.Connected:
                    Image.color = OnlineColor;
                    Text.text = "Connected";
                    break;
                case ConnectionStatus.Disconnected:
                    Image.color = OfflineColor;
                    Text.text = "Offline";
                    break;
                case ConnectionStatus.Connecting:
                    Image.color = ConnectingColor;
                    Text.text = "Connecting";
                    break;
                default:
                    Image.color = UnknownColor;
                    Text.text = "Unknown";
                    break;
            }
        }

        protected virtual void OnDestroy()
        {
            _connection.OnStatusChange -= UpdateStatusView;
        }
    }
}