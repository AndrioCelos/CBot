using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CBot {
    public class Identification {
        public IRC.IRCClient Connection;
        public string Nickname;
        public string AccountName;
        public bool Watched;
        public List<string> Channels;
    }
}
