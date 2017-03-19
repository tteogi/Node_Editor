using System;
using System.Collections;
using System.Collections.Generic;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// This is an example of main game component in master server. 
    /// It should contain server-side code which has to do with your game,
    /// for example handling item purchase requests and etc.
    /// You can build your game on top of it, or just use it as an example 
    /// on how to setup your own game.
    /// </summary>
    public class RoomsDemoModule : MasterModule
    {
        private IMaster _master;

        void Awake()
        {
            // Make sure we always have just one instance in the server
            if (DestroyIfExists()) Destroy(gameObject);
        }

        /// <summary>
        /// Called by master server when module should be started
        /// </summary>
        /// <param name="master"></param>
        public override void Initialize(IMaster master)
        {
            _master = master;
            _master.SetClientHandler(new PacketHandler(RoomsDemoOpCodes.BuyItem, HandleBuyItem));
        }

        /// <summary>
        /// Handles clients request to buy an item
        /// </summary>
        /// <param name="message"></param>
        private void HandleBuyItem(IIncommingMessage message)
        {
            // Get a template of an item we want to buy
            var itemToBuy = RoomsDemo.GetItemTemplate(message.AsString());

            if (itemToBuy == null)
            {
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            // Get a profile of whoever send the request
            var profile = message.Peer.GetProperty(BmPropCodes.Profile) as ObservableProfile;

            if (profile == null)
            {
                message.Respond(AckResponseStatus.Failed);
                return;
            }

            // Get coins property of the profile
            var coins = profile.GetProperty<ObservableInt>(RoomsDemoProfileKeys.Coins);

            // Try to take some of the coins
            if (coins.TryTake(itemToBuy.Price))
            {
                // Enough coins

                // Change the weapon property of the profile
                profile.GetProperty<ObservableString>(RoomsDemoProfileKeys.Weapon).Set(itemToBuy.Name);

                message.Respond(AckResponseStatus.Success);
            }
            else
            {
                // Not enough coins
                message.Respond("Not enough coins. Get a job!", AckResponseStatus.Failed);
                return;
            }
        }
    }

    /// <summary>
    /// Very simple item template implementation
    /// </summary>
    public class AwesomeItemTemplate
    {
        public string Name { get; set; }
        public int Price { get; set; }
        public string Sprite { get; set; }

        public AwesomeItemTemplate(string name, int price, string sprite)
        {
            Name = name;
            Price = price;
            Sprite = sprite;
        }
    }
}