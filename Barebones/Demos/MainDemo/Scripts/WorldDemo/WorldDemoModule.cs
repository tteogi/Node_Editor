using System;
using System.Collections.Generic;
using System.Linq;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

/// <summary>
/// This is the main module of the World demo.
/// </summary>
public class WorldDemoModule : MasterModule
{
    public const string ZoneNameKey = "ZoneName";

    protected IMaster Master;
    protected AuthModule AuthModule;
    protected GamesModule Games;
    protected SpawnersModule SpawnersModule;

    private bool _areZonesSpawned;

    /// <summary>
    /// If this is set to true, master server on editor will not spawn game zones
    /// </summary>
    public bool DontSpawnZonesInEditor = true;

    void Awake()
    {
        // Destroy this game object if it already exists
        if (DestroyIfExists()) Destroy(gameObject);

        // Don't destroy the module on load
        DontDestroyOnLoad(gameObject);

        // Register dependencies
        AddDependency<AuthModule>();
        AddDependency<GamesModule>();
        AddDependency<SpawnersModule>();
    }

    /// <summary>
    /// This is only called on the master server
    /// </summary>
    /// <param name="master"></param>
    public override void Initialize(IMaster master)
    {
        Master = master;
        AuthModule = master.GetModule<AuthModule>();
        Games = master.GetModule<GamesModule>();
        SpawnersModule = master.GetModule<SpawnersModule>();

        // Add game server handlers
        Games.SetGameServerHandler(new PacketHandler(WorldDemoOpCodes.TeleportRequest, HandleTeleportRequest));

        // Add client handlers
        master.SetClientHandler(new PacketHandler(WorldDemoOpCodes.EnterWorldRequest, HandleEnterWorldRequest));
        master.SetClientHandler(new PacketHandler(WorldDemoOpCodes.GetCurrentZoneAccess, HandleGetZoneAccess));

        //----------------------------------------------
        // Spawn game servers (zones)

        // Find a spawner 
        var spawner = SpawnersModule.GetSpawners().FirstOrDefault();

        if (spawner != null)
        {
            // We found a spawner
            SpawnZoneServers(spawner);
        }
        else
        {
            // Spawners are not yet registered to the master, 
            // so let's listen to an event and wait for them
            SpawnersModule.OnSpawnerRegistered += link =>
            {
                // Ignore if zones are already spawned
                if (_areZonesSpawned) return;

                // Spawn the zones
                SpawnZoneServers(link);

                _areZonesSpawned = true;
            };
        }
    }

    /// <summary>
    /// Spawns all of the zones for the demo
    /// </summary>
    /// <param name="spawner"></param>
    public virtual void SpawnZoneServers(SpawnerLink spawner)
    {
#if UNITY_EDITOR
        if (DontSpawnZonesInEditor)
            return;
#endif

#if !UNITY_EDITOR
        // Server will start 
        if (!Environment.GetCommandLineArgs().Contains("-spawnZones"))
            return;
#endif

        spawner.OrderSpawn(GenerateZoneSpawnInfo("WorldZoneMain"))
            .WhenDone((task => Logs.Info("Main zone spawn status: " + task.Status)))
            .Start();

        spawner.OrderSpawn(GenerateZoneSpawnInfo("WorldZoneSecondary"))
            .WhenDone((task => Logs.Info("Secondary zone spawn status: " + task.Status)))
            .Start();
    }

    /// <summary>
    /// Helper method, which generates a settings dictionary for our zones
    /// </summary>
    /// <param name="sceneName"></param>
    /// <returns></returns>
    public Dictionary<string, string> GenerateZoneSpawnInfo(string sceneName)
    {
        return new Dictionary<string, string>()
        {
            {GameProperty.SceneName, sceneName },
            {GameProperty.IsPrivate, "true" },
            {GameProperty.GameName, sceneName}
        };
    }

#region Message handlers

    /// <summary>
    /// Handles client's request to get access to the zone
    /// he is supposed to be in
    /// </summary>
    /// <param name="message"></param>
    public virtual void HandleGetZoneAccess(IIncommingMessage message)
    {
        var access = message.Peer.GetProperty(WorldDemoPropCodes.ZoneAccess) as GameAccessPacket;

        if (access == null)
        {
            message.Respond("No access found", AckResponseStatus.Failed);
            return;
        }

        message.Respond(access, AckResponseStatus.Success);

        // Delete the access (making it usable only once)
        message.Peer.SetProperty(WorldDemoPropCodes.ZoneAccess, null);
    }

    /// <summary>
    /// Handles a request from game server to teleport
    /// user to another game server / zone.
    /// </summary>
    /// <param name="message"></param>
    public virtual void HandleTeleportRequest(IIncommingMessage message)
    {
        var request = message.DeserializePacket(new TeleportRequestPacket());

        var userSession = AuthModule.GetLoggedInSession(request.Username);
        var peer = userSession.Peer;

        // Find the game server wchich represent the zone we need
        var gameServer = Games.GetOpenServers()
            .Where(s => s.Properties.ContainsKey(ZoneNameKey))
            .FirstOrDefault(s => s.Properties[ZoneNameKey] == request.ZoneName);

        if (gameServer == null)
        {
            // If no game server with that zone name was found
            message.Respond("Zone was not found", AckResponseStatus.Failed);
            return;
        }

        // Request an access to game server
        gameServer.RequestPlayerAccess(userSession, (access, error) =>
        {
            if (access == null)
            {
                // We didn't get the access
                message.Respond("Failed to get access to the zone: " + error, AckResponseStatus.Failed);
                return;
            }

            // We have the access to new zone, let's store it
            // so player can request it when he's on the loading screen
            peer.SetProperty(WorldDemoPropCodes.ZoneAccess, access);

            // Notify game server that access was received
            message.Respond(AckResponseStatus.Success);
        });
    }

    /// <summary>
    /// Handles users request to join the game world.
    /// It picks a random (*first) game server
    /// </summary>
    /// <param name="message"></param>
    public virtual void HandleEnterWorldRequest(IIncommingMessage message)
    {
        var session = message.Peer.GetProperty(BmPropCodes.Session) as Session;

        if (session == null || session.Username == null)
        {
            // Invalid player session
            message.Respond("Invalid session", AckResponseStatus.Unauthorized);
            return;
        }

        // Get world servers. We can filter world zones by checking
        // if a game server has a zone name key
        var worldServers = Games.GetOpenServers()
            .Where(s => s.Properties.ContainsKey(ZoneNameKey));

        // Find which zone we should be getting into.

        // You'd probably want to load the name of the zone
        // the user was in before quitting the game, but to keep this
        // example simple, we'll just take the first zone from the list
        var gameServer = worldServers.FirstOrDefault();

        if (gameServer == null)
        {
            message.Respond("Zone not found", AckResponseStatus.Failed);
            return;
        }

        // Request an access
        gameServer.RequestPlayerAccess(session, (access, error) =>
        {
            if (access == null)
            {
                // We didn't get the access
                message.Respond("Failed to get access to the zone: " + error, AckResponseStatus.Failed);
                return;
            }

            // We have the access to new zone, let's store it
            // so player can request it when he's on the loading screen
            message.Peer.SetProperty(WorldDemoPropCodes.ZoneAccess, access);

            // Notify client that he's ready to enter the zone
            message.Respond(access, AckResponseStatus.Success);
        });
    }

#endregion

    public class WorldDemoPropCodes
    {
        public const int ZoneAccess = 101;
    }
}
