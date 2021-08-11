using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CBot;
using AnIRC;
using System.Threading.Tasks;

namespace CommandManager {
	public struct CommandSuppression {
		internal Command FakeCommand;
	}

	[ApiVersion(4, 0)]
	public class CommandManagerPlugin : Plugin {
		private Dictionary<string, Tuple<string, DateTime>> commandListCache = new Dictionary<string, Tuple<string, DateTime>>();

		private Dictionary<string, CommandSuppression> commandSuppressions = new Dictionary<string, CommandSuppression>(StringComparer.InvariantCultureIgnoreCase);

		public override string Name => "Command Manager";

		public override void Initialize() {
			commandSuppressions.Add("IRCHighway,#game,start", new CommandSuppression() { FakeCommand = GetEmptyCommand(Bot.Plugins.First(p => p.Key == "UNO").Obj.Commands["start"]) });
		}

		public static Command GetEmptyCommand(Command command) {
			return new Command(new CommandAttribute(command.Attribute.Names.ToArray(), 0, 0, "N/A", "This command has been suppressed.") { PriorityHandler = (e => command.Attribute.PriorityHandler.Invoke(e) + 1) }, (v1, v2) => { });
		}

		public override async Task<IEnumerable<Command>> CheckCommands(IrcUser sender, IrcMessageTarget target, string label, string parameters, bool isGlobalCommand) {
			var key = $"{target.Client.NetworkName},{target.Target},{label}";
			if (commandSuppressions.TryGetValue(key, out var suppression)) {
				return new[] { suppression.FakeCommand };
			}
			return await base.CheckCommands(sender, target, label, parameters, isGlobalCommand);
		}

		[Command("addalias", 2, 2, "addalias <alias> <command>", "Adds an alias to a command.", Permission = ".addalias")]
		public async void CommandAddAlias(object sender, CommandEventArgs e) {
			string alias = e.Parameters[1];
			Command command = (await Bot.GetCommand(e.Sender, e.Target, null, alias, null)).Value.command;
			foreach (var pluginEntry in Bot.Plugins) {
				if (!pluginEntry.Obj.Commands.TryGetValue(alias, out command)) continue;

				// Check the scope.
				if ((command.Attribute.Scope & CommandScope.PM) == 0 && !(e.Target is IrcChannel)) continue;
				if ((command.Attribute.Scope & CommandScope.Channel) == 0 && e.Target is IrcChannel) continue;

				// Check for permissions.
				string permission;
				if (command.Attribute.Permission == null)
					permission = null;
				else if (command.Attribute.Permission != "" && command.Attribute.Permission.StartsWith("."))
					permission = this.Key + command.Attribute.Permission;
				else
					permission = command.Attribute.Permission;

				if (permission != null) {
					if (!await Bot.CheckPermissionAsync(e.Sender, permission)) {
						if (command.Attribute.NoPermissionsMessage != null)
							e.Whisper(command.Attribute.NoPermissionsMessage);
						return;
					}
				}

				if (pluginEntry.Obj.Commands.ContainsKey(e.Parameters[0])) {
					e.Reply("A command with that name is already defined.");
					return;
				}

				pluginEntry.Obj.Commands.Add(e.Parameters[0], command);
				command.Attribute.Names.Add(e.Parameters[0]);

				e.Reply("Added an alias successfully.");

				return;
			}
		}

		[Command("delalias", 2, 2, "delalias <alias>", "Removes an alias from a command.", Permission = ".delalias")]
		public async void CommandDeleteAlias(object sender, CommandEventArgs e) {
			string alias = e.Parameters[0];
			Command command;
			foreach (var pluginEntry in Bot.Plugins) {
				if (!pluginEntry.Obj.Commands.TryGetValue(alias, out command)) continue;

				// Check the scope.
				if ((command.Attribute.Scope & CommandScope.PM) == 0 && !(e.Target is IrcChannel)) continue;
				if ((command.Attribute.Scope & CommandScope.Channel) == 0 && e.Target is IrcChannel) continue;

				// Check for permissions.
				string permission;
				if (command.Attribute.Permission == null)
					permission = null;
				else if (command.Attribute.Permission != "" && command.Attribute.Permission.StartsWith("."))
					permission = this.Key + command.Attribute.Permission;
				else
					permission = command.Attribute.Permission;

				if (permission != null) {
					if (!await Bot.CheckPermissionAsync(e.Sender, permission)) {
						if (command.Attribute.NoPermissionsMessage != null)
							e.Whisper(command.Attribute.NoPermissionsMessage);
						return;
					}
				}

				if (command.Attribute.Names.Count == 1) {
					e.Fail("That's the only alias, and cannot be deleted.");
					return;
				}

				pluginEntry.Obj.Commands.Remove(e.Parameters[0]);
				command.Attribute.Names.RemoveAll(name => name.Equals(e.Parameters[0], StringComparison.CurrentCultureIgnoreCase));
				e.Reply("Deleted the alias successfully.");

				return;
			}
		}

		[Command("addtrigger", 1, 1, "addtrigger /<regex>/ <command>", "Sets up a trigger that runs a command.",
			Permission = ".addtrigger")]
		public async void CommandAddTrigger(object sender, CommandEventArgs e) {
			var match = Regex.Match(e.Parameters[0], @"(?:/(\\.|[^\/])*/)?(\S+)(?>\s+)(.+)");
			if (!match.Success) return;

			string regex, switches;
			if (match.Groups[1].Success) {
				regex = match.Groups[1].Value;
				switches = match.Groups[2].Value;
			} else {
				regex = match.Groups[2].Value;
				switches = "";
			}

			var fields = match.Groups[3].Value.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
			string alias = fields[0];
			Command command;
			foreach (var pluginEntry in Bot.Plugins) {
				if (!pluginEntry.Obj.Commands.TryGetValue(alias, out command)) continue;

				// Check the scope.
				if ((command.Attribute.Scope & CommandScope.PM) == 0 && !(e.Target is IrcChannel)) continue;
				if ((command.Attribute.Scope & CommandScope.Channel) == 0 && e.Target is IrcChannel) continue;

				// Check for permissions.
				string permission;
				if (command.Attribute.Permission == null)
					permission = null;
				else if (command.Attribute.Permission != "" && command.Attribute.Permission.StartsWith("."))
					permission = this.Key + command.Attribute.Permission;
				else
					permission = command.Attribute.Permission;

				if (permission != null) {
					if (!await Bot.CheckPermissionAsync(e.Sender, permission)) {
						if (command.Attribute.NoPermissionsMessage != null)
							e.Whisper(command.Attribute.NoPermissionsMessage);
						return;
					}
				}

				pluginEntry.Obj.Triggers.Add(new Trigger(new TriggerAttribute(regex), (_sender, _e) => command.Handler.Invoke(_sender, new CommandEventArgs(_e.Client, _e.Target, _e.Sender, _e.Match.Groups.Cast<Group>().Select(g => g.Value).ToArray()))));

				e.Reply("Added a trigger successfully.");

				return;
			}
		}

	}
}
