using System;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// A collection of static methods, 
    /// that allows to work with lobbies easier
    /// </summary>
    public partial class LobbiesModule
    {
        public delegate void LobbyCallback(string error);
        public delegate void CreateLobbyCallback(int? lobbyId, string error);
        public delegate void LobbyInfoCallback(LobbyDataPacket data, string error);
        public delegate void GameAccessRequestCallback(GameAccessPacket access, string error);
        public delegate void PlayerDataCallback(LobbyMemberData data, string error);
        public delegate void IsInLobbyCallback(bool isInLobby);

        /// <summary>
        /// Sends a request to create a new lobby. Given parameters will be sent to a specified
        /// lobby factory.
        /// </summary>
        /// <param name="lobbyFactory">Which factory should be used to create the lobby</param>
        /// <param name="parameters"></param>
        /// <param name="callback"></param>
        public static void CreateLobby(string lobbyFactory, Dictionary<string, string> parameters, 
            CreateLobbyCallback callback)
        {
            // Add the lobby type 
            parameters[LobbyTypePropKey] = lobbyFactory;

            var msg = MessageHelper.Create(BmOpCodes.LobbyCreate, parameters.ToBytes());

            // Send the request
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null, response.HasData ? response.AsString() : "Failed to create lobby");
                    return;
                }

                callback.Invoke(response.AsInt(), null);
            });
        }

        /// <summary>
        /// Sends a request to join a lobby
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="callback"></param>
        public static void JoinLobby(int lobbyId, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyJoin, lobbyId);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(response.HasData ? response.AsString() : "Failed to join lobby");
                    return;
                }
                
                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to leave the lobby, which player is currently in
        /// </summary>
        /// <param name="callback"></param>
        public static void LeaveLobby(LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyLeave);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(response.HasData ? response.AsString() : "Failed to leave lobby");
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to start a game
        /// </summary>
        /// <param name="callback"></param>
        public static void StartGame(LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyStartGame);
            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var error = response.HasData ? response.AsString() : "Could not start the game";   
                    callback.Invoke(error);
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to join a specified team
        /// </summary>
        /// <param name="teamName"></param>
        /// <param name="callback"></param>
        public static void JoinTeam(string teamName, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyJoinTeam, teamName);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(response.HasData ? response.AsString() : "Failed to switch teams");
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to change player's readyness status
        /// </summary>
        /// <param name="isReady"></param>
        /// <param name="callback"></param>
        public static void SetReady(bool isReady, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbySetReady, isReady ? 1 : 0);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(response.HasData ? response.AsString() : "Failed to change 'ready' status");
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to change player properties
        /// </summary>
        /// <param name="modifiedProperties"></param>
        /// <param name="callback"></param>
        public static void SetPlayerProperties(Dictionary<string, string> modifiedProperties, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyMemberPropertySet, modifiedProperties.ToBytes());

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var error = response.HasData ? response.AsString() : "Failed to change player properties";
                    callback.Invoke(error);
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to set lobby properties
        /// </summary>
        /// <param name="modifiedProperties"></param>
        /// <param name="callback"></param>
        public static void SetLobbyProperties(Dictionary<string, string> modifiedProperties, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyPropertySet, modifiedProperties.ToBytes());

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var error = response.HasData ? response.AsString() : "Failed to change lobby properties";
                    callback.Invoke(error);
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to retrieve information about lobby,
        /// which user is currently in
        /// </summary>
        /// <param name="callback"></param>
        public static void GetLobbyInfo(LobbyInfoCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyInfo);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null, response.HasData ? response.AsString() : "Failed to get lobby info");
                    return;
                }

                var info = response.DeserializePacket(new LobbyDataPacket());

                callback.Invoke(info, null);
            });
        }

        /// <summary>
        /// Sends a chat message to a lobby, which user has joined
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public static void SendChatMessage(string message, LobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyChatMessage, message);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var error = response.HasData ? response.AsString() : "Failed to send a message";
                    callback.Invoke(error);
                    return;
                }

                callback.Invoke(null);
            });
        }

        /// <summary>
        /// Sends a request to retrieve access to a game, which is attached
        /// to current user's lobby
        /// </summary>
        /// <param name="callback"></param>
        public static void GetLobbyGameAccess(GameAccessRequestCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyGameAccessRequest);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    var error = response.HasData ? response.AsString() : "Failed to get accesss to the game";
                    callback.Invoke(null, error);
                }

                var access = response.DeserializePacket(new GameAccessPacket());

                callback.Invoke(access, null);
            });
        }

        /// <summary>
        /// Sends a request to check if user has joined any lobby
        /// </summary>
        /// <param name="callback"></param>
        public static void IsInLobby(IsInLobbyCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyIsInLobby);

            Connections.ClientToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false);
                    return;
                }

                callback.Invoke(true);
            });
        }

        #region Game server methods

        /// <summary>
        /// Sends a request from game server to attach itself to a specific lobby
        /// </summary>
        /// <param name="lobbyId"></param>
        /// <param name="callback"></param>
        public static void AttachLobbyToGame(int lobbyId, LobbyInfoCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyAttachToGame, lobbyId);

            Connections.GameToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Failed to attach lobby"));
                    return;
                }

                var data = response.DeserializePacket(new LobbyDataPacket());

                callback.Invoke(data, null);
            });
        }

        /// <summary>
        /// Sends a request from a game server to get data about a specific member
        /// of the lobby
        /// </summary>
        /// <param name="username"></param>
        /// <param name="callback"></param>
        public static void GetPlayerData(string username, PlayerDataCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyGetPlayerData, username);

            Connections.GameToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null, response.AsString("Failed to get data"));
                    return;
                }

                var data = response.DeserializePacket(new LobbyMemberData());

                callback.Invoke(data, null);
            });
        }

        /// <summary>
        /// Sends a request from game server to override player properties
        /// </summary>
        /// <param name="username"></param>
        /// <param name="propKey"></param>
        /// <param name="value"></param>
        /// <param name="callback"></param>
        public static void OverridePlayerProperty(string username, string propKey, string value, 
            LobbyCallback callback)
        {
            var packet = new LobbyMemberPropChangePacket()
            {
                Username = username,
                Property = propKey,
                Value = value
            };

            var msg = MessageHelper.Create(BmOpCodes.LobbyMemberPropertySet, packet.ToBytes());

            Connections.GameToMaster.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(response.AsString("Failed to get data"));
                    return;
                }

                callback.Invoke(null);
            });
        }

        #endregion


    }
}