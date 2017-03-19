using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Barebones.Networking
{
    public class BTimer : MonoBehaviour
    {
        public static long CurrentTick { get; protected set; }
        public delegate void DoneHandler(bool isSuccessful);

        private static BTimer _instance;

        private List<Action> _pendingActionsOnUpdate;

        /// <summary>
        /// Event, which is invoked every second
        /// </summary>
        public event Action<long> OnTick;

        public event Action ApplicationQuit;

        public static BTimer Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("BTimer");
                    _instance = go.AddComponent<BTimer>();
                }
                return _instance;
            }
        }

        // Use this for initialization
        private void Awake()
        {
            _pendingActionsOnUpdate = new List<Action>();
            _instance = this;
            DontDestroyOnLoad(this);

            StartCoroutine(StartTicker());
        }

        void Update()
        {
            if (_pendingActionsOnUpdate.Count > 0)
            {
                foreach (var actions in _pendingActionsOnUpdate)
                {
                    actions.Invoke();
                }

                _pendingActionsOnUpdate.Clear();
            }
        }

        /// <summary>
        ///     Waits while condition is false
        ///     If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condiction"></param>
        /// <param name="doneCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitUntil(Func<bool> condiction, DoneHandler doneCallback, float timeoutSeconds)
        {
            Instance.StartCoroutine(WaitWhileTrueCoroutine(condiction, doneCallback, timeoutSeconds, true));
        }

        /// <summary>
        ///     Waits while condition is true
        ///     If timed out, callback will be invoked with false
        /// </summary>
        /// <param name="condiction"></param>
        /// <param name="doneCallback"></param>
        /// <param name="timeoutSeconds"></param>
        public static void WaitWhile(Func<bool> condiction, DoneHandler doneCallback, float timeoutSeconds)
        {
            Instance.StartCoroutine(WaitWhileTrueCoroutine(condiction, doneCallback, timeoutSeconds));
        }

        private static IEnumerator WaitWhileTrueCoroutine(Func<bool> condition, DoneHandler callback,
            float timeoutSeconds, bool reverseCondition = false)
        {
            while ((timeoutSeconds > 0) && (condition.Invoke() == !reverseCondition))
            {
                timeoutSeconds -= Time.deltaTime;
                yield return null;
            }

            callback.Invoke(timeoutSeconds > 0);
        }

        ///// <summary>
        ///// Waits a specified time interval and calls a callback, unlimited times,
        ///// until coroutine is stoped manually
        ///// </summary>
        ///// <param name="intervalSecs"></param>
        ///// <param name="callback"></param>
        ///// <returns></returns>
        //public static Coroutine StartTicking(float intervalSecs, Action callback)
        //{
        //    return Instance.StartCoroutine(DoTicking(intervalSecs, callback));
        //}

        //private static IEnumerator DoTicking(float interval, Action callback)
        //{
        //    while (true)
        //    {
        //        yield return new WaitForSecondsRealtime(interval);

        //        try
        //        {
        //            callback.Invoke();
        //        }
        //        catch (Exception e)
        //        {
                    
        //        }
        //    }
        //}

        public static void AfterSeconds(float time, Action callback)
        {
            Instance.StartCoroutine(Instance.StartWaitingSeconds(time, callback));
        }

        public void ExecuteOnUpdate(Action action)
        {
            _pendingActionsOnUpdate.Add(action);
        }

        private IEnumerator StartWaitingSeconds(float time, Action callback)
        {
            yield return new WaitForSeconds(time);
            callback.Invoke();
        }

        private IEnumerator StartTicker()
        {
            CurrentTick = 0;
            while (true)
            {
                yield return new WaitForSeconds(1);
                CurrentTick++;
                try
                {
                    if (OnTick != null)
                        OnTick.Invoke(CurrentTick);
                }
                catch (Exception e)
                {
                    Logs.Error(e);
                }
            }
        }

        void OnDestroy()
        {
        }

        void OnApplicationQuit()
        {
            if (ApplicationQuit != null)
                ApplicationQuit.Invoke();
        }
    }
}