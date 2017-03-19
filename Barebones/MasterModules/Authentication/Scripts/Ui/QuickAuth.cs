using Barebones.MasterServer;
using UnityEngine;

namespace Barebones.MasterServer
{
    
    public class QuickAuth : MonoBehaviour
    {
        public GameObject LoginWindow;
        public GameObject RegisterWindow;

        void Awake()
        {
            LoginWindow = LoginWindow ?? FindObjectOfType<LoginView>().gameObject;
            RegisterWindow = RegisterWindow ?? FindObjectOfType<RegisterView>().gameObject;
        }

        public void OnLoginClick()
        {
            if (!Auth.IsLoggedIn)
                LoginWindow.gameObject.SetActive(true);
        }

        public void OnGuestAccessClick()
        {
            var promise = BmEvents.Channel.FireWithPromise(BmEvents.Loading, "Logging in");
            Auth.LogInAsGuest((successful, error) =>
            {
                promise.Finish();

                if (!successful)
                    BmEvents.Channel.Fire(BmEvents.ShowDialogBox, DialogBoxData.CreateError(error));
            });
        }

        public void OnRegisterClick()
        {
            if (!Auth.IsLoggedIn)
                RegisterWindow.SetActive(true);
        }
    }
}
