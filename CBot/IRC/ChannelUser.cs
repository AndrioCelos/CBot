using System;
using System.Text;

namespace IRC {
    public class ChannelUser {
        public string Nickname { get; private set; }
        public IRC.ChannelAccess Access { get; internal set; }
        public DateTime LastActive { get; internal set; }
        public IRCClient client { get; internal set; }

        public ChannelUser(string nickname, IRCClient client) {
            this.Nickname = nickname;
            this.client = client;
            this.LastActive = default(DateTime);
        }

        public User User {
            get {
                return this.client.Users[this.Nickname];
            }
        }

        public override string ToString() {
            return this.Nickname;
        }
    }
}