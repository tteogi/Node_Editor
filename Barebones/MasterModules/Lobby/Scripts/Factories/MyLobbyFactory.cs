using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class MyLobbyFactory : ILobbyFactory
    {
        private LobbiesModule _module;

        public MyLobbyFactory(string id)
        {
            Id = id;
        }

        public GameLobby CreateLobby(Dictionary<string, string> properties)
        {
            // Create the teams
            var teamA = new LobbyTeam("Counter Terrorists")
            {
                MaxPlayers = 5,
                MinPlayers = 0
            };
            var teamB = new LobbyTeam("Terrorists")
            {
                MaxPlayers = 5,
                MinPlayers = 0
            };

            // Set their colors (this could be any property)
            teamA.SetProperty("color", "0000FF");
            teamB.SetProperty("color", "FF0000");

            // Create configuration
            var config = new LobbyConfig();
            config.EnableManualStart = true;
            config.AllowJoiningWhenGameIsLive = true;
            config.EnableGameMasters = true;

            // Get the lobby name
            var lobbyName = properties.ContainsKey(GameProperty.GameName)
                ? properties[GameProperty.GameName]
                : "Default name";

            // Create the lobby
            var lobby = new GameLobby(_module.GenerateLobbyId(),
                new[] { teamA, teamB }, _module, config)
            {
                Name = lobbyName,
                Type = Id
            };

            // Override properties with what user provided
            lobby.SetLobbyProperties(properties);

            // Add control for the game speed
            lobby.AddControl(new LobbyPropertyData()
            {
                Label = "Game Speed",
                Options = new List<string>() { "1x", "2x", "3x" },
                PropertyKey = "speed"
            }, "2x"); // Default option

            // Add control to enable/disable gravity
            lobby.AddControl(new LobbyPropertyData()
            {
                Label = "Gravity",
                Options = new List<string>() { "On", "Off" },
                PropertyKey = "gravity",
            });

            return lobby;
        }

        public string Id { get; protected set; }

        public void OnRegisteredToModule(LobbiesModule module)
        {
            _module = module;
        }
    }
}