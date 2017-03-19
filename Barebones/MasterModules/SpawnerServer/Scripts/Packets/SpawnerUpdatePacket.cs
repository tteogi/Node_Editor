using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     RegistrationPacket, which is sent from spawner server to master server,
    ///     to up
    /// </summary>
    public class SpawnerUpdatePacket : SerializablePacket
    {
        public int RunningGameServersCount;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(RunningGameServersCount);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            RunningGameServersCount = reader.ReadInt32();
        }
    }
}