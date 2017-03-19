using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public interface IGamesListProvider
    {
        IEnumerable<GameInfoPacket> GetPublicGames(ISession user, Dictionary<string, string> filters);
    }
}