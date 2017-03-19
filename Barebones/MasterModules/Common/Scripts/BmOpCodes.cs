namespace Barebones.MasterServer
{
    /// <summary>
    /// Operation codes, used within Master Server Framework
    /// </summary>
    public class BmOpCodes
    {
        public const short Error = -1;

        public const short Login = 32000;
        public const short Register = 32001;
        public const short CreateGameServer = 32002;
        public const short CreateGameStatus = 32003;
        public const short RegisterSpawner = 32004;
        public const short RegisterGameServer = 32005;
        public const short AccessRequest = 32006;
        public const short GamesListRequest = 32007;

        public const short SpawnGameServer = 32008;
        public const short UnityProcessStarted = 32009;

        public const short PlayerJoinedGame = 32010;
        public const short PlayerLeftGame = 32011;

        public const short DisconnectPlayer = 32012;

        public const short KillProcess = 32013;
        public const short OpenGameServer = 32014;
        public const short SpawnerUpdate = 32015;
        public const short AbortSpawn = 32016;
        public const short CreatedGameAccessRequest = 32017;
        public const short SpawnerGameClosed = 32018;

        public const short ProfileRequest = 32019;
        public const short ProfileUpdate = 32020;
        public const short JoinChatChannel = 32021;
        public const short LeaveChatChannel = 32022;
        public const short ChatMessage = 32023;
        public const short GetUserChannels = 32024;
        public const short CreateGuild = 32025;

        public const short PasswordResetCodeRequest = 32026;
        public const short PasswordChange = 32027;
        public const short ConfirmEmail = 32028;
        public const short AesKeyRequest = 32029;

        public const short ConfirmEmailCodeRequest = 32030;
        public const short LobbyJoin = 32031;
        public const short LobbyLeave = 32032;
        public const short LobbyCreate = 32033;
        public const short LobbyInfo = 32034;
        public const short LobbyPropertySet = 32035;
        public const short LobbyMemberPropertySet = 32036;
        public const short LobbySetReady = 32037;
        public const short LobbyStartGame = 32038;
        public const short LobbyStateChange = 32039;
        public const short LobbyMasterChange = 32040;
        public const short LobbyChatMessage = 32041;
        public const short LobbyJoinTeam = 32042;
        public const short LobbyGameAccessRequest = 32043;
        public const short LobbyAttachToGame = 32044;
        public const short LobbyGetPlayerData = 32045;
        public const short LobbyIsInLobby = 32046;
        public const short LobbyStatusTextChange = 32047;

        public const short FindMatch = 32048;
        public const short GetLoggedInCount = 32049;

        public const short SpawnerInspectorDataRequest = 32050;
        public const short GetGameProcesses = 32051;
        public const short GameProcessCreated = 32052;
        public const short GameProcessKilled = 32053;

        public const short SetLocalChannel = 32054;
    }
}