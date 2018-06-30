using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using CBot;
using AnIRC;

namespace PluginManager {
	[ApiVersion(3, 7)]
	public class PluginManagerPlugin : Plugin {
		public override string Name => "Plugin Manager";

		[Command(new string[] { "loadplugin", "load" }, 1, 2, "load [key] <file>", "Loads a plugin",
			Permission = "me.manageplugins")]
		public void CommandLoad(object sender, CommandEventArgs e) {
			string key; string filename; string realFilename;
			if (e.Parameters.Length == 2) {
				key = e.Parameters[0];
				filename = e.Parameters[1];
			} else {
				filename = e.Parameters[1];
				key = Path.GetFileNameWithoutExtension(filename);
			}

			if (Bot.Plugins.Contains(key)) {
				e.Reply(string.Format("A plugin with the key \u0002{0}\u000F is already loaded.", key));
				return;
			}
			if (!File.Exists(realFilename = filename)) {
				if (Path.IsPathRooted(realFilename)) {
					e.Reply(string.Format("The file \u0002{0}\u000F doesn't exist.", filename));
					return;
				} else {
					realFilename = Path.Combine("Plugins", filename);
					if (!File.Exists(realFilename)) {
						e.Reply(string.Format("The file \u0002{0}\u000F couldn't be found.", filename));
						return;
					}
				}
			}

			try {
				Bot.LoadPlugin(key, realFilename);
				e.Reply(string.Format("Loaded \u0002{0}\u0002.", Bot.Plugins[key].Obj.Name));
			} catch (InvalidPluginException ex) {
				e.Reply("That file could not be loaded: {0}", ex.Message);
			} catch (Exception ex) {
				e.Reply(string.Format("Failed to load the plugin: {0}", ex.Message));
			}
		}

		[Command(new string[] { "saveplugin", "save" }, 1, 1, "save <key>", "Instructs a plugin to save data.",
			Permission = "me.manageplugins")]
		public void CommandSave(object sender, CommandEventArgs e) {
			PluginEntry plugin;
			if (Bot.Plugins.TryGetValue(e.Parameters[0], out plugin)) {
				plugin.Obj.OnSave();
				e.Reply(string.Format("Called \u0002{0}\u0002 to save successfully.", e.Parameters[0]));
			} else {
				e.Reply(string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
			}
		}

		[Command(new string[] { "saveallplugins", "saveall" }, 0, 0, "saveall", "Instructs all plugins to save data.",
			Permission = "me.manageplugins")]
		public void CommandSaveAll(object sender, CommandEventArgs e) {
			foreach (PluginEntry pluginData in Bot.Plugins) {
				pluginData.Obj.OnSave();
			}
			e.Reply("Called plugins to save successfully.");
		}

		[Command(new string[] { "unloadplugin", "unload" }, 1, 1, "unload <key>", "Drops a plugin. It's impossible to actually unload it on the fly.",
			Permission = "me.manageplugins")]
		public void CommandUnload(object sender, CommandEventArgs e) {
			PluginEntry plugin;
			if (Bot.Plugins.TryGetValue(e.Parameters[0], out plugin)) {
				Bot.DropPlugin(e.Parameters[0]);
				e.Reply(string.Format("Dropped \u0002{0}\u0002.", e.Parameters[0]));
			} else {
				e.Reply(string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
			}
		}

		[Command(new string[] { "channels", "chans", "pluginchannels", "pluginchans" }, 1, 2, "channels <plugin> [[+|-]channels]", "Views or changes a plugin's channel list.",
			Permission = "me.manageplugins")]
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
					e.Reply(string.Format("\u0002{0}\u0002 is now assigned to the following channels: \u0002{1}\u0002.", e.Parameters[0], string.Join("\u0002, \u0002", plugin.Obj.Channels)));
				} else {
					e.Reply(string.Format("\u0002{0}\u0002 is assigned to the following channels: \u0002{1}\u0002.", e.Parameters[0], string.Join("\u0002, \u0002", plugin.Obj.Channels)));
				}
			} else {
				e.Reply(string.Format("I haven't loaded a plugin with key \u0002{0}\u001F.", e.Parameters[0]));
			}
		}

		[Command("threadstate", 1, 1, "threadstate <network>", "Shows what command an IRC read thread is currently doing.",
			Permission = "me.debug")]
		public void CommandThreadState(object sender, CommandEventArgs e) {
			IrcClient client = null;

			foreach (ClientEntry clientEntry in Bot.Clients) {
				IrcClient _client = clientEntry.Client;
				if (_client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (_client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
					client = _client;
					break;
				}
			}
			if (client == null) {
				e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
				return;
			}

			var method = Bot.GetClientEntry(client).CurrentProcedure;
			if (method == null) {
				e.Reply(string.Format("\u0002{0}\u0002's read thread is \u0002standing by\u0002.", e.Parameters[0]));
			} else {
				var attribute = method.GetCustomAttributes<CommandAttribute>().FirstOrDefault();
				if (attribute != null) {
					e.Reply(string.Format("\u0002{0}\u0002's read thread is in \u0002{1}\u0002 – !\u0002{2}\u0002.", e.Parameters[0], Bot.GetClientEntry(client).CurrentPlugin.Key, attribute.Names[0]));
				} else {
					e.Reply(string.Format("\u0002{0}\u0002's read thread is in \u0002{1}\u0002 – !\u0002{2}\u0002.", e.Parameters[0], Bot.GetClientEntry(client).CurrentPlugin.Key, method.Name));
				}
			}
		}
	}
}
