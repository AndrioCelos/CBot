using System;
using System.Text;

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
    public class User {
        /// <summary>The user's nickname</summary>
        public string Nickname { get; protected internal set; }
        /// <summary>The user's ident username</summary>
        public string Username { get; protected internal set; }
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
        public ChannelCollection Channels { get; }

        /// <summary>The IRCClient object that this user belongs to.</summary>
        public IRCClient Client { get; internal set; }

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
                return this.Username + "@" + this.Host;
            }
        }

        /// <summary>
        /// Creates a new <see cref="User"/> with the specified identity data.
        /// </summary>
        /// <param name="client">The IRCClient object that this user belongs to.</param>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="username">The user's ident username.</param>
        /// <param name="host">The user's host address.</param>
        public User(IRCClient client, string nickname, string username, string host) {
            this.Client = client;
            this.Nickname = nickname;
            this.Username = username;
            this.Host = host;
            this.Channels = new ChannelCollection();
        }
        /// <summary>
        /// Creates a new <see cref="User"/> with the specified hostmask.
        /// </summary>
        /// <param name="hostmask">The user's hostmask, in nick!user@host format.</param>
        public User(string hostmask) {
            this.SetMask(hostmask);
            this.Channels = new ChannelCollection();
        }

        internal protected void SetMask(string Hostmask) {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(Hostmask, @"([^!@]*)(?:!([^@]*))?(?:@(.*))?");
            this.Nickname = match.Groups[1].Value;
            this.Username = match.Groups[2].Success ? match.Groups[2].Value : "*";
            this.Host = match.Groups[3].Success ? match.Groups[3].Value : "*";
        }
        
        /// <summary>
        /// Returns the hostmask of the given <see cref="User"/> object.
        /// </summary>
        /// <param name="User">The User object to convert.</param>
        /// <returns>The user's hostmask.</returns>
        public static implicit operator string(User User) {
            return User.ToString();
        }
        
        /// <summary>
        /// Creates a User object from the given string.
        /// </summary>
        /// <param name="User">The string to convert.</param>
        /// <returns>A User object with the specified string as the hostmask.</returns>
        public static explicit operator User(string User) {
            return new User(User);
        }
        
        /// <summary>
        /// Returns ths hostmask of this User object.
        /// </summary>
        /// <returns>This User's hostmask, in nick!user@host format.</returns>
        public override string ToString() {
            return string.Concat(new string[]
				{
					this.Nickname,
					"!",
					this.Username,
					"@",
					this.Host
				});
        }

        /// <summary>
        /// Determines whether two User objects are the same.
        /// </summary>
        /// <param name="user1">The first User object to compare.</param>
        /// <param name="user2">The second User object to compare.</param>
        /// <returns>True if the two user objects have the same hostmask; false otherwise.</returns>
        // TODO: Perhaps we should compare on IRCClient and nickname only, not the full hostmask.
        public static bool operator ==(IRC.User user1, IRC.User user2) {
            if ((object) user2 == null) return false;
            return user1.Nickname == user2.Nickname && user1.Username == user2.Username && user1.Host == user2.Host;
        }
        public static bool operator ==(string user1, IRC.User user2) {
            if ((object) user2 == null) return false;
            return user1 == user2.ToString();
        }
        public static bool operator ==(IRC.User user1, string user2) {
            if ((object) user2 == null) return false;
            return user1.ToString() == user2;
        }
        /// <summary>
        /// Determines whether two User objects are different.
        /// </summary>
        /// <param name="user1">The first User object to compare.</param>
        /// <param name="user2">The second User object to compare.</param>
        /// <returns>True if the two user objects have different hostmasks; false otherwise.</returns>
        public static bool operator !=(IRC.User user1, IRC.User user2) {
            if ((object) user2 == null) return true;
            return user1.Nickname != user2.Nickname || user1.Username != user2.Username || user1.Host != user2.Host;
        }
        public static bool operator !=(string user1, IRC.User user2) {
            if ((object) user2 == null) return true;
            return user1 != user2.ToString();
        }
        public static bool operator !=(IRC.User user1, string user2) {
            if ((object) user2 == null) return true;
            return user1.ToString() != user2;
        }

        /// <summary>
        /// Calculates the hash code of the user's hostmask.
        /// </summary>
        /// <returns>The hash code of the user's hostmask.</returns>
        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        /// <summary>
        /// Determines whether a specified object is equal to this User object.
        /// </summary>
        /// <param name="obj">The object to compare.</param>
        /// <returns>True obj is a User object that is equal to this one; false otherwise.</returns>
        public override bool Equals(object obj) {
            return obj is User && this == (User) obj;
        }
    }
}