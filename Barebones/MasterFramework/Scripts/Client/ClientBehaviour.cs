using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This is a helper class (MonoBehaviour extension), that has some
    ///     useful methods that can be overriden. Most useful when designing UI
    ///     components
    /// </summary>
    public class ClientBehaviour : MonoBehaviour
    {
        /// <summary>
        ///     True, if user is logged in
        /// </summary>
        protected bool IsLoggedIn
        {
            get { return Auth.IsLoggedIn; }
        }

        /// <summary>
        ///     Player data, or null, if player is not logged in
        /// </summary>
        protected PlayerDataPacket PlayerData
        {
            get { return Auth.PlayerData; }
        }

        /// <summary>
        ///     Clients connection to master server
        /// </summary>
        protected IClientSocket MasterConnection { get; private set; }

        public EventsChannel Events
        {
            get { return BmEvents.Channel; }
        }

        /// <summary>
        ///     Returns true, if client is connected to master server
        /// </summary>
        protected bool IsConnectedToMaster
        {
            get { return MasterConnection.IsConnected; }
        }

        public void Awake()
        {
            MasterConnection = Connections.ClientToMaster;

            Auth.OnLoggedIn += OnLoggedIn;
            Auth.OnLoggedOut += OnLoggedOut;

            MasterConnection.OnConnected += OnConnectedToMaster;
            MasterConnection.OnDisconnected += OnDisconnectedFromMaster;
            OnAwake();
        }

        public void OnDestroy()
        {
            Auth.OnLoggedIn -= OnLoggedIn;
            Auth.OnLoggedOut -= OnLoggedOut;

            MasterConnection.OnConnected -= OnConnectedToMaster;
            MasterConnection.OnDisconnected -= OnDisconnectedFromMaster;

            OnDestroyEvent();
        }

        /// <summary>
        ///     Called when client logs in
        /// </summary>
        protected virtual void OnLoggedIn()
        {
        }

        /// <summary>
        ///     Called when client logs out
        /// </summary>
        protected virtual void OnLoggedOut()
        {
        }

        /// <summary>
        ///     Called on client, when it connects to master server
        /// </summary>
        protected virtual void OnConnectedToMaster()
        {
        }

        /// <summary>
        ///     Called on client, when it disconnects from master server
        /// </summary>
        protected virtual void OnDisconnectedFromMaster()
        {
        }

        protected virtual void OnAwake()
        {
        }

        protected virtual void OnDestroyEvent()
        {
        }
    }
}