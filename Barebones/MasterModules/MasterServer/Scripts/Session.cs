using System;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a basic client session, required for master server to work with
    /// </summary>
    public class Session : ISession
    {
        public Session(int id, IPeer peer)
        {
            Id = id;
            Peer = peer;
            peer.OnDisconnect += OnPeerDisconnect;
        }

        public int Id { get; private set; }
        public IPeer Peer { get; private set; }

        public string Username { get; private set; }
        public IAccountData Account { get; set; }

        /// <summary>
        ///     Event, which is invoked when this session disconnects
        /// </summary>
        public event Action<ISession> OnDisconnect;

        public void SetUsername(string username)
        {
            if (Username != null)
                throw new Exception("Username is already set");

            Username = username;
        }

        /// <summary>
        ///     Tries to force disconnect a connected session
        /// </summary>
        public virtual void ForceDisconnect()
        {
            Peer.Disconnect("Forced DC");
        }

        /// <summary>
        ///     Called when peer disconnects.
        ///     Invokes <see cref="OnDisconnect" /> event
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnPeerDisconnect(IPeer peer)
        {
            peer.OnDisconnect -= OnPeerDisconnect;
            if (OnDisconnect != null)
                OnDisconnect.Invoke(this);
        }
    }
}