using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    public class ChannelUserCollection : IEnumerable<ChannelUser>, IEnumerable {
        public class Enumerator : IEnumerator<ChannelUser>, IEnumerator  {
            private ChannelUserCollection collection;
            private int index;
            private ChannelUser current;

            internal Enumerator(ChannelUserCollection collection) {
                this.collection = collection;
                collection.enumerator = this;
                this.index = collection.Count;
                this.current = null;
            }

            public void Dispose() { }

            public ChannelUser Current {
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

        private ChannelUser[] array;
        private int count;
        private ChannelUserCollection.Enumerator enumerator;

        public ChannelUserCollection() {
            this.array = new ChannelUser[4];
            this.count = 0;
        }

        public ChannelUserCollection.Enumerator GetEnumerator() {
            this.enumerator = new ChannelUserCollection.Enumerator(this);
            return this.enumerator;
        }

        IEnumerator<ChannelUser> IEnumerable<ChannelUser>.GetEnumerator() {
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
                array.SetValue(this.array[i], index++);
        }

        public ChannelUser this[int index] {
            get {
                if (index >= this.count) throw new ArgumentOutOfRangeException("index");
                return this.array[this.count - 1 - index];
            }
        }

        public ChannelUser this[string nickname] {
            get {
                for (int i = 0; i < this.count; ++i)
                    if (this.array[i].Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return this.array[i];
                throw new KeyNotFoundException();
            }
        }

        internal void Add(ChannelUser user) {
            this.enumerator = null;
            if (this.array.Length == this.count) {
                ChannelUser[] array2 = new ChannelUser[this.count * 2];
                this.array.CopyTo(array2, 0);
                this.array = array2;
            }
            this.array[this.count++] = user;
        }

        private void RemoveSub(int index) {
            this.enumerator = null;
            --this.count;
            if (index < this.count) Array.Copy(this.array, index + 1, this.array, index, this.count - index);
        }

        internal bool Remove(string nickname) {
            int i;
            for (i = 0; i < this.count; ++i)
                if (this.array[i].Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)) {
                    this.RemoveSub(i);
                    return true;
                }
            return false;
        }

        internal bool Remove(ChannelUser user) {
            int i;
            for (i = 0; i < this.count; ++i)
                if (this.array[i] == user) {
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
            for (int i = 0; i < this.count; ++i) {
                if (this.array[i].Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public bool TryGetValue(string nickname, out ChannelUser value) {
            for (int i = 0; i < this.count; ++i) {
                if (this.array[i].Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)) {
                    value = this.array[i];
                    return true;
                }
            }
            value = default(ChannelUser);
            return false;
        }

        public int IndexOf(string nickname) {
            for (int i = 0; i < this.count; ++i)
                if (this.array[i].Nickname.Equals(nickname, StringComparison.OrdinalIgnoreCase)) return this.count - 1 - i;
            return -1;
        }


    }

    /*
    public class UserCollection : SortedDictionary<string, IRC.User> {
        public new IRC.User this[string Nickname] {
            get {
                return base[Nickname];
            }
        }
        public new int Count {
            get {
                return base.Count;
            }
        }
        public int HalfVoiceCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.HalfVoice);
            }
        }
        public int VoiceCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.Voice);
            }
        }
        public int HalfOpCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.HalfOp);
            }
        }
        public int OpCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.Op);
            }
        }
        public int AdminCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.Admin);
            }
        }
        public int OwnerCount {
            get {
                return this.AccessCount(IRCClient.ChannelAccessModes.Owner);
            }
        }
        public UserCollection() {
        }
        public void Add(IRC.User User) {
            base.Add(User.Nickname, User);
        }
        public void Remove(IRC.User User) {
            base.Remove(User.Nickname);
        }
        public new void Remove(string Nickname) {
            base.Remove(Nickname);
        }
        public int AccessCount(IRCClient.ChannelAccessModes Access) {
            int lCount = 0;
            checked {
                try {
                    SortedDictionary<string, IRC.User>.Enumerator enumerator = this.GetEnumerator();
                    while (enumerator.MoveNext()) {
                        KeyValuePair<string, IRC.User> User = enumerator.Current;
                        bool flag = (User.Value.ChannelAccess & Access) == Access;
                        if (flag) {
                            ++lCount;
                        }
                    }
                } finally {
                    SortedDictionary<string, IRC.User>.Enumerator enumerator;
                    ((IDisposable) enumerator).Dispose();
                }
                return lCount;
            }
        }
        private bool UserHasAccess(string Nickname, IRCClient.ChannelAccessModes Access) {
            IRC.User User;
            bool flag = this.TryGetValue(Nickname, out User);
            return flag && User.ChannelAccess >= Access;
        }
        public bool IsHalfVoice(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.HalfVoice);
        }
        public bool IsVoice(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.Voice);
        }
        public bool IsHalfOp(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.HalfOp);
        }
        public bool IsOp(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.Op);
        }
        public bool IsAdmin(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.Admin);
        }
        public bool IsOwner(string Nickname) {
            return this.UserHasAccess(Nickname, IRCClient.ChannelAccessModes.Owner);
        }
    }
     */

}