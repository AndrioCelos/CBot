using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

using static IRC.Replies;

namespace IRC {
    /// <summary>
    /// Represents a user on IRC.
    /// </summary>
    public class IrcUser : IrcMessageTarget {
        /// <summary>Returns the <see cref="IrcClient"/> that this user belongs to.</summary>
        public override IrcClient Client { get; }

        /// <summary>Returns the user's nickname.</summary>
        public override string Target => this.Nickname;

        /// <summary>The user's nickname.</summary>
        public string Nickname { get; protected internal set; }
        /// <summary>The user's ident username.</summary>
        public string Ident { get; protected internal set; }
        /// <summary>The user's displayed host.</summary>
        public string Host { get; protected internal set; }
        /// <summary>The user's account name.</summary>
        public string Account { get; set; }

        /// <summary>The user's full name.</summary>
        public string FullName { get; protected internal set; }
        /// <summary>The user's gender, if they have it set.</summary>
        public Gender Gender { get; set; }
        /// <summary>True if the user is in our watch list.</summary>
        public bool Watched { get; protected internal set; }
        /// <summary>True if the user is marked as away.</summary>
        public bool Away { get; protected internal set; }
        /// <summary>The user's away message.</summary>
        public string AwayReason { get; protected internal set; }
        /// <summary>The time when the user marked themselves away.</summary>
        public DateTime AwaySince { get; protected internal set; }
        /// <summary>True if the user is a server oper.</summary>
        public bool Oper { get; protected internal set; }

        /// <summary>Returns true if this user is the local user for its <see cref="IrcClient"/> object.</summary>
        public bool IsMe => (this.Client != null && this == this.Client.Me);
        /// <summary>Returns true if this user is in our watch list or in a common channel with us.</summary>
        public bool IsSeen => (this.Watched || this.Channels.Count != 0);

        /// <summary>A list of channels we share with this user</summary>
        public IrcChannelCollection Channels { get; internal set; }

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
        public string UserAndHost => this.Ident + "@" + this.Host;

        /// <summary>
        /// Creates a new <see cref="IrcUser"/> with the specified identity data.
        /// </summary>
        /// <param name="client">The <see cref="IrcClient"/> that this user belongs to.</param>
        /// <param name="nickname">The user's nickname.</param>
        /// <param name="ident">The user's ident username.</param>
        /// <param name="host">The user's displayed host.</param>
        /// <param name="account">The user's account name, or null if it isn't known.</param>
        /// <param name="fullName">The user's full name, or null if it isn't known.</param>
        public IrcUser(IrcClient client, string nickname, string ident, string host, string account, string fullName) {
            this.Client = client;
            this.Nickname = nickname;
            this.Ident = ident;
            this.Host = host;
            this.Account = account;
            this.FullName = fullName;
            this.Channels = new IrcChannelCollection(client);

            this.id = Interlocked.Increment(ref nextId);
        }
        /// <summary>
        /// Creates a new <see cref="IrcUser"/> with the specified identity data.
        /// </summary>
        /// <param name="client">The <see cref="IrcClient"/> that this user belongs to.</param>
        /// <param name="hostmask">The user's displayed hostmask.</param>
        /// <param name="fullName">The user's full name, or null if it isn't known.</param>
        public IrcUser(IrcClient client, string hostmask, string fullName) {
            this.Client = client;
            this.SetMask(hostmask);
            this.FullName = fullName;
            this.Channels = new IrcChannelCollection(client);

            this.id = Interlocked.Increment(ref nextId);
        }

		/// <summary>Sets the <see cref="Nickname"/>, <see cref="Ident"/> and <see cref="Host"/> properties according to the specified hostmask.</summary>
        protected internal void SetMask(string hostmask) {
            this.Nickname = Hostmask.GetNickname(hostmask);
            this.Ident = Hostmask.GetIdent(hostmask);
            this.Host = Hostmask.GetHost(hostmask);
        }

        /// <summary>
        /// Returns ths hostmask of this <see cref="IrcUser"/>.
        /// </summary>
        /// <returns>This user's hostmask, in nick!user@host format.</returns>
        public override string ToString() => this.Nickname + "!" + this.Ident + "@" + this.Host;

        /// <summary>
        /// Determines whether two <see cref="IrcUser"/> objects are equal.
        /// </summary>
        /// <returns>True if the two user objects have the same hostmask; false otherwise.</returns>
        // TODO: Perhaps we should compare on IrcClient and nickname only, not the full hostmask.
        public static bool operator ==(IrcUser user1, IrcUser user2) {
            if (ReferenceEquals(user1, null)) return ReferenceEquals(user2, null);
            if (ReferenceEquals(user2, null)) return false;
            return user1.Nickname == user2.Nickname && user1.Ident == user2.Ident && user1.Host == user2.Host;
        }
        /// <summary>
        /// Determines whether two User objects are different.
        /// </summary>
        /// <param name="user1">The first User object to compare.</param>
        /// <param name="user2">The second User object to compare.</param>
        /// <returns>True if the two user objects have different hostmasks; false otherwise.</returns>
        public static bool operator !=(IrcUser user1, IrcUser user2) {
            if (ReferenceEquals(user1, null)) return !ReferenceEquals(user2, null);
            if (ReferenceEquals(user2, null)) return true;
            return user1.Nickname != user2.Nickname || user1.Ident != user2.Ident || user1.Host != user2.Host;
        }

        /// <summary>
        /// Returns an integer value unique to this User instance, which will not change if the user's information changes.
        /// </summary>
        /// <returns>An integer identifying this User instance.</returns>
        /// <remarks>Be careful when associating data with this ID. The <see cref="IrcUser"/> object will be invalidated if your or their client disconnects.</remarks>
        public override int GetHashCode() {
            return this.id;
        }

        /// <summary>
        /// Determines whether a specified object is equal to this <see cref="IrcUser"/> object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns>True obj is an <see cref="IrcUser"/> object that is equal to this one; false otherwise.</returns>
        public override bool Equals(object other) {
            return other is IrcUser && this == (IrcUser) other;
        }

		/// <summary>Waits for the next private PRIVMSG from this user.</summary>
		public Task<string> ReadAsync() => this.ReadAsync(this.Client.Me);
		/// <summary>Waits for the next PRIVMSG from this user to the specified target.</summary>
		public Task<string> ReadAsync(IrcMessageTarget target) {
			var asyncRequest = new AsyncRequest.MessageAsyncRequest(this, target, false);
			this.Client.AddAsyncRequest(asyncRequest);
			return (Task<string>)asyncRequest.Task;
		}

		/// <summary>Waits for the next private NOTICE from this user.</summary>
		public Task<string> ReadNoticeAsync() => this.ReadNoticeAsync(this.Client.Me);
		/// <summary>Waits for the next NOTICE from this user to the specified target.</summary>
		public Task<string> ReadNoticeAsync(IrcMessageTarget target) {
			var asyncRequest = new AsyncRequest.MessageAsyncRequest(this, target, true);
			this.Client.AddAsyncRequest(asyncRequest);
			return (Task<string>) asyncRequest.Task;
		}

		/// <summary>Sends a CTCP request to this user and awaits a reply.</summary>
		/// <param name="message">The CTCP request and parameters.</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the status of the request.
		/// The task will return the part of the response after the request token, or null if that part was not present.
		/// </returns>
		public Task<string> CtcpAsync(string message) {
			var fields = message.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
			return this.CtcpAsync(fields[0], fields.Length >= 2 ? fields[1] : null);
		}
		/// <summary>Sends a CTCP request to this user and awaits a reply.</summary>
		/// <param name="request">The CTCP request token.</param>
		/// <param name="arg">The parameter to the CTCP request..</param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the status of the request.
		/// The task will return the part of the response after the request token, or null if that part was not present.
		/// </returns>
		public Task<string> CtcpAsync(string request, string arg) {
			var asyncRequest = new AsyncRequest.CtcpAsyncRequest(this.Client, this.Nickname, request);
			this.Client.AddAsyncRequest(asyncRequest);
			this.Ctcp(request, arg);
			return (Task<string>) asyncRequest.Task;
		}
		/// <summary>Sends a CTCP request to this user and awaits a reply.</summary>
		/// <param name="request"></param>
		/// <param name="args"></param>
		/// <returns>
		/// A <see cref="Task{TResult}"/> representing the status of the request.
		/// The task will return the part of the response after the request token, or null if that part was not present.
		/// </returns>
		public Task<string> CtcpAsync(string request, params string[] args) {
			var asyncRequest = new AsyncRequest.CtcpAsyncRequest(this.Client, this.Nickname, request);
			this.Client.AddAsyncRequest(asyncRequest);
			this.Ctcp(request, args);
			return (Task<string>) asyncRequest.Task;
		}

		/// <summary>Sends a WHOIS request to look up this user and awaits a reply.</summary>
		public Task<WhoisResponse> WhoisAsync() => this.Client.WhoisAsync(this.Nickname);

		/// <summary>Asynchronously looks up the services account name of the specified user.</summary>
		// TODO: WHOX support
		public Task<string> GetAccountAsync() => this.GetAccountAsync(false);
		/// <summary>Asynchronously looks up the services account name of the specified user.</summary>
		/// <param name="force">If true, a request will be sent even if an account name is already known.</param>
		public async Task<string> GetAccountAsync(bool force) {
            if (this.Client.State < IrcClientState.ReceivingServerInfo) throw new InvalidOperationException("The client must be registered to perform this operation.");

            if (!force && this.Account != null) return this.Account;

            var response = await this.WhoisAsync();
            return response.Account;
        }
    }

    /// <summary>
    /// Represents the local user on IRC and provides identity information to log in.
    /// </summary>
    public class IrcLocalUser : IrcUser {
        internal IrcClient client;
        public override IrcClient Client => this.client;

        /// <summary>Returns or sets the user's nickname.</summary>
        public new string Nickname {
            get { return base.Nickname; }
            set {
                if (this.Client?.State < IrcClientState.Registering)
                    base.Nickname = value;
                else
                    this.Client.Send("NICK " + value);
            }
        }
        /// <summary>Returns or sets the user's ident username.</summary>
        /// <exception cref="InvalidOperationException">An attempt was made to set this property after the <see cref="IrcClient"/> has logged in.</exception>
        public new string Ident {
            get { return base.Ident; }
            set {
                if (this.Client?.State >= IrcClientState.Registering) throw new InvalidOperationException("This property cannot be set after the client has registered.");
                else base.Ident = value;
            }
        }
        /// <summary>Returns or sets the user's full name.</summary>
        /// <exception cref="InvalidOperationException">An attempt was made to set this property after the <see cref="IrcClient"/> has logged in.</exception>
        public new string FullName {
            get { return base.FullName; }
            set {
                if (this.Client?.State >= IrcClientState.Registering) throw new InvalidOperationException("This property cannot be set after the client has registered.");
                else base.FullName = value;
            }
        }

		/// <summary>Attempts to change the local user's nickname and awaits a response from the server.</summary>
        public Task SetNicknameAsync(string newNickname) {
            if (newNickname == null) throw new ArgumentNullException(nameof(newNickname));

            if (this.Client?.State < IrcClientState.Registering) {
                base.Nickname = newNickname;
                return Task.FromResult<object>(null);
            }

            var request = new AsyncRequest.VoidAsyncRequest(this.client, this.Nickname, "NICK", null, ERR_NONICKNAMEGIVEN, ERR_ERRONEUSNICKNAME, ERR_NICKNAMEINUSE, ERR_NICKCOLLISION, ERR_UNAVAILRESOURCE, ERR_RESTRICTED);
            this.client.AddAsyncRequest(request);
            this.client.Send("NICK " + newNickname);
            return request.Task;
        }

        /// <summary>Initializes a new <see cref="IrcLocalUser"/> with the specified identity data.</summary>
        public IrcLocalUser(string nickname, string ident, string fullName) : base(null, nickname, ident, "*", null, fullName) { }
    }
}