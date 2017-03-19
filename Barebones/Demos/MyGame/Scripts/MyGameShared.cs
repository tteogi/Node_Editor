using UnityEngine;
using Barebones.MasterServer;

public class MyGameShared : MonoBehaviour {

	void Awake () {
        // Let the module know which factory to use
        ProfilesModule.SetFactory(ProfileFactory);
    }

    public static ObservableProfile ProfileFactory(string username)
    {
        // Create a profile and add coins property to it
        var profile = new ObservableProfile(username);
        profile.AddProperty(new ObservableInt(MyProfileKeys.Coins, 10));
        return profile;
    }
}

public class MyProfileKeys
{
    public const int Coins = 0;
}