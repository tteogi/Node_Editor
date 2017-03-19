using Barebones.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace Barebones.MasterServer
{
    public class GameProcessInfoPacket : MessageBase
    {
        public string CmdArgs;
        public int ProcessId;
        public int SpawnId;
    }
}