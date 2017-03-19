using System;

namespace Barebones.Networking
{
    public interface IServerSocket
    {
        /// <summary>
        /// Invoked, when a client connects to this socket
        /// </summary>
        event Action<IPeer> OnConnected;

        /// <summary>
        /// Invoked, when client disconnects from this socket
        /// </summary>
        event Action<IPeer> OnDisconnected;

        /// <summary>
        /// Opens the socket and starts listening to a given port
        /// </summary>
        /// <param name="port"></param>
        void Listen(int port);

        /// <summary>
        /// Stops listening
        /// </summary>
        void Stop();
    }
}