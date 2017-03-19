using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Represents an implementation of game lobby
    /// </summary>
    public class GameLobby : IGameLobby
    {
        /// <summary>
        /// Members of the lobby
        /// </summary>
        protected Dictionary<string, LobbyMember> Members;
        protected Dictionary<string, string> Properties;
        protected Dictionary<string, LobbyTeam> Teams;

        public event Action<LobbyMember> PlayerAdded; 
        public event Action<LobbyMember> PlayerRemoved;

        private LobbyMember _gameMaster;
        private LobbyState _state;
        private string _statusText = "";

        public event Action<GameLobby> OnDestroy;

        protected LobbiesModule Module;

        protected SpawnTask GameSpawnTask;

        protected List<LobbyPropertyData> Controls;

        protected IRegisteredGameServer GameServer;

        public GameLobby(int lobbyId, IEnumerable<LobbyTeam> teams, LobbiesModule module) 
            : this(lobbyId, teams, module, new LobbyConfig())
        {

        }
        
        public GameLobby(int lobbyId, IEnumerable<LobbyTeam> teams, 
            LobbiesModule module, LobbyConfig config)
        {
            ServerAddress = "";
            Config = config;
            Id = lobbyId;
            Module = module;

            Controls = new List<LobbyPropertyData>();
            Members = new Dictionary<string, LobbyMember>();
            Properties = new Dictionary<string, string>();

            StatusText = "Preparations";

            ShowInLists = true;

            // Generate a list of teams for the lobby
            Teams = teams.ToDictionary(t => t.Name, t => t);

            MaxPlayers = Teams.Values.Sum(t => t.MaxPlayers);
            MinPlayers = Teams.Values.Sum(t => t.MinPlayers);
        }

        public int MinPlayers { get; protected set; }

        public LobbyConfig Config { get; protected set; }

        public int Id { get; private set; }

        public string Name { get; set; }

        public LobbyState State
        {
            get
            {
                return _state;
            }
            protected set
            {
                if (_state == value)
                    return;

                _state = value;
                OnLobbyStateChange(value);
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            protected set
            {
                if (_statusText == value)
                    return;

                OnStatusTextChange(value);
            }
        }

        public bool ShowInLists { get; set; }
        public string Type { get; set; }

        public bool IsDestroyed { get; private set; }

        public int PlayerCount { get { return Members.Count; } }
        public int MaxPlayers { get; protected set; }

        public string ServerAddress { get; protected set; }

        protected LobbyMember GameMaster
        {
            get { return _gameMaster; }
            set
            {
                if (!Config.EnableGameMasters)
                    return;
                _gameMaster = value;
                OnGameMasterChange();
            }
        }

        public string AddPlayer(ISession session)
        {
            if (session.Username == null)
                return "Unauthorized";

            if (Members.ContainsKey(session.Username))
                return "Already in the lobby";

            if (!IsPlayerAllowed(session))
                return "Not allowed to join the lobby";

            if (session.Peer.GetProperty(BmPropCodes.Lobby) != null)
                return "You're already in a different lobby";

            if (Members.Values.Count >= MaxPlayers)
                return "Lobby is full";

            if (!Config.AllowJoiningWhenGameIsLive && State != LobbyState.Preparations)
                return "Game is already in progress";

            // Create an "instance" of the member
            var member = CreateMember(session);

            // Add it to a team
            var team = PickTeamForPlayer(member);

            if (team == null)
                return "Invalid lobby team";

            if (!team.AddMember(member))
                return "Not allowed to join a team";

            Members.Add(session.Username, member);

            // Save the reference to lobby in peer properties
            session.Peer.SetProperty(BmPropCodes.Lobby, this);

            if (GameMaster == null)
                PickNewGameMaster(false);

            // Listen to disconnect event of the player
            session.OnDisconnect += OnPlayerSessionDisconnected;

            OnPlayerAdded(member);

            if (PlayerAdded != null)
                PlayerAdded.Invoke(member);

            return null;
        }

        public void AddControl(LobbyPropertyData propertyData, string defaultValue)
        {
            SetLobbyProperty(propertyData.PropertyKey, defaultValue);
            Controls.Add(propertyData);
        }

        public void AddControl(LobbyPropertyData propertyData)
        {
            var defaultValue = "";

            if (propertyData.Options != null && propertyData.Options.Count > 0)
            {
                defaultValue = propertyData.Options.First();
            }
            SetLobbyProperty(propertyData.PropertyKey, defaultValue);
            Controls.Add(propertyData);
        }

        public void RemovePlayer(ISession session)
        {
            LobbyMember member;
            Members.TryGetValue(session.Username, out member);

            // If this player was never in the lobby
            if (member == null)
                return;

            Members.Remove(session.Username);

            // Remove member from it's current team
            if (member.Team != null)
                member.Team.RemoveMember(member);

            // Change the game master
            if (GameMaster == member)
                PickNewGameMaster();

            // Remove lobby property
            session.Peer.SetProperty(BmPropCodes.Lobby, null);

            // Remove disconnect listener
            session.OnDisconnect -= OnPlayerSessionDisconnected;

            OnPlayerRemoved(member);
            
            if (PlayerRemoved != null)
                PlayerRemoved.Invoke(member);
        }

        public LobbyMember GetPlayer(string username)
        {
            LobbyMember member;
            Members.TryGetValue(username, out member);
            return member;
        }

        public void SetReadyState(ISession session, bool state)
        {
            LobbyMember member;
            Members.TryGetValue(session.Username, out member);

            if (member == null)
                return;

            member.IsReady = state;

            OnPlayerReadyStatusChange(member);

            if (Members.Values.All(m => m.IsReady))
                OnAllPlayersReady();
        }

        public void OverridePlayerProperty(string username, string propKey, string propValue)
        {
            // Invalid property
            if (propKey == null)
                return;

            LobbyMember member;
            Members.TryGetValue(username, out member);

            if (member == null)
                return;

            member.SetProperty(propKey, propValue);

            OnPlayerPropertyChange(member, propKey);
        }

        public bool SetPlayerProperty(ISession session, string propKey, string propValue)
        {
            // Invalid property
            if (propKey == null)
                return false;

            LobbyMember member;
            Members.TryGetValue(session.Username, out member);

            if (member == null)
                return false;

            // Check if player is allowed to change this property
            if (!CanPlayerSetProperty(member, propKey, propValue))
                return false;

            member.SetProperty(propKey, propValue);

            OnPlayerPropertyChange(member, propKey);

            return true;
        }

        public bool SetLobbyProperty(ISession requester, string propKey, string propValue)
        {
            // Ignore if the one who sets property is not a game master
            if (GameMaster == null || GameMaster.PlayerSession != requester)
                return false;

            SetLobbyProperty(propKey, propValue);
            
            return true;
        }

        public void SetLobbyProperty(string propKey, string propValue)
        {
            if (Properties.ContainsKey(propKey))
            {
                Properties[propKey] = propValue;
            }
            else
            {
                Properties.Add(propKey, propValue);
            }

            OnLobbyPropertyChange(propKey);
        }

        public void SetLobbyProperties(Dictionary<string, string> properties)
        {
            foreach (var property in properties)
            {
                Properties[property.Key] = property.Value;
            }
        }

        /// <summary>
        /// Invoked when one of the members disconnects
        /// </summary>
        /// <param name="session"></param>
        private void OnPlayerSessionDisconnected(ISession session)
        {
            RemovePlayer(session);
        }

        public int GetMembersCount()
        {
            return Members.Count;
        }

        public void Broadcast(IMessage message)
        {
            foreach (var member in Members.Values)
            {
                member.PlayerSession.Peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        public void Broadcast(IMessage message, Func<LobbyMember, bool> condition)
        {
            foreach (var member in Members.Values)
            {
                if (!condition(member))
                    continue;

                member.PlayerSession.Peer.SendMessage(message, DeliveryMethod.Reliable);
            }
        }

        public void BroadcastChatMessage(string message, bool isError = false, 
            string sender = "System")
        {
            var msg = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            Broadcast(MessageHelper.Create(BmOpCodes.LobbyChatMessage, msg.ToBytes()));
        }

        public void SendChatMessage(ISession session, string message, bool isError = false, 
            string sender = "System")
        {
            var packet = new LobbyChatPacket()
            {
                Message = message,
                Sender = sender,
                IsError = isError
            };

            var msg = MessageHelper.Create(BmOpCodes.LobbyChatMessage, packet.ToBytes());

            session.Peer.SendMessage(msg, DeliveryMethod.Reliable);
        }

        public void SetGameSpawnTask(SpawnTask task)
        {
            if (task == null)
                return;

            if (GameSpawnTask == task)
                return;

            if (GameSpawnTask != null)
            {
                // Unsubscribe from previous game
                GameSpawnTask.OnStatusChange -= OnSpawnServerStatusChange;
                GameSpawnTask.OnGameServerRegistered -= OnGameServerRegistered;
            }

            GameSpawnTask = task;

            task.OnStatusChange += OnSpawnServerStatusChange;
            task.OnGameServerRegistered += OnGameServerRegistered;
        }

        private void OnStatusTextChange(string text)
        {
            var msg = MessageHelper.Create(BmOpCodes.LobbyStatusTextChange, text);

            Broadcast(msg);
        }

        public void Destroy()
        {
            if (IsDestroyed)
                return;

            IsDestroyed = true;

            // Remove players
            foreach (var member in Members.Values.ToList())
            {
                RemovePlayer(member.PlayerSession);
            }

            if (GameSpawnTask != null)
            {
                GameSpawnTask.OnStatusChange -= OnSpawnServerStatusChange;
                GameSpawnTask.OnGameServerRegistered -= OnGameServerRegistered;
                GameSpawnTask.Abort("Lobby destroyed");
            }

            OnLobbyDestroyed();

            if (OnDestroy != null)
            {
                OnDestroy.Invoke(this);
            }
        }

        #region Virtual methods

        protected virtual void OnPlayerRemoved(LobbyMember member)
        {
            // Destroy lobby if last member left
            if (Members.Count == 0)
                Destroy();

            if (GameServer != null)
            {
                GameServer.DisconnectPlayer(member.PlayerSession);
            }

            // Notify others about the user who left
            Broadcast(MessageHelper.Create(BmOpCodes.LobbyLeave, member.PlayerSession.Username));
        }

        protected virtual void OnPlayerAdded(LobbyMember member)
        {
            // Notify others about the new user
            var msg = MessageHelper.Create(BmOpCodes.LobbyJoin, member.GeneratePacket().ToBytes());

            // Don't send to the person who just joined
            Broadcast(msg, m => m != member);
        }

        protected virtual void OnPlayerPropertyChange(LobbyMember member, string propertyKey)
        {
            // Broadcast the changes
            var changesPacket = new LobbyMemberPropChangePacket()
            {
                Username = member.PlayerSession.Username,
                Property = propertyKey,
                Value = member.GetProperty(propertyKey)
            };
            Broadcast(MessageHelper.Create(BmOpCodes.LobbyMemberPropertySet, changesPacket.ToBytes()));
        }

        protected virtual void OnPlayerReadyStatusChange(LobbyMember member)
        {
            // Broadcast the new status
            var packet = new StringPairPacket()
            {
                A = member.PlayerSession.Username,
                B = member.IsReady.ToString()
            };
            Broadcast(MessageHelper.Create(BmOpCodes.LobbySetReady, packet.ToBytes()));
        }

        protected virtual void OnPlayerTeamChanged(LobbyMember member, LobbyTeam newTeam)
        {
            var packet = new StringPairPacket()
            {
                A = member.PlayerSession.Username,
                B = newTeam.Name
            };

            // Broadcast the change
            var msg = MessageHelper.Create(BmOpCodes.LobbyJoinTeam, packet.ToBytes());
            Broadcast(msg);
        }

        protected virtual void OnLobbyStateChange(LobbyState state)
        {
            switch (state)
            {
                case LobbyState.FailedToStart:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.Preparations:
                    StatusText = "Failed to start server";
                    break;
                case LobbyState.StartingGameServer:
                    StatusText = "Starting game server";
                    break;
                case LobbyState.GameInProgress:
                    StatusText = "Game in progress";
                    break;
                case LobbyState.GameOver:
                    StatusText = "Game is over";
                    break;
                default:
                    StatusText = "Unknown lobby state";
                    break;
            }

            // Disable ready states
            foreach (var lobbyMember in Members.Values)
                SetReadyState(lobbyMember.PlayerSession, false);

            var msg = MessageHelper.Create(BmOpCodes.LobbyStateChange, (int) state);
            Broadcast(msg);
        }

        protected virtual void OnAllPlayersReady()
        {
            if (!Config.StartGameWhenAllReady)
                return;

            if (Teams.Values.Any(t => t.PlayerCount < t.MinPlayers))
                return;

            StartGame();
        }

        protected virtual void OnLobbyPropertyChange(string propertyKey)
        {
            var packet = new StringPairPacket()
            {
                A = propertyKey,
                B = Properties[propertyKey]
            };

            // Broadcast new properties
            Broadcast(MessageHelper.Create(BmOpCodes.LobbyPropertySet, packet.ToBytes()));
        }

        protected virtual void OnGameMasterChange()
        {
            
        }

        protected virtual void OnSpawnServerStatusChange(SpawnGameStatus status)
        {
            var isStarting = status > SpawnGameStatus.None && status < SpawnGameStatus.Open;

            // If the game is currently starting
            if (isStarting && State != LobbyState.StartingGameServer)
            {
                State = LobbyState.StartingGameServer;
                return;
            }

            // If game is running
            if (status == SpawnGameStatus.Open)
            {
                State = LobbyState.GameInProgress;
            }

            // If game is aborted / closed
            if (status < SpawnGameStatus.None)
            {
                // If game was open before
                if (State == LobbyState.StartingGameServer)
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.FailedToStart;
                    BroadcastChatMessage("Failed to start a game server", true);
                }
                else
                {
                    State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
                }
            }
        }

        protected virtual void OnGameServerRegistered(IRegisteredGameServer server)
        {
            ServerAddress = server.RegistrationPacket.PublicAddress;
            GameServer = server;
            server.Disconnected += OnGameServerDisconnected;
        }

        protected virtual void OnGameServerDisconnected(IRegisteredGameServer gameServer)
        {
            if (GameServer != null)
                GameServer.Disconnected -= OnGameServerDisconnected;

            ServerAddress = "";

            State = Config.PlayAgainEnabled ? LobbyState.Preparations : LobbyState.GameOver;
        }

        public virtual LobbyTeam PickTeamForPlayer(LobbyMember member)
        {
            return Teams.Values
                .Where(t => t.CanAddPlayer(member))
                .OrderBy(t => t.PlayerCount).FirstOrDefault();
        }

        protected virtual void PickNewGameMaster(bool broadcastChange = true)
        {
            if (!Config.EnableGameMasters)
                return;

            GameMaster = Members.Values.FirstOrDefault();

            if (broadcastChange)
            {
                var masterUsername = GameMaster != null ? GameMaster.PlayerSession.Username : "";
                var msg = MessageHelper.Create(BmOpCodes.LobbyMasterChange, masterUsername);
                Broadcast(msg);
            }
        }

        protected virtual bool CanPlayerSetProperty(LobbyMember member, string key, string value)
        {
            return true;
        }

        protected virtual bool IsPlayerAllowed(ISession session)
        {
            return true;
        }

        protected virtual LobbyMember CreateMember(ISession session)
        {
            return new LobbyMember(session);
        }

        protected virtual void OnLobbyDestroyed()
        {
            
        }

        public virtual bool StartGame()
        {
            if (IsDestroyed)
                return false;

            // Make the game server private, so that the only way to access it 
            // is through lobby
            Properties[GameProperty.IsPrivate] = "true";

            var task = Module.SpawnersModule.Spawn(Properties, GenerateCmdArgs());

            if (task == null)
            {
                BroadcastChatMessage("Servers are busy", true);
                return false;
            }

            State = LobbyState.StartingGameServer;

            SetGameSpawnTask(task);

            return true;
        }

        public virtual bool StartGameManually(ISession requester)
        {
            if (!Config.EnableManualStart)
            {
                SendChatMessage(requester, "You cannot start the game manually", true);
                return false;
            }

            // If not game master
            if (GameMaster.PlayerSession != requester)
            {
                SendChatMessage(requester, "You're not the master of this game", true);
                return false;
            }

            if (State != LobbyState.Preparations)
            {
                SendChatMessage(requester, "Invalid lobby state", true);
                return false;
            }

            if (IsDestroyed)
            {
                SendChatMessage(requester, "Invalid lobby state (2)", true);
                return false;
            }

            if (Members.Values.Any(m => !m.IsReady && m != _gameMaster))
            {
                SendChatMessage(requester, "Not all players are ready", true);
                return false;
            }

            if (Members.Count < MinPlayers)
            {
                SendChatMessage(
                    requester, 
                    string.Format("Not enough players. Need {0} more ", (MinPlayers - Members.Count)), 
                    true);
                return false;
            }

            var lackingTeam = Teams.Values.FirstOrDefault(t => t.MinPlayers > t.PlayerCount);

            if (lackingTeam != null)
            {
                var msg = string.Format("Team {0} does not have enough players", lackingTeam.Name);
                SendChatMessage(requester, msg, true);
                return false;
            }

            return StartGame();
        }

        public virtual bool TryJoinTeam(string teamName, ISession session)
        {
            if (!Config.EnableTeamSwitching)
                return false;

            LobbyMember member;
            Members.TryGetValue(session.Username, out member);

            if (member == null)
                return false;

            var currentTeam = member.Team;
            var newTeam = Teams[teamName];

            // Ignore, if any of the teams is invalid
            if (currentTeam == null || newTeam == null)
                return false;

            if (newTeam.PlayerCount >= newTeam.MaxPlayers)
            {
                SendChatMessage(session, "Team is full", true);
                return false;
            }

            // Try to add the member
            if (!newTeam.AddMember(member))
                return false;

            // Remove member from previous team
            currentTeam.RemoveMember(member);

            OnPlayerTeamChanged(member, newTeam);

            return true;
        }

        public virtual LobbyDataPacket GenerateLobbyData(IPeer requester)
        {
            var info = new LobbyDataPacket
            {
                LobbyType = Type ?? "",
                GameMaster = GameMaster != null ? GameMaster.PlayerSession.Username : "",
                LobbyName = Name,
                LobbyId = Id,
                LobbyProperties = Properties,
                Players = Members.Values
                    .ToDictionary(m => m.PlayerSession.Username, m => GenerateMemberData(m)),
                Teams = Teams.Values.ToDictionary(t => t.Name, t => t.GenerateData()),
                Controls = Controls,
                LobbyState = (byte)State,
                MaxPlayers = MaxPlayers,
                EnableTeamSwitching = Config.EnableTeamSwitching,
                EnableReadySystem = Config.EnableReadySystem,
                EnableManualStart = Config.EnableManualStart
            };

            return info;
        }

        public Dictionary<string, string> GetProperties()
        {
            return Properties;
        }

        public virtual Dictionary<string, string> GenerateTeamData(LobbyTeam team)
        {
            return new Dictionary<string, string>();
        }

        public virtual LobbyMemberData GenerateMemberData(LobbyMember member)
        {
            return member.GeneratePacket();
        }

        protected virtual string GenerateCmdArgs()
        {
            return "-bmLobbyId " + Id;
        }

        #endregion

        #region Handlers

        public virtual void HandleGameAccessRequest(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

            if (GameServer == null)
            {
                message.Respond("Game Server is not started yet");   
                return;
            }

            GameServer.RequestPlayerAccess(session, (access, error) =>
            {
                if (access == null)
                {
                    message.Respond(error ?? "Cannot access game server", AckResponseStatus.Failed);
                    return;
                }

                message.Respond(access.ToBytes(), AckResponseStatus.Success);
            });
        }

        public virtual void HandleChatMessage(IIncommingMessage message)
        {
            var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;
            var text = message.AsString();

            var messagePacket = new LobbyChatPacket()
            {
                Message = text,
                Sender = session.Username
            };

            var msg = MessageHelper.Create(BmOpCodes.LobbyChatMessage, messagePacket.ToBytes());

            Broadcast(msg);
        }

        #endregion


    }
}