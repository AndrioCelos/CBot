using System;
using System.Collections;
using System.Collections.Generic;

namespace IRC {
    public class IRCChannelCollection : IEnumerable<IRCChannel>, IEnumerable {
        public class Enumerator : IEnumerator<IRCChannel>, IDisposable, IEnumerator {
            private IRCChannelCollection collection;
            private int index;
            private IRCChannel current;

            internal Enumerator(IRCChannelCollection collection) {
                this.collection = collection;
                collection.enumerator = this;
                this.index = collection.Count;
                this.current = null;
            }

            public void Dispose() { }

            public IRCChannel Current {
                get { return current; }
            }

            object IEnumerator.Current {
                get { return this.Current; }
            }

            public bool MoveNext() {
                if (collection.enumerator != this) throw new InvalidOperationException("The enumerator is invalid because the collection has changed.");
                if (index <= 0) {
                    this.current = null;
                    return false;
                }
                this.current = collection.array[--index];
                return true;
            }

            public void Reset() {
                if (collection.enumerator != this) throw new InvalidOperationException("The enumerator is invalid because the collection has changed.");
                index = collection.Count;
                this.current = null;
            }
        }

        private IRCChannel[] array;
        private int count;
        private IRCChannelCollection.Enumerator enumerator;
        private IRCClient client;

        public IRCChannelCollection() {
            this.array = new IRCChannel[4];
            this.count = 0;
        }
        public IRCChannelCollection(IRCClient client) : this() {
            this.client = client;
        }

        public IRCChannelCollection.Enumerator GetEnumerator() {
            this.enumerator = new IRCChannelCollection.Enumerator(this);
            return this.enumerator;
        }

        IEnumerator<IRCChannel> IEnumerable<IRCChannel>.GetEnumerator() {
            return this.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return this.GetEnumerator();
        }

        public int Count {
            get { return count; }
        }

        public void CopyTo(Array array, int index) {
            if (array == null)
                throw new ArgumentNullException("array");
            if (array.Rank != 1)
                throw new ArgumentException("array cannot be multi-dimensional.", "array");
            if (index < 0)
                throw new ArgumentOutOfRangeException("index cannot be negative.", "index");
            if (array.Length - index < this.count)
                throw new ArgumentException("index is outside of the array.", "index");

            for (int i = this.Count - 1; i >= 0; --i)
                array.SetValue(this.array[i], ++index);
        }

        public IRCChannel this[int index] {
            get {
                if (index >= this.count) throw new ArgumentOutOfRangeException("index");
                return this.array[this.count - 1 - index];
            }
        }

        public IRCChannel this[string nickname] {
            get {
                for (int i = 0; i < this.count; ++i)
                    if (this.array[i].Name.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return this.array[i];
                throw new KeyNotFoundException();
            }
        }

        internal void Add(IRCChannel Channel) {
            this.enumerator = null;
            if (this.array.Length == this.count) {
                IRCChannel[] array2 = new IRCChannel[this.count * 2];
                this.array.CopyTo(array2, 0);
                this.array = array2;
            }
            this.array[this.count++] = Channel;
        }

        private void RemoveSub(int index) {
            this.enumerator = null;
            --this.count;
            if (index < this.count) Array.Copy(this.array, index + 1, this.array, index, this.count - index);
        }

        internal bool Remove(string nickname) {
            int i;
            for (i = 0; i < this.count; ++i)
                if (this.array[i].Name.Equals(nickname, StringComparison.OrdinalIgnoreCase)) {
                    this.RemoveSub(i);
                    return true;
                }
            return false;
        }

        internal bool Remove(IRCChannel Channel) {
            int i;
            for (i = 0; i < this.count; ++i)
                if (this.array[i] == Channel) {
                    this.RemoveSub(i);
                    return true;
                }
            return false;
        }

        internal void Clear() {
            this.enumerator = null;
            this.count = 0;
        }

        public bool Contains(string nickname) {
            for (int i = 0; i < this.count; ++i)
                if (this.array[i].Name.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public bool TryGetValue(string name, out IRCChannel value) {
            StringComparer comparer;
            if (this.client == null)
                comparer = IRCStringComparer.RFC1459;
            else
                comparer = this.client.CaseMappingComparer;

            for (int i = 0; i < this.count; ++i) {
                if (comparer.Equals(this.array[i].Name, name)) {
                    value = this.array[i];
                    return true;
                }
            }
            value = default(IRCChannel);
            return false;
        }

        public int IndexOf(string name) {
            StringComparer comparer;
            if (this.client == null)
                comparer = IRCStringComparer.RFC1459;
            else
                comparer = this.client.CaseMappingComparer;

            for (int i = 0; i < this.count; ++i)
                if (comparer.Equals(this.array[i].Name, name)) return this.count - 1 - i;
            return -1;
        }
    }
}

