using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Barebones.Logging;
using Barebones.Networking;

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using LiteDB;
#endif

using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Handles player profiles within master server.
    /// Listens to changes in player profiles, and sends updates to
    /// clients of interest.
    /// Also, reads changes from game server, and applies them to players profile
    /// </summary>
    public partial class ProfilesModule : MasterModule
    {
        /// <summary>
        /// Time to pass after logging out, until profile
        /// will be removed from the lookup. Should be enough for game
        /// server to submit last changes
        /// </summary>
        public float UnloadProfileAfter = 20f;

        /// <summary>
        /// Interval, in which updated profiles will be saved to database
        /// </summary>
        public float SaveProfileInterval = 1f;

        /// <summary>
        /// Interval, in which profile updates will be sent to clients
        /// </summary>
        public float ClientUpdateInterval = 0f;

        public IProfilesDatabase Database;

        private Dictionary<string, ObservableProfile> _profiles;

        private AuthModule _auth;

        private HashSet<string> _debouncedSaves;
        private HashSet<string> _debouncedClientUpdates;

        protected BmLogger Logger = LogManager.GetLogger(typeof(ProfilesModule).ToString());

        void Awake()
        {
            if (DestroyIfExists()) return;

            AddDependency<AuthModule>();
            AddDependency<GamesModule>();

            _profiles = new Dictionary<string, ObservableProfile>();
            _debouncedSaves = new HashSet<string>();
            _debouncedClientUpdates = new HashSet<string>();
        }

        public override void Initialize(IMaster master)
        {
            var dbFactory = DatabaseAccessorFactory.Instance;
            if (dbFactory == null)
            {
                Logger.Error("Datbase accessor factory was not found in the scene");
            }

            Database = dbFactory.GetAccessor<IProfilesDatabase>();

            if (Database == null)
            {
                Logger.Error("No accessor for profiles database found");
            }

            master.SetClientHandler(new PacketHandler(BmOpCodes.ProfileRequest, HandleClientProfileRequest));

            // Auth dependency setup
            _auth = master.GetModule<AuthModule>();
            _auth.OnLogin += OnLogin;

            // Games dependency setup
            var games = master.GetModule<GamesModule>();
            games.SetGameServerHandler(new PacketHandler(BmOpCodes.ProfileRequest, HandleGameServerProfileRequest));
            games.SetGameServerHandler(new PacketHandler(BmOpCodes.ProfileUpdate, HandleGsProfileUpdates));
        }

        /// <summary>
        /// Handles a message from game server, which includes player profiles updates
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGsProfileUpdates(IIncommingMessage message)
        {
            var data = message.AsBytes();

            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    // Read profiles count
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        // Read username
                        var username = reader.ReadString();

                        // Read updates length
                        var updatesLength = reader.ReadInt32();

                        // Read updates
                        var updates = reader.ReadBytes(updatesLength);

                        try
                        {
                            ObservableProfile profile;
                            _profiles.TryGetValue(username, out profile);

                            if (profile != null)
                            {
                                profile.ApplyUpdates(updates);
                            }
                        }
                        catch (Exception e)
                        {
                            Logs.Error("Error while trying to handle profile updates from master server");
                            Logs.Error(e);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles a request from client to get profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleClientProfileRequest(IIncommingMessage message)
        {
            var profile = message.Peer.GetProperty(BmPropCodes.Profile) as ObservableProfile;

            if (profile == null)
            {
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            message.Respond(profile.ToBytes(), AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from game server to get a profile
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGameServerProfileRequest(IIncommingMessage message)
        {
            var username = message.AsString();

            ObservableProfile profile;
            _profiles.TryGetValue(username, out profile);

            if (profile == null)
            {
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            message.Respond(profile.ToBytes(), AckResponseStatus.Success);
        }

        /// <summary>
        /// Invoked, when user logs into the master server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="accountData"></param>
        private void OnLogin(ISession session, IAccountData accountData)
        {
            session.OnDisconnect += OnSessionDisconnect;

            // Create a profile
            ObservableProfile profile;

            if (_profiles.ContainsKey(accountData.Username))
            {
                // There's a profile from before, which we can use
                profile = _profiles[accountData.Username];
            }
            else
            {
                // We need to create a new one
                profile = DefaultProfileFactory(accountData.Username);
                _profiles.Add(accountData.Username, profile);
            }

            // Restore profile data from database (only if not a guest)
            if (!accountData.IsGuest)
                Database.RestoreProfile(profile);

            // Save profile property
            session.Peer.SetProperty(BmPropCodes.Profile, profile);

            // Listen to profile events
            profile.OnChanged += OnProfileChanged;
        }

        /// <summary>
        /// Invoked, when profile is changed
        /// </summary>
        /// <param name="profile"></param>
        private void OnProfileChanged(ObservableProfile profile)
        {
            // Debouncing is used to reduce a number of updates per interval to one
            // TODO make debounce lookup more efficient than using string hashet

            if (!_debouncedSaves.Contains(profile.Username))
            {
                // If profile is not already waiting to be saved
                _debouncedSaves.Add(profile.Username);
                StartCoroutine(SaveProfile(profile, SaveProfileInterval));
            }

            if (!_debouncedClientUpdates.Contains(profile.Username))
            {

                // If it's a master server
                _debouncedClientUpdates.Add(profile.Username);
                StartCoroutine(SendUpdatesToClient(profile, ClientUpdateInterval));
            }
        }

        /// <summary>
        /// Invoked, when user logs out (disconnects from master)
        /// </summary>
        /// <param name="session"></param>
        private void OnSessionDisconnect(ISession session)
        {
            // Unload profile
            StartCoroutine(UnloadProfile(session.Username, UnloadProfileAfter));
        }

        /// <summary>
        /// Saves a profile into database after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private IEnumerator SaveProfile(ObservableProfile profile, float delay)
        {
            // Wait for the delay
            yield return new WaitForSecondsRealtime(delay);

            // Check if guest. TODO DO this without session lookup
            var session = _auth.GetLoggedInSession(profile.Username);

            // Ignore if guest
            if (session != null && session.Account != null && session.Account.IsGuest)
                yield break;

            // Remove value from debounced updates
            _debouncedSaves.Remove(profile.Username);

            Database.UpdateProfile(profile);
        }

        /// <summary>
        /// Collets changes in the profile, and sends them to client after delay
        /// </summary>
        /// <param name="profile"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private IEnumerator SendUpdatesToClient(ObservableProfile profile, float delay)
        {
            // Wait for the delay
            if (delay > 0.01f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            // Remove value from debounced updates
            _debouncedClientUpdates.Remove(profile.Username);

            var session = _auth.GetLoggedInSession(profile.Username);

            if (session == null)
                yield break;

            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    profile.GetUpdates(writer);
                }

                session.Peer.SendMessage(MessageHelper.Create(BmOpCodes.ProfileUpdate, ms.ToArray()),
                    DeliveryMethod.ReliableSequenced);
            }
        }

        /// <summary>
        /// Coroutine, which unloads profile after a period of time
        /// </summary>
        /// <param name="username"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        private IEnumerator UnloadProfile(string username, float delay)
        {
            // Wait for the delay
            yield return new WaitForSecondsRealtime(delay);

            // If user is not actually logged in, remove the profile
            if (_auth.IsLoggedIn(username))
                yield break;

            ObservableProfile profile;
            _profiles.TryGetValue(username, out profile);

            if (profile == null)
                yield break;

            // Remove profile
            _profiles.Remove(username);

            // Remove listeners
            profile.OnChanged -= OnProfileChanged;
        }
    }
}