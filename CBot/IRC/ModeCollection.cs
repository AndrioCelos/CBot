using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    /// <summary>
    /// Represents a read-only set of IRC modes and parameters.
    /// </summary>
    public class ModeSet : ISet<char>, IReadOnlyCollection<char> {
        private HashSet<char> modes = new HashSet<char>();
        private Dictionary<char, string> parameters = new Dictionary<char, string>(8);

        public int Count => this.modes.Count;

        internal void Add(char mode) => this.modes.Add(mode);
        internal void Add(char mode, string parameter) {
            this.modes.Add(mode);
            this.parameters[mode] = parameter;
        }
        internal bool Remove(char mode) {
            this.parameters.Remove(mode);
            return this.modes.Remove(mode);
        }
        internal void Clear() {
            this.modes.Clear();
            this.parameters.Clear();
        }

        public bool Contains(char item) => this.modes.Contains(item);

        public string GetParameter(char mode) => this.parameters[mode];
        internal string SetParameter(char mode, string parameter) => this.parameters[mode] = parameter;

        public void CopyTo(char[] array, int arrayIndex) => this.modes.CopyTo(array, arrayIndex);

        public IEnumerator<char> GetEnumerator() => this.modes.GetEnumerator();

        public override string ToString() {
            StringBuilder builder = new StringBuilder("+");
            StringBuilder builder2 = new StringBuilder();

            foreach (char mode in this.modes) {
                builder.Append(mode);

                string parameter;
                if (this.parameters.TryGetValue(mode, out parameter)) {
                    builder2.Append(' ');
                    builder2.Append(parameter);
                }
            }

            return builder.ToString() + builder2.ToString();
        }

        #region Interface implementations
        bool ICollection<char>.IsReadOnly => true;
        IEnumerator IEnumerable.GetEnumerator() => this.modes.GetEnumerator();

        bool ISet<char>.IsProperSubsetOf(IEnumerable<char> other) => this.modes.IsProperSubsetOf(other);
        bool ISet<char>.IsProperSupersetOf(IEnumerable<char> other) => this.modes.IsProperSupersetOf(other);
        bool ISet<char>.IsSubsetOf(IEnumerable<char> other) => this.modes.IsSubsetOf(other);
        bool ISet<char>.IsSupersetOf(IEnumerable<char> other) => this.modes.IsSupersetOf(other);
        bool ISet<char>.Overlaps(IEnumerable<char> other) => this.modes.Overlaps(other);
        bool ISet<char>.SetEquals(IEnumerable<char> other) => this.modes.SetEquals(other);

        void ICollection<char>.Add(char item) { throw new NotSupportedException("ModeCollection is read-only."); }
        bool ISet<char>.Add(char item) { throw new NotSupportedException("ModeCollection is read-only."); }
        void ICollection<char>.Clear() { throw new NotSupportedException("ModeCollection is read-only."); }
        void ISet<char>.ExceptWith(IEnumerable<char> other) { throw new NotSupportedException("ModeCollection is read-only."); }
        void ISet<char>.IntersectWith(IEnumerable<char> other) { throw new NotSupportedException("ModeCollection is read-only."); }
        bool ICollection<char>.Remove(char item) { throw new NotSupportedException("ModeCollection is read-only."); }
        void ISet<char>.SymmetricExceptWith(IEnumerable<char> other) { throw new NotSupportedException("ModeCollection is read-only."); }
        void ISet<char>.UnionWith(IEnumerable<char> other) { throw new NotSupportedException("ModeCollection is read-only."); }
        #endregion
    }

    public struct ModeChange {
        public bool Direction;
        public char Mode;
        public string Parameter;
    }
}
