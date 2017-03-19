using UnityEngine;
using System.Collections;
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine.UI;

/// <summary>
/// Handles inputs from the email confirmation window
/// </summary>
public class EmailConfirmView : MonoBehaviour
{
    public Button ResendButton;
    public InputField Code;

    // Use this for initialization
    void Awake () {
	    
	}

    public void OnConfirmClick()
    {
        Auth.ConfirmEmail(Code.text, (successful, error) =>
        {
            if (!successful)
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                    DialogBoxData.CreateError("Confirmation failed: " + error));
                return;
            }

            BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                    DialogBoxData.CreateInfo("Email confirmed successfully"));

            // Hide the window
            gameObject.SetActive(false);
        });
    }

    public void OnResendClick()
    {
        ResendButton.interactable = false;

        Auth.RequestEmailConfirmationCode((successful, error) =>
        {
            if (!successful)
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                    DialogBoxData.CreateError("Confirmation code request failed: " + error));

                ResendButton.interactable = true;
                return;
            }

            BmEvents.Channel.Fire(BmEvents.ShowDialogBox,
                    DialogBoxData.CreateInfo("Confirmation code was sent to your e-mail. " +
                                             "It should arrive within few minutes"));
        });
    }
}
