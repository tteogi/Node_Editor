using System.Collections;
using Barebones.Networking;
using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Displays progress of game creation
    /// </summary>
    public class CreateGameProgressView : MonoBehaviour
    {
        public Button AbortButton;

        public float EnableAbortAfterSeconds = 10;
        public float ForceCloseAfterAbortRequestTimeout = 10;

        public string PleaseWaitText = "Please wait...";

        protected GameCreationProcess Request;
        public Image RotatingImage;

        public Text StatusText;

        // Use this for initialization
        private void Start()
        {
        }

        private void Update()
        {
            RotatingImage.transform.Rotate(Vector3.forward, Time.deltaTime*360*2);

            if (Request == null)
                return;

            if (StatusText != null)
                StatusText.text = string.Format("Progress: {0}/{1} ({2})",
                    (int) Request.Status,
                    (int) CreateGameStatus.Open,
                    Request.Status);
        }

        public void OnAbortClick()
        {
            if (Request == null)
            {
                // If there's no  request to abort, just hide the window
                gameObject.SetActive(false);
                return;
            }

            // Start a timer which will close the window
            // after timeout, in case abortion fails
            StartCoroutine(CloseAfterRequest(ForceCloseAfterAbortRequestTimeout, Request.SpawnId));

            // Disable abort button
            AbortButton.interactable = false;

            Request.SendAbort(isHandled =>
            {
                // If request is not handled, enable the button abort button
                AbortButton.interactable = !isHandled;
            });
        }

        public IEnumerator EnableAbortDelayed(float seconds, int spawnId)
        {
            yield return new WaitForSeconds(seconds);

            if ((Request != null) && (Request.SpawnId == spawnId))
                AbortButton.interactable = true;
        }

        public IEnumerator CloseAfterRequest(float seconds, int spawnId)
        {
            yield return new WaitForSeconds(seconds);

            if ((Request != null) && (Request.SpawnId == spawnId))
            {
                gameObject.SetActive(false);

                // Send another abort request just in case
                // (maybe something unstucked?)
                Request.SendAbort();
            }
        }

        protected void OnStatusChange(CreateGameStatus status)
        {
            if (status == CreateGameStatus.Aborted)
            {
                // If game was aborted
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                    DialogBoxData.CreateInfo("Game creation aborted"));

                // Hide the window
                gameObject.SetActive(false);
            }

            if (status == CreateGameStatus.Ready)
            {
                // TODO Get a server id and a password
                var msg = MessageHelper.Create(BmOpCodes.CreatedGameAccessRequest);

                Connections.ClientToMaster.Peer.SendMessage(msg, (responseStatus, response) =>
                {
                    if (responseStatus != AckResponseStatus.Success)
                    {
                        var errorMessage = response.HasData ? response.AsString() : "Unknown error";

                        BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                            DialogBoxData.CreateError("Can't get a pass from the server: " + errorMessage + "." +
                                                         " Please abort"));
                        return;
                    }

                    var accessData = response.DeserializePacket(new GameAccessPacket());

                    GameConnector.Connect(accessData);
                });
            }
        }

        public void Display(GameCreationProcess request)
        {
            if (Request != null)
                Request.OnStatusChange -= OnStatusChange;

            if (request == null)
                return;

            request.OnStatusChange += OnStatusChange;

            Request = request;
            gameObject.SetActive(true);

            // Disable abort, and enable it after some time
            AbortButton.interactable = false;
            StartCoroutine(EnableAbortDelayed(EnableAbortAfterSeconds, request.SpawnId));

            if (StatusText != null)
                StatusText.text = PleaseWaitText;
        }

        private void OnDestroy()
        {
            if (Request != null)
                Request.OnStatusChange -= OnStatusChange;
        }
    }
}