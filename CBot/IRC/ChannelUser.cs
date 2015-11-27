using System;

namespace IRC {
    /// <summary>
    /// Represents a user's presence on a channel.
    /// </summary>
    public class IRCChannelUser {
        /// <summary>The user's nickname.</summary>
        public string Nickname { get; internal set; }
        /// <summary>The user's access level on the channel.</summary>
        public ChannelStatus Status { get; internal set; }
        /// <summary>The time the user last spoke in the channel, or default(DateTime) if they haven't yet spoken.</summary>
        public DateTime LastActive { get; internal set; }
        /// <summary>The IRC connection that this ChannelUser belongs to.</summary>
        public IRCClient Client { get; internal set; }

        /// <summary>Creates a ChannelUser object representing the specified user.</summary>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="client">The IRC connection that this ChannelUser belongs to.</param>
        public IRCChannelUser(IRCClient client, string nickname) : this(client, nickname, new ChannelStatus(client)) { }
        /// <summary>Creates a ChannelUser object representing the specified user with the specified status.</summary>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="client">The IRC connection that this ChannelUser belongs to.</param>
        /// <param name="status">The status that this user has.</param>
        public IRCChannelUser(IRCClient client, string nickname, ChannelStatus status) {
            this.Nickname = nickname;
            this.Client = client;
            this.Status = status;
            this.LastActive = default(DateTime);
        }

        /// <summary>Returns the User object that represents this user.</summary>
        public IRCUser User {
            get {
                return this.Client.Users[this.Nickname];
            }
        }

        /// <summary>Returns the user's nickname and status prefixes.</summary>
        public override string ToString() {
            return this.Status.GetPrefixes() + this.Nickname;
        }
    }
}