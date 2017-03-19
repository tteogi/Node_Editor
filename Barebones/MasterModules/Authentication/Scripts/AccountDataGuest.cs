using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a guest account
    /// </summary>
    public class AccountDataGuest : IAccountData
    {
        public AccountDataGuest(string username)
        {
            Username = username;
        }

        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string Token { get; set; }

        public bool IsAdmin
        {
            get { return false; }
        }

        public bool IsGuest
        {
            get { return true; }
        }

        public bool IsEmailConfirmed { get; set; }

        public event Action<IAccountData> OnChange;

        public void MarkAsDirty()
        {
            if (OnChange != null)
                OnChange.Invoke(this);
        }
    }
}