using System;
using System.Collections;
using System.Collections.Generic;

namespace IRC {
    /// <summary>
    /// Represents a read-only list of users on IRC.
    /// </summary>
    /// <seealso cref="IrcUser"/>
    public class IrcUserCollection : ICollection<IrcUser>, IReadOnlyCollection<IrcUser> {
        /// <summary>Returns the <see cref="IrcClient"/> that this list belongs to.</summary>
        protected IrcClient Client { get; }
        /// <summary>Returns the underlying dictionary of this <see cref="IrcUserCollection"/>.</summary>
        protected Dictionary<string, IrcUser> Users { get; }

        /// <summary>Initializes a new <see cref="IrcUserCollection"/> belonging to the specified <see cref="IrcClient"/>.</summary>
        protected internal IrcUserCollection(IrcClient client) {
            this.Client = client;
            this.Users = new Dictionary<string, IrcUser>(client.CaseMappingComparer);
        }

        /// <summary>Returns the number of users in this list.</summary>
        public int Count => this.Users.Count;

        /// <summary>Returns the <see cref="IrcUser"/> with the specified nickname.</summary>
        public IrcUser this[string nickname] => this.Users[nickname];

        internal void Add(IrcUser user) => this.Users.Add(user.Nickname, user);

        internal bool Remove(string nickname) => this.Users.Remove(nickname);
        internal bool Remove(IrcUser user) => this.Users.Remove(user.Nickname);
        internal void Clear() => this.Users.Clear();

		/// <summary>Returns the <see cref="IrcUser"/> object representing the channel with the specified name, creating one if necessary.</summary>
		internal IrcUser Get(string mask, bool add) => this.Get(mask, null, null, add);
		/// <summary>
		/// Returns the <see cref="IrcUser"/> object representing the channel with the specified name, creating one if necessary,
		/// and sets its details to the specified ones (from extended-join).
		/// </summary>
		internal IrcUser Get(string mask, string account, string fullName, bool add) {
            IrcUser user;
            string nickname; string ident; string host;

            nickname = Hostmask.GetNickname(mask);
            ident = Hostmask.GetIdent(mask);
            host = Hostmask.GetHost(mask);

            if (this.TryGetValue(nickname, out user)) {
                if (ident != "*") user.Ident = ident;
                if (host != "*") user.Host = host;
                if (account != null) user.Account = (account == "*" ? null : account);
                if (fullName != null) user.FullName = fullName;
            } else {
                user = new IrcUser(this.Client, nickname, ident, host, (account == "*" ? null : account), fullName);
                if (add) this.Add(user);
            }

            return user;
        }

        /// <summary>Determines whether a user with the specified nickname is in this list.</summary>
        public bool Contains(string nickname) => this.Users.ContainsKey(nickname);

        /// <summary>Attempts to get the user with the specified nickname and returns a value indicating whether they were found.</summary>
        /// <param name="nickname">The nickname to search for.</param>
        /// <param name="value">When this method returns, contains the <see cref="IrcUser"/> searched for, or null if no such user is in the list.</param>
        public bool TryGetValue(string nickname, out IrcUser value) => this.Users.TryGetValue(nickname, out value);

        /// <summary>Returns an enumerator that enumerates the <see cref="IrcUser"/>s in this list. The order is undefined.</summary>
        public IEnumerator<IrcUser> GetEnumerator() => this.Users.Values.GetEnumerator();

        /// <summary>Copies all of the <see cref="IrcUser"/>s in this list to the specified array, starting at the specified index in the target array.</summary>
        public void CopyTo(IrcUser[] array, int startIndex) => this.Users.Values.CopyTo(array, startIndex);

        #region ICollection support
        bool ICollection<IrcUser>.IsReadOnly => true;
        void ICollection<IrcUser>.Add(IrcUser item) { throw new NotSupportedException("IrcUserCollection is read-only."); }
        void ICollection<IrcUser>.Clear() { throw new NotSupportedException("IrcUserCollection is read-only."); }
        bool ICollection<IrcUser>.Contains(IrcUser item) => this.Users.ContainsValue(item);
        bool ICollection<IrcUser>.Remove(IrcUser item) { throw new NotSupportedException("IrcUserCollection is read-only."); }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        #endregion
    }
}

