using System.Collections;
using Barebones.MasterServer;
using UnityEngine;

/// <summary>
/// This script represents our game server logic.
/// It implements <see cref="IGameServer"/> interface by extending
/// <see cref="UnetGameServer"/>
/// </summary>
public class RoomGameServer : UnetGameServer
{
    protected LobbyDataPacket LobbyData;

    public override void StartGameServerWithLobby(bool startHost, LobbyDataPacket lobbyData)
    {
        base.StartGameServerWithLobby(startHost, lobbyData);

        // Save the lobby data for later use
        LobbyData = lobbyData;
    }

    /// <summary>
    /// This is called after client connects to game server,
    /// and provides a valid access token
    /// </summary>
    /// <param name="client"></param>
    protected override void OnServerUserJoined(UnetClient client)
    {
        if (LobbyId.HasValue)
        {
            // If this game server has lobby attached
            SetupPlayerWithLobbyData(client);
            return;
        }

        // We could actually spawn a user right here, but we want to
        // utilize player profiles, so let's request one first
        Game.GetProfile(client.Username, profile =>
        {
            if (profile == null)
            {
                // We failed to retrieve players profile, let's disconnect him
                DisconnectPlayer(client.Username);
                return;
            }

            // We've received the profile, and can now spawn the player character
            SpawnPlayer(client, profile);
        });
    }

    protected virtual void SetupPlayerWithLobbyData(UnetClient client)
    {
        // Get lobby data of the player
        LobbiesModule.GetPlayerData(client.Username, (data, error) =>
        {
            if (data == null)
            {
                // Disconnect player if we couldn't get his connection data
                DisconnectPlayer(client.Username);

                Logs.Error("Game server couldn't retrieve lobby player data");
                return;
            }

            // Get data of the team a player belongs to
            var team = LobbyData.Teams[data.Team];

            var teamColor = team.Properties.ContainsKey("color") ? team.Properties["color"] : "";

            // Get players profile
            Game.GetProfile(client.Username, profile =>
            {
                var playerChar = SpawnPlayer(client, profile);

                // Set players team color
                playerChar.FlagColor = teamColor;
            });

        });
    }

    /// <summary>
    /// Creates a player object in the scene
    /// </summary>
    /// <param name="client"></param>
    /// <param name="profile"></param>
    private MiniPlayerController SpawnPlayer(UnetClient client, ObservableProfile profile)
    {
        // Get some of the properties from the profile
        var coinsProperty = profile.GetProperty<ObservableInt>(RoomsDemoProfileKeys.Coins);
        var weaponProperty = profile.GetProperty<ObservableString>(RoomsDemoProfileKeys.Weapon);

        // Weapon property was a string with a name, we want to get the actual item object
        var weapon = RoomsDemo.GetItemTemplate(weaponProperty.Value);

        // Spawn players character
        var player = MiniNetworkManager.SpawnPlayer(client.Connection, client.Username, weapon.Sprite);

        // Set player coins from the property
        player.Coins = coinsProperty.Value;

        // Subscribe to the coins change listener.
        // It will be invoked on server, when client picks up a coin.
        player.OnCoinsChanged += () =>
        {
            // Server will update a number of coins the player has
            coinsProperty.Set(player.Coins);
        };

        return player;
    }

    /// <summary>
    /// Invoked when player leaves game server
    /// </summary>
    /// <param name="client"></param>
    protected override void OnServerUserLeft(UnetClient client)
    {
    }
}
