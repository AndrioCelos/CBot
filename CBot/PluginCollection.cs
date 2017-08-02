using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CBot {
    public class PluginCollection : ReadOnlyCollection<PluginEntry> {
        public PluginCollection() : base(new List<PluginEntry>()) { }

        public PluginEntry this[string key] {
            get {
                foreach (var plugin in this) {
                    if (plugin.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                        return plugin;
                }
                throw new KeyNotFoundException("No plugin with the specified key is loaded.");
            }
        }

        public int IndexOf(string key) {
            for (int i = 0; i < this.Count; ++i) {
                if (this[i].Key.Equals(key, StringComparison.InvariantCultureIgnoreCase))
                    return i;
            }
            return -1;
        }

        public bool Contains(string key) => (this.IndexOf(key) != -1);

        public bool TryGetValue(string key, out PluginEntry plugin) {
            foreach (var _plugin in this) {
                if (_plugin.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                    plugin = _plugin;
                    return true;
                }
            }
            plugin = null;
            return false;
        }

        internal void Add(PluginEntry plugin) => this.Items.Add(plugin);
        internal bool Remove(string key) {
            int index = this.IndexOf(key);
            if (index == -1) return false;
            this.RemoveAt(index);
            return true;
        }
        internal void RemoveAt(int index) => this.Items.RemoveAt(index);
    }
}
