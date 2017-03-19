using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class GameServerSpawnRequestPacket : SerializablePacket
    {
        /// <summary>
        ///     Custom args, that will be added when launching executable
        /// </summary>
        public string CustomArgs = " ";

        /// <summary>
        ///     Fps limit of the spawned game instance
        /// </summary>
        public int FpsLimit = 30;

        /// <summary>
        ///     Master key, which will be used by game server to register to master
        /// </summary>
        public string MasterKey = "";

        /// <summary>
        ///     Name of the scene, which should contain the server
        /// </summary>
        public string SceneName = "";

        /// <summary>
        ///     Unique identifier, which is used to tell which server has started
        /// </summary>
        public int SpawnId;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(SpawnId);
            writer.Write(CustomArgs);
            writer.Write(MasterKey);
            writer.Write(SceneName);
            writer.Write(FpsLimit);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            SpawnId = reader.ReadInt32();
            CustomArgs = reader.ReadString();
            MasterKey = reader.ReadString();
            SceneName = reader.ReadString();
            FpsLimit = reader.ReadInt32();
        }
    }
}