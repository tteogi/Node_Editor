using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class StartGameServerData
    {
        public string GameIpAddress;
        public int GamePort;
        public IClientSocket MasterConnection;
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
        public bool UseWebsockets { get; set; }
    }
}