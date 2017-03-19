using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class RoomJoinRequestDataPacket : SerializablePacket
    {
        public int RoomId;
        public string RoomPassword = "";

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(RoomId);
            writer.Write(RoomPassword);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            RoomId = reader.ReadInt32();
            RoomPassword = reader.ReadString();
        }
    }
}