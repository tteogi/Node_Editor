using System.Collections.Generic;
using System.ComponentModel;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a list of game servers
    /// </summary>
    public class GamesList : ClientBehaviour
    {
        private GenericUIList<GameInfoPacket> _items;
        public GameObject CreateRoomWindow;

        public Button GameJoinButton;
        public GamesListItem ItemPrefab;
        public LayoutGroup LayoutGroup;

        // Use this for initialization
        protected override void OnAwake()
        {
            _items = new GenericUIList<GameInfoPacket>(ItemPrefab.gameObject, LayoutGroup);
        }

        protected virtual void HandleRoomsShowEvent(object arg1, object arg2)
        {
            gameObject.SetActive(true);
        }

        private void OnEnable()
        {
            if (IsConnectedToMaster)
                RequestRooms();
        }

        protected override void OnConnectedToMaster()
        {
            base.OnConnectedToMaster();

            // Get rooms, if at the time of connecting the lobby is visible
            if (gameObject.activeSelf)
                RequestRooms();
        }

        public void Setup(IEnumerable<GameInfoPacket> data)
        {
            _items.Generate<GamesListItem>(data, (packet, item) => { item.Setup(packet); });
            UpdateGameJoinButton();
        }

        private void UpdateGameJoinButton()
        {
            GameJoinButton.interactable = GetSelectedItem() != null;
        }

        public GamesListItem GetSelectedItem()
        {
            return _items.FindObject<GamesListItem>(item => item.IsSelected);
        }

        public void Select(GamesListItem gamesListItem)
        {
            _items.Iterate<GamesListItem>(item => { item.SetIsSelected(!item.IsSelected && (gamesListItem == item)); });
            UpdateGameJoinButton();
        }

        public void OnRefreshClick()
        {
            RequestRooms();
        }

        public void OnJoinGameClick()
        {
            var selected = GetSelectedItem();

            if (selected == null)
                return;

            if (selected.IsLobby)
            {
                OnJoinLobbyClick(selected.RawData);
                return;
            }

            if (selected.IsPasswordProtected)
            {
                // If room is password protected
                var dialogData = DialogBoxData
                    .CreateTextInput("Room is password protected. Please enter the password to proceed", password =>
                    {
                        GamesModule.GetAccess(new RoomJoinRequestDataPacket
                        {
                            RoomPassword = password,
                            RoomId = selected.GameId
                        }, OnPassReceived);
                    });
                Events.Fire(BmEvents.ShowDialogBox, dialogData);
                return;
            }

            // Room does not require a password
            GamesModule.GetAccess(new RoomJoinRequestDataPacket
            {
                RoomId = selected.GameId
            }, OnPassReceived);

        }

        protected virtual void OnJoinLobbyClick(GameInfoPacket packet)
        {
            var loadingPromise = BmEvents.Channel.FireWithPromise(BmEvents.Loading);

            LobbiesModule.JoinLobby(packet.Id, error =>
            {
                loadingPromise.Finish();

                if (error != null)
                {
                    DialogBoxView.ShowError(error);
                    return;
                }

                if (!BmEvents.Channel.Fire(BmEvents.OpenLobby))
                {
                    Logs.Error("Client joined a lobby, but nothing handled an event to" +
                                   " open lobby view. Event: " + BmEvents.OpenLobby);   
                }
            });
        }

        protected virtual void OnPassReceived(GameAccessPacket packet, string errorMessage)
        {
            if (packet == null)
            {
                Events.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(errorMessage));
                return;
            }

            GameConnector.Connect(packet);
        }

        protected virtual void RequestRooms()
        {
            if (MasterConnection == null)
            {
                Logs.Error("Tried to request rooms, but no connection was set");
                return;
            }

            var loadingPromise = Events.FireWithPromise(BmEvents.Loading, "Retrieving Rooms list...");

            MasterConnection.Peer.SendMessage(MessageHelper.Create(BmOpCodes.GamesListRequest), (status, message) =>
            {
                loadingPromise.Finish();

                if (status != AckResponseStatus.Success)
                {
                    Events.Fire(BmEvents.ShowDialogBox,
                        new DialogBoxData("Couldn't retrieve game rooms list"));
                    return;
                }

                var data = message.DeserializeList(() => new GameInfoPacket());

                Setup(data);
            });
        }

        public void OnCreateGameClick()
        {
            if (CreateRoomWindow == null)
            {
                Logs.Error("You need to set a CreateRoomWindow");
                return;
            }
            CreateRoomWindow.gameObject.SetActive(true);
        }

        public void OnCloseClick()
        {
            gameObject.SetActive(false);
        }
    }
}