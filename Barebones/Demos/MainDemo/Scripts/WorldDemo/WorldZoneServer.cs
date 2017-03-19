using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Barebones.MasterServer;
using Barebones.Networking;

/// <summary>
/// This game server represents a single zone within "MMO" game.
/// (it's used to demonstrate how developers can setup multiple game servers
/// to be joined together, allowing player to walk from one to another)
/// </summary>
public class WorldZoneServer : UnetGameServer
{
    [Header("World Zone Info")]
    public string ZoneName = "Main";

    /// <summary>
    /// If server is not connected to master within given amount of time,
    /// it will be terminated
    /// </summary>
    public float ConnectToMasterTimeout = 10f;

    private HashSet<string> _pendingTeleportationRequests;

    // Use this for initialization
    protected override void Awake () {
        base.Awake();

        _pendingTeleportationRequests = new HashSet<string>();

        // If not a server
        if (!BmArgs.IsServer)
            return;

        StartCoroutine(HandleShutdown());
    }
	
	// Update is called once per frame
	void Update () {
	
	}

    protected override void OnServerUserJoined(UnetClient client)
    {
        MiniNetworkManager.SpawnPlayer(client.Connection, client.Username, "none");
    }

    protected override void OnServerUserLeft(UnetClient client)
    {
    }

    /// <summary>
    /// We want our MMO-type game servers to have a "zone" attribute,
    /// so we override this method to assing zone name to registration packet
    /// </summary>
    /// <param name="masterKey"></param>
    /// <returns></returns>
    public override RegisterGameServerPacket CreateRegisterPacket(string masterKey)
    {
        // Let the base method create the packet for us
        var registrationPacket = base.CreateRegisterPacket(masterKey);

        // Add the additional property
        registrationPacket.Properties.Add(WorldDemoModule.ZoneNameKey, ZoneName);

        return registrationPacket;
    }

    /// <summary>
    /// Sends a request to master server, to request a specific user to be
    /// teleported to another zone
    /// </summary>
    /// <param name="username"></param>
    /// <param name="zoneName"></param>
    public virtual void TeleportPlayerToAnotherZone(string username, string zoneName)
    {
        // Ignore if there's already a request pending
        if (_pendingTeleportationRequests.Contains(username))
            return;

        var client = GetClient(username);

        // Ignore if this user is not actually in the game
        if (client == null)
            return;

        var packet = new TeleportRequestPacket()
        {
            Username = username,
            ZoneName = zoneName
        };

        var msg = MessageHelper.Create(WorldDemoOpCodes.TeleportRequest, packet.ToBytes());

        MasterConnection.SendMessage(msg, (status, response) =>
        {
            // Remove from the list of pending requests
            _pendingTeleportationRequests.Remove(username);

            if (status != AckResponseStatus.Success)
            {
                Logs.Error(string.Format("Failed to teleport user '{0}' to zone '{1}': " +
                    response.AsString(), username, zoneName));
                return;
            }

            // At this point, we are certain that player got access to another zone,
            // so we can force disconnect the player. After that, player will enter the loading screen,
            // from which he will connect to another zone
            StartCoroutine(client.Disconnect());
        });
    }

    /// <summary>
    /// Takes care of shutting down game server (zone) when it's 
    /// necessary
    /// </summary>
    /// <returns></returns>
    private IEnumerator HandleShutdown()
    {
        Connections.GameToMaster.OnDisconnected += () =>
        {
            // Terminate application, when connection with master is lost
            Application.Quit();
        };

        yield return new WaitForSeconds(ConnectToMasterTimeout);

        if (!Connections.GameToMaster.IsConnected)
        {
            Application.Quit();
        }
    }
}
