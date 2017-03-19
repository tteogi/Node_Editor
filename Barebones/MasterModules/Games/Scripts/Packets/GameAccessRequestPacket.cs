using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class GameAccessRequestPacket : SerializablePacket
    {
        public int SessionId;
        public string Username;
        public Dictionary<string, string> AdditionalData;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(SessionId);
            writer.WriteDictionary(AdditionalData);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            SessionId = reader.ReadInt32();
            AdditionalData = reader.ReadDictionary();
        }
    }
}