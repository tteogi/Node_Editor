using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents account data
    /// </summary>
    public interface IAccountData
    {
        string Username { get; set; }
        string Password { get; set; }
        string Email { get; set; }
        string Token { get; set; }
        bool IsAdmin { get; }
        bool IsGuest { get; }
        bool IsEmailConfirmed { get; set; }

        event Action<IAccountData> OnChange;
        void MarkAsDirty();
    }
}