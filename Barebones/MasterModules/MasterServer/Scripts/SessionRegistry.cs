using System;
using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Manages user sessions
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SessionRegistry<T> where T : ISession
    {
        private readonly SessionFactory _factory;
        private readonly Dictionary<int, T> _sessions;

        private int _idGenerator;

        public SessionRegistry(SessionFactory factory)
        {
            _factory = factory;
            _sessions = new Dictionary<int, T>();
        }

        /// <summary>
        ///     Event, which is invoked when session is removed from registry
        /// </summary>
        public event Action<T> OnSessionRemoved;

        /// <summary>
        ///     Event, which is invoked when session is added to registry
        /// </summary>
        public event Action<T> OnSessionAdded;

        /// <summary>
        ///     Creates a new session for given peer. Session is created through factory,
        ///     which is provided through constructor parameters
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        public T Create(IPeer peer)
        {
            var session = (T) _factory.Invoke(_idGenerator++, peer);
            Register(session);
            return session;
        }

        /// <summary>
        ///     Adds a session to the registry
        /// </summary>
        /// <param name="session"></param>
        private void Register(T session)
        {
            _sessions.Add(session.Id, session);

            if (OnSessionAdded != null)
                OnSessionAdded.Invoke(session);
        }

        public void Remove(int sessionId)
        {
            T session;
            _sessions.TryGetValue(sessionId, out session);

            _sessions.Remove(sessionId);

            if ((session != null) && (OnSessionRemoved != null))
                OnSessionRemoved.Invoke(session);
        }

        /// <summary>
        ///     Returns a session by sessionId
        /// </summary>
        /// <param name="sessionId"></param>
        /// <returns>null, if no session found</returns>
        public T Get(int sessionId)
        {
            T session;
            _sessions.TryGetValue(sessionId, out session);
            return session;
        }
    }
}