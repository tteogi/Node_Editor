using System;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    public partial class SpawnersModule
    {
        public delegate void GameCreationStartedCallback(GameCreationProcess request, string error);
        public delegate void InstanceStartedCallback(Dictionary<string, string> userParameters);

        /// <summary>
        ///     Clients last game create request
        /// </summary>
        public static GameCreationProcess LastCreateRequest { get; private set; }

        /// <summary>
        ///     Sends a request to master server, to create a "user created" game
        /// </summary>
        /// <param name="values"></param>
        /// <param name="startedCallback"></param>
        public static void CreateUserGame(Dictionary<string, string> values, GameCreationStartedCallback startedCallback)
        {
            var connection = Connections.ClientToMaster;

            if (!connection.IsConnected)
                throw new Exception("Client to Master connection is not established");

            // Add a handler which will update request status
            connection.SetHandler(new PacketHandler(BmOpCodes.CreateGameStatus, message =>
            {
                if (LastCreateRequest != null)
                    LastCreateRequest.ChangeStatus((CreateGameStatus)message.AsInt());
            }));

            var msg = MessageHelper.Create(BmOpCodes.CreateGameServer, values.ToBytes());

            connection.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var errorMessage = (response != null) && response.HasData
                        ? response.AsString()
                        : "Unknown error";

                    startedCallback.Invoke(null, errorMessage);
                    return;
                }

                var spawnId = response.AsInt();
                var request = new GameCreationProcess(connection, spawnId);
                LastCreateRequest = request;

                startedCallback.Invoke(request, null);
            });
        }

        /// <summary>
        ///     Notifies master server that the instance is started, and retrieves a dictionary of
        ///     parameters that user set while creating the game.
        ///     Callback is invoked with "null" if failed to notify
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="callback"></param>
        public static void NotifyProcessStarted(int spawnId, InstanceStartedCallback callback)
        {
            var connection = Connections.GameToMaster;

            if (!connection.IsConnected)
                throw new Exception("Not connected to master");

            if (spawnId < 0)
                throw new Exception("Invalid instance id");

            var msg = MessageHelper.Create(BmOpCodes.UnityProcessStarted, spawnId);

            connection.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null);
                    return;
                }

                var data = new Dictionary<string, string>().FromBytes(response.AsBytes());

                callback.Invoke(data);
            });
        }
    }
}