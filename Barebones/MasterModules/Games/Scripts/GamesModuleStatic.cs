using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Static methods, that extend game modules functionality.
    ///     Mostly contains helper methods for game servers and clients
    /// </summary>
    public partial class GamesModule
    {
        public delegate void AccessRequestCallback(GameAccessPacket packet, string error);

        public delegate void GameServerRegisterCallback(RegisteredGame game);

        public delegate RegisteredGame RegisteredGameFactory(int gameId, IClientSocket connection,
            RegisterGameServerPacket readyPacket, IGameServer server);

        public static RegisteredGameFactory Factory;

        /// <summary>
        ///     Returns true, if whoever is using this instance, connected to game
        ///     server as a client
        /// </summary>
        public static bool IsClient { get; set; }

        /// <summary>
        ///     Current game, that is registered to master server.
        ///     Property accessible to game server
        /// </summary>
        public static RegisteredGame CurrentGame { get; private set; }

        /// <summary>
        ///     Invoked on game server, when game is successfully registered
        /// </summary>
        public static event Action<RegisteredGame> OnGameRegistered;

        /// <summary>
        ///     Invoked, when game server starts (before registration)
        /// </summary>
        public static event Action<IGameServer> OnGameServerStarted;

        #region Game Server methods

        public static void NotifyServerStarted(IGameServer server)
        {
            if (OnGameServerStarted != null)
                OnGameServerStarted.Invoke(server);
        }

        /// <summary>
        ///     Sends a notification to master server, to indicate that game server is ready to be accessed
        /// </summary>
        public static void RegisterGame(IGameServer server,
            GameServerRegisterCallback callback, string masterKey = "")
        {
            RegisterGame(server.CreateRegisterPacket(masterKey), server, callback);
        }

        /// <summary>
        ///     Sends a notification to master server, to indicate that game server is ready to be accessed
        /// </summary>
        /// <param name="registerPacket"></param>
        /// <param name="server"></param>
        /// <param name="callback"></param>
        public static void RegisterGame(RegisterGameServerPacket registerPacket, IGameServer server,
            GameServerRegisterCallback callback)
        {
            if ((CurrentGame != null) && !CurrentGame.IsStopped)
            {
                Logs.Error("Cannot register a game server, because the last game server was not stopped");
                return;
            }

            var connection = Connections.GameToMaster;
            var msg = MessageHelper.Create(BmOpCodes.RegisterGameServer, registerPacket.ToBytes());

            if (!connection.IsConnected)
            {
                Logs.Error("You can't register a game server if you're not connected to Master. " +
                               "Please connect to master before registering");
                return;
            }

            if (string.IsNullOrEmpty(registerPacket.CmdArgs))
            {
                // Add cmd args of this process
                registerPacket.CmdArgs = string.Join(" ", Environment.GetCommandLineArgs());
            }

            connection.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null);
                    return;
                }

                var gameId = response.AsInt();

                // Create a game instance
                var game = Factory != null
                    ? Factory.Invoke(gameId, connection, registerPacket, server)
                    : CreateRegisteredGameDefault(gameId, connection, registerPacket, server);

                CurrentGame = game;

                game.Initialize();

                if (OnGameRegistered != null)
                    OnGameRegistered.Invoke(CurrentGame);

                callback.Invoke(game);
            });
        }

        private static RegisteredGame CreateRegisteredGameDefault(int gameId, IClientSocket connection,
            RegisterGameServerPacket readyPacket, IGameServer server)
        {
            return new RegisteredGame(gameId, connection, readyPacket, server);
        }

        #endregion

        #region Client methods

        /// <summary>
        ///     Retrieves an access to game server
        /// </summary>
        /// <param name="data">request data</param>
        /// <param name="callback"></param>
        /// <param name="connectionId"></param>
        public static void GetAccess(RoomJoinRequestDataPacket data, AccessRequestCallback callback)
        {
            var connection = Connections.ClientToMaster;

            if (!connection.IsConnected)
            {
                Logs.Error("Cannot receive a pass, because not connected to master");
                callback.Invoke(null, "Invalid connection");
                return;
            }

            connection.Peer.SendMessage(BmOpCodes.AccessRequest, data.ToBytes(), (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null, response.HasData ? response.AsString() : "Internal Server Error");
                    return;
                }

                callback.Invoke(response.DeserializePacket(new GameAccessPacket()), null);
            });
        }

        #endregion
    }
}