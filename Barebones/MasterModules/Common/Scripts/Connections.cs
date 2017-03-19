using System.Collections.Generic;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

/// <summary>
///     Class, that gives easy access to connections
/// </summary>
public class Connections
{
    public enum ConnectionId
    {
        ClientToMaster,
        GameToMaster,
        SpawnerToMaster
    }

    private static readonly Dictionary<ConnectionId, IClientSocket> _connections;

    public static IClientSocket ClientToMaster;
    public static IClientSocket GameToMaster;

    static Connections()
    {

        ClientToMaster = CreateClientSocket();
        GameToMaster = new ClientSocketWs();

        _connections = new Dictionary<ConnectionId, IClientSocket>();
        _connections.Add(ConnectionId.ClientToMaster, ClientToMaster);
        _connections.Add(ConnectionId.GameToMaster, GameToMaster);
    }

    public static IClientSocket CreateClientSocket()
    {
        return CreateClientSocket(true);
    }

    public static IClientSocket CreateClientSocket(bool useWebsockets)
    {
        if (useWebsockets)
        {
            return new ClientSocketWs();
        }

        return new ClientSocketUnet();
    }

    public static IServerSocket CreateServerSocket()
    {
        return CreateServerSocket(true);
    }

    public static IServerSocket CreateServerSocket(bool useWebsockets)
    {
        if (useWebsockets)
            return new ServerSocketWs();

        return new ServerSocketUnet();
    }

    public static IClientSocket GetConnection(ConnectionId connectionId)
    {
        return _connections[connectionId];
    }
}