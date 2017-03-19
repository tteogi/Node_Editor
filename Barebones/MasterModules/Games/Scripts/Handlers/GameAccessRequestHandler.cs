using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Responsible for handling a packet, sent from Client to Master server,
    ///     requesting master server to retrieve an access from Game Server
    ///     Client -> Master Server -> Game Server (ack)-> Master Server (ack) -> Client
    /// </summary>
    public class GameAccessRequestHandler : IPacketHandler
    {
        private readonly GamesModule _module;

        public GameAccessRequestHandler(GamesModule module)
        {
            _module = module;
        }

        public short OpCode
        {
            get { return BmOpCodes.AccessRequest; }
        }

        public void Handle(IIncommingMessage message)
        {
            var data = message.DeserializePacket(new RoomJoinRequestDataPacket());

            var game = _module.GetGameServer(data.RoomId);

            if (game == null)
            {
                message.Respond("Game Server does not exist".ToBytes(), AckResponseStatus.Error);
                return;
            }

            if (!game.IsOpen)
            {
                message.Respond("Game Server is not open yet".ToBytes(), AckResponseStatus.Error);
                return;
            }

            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if ((session == null) || (session.Username == null))
            {
                message.Respond("Unauthorized. Please log in first".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            if (!string.IsNullOrEmpty(game.Password) && (game.Password != data.RoomPassword))
            {
                message.Respond("Invalid Password".ToBytes(), AckResponseStatus.Unauthorized);
                return;
            }

            // Send a request to game server, so that it generates a pass
            game.RequestPlayerAccess(session, (access, error) =>
            {
                if (access == null)
                {
                    // Failure
                    message.Respond((error ?? "Invalid Request").ToBytes(), AckResponseStatus.Failed);
                    return;
                }

                // Success
                message.Respond(MessageHelper.Create(OpCode, access.ToBytes()), AckResponseStatus.Success);
            });
        }
    }
}