using Newtonsoft.Json;

namespace CBot {
    /// <summary>
    /// Contains metadata about an active plugin.
    /// </summary>
    public class PluginEntry {
        /// <summary>The plugin's key.</summary>
		[JsonIgnore]
        public string Key { get; internal set; }
        /// <summary>The file path that the plugin was loaded from.</summary>
        public string Filename { get; internal set; }
		/// <summary>The plugin object itself.</summary>
		[JsonIgnore]  // Plugin-specific configuration needs to be done by the plugin itself.
		public Plugin Obj { get; internal set; }

		public PluginEntry(string key, string filename, string[] channels) {
			this.Key = key;
			this.Filename = filename;
			this.channels = channels;
		}

        private string[] channels;
        public string[] Channels {
            get { return this.Obj?.Channels ?? this.channels;  }
            set {
                if (this.Obj == null) this.channels = value;
                else this.Obj.Channels = value;
            }
        }
    }
}
