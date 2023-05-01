using System.Reflection;
using System.Text;
using AnIRC;
using CBot;

namespace HelpCommand;
[ApiVersion(4, 0)]
public class HelpCommandPlugin : Plugin {
	public override string Name => "HelpCommand";

	public override string Help(string Topic, IrcMessageTarget target) {
		if (Topic == null) {
			return $"Use {Colours.Bold}!cmdlist{Colours.Bold} for a list of my commands, or {Colours.Bold}!cmdinfo {Colours.Underline}label{Colours.Reset} for information on one.";
		}
		return null;
	}

	[Command("help", 0, 1, "help [topic]", "Shows help text for active plugins.")]
	public async void CommandHelp(object? sender, CommandEventArgs e)
		=> await this.ShowHelp(e.Target, e.Sender, e.Parameters.Length >= 1 ? e.Parameters[0] : null);

	[Trigger(@"^\s*help\s*$")]
	public async void TriggerHelp(object? sender, TriggerEventArgs e)
		=> await this.ShowHelp(e.Target, e.Sender, null);

	private async Task ShowHelp(IrcMessageTarget target, IrcUser user, string topic) {
		IrcMessageTarget target2 = null;
		bool anyText = false;

		// If the topic is a channel name, look for information on that channel.
		if (target.Client.IsChannel(topic)) {
			if (target.Client.Channels.TryGetValue(topic, out var c)) {
				target2 = c;
			} else {
				Bot.Say(user.Client, user.Nickname, "I'm not on that channel.");
				return;
			}
		} else
			target2 = target;

		foreach (var plugin in Bot.Plugins) {
			if (plugin.Obj.IsActiveTarget(target2)) {
				var text = plugin.Obj.Help(null, target);
				if (text != null) {
					anyText = true;
					foreach (var line in text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)) {
						Bot.Say(user.Client, user.Nickname, Bot.ReplaceCommands(line, target));
						Thread.Sleep(600);
					}
				}
			}
		}

		if (!anyText) {
			// If they typed a command label, run `!cmdinfo`.
			if (!await showCommandInfo(target, user, topic))
				Bot.Say(user.Client, user.Nickname, $"I have no information on that topic. Use {Colours.Bold}{Bot.ReplaceCommands("!cmdlist", target)}{Colours.Bold} for a list of commands.");
		}
	}

	private async Task<bool> showCommandInfo(IrcMessageTarget target, IrcUser user, string message) {
		if (!Bot.IsCommand(target, message, false, out var pluginKey, out var label, out var prefix, out var parameters)) return false;

		var result = await Bot.GetCommand(user, target, pluginKey, label, parameters);
		if (result == null) return false;
		var command = result.Value.command;

		// Check for permissions.
		var attribute = command.Attribute;
		string permission;
		if (attribute.Permission == null)
			permission = null;
		else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
			permission = result.Value.plugin.Key + attribute.Permission;
		else
			permission = attribute.Permission;

		if (permission != null && !await Bot.CheckPermissionAsync(user, permission)) {
			if (attribute.NoPermissionsMessage != null) Bot.Say(user.Client, user.Nickname, attribute.NoPermissionsMessage);
			return true;
		}

		Bot.Say(user.Client, user.Nickname, Colours.Bold + "Aliases:" + Colours.Bold + " " + string.Join(" ", command.Attribute.Names));
#if (DEBUG)
		Bot.Say(user.Client, user.Nickname, Colours.Bold + "Priority:" + Colours.Bold + " " + command.Attribute.PriorityHandler.Invoke(new CommandEventArgs(target.Client, target, user, new string[0])));
#endif
		Bot.Say(user.Client, user.Nickname, Colours.Bold + "Syntax:" + Colours.Bold + " " + command.Attribute.Syntax);
		Bot.Say(user.Client, user.Nickname, command.Attribute.Description);

		return true;
	}

	[Command("cmdlist", 0, 1, "cmdlist [plugin]", "Returns a list of my commands.")]
	public async void CommandCommandList(object? sender, CommandEventArgs e) {
		StringBuilder generalBuilder = new StringBuilder("\u0002General commands:\u0002 ");
		StringBuilder channelBuilder = new StringBuilder(string.Format("\u0002Commands for {0}:\u0002 ", e.Target));
		int channelMinLength = channelBuilder.Length;

		foreach (var pluginEntry in Bot.Plugins) {
			List<string> commands = new List<string>(16);

			bool isGeneral = false; bool found = false;
			foreach (string channel in pluginEntry.Obj.Channels) {
				string[] fields = channel.Split(new char[] { '/' }, 2);
				string channelSub;
				if (fields.Length == 1) channelSub = fields[0];
				else {
					if (fields[0] != "*" && !fields[0].Equals(e.Client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) && !fields[0].Equals(e.Client.Address, StringComparison.OrdinalIgnoreCase))
						continue;
					channelSub = fields[1];
				}
				if (channelSub == "*" || channelSub == "*#") { found = true; isGeneral = true; } else if (e.Client.CaseMappingComparer.Equals(channelSub, e.Target?.Target)) { found = true; } else continue;
			}
			if (!found) continue;

			foreach (MethodInfo method in pluginEntry.Obj.GetType().GetMethods()) {
				foreach (CommandAttribute attribute in method.GetCustomAttributes(typeof(CommandAttribute), true)) {
					// Check the scope.
					if ((attribute.Scope & CommandScope.PM) == 0 && !(e.Target is IrcChannel)) continue;
					if ((attribute.Scope & CommandScope.Channel) == 0 && e.Target is IrcChannel) continue;

					// Check for permissions.
					string permission;
					if (attribute.Permission == null)
						permission = null;
					else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
						permission = pluginEntry.Key + attribute.Permission;
					else
						permission = attribute.Permission;

					if (permission != null && !await Bot.CheckPermissionAsync(e.Sender, permission)) continue;

					// Add the command.
					commands.Add(attribute.Names[0]);
				}
			}

			if (commands.Count != 0) {
				commands.Sort();

				StringBuilder builder = isGeneral ? generalBuilder : channelBuilder;
				builder.AppendFormat(" \u00034[{0}]\u000F ", pluginEntry.Key);
				builder.Append(string.Join(" ", commands));
			}
		}

		if (generalBuilder.Length > "\u0002General commands:\u0002 ".Length)
			e.Whisper(generalBuilder.ToString());
		if (channelBuilder.Length > channelMinLength)
			e.Whisper(channelBuilder.ToString());
	}

	[Command("cmdinfo", 1, 1, "cmdinfo <command>", "Returns information on a command.")]
	public async void CommandCommandInfo(object? sender, CommandEventArgs e) {
		if (!await showCommandInfo(e.Target, e.Sender, e.Parameters[0])) {
			e.Whisper($"I don't recognise that command. Use {Colours.Bold}{Bot.ReplaceCommands("!cmdlist", e.Target)}{Colours.Bold} for a list of commands.");
		}
	}
}
