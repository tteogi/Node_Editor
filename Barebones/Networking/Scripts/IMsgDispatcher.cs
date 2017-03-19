namespace Barebones.Networking
{
    public interface IMsgDispatcher
    {
        /// <summary>
        /// Peer, to which we have connected
        /// </summary>
        IPeer Peer { get; }

        void SendMessage(short opCode, ISerializablePacket packet, DeliveryMethod method);
        void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback);
        void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, byte[] data, DeliveryMethod method);
        void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback);
        void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, string data, DeliveryMethod method);
        void SendMessage(short opCode, string data, ResponseCallback responseCallback);
        void SendMessage(short opCode, string data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, int data, DeliveryMethod method);
        void SendMessage(short opCode, int data, ResponseCallback responseCallback);
        void SendMessage(short opCode, int data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(IMessage message, DeliveryMethod method);
        void SendMessage(IMessage message, ResponseCallback responseCallback);
        void SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs);
    }
}