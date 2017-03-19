#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MongoDB.Driver;
#endif
using UnityEngine;

namespace Barebones.MasterServer
{
    public class MongoDbFactory : DatabaseAccessorFactory
    {
        [Header("MongoDB related")]
        public string ConnectionString = "mongodb://localhost";
        public string ConnectionStringArgName = "-bmMongo";
        public string DatabaseName = "masterServer";

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
        private MongoClient _client;
#endif
        protected override void Awake()
        {
            base.Awake();

#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

            var arg = BmArgs.ExtractValue(ConnectionStringArgName);
            if (arg != null)
            {
                // If connection string was passed via arguments
                ConnectionString = arg;
            }

            _client = new MongoClient(ConnectionString);

            SetAccessor<IAuthDatabase>(new AuthDbMongo(_client, DatabaseName));
            SetAccessor<IProfilesDatabase>(new ProfilesDbMongo(_client, DatabaseName));
#endif
        }
    }
}

