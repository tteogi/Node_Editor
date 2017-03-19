using System;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a state of creating a game server (a.k.a spawning a game server)
    ///     on the client.
    /// </summary>
    public class GameCreationProcess
    {
        public GameCreationProcess(IClientSocket connection, int spawnId)
        {
            SpawnId = spawnId;
            Status = CreateGameStatus.InQueue;
        }

        public int SpawnId { get; private set; }

        public CreateGameStatus Status { get; private set; }
        public event Action<CreateGameStatus> OnStatusChange;

        public void ChangeStatus(CreateGameStatus status)
        {
            Status = status;
            if (OnStatusChange != null)
                OnStatusChange.Invoke(status);
        }

        /// <summary>
        ///     Sends an abort request
        ///     Callback is invoked with "true", if request is successfully handled
        /// </summary>
        /// <param name="callback"></param>
        public void SendAbort(Action<bool> callback = null)
        {
            var msg = MessageHelper.Create(BmOpCodes.AbortSpawn, SpawnId);
            Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
            {
                if (callback != null)
                    callback.Invoke(status == AckResponseStatus.Success);
            });
        }
    }
}