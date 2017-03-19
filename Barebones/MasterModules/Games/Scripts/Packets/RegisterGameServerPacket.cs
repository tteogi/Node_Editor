using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     RegistrationPacket, which is sent from game server to master server, to notify
    ///     master that game server is fully set up
    /// </summary>
    public class RegisterGameServerPacket : SerializablePacket
    {
        public byte[] ExtraData = new byte[0];
        public bool IsPrivate;
        public string MasterKey = "";
        public int MaxPlayers = 16;
        public string Name = "Untitled";
        public string Password = "";
        public string CmdArgs = "";
        public Dictionary<string, string> Properties;
        public string PublicAddress;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PublicAddress);
            writer.Write(Name);
            writer.Write(MaxPlayers);
            writer.Write(IsPrivate);
            writer.Write(Password);
            writer.Write(MasterKey);
            writer.Write(CmdArgs);

            // Additional dictionary
            var bytes = Properties != null ? Properties.ToBytes() : new byte[0];
            writer.Write(bytes.Length);
            writer.Write(bytes);

            // Aditional bytes
            writer.Write(ExtraData != null ? ExtraData.Length : 0);
            if (ExtraData != null)
                writer.Write(ExtraData);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PublicAddress = reader.ReadString();
            Name = reader.ReadString();
            MaxPlayers = reader.ReadInt32();
            IsPrivate = reader.ReadBoolean();
            Password = reader.ReadString();
            MasterKey = reader.ReadString();
            CmdArgs = reader.ReadString();

            // Additional dictionary
            var length = reader.ReadInt32();
            if (length > 0)
                Properties = new Dictionary<string, string>()
                    .FromBytes(reader.ReadBytes(length));

            // Aditional bytes
            var dataLength = reader.ReadInt32();
            if (dataLength > 0)
                ExtraData = reader.ReadBytes(dataLength);
        }
    }
}