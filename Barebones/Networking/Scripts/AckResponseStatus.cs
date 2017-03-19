namespace Barebones.Networking
{
    public static class AckResponseStatus
    {
        public const byte Default = 0,
            Success = 1,
            Timeout = 2,
            Error = 3,
            Unauthorized = 4,
            Invalid = 5,
            Failed = 6,
            NotConnected = 7;
    }
}