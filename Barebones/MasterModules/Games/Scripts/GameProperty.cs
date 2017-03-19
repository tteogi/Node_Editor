namespace Barebones.MasterServer
{
    /// <summary>
    ///     Property keys, used when creating "user created" game servers
    /// </summary>
    public static class GameProperty
    {
        public const string MaxPlayers = "maxPlayers";
        public const string GameName = "name";
        public const string IsPrivate = "private";
        public const string Password = "password";

        // Custom properties
        public const string MapName = "map";
        public const string SceneName = "scene";
    }
}