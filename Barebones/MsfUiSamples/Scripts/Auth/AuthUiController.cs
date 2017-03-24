using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Containts references to auth ui components, and methods to display them.
    /// </summary>
    public class AuthUiController : MonoBehaviour
    {
        public QuickAuthUi QuickAuthUi;
        public LoginUi LoginWindow;
        public RegisterUi RegisterWindow;
        public PasswordResetUi PasswordResetWindow;
        public EmailConfirmUi EmailConfirmationWindow;

        public static AuthUiController Instance;

        protected virtual void Awake()
        {
            Instance = this;
            QuickAuthUi = QuickAuthUi ?? FindObjectOfType<QuickAuthUi>();
            LoginWindow = LoginWindow ?? FindObjectOfType<LoginUi>();
            RegisterWindow = RegisterWindow ?? FindObjectOfType<RegisterUi>();
            PasswordResetWindow = PasswordResetWindow ?? FindObjectOfType<PasswordResetUi>();
            EmailConfirmationWindow = EmailConfirmationWindow ?? FindObjectOfType<EmailConfirmUi>();
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Displays login window
        /// </summary>
        public virtual void ShowLoginWindow()
        {
            LoginWindow.gameObject.SetActive(true);
        }

        /// <summary>
        /// Displays registration window
        /// </summary>
        public virtual void ShowRegisterWindow()
        {
            RegisterWindow.gameObject.SetActive(true);
        }

        /// <summary>
        /// Displays password reset window
        /// </summary>
        public virtual void ShowPasswordResetWindow()
        {
            PasswordResetWindow.gameObject.SetActive(true);
        }

        /// <summary>
        /// Displays e-mail confirmation window
        /// </summary>
        public virtual void ShowEmailConfirmationWindow()
        {
            EmailConfirmationWindow.gameObject.SetActive(true);
        }
    }
}