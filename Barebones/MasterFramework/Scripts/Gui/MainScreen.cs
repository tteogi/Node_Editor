using Barebones.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Main screen. Handles what's visible and what's not when user
    ///     logs in or logs out.
    /// </summary>
    public class MainScreen : ClientBehaviour
    {
        [Header("Authenticated")] public GameObject AuthenticatedMenu;
        
        public LoginView LoginWindow;
        public GameObject LogoutButton;
        public GameObject RegisterWindow;
        public GameObject NotConnectedWindow;

        [Header("Unauthenticated")] public GameObject UnauthenticatedMenu;

        public Text Welcome;
        public string WelcomeText = "Hello, <color=\"#17A861FF\">{0}</color>. ";

        [Header("Lobby Related")]
        public LobbyView LobbyView;

        public bool TryDisplayLobby = true;

        protected override void OnAwake()
        {
            UpdateMenu();
            Events.Subscribe(BmEvents.LoginRestoreForm, (o, o1) => RestoreLogin(o as LoginFormData));
            Events.Subscribe(BmEvents.OpenLobby, (o, o1) => OnOpenLobbyRequest());

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (TryDisplayLobby && !BmArgs.IsServer && Connections.ClientToMaster.IsConnected)
            {
                Connections.ClientToMaster.WaitConnection(socket =>
                {
                    ShowLobbyIfInLobby();
                });
            }
        }

        /// <summary>
        /// Checks if player is in lobby, and if he is - displays the lobby window
        /// </summary>
        protected virtual void ShowLobbyIfInLobby()
        {
            LobbiesModule.IsInLobby(isInLobby =>
            {
                if (isInLobby && LobbyView != null)
                {
                    LobbyView.gameObject.SetActive(true);
                }
            });
        }

        protected virtual void OnOpenLobbyRequest()
        {
            LobbyView.gameObject.SetActive(true);
        }

        /// <summary>
        ///     Tries to restore login form from formData
        /// </summary>
        /// <param name="formData"></param>
        protected virtual void RestoreLogin(LoginFormData formData)
        {
            if (formData == null)
                return;

            if (formData.Username != null)
                LoginWindow.Username.text = formData.Username;

            if (formData.Password != null)
                LoginWindow.Password.text = formData.Password;

            LoginWindow.gameObject.SetActive(true);
        }

        protected override void OnLoggedIn()
        {
            base.OnLoggedIn();
            UpdateMenu();
        }

        protected override void OnLoggedOut()
        {
            base.OnLoggedOut();
            UpdateMenu();
        }

        public void UpdateMenu()
        {
            var isLoggedIn = (PlayerData != null) && (PlayerData.Username != null);

            if (isLoggedIn)
                Welcome.text = string.Format(WelcomeText, PlayerData.Username);

            Welcome.gameObject.SetActive(isLoggedIn);

            UnauthenticatedMenu.SetActive(!isLoggedIn && IsConnectedToMaster);
            AuthenticatedMenu.SetActive(isLoggedIn);

            LogoutButton.SetActive(isLoggedIn);

            NotConnectedWindow.gameObject.SetActive(!IsConnectedToMaster);
        }

        protected override void OnConnectedToMaster()
        {
            base.OnConnectedToMaster();

            UpdateMenu();
        }

        public void OnLogoutClick()
        {
            Auth.LogOut();
        }
    }
}