using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Packet, which is sent from spawner server to master server
    ///     when spawner server starts
    /// </summary>
    public class SpawnerRegisterPacket : SerializablePacket
    {
        /// <summary>
        ///     How many servers this spawner supports
        /// </summary>
        public int MaxServers;

        /// <summary>
        /// Public IP of the machine, on which the server is running
        /// </summary>
        public string PublicIp;

        public string Region = "";

        /// <summary>
        ///     Properties of the spawner server
        /// </summary>
        public Dictionary<string, string> Properties;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(MaxServers);
            writer.Write(PublicIp);
            writer.Write(Region);
            writer.WriteDictionary(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            MaxServers = reader.ReadInt32();
            PublicIp = reader.ReadString();
            Region = reader.ReadString();
            Properties = reader.ReadDictionary();
        }
    }
}