using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    public class ChannelCollection : IEnumerable<Channel>, IEnumerable {
        public class Enumerator : IEnumerator<Channel>, IDisposable, IEnumerator {
            private ChannelCollection collection;
            private int index;
            private Channel current;

            internal Enumerator(ChannelCollection collection) {
                this.collection = collection;
                collection.enumerator = this;
                this.index = collection.Count;
                this.current = null;
            }

            public void Dispose() { }

            public Channel Current {
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

        private Channel[] array;
        private int count;
        private ChannelCollection.Enumerator enumerator;
        private IRCClient client;

        public ChannelCollection() {
            this.array = new Channel[4];
            this.count = 0;
        }
        public ChannelCollection(IRCClient client) : this() {
            this.client = client;
        }

        public ChannelCollection.Enumerator GetEnumerator() {
            this.enumerator = new ChannelCollection.Enumerator(this);
            return this.enumerator;
        }

        IEnumerator<Channel> IEnumerable<Channel>.GetEnumerator() {
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

        public Channel this[int index] {
            get {
                if (index >= this.count) throw new ArgumentOutOfRangeException("index");
                return this.array[this.count - 1 - index];
            }
        }

        public Channel this[string nickname] {
            get {
                for (int i = 0; i < this.count; ++i)
                    if (this.array[i].Name.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return this.array[i];
                throw new KeyNotFoundException();
            }
        }

        internal void Add(Channel Channel) {
            this.enumerator = null;
            if (this.array.Length == this.count) {
                Channel[] array2 = new Channel[this.count * 2];
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

        internal bool Remove(Channel Channel) {
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

        public bool TryGetValue(string name, out Channel value) {
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
            value = default(Channel);
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

