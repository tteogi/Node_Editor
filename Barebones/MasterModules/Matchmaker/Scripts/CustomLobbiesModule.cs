namespace Barebones.MasterServer
{
    public class CustomLobbiesModule : LobbiesModule
    {
        protected override GameInfoPacket GenerateGameInfo(ISession session, IGameLobby lobby)
        {
            // Get info from the base method
            var info =  base.GenerateGameInfo(session, lobby);

            var concreteLobby = lobby as GameLobby;

            // Add a new property
            info.Properties.Add("state", concreteLobby.State.ToString());

            return info;
        }
    }
}