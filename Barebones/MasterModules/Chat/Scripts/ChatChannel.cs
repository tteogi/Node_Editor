using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class ChatChannel
    {
        private readonly ChatModule _module;

        public string Name { get; private set; }

        private Dictionary<string, ISession> _users;

        public ChatChannel(ChatModule module, string name)
        {
            Name = name;
            _module = module;
            _users = new Dictionary<string, ISession>();
        }

        /// <summary>
        /// Returns true, if user successfully joined a channel
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public bool AddUser(ISession session)
        {
            if (!IsUserAllowed(session))
                return false;

            // Add disconnect listener
            session.OnDisconnect += OnUserDisconnect;

            // Add user
            _users.Add(session.Username, session);

            // Add channel to users collection
            var userChannels = _module.GetUserChannels(session);

            var lowercaseName = Name.ToLower();

            if (userChannels.ContainsKey(lowercaseName))
            {
                userChannels[lowercaseName] = this;
            }
            else
            {
                userChannels.Add(lowercaseName, this);
            }

            OnJoined(session);
            return true;
        }

        protected virtual void OnJoined(ISession session)
        {
            
        }

        protected virtual void OnLeft(ISession session)
        {

        }

        protected virtual bool IsUserAllowed(ISession session)
        {
            // Can't join if already here
            return !_users.ContainsKey(session.Username);
        } 

        /// <summary>
        /// Invoked, when user, who is connected to this channel, leaves
        /// </summary>
        /// <param name="session"></param>
        protected virtual void OnUserDisconnect(ISession session)
        {
            RemoveUser(session);
        }

        public void RemoveUser(ISession session)
        {
            // Remove disconnect listener
            session.OnDisconnect -= OnUserDisconnect;

            // Remove channel from users collection
            _module.GetUserChannels(session).Remove(Name.ToLower());

            // Remove user
            _users.Remove(session.Username);

            var playersLocalChannel = session.Peer.GetProperty(BmPropCodes.LocalChatChannel) as ChatChannel;
            if (playersLocalChannel == this)
            {
                // If this channel was set as players local channel, unset it 
                session.Peer.SetProperty(BmPropCodes.LocalChatChannel, null);
            }

            OnLeft(session);
        }

        /// <summary>
        /// Handle messages
        /// </summary>
        public virtual void BroadcastMessage(ChatMessagePacket packet)
        {
            // Override name to be in a "standard" format (uppercase letters and etc.)
            packet.Receiver = Name;

            var msg = MessageHelper.Create(BmOpCodes.ChatMessage, packet.ToBytes());

            foreach (var session in _users.Values)
            {
                session.Peer.SendMessage(msg, DeliveryMethod.Reliable);
            }
        }
    }
}