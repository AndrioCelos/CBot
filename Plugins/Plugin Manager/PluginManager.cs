using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CBot;
using IRC;

namespace PluginManager
{
    [APIVersion(3, 1)]
    public class PluginManagerPlugin : Plugin
    {
        public override string Name {
            get {
                return "Plugin Manager";
            }
        }

        [Command(new string[] { "loadplugin", "load" }, 1, 2, "load [key] <file>", "Loads a plugin",
            "me.manageplugins", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandLoad(object sender, CommandEventArgs e) {
            string key; string filename; string realFilename;
            if (e.Parameters.Length == 2) {
                key = e.Parameters[0];
                filename = e.Parameters[1];
            } else {
                filename = e.Parameters[1];
                key = Path.GetFileNameWithoutExtension(filename);
            }

            if (Bot.Plugins.ContainsKey(key)) {
                Bot.Say(e.Connection, e.Channel, string.Format("A plugin with the key \u0002{0}\u000F is already loaded.", key));
                return;
            }
            if (!File.Exists(realFilename = filename)) {
                if (Path.IsPathRooted(realFilename)) {
                    Bot.Say(e.Connection, e.Channel, string.Format("The file \u0002{0}\u000F doesn't exist.", filename));
                    return;
                } else {
                    realFilename = Path.Combine("Plugins", filename);
                    if (!File.Exists(realFilename)) {
                        Bot.Say(e.Connection, e.Channel, string.Format("The file \u0002{0}\u000F couldn't be found.", filename));
                        return;
                    }
                }
            }

            try {
                if (Bot.LoadPlugin(key, realFilename))
                    Bot.Say(e.Connection, e.Channel, string.Format("Loaded \u0002{0}\u0002.", Bot.Plugins[key].Obj.Name));
                else
                    Bot.Say(e.Connection, e.Channel, "Failed to load the plugin. See the console for details.");
            } catch (Exception ex) {
                    Bot.Say(e.Connection, e.Channel, string.Format("Failed to load the plugin: {0}", ex.Message));
            }
        }

        [Command(new string[] { "saveplugin", "save" }, 1, 1, "save <key>", "Instructs a plugin to save data.",
            "me.manageplugins", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandSave(object sender, CommandEventArgs e) {
            PluginEntry plugin;
            if (Bot.Plugins.TryGetValue(e.Parameters[0], out plugin)) {
                plugin.Obj.OnSave();
                Bot.Say(e.Connection, e.Channel, string.Format("Called \u0002{0}\u0002 to save successfully.", e.Parameters[0]));
            } else {
                Bot.Say(e.Connection, e.Channel, string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
            }
        }

        [Command(new string[] { "saveallplugins", "saveall" }, 0, 0, "saveall", "Instructs all plugins to save data.",
            "me.manageplugins", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandSaveAll(object sender, CommandEventArgs e) {
            foreach (PluginEntry pluginData in Bot.Plugins.Values) {
                pluginData.Obj.OnSave();
            }
            Bot.Say(e.Connection, e.Channel, "Called plugins to save successfully.");
        }

        [Command(new string[] { "unloadplugin", "unload" }, 1, 1, "unload <key>", "Drops a plugin. It's impossible to actually unload it on the fly.",
            "me.manageplugins", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandUnload(object sender, CommandEventArgs e) {
            PluginEntry plugin;
            if (Bot.Plugins.TryGetValue(e.Parameters[0], out plugin)) {
                plugin.Obj.OnUnload();
                plugin.Obj.Channels = new string[0];
                Bot.Plugins.Remove(e.Parameters[0]);
                Bot.Say(e.Connection, e.Channel, string.Format("Dropped \u0002{0}\u0002.", e.Parameters[0]));
            } else {
                Bot.Say(e.Connection, e.Channel, string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
            }
        }

        [Command(new string[] { "channels", "chans", "pluginchannels", "pluginchans" }, 1, 2, "channels <plugin> [[+|-]channels]", "Views or changes a plugin's channel list.",
            "me.manageplugins", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandChannels(object sender, CommandEventArgs e) {
            PluginEntry plugin; short direction = 0;
            if (Bot.Plugins.TryGetValue(e.Parameters[0], out plugin)) {
                if (e.Parameters.Length == 2) {
                    List<string> channels = new List<string>(plugin.Obj.Channels);

                    foreach (string channel in e.Parameters[1].Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                        string realChannel;

                        if (channel.StartsWith("+")) {
                            direction = 1;
                            realChannel = channel.Substring(1);
                        } else if (channel.StartsWith("-")) {
                            direction = -1;
                            realChannel = channel.Substring(1);
                        } else {
                            if (direction == 0) {
                                channels.Clear();
                                direction = 1;
                            }
                            realChannel = channel;
                        }

                        if (direction == 1) {
                            if (!channels.Contains(realChannel, StringComparer.OrdinalIgnoreCase)) channels.Add(realChannel);
                        } else {
                            for (int i = 0; i < channels.Count; ++i) {
                                if (channels[i].Equals(realChannel, StringComparison.OrdinalIgnoreCase)) {
                                    channels.RemoveAt(i);
                                    break;
                                }
                            }
                        }
                    }
                    plugin.Obj.Channels = channels.ToArray();
                    Bot.Say(e.Connection, e.Channel, string.Format("\u0002{0}\u0002 is now assigned to the following channels: \u0002{1}\u0002.", e.Parameters[0], string.Join("\u0002, \u0002", plugin.Obj.Channels)));
                } else {
                    Bot.Say(e.Connection, e.Channel, string.Format("\u0002{0}\u0002 is assigned to the following channels: \u0002{1}\u0002.", e.Parameters[0], string.Join("\u0002, \u0002", plugin.Obj.Channels)));
                }
            } else {
                Bot.Say(e.Connection, e.Channel, string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
            }
        }

    }
}
