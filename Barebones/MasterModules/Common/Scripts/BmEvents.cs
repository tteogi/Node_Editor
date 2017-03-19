
namespace Barebones.MasterServer
{
    /// <summary>
    /// Holds master server event keys
    /// </summary>
    public static class BmEvents
    {
        // Primary events channel
        public static EventsChannel Channel = new EventsChannel("bm");

        // Game server
        public const string StartGameServer = "bbm.startGameServer";

        // Auth 
        public const string LoginRestoreForm = "bbm.auth.resoreLogin";

        // Lobby
        public const string OpenLobby = "bbm.openLobby";

        // General
        public const string Loading = "bbc.loading";
        public const string ShowDialogBox = "bbc.showDialogBox";
        
    }
}