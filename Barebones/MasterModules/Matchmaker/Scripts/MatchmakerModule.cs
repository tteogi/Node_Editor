using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// This is an example of match finder
    /// </summary>
    public class MatchmakerModule : MasterModule
    {
        protected LobbiesModule Lobbies;
        protected GamesModule Games;

        protected List<IGamesListProvider> GameProviders;

        protected virtual void Awake()
        {
            AddDependency<LobbiesModule>();
            AddDependency<GamesModule>();

            GameProviders = new List<IGamesListProvider>();
        }

        public override void Initialize(IMaster master)
        {
            Lobbies = master.GetModule<LobbiesModule>();
            Games = master.GetModule<GamesModule>();

            GameProviders.Add(Lobbies);
            GameProviders.Add(Games);

            master.SetClientHandler(new PacketHandler(BmOpCodes.GamesListRequest, HandleGamesListRequest));
            master.SetClientHandler(new PacketHandler(BmOpCodes.FindMatch, HandleFindMatchRequest));
        }

        #region Message handlers

        /// <summary>
        /// Handles a request to find a random match
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleFindMatchRequest(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            // Get the request parameters
            var parameters = new Dictionary<string, string>().FromBytes(message.AsBytes());

            string lobbyType;
            parameters.TryGetValue("type", out lobbyType);

            // Get lobbies that are not full
            var lobbies = Lobbies.GetLobbies(l => l.PlayerCount < l.MaxPlayers);

            // Filter by lobby type if provided
            if (lobbyType != null)
                lobbies = lobbies.Where(l => l.Type == lobbyType);

            // Find the first lobby which didn't give an error when trying to add a player
            var lobby = lobbies.FirstOrDefault(l => l.AddPlayer(session) == null);

            if (lobby != null)
            {
                // We found a lobby, everythings great!
                message.Respond(AckResponseStatus.Success);
                return;
            }

            // ------------------------------------------
            // If we got here, we didn't find a lobby yet
            // so we need to try and create one

            var createdLobby = Lobbies.CreateLobby(parameters, lobbyType);

            if (createdLobby == null || createdLobby.AddPlayer(session) != null)
            {
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            Lobbies.RegisterLobby(createdLobby);

            message.Respond(AckResponseStatus.Success);

        }

        /// <summary>
        /// Handles a request to retrieve a list of available games
        /// </summary>
        /// <param name="message"></param>
        protected virtual void HandleGamesListRequest(IIncommingMessage message)
        {
            // Establish the filters
            var filters = message.HasData ?
                new Dictionary<string, string>().FromBytes(message.AsBytes()) :
                new Dictionary<string, string>();

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            var list = new List<GameInfoPacket>();

            foreach (var provider in GameProviders)
            {
                list.AddRange(provider.GetPublicGames(session, filters));
            }

            // Convert to generic list and serialize to bytes
            var bytes = list.Select(l => (ISerializablePacket)l).ToBytes();

            message.Respond(bytes, AckResponseStatus.Success);
        }

        #endregion
    }
}