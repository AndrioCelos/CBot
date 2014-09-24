using System;
using System.Text.RegularExpressions;

using IRC;

namespace CBot {
    public class CommandEventArgs : EventArgs {
        public IRCClient Connection { get; private set; }
        public string Channel { get; private set; }
        public User Sender { get; private set; }

        public string[] Parameters { get; private set; }

        public bool Cancel { get; set; }

        public CommandEventArgs(IRCClient Connection, string Channel, User Sender, string[] Parameters) {
            this.Connection = Connection;
            this.Channel = Channel;
            this.Sender = Sender;
            this.Parameters = Parameters;
            this.Cancel = true;
        }
    }

    public class RegexEventArgs : EventArgs {
        public IRCClient Connection { get; private set; }
        public string Channel { get; private set; }
        public User Sender { get; private set; }

        public Match Match { get; private set; }

        public bool Cancel { get; set; }

        public RegexEventArgs(IRCClient Connection, string Channel, User Sender, Match Match) {
            this.Connection = Connection;
            this.Channel = Channel;
            this.Sender = Sender;
            this.Match = Match;
            this.Cancel = false;
        }
    }
}