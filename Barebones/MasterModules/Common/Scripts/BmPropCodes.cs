using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Property codes (for setting and getting <see cref="IPeer"/> properties)
    /// </summary>
    public class BmPropCodes
    {
        public const short Session = 32001;
        //public const short ServerInstanceId = 32002;
        public const short Game = 32002;
        public const short SpawnRequest = 32003;
        public const short SpawnId = 32004;

        public const short SpawnerLink = 32005;
        public const short Profile = 32006;
        public const short ChatChannels = 32007;
        public const short LocalChatChannel = 32008;
        public const short Guild = 32009;
        public const short AesKey = 32010;
        public const short AesKeyEncrypted = 32011;

        public const short Lobby = 32012;
    }
}