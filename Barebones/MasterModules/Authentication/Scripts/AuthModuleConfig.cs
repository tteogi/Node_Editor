using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public class AuthModuleConfig
    {
        public List<string> ForbiddenUsernames = new List<string>();
        public List<string> ForbiddenWordsInUsernames = new List<string>();
        public int UsernameMaxChars = 12;
        public int UsernameMinChars = 3;
    }
}