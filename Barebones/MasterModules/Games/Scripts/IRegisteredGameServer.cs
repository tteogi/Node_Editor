using System;
using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public delegate void AccessRequestCallback(GameAccessPacket access, string error);

    /// <summary>
    ///     This represents a state of Game Server within Master.
    /// </summary>
    public interface IRegisteredGameServer
    {
        /// <summary>
        ///     Unique game identifier
        /// </summary>
        int GameId { get; }

        /// <summary>
        ///     Number of players who play in this game server
        /// </summary>
        int OnlinePlayers { get; }

        /// <summary>
        ///     Maximum number of players who are allowed to play in this server
        /// </summary>
        int MaxPlayers { get; }

        /// <summary>
        ///     Name of the game
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     Connection to game server
        /// </summary>
        IPeer ServerPeer { get; }

        /// <summary>
        ///     If true, this game will not appear in regular listings
        /// </summary>
        bool IsPrivate { get; }

        /// <summary>
        ///     If false, players are not allowed to get in with regular request
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        ///     Properties of the game
        /// </summary>
        Dictionary<string, string> Properties { get; }

        /// <summary>
        ///     Password of the game
        /// </summary>
        string Password { get; }

        /// <summary>
        ///     RegistrationPacket, that was used to register game server to master server
        /// </summary>
        RegisterGameServerPacket RegistrationPacket { get; }

        /// <summary>
        ///     Event, which is invoked after game server opens
        /// </summary>
        event Action OnOpened;

        /// <summary>
        /// Event, which is invoked when game server disconnects from master
        /// </summary>
        event Action<IRegisteredGameServer> Disconnected;

        /// <summary>
        ///     Called when game server notifies master that new player has joined
        ///     (used an access code successfully)
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        bool OnPlayerJoined(ISession session);

        /// <summary>
        ///     Called when game server notifies master that a player has left
        /// </summary>
        ISession OnPlayerLeft(string username);

        /// <summary>
        ///     Sends an access request to game server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="successCallback"></param>
        /// <param name="failureCallback"></param>
        void RequestPlayerAccess(ISession session, AccessRequestCallback callback);

        /// <summary>
        ///     Sends an access request to game server
        /// </summary>
        /// <param name="session"></param>
        /// <param name="successCallback"></param>
        /// <param name="failureCallback"></param>
        void RequestPlayerAccess(ISession session, Dictionary<string, string> additionalInfo, 
            AccessRequestCallback callback);

        /// <summary>
        ///     Method, that should be called when game server disconnects
        /// </summary>
        void OnDisconnected();

        /// <summary>
        ///     Opens game server for public access
        /// </summary>
        void Open();

        /// <summary>
        /// Disconnects a player from game server
        /// </summary>
        /// <param name="session"></param>
        void DisconnectPlayer(ISession session);

        /// <summary>
        ///     Tries to destroy this game server (kill a process).
        ///     Default implementation sends a request to game server for it to destroy itself
        /// </summary>
        void Destroy();
    }
}