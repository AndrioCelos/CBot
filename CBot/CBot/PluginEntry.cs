using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CBot {
    /// <summary>
    /// Contains metadata about an active plugin.
    /// </summary>
    public class PluginEntry {
        /// <summary>The plugin's key.</summary>
        public string Key { get; internal set; }
        /// <summary>The file path that the plugin was loaded from.</summary>
        public string Filename { get; internal set; }
        /// <summary>The plugin object itself.</summary>
        public Plugin Obj { get; internal set; }
    }
}
