using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRC {
    /// <summary>Represents a list of status modes a user has on a channel.</summary>
    public class ChannelStatus : IEnumerable, IEnumerable<char>, IReadOnlyCollection<char>, ISet<char> {
        /// <summary>The IRCClient that this object belongs to.</summary>
        public IRCClient Client { get; }

        private HashSet<char> modes;

        /// <summary>Returns true if this set contains half-voice (channel mode V).</summary>
        public bool HasHalfVoice => this.modes.Contains('V');
        /// <summary>Returns true if this set contains voice (channel mode v).</summary>
        public bool HasVoice     => this.modes.Contains('v');
        /// <summary>Returns true if this set contains half-operator status (channel mode h).</summary>
        public bool HasHalfop    => this.modes.Contains('h');
        /// <summary>Returns true if this set contains operator status (channel mode o).</summary>
        public bool HasOp        => this.modes.Contains('o');
        /// <summary>Returns true if this set contains administrator status (channel mode a).</summary>
        public bool HasAdmin     => this.modes.Contains('a');
        /// <summary>Returns true if this set contains owner status (channel mode q).</summary>
        public bool HasOwner     => this.modes.Contains('q');

        private static ChannelStatus[] standardModes;
        /// <summary>Returns a ChannelStatus object that represents half-voice and is not associated with any network.</summary>
        public static ChannelStatus HalfVoice => standardModes[0];
        /// <summary>Returns a ChannelStatus object that represents voice and is not associated with any network.</summary>
        public static ChannelStatus Voice     => standardModes[1];
        /// <summary>Returns a ChannelStatus object that represents half-operator status and is not associated with any network.</summary>
        public static ChannelStatus Halfop    => standardModes[2];
        /// <summary>Returns a ChannelStatus object that represents operator status and is not associated with any network.</summary>
        public static ChannelStatus Op        => standardModes[3];
        /// <summary>Returns a ChannelStatus object that represents administrator status and is not associated with any network.</summary>
        public static ChannelStatus Admin     => standardModes[4];
        /// <summary>Returns a ChannelStatus object that represents owner status and is not associated with any network.</summary>
        public static ChannelStatus Owner     => standardModes[5];

        static ChannelStatus() {
            standardModes = new ChannelStatus[6];

            standardModes[0] = new ChannelStatus(null);
            standardModes[1] = new ChannelStatus(null);
            standardModes[2] = new ChannelStatus(null);
            standardModes[3] = new ChannelStatus(null);
            standardModes[4] = new ChannelStatus(null);
            standardModes[5] = new ChannelStatus(null);

            standardModes[0].Add('V');
            standardModes[1].Add('v');
            standardModes[2].Add('h');
            standardModes[3].Add('o');
            standardModes[4].Add('a');
            standardModes[5].Add('q');
        }

        /// <summary>Creates a new empty ChannelStatus set associated with the given IRCClient.</summary>
        /// <param name="client">The IRCClient object to which the new object is relevant.</param>
        public ChannelStatus(IRCClient client) {
            this.Client = client;
            this.modes = new HashSet<char>();
        }
        /// <summary>Creates a new ChannelStatus object associated with the given IRCClient and with the given set of channel modes.</summary>
        /// <param name="client">The IRCClient object to which the new object is relevant.</param>
        /// <param name="modes">An IEnumerable&lt;char&gt; containing channel modes to add to the set.</param>
        public ChannelStatus(IRCClient client, IEnumerable<char> modes) {
            this.Client = client;
            this.modes = new HashSet<char>(modes);
        }

        /// <summary>Creates a new ChannelStatus object associated with the given IRCClient and with the given status prefix.</summary>
        /// <param name="client">The IRCClient object to which the new object is relevant.</param>
        /// <param name="prefixes">The prefixes to add to the set.</param>
        public static ChannelStatus FromPrefix(IRCClient client, IEnumerable<char> prefixes) {
            var prefixTable = (client?.Extensions ?? new Extensions()).StatusPrefix;
            var status = new ChannelStatus(client);

            foreach (var prefix in prefixes) {
                char mode;
                if (prefixTable.TryGetValue(prefix, out mode)) status.Add(mode);
            }
            return status;
        }

        protected internal bool Add(char mode) {
            return this.modes.Add(mode);
        }
        protected internal bool Remove(char mode) {
            return this.modes.Remove(mode);
        }
        protected internal void Clear() {
            this.modes.Clear();
        }

        /// <summary>
        /// Compares this ChannelStatus object with another object and returns true if they are equal.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public override bool Equals(object other) {
            if (other == null || !(other is ChannelStatus)) return false;
            return (this == (ChannelStatus) other);
        }

        /// <summary>Returns the status prefixes represented by this ChannelStatus object.</summary>
        public string GetPrefixes() {
            StringBuilder builder = new StringBuilder(this.Count);
            foreach (var prefix in (this.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (this.Contains(prefix.Value)) builder.Append(prefix.Key);
            }
            return builder.ToString();
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        /// <summary>Returns the channel modes represented by this ChannelStatus object.</summary>
        public override string ToString() {
            return new string(this.modes.ToArray());
        }

        /// <summary>
        /// Compares this ChannelStatus object with another object and returns true if they are equal.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator ==(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return ((object) v2 == null);
            if ((object) v2 == null) return false;

            // TODO: This'll do weird things if the two objects are from different IRC networks.
            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return v2.Contains(prefix.Value);
                if (v2.Contains(prefix.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Compares this ChannelStatus object with another object and returns true if they are not equal.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator !=(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return ((object) v2 != null);
            if ((object) v2 == null) return false;

            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return !v2.Contains(prefix.Value);
                if (v2.Contains(prefix.Value)) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if v1 represents a lower status than v2.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator <(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return ((object) v2 != null);
            if ((object) v2 == null) return false;

            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return false;
                if (v2.Contains(prefix.Value)) return !v1.Contains(prefix.Value);
            }
            return false;
        }

        /// <summary>
        /// Returns true if v1 represents a lower or equal status to v2.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator <=(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return true;
            if ((object) v2 == null) return ((object) v1 == null);

            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return v2.Contains(prefix.Value);
                if (v2.Contains(prefix.Value)) return true;
            }
            return true;
        }

        /// <summary>
        /// Returns true if v1 represents a higher or equal status to v2.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator >=(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return ((object) v2 == null);
            if ((object) v2 == null) return true;

            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return true;
                if (v2.Contains(prefix.Value)) return v1.Contains(prefix.Value);
            }
            return true;
        }

        /// <summary>
        /// Returns true if v1 represents a higher status than v2.
        /// Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.
        /// </summary>
        public static bool operator >(ChannelStatus v1, ChannelStatus v2) {
            if ((object) v1 == null) return false;
            if ((object) v2 == null) return ((object) v1 != null);

            foreach (var prefix in (v1.Client?.Extensions ?? v2.Client?.Extensions ?? new Extensions()).StatusPrefix) {
                if (v1.Contains(prefix.Value)) return !v2.Contains(prefix.Value);
                if (v2.Contains(prefix.Value)) return false;
            }
            return false;
        }

        #region Set methods
        public int Count => this.modes.Count;
        public bool IsReadOnly => true;

        public bool Contains(char item) {
            return this.modes.Contains(item);
        }

        public void CopyTo(char[] array, int arrayIndex) {
            this.modes.CopyTo(array, arrayIndex);
        }

        public IEnumerator<char> GetEnumerator() {
            return this.modes.GetEnumerator();
        }

        public bool IsSubsetOf(IEnumerable<char> other) {
            return this.modes.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<char> other) {
            return this.modes.IsSupersetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<char> other) {
            return this.modes.IsProperSupersetOf(other);
        }

        public bool IsProperSubsetOf(IEnumerable<char> other) {
            return this.modes.IsProperSubsetOf(other);
        }

        public bool Overlaps(IEnumerable<char> other) {
            return this.modes.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<char> other) {
            return this.modes.SetEquals(other);
        }
        #endregion

        #region Explitit interface implementations
        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        void ICollection<char>.Add(char item) {
            throw new NotSupportedException();
        }

        void ICollection<char>.Clear() {
            throw new NotSupportedException();
        }

        bool ICollection<char>.Remove(char item) {
            throw new NotSupportedException();
        }

        bool ISet<char>.Add(char item) {
            throw new NotSupportedException();
        }

        void ISet<char>.UnionWith(IEnumerable<char> other) {
            throw new NotSupportedException();
        }

        void ISet<char>.IntersectWith(IEnumerable<char> other) {
            throw new NotSupportedException();
        }

        void ISet<char>.ExceptWith(IEnumerable<char> other) {
            throw new NotSupportedException();
        }

        void ISet<char>.SymmetricExceptWith(IEnumerable<char> other) {
            throw new NotSupportedException();
        }
        #endregion
    }
}
