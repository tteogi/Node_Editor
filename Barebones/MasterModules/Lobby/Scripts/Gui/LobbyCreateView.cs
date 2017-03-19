using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents a simple window, which demonstrates 
    /// how lobbies can be created
    /// </summary>
    public class LobbyCreateView : MonoBehaviour
    {
        public Dropdown TypeDropdown;
        public Dropdown MapDropdown;
        public InputField Name;
        public LobbyView LobbyView;

        /// <summary>
        /// List of available lobby factories
        /// </summary>
        public List<CustomPair> LobbyFactories = new List<CustomPair>();

        /// <summary>
        /// A list of maps
        /// </summary>
        public List<CustomPair> Maps = new List<CustomPair>();

        protected virtual void Awake()
        {
            LobbyFactories.Add(new CustomPair("DEATHMATCH", "Deathmatch 10"));
            LobbyFactories.Add(new CustomPair("1 VS 1", "1 vs 1"));
            LobbyFactories.Add(new CustomPair("2 VS 2 VS 4", "2 vs 2 vs 4"));
            LobbyFactories.Add(new CustomPair("3 VS 3 AUTO", "3 vs 3 auto"));

            Maps.Add(new CustomPair("GameRoom", "Default"));
        }

        protected virtual void Start()
        {
            TypeDropdown.ClearOptions();
            TypeDropdown.AddOptions(LobbyFactories.Select(t => t.Value).ToList());

            MapDropdown.ClearOptions();
            MapDropdown.AddOptions(Maps.Select(t => t.Value).ToList());
        }

        /// <summary>
        /// Invoked, when user clicks a "Create" button
        /// </summary>
        public void OnCreateClick()
        {
            var properties = new Dictionary<string, string>()
            {
                {GameProperty.GameName, Name.text },
                {GameProperty.SceneName, GetSelectedMap() },
                {GameProperty.MapName, MapDropdown.captionText.text}
            };

            var loadingPromise = BmEvents.Channel.FireWithPromise(BmEvents.Loading, "Sending request");

            // Send a request to create the lobby
            LobbiesModule.CreateLobby(GetSelectedFactory(), properties, (id, error) =>
            {
                loadingPromise.Finish();

                if (!id.HasValue)
                {
                    return;
                }

                LobbyView.gameObject.SetActive(true);
                gameObject.SetActive(false);
            });
        }


        /// <summary>
        /// Translates factory selection into the
        /// actual factory string representation
        /// </summary>
        public string GetSelectedFactory()
        {
            var text = TypeDropdown.captionText.text;
            return LobbyFactories.FirstOrDefault(m => m.Value == text).Key;
        }

        /// <summary>
        /// Translates map selection into the
        /// scene name
        /// </summary>
        public string GetSelectedMap()
        {
            var text = MapDropdown.captionText.text;
            return Maps.FirstOrDefault(m => m.Value == text).Key;
        }

        [Serializable]
        public class CustomPair
        {
            public string Key;
            public string Value;

            public CustomPair(string key, string value)
            {
                Key = key;
                Value = value;
            }

        }
    }
}