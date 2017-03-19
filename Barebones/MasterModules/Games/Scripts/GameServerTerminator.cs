using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This is an example of how you can manage game server termination.
    ///     This script should be added to the first scene
    /// </summary>
    public class GameServerTerminator : MonoBehaviour
    {
        private RegisteredGame _game;

        private IGameServer _server;

        [Header("Terminates server if first player doesn't join")]
        public float FirstPlayerTimeoutSecs = 25;

        [Header("Terminates if server doesn't start in")]
        public float ServerStartTimeoutSecs = 15;

        public float TerminateEmptyOnIntervals = 60;

        public bool TerminateOnConnectionLost = true;

        public bool TerminateWhenLastPlayerQuits = true;

        private void Awake()
        {
            // Ignore if it's not a user created room
            if (!BmArgs.StartSpawned)
            {
                Destroy(gameObject);
                return;
            }

            GamesModule.OnGameServerStarted += OnGameServerStarted;
            GamesModule.OnGameRegistered += OnGameRegistered;

            if (ServerStartTimeoutSecs > 0)
                StartCoroutine(StartStartedTimeout(ServerStartTimeoutSecs));

            if (FirstPlayerTimeoutSecs > 0)
                StartCoroutine(StartFirstPlayerTimeout(FirstPlayerTimeoutSecs));

            if (TerminateEmptyOnIntervals > 0)
                StartCoroutine(StartEmptyIntervalsCheck(TerminateEmptyOnIntervals));

            if (TerminateOnConnectionLost)
                StartCoroutine(StartWaitingForConnectionLost());
        }

        private void OnGameServerStarted(IGameServer server)
        {
            _server = server;
        }

        private void OnGameRegistered(RegisteredGame game)
        {
            _game = game;

            if (TerminateWhenLastPlayerQuits)
                _game.OnPlayerRemoved += OnUserLeft;
        } 


        /// <summary>
        ///     Called every time a user leaves the room.
        /// </summary>
        /// <param name="username"></param>
        private void OnUserLeft(string username)
        {
            if ((_game != null) && !_game.HasConnectedUsers())
            {
                Logs.Error("Terminating game server because last player left");
                Application.Quit();
            }
        }

        /// <summary>
        ///     Each second checks if we're still connected, and if we are not,
        ///     terminates game server
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartWaitingForConnectionLost()
        {
            while (true)
            {
                yield return new WaitForSeconds(1);
                if ((_game != null) && !_game.Connection.IsConnected)
                {
                    Logs.Error("Terminating game server connection is lost");
                    Application.Quit();
                }
            }
        }

        /// <summary>
        ///     Each time, after the amount of seconds provided passes, checks
        ///     if the server is empty, and if it is - terminates application
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        private IEnumerator StartEmptyIntervalsCheck(float timeout)
        {
            while (true)
            {
                yield return new WaitForSeconds(timeout);
                if ((_game != null) && !_game.HasConnectedUsers())
                {
                    Logs.Error("Terminating game server, because it's empty at the time of an interval check.");
                    Application.Quit();
                }
            }
        }

        void OnDestroy()
        {
            GamesModule.OnGameServerStarted -= OnGameServerStarted;
            GamesModule.OnGameRegistered -= OnGameRegistered;
        }

        private IEnumerator StartFirstPlayerTimeout(float timeout)
        {
            yield return new WaitForSeconds(timeout);
            if ((_game != null) && !_game.HasConnectedUsers())
            {
                Logs.Error("Terminated game server because first player didn't show up");
                Application.Quit();
            }
        }

        /// <summary>
        ///     Waits a number of seconds, and checks if the server has started.
        ///     If not - terminates the server
        /// </summary>
        /// <returns></returns>
        private IEnumerator StartStartedTimeout(float timeout)
        {
            yield return new WaitForSeconds(timeout);
            if (_server == null)
                Application.Quit();
        }
    }
}