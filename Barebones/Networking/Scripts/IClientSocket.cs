using System;
using Barebones.MasterServer;

namespace Barebones.Networking
{
    public interface IClientSocket : IMsgDispatcher
    {
        /// <summary>
        /// Connection status
        /// </summary>
        ConnectionStatus Status { get; }

        /// <summary>
        /// Returns true, if we are connected to another socket
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Returns true, if we're in the process of connecting
        /// </summary>
        bool IsConnecting { get; }

        /// <summary>
        /// Event, which is invoked when we successfully 
        /// connect to another socket
        /// </summary>
        event Action OnConnected;

        /// <summary>
        /// Event, which is invoked when we are
        /// disconnected from another socket
        /// </summary>
        event Action OnDisconnected;

        /// <summary>
        /// Event, invoked when connection status changes
        /// </summary>
        event Action<ConnectionStatus> OnStatusChange;

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        IClientSocket Connect(string ip, int port, int timeoutMillis);
        
        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        IClientSocket Connect(string ip, int port);

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        /// <param name="timeoutSeconds"></param>
        void WaitConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds);

        /// <summary>
        /// Invokes a callback when connection is established, or after the timeout
        /// (even if failed to connect). If already connected, callback is invoked instantly
        /// </summary>
        /// <param name="connectionCallback"></param>
        void WaitConnection(Action<IClientSocket> connectionCallback);

        /// <summary>
        /// Adds a package handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        [Obsolete("Use SetHandler")]
        IPacketHandler AddHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler SetHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        IPacketHandler SetHandler(short opCode, Action<IIncommingMessage> handlerMethod);

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        void RemoveHandler(IPacketHandler handler);

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        void Reconnect();

        /// <summary>
        /// Closes socket connection
        /// </summary>
        void Disconnect();
    }
}