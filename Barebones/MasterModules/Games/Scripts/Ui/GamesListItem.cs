using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a single row in the games list
    /// </summary>
    public class GamesListItem : MonoBehaviour
    {
        public GameInfoPacket RawData { get; protected set; }
        public Image BgImage;
        public Color DefaultBgColor;
        public GamesList ListView;
        public GameObject LockImage;
        public Text MapName;
        public Text Name;
        public Text Online;

        public Color SelectedBgColor;

        public string UnknownMapName = "Unknown";

        public int GameId { get; private set; }
        public bool IsSelected { get; private set; }
        public bool IsLobby { get; private set; }

        public bool IsPasswordProtected
        {
            get { return RawData.IsPasswordProtected; }
        }

        // Use this for initialization
        private void Awake()
        {
            BgImage = GetComponent<Image>();
            DefaultBgColor = BgImage.color;

            SetIsSelected(false);
        }

        public void SetIsSelected(bool isSelected)
        {
            IsSelected = isSelected;
            BgImage.color = isSelected ? SelectedBgColor : DefaultBgColor;
        }

        public void Setup(GameInfoPacket data)
        {
            RawData = data;
            IsLobby = data.IsLobby;
            SetIsSelected(false);
            Name.text = data.Name;
            GameId = data.Id;
            LockImage.SetActive(data.IsPasswordProtected);
            Online.text = string.Format("{0}/{1}", data.OnlinePlayers, data.MaxPlayers);
            MapName.text = data.Properties.ContainsKey("map") ? data.Properties["map"] : UnknownMapName;
        }

        public void OnClick()
        {
            ListView.Select(this);
        }
    }
}