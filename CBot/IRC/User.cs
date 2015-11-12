using System;
using System.Text;
using System.Threading;

namespace IRC {
    /// <summary>
    /// Specifies a user's gender.
    /// </summary>
    public enum Gender {
        Unspecified,
        Male,
        Female,
        Bot
    }

    /// <summary>
    /// Represents a user on IRC
    /// </summary>
    public class IRCUser {
        /// <summary>The user's nickname</summary>
        public string Nickname { get; protected internal set; }
        /// <summary>The user's ident username</summary>
        public string Ident { get; protected internal set; }
        /// <summary>The user's displayed host</summary>
        public string Host { get; protected internal set; }
        /// <summary>The user's account name.</summary>
        public string Account { get; protected internal set; }

        /// <summary>The user's full name</summary>
        public string FullName { get; protected internal set; }
        /// <summary>The user's gender, if they have it set</summary>
        public Gender Gender { get; set; }
        /// <summary>True if the user is in our watch list</summary>
        public bool Watched { get; protected internal set; }
        /// <summary>True if the user is marked as away</summary>
        public bool Away { get; protected internal set; }
        /// <summary>The user's away message</summary>
        public string AwayReason { get; protected internal set; }
        /// <summary>The time when the user marked themselves away</summary>
        public DateTime AwaySince { get; protected internal set; }
        /// <summary>True if the user is a server oper</summary>
        public bool Oper { get; protected internal set; }

        /// <summary>A list of channels we share with this user</summary>
        public IRCChannelCollection Channels { get; private set; }

        /// <summary>The IRCClient object that this user belongs to.</summary>
        public IRCClient Client { get; internal set; }

        private int id;
        private static int nextId = -1;

        /// <summary>Returns a gender-specific subject pronoun if this user's gender is known, or "They" if not.</summary>
        public string GenderRefThey {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "He";
                    case Gender.Female: return "She";
                    case Gender.Bot: return "It";
                    default: return "They";
                }
            }
        }
        /// <summary>Returns a gender-specific object pronoun if this user's gender is known, or "Them" if not.</summary>
        public string GenderRefThem {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "Him";
                    case Gender.Female: return "Her";
                    case Gender.Bot: return "It";
                    default: return "Them";
                }
            }
        }
        /// <summary>Returns a gender-specific possessive adjective if this user's gender is known, or "Their" if not.</summary>
        public string GenderRefTheir {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "His";
                    case Gender.Female: return "Her";
                    case Gender.Bot: return "Its";
                    default: return "Their";
                }
            }
        }
        
        /// <summary>Returns this user's username and hostname, separated by a '@'.</summary>
        public string UserAndHost {
            get {
                return this.Ident + "@" + this.Host;
            }
        }

        /// <summary>
        /// Creates a new <see cref="IRCUser"/> with the specified identity data.
        /// </summary>
        /// <param name="client">The IRCClient object that this user belongs to.</param>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="username">The user's ident username.</param>
        /// <param name="host">The user's host address.</param>
        public IRCUser(IRCClient client, string nickname, string username, string host, string account, string fullName) {
            this.Client = client;
            this.Nickname = nickname;
            this.Ident = username;
            this.Host = host;
            this.FullName = fullName;
            this.Channels = new IRCChannelCollection();

            this.id = Interlocked.Increment(ref nextId);
        }
        public IRCUser(IRCClient client, string hostmask, string fullName) {
            this.Client = client;
            this.SetMask(hostmask);
            this.FullName = fullName;
            this.Channels = new IRCChannelCollection();

            this.id = Interlocked.Increment(ref nextId);
        }

        internal protected void SetMask(string Hostmask) {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(Hostmask, @"([^!@]*)(?:!([^@]*))?(?:@(.*))?");
            this.Nickname = match.Groups[1].Value;
            this.Ident = match.Groups[2].Success ? match.Groups[2].Value : "*";
            this.Host = match.Groups[3].Success ? match.Groups[3].Value : "*";
        }
        
        /// <summary>
        /// Returns ths hostmask of this User object.
        /// </summary>
        /// <returns>This User's hostmask, in nick!user@host format.</returns>
        public override string ToString() {
            return this.Nickname + "!" + this.Ident + "@" + this.Host;
        }

        /// <summary>
        /// Determines whether two User objects are the same.
        /// </summary>
        /// <param name="user1">The first User object to compare.</param>
        /// <param name="user2">The second User object to compare.</param>
        /// <returns>True if the two user objects have the same hostmask; false otherwise.</returns>
        // TODO: Perhaps we should compare on IRCClient and nickname only, not the full hostmask.
        public static bool operator ==(IRC.IRCUser user1, IRC.IRCUser user2) {
            if ((object) user2 == null) return false;
            return user1.Nickname == user2.Nickname && user1.Ident == user2.Ident && user1.Host == user2.Host;
        }
        /// <summary>
        /// Determines whether two User objects are different.
        /// </summary>
        /// <param name="user1">The first User object to compare.</param>
        /// <param name="user2">The second User object to compare.</param>
        /// <returns>True if the two user objects have different hostmasks; false otherwise.</returns>
        public static bool operator !=(IRC.IRCUser user1, IRC.IRCUser user2) {
            if ((object) user2 == null) return true;
            return user1.Nickname != user2.Nickname || user1.Ident != user2.Ident || user1.Host != user2.Host;
        }

        /// <summary>
        /// Returns an integer value unique to this User instance, which will not change if the user's information changes.
        /// </summary>
        /// <returns>An integer identifying this User instance.</returns>
        /// <remarks>Be careful when associating data with this ID. The User object will be invalidated if your or their client disconnects.</remarks>
        public override int GetHashCode() {
            return this.id;
        }

        /// <summary>
        /// Determines whether a specified object is equal to this User object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True obj is a User object that is equal to this one; false otherwise.</returns>
        public override bool Equals(object obj) {
            return obj is IRCUser && this == (IRCUser) obj;
        }
    }

    public class IRCLocalUser : IRCUser {
        /// <summary>Returns or sets the user's nickname.</summary>
        public new string Nickname {
            get { return base.Nickname; }
            set {
                if (this.Client?.State < IRCClientState.Registering)
                    base.Nickname = value;
                else if (this.Client?.State == IRCClientState.Registering) {
                    this.Client.Send("NICK " + value);
                    base.Nickname = value;
                } else
                    this.Client.Send("NICK " + value);
            }
        }
        /// <summary>Returns or sets the user's ident username.</summary>
        public new string Ident {
            get { return base.Nickname; }
            set {
                if (this.Client?.State >= IRCClientState.Registering) throw new InvalidOperationException("This property cannot be set after the client has registered.");
                else base.Ident = value;
            }
        }
        /// <summary>Returns or sets the user's full name.</summary>
        public new string FullName {
            get { return base.FullName; }
            set {
                if (this.Client?.State >= IRCClientState.Registering) throw new InvalidOperationException("This property cannot be set after the client has registered.");
                else base.FullName = value;
            }
        }

        public IRCLocalUser(string nickname, string ident, string fullName) : base(null, nickname, ident, "*", null, fullName) { }

        protected internal void SetNickname(string nickname) {
            base.Nickname = nickname;
        }
    }
}