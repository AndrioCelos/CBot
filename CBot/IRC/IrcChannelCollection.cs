using System;
using System.Collections;
using System.Collections.Generic;

namespace IRC {
    /// <summary>
    /// Represents a read-only list of IRC channels.
    /// </summary>
    /// <seealso cref="IrcChannel"/>
    public class IrcChannelCollection : ICollection<IrcChannel>, IReadOnlyCollection<IrcChannel> {
        /// <summary>Returns the <see cref="IrcClient"/> that this list belongs to.</summary>
        protected IrcClient Client { get; }
        /// <summary>Returns the underlying dictionary of this <see cref="IrcChannelCollection"/>.</summary>
        protected Dictionary<string, IrcChannel> Channels { get; }

        /// <summary>Initializes a new <see cref="IrcChannelCollection"/> belonging to the specified <see cref="IrcClient"/>.</summary>
        protected internal IrcChannelCollection(IrcClient client) {
            this.Client = client;
            this.Channels = new Dictionary<string, IrcChannel>(client?.CaseMappingComparer ?? IrcStringComparer.RFC1459);
        }

        /// <summary>Returns the number of channels in this list.</summary>
        public int Count => this.Channels.Count;

        /// <summary>Returns the <see cref="IrcChannel"/> with the specified name.</summary>
        public IrcChannel this[string name] => this.Channels[name];

        internal void Add(IrcChannel channel) => this.Channels.Add(channel.Name, channel);

        internal bool Remove(string name) => this.Channels.Remove(name);
        internal bool Remove(IrcChannel channel) => this.Channels.Remove(channel.Name);
        internal void Clear() => this.Channels.Clear();

        /// <summary>Determines whether a channel with the specified name is in this list.</summary>
        public bool Contains(string name) => this.Channels.ContainsKey(name);

        /// <summary>Attempts to get the channel with the specified name and returns a value indicating whether it was found.</summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="value">When this method returns, contains the <see cref="IrcChannel"/> searched for, or null if no such channel is in the list.</param>
        public bool TryGetValue(string name, out IrcChannel value) => this.Channels.TryGetValue(name, out value);

        /// <summary>Returns an enumerator that enumerates the <see cref="IrcChannel"/>s in this list. The order is undefined.</summary>
        public IEnumerator<IrcChannel> GetEnumerator() => this.Channels.Values.GetEnumerator();

        /// <summary>Copies all of the <see cref="IrcChannel"/>s in this list to the specified array, starting at the specified index in the target array.</summary>
        public void CopyTo(IrcChannel[] array, int startIndex) => this.Channels.Values.CopyTo(array, startIndex);

		/// <summary>Returns the <see cref="IrcChannel"/> object representing the channel with the specified name, creating one if necessary.</summary>
        internal IrcChannel Get(string name) {
            IrcChannel channel;
            if (this.TryGetValue(name, out channel)) return channel;
            return new IrcChannel(this.Client, name);
        }

        #region ICollection support
        bool ICollection<IrcChannel>.IsReadOnly => true;
        void ICollection<IrcChannel>.Add(IrcChannel item) { throw new NotSupportedException("IrcChannelCollection is read-only."); }
        void ICollection<IrcChannel>.Clear() { throw new NotSupportedException("IrcChannelCollection is read-only."); }
        bool ICollection<IrcChannel>.Contains(IrcChannel item) => this.Channels.ContainsValue(item);
        bool ICollection<IrcChannel>.Remove(IrcChannel item) { throw new NotSupportedException("IrcChannelCollection is read-only."); }
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        #endregion
    }
}

