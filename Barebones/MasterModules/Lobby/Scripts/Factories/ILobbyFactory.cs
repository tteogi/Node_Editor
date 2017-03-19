using System.Collections.Generic;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents a factory which is responsible for
    /// creating game lobbies
    /// </summary>
    public interface ILobbyFactory
    {
        /// <summary>
        /// Creates and returns a game lobby, from properties given
        /// </summary>
        /// <param name="properties"></param>
        /// <returns></returns>
        GameLobby CreateLobby(Dictionary<string, string> properties);

        /// <summary>
        /// Factory identifier
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Invoked, when factory is registered to the lobbies module
        /// </summary>
        /// <param name="module"></param>
        void OnRegisteredToModule(LobbiesModule module);
    }
}