using System;
using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public interface IGameLobby
    {
        int Id { get; }
        string Name { get; }
        bool ShowInLists { get; set; }
        string Type { get; set; }

        event Action<GameLobby> OnDestroy;

        int PlayerCount { get; }
        int MaxPlayers { get; }
        string ServerAddress { get; }

        string AddPlayer(ISession session);
        LobbyMember GetPlayer(string username);

        void Broadcast(IMessage message);
        void Broadcast(IMessage message, Func<LobbyMember, bool> condition);
        LobbyDataPacket GenerateLobbyData(IPeer requester);

        Dictionary<string, string> GetProperties();
    }
}