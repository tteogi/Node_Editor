using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ZonePortal : NetworkBehaviour
{
    public string Title = "Teleport";
    public string ZoneName = "";

    public GameObject PortalNamePrefab;

    protected GameObject NameObject;

    // Use this for initialization
    void Start ()
    {
        StartCoroutine(DisplayName());

    }
	
	// Update is called once per frame
	void Update () {
	
	}

    public IEnumerator DisplayName()
    {
        if (BmArgs.DestroyObjects)
            yield break;

        // Create a player name
        NameObject = Instantiate(PortalNamePrefab);
        NameObject.GetComponentInChildren<Text>().text = Title ?? "Portal";
        NameObject.transform.SetParent(FindObjectOfType<Canvas>().transform);

        while (true)
        {
            // While we're still "online"
            NameObject.transform.position = RectTransformUtility
                                                .WorldToScreenPoint(Camera.main, transform.position) + Vector2.up * 30;
            yield return 0;
        }
    }

    public void OnTeleportClick()
    {
        
    }

    public void OnTriggerEnter(Collider collider)
    {
        // Ignore if it's not the server who received the event
        if (!isServer) return;

        var player = collider.GetComponent<MiniPlayerController>();

        // Ignore if collider is not a player
        if (player == null) return;

        FindObjectOfType<WorldZoneServer>().TeleportPlayerToAnotherZone(player.Name, ZoneName);
    }
}
