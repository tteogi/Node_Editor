using System.Collections.Generic;
using System.Linq;
using Barebones.Logging;
using Barebones.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Handles most of the chat functionality on the server
    /// </summary>
    public partial class ChatModule : MasterModule
    {
        private static ChatModule _instance;

        /// <summary>
        /// Collection of active channels
        /// </summary>
        protected Dictionary<string, ChatChannel> Channels;

        protected AuthModule Auth;

        /// <summary>
        /// List of words, chat should not appear in chat names
        /// </summary>
        public List<string> ForbiddenWordsInChNames;

        public int MinChannelNameLength = 2;
        public int MaxChannelNameLength = 25;

        public BmLogger Logger = LogManager.GetLogger(typeof(ChatModule).ToString());
        public LogLevel LogLevel = Logging.LogLevel.Warn;

        /// <summary>
        /// If true, players, who join a game, will automatically join this game's channel
        /// </summary>
        [Header("Game Server Chat Channels")]
        public bool EnableGameChat = true;

        public string GamesChPrefix = "Game-";

        protected virtual void Awake()
        {
            Logger.LogLevel = LogLevel;

            if (DestroyIfExists()) return;

            Channels = new Dictionary<string, ChatChannel>();

            AddDependency<AuthModule>();

            if (EnableGameChat)
            {
                ForbiddenWordsInChNames.Add(GamesChPrefix);
                AddDependency<GamesModule>();
            }
        }

        public override void Initialize(IMaster master)
        {
            Auth = master.GetModule<AuthModule>();

            if (EnableGameChat)
            {
                InitializeGameServerChannels(master.GetModule<GamesModule>());
            }

            master.SetClientHandler(new PacketHandler(BmOpCodes.JoinChatChannel, HandleJoinChannel));
            master.SetClientHandler(new PacketHandler(BmOpCodes.LeaveChatChannel, HandleLeaveChannel));
            master.SetClientHandler(new PacketHandler(BmOpCodes.ChatMessage, OnChatMessageReceived));
            master.SetClientHandler(new PacketHandler(BmOpCodes.GetUserChannels, HandleGetChannels));
            master.SetClientHandler(new PacketHandler(BmOpCodes.SetLocalChannel, HandleSetLocalChannel));

            Logger.Debug("Chat Module initialized");
        }

        /// <summary>
        /// Invoked when chat message is received
        /// </summary>
        /// <param name="message"></param>
        protected virtual void OnChatMessageReceived(IIncommingMessage message)
        {
            var packet = message.DeserializePacket(new ChatMessagePacket());
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (HandleChatMessage(packet, session, message))
            {
                message.Respond(AckResponseStatus.Success);
                return;
            }

            // If the message is not handled
            message.Respond("Not handled", AckResponseStatus.Error);
        }

        /// <summary>
        /// Handles chat message.
        /// Returns true, if message was handled
        /// If it returns false, message sender will receive a "Not Handled" response.
        /// </summary>
        protected virtual bool HandleChatMessage(ChatMessagePacket message, ISession sender, IIncommingMessage rawMessage)
        {
            if (sender.Username == null)
            {
                rawMessage.Respond("You must be logged in first", AckResponseStatus.Failed);
                return true;
            }

            // Set a true sender
            message.Sender = sender.Username;

            switch (message.Type)
            {
                case ChatMessagePacket.ChannelMessage:
                    var channels = GetUserChannels(sender);
                    ChatChannel channel;

                    if (string.IsNullOrEmpty(message.Receiver))
                    {
                        // A local message
                        if (HandleLocalMessage(message, sender))
                        {
                            rawMessage.Respond(AckResponseStatus.Success);
                        }
                        else
                        {
                            rawMessage.Respond("You're not in a local channel", AckResponseStatus.Failed);
                        }
                        return true;
                    }

                    channels.TryGetValue(message.Receiver.ToLower(), out channel);

                    if (channel == null)
                    {
                        // Not in this channel
                        rawMessage.Respond(string.Format("You're not in the '{0}' channel", message.Receiver), 
                            AckResponseStatus.Failed);
                        return true;
                    }

                    channel.BroadcastMessage(message);
                    
                    return true;

                case ChatMessagePacket.PrivateMessage:
                    var receiver = Auth.GetLoggedInSession(message.Receiver);

                    if (receiver == null)
                    {
                        rawMessage.Respond(string.Format("User '{0}' is not online", message.Receiver), AckResponseStatus.Failed);
                        return true;
                    }

                    var msg = MessageHelper.Create(BmOpCodes.ChatMessage, message.ToBytes());
                    receiver.Peer.SendMessage(msg, DeliveryMethod.Reliable);

                    return true;
            }

            return false;
        }

        /// <summary>
        /// Handles local messages. Returns true, if message is handled.
        /// This method should try to determine what's the current local channel of the user
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="sender"></param>
        /// <returns></returns>
        protected bool HandleLocalMessage(ChatMessagePacket packet, ISession sender)
        {
            var channel = sender.Peer.GetProperty(BmPropCodes.LocalChatChannel) as ChatChannel;

            if (channel == null)
                return false;

            channel.BroadcastMessage(packet);
            return true;
        }
        
        /// <summary>
        /// Handles user's request to join
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleJoinChannel(IIncommingMessage message)
        {
            var channelName = message.AsString();

            var channel = GetOrCreateChannel(channelName);

            if (channel == null)
            {
                // There's no such channel
                message.Respond("This channel is forbidden", AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (!channel.AddUser(session))
            {
                message.Respond("Failed to join a channel", AckResponseStatus.Failed);
                return;
            }

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles a request from client, to set a specific channel 
        /// as clients local channel
        /// </summary>
        /// <param name="message"></param>
        private void HandleSetLocalChannel(IIncommingMessage message)
        {
            var channelName = message.AsString();

            var channel = GetOrCreateChannel(channelName);

            if (channel == null)
            {
                // There's no such channel
                message.Respond("This channel is forbidden", AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            // Add user to channel
            channel.AddUser(session);

            // Set the property of local chat channel
            message.Peer.SetProperty(BmPropCodes.LocalChatChannel, channel);

            // Respond with a "success" status
            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to leave a chat
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleLeaveChannel(IIncommingMessage message)
        {
            var channelName = message.AsString().ToLower();

            ChatChannel channel;
            Channels.TryGetValue(channelName, out channel);

            if (channel == null)
            {
                message.Respond("This channel does not exist", AckResponseStatus.Failed);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            // Remove from channel
            channel.RemoveUser(session);

            message.Respond(AckResponseStatus.Success);
        }

        /// <summary>
        /// Handles user's request to leave a chat
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGetChannels(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            // Remove from channel
            var channels = GetUserChannels(session).Values.Select(c => c.Name);

            message.Respond(channels.ToBytes(), AckResponseStatus.Success);
        }

        public ChatChannel GetChannel(string name)
        {
            ChatChannel result;
            Channels.TryGetValue(name.ToLower(), out result);
            return result;
        }

        public virtual ChatChannel GetOrCreateChannel(string channelName)
        {
            return GetOrCreateChannel(channelName, false);
        }

        /// <summary>
        /// Retrieves an existing channel or creates a new one.
        /// If <see cref="ignoreForbidden"/> value is set to false,
        /// before creating a channel, a check will be executed to make sure that
        /// no forbidden words are used in the name
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="ignoreForbidden"></param>
        /// <returns></returns>
        protected virtual ChatChannel GetOrCreateChannel(string channelName, bool ignoreForbidden)
        {
            var lowercaseName = channelName.ToLower();
            ChatChannel channel;
            Channels.TryGetValue(lowercaseName, out channel);

            if (channel == null)
            {
                if (channelName.Length < MinChannelNameLength)
                    return null;

                if (channelName.Length > MaxChannelNameLength)
                    return null;

                // There's no such channel, but we might be able to create one
                if (!ignoreForbidden 
                    && ForbiddenWordsInChNames.Any(w => !string.IsNullOrEmpty(w) && channelName.Contains(w.ToLower())))
                {
                    // Channel contains a forbidden word
                    return null;
                }

                channel = new ChatChannel(this, channelName);
                Channels.Add(lowercaseName, channel);
            }

            return channel;
        }

        /// <summary>
        /// When users join a game, they will automatically join games chat channel,
        /// which will be set as their local channel
        /// </summary>
        /// <param name="games"></param>
        private void InitializeGameServerChannels(GamesModule games)
        {
            // When player joins a game
            games.OnPlayerJoinedGame += (session, server) =>
            {
                var channelName = GamesChPrefix + server.GameId;
                var channel = GetOrCreateChannel(channelName, true);

                channel.AddUser(session);

                // Set current channel as local
                session.Peer.SetProperty(BmPropCodes.LocalChatChannel, channel);
            };

            // When player leaves a game
            games.OnPlayerLeftGame += (session, server) =>
            {
                var channelName = GamesChPrefix + server.GameId;
                var channel = GetOrCreateChannel(channelName);

                channel.RemoveUser(session);
            };
        }

        /// <summary>
        /// Returns a mutable collection (Dictionary) of chat channels a user is in
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public virtual Dictionary<string, ChatChannel> GetUserChannels(ISession session)
        {
            var channels = session.Peer.GetProperty(BmPropCodes.ChatChannels) as Dictionary<string, ChatChannel>;

            if (channels == null)
            {
                // In case this session has no channels collection, we need to add it
                channels = new Dictionary<string, ChatChannel>();
                session.Peer.SetProperty(BmPropCodes.ChatChannels, channels);
            }

            return channels;
        }
    }
}