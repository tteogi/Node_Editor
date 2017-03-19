using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Game creation window
    /// </summary>
    public class CreateGameView : ClientBehaviour
    {
        public Dropdown Map;

        public Image MapImage;

        public List<MapSelection> Maps;
        public int MaxNameLength = 14;
        public Dropdown MaxPlayers;

        public int MaxPlayersLowerLimit = 2;
        public int MaxPlayersUpperLimit = 10;

        public int MinNameLength = 3;

        public CreateGameProgressView ProgressView;
        public InputField RoomName;

        protected override void OnAwake()
        {
            ProgressView = ProgressView ?? FindObjectOfType<CreateGameProgressView>();
            Map.ClearOptions();
            Map.AddOptions(Maps.Select(m => new Dropdown.OptionData(m.Name)).ToList());

            OnMapChange();
        }

        public void OnCreateClick()
        {
            if (ProgressView == null)
            {
                Logs.Error("You need to set a ProgressView");
                return;
            }

            if (!IsLoggedIn)
            {
                ShowError("You must be logged in to create a room");
                return;
            }

            var name = RoomName.text.Trim();

            if (string.IsNullOrEmpty(name) || (name.Length < MinNameLength) || (name.Length > MaxNameLength))
            {
                ShowError(string.Format("Invalid length of game name, shoul be between {0} and {1}", MinNameLength,
                    MaxNameLength));
                return;
            }

            var maxPlayers = 0;
            int.TryParse(MaxPlayers.captionText.text, out maxPlayers);

            if ((maxPlayers < MaxPlayersLowerLimit) || (maxPlayers > MaxPlayersUpperLimit))
            {
                ShowError(string.Format("Invalid number of max players. Value should be between {0} and {1}",
                    MaxPlayersLowerLimit, MaxPlayersUpperLimit));
                return;
            }

            var settings = new Dictionary<string, string>
            {
                {GameProperty.MaxPlayers, maxPlayers.ToString()},
                {GameProperty.GameName, name},
                {GameProperty.MapName, GetSelectedMap().Name},
                {GameProperty.SceneName, GetSelectedMap().Scene}
            };

            SpawnersModule.CreateUserGame(settings, (request, error) =>
            {
                if (request == null)
                {
                    ProgressView.gameObject.SetActive(false);
                    Events.Fire(BmEvents.ShowDialogBox,
                        DialogBoxData.CreateError("Failed to create a game: " + error));
                    return;
                }

                ProgressView.Display(request);
            });
        }

        public MapSelection GetSelectedMap()
        {
            var text = Map.captionText.text;
            return Maps.FirstOrDefault(m => m.Name == text);
        }

        public void OnMapChange()
        {
            var selected = GetSelectedMap();

            if (selected == null)
            {
                Logs.Error("Invalid map selection");
                return;
            }

            MapImage.sprite = selected.Sprite;
        }

        private void ShowError(string error)
        {
            Events.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(error));
        }

        [Serializable]
        public class MapSelection
        {
            public string Name;
            public string Scene;
            public Sprite Sprite;
        }
    }
}