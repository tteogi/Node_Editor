using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Barebones.MasterServer;

namespace Barebones.MasterServer
{
    /// <summary>
    /// This component should be in both client and server.
    /// It contains static data of the game, such as item templates 
    /// and <see cref="ProfileFactory"/>, which is necessary to construct profiles
    /// on both client and server.
    /// </summary>
    public class RoomsDemo : MonoBehaviour
    {
        private static Dictionary<string, Sprite> _spriteLookup;

        // List of available game items
        public static Dictionary<string, AwesomeItemTemplate> ItemTemplates = new Dictionary<string, AwesomeItemTemplate>()
        {
            {"Knife",  new AwesomeItemTemplate("Knife", 1, "knife")},
            {"Lollipop",  new AwesomeItemTemplate("Lollipop", 2, "lollipop")},
            {"Fork",  new AwesomeItemTemplate("Fork", 4, "fork")},
            {"Bow",  new AwesomeItemTemplate("Bow", 6, "bow")},
            {"Carrot",  new AwesomeItemTemplate("Carrot", 1, "carrot")},
        };

        void Awake()
        {
            // Set profile factory, so that it can be used on both client and server
            ProfilesModule.SetFactory(ProfileFactory);
        }

        // Use this for initialization
        void Start()
        {

        }

        /// <summary>
        /// Returns a template of an item
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static AwesomeItemTemplate GetItemTemplate(string name)
        {
            AwesomeItemTemplate result;
            ItemTemplates.TryGetValue(name, out result);
            return result;
        }

        /// <summary>
        /// Returns a sprite of a given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Sprite GetSprite(string name)
        {
            if (_spriteLookup == null)
            {
                // Sprites lookup has not been set up
                _spriteLookup = new Dictionary<string, Sprite>();
                var sprites = Resources.LoadAll<Sprite>("Textures/awesome_game");
                foreach (var sprite in sprites)
                {
                    _spriteLookup.Add(sprite.name, sprite);
                }
            }

            Sprite result;
            _spriteLookup.TryGetValue(name, out result);
            return result;
        }

        /// <summary>
        /// This method will be called when constructing profiles,
        /// in both client and server
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public static ObservableProfile ProfileFactory(string username)
        {
            var profile = new ObservableProfile(username);

            // Adding profile variables with default values
            profile.AddProperty(new ObservableInt(RoomsDemoProfileKeys.Coins, 10));
            profile.AddProperty(new ObservableString(RoomsDemoProfileKeys.Weapon, "Carrot"));
            profile.AddProperty(new ObservableDictionaryInt(RoomsDemoProfileKeys.Inventory));
            profile.AddProperty(new ObservableDictStringFloat(333, new Dictionary<string, float>()));

            return profile;
        }
    }

}
