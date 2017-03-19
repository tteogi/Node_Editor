using System.Collections;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Automatically connects to the master server as a client
    /// </summary>
    public class MasterServerConnector : MonoBehaviour
    {
        public string IpAddress = "127.0.0.1";

        public int Port = 5000;

        /// <summary>
        /// If true, when launching a server through command line,
        /// this script will not try to connect to master as a client
        /// </summary>
        public bool SkipIfServer = true;

        [Header("Debugging (Editor only)")]
        public bool OverrideEditorAddress = true;
        public string EditorAddress = "127.0.0.1";

        // Use this for initialization
        private void Start()
        {
#if UNITY_EDITOR
            if (OverrideEditorAddress)
            {
                IpAddress = EditorAddress;
            }

            if (Master.Instance != null && !Master.IsStarted && Master.Instance.AutoStartInEditor)
            {
                // If we're also starting a master server and it's not started,
                // Connect only after it's started
                Master.OnStarted += () =>
                {
                    Connect();
                };
                return;
            }
#endif
            // Regular connection
            Connect();
        }

        private void Connect()
        {
#if !UNITY_EDITOR
            // Ignore if this is supposed to be a server
            var isServer = BmArgs.StartManual || BmArgs.StartSpawned;

            Logs.Trace(isServer + " " + SkipIfServer);

            if (SkipIfServer && isServer)
                return;
#endif

            var connection = Connections.ClientToMaster;

            // Connect, if not already connected
            if (!connection.IsConnected && !connection.IsConnecting)
            {
                connection.Connect(IpAddress, Port);
            }
        }
    }
}