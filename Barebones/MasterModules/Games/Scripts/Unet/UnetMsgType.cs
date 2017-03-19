using UnityEngine.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Message types, used in standard uNET HLAPI communications.
    /// </summary>
    public class UnetMsgType
    {
        public static short GameJoin = MsgType.Highest + 1;
        public static short LeaveGame = MsgType.Highest + 2;
        public static short AskToDisconnect = MsgType.Highest + 3;
        
        public const short Highest = MsgType.Highest + 3; // Always update
    }
}