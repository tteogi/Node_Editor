using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Main scene controller
/// </summary>
public class WorldDemoMainScene : MonoBehaviour
{
    public string LoadingSceneName = "WorldLoading";

	// Use this for initialization
	void Start () {
	
	}

    /// <summary>
    /// Sends a request to enter the world
    /// </summary>
    public void OnEnterWorldClick()
    {
        var msg = MessageHelper.Create(WorldDemoOpCodes.EnterWorldRequest);
        Connections.ClientToMaster.SendMessage(msg, (status, response) =>
        {
            if (status != AckResponseStatus.Success)
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, 
                    DialogBoxData.CreateError("Failed to join the world: " + response.AsString()));
                return;
            }

            // Go to loading scene
            SceneManager.LoadScene(LoadingSceneName);
        });
    }
}
