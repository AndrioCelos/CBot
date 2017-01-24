using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IRC {
    /// <summary>
    /// Represents a read-only list of users on a channel.
    /// </summary>
    /// <seealso cref="IrcChannelUser"/>
    public class IrcChannelUserCollection : ICollection<IrcChannelUser>, IReadOnlyCollection<IrcChannelUser> {
        /// <summary>Returns the <see cref="IrcClient"/> that this list belongs to.</summary>
        protected IrcClient Client { get; }
        /// <summary>Returns the underlying dictionary of this <see cref="IrcChannelUserCollection"/>.</summary>
        protected Dictionary<string, IrcChannelUser> Users { get; }

        /// <summary>Initializes a new <see cref="IrcChannelUserCollection"/> belonging to the specified <see cref="IrcClient"/>.</summary>
        protected internal IrcChannelUserCollection(IrcClient client) {
            this.Client = client;
            this.Users = new Dictionary<string, IrcChannelUser>(client.CaseMappingComparer);
        }

        /// <summary>Returns the number of users in this list.</summary>
        public int Count => this.Users.Count;
        /// <summary>Returns the number of users in this list who have the specified status or higher.</summary>
        public int StatusCount(ChannelStatus status) => this.Users.Values.Count(user => user.Status >= status);

        /// <summary>Returns the <see cref="IrcChannelUser"/> with the specified nickname.</summary>
        public IrcChannelUser this[string nickname] {
            get { return this.Users[nickname]; }
            internal set { this.Users[nickname] = value; }
        }

        public IEnumerable<IrcChannelUser> Matching(string hostmask)
            => this.Users.Values.Where(user => Hostmask.Matches(user.User.ToString(), hostmask));

        internal void Add(IrcChannelUser user) => this.Users.Add(user.Nickname, user);

        internal bool Remove(string nickname) => this.Users.Remove(nickname);
        internal bool Remove(IrcChannelUser user) => this.Users.Remove(user.Nickname);
        internal void Clear() => this.Users.Clear();

        /// <summary>Determines whether a user with the specified nickname is in this list.</summary>
        public bool Contains(string nickname) => this.Users.ContainsKey(nickname);

        /// <summary>Attempts to get the user with the specified nickname and returns a value indicating whether they were found.</summary>
        /// <param name="nickname">The nickname to search for.</param>
        /// <param name="value">When this method returns, contains the <see cref="IrcChannelUser"/> searched for, or null if no such user is in the list.</param>
        public bool TryGetValue(string nickname, out IrcChannelUser value) => this.Users.TryGetValue(nickname, out value);

        /// <summary>Returns an enumerator that enumerates the <see cref="IrcChannelUser"/>s in this list. The order is undefined.</summary>
        public IEnumerator<IrcChannelUser> GetEnumerator() => this.Users.Values.GetEnumerator();

        /// <summary>Copies all of the <see cref="IrcChannelUser"/>s in this list to the specified array, starting at the specified index in the target array.</summary>
        public void CopyTo(IrcChannelUser[] array, int startIndex) => this.Users.Values.CopyTo(array, startIndex);

        #region ICollection support
        bool ICollection<IrcChannelUser>.IsReadOnly => true;
        void ICollection<IrcChannelUser>.Add(IrcChannelUser item) { throw new NotSupportedException("IrcChannelUserCollection is read-only."); }
        void ICollection<IrcChannelUser>.Clear() { throw new NotSupportedException("IrcChannelUserCollection is read-only."); }
        bool ICollection<IrcChannelUser>.Contains(IrcChannelUser item) => this.Users.ContainsValue(item);
        bool ICollection<IrcChannelUser>.Remove(IrcChannelUser item) { throw new NotSupportedException("IrcChannelUserCollection is read-only."); }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        #endregion
    }
}