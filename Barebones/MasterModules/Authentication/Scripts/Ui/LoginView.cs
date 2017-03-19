using UnityEngine;
using UnityEngine.UI;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a basic view for login form
    /// </summary>
    public class LoginView : ClientBehaviour
    {
        public Text ErrorText;
        public Button LoginButton;
        public InputField Password;
        public Toggle Remember;
        public InputField Username;
        public GameObject PasswordResetWindow;

        protected string RememberPrefKey = "bm.auth.remember";
        protected string UsernamePrefKey = "bm.auth.username";

        protected override void OnAwake()
        {
            ErrorText = ErrorText ?? transform.FindChild("Error").GetComponent<Text>();
            LoginButton = LoginButton ?? transform.FindChild("Button").GetComponent<Button>();
            Password = Password ?? transform.FindChild("Password").GetComponent<InputField>();
            Remember = Remember ?? transform.FindChild("Remember").GetComponent<Toggle>();
            Username = Username ?? transform.FindChild("Username").GetComponent<InputField>();

            if (PasswordResetWindow == null)
            {
                var window = FindObjectOfType<PasswordResetView>();
                PasswordResetWindow = window != null ? window.gameObject : null;
            }

            ErrorText.gameObject.SetActive(false);
        }

        // Use this for initialization
        private void Start()
        {
            RestoreRememberedValues();
        }

        private void OnEnable()
        {
            gameObject.transform.localPosition = Vector3.zero;
        }

        protected override void OnLoggedIn()
        {
            base.OnLoggedIn();

            gameObject.SetActive(false);
        }

        /// <summary>
        ///     Tries to restore previously held values
        /// </summary>
        protected virtual void RestoreRememberedValues()
        {
            Username.text = PlayerPrefs.GetString(UsernamePrefKey, Username.text);
            Remember.isOn = PlayerPrefs.GetInt(RememberPrefKey, -1) > 0;
        }

        /// <summary>
        ///     Checks if inputs are valid
        /// </summary>
        /// <returns></returns>
        protected virtual bool ValidateInput()
        {
            var error = "";

            if (Username.text.Length < 3)
                error += "Username is too short \n";

            if (Password.text.Length < 3)
                error += "Password is too short \n";

            if (error.Length > 0)
            {
                // We've got an error
                error = error.Remove(error.Length - 1);
                ShowError(error);
                return false;
            }

            return true;
        }

        protected void ShowError(string message)
        {
            ErrorText.gameObject.SetActive(true);
            ErrorText.text = message;
        }

        /// <summary>
        ///     Called after clicking login button
        /// </summary>
        protected virtual void HandleRemembering()
        {
            if (!Remember.isOn)
            {
                // Remember functionality is off. Delete all values
                PlayerPrefs.DeleteKey(UsernamePrefKey);
                PlayerPrefs.DeleteKey(RememberPrefKey);
                return;
            }

            // Remember is on
            PlayerPrefs.SetString(UsernamePrefKey, Username.text);
            PlayerPrefs.SetInt(RememberPrefKey, 1);
        }

        public virtual void OnLoginClick()
        {
            if (IsLoggedIn)
            {
                Events.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError("You're already logged in"));
                return;
            }

            // Disable error
            ErrorText.gameObject.SetActive(false);

            // Ignore if didn't pass validation
            if (!ValidateInput())
                return;

            HandleRemembering();

            Auth.LogIn(Username.text, Password.text, (isSuccess, error) =>
            {
                if (!isSuccess && (error != null))
                    ShowError(error);
            });
        }

        public virtual void OnPasswordForgotClick()
        {
            PasswordResetWindow.SetActive(true);
        }
    }
}