using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents the example of a basic lobby view
    /// </summary>
    public class LobbyView : LobbyListener
    {
        public LobbyUserView UserPrefab;
        public Text LobbyName;
        public LobbyTeamView TeamPrefab;
        public LayoutGroup TeamsLayoutGroup;
        public LobbyChatView Chat;
        public GameObject LoadingScreen;
        public Text GameStatus;
        public Text PlayerCount;
        public Text LobbyType;

        public LobbyPropControllersView PropControllers;

        public Button PlayButton;
        public Button ReadyButton;
        public Image ReadyButtonTick;

        public Button StartButton;

        protected Dictionary<string, LobbyTeamView> Teams;
        protected GenericPool<LobbyTeamView> TeamsPool;
        
        protected Dictionary<string, LobbyUserView> Users;
        protected GenericPool<LobbyUserView> UsersPool;

        protected bool IsReady;

        /// <summary>
        /// Text, appended to the user who is game master
        /// </summary>
        public string MasterText = "<color=orange> (Master)</color>";

        /// <summary>
        /// Text, appended to user who is the "current user"
        /// </summary>
        public string CurrentPlayerText = "<color=green> (You)</color>";

        /// <summary>
        /// If true, when the game becomes ready, user joins automatically
        /// </summary>
        public bool AutoJoinGameWhenReady = true;
        
        private bool _wasGameRunningWhenOpened;

        protected override void Awake()
        {
            Teams = new Dictionary<string, LobbyTeamView>();
            TeamsPool = new GenericPool<LobbyTeamView>(TeamPrefab);

            Users = new Dictionary<string, LobbyUserView>();
            UsersPool = new GenericPool<LobbyUserView>(UserPrefab);
        }

        protected override void OnEnable()
        {
            if (LoadingScreen != null)
                LoadingScreen.gameObject.SetActive(true);

            try
            {
                LobbiesModule.GetLobbyInfo((info, error) =>
                {
                    if (info == null)
                    {
                        DialogBoxView.ShowError(error);
                        gameObject.SetActive(false);
                        return;
                    }

                    Setup(info);

                    if (LoadingScreen != null)
                        LoadingScreen.gameObject.SetActive(false);
                });
            }
            catch (Exception e)
            {
                gameObject.SetActive(false);
                throw e;
            }
        }

        /// <summary>
        /// Sets up the lobby view from the data given
        /// </summary>
        /// <param name="data"></param>
        protected override void Setup(LobbyDataPacket data)
        {
            base.Setup(data);

            Reset();

            _wasGameRunningWhenOpened = (LobbyState) data.LobbyState == LobbyState.GameInProgress;

            LobbyName.text = data.LobbyName;
            LobbyType.text = data.LobbyType;

            Teams.Clear();
            Users.Clear();

            // Setup teams
            foreach (var team in data.Teams)
            {
                Teams.Add(team.Key, CreateTeamView(team.Key, team.Value));
            }

            // Setup users
            foreach (var player in data.Players)
            {
                Users.Add(player.Key, CreateUserView(player.Value));
            }

            if (data.Players.ContainsKey(CurrentUser))
            {
                IsReady = data.Players[CurrentUser].IsReady;
            }

            if (PropControllers != null)
            {
                PropControllers.Setup(data.Controls);
            }

            OnGameMasterChanged(data.GameMaster, data.GameMaster == CurrentUser);
            OnLobbyStateChange((LobbyState) data.LobbyState);

            UpdateTeamJoinButtons();
            UpdateReadyButton();
            UpdateStartGameButton();
            UpdatePlayerCount();
        }

        /// <summary>
        /// Creates the team view from the data given
        /// </summary>
        /// <param name="teamName"></param>
        /// <param name="teamProperties"></param>
        /// <returns></returns>
        protected virtual LobbyTeamView CreateTeamView(string teamName, 
            LobbyTeamData data)
        {
            var teamView = TeamsPool.GetResource();
            teamView.Setup(teamName, data);
            teamView.gameObject.SetActive(true);
            teamView.transform.SetParent(TeamsLayoutGroup.transform, false);
            teamView.transform.SetAsLastSibling();

            return teamView;
        }

        /// <summary>
        /// Creates user view from the date given
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected virtual LobbyUserView CreateUserView(LobbyMemberData data)
        {
            // Get team
            var team = Teams[data.Team];

            // Get user view
            var user = UsersPool.GetResource();
            user.Reset();
            user.gameObject.SetActive(true);
            user.Setup(data);
            user.IsCurrentPlayer = data.Name == CurrentUser;

            // Add user to team
            user.transform.SetParent(team.UsersLayoutGroup.transform, false);
            user.transform.SetAsLastSibling();

            // Set ready status from data
            if (data.Name == CurrentUser)
                IsReady = data.IsReady;

            user.SetReadyStatusVisibility(AreUserReadyStatesVisible());

            // Generate username text
            user.Username.text = GenerateUsernameText(user);
            
            return user;
        }

        #region Update methods

        /// <summary>
        /// Enables / disables "join team" buttons, according to the state of the lobby
        /// </summary>
        protected virtual void UpdateTeamJoinButtons()
        {
            var currentPlayer = RawData.Players[CurrentUser];

            foreach (var team in Teams)
            {
                // Disable join team button if team swtiching is not allowed
                team.Value.JoinButton.gameObject.SetActive(RawData.EnableTeamSwitching);

                // Disable join team button if we're already in this team
                if (team.Key == currentPlayer.Team)
                {
                    team.Value.JoinButton.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Enables / disables ready button, according to the
        /// state of the lobby
        /// </summary>
        protected virtual void UpdateReadyButton()
        {
            var user = Users[CurrentUser];

            // Hide / show ready button if the ready system is enabled
            ReadyButton.gameObject.SetActive(RawData.EnableReadySystem && State == LobbyState.Preparations);

            ReadyButtonTick.gameObject.SetActive(user.IsReady);
        }


        /// <summary>
        /// Enables / disables "start game" button, according to the
        /// state of the lobby
        /// </summary>
        protected virtual void UpdateStartGameButton()
        {
            var isCurrentPlayerMaster = Users[CurrentUser].IsMaster;
            var isManualStart = RawData.EnableManualStart;

            var canGameBeStarted = State == LobbyState.Preparations;

            // Show / hide the button
            StartButton.gameObject.SetActive(isCurrentPlayerMaster && isManualStart && canGameBeStarted);
        }


        /// <summary>
        /// Enables / disables "play" button, according to the
        /// state of the lobby
        /// </summary>
        protected virtual void UpdatePlayButton()
        {
            PlayButton.gameObject.SetActive(State == LobbyState.GameInProgress);
        }


        /// <summary>
        /// Enables / disables lobby property controls, 
        /// according to the state of the lobby
        /// </summary>
        protected virtual void UpdateControls()
        {
            var controlsView = GetComponentInChildren<LobbyPropControllersView>();

            if (controlsView != null)
            {
                controlsView.SetAllowEditing(IsGameMaster && State == LobbyState.Preparations);
            }
        }

        /// <summary>
        /// Updates the player count text
        /// </summary>
        protected virtual void UpdatePlayerCount()
        {
            PlayerCount.text = string.Format("{0} Players ({1} Max)", Users.Count, RawData.MaxPlayers);
        }

        /// <summary>
        /// Updates player readyness states
        /// </summary>
        protected virtual void UpdatePlayerStates()
        {
            var showReadyState = AreUserReadyStatesVisible();

            foreach (var userView in Users.Values)
            {
                userView.SetReadyStatusVisibility(showReadyState);
            }
        }

        #endregion

        /// <summary>
        /// Resets the lobby view
        /// </summary>
        protected virtual void Reset()
        {
            PlayButton.gameObject.SetActive(false);

            LobbyName.text = "";

            // Cleanup teams
            foreach (var team in Teams)
            {
                TeamsPool.Store(team.Value);
                team.Value.Reset();
            }
            Teams.Clear();

            // Cleanup users
            foreach (var user in Users)
            {
                UsersPool.Store(user.Value);
                user.Value.Reset();
            }
            Users.Clear();

            // Clear the chat 
            if (Chat != null)
            {
                Chat.Clear();
            }
        }

        #region Overrides

        /// <summary>
        /// Invoked, when lobby property is changed in the Master server
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected override void OnLobbyPropertyChange(string key, string value)
        {
            PropControllers.OnPropertyChange(key, value);
        }

        /// <summary>
        /// Invoked, when player property is changed in the master server
        /// </summary>
        /// <param name="username"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        protected override void OnPlayerPropertyChange(string username, string key, string value)
        {
            LobbyUserView user;
            Users.TryGetValue(username, out user);

            if (user == null)
                return;

            if (key == LobbiesModule.TeamPropKey)
            {
                // Player changed teams
                var newTeam = Teams[value];
                user.transform.SetParent(newTeam.transform, false);
            }
        }

        /// <summary>
        /// Invoked, when play changes it's ready'ness status
        /// </summary>
        /// <param name="username"></param>
        /// <param name="isReady"></param>
        protected override void OnPlayerReadyStatusChange(string username, bool isReady)
        {
            LobbyUserView user;
            Users.TryGetValue(username, out user);

            if (user == null)
                return;

            user.SetReady(isReady);

            if (username == CurrentUser)
                UpdateReadyButton();

            UpdateStartGameButton();
        }

        /// <summary>
        /// Invoked, when player switches teams
        /// </summary>
        /// <param name="username"></param>
        /// <param name="teamName"></param>
        protected override void OnPlayerSwitchedTeams(string username, string teamName)
        {
            var user = Users.Values.FirstOrDefault(u => u.RawData.Name == username);

            var team = Teams.Values.FirstOrDefault(t => t.Name == teamName);

            if (team == null || user == null)
            {
                Logs.Error(user);
                return;
            }

            user.RawData.Team = teamName;

            user.transform.SetParent(team.transform, false);

            UpdateTeamJoinButtons();
            UpdateStartGameButton();
        }

        /// <summary>
        /// Invoked, when game master has changed
        /// </summary>
        /// <param name="masterUsername"></param>
        /// <param name="isCurrentUserMaster"></param>
        protected override void OnGameMasterChanged(string masterUsername, bool isCurrentUserMaster)
        {
            foreach (var user in Users.Values)
            {
                user.SetReadyStatusVisibility(RawData.EnableReadySystem);
                user.IsMaster = user.RawData.Name == masterUsername;

                user.Username.text = GenerateUsernameText(user);
            }

            UpdateStartGameButton();
            UpdateControls();
        }

        /// <summary>
        /// Invoked, when lobby state changes
        /// </summary>
        /// <param name="state"></param>
        protected override void OnLobbyStateChange(LobbyState state)
        {
            State = state;

            UpdateStartGameButton();
            UpdateReadyButton();
            UpdatePlayButton();
            UpdateControls();
            UpdatePlayerStates();

            // Emulate clicking a play button
            if (state == LobbyState.GameInProgress
                && AutoJoinGameWhenReady
                && !_wasGameRunningWhenOpened)
            {
                OnPlayClick();
            }
        }

        /// <summary>
        /// Invoked, when new player joins the lobby
        /// </summary>
        /// <param name="data"></param>
        protected override void OnPlayerJoined(LobbyMemberData data)
        {
            Users.Add(data.Name, CreateUserView(data));

            UpdateStartGameButton();
            UpdatePlayerCount();
        }

        /// <summary>
        /// Invoked, when player leaves the lobby
        /// </summary>
        /// <param name="username"></param>
        protected override void OnPlayerLeft(string username)
        {
            LobbyUserView user;
            Users.TryGetValue(username, out user);

            if (user == null)
                return;

            Users.Remove(username);

            UsersPool.Store(user);

            UpdateStartGameButton();
            UpdatePlayerCount();
        }

        /// <summary>
        /// Invoked, when a new chat message is received
        /// </summary>
        /// <param name="message"></param>
        protected override void OnChatMessageReceived(LobbyChatPacket message)
        {
            Chat.OnMessageReceived(message);
        }

        /// <summary>
        /// Invoked, when lobby status text changes
        /// </summary>
        /// <param name="text"></param>
        public override void OnStatusTextChange(string text)
        {
            GameStatus.text = text;
        }

        #endregion


        /// <summary>
        /// Generates text for the username. Also, appends
        /// "master" and "current player" tags
        /// </summary>
        /// <param name="view"></param>
        /// <returns></returns>
        protected virtual string GenerateUsernameText(LobbyUserView view)
        {
            var username = view.RawData.Name;

            if (view.IsCurrentPlayer)
            {
                username += CurrentPlayerText;
            }

            if (view.IsMaster)
            {
                username += MasterText;
            }

            return username;
        }

        /// <summary>
        /// Returns true if user ready states should be visible
        /// </summary>
        /// <returns></returns>
        protected bool AreUserReadyStatesVisible()
        {
            return State == LobbyState.Preparations && RawData.EnableReadySystem;
        }

        #region User Actions

        /// <summary>
        /// Invoked, when client clicks "ready" button
        /// </summary>
        public virtual void OnToggleReady()
        {
            var shouldBeReady = !IsReady;
            LobbiesModule.SetReady(shouldBeReady, error =>
            {
                if (error != null)
                    return;

                IsReady = shouldBeReady;
            });
        }

        /// <summary>
        ///  Invoked, when client cliks "leavel" button
        /// </summary>
        public virtual void OnLeaveClick()
        {
            LobbiesModule.LeaveLobby(error =>
            {
                if (error != null)
                    return;

                gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// Invoked, when client clicks "Start" button
        /// </summary>
        public virtual void OnStartClick()
        {
            LobbiesModule.StartGame(error =>
            {
            });
        }

        /// <summary>
        /// Invoked, when client clicks "Play" button
        /// </summary>
        public virtual void OnPlayClick()
        {
            LobbiesModule.GetLobbyGameAccess((access, error) =>
            {
                if (access == null)
                {
                    DialogBoxView.ShowError(error);
                    return;
                }

                GameConnector.Connect(access);
            });
        }

        #endregion

    }
}