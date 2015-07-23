using System;
using System.Text;

namespace IRC {
    /// <summary>
    /// Represents a user's presence on a channel.
    /// </summary>
    public class ChannelUser {
        /// <summary>The user's nickname.</summary>
        public string Nickname { get; }
        /// <summary>The user's access level on the channel.</summary>
        public IRC.ChannelAccess Access { get; internal set; }
        /// <summary>The time the user last spoke in the channel, or default(DateTime) if they haven't yet spoken.</summary>
        public DateTime LastActive { get; internal set; }
        /// <summary>The IRC connection that this ChannelUser belongs to.</summary>
        public IRCClient Client { get; internal set; }

        /// <summary>Creates a ChannelUser object representing the specified user.</summary>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="client">The IRC connection that this ChannelUser belongs to.</param>
        public ChannelUser(string nickname, IRCClient client) {
            this.Nickname = nickname;
            this.Client = client;
            this.LastActive = default(DateTime);
        }

        /// <summary>Returns this user's User object.</summary>
        public User User {
            get {
                return this.Client.Users[this.Nickname];
            }
        }

        /// <summary>Returns the user's nickname.</summary>
        public override string ToString() {
            return this.Nickname;
        }
    }
}