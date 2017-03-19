using System;
using System.Collections.Generic;
using Barebones.MasterServer;
using UnityEngine;
using UnityEngine.Networking;

namespace Barebones.Networking
{
    /// <summary>
    ///     Represents a socket (client socket), which can be used to connect
    ///     to another socket (server socket)
    /// </summary>
    public class ClientSocketUnet : BaseClientSocket, IClientSocket, IUpdatable
    {
        public static bool RethrowExceptionsInEditor = true;

        private readonly HostTopology _topology;
        private int _connectionId;

        private readonly Dictionary<short, IPacketHandler> _handlers;

        private string _ip;
        private int _port;

        private bool _isConnectionPending;
        private readonly byte[] _msgBuffer;

        private PeerUnet _serverPeer;
        private int _socketId;

        private ConnectionStatus _status;
        private int _stopConnectingTick;

        public ClientSocketUnet() : this(BarebonesTopology.Topology)
        {
            _handlers = new Dictionary<short, IPacketHandler>();
        }

        public ClientSocketUnet(HostTopology topology)
        {
            _msgBuffer = new byte[NetworkMessage.MaxMessageSize];
            _topology = topology;
        }

        /// <summary>
        /// Event, which is invoked when we successfully 
        /// connect to another socket
        /// </summary>
        public event Action OnConnected;

        /// <summary>
        /// Event, which is invoked when we are
        /// disconnected from another socket
        /// </summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Event, invoked when connection status changes
        /// </summary>
        public event Action<ConnectionStatus> OnStatusChange;

        /// <summary>
        /// Returns true, if we are connected to another socket
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Returns true, if we're in the process of connecting
        /// </summary>
        public bool IsConnecting { get; private set; }

        /// <summary>
        /// Connection status
        /// </summary>
        public ConnectionStatus Status
        {
            get { return _status; }
            private set
            {
                if ((_status != value) && (OnStatusChange != null))
                {
                    _status = value;
                    OnStatusChange.Invoke(_status);
                    return;
                }
                _status = value;
            }
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port)
        {
            Connect(ip, port, 10000);
            return this;
        }

        /// <summary>
        /// Starts connecting to another socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="timeoutMillis"></param>
        /// <returns></returns>
        public IClientSocket Connect(string ip, int port, int timeoutMillis)
        {
            NetworkTransport.Init();
            _stopConnectingTick = Environment.TickCount + timeoutMillis;
            _ip = ip;
            _port = port;
            IsConnecting = true;

            _socketId = NetworkTransport.AddHost(_topology, 0);

            BmUpdateRunner.Instance.Add(this);
            return this;
        }

        /// <summary>
        /// Disconnects and connects again
        /// </summary>
        public void Reconnect()
        {
            Disconnect();

            Connect(_ip, _port);
        }

        /// <summary>
        ///     Invokes a callback after a successfull connection,
        ///     instantly if connected, or after the timeout, if failed to connect
        /// </summary>
        /// <param name="connectionCallback"></param>
        public void WaitConnection(Action<IClientSocket> connectionCallback)
        {
            WaitConnection(connectionCallback, 10);
        }

        /// <summary>
        ///     Invokes a callback after a successfull connection,
        ///     instantly if connected, or after the timeout, if failed to connect
        /// </summary>
        public void WaitConnection(Action<IClientSocket> connectionCallback, float timeoutSeconds)
        {
            if (IsConnected)
            {
                connectionCallback.Invoke(this);
                return;
            }

            var isConnected = false;
            Action onConnected = null;
            onConnected = () =>
            {
                OnConnected -= onConnected;
                isConnected = true;
                connectionCallback.Invoke(this);
            };

            OnConnected += onConnected;

            BTimer.AfterSeconds(timeoutSeconds, () =>
            {
                if (!isConnected)
                {
                    OnConnected -= onConnected;
                    connectionCallback.Invoke(this);
                }
            });
        }

        [Obsolete("Use SetHandler")]
        public IPacketHandler AddHandler(IPacketHandler handler)
        {
            SetHandler(handler);
            return handler;
        }

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler SetHandler(IPacketHandler handler)
        {
            _handlers[handler.OpCode] = handler;
            return handler;
        }

        /// <summary>
        /// Adds a packet handler, which will be invoked when a message of
        /// specific operation code is received
        /// </summary>
        public IPacketHandler SetHandler(short opCode, Action<IIncommingMessage> handlerMethod)
        {
            var handler = new PacketHandler(opCode, handlerMethod);
            SetHandler(handler);
            return handler;
        }

        /// <summary>
        /// Removes the packet handler, but only if this exact handler
        /// was used
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveHandler(IPacketHandler handler)
        {
            IPacketHandler previousHandler;
            _handlers.TryGetValue(handler.OpCode, out previousHandler);

            if (previousHandler != handler)
                return;

            _handlers.Remove(handler.OpCode);
        }

        public void Disconnect()
        {
            byte error;
            NetworkTransport.Disconnect(_socketId, _connectionId, out error);
            NetworkTransport.RemoveHost(_socketId);

            // When we disconnect ourselves, we dont get NetworkEventType.DisconnectEvent 
            // Not sure if that's the expected behaviour, but oh well...
            // TODO Make sure there's no other way
            HandleDisconnect(_connectionId, error);
        }

        public void Update()
        {
            if (_socketId == -1)
                return;

            byte error;

            if (IsConnecting && !IsConnected)
            {
                // Try connecting

                if (Environment.TickCount > _stopConnectingTick)
                {
                    // Timeout reached
                    StopConnecting();
                    return;
                }

                Status = ConnectionStatus.Connecting;

                if (!_isConnectionPending)
                {
                    // TODO Finish implementing multiple connects 
                    _connectionId = NetworkTransport.Connect(_socketId, _ip, _port, 0, out error);

                    _isConnectionPending = true;

                    if (error != (int) NetworkError.Ok)
                    {
                        StopConnecting();
                        return;
                    }
                }
            }

            NetworkEventType networkEvent;

            do
            {
                int connectionId;
                int channelId;
                int receivedSize;

                networkEvent = NetworkTransport.ReceiveFromHost(_socketId, out connectionId, out channelId, _msgBuffer,
                    _msgBuffer.Length, out receivedSize, out error);

                switch (networkEvent)
                {
                    case NetworkEventType.ConnectEvent:
                        HandleConnect(connectionId, error);
                        break;
                    case NetworkEventType.DataEvent:
                        HandleData(connectionId, channelId, receivedSize, error);
                        break;
                    case NetworkEventType.DisconnectEvent:
                        Logs.Debug("Disconnect event!");

                        HandleDisconnect(connectionId, error);
                        break;
                    case NetworkEventType.Nothing:
                        break;
                    default:
                        Logs.Error("Unknown network message type received: " + networkEvent);
                        break;
                }
            } while (networkEvent != NetworkEventType.Nothing);
        }

        private void StopConnecting()
        {
            IsConnecting = false;
            Status = ConnectionStatus.Disconnected;
            BmUpdateRunner.Instance.Remove(this);
        }

        private void HandleDisconnect(int connectionId, byte error)
        {
            if (_serverPeer != null)
                _serverPeer.Dispose();

            if (_connectionId != connectionId)
                return;

            _isConnectionPending = false;

            BmUpdateRunner.Instance.Remove(this);

            Status = ConnectionStatus.Disconnected;
            IsConnected = false;

            if (_serverPeer != null)
            {
                _serverPeer.SetIsConnected(false);
                _serverPeer.NotifyDisconnectEvent();
            }

            if (OnDisconnected != null)
                OnDisconnected.Invoke();
        }

        private void HandleData(int connectionId, int channelId, int receivedSize, byte error)
        {
            if (_serverPeer == null)
                return;

            _serverPeer.HandleDataReceived(_msgBuffer, 0);
        }

        private void HandleConnect(int connectionId, byte error)
        {
            if (_connectionId != connectionId)
                return;

            _isConnectionPending = false;

            IsConnecting = false;
            IsConnected = true;

            Status = ConnectionStatus.Connected;

            if (_serverPeer != null)
                _serverPeer.OnMessage -= HandleMessage;

            _serverPeer = new PeerUnet(connectionId, _socketId);
            Peer = _serverPeer;
            _serverPeer.SetIsConnected(true);
            _serverPeer.OnMessage += HandleMessage;

            if (OnConnected != null)
                OnConnected.Invoke();
        }

        private void HandleMessage(IIncommingMessage message)
        {
            try
            {
                IPacketHandler handler;
                _handlers.TryGetValue(message.OpCode, out handler);

                if (handler != null)
                    handler.Handle(message);
                else if (message.IsExpectingResponse)
                {
                    Logs.Error("Connection is missing a handler. OpCode: " + message.OpCode);
                    message.Respond(AckResponseStatus.Error);
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                if (RethrowExceptionsInEditor)
                    throw;
#endif

                Logs.Error("Failed to handle a message. OpCode: " + message.OpCode);
                Logs.Error(e);

                if (!message.IsExpectingResponse)
                    return;

                try
                {
                    message.Respond(AckResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
            }
        }
    }
}