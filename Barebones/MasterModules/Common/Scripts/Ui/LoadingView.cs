using System.Collections.Generic;
using Barebones.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a loading window
    /// </summary>
    public class LoadingView : MonoBehaviour
    {
        private EventsChannel _events;

        private GenericPool<LoadingViewItem> _pool;

        private Dictionary<int, LoadingViewItem> _visibleLoadingItems;
        public string DefaultLoadingMessage = "Loading...";

        public List<string> EventsToListen = new List<string> {BmEvents.Loading};

        public LayoutGroup ItemsGroup;
        public LoadingViewItem LoadingItemPrefab;

        public Image RotatingImage;

        private void Awake()
        {
            _visibleLoadingItems = new Dictionary<int, LoadingViewItem>();
            _pool = new GenericPool<LoadingViewItem>(LoadingItemPrefab);
            _events = BmEvents.Channel;

            // Register handler to all of the events
            foreach (var e in EventsToListen)
                _events.Subscribe(e, OnLoadingEvent);

            gameObject.SetActive(false);
        }

        private void Update()
        {
            RotatingImage.transform.Rotate(Vector3.forward, Time.deltaTime*360*2);
        }

        private void OnEnable()
        {
            gameObject.transform.SetAsLastSibling();
        }

        private void OnLoadingEvent(object arg1, object arg2)
        {
            HandleEvent(arg1 as EventsChannel.Promise, arg2 as string);
        }

        protected virtual void HandleEvent(EventsChannel.Promise promise, string message)
        {
            // If this is the first item to get to the list
            if (_visibleLoadingItems.Count == 0)
                gameObject.SetActive(true);

            OnLoadingStarted(promise, message ?? DefaultLoadingMessage);
            promise.Subscribe(OnLoadingFinished);
        }

        protected virtual void OnLoadingStarted(EventsChannel.Promise promise, string message)
        {
            // Create an item
            var newItem = _pool.GetResource();
            newItem.Id = promise.EventId;
            newItem.Message.text = message;

            // Move item to the list
            newItem.transform.SetParent(ItemsGroup.transform, false);
            newItem.transform.SetAsLastSibling();
            newItem.gameObject.SetActive(true);

            // Store the item
            _visibleLoadingItems.Add(newItem.Id, newItem);
        }

        protected virtual void OnLoadingFinished(EventsChannel.Promise promise)
        {
            LoadingViewItem item;
            _visibleLoadingItems.TryGetValue(promise.EventId, out item);

            if (item == null)
                return;

            // Remove the item
            _visibleLoadingItems.Remove(promise.EventId);
            _pool.Store(item);

            // if everything is done loading, we can disable the loading view
            if (_visibleLoadingItems.Count == 0)
                gameObject.SetActive(false);
        }
    }
}