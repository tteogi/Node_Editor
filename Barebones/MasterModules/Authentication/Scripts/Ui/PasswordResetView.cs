using UnityEngine;
using System.Collections;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    public class PasswordResetView : MonoBehaviour
    {
        public InputField Email;
        public InputField ResetCode;
        public InputField Password;
        public InputField PasswordRepeat;

        // Use this for initialization
        void Start()
        {

        }

        private void OnEnable()
        {
            gameObject.transform.localPosition = Vector3.zero;
        }

        public void OnSendCodeClick()
        {
            var email = Email.text.ToLower().Trim();

            if (email.Length < 3 || !email.Contains("@"))
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("Invalid e-mail address provided"));
                return;
            }

            var promise = BmEvents.Channel.FireWithPromise(BmEvents.Loading, "Requesting reset code");

            Auth.RequestPasswordReset(email, (successful, error) =>
            {
                promise.Finish();

                if (!successful)
                {
                    BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(error));
                    return;
                }

                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateInfo(
                    "Reset code has been sent to the provided e-mail address."));
            });
        }

        public void OnResetClick()
        {
            var email = Email.text.Trim().ToLower();
            var code = ResetCode.text;
            var newPassword = Password.text;

            if (Password.text != PasswordRepeat.text)
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("Passwords do not match"));
                return;
            }

            if (newPassword.Length < 3)
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("Password is too short"));
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("Invalid code"));
                return;
            }

            if (email.Length < 3 || !email.Contains("@"))
            {
                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("Invalid e-mail address provided"));
                return;
            }

            var data = new Auth.PasswordChangeData()
            {
                Email = email,
                Code = code,
                NewPassword = newPassword
            };

            var promise = BmEvents.Channel.FireWithPromise(BmEvents.Loading, "Changing a password");

            Auth.ChangePassword(data, (successful, error) =>
            {
                promise.Finish();

                if (!successful)
                {
                    BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(error));
                    return;
                }

                BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateInfo(
                    "Password changed successfully"));

                BmEvents.Channel.Fire(BmEvents.LoginRestoreForm, new LoginFormData
                {
                    Username = null,
                    Password = ""
                });

                gameObject.SetActive(false);
            });
        }
    }
}
