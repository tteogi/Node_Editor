using System;
using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    public partial class ChatModule
    {
        public delegate void SetLocalChannelCallback(bool isSuccessful);

        public delegate void ChatCallback(bool isSuccessful, string error);

        /// <summary>
        /// Sends a request to join a channel
        /// </summary>
        /// <param name="name"></param>
        /// <param name="callback"></param>
        public static void JoinChannel(string name, ChatCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.JoinChatChannel, name);
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.HasData ? response.AsString() : "Unknown Error");
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to leave a channel
        /// </summary>
        /// <param name="name"></param>
        /// <param name="callback"></param>
        public static void LeaveChannel(string name, ChatCallback callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.LeaveChatChannel, name);
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.HasData ? response.AsString() : "Unknown Error");
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        /// <summary>
        /// Retrieves a list of channel names, which client has joined
        /// </summary>
        /// <param name="callback"></param>
        public static void GetChannels(Action<List<string>> callback)
        {
            var msg = MessageHelper.Create(BmOpCodes.GetUserChannels);

            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(null);
                }
                else
                {
                    var list = new List<string>().FromBytes(response.AsBytes());

                    callback.Invoke(list);
                }
            });
        }

        /// <summary>
        /// Sends a request to set a specific channel as a local channel 
        /// (the one, to which messages will be sent if client doesn't specify channel)
        /// </summary>
        /// <param name="channelName"></param>
        public static void SetLocalChannel(string channelName, SetLocalChannelCallback callback )
        {
            var msg = MessageHelper.Create(BmOpCodes.SetLocalChannel, channelName);
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                callback.Invoke(status == AckResponseStatus.Success);
            });
        }

        /// <summary>
        /// Sends a chat message to master server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="callback"></param>
        public static void SendMessage(ChatMessagePacket packet, ChatCallback callback )
        {
            var msg = MessageHelper.Create(BmOpCodes.ChatMessage, packet.ToBytes());
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                if (status != AckResponseStatus.Success)
                {
                    callback.Invoke(false, response.HasData ? response.AsString() : "Unknown Error");
                    return;
                }

                callback.Invoke(true, null);
            });
        }
    }
}