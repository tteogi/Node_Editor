using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Barebones.MasterServer;
using Barebones.Networking;

/// <summary>
/// Controls what's visible in the demo, and acts as 
/// a demo selector.
/// </summary>
public class DemoMainScreen : MonoBehaviour
{
    public RoomsDemoGameUi RoomsDemoGame;
    public WorldDemoMainScene WorldGame;

    public GameObject[] HideAfterDemoSelect;

    private static Type _selectedDemo;

	// Use this for initialization
	void Awake ()
	{
	    RoomsDemoGame = RoomsDemoGame ?? FindObjectOfType<RoomsDemoGameUi>();
        WorldGame = WorldGame ?? FindObjectOfType<WorldDemoMainScene>();
	}

    public void OpenSessionsDemo()
    {
        _selectedDemo = RoomsDemoGame.GetType();
        UpdateView();
    }

    public void OpenMmoDemo()
    {
        _selectedDemo = WorldGame.GetType();
        UpdateView();
    }

    private void UpdateView()
    {
        // Ignore, if demo is not yet selected
        if (_selectedDemo == null) return;

        if (_selectedDemo == RoomsDemoGame.GetType())
        {
            RoomsDemoGame.gameObject.SetActive(true);
            WorldGame.gameObject.SetActive(false);
        }
        else
        {
            WorldGame.gameObject.SetActive(true);
            RoomsDemoGame.gameObject.SetActive(false);
        }

        // Hide objects
        foreach (var obj in HideAfterDemoSelect)
        {
            if (obj != null)
                obj.SetActive(false);
        }
    }

    /// <summary>
    /// Called, when user clisk "Random Game" button
    /// </summary>
    public void OnRandomGameClick()
    {
        var data = new Dictionary<string, string>()
        {
            {GameProperty.SceneName, "GameRoom"},
            {GameProperty.MapName, "Random Map"}
        };

        var msg = MessageHelper.Create(BmOpCodes.FindMatch, data.ToBytes());

        Connections.ClientToMaster.SendMessage(msg, (status, response) =>
        {
            if (status != AckResponseStatus.Success)
            {
                DialogBoxView.ShowError("Failed to join a random game");
                return;
            }

            // If we got here, we should be added to the lobby  
            // so let's display it

            GetComponent<MainScreen>().LobbyView.gameObject.SetActive(true);
        });
    }
}
