using System;

namespace IRC {
    /// <summary>
    /// Represents a user's presence on a channel.
    /// </summary>
    public class IrcChannelUser {
        /// <summary>The <see cref="IrcClient"/> that this <see cref="IrcChannelUser"/> belongs to.</summary>
        public IrcClient Client { get; }
        /// <summary>The <see cref="IrcChannel"/> that this <see cref="IrcChannelUser"/> belongs to.</summary>
        public IrcChannel Channel { get; }
        /// <summary>The user's nickname.</summary>
        public string Nickname { get; internal set; }
        /// <summary>The user's access level on the channel.</summary>
        public ChannelStatus Status { get; internal set; }
        /// <summary>The time the user last spoke in the channel, or default(DateTime) if they haven't yet spoken.</summary>
        public DateTime LastActive { get; internal set; }

        /// <summary>The time the user joined the channel, if known, or default(DateTime) if not.</summary>
        public DateTime JoinTime { get; internal set; }

        /// <summary>Creates a <see cref="IrcChannelUser"/> object representing the specified user.</summary>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="client">The IRC client that this <see cref="IrcChannelUser"/> belongs to.</param>
        public IrcChannelUser(IrcClient client, IrcChannel channel, string nickname) : this(client, channel, nickname, new ChannelStatus(client)) { }
        /// <summary>Creates a <see cref="IrcChannelUser"/> object representing the specified user with the specified status.</summary>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="client">The IRC client that this <see cref="IrcChannelUser"/> belongs to.</param>
        /// <param name="status">The status that this user has.</param>
        public IrcChannelUser(IrcClient client, IrcChannel channel,string nickname, ChannelStatus status) {
            this.Nickname = nickname;
            this.Client = client;
            this.Status = status;
        }

        /// <summary>Returns the User object that represents this user.</summary>
        /// <exception cref="System.Collections.Generic.KeyNotFoundException">This user is not known on the network.</exception>
        public IrcUser User {
            get {
                IrcUser user;
                this.Client.Users.TryGetValue(this.Nickname, out user);
                return user;
            }
        }

        /// <summary>Returns the user's nickname and status prefixes.</summary>
        public override string ToString() {
            return this.Status.GetPrefixes() + this.Nickname;
        }
    }
}