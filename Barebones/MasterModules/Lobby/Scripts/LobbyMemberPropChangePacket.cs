using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    /// RegistrationPacket, containing data about which player changed which property
    /// </summary>
    public class LobbyMemberPropChangePacket : SerializablePacket
    {
        public string Username;
        public string Property;
        public string Value;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(Property);
            writer.Write(Value);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            Property = reader.ReadString();
            Value = reader.ReadString();
        }
    }
}