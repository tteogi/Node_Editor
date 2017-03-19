using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class GameAccessPacket : SerializablePacket
    {
        public string AccessToken;
        public string Address;
        public string SceneName = "";
        public Dictionary<string, string> Properties;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(AccessToken);
            writer.Write(Address);
            writer.Write(SceneName);
            
            Properties.ToWriter(writer);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            AccessToken = reader.ReadString();
            Address = reader.ReadString();
            SceneName = reader.ReadString();

            Properties = reader.ReadDictionary();
        }
    }
}