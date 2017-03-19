namespace Barebones.MasterServer
{
    /// <summary>
    ///     Status of game spawning/creation request
    /// </summary>
    public enum CreateGameStatus
    {
        Aborted = -2,
        Aborting = -1,
        Unknown,
        InQueue,
        StartingInstance,
        WaitingToGetReady,

        Ready,
        Open
    }
}