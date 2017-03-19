using System;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Partial profiles module class, which holds most of
    /// the static functionality.
    /// Mainly helper functions for clients and Game Servers 
    /// to use
    /// </summary>
    public partial class ProfilesModule
    {
        public delegate ObservableProfile ProfileFactory(string username);

        private static ProfileFactory _factory;

        /// <summary>
        /// Current profile of player who is authorized, and requested
        /// to get profile <see cref="GetClientProfile"/> at least one
        /// </summary>
        public static ObservableProfile Profile { get; private set; }

        /// <summary>
        /// Event, which is invoked when authorized client first
        /// loads the profile. Called only once per authenticated session
        /// </summary>
        public static event Action<ObservableProfile> OnProfileLoaded;

        static ProfilesModule()
        {
            Connections.ClientToMaster.SetHandler(new PacketHandler(BmOpCodes.ProfileUpdate, HandleProfileUpdate));
        }

        /// <summary>
        /// Invoked on client.
        /// Handles profile update message from master server
        /// </summary>
        /// <param name="message"></param>
        private static void HandleProfileUpdate(IIncommingMessage message)
        {
            if (Profile == null)
                return;

            Profile.ApplyUpdates(message.AsBytes());
        }

        /// <summary>
        /// Sets a default profiles factory
        /// </summary>
        /// <param name="factory"></param>
        public static void SetFactory(ProfileFactory factory)
        {
            _factory = factory;
        }

        public static ObservableProfile DefaultProfileFactory(string username)
        {
            if (_factory == null)
            {
                Logs.Error("Default profile factory is not set. " +
                               "Empty profile will be created instead");
                return new ObservableProfile(username);
            }
            return _factory.Invoke(username);
        }

        /// <summary>
        /// Invoked on client.
        /// Requests to get a profile of user, who is currently logged in.
        /// Once profile is received, callback is invoked. Callback is invoked with
        /// "null", if no profile is received. Uses default profiles factory, set by 
        /// <see cref="SetFactory"/>
        /// </summary>
        /// <param name="callback"></param>
        public static void GetClientProfile(Action<ObservableProfile> callback)
        {
            GetClientProfile(callback, DefaultProfileFactory);
        }

        /// <summary>
        /// Invoked on client.
        /// Requests to get a profile of user, who is currently logged in.
        /// Once profile is received, callback is invoked. Callback is invoked with
        /// "null", if no profile is received
        /// </summary>
        public static void GetClientProfile(Action<ObservableProfile> callback, ProfileFactory factory)
        {
            if (!Auth.IsLoggedIn)
            {
                Logs.Error("Can't load a profile if you're not logged in");
                callback.Invoke(null);
                return;
            }

            if (Profile != null)
            {
                callback.Invoke(Profile);
            }

            GetProfile(Auth.PlayerData.Username, Connections.ClientToMaster, profile =>
            {
                Profile = profile;
                Auth.OnLoggedOut += OnLoggedOut;
                if (profile != null && OnProfileLoaded != null)
                {
                    OnProfileLoaded.Invoke(profile);
                }

                callback.Invoke(profile);
            }, factory);
        }

        private static void OnLoggedOut()
        {
            Profile = null;
            Auth.OnLoggedOut -= OnLoggedOut;
        }

        /// <summary>
        /// Invoked on games server. Returns a profile of specified player.
        /// Uses default profiles factory, set by <see cref="SetFactory"/> 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="connection"></param>
        /// <param name="callback"></param>
        public static void GetProfile(string username, IClientSocket connection,
            Action<ObservableProfile> callback)
        {
            GetProfile(username, connection, callback, DefaultProfileFactory);
        }

        /// <summary>
        /// Invoked on games server. Returns a profile of specified player
        /// </summary>
        /// <param name="username"></param>
        /// <param name="connection"></param>
        /// <param name="callback"></param>
        /// <param name="profileFactory"></param>
        public static void GetProfile(string username, IClientSocket connection, 
            Action<ObservableProfile> callback, ProfileFactory profileFactory)
        {
            if (username == null)
                username = "";

            connection.Peer.SendMessage(MessageHelper.Create(BmOpCodes.ProfileRequest, username), (status, response) =>
            {
                if (status == AckResponseStatus.Success)
                {
                    var profile = profileFactory(username);
                    profile.FromBytes(response.AsBytes());
                    callback.Invoke(profile);
                    return;
                }

                callback.Invoke(null);
            });
        }
    }
}