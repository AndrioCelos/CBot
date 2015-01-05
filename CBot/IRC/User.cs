using System;
using System.Text;

namespace IRC {
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
        public ChannelCollection Channels { get; private set; }

        public IRCClient Client { get; internal set; }

        public string GenderRefThey {
            get {
                switch (this.Gender) {
                    case IRC.Gender.Male: return "He";
                    case IRC.Gender.Female: return "She";
                    case IRC.Gender.Bot: return "It";
                    default: return "They";
                }
            }
        }
        public string GenderRefThem {
            get {
                switch (this.Gender) {
                    case IRC.Gender.Male: return "Him";
                    case IRC.Gender.Female: return "Her";
                    case IRC.Gender.Bot: return "It";
                    default: return "Them";
                }
            }
        }
        public string GenderRefTheir {
            get {
                switch (this.Gender) {
                    case IRC.Gender.Male: return "His";
                    case IRC.Gender.Female: return "Her";
                    case IRC.Gender.Bot: return "Its";
                    default: return "Their";
                }
            }
        }

        public string UserAndHost {
            get {
                return this.Username + "@" + this.Host;
            }
        }
        public User(IRCClient client, string Nickname, string Username, string Host) {
            this.Client = client;
            this.Nickname = Nickname;
            this.Username = Username;
            this.Host = Host;
            this.Channels = new ChannelCollection();
        }
        public User(string Hostmask) {
            this.SetMask(Hostmask);
            this.Channels = new ChannelCollection();
        }
        internal protected void SetMask(string Hostmask) {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(Hostmask, @"([^!@]*)(?:!([^@]*))?(?:@(.*))?");
            this.Nickname = match.Groups[1].Value;
            this.Username = match.Groups[2].Success ? match.Groups[2].Value : "*";
            this.Host = match.Groups[3].Success ? match.Groups[3].Value : "*";
        }
        public static implicit operator string(IRC.User User) {
            return User.ToString();
        }
        public static explicit operator IRC.User(string User) {
            return new IRC.User(User);
        }
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

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        public override bool Equals(object obj) {
            return this == (User) obj;
        }
    }
}