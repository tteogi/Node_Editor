using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Interface, which should be implemented by game servers
    /// </summary>
    public interface IGameServer
    {
        /// <summary>
        ///     Should create a packet which will be sent to Master Server to register
        /// </summary>
        /// <returns></returns>
        RegisterGameServerPacket CreateRegisterPacket(string masterKey);

        /// <summary>
        ///     Should force disconnect a player
        /// </summary>
        /// <param name="username"></param>
        void DisconnectPlayer(string username);

        /// <summary>
        ///     Should return a scene, to which a client must switch before connecting to game server
        /// </summary>
        /// <returns></returns>
        string GetClientConnectionScene();

        /// <summary>
        ///     Called, when game server is successfully registered to master
        /// </summary>
        /// <param name="game"></param>
        void OnRegisteredToMaster(IClientSocket masterConnection, RegisteredGame game);

        /// <summary>
        ///     Called, when game server is successfully opened to public
        /// </summary>
        void OnOpenedToPublic();

        void Shutdown();

        /// <summary>
        /// Checks if access should be given to the requester.
        /// Returns false, if access should not be given.
        /// This is a good place to implement custom logic for player bans and etc.
        /// Most basic implementation can just "return true"
        /// </summary>
        /// <param name="request"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        bool ValidateAccessRequest(GameAccessRequestPacket request, out string error);

        /// <summary>
        /// This is called right before game server gives access data to a player.
        /// You can use this method to add custom properties to access data.
        /// In most cases, this method will probably be empty
        /// </summary>
        /// <param name="request"></param>
        /// <param name="accessData"></param>
        void FillAccessProperties(GameAccessRequestPacket request, GameAccessPacket accessData);
    }
}