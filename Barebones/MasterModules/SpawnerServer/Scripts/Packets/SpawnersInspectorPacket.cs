using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class SpawnersInspectorPacket : SerializablePacket
    {
        public List<SISpawnerData> Spawners;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Spawners.Count);

            foreach (var spawner in Spawners)
            {
                writer.Write(spawner.Ip);
                writer.Write(spawner.SpawnerId);
                writer.Write(spawner.MaxGameServers);
                writer.Write(spawner.Region);

                // Write all game servers
                writer.Write(spawner.GameServers.Count);
                foreach (var gameServer in spawner.GameServers)
                {
                    writer.Write(gameServer.GameId);
                    writer.Write(gameServer.SpawnId);
                    writer.Write(gameServer.CmdArgs);
                    writer.Write(gameServer.Name);
                    writer.Write(gameServer.CurrentPlayers);
                    writer.Write(gameServer.MaxPlayers);
                }
            }
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            var spawnerCount = reader.ReadInt32();
            Spawners = new List<SISpawnerData>();

            for (var i = 0; i < spawnerCount; i++)
            {
                var spawner = new SISpawnerData();
                spawner.Ip = reader.ReadString();
                spawner.SpawnerId = reader.ReadInt32();
                spawner.MaxGameServers = reader.ReadInt32();
                spawner.Region = reader.ReadString();

                spawner.GameServers = new List<SIGameServerData>();

                // Read each game server info
                var gsCount = reader.ReadInt32();
                for (var j = 0; j < gsCount; j++)
                {
                    var gs = new SIGameServerData();
                    gs.GameId = reader.ReadInt32();
                    gs.SpawnId = reader.ReadInt32();
                    gs.CmdArgs = reader.ReadString();
                    gs.Name = reader.ReadString();
                    gs.CurrentPlayers = reader.ReadInt32();
                    gs.MaxPlayers = reader.ReadInt32();

                    spawner.GameServers.Add(gs);
                }

                Spawners.Add(spawner);
            }
        }

        public class SISpawnerData
        {
            public string Ip;
            public int MaxGameServers;
            public int SpawnerId;
            public string Region;
            public List<SIGameServerData> GameServers;
        }

        public class SIGameServerData
        {
            public int GameId;
            public int SpawnId;
            public string CmdArgs;
            public string Name;
            public int CurrentPlayers;
            public int MaxPlayers;
        }
        
    }
}