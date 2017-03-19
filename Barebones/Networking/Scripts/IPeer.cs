using System;

namespace Barebones.Networking
{
    public delegate void ResponseCallback(byte status, IIncommingMessage response);

    /// <summary>
    ///     Represents connection peer
    /// </summary>
    public interface IPeer : IDisposable
    {
        /// <summary>
        ///     Unique peer id
        /// </summary>
        int Id { get; }

        /// <summary>
        ///     True, if connection is stil valid
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        ///     Invoked when peer disconnects
        /// </summary>
        event Action<IPeer> OnDisconnect;

        /// <summary>
        ///     Invoked when peer receives a message
        /// </summary>
        event Action<IIncommingMessage> OnMessage;

        /// <summary>
        ///     Force disconnect
        /// </summary>
        /// <param name="reason"></param>
        void Disconnect(string reason);

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs,
            DeliveryMethod deliveryMethod);

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <param name="timeoutSecs">If peer fails to respons within this time frame, callback will be invoked with timeout status</param>
        /// <returns></returns>
        int SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs);

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="responseCallback">Callback method, which will be invoked when peer responds</param>
        /// <returns></returns>
        int SendMessage(IMessage message, ResponseCallback responseCallback);

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="deliveryMethod">Delivery method</param>
        /// <returns></returns>
        void SendMessage(IMessage message, DeliveryMethod deliveryMethod);

        /// <summary>
        ///     Sends a message to peer
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <param name="ackCallback"></param>
        void SendMessage(short opCode, byte[] data, ResponseCallback ackCallback);

        /// <summary>
        ///     Stores a property into peer
        /// </summary>
        /// <param name="id"></param>
        /// <param name="data"></param>
        void SetProperty(int id, object data);

        /// <summary>
        ///     Retrieves a property from the peer
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        object GetProperty(int id);

        /// <summary>
        ///     Retrieves a property from the peer, and if it's not found,
        ///     retrieves a default value
        /// </summary>
        /// <param name="id"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        object GetProperty(int id, object defaultValue);
    }
}