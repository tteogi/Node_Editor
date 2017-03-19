using UnityEngine;
using UnityEngine.Networking.NetworkSystem;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Base class for connectors.
    ///     Connectors should provide means for client to connect
    ///     to game server
    /// </summary>
    public abstract class GameConnector : MonoBehaviour
    {
        public delegate void GameConnectHandler(bool isSuccessful);

        /// <summary>
        ///     Latest access data. When switching scenes, if this is set,
        ///     connector should most likely try to use this data to connect to game server
        ///     (if the scene is right)
        /// </summary>
        protected static GameAccessPacket AccessData;

        /// <summary>
        ///     Connector instance
        /// </summary>
        public static GameConnector Instance;

        protected virtual void Awake()
        {
            Instance = this;
        }

        protected virtual void OnDestroy()
        {
            Instance = null;
        }

        /// <summary>
        ///     Should try to connect to game server with data, provided
        ///     in the access packet
        /// </summary>
        /// <param name="access"></param>
        protected abstract void ConnectToGame(GameAccessPacket access);

        /// <summary>
        ///     Sends access to game server, so that game server can check if user
        ///     can actually access the game
        /// </summary>
        /// <param name="access"></param>
        public abstract void SendAccessToGameServer(GameAccessPacket access);

        #region Static

        /// <summary>
        ///     Publicly accessible method, which clients should use to connect
        ///     to game servers
        /// </summary>
        /// <param name="packet"></param>
        public static void Connect(GameAccessPacket packet)
        {
            if (Instance == null)
            {
                Logs.Error("Failed to connect to game server. No Game Connector was found in the scene");
                return;
            }

            GamesModule.IsClient = true;

            // Save the access data
            AccessData = packet;

            Instance.ConnectToGame(packet);
        }

        #endregion
    }
}