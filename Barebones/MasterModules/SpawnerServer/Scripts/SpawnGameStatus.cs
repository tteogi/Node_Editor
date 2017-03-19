namespace Barebones.MasterServer
{
    public enum SpawnGameStatus
    {
        Aborted = -2,
        Aborting = -1,

        None,
        InQueue,
        StartingProcess,
        WaitingToGetReady,

        Ready,
        Open
    }
}