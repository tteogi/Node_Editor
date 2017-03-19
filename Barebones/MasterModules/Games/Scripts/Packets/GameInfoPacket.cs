using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class GameInfoPacket : SerializablePacket
    {
        public int Id;
        public string Address = "";
        public bool IsLobby;

        public bool IsManual;

        public bool IsPasswordProtected;
        public int MaxPlayers;
        public string Name;
        public int OnlinePlayers;
        public Dictionary<string, string> Properties;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Address ?? "");
            writer.Write(IsLobby);
            writer.Write(Name);
            writer.Write(OnlinePlayers);
            writer.Write(MaxPlayers);
            writer.Write(IsManual);

            writer.Write(IsPasswordProtected);

            var bytes = Properties != null ? Properties.ToBytes() : new byte[0];
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadInt32();
            Address = reader.ReadString();

            IsLobby = reader.ReadBoolean();
            Name = reader.ReadString();
            OnlinePlayers = reader.ReadInt32();
            MaxPlayers = reader.ReadInt32();
            IsManual = reader.ReadBoolean();

            IsPasswordProtected = reader.ReadBoolean();

            var length = reader.ReadInt32();
            if (length > 0)
                Properties = new Dictionary<string, string>()
                    .FromBytes(reader.ReadBytes(length));
            else
                Properties = new Dictionary<string, string>();
        }
    }
}