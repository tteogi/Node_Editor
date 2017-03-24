using System.Collections;
using System.Runtime.Remoting.Messaging;
using Barebones.MasterServer;
using UnityEngine;

public class ProfilesTestScript : MonoBehaviour
{

    private string _username;

	// Use this for initialization
	void Start ()
	{
	    StartCoroutine(WaitForMasterToStart());
	}

    private IEnumerator WaitForMasterToStart()
    {
        var master = FindObjectOfType<MasterServerBehaviour>();

        do yield return null;
        while (!master.IsRunning);

        // Set the profiles module
        var profilesModule = master.GetModule<ProfilesModule>();
        profilesModule.ProfileFactory = (username, peer) => new ObservableServerProfile(username)
        {
            new ObservableInt(0, 5),
            new ObservableString(1, "TestingString")
        };

        // Imitate the client
        StartCoroutine(ImitateClient());
    }

    private IEnumerator ImitateClient()
    {
        var connection = Msf.Connection;

        connection.Connect("127.0.0.1" ,5000);

        // Wait until connected to master
        while (!connection.IsConnected)
            yield return null;

        //Msf.Client.Auth.LogInAsGuest((accountInfo, error) =>
        Msf.Client.Auth.LogIn("aaaa", "aaaa", (accountInfo, error) =>
        {
            if (accountInfo == null)
            {
                Logs.Error(error);
                return;
            }

            var profile = new ObservableProfile()
            {
                new ObservableInt(0, 5),
                new ObservableString(1, "TEE")
            };

            _username = accountInfo.Username;

            Msf.Client.Profiles.GetProfileValues(profile, (isSuccessful, profileError) =>
            {
                if (!isSuccessful)
                {
                    Logs.Error(profileError);
                    return;
                }

                profile.PropertyUpdated += (code, property) =>
                {
                    Logs.Info("Property changed:" + code + " - " + property.SerializeToString());
                };

                // Imitate game server
                StartCoroutine(ImitateGameServer());
            }, connection);
        }, connection);
    }

    private IEnumerator ImitateGameServer()
    {
        var connection = Msf.Advanced.ClientSocketFactory();

        connection.Connect("127.0.0.1", 5000);

        // Wait until connected to master
        while (!connection.IsConnected)
            yield return null;

        var profile = new ObservableServerProfile(_username)
        {
            new ObservableInt(0, 5),
            new ObservableString(1, "TEE")
        };

        Msf.Server.Profiles.FillProfileValues(profile, (successful, error) =>
        {
            if (!successful)
            {
                Logs.Error(error);
                return;
            }

            profile.GetProperty<ObservableInt>(0).Add(4);
            profile.GetProperty<ObservableString>(1).Set("Medis");

        }, connection);

    }

    // Update is called once per frame
    void Update () {
		
	}
}
