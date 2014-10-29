using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CBot {
    public class PluginEntry {
        public string Key { get; internal set; }
        public string Filename { get; internal set; }
        public Plugin Obj { get; internal set; }
    }
}
