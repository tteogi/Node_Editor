using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// In this demo, loading screen acts as "transferer" from
/// one game server to another.
/// </summary>
public class WorldDemoLoadingScene : MonoBehaviour
{
    public string MainScene = "DemoMain";

	// Use this for initialization
	void Start () {

	    if (!Connections.ClientToMaster.IsConnected)
	    {
	        // If we're not connected to master, jump back to maion screen
            SceneManager.LoadScene(MainScene);
	        return;
	    }

        // Get access to the zone we are supposed to be in.
	    var msg = MessageHelper.Create(WorldDemoOpCodes.GetCurrentZoneAccess);
        Connections.ClientToMaster.SendMessage(msg, (status, response) =>
        {
            if (status != AckResponseStatus.Success)
            {
                // If we've failed to request a teleport
                Logs.Warn("Teleport request failed. Reason: " + response.AsString() + "." +
                                 "This might be intentional(when quitting a game)");
                SceneManager.LoadScene(MainScene);
                return;
            }

            var access = response.DeserializePacket(new GameAccessPacket());

            GameConnector.Connect(access);
        });
	}
}
