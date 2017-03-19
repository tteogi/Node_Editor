using System;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.Networking
{
    /// <summary>
    ///     This is an abstract implementation of <see cref="IPeer" /> interface,
    ///     which handles acknowledgements and SendMessage overloads.
    ///     Extend this, if you want to implement custom protocols
    /// </summary>
    public abstract class BasePeer : IPeer
    {
        public static bool DontCatchExceptionsInEditor = true;

        private static readonly object _idGenerationLock = new object();
        private static int _peerIdGenerator;

        /// <summary>
        ///     Default timeout, after which response callback is invoked with
        ///     timeout status.
        /// </summary>
        public static int DefaultTimeoutSecs = 60;

        private readonly Dictionary<int, ResponseCallback> _acks;

        protected readonly List<long[]> _ackTimeoutQueue;
        private readonly Dictionary<int, object> _data;

        private int _id = -1;

        private int _nextAckId = 1;

        private IIncommingMessage _timeoutMessage;

        protected BasePeer()
        {
            _data = new Dictionary<int, object>();
            _acks = new Dictionary<int, ResponseCallback>(30);
            _ackTimeoutQueue = new List<long[]>();
            BTimer.Instance.OnTick += HandleAckDisposalTick;

            _timeoutMessage = new IncommingMessage(-1, 0, "Time out".ToBytes(), DeliveryMethod.Reliable, this)
            {
                StatusCode = AckResponseStatus.Timeout
            };
        }

        public event Action<IIncommingMessage> OnMessage;
        public event Action<IPeer> OnDisconnect;

        public void SendMessage(short opCode, byte[] data, ResponseCallback ackCallback)
        {
            var message = MessageHelper.Create(opCode, data);
            SendMessage(message, ackCallback);
        }

        /// <summary>
        ///     Saves data into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        public void SetProperty(int id, object data)
        {
            if (_data.ContainsKey(id))
                _data[id] = data;
            else
                _data.Add(id, data);
        }

        /// <summary>
        ///     Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public object GetProperty(int id)
        {
            object value;

            _data.TryGetValue(id, out value);

            return value;
        }

        /// <summary>
        ///     Retrieves data from peer, which was stored with <see cref="SetProperty" />
        /// </summary>
        /// <param name="id"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public object GetProperty(int id, object defaultValue)
        {
            var obj = GetProperty(id);
            return obj ?? defaultValue;
        }

        public void Dispose()
        {
            BTimer.Instance.OnTick -= HandleAckDisposalTick;
        }

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback)
        {
            return SendMessage(message, responseCallback, DefaultTimeoutSecs);
        }

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback,
            int timeoutSecs)
        {
            return SendMessage(message, responseCallback, timeoutSecs, DeliveryMethod.Reliable);
        }

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public int SendMessage(IMessage message, ResponseCallback responseCallback,
            int timeoutSecs, DeliveryMethod deliveryMethod)
        {
            if (!IsConnected)
            {
                responseCallback.Invoke(AckResponseStatus.NotConnected, null);
                return -1;
            }

            var id = RegisterAck(message, responseCallback, timeoutSecs);

            SendMessage(message, deliveryMethod);

            return id;
        }

        /// <summary>
        ///     True, if connection is stil valid
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        public abstract void SendMessage(IMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        ///     Force disconnect
        /// </summary>
        /// <param name="reason"></param>
        public abstract void Disconnect(string reason);

        public void NotifyDisconnectEvent()
        {
            if (OnDisconnect != null)
                OnDisconnect(this);
        }

        protected void NotifyMessageEvent(IIncommingMessage message)
        {
            if (OnMessage != null)
                OnMessage(message);
        }

        protected int RegisterAck(IMessage message, ResponseCallback responseCallback,
            int timeoutSecs)
        {
            int id;

            lock (_acks)
            {
                id = _nextAckId++;
                _acks.Add(id, responseCallback);
            }

            message.AckRequestId = id;

            StartAckTimeout(id, timeoutSecs);
            return id;
        }

        protected void TriggerAck(int ackId, byte statusCode, IIncommingMessage message)
        {
            lock (_acks)
            {
                ResponseCallback ackCallback;
                _acks.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null) return;

                _acks.Remove(ackId);
                ackCallback(statusCode, message);
            }
        }

        private void StartAckTimeout(int ackId, int timeoutSecs)
        {
            // +1, because it might be about to tick in a few miliseconds
            _ackTimeoutQueue.Add(new[] {ackId, BTimer.CurrentTick + timeoutSecs + 1});
        }

        public virtual void HandleMessage(IIncommingMessage message)
        {
            if (OnMessage != null)
                OnMessage(message);
        }

        public void HandleDataReceived(byte[] buffer, int start)
        {
            IIncommingMessage message = null;

            try
            {
                message = MessageHelper.FromBytes(buffer, start, this);

                if (message.AckRequestId.HasValue)
                {
                    // We received a message which is a response to our ack request
                    TriggerAck(message.AckRequestId.Value, message.StatusCode, message);
                    return;
                }
            }
            catch (Exception e)
            {
#if UNITY_EDITOR
                if (DontCatchExceptionsInEditor)
                    throw e;
#endif

                Debug.LogError("Failed parsing an incomming message: " + e);

                return;
            }

            HandleMessage(message);
        }

        #region Ack Disposal Stuff

        /// <summary>
        ///     Unique id
        /// </summary>
        public int Id
        {
            get
            {
                if (_id < 0)
                    lock (_idGenerationLock)
                    {
                        if (_id < 0)
                            _id = _peerIdGenerator++;
                    }
                return _id;
            }
        }

        /// <summary>
        ///     Called when ack disposal thread ticks
        /// </summary>
        private void HandleAckDisposalTick(long currentTick)
        {
            // TODO test with ordered queue, might be more performant
            _ackTimeoutQueue.RemoveAll(a =>
            {
                if (a[1] > currentTick) return false;

                try
                {
                    CancelAck((int) a[0], AckResponseStatus.Timeout);
                }
                catch (Exception e)
                {
                    Logs.Error(e);
                }

                return true;
            });
        }

        private void CancelAck(int ackId, byte responseCode)
        {
            lock (_acks)
            {
                ResponseCallback ackCallback;
                _acks.TryGetValue(ackId, out ackCallback);

                if (ackCallback == null) return;

                _acks.Remove(ackId);
                ackCallback(responseCode, _timeoutMessage);
            }
        }

        #endregion
    }
}