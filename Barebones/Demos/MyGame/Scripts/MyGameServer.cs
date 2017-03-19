using Barebones.MasterServer;
using UnityEngine;

public class MyGameServer : UnetGameServer {

    /// <summary>
    /// Called, when authenticated user sends a valid access token
    /// </summary>
    /// <param name="client"></param>
    protected override void OnServerUserJoined(UnetClient client)
    {
        // Retrieve a profile first
        ProfilesModule.GetProfile(client.Username, MasterConnection, profile =>
        {
            if (profile == null)
            {
                // We failed to retrieve players profile, let's disconnect him
                DisconnectPlayer(client.Username);
                return;
            }

            // Get coins property
            var coinsProperty = profile.GetProperty<ObservableInt>(MyProfileKeys.Coins);

            // Spawn player (the same method we used earlier)
            var player = MiniNetworkManager.SpawnPlayer(client.Connection, client.Username, "knife");
            player.Coins = coinsProperty.Value;

            // Subscribe to the coins change listener.
            // It will be invoked on server, when client picks up a coin.
            player.OnCoinsChanged += () =>
            {
                // Update clients profile by setting a different coins value
                coinsProperty.Set(player.Coins);
            };
        });
    }

    protected override void OnServerUserLeft(UnetClient client)
    {
    }
}
