using System;

namespace Barebones.Networking
{
    /// <summary>
    ///     Generic packet handler
    /// </summary>
    public class PacketHandler : IPacketHandler
    {
        private readonly Action<IIncommingMessage> _handler;
        private readonly short _opCode;

        public PacketHandler(short opCode, Action<IIncommingMessage> handler)
        {
            _opCode = opCode;
            _handler = handler;
        }

        public short OpCode
        {
            get { return _opCode; }
        }

        public void Handle(IIncommingMessage message)
        {
            _handler.Invoke(message);
        }
    }
}