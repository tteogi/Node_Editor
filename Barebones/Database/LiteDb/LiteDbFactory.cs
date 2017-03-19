#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;

namespace Barebones.MasterServer
{
    public class LiteDbFactory : DatabaseAccessorFactory
    {
        protected override void Awake()
        {
            base.Awake();
            SetAccessor<IAuthDatabase>(new AuthDbLdb(new LiteDatabase("./auth.db")));
            SetAccessor<IProfilesDatabase>(new ProfilesDatabaseLdb(new LiteDatabase("./profiles.db")));
        }
    }
}

#endif