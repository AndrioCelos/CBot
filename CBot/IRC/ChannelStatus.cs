using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IRC {
    /// <summary>Represents a set of status modes a user has on a channel.</summary>
    /// <example>
    ///     The following code checks whether we have halfop status or higher in a channel.
    ///     <code>
    ///         var me = client.Channels["#lobby"].Me;
    ///         if (me.Status >= ChannelStatus.Halfop) {
    ///             // Do something.
    ///         } else
    ///             client.Channels["#lobby"].Say("I don't have halfop status.");
    ///     </code>
    ///     If the network doesn't have halfops, the check becomes equivalent to <c>(me.Status > ChannelStatus.Voice)</c>.
    /// </example>
    public class ChannelStatus : ISet<char>, IReadOnlyCollection<char>, IComparable<ChannelStatus>, IEquatable<ChannelStatus> {
        /// <summary>The <see cref="IrcClient"/> that this object belongs to.</summary>
        public IrcClient Client { get; }

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
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents half-voice (channel mode V) and is not associated with any network.</summary>
        public static ChannelStatus HalfVoice => standardModes[0];
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents voice (channel mode v) and is not associated with any network.</summary>
        public static ChannelStatus Voice     => standardModes[1];
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents half-operator status (channel mode h) and is not associated with any network.</summary>
        public static ChannelStatus Halfop    => standardModes[2];
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents operator status (channel mode o) and is not associated with any network.</summary>
        public static ChannelStatus Op        => standardModes[3];
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents administrator status (channel mode a) and is not associated with any network.</summary>
        public static ChannelStatus Admin     => standardModes[4];
        /// <summary>Returns a <see cref="ChannelStatus"/> object that represents owner status (channel mode q) and is not associated with any network.</summary>
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

		/// <summary>Initializes a new empty <see cref="ChannelStatus"/> set associated with no <see cref="IrcClient"/>.</summary>
		public ChannelStatus() : this(null) { }
		/// <summary>Initializes a new empty <see cref="ChannelStatus"/> set associated with the given <see cref="IrcClient"/>.</summary>
		/// <param name="client">The <see cref="IrcClient"/> object to which the new object is relevant.</param>
		public ChannelStatus(IrcClient client) {
            this.Client = client;
            this.modes = new HashSet<char>();
        }
		/// <summary>Initializes a new <see cref="ChannelStatus"/> object associated with the given <see cref="IrcClient"/> and with the given set of channel modes.</summary>
		/// <param name="client">The <see cref="IrcClient"/> object to which the new object is relevant.</param>
		/// <param name="modes">An <see cref="IEnumerable{T}"/> of <see cref="char"/> containing channel modes to add to the set.</param>
		public ChannelStatus(IrcClient client, IEnumerable<char> modes) {
            this.Client = client;
            this.modes = new HashSet<char>(modes);
        }

        /// <summary>Initializes a new ChannelStatus object associated with the given <see cref="IrcClient"/> and with the given status prefixes.</summary>
        /// <param name="client">The <see cref="IrcClient"/> object to which the new object is relevant.</param>
        /// <param name="prefixes">The prefixes to add to the set. Unrecognizes prefixes are ignored.</param>
        public static ChannelStatus FromPrefix(IrcClient client, IEnumerable<char> prefixes) {
			var extensions = client?.Extensions ?? IrcExtensions.Default;
            var prefixTable = extensions.StatusPrefix;

            var status = new ChannelStatus(client);

            foreach (var prefix in prefixes) {
                char mode;
                if (prefixTable.TryGetValue(prefix, out mode)) status.Add(mode);
            }
            return status;
        }

        /// <summary>Adds a mode to this set.</summary>
        /// <returns>True if the mode was added; false if it was not because it was already in the set.</returns>
        protected internal bool Add(char mode) => this.modes.Add(mode);
        /// <summary>Removes a mode from this set.</summary>
        /// <returns>True if the mode was removed; false if it was not in the set.</returns>
        protected internal bool Remove(char mode) => this.modes.Remove(mode);
        /// <summary>Removes all modes from this set.</summary>
        protected internal void Clear() => this.modes.Clear();

        /// <summary>
        /// Compares this <see cref="ChannelStatus"/> object with another object and returns true if they are equal.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public override bool Equals(object other) => (this == other as ChannelStatus);
        /// <summary>
        /// Compares this <see cref="ChannelStatus"/> object with another <see cref="ChannelStatus"/> object and returns true if they are equal.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public bool Equals(ChannelStatus other) => (this == other);

		/// <summary>
		///     Compares this <see cref="ChannelStatus"/> object with another <see cref="ChannelStatus"/> object and returns a value indicating their relative power level.
		///     <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
		/// </summary>
		/// <returns>
		///		<list type="bullet">
		///			<item><description>Less than zero if this <see cref="ChannelStatus"/> object represents lower status than the other;</description></item>
		///			<item><description>Zero if this <see cref="ChannelStatus"/> object represents equal status to the other;</description></item>
		///			<item><description>Greater than zero if this <see cref="ChannelStatus"/> object represents higher status than the other, or the other is null.</description></item>
		///		</list>
		/// </returns>
		/// <remarks>
		///     <para>This method takes into consideration all status modes listed in the RPL_ISUPPORT reply,
		///     plus modes mentioned by the static fields of <see cref="ChannelStatus"/>, even if the IRC network doesn't support those modes.</para>
		///     <para>For example, on networks that don't have halfops,
		///         <c>user.Status >= <see cref="Halfop"/></c> is equivalent to <c>user.Status > <see cref="Voice"/></c>.</para>
		/// </remarks>
		public int CompareTo(ChannelStatus other) {
            if ((object) other == null) return 1;

			var extensions = this.Client?.Extensions ?? other.Client?.Extensions ?? IrcExtensions.Default;
            foreach (var mode in extensions.allStatus) {
                if (this.Contains(mode)) return (other.Contains(mode) ? 0 : 1);
                if (other.Contains(mode)) return -1;
            }
            return 0;
        }

		/// <summary>Returns the status prefixes represented by this <see cref="ChannelStatus"/> object.</summary>
		public string GetPrefixes() {
            var builder = new StringBuilder(this.Count);

			var extensions = this.Client?.Extensions ?? IrcExtensions.Default;
			foreach (var prefix in extensions.StatusPrefix) {
                if (this.Contains(prefix.Value)) builder.Append(prefix.Key);
            }
            return builder.ToString();
        }

        /// <summary>Returns the hash code of this <see cref="ChannelStatus"/> object's string representation.</summary>
        /// <seealso cref="ToString"/>
        public override int GetHashCode() => this.ToString().GetHashCode();

        /// <summary>Returns the channel modes represented by this ChannelStatus object.</summary>
        public override string ToString() => new string(this.modes.ToArray());

		/// <summary>
		/// Compares this <see cref="ChannelStatus"/> object with another object and returns true if they are equal.
		/// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
		/// </summary>
		public static bool operator ==(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 == null ? (object) v2 == null : v1.CompareTo(v2) == 0);

		/// <summary>
		/// Compares this <see cref="ChannelStatus"/> object with another object and returns true if they are not equal.
		/// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
		/// </summary>
		public static bool operator !=(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 == null ? (object) v2 != null : v1.CompareTo(v2) != 0);


        /// <summary>
        /// Returns true if <paramref name="v1"/> represents a lower status than <paramref name="v2"/>.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public static bool operator <(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 == null ? v2 != (object) null : v1.CompareTo(v2) < 0);

        /// <summary>
        /// Returns true if <paramref name="v1"/> represents a lower or equal status to <paramref name="v2"/>.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public static bool operator <=(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 == null || v1.CompareTo(v2) <= 0);

        /// <summary>
        /// Returns true if <paramref name="v1"/> represents a higher or equal status to <paramref name="v2"/>.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public static bool operator >=(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 == null ? (object) v2 == null : v1.CompareTo(v2) >= 0);

        /// <summary>
        /// Returns true if <paramref name="v1"/> represents a higher status than <paramref name="v2"/>.
        /// <para>Two sets with the same highest ranking mode are considered equal regardless of lower-ranking modes.</para>
        /// </summary>
        public static bool operator >(ChannelStatus v1, ChannelStatus v2)
            => ((object) v1 != null && v1.CompareTo(v2) > 0);

        #region Set methods
		/// <summary>Returns the number of modes in this set.</summary>
        public int Count => this.modes.Count;
		/// <summary>Returns true, indicating that <see cref="ChannelStatus"/> is read-only.</summary>
        public bool IsReadOnly => true;

		/// <summary>Determines whether the specified mode character is present in this set.</summary>
        public bool Contains(char item) => this.modes.Contains(item);
        public void CopyTo(char[] array, int arrayIndex) => this.modes.CopyTo(array, arrayIndex);
        public IEnumerator<char> GetEnumerator() => this.modes.GetEnumerator();
        public bool IsSubsetOf(IEnumerable<char> other) => this.modes.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<char> other) => this.modes.IsSupersetOf(other);
        public bool IsProperSupersetOf(IEnumerable<char> other) => this.modes.IsProperSupersetOf(other);
        public bool IsProperSubsetOf(IEnumerable<char> other) => this.modes.IsProperSubsetOf(other);
        public bool Overlaps(IEnumerable<char> other) => this.modes.Overlaps(other);
        public bool SetEquals(IEnumerable<char> other) => this.modes.SetEquals(other);
        #endregion

        #region Explicit interface implementations
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        void ICollection<char>.Add(char item) { throw new NotSupportedException("ChannelStatus is read-only."); }
        void ICollection<char>.Clear() { throw new NotSupportedException("ChannelStatus is read-only."); }
        bool ICollection<char>.Remove(char item) { throw new NotSupportedException("ChannelStatus is read-only."); }
        bool ISet<char>.Add(char item) { throw new NotSupportedException("ChannelStatus is read-only."); }
        void ISet<char>.UnionWith(IEnumerable<char> other) { throw new NotSupportedException("ChannelStatus is read-only."); }
        void ISet<char>.IntersectWith(IEnumerable<char> other) { throw new NotSupportedException("ChannelStatus is read-only."); }
        void ISet<char>.ExceptWith(IEnumerable<char> other) { throw new NotSupportedException("ChannelStatus is read-only."); }
        void ISet<char>.SymmetricExceptWith(IEnumerable<char> other) { throw new NotSupportedException("ChannelStatus is read-only."); }
        #endregion
    }
}
