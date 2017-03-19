using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine.UI;

/// <summary>
/// This represents UI of the "Awesome Game" in  the main screen
/// </summary>
public class RoomsDemoGameUi : ClientBehaviour
{
    public RoomsDemoShopItem ShopItemPrefab;
    public LayoutGroup ShopItemsGroup;

    private GenericUIList<AwesomeItemTemplate> _itemsList;

    public Text TotalCoins;
    public Text CurrentItemName;
    public Image CurrentItemImage;

    private ObservableProfile _profile;
    private ObservableInt _coins;
    private ObservableString _currentWeapon;

    protected override void OnAwake()
    {
        _itemsList = new GenericUIList<AwesomeItemTemplate>(ShopItemPrefab.gameObject, ShopItemsGroup);

        // Generate ui list
        _itemsList.Generate<RoomsDemoShopItem>(RoomsDemo.ItemTemplates.Values, (item, shopItem) =>
        {
            shopItem.Setup(item);
        });

        // In case we're already logged in, invoke manually
        if (IsLoggedIn)
            OnLoggedIn();

        if (ProfilesModule.Profile != null)
        {
            SubscribeToProfileChanges(ProfilesModule.Profile);
        }
    }

    /// <summary>
    /// Invoked when client logs in
    /// </summary>
    protected override void OnLoggedIn()
    {
        // Load a profile
        if (ProfilesModule.Profile == null)
            ProfilesModule.GetClientProfile(SubscribeToProfileChanges, RoomsDemo.ProfileFactory);
    }

    /// <summary>
    /// Called, when client profile is loaded
    /// </summary>
    /// <param name="profile"></param>
    private void SubscribeToProfileChanges(ObservableProfile profile)
    {
        _profile = profile;

        // Get coins property and subscribe to coin changes
        _coins = profile.GetProperty<ObservableInt>(RoomsDemoProfileKeys.Coins);
        _coins.OnDirty += OnCoinsChange;
        OnCoinsChange(_coins); // Manually update current value

        // Get current weapon property and subscribe to changes
        _currentWeapon = profile.GetProperty<ObservableString>(RoomsDemoProfileKeys.Weapon);
        _currentWeapon.OnDirty += OnWeaponChange;
        OnWeaponChange(_currentWeapon); // Manually update current value
    }

    /// <summary>
    /// Called on client, when coins value in profile changes
    /// </summary>
    /// <param name="property"></param>
    private void OnCoinsChange(IObservableProperty property)
    {
        TotalCoins.text = _coins.Value.ToString();

        UpdateShopItems();
    }

    /// <summary>
    /// Called on client, when weapon property changes in the server
    /// </summary>
    /// <param name="property"></param>
    private void OnWeaponChange(IObservableProperty property)
    {
        var itemName = _currentWeapon.Value;

        var item = RoomsDemo.GetItemTemplate(itemName);

        if (item != null)
        {
            CurrentItemImage.gameObject.SetActive(true);
            CurrentItemImage.sprite = RoomsDemo.GetSprite(item.Sprite);
            CurrentItemName.text = item.Name;
        }
        else
        {
            CurrentItemName.text = "";
            CurrentItemImage.gameObject.SetActive(false);
        }

        UpdateShopItems();
    }

    protected override void OnDestroyEvent()
    {
        base.OnDestroyEvent();

        if (_profile != null)
        {
            // Remove listeners from properties
            _coins.OnDirty -= OnCoinsChange;
            _currentWeapon.OnDirty -= OnWeaponChange;
        }
    }

    /// <summary>
    /// Sends a request to master server to buy an item
    /// </summary>
    /// <param name="item"></param>
    public void BuyItem(AwesomeItemTemplate item)
    {
        var msg = MessageHelper.Create(RoomsDemoOpCodes.BuyItem, item.Name);

        // Show loading window
        var promise = Events.FireWithPromise(BmEvents.Loading, "Buying item...");

        MasterConnection.Peer.SendMessage(msg, (status, response) =>
        {
            // Close loading window
            promise.Finish();

            if (status != AckResponseStatus.Success)
            {
                // Show error, is buying an item failed
                var errorMessage = response.HasData ? response.AsString() : "Couldn't buy an item";
                Events.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(errorMessage));
                return;
            }
        });
    }

    /// <summary>
    /// Redraws the list of items in the shop, and updates their status
    /// (what's owned and etc...)
    /// </summary>
    public void UpdateShopItems()
    {
        var currentItem = _currentWeapon != null ? _currentWeapon.Value : "";
        var currentCoins = _coins != null ? _coins.Value : 0;

        // Generate ui list
        _itemsList.Generate<RoomsDemoShopItem>(RoomsDemo.ItemTemplates.Values, (item, shopItem) =>
        {
            shopItem.Setup(item);
            shopItem.BuyButton.interactable = item.Price <= currentCoins && item.Name != currentItem;
            shopItem.BuyButtonText.text = item.Name == currentItem ? "OWNED" : "BUY";
        });
    }
}
