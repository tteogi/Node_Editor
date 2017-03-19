using Barebones.Networking;

public class TeleportRequestPacket : SerializablePacket
{
    public string Username;
    public string ZoneName;

    public override void ToBinaryWriter(EndianBinaryWriter writer)
    {
        writer.Write(Username);
        writer.Write(ZoneName);
    }

    public override void FromBinaryReader(EndianBinaryReader reader)
    {
        Username = reader.ReadString();
        ZoneName = reader.ReadString();
    }
}
