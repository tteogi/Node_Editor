using System;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public delegate ISession SessionFactory(int sessionId, IPeer peer);

    /// <summary>
    ///     Interface of a client session within master server
    /// </summary>
    public interface ISession
    {
        /// <summary>
        ///     Session ID
        /// </summary>
        int Id { get; }

        /// <summary>
        ///     Username of the account. null until logs in
        /// </summary>
        string Username { get; }

        /// <summary>
        ///     Account data. Null until logs in
        /// </summary>
        IAccountData Account { get; set; }

        /// <summary>
        ///     Owner of the session
        /// </summary>
        IPeer Peer { get; }

        /// <summary>
        ///     Event, which is invoked when this session disconnects
        /// </summary>
        event Action<ISession> OnDisconnect;

        /// <summary>
        ///     Tries to force disconnect a connected session
        /// </summary>
        void ForceDisconnect();

        void SetUsername(string username);
    }
}