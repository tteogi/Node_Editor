using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Extension of regular network manager, which overrides default methods
    ///     and invokes specific events.
    /// </summary>
    public class EventfulNetworkManager : NetworkManager
    {
        private bool _onServerConnectFixed;

        [Header("Override settings")] public bool DestroyPlayersForConnection = true;

        public event Action OnStartServerEvent;
        public event Action OnStartHostEvent;
        public event Action OnStopClientEvent;
        public event Action<NetworkConnection> OnClientConnectEvent;
        public event Action<NetworkConnection> OnClientDisconnectEvent;
        public event Action<NetworkConnection> OnServerConnectEvent;
        public event Action<NetworkConnection> OnServerDisconnectEvent;
        public event Action<NetworkConnection, short> OnServerAddPlayerEvent;
        public event Action OnStopServerEvent;

        [Header("Force uNET Bug Fixes")]
        // If true, applies a fix for "maximum hosts cannot exceed {16}"
        public bool FixClientRemoveHostBug = true;

        public sealed override void OnServerAddPlayer(NetworkConnection conn, short playerControllerId)
        {
            if (OnServerAddPlayerEvent != null)
                OnServerAddPlayerEvent.Invoke(conn, playerControllerId);
        }

        public override void OnStartServer()
        {
            if (OnStartServerEvent != null)
                OnStartServerEvent.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            if (OnStopClientEvent != null)
                OnStopClientEvent.Invoke();
        }

        public override void OnClientConnect(NetworkConnection conn)
        {
            base.OnClientConnect(conn);

            if (OnClientConnectEvent != null)
                OnClientConnectEvent.Invoke(conn);
                
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            // Work around the uNET issue "maximum hosts cannot exceed {16}"
            // which happens because unity doesn't properly remove hostId
            // on client when he's disconnected
            if (FixClientRemoveHostBug)
            {
                StopClient();
            }

            if (OnClientDisconnectEvent != null)
                OnClientDisconnectEvent.Invoke(conn);
        }

        public override void OnServerConnect(NetworkConnection conn)
        {
            if (conn.connectionId == 0)
            {
                // Ignore the second run
                if (_onServerConnectFixed)
                    return;

                _onServerConnectFixed = true;
            }

            if (OnServerConnectEvent != null)
                OnServerConnectEvent.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            if (DestroyPlayersForConnection)
                NetworkServer.DestroyPlayersForConnection(conn);

            if (OnServerDisconnectEvent != null)
                OnServerDisconnectEvent.Invoke(conn);
        }

        public override void OnStopServer()
        {
            if (OnStopServerEvent != null)
                OnStopServerEvent.Invoke();
        }

        public override void OnStartHost()
        {
            if (OnStartHostEvent != null)
                OnStartHostEvent.Invoke();
        }
    }
}