using System.Collections.Generic;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents a member of the lobby
    /// </summary>
    public class LobbyMember
    {
        /// <summary>
        /// Player's session
        /// </summary>
        public readonly ISession PlayerSession;

        /// <summary>
        /// Player's properties
        /// </summary>
        protected Dictionary<string, string> Properties;

        public LobbyMember(ISession playerSession)
        {
            PlayerSession = playerSession;
            Properties = new Dictionary<string, string>();
        }

        /// <summary>
        /// True, if member is ready to play
        /// </summary>
        public bool IsReady { get; set; }

        /// <summary>
        /// A lobby team, to which this member belongs
        /// </summary>
        public virtual LobbyTeam Team { get; set; }

        /// <summary>
        /// Changes property value of the player
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void SetProperty(string key, string value)
        {
            Properties[key] = value;
        }

        /// <summary>
        /// Retrieves a property value of current member
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string GetProperty(string key)
        {
            string result;
            Properties.TryGetValue(key, out result);
            return result;
        }
        
        /// <summary>
        /// Creates a lobby member data packet
        /// </summary>
        /// <returns></returns>
        public virtual LobbyMemberData GeneratePacket()
        {
            return new LobbyMemberData()
            {
                IsReady = IsReady,
                Name = PlayerSession.Username,
                Properties = Properties, // Consider cloning properties
                Team = Team != null ? Team.Name : ""
            };
        }
    }
}