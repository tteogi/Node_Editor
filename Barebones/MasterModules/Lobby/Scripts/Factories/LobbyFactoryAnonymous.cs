using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Barebones.MasterServer;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Lobby factory implementation, which simply invokes
    /// an anonymous method
    /// </summary>
    public class LobbyFactoryAnonymous : ILobbyFactory
    {
        private LobbiesModule _module;
        private readonly LobbyCreationFactory _factory;

        public delegate GameLobby LobbyCreationFactory(LobbiesModule module, Dictionary<string, string> properties);

        public LobbyFactoryAnonymous(string id, LobbyCreationFactory factory)
        {
            Id = id;
            _factory = factory;
        }

        public GameLobby CreateLobby(Dictionary<string, string> properties)
        {
            var lobby = _factory.Invoke(_module, properties);

            // Add the lobby type if it's not set by the factory method
            if (lobby != null && lobby.Type == null)
                lobby.Type = Id;

            return lobby;
        }

        public string Id { get; private set; }

        public void OnRegisteredToModule(LobbiesModule module)
        {
            _module = module;
        }
    }
}


