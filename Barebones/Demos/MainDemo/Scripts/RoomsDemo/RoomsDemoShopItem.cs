using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using UnityEngine.UI;

/// <summary>
/// Represents an item in the shop ui
/// </summary>
public class RoomsDemoShopItem : MonoBehaviour
{
    public Image Sprite;
    public Text Price;

    public Button BuyButton;
    public Text BuyButtonText;

    public RoomsDemoGameUi Ui;
    private AwesomeItemTemplate _item;

    // Use this for initialization
    void Start () {
	    
	}

    public void Setup(AwesomeItemTemplate item)
    {
        _item = item;

        Sprite.sprite = RoomsDemo.GetSprite(item.Sprite);
        Price.text = item.Price.ToString();
    }

    public void OnBuyClick()
    {
        Ui.BuyItem(_item);
    }
}
