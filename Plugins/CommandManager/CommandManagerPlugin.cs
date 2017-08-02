using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CBot;
using AnIRC;

namespace CommandManager {
    [ApiVersion(3, 6)]
    public class CommandManagerPlugin : Plugin {
        private Dictionary<string, Tuple<string, DateTime>> commandListCache = new Dictionary<string, Tuple<string, DateTime>>();

        public override string Name => "Command Manager";

        [Command("addalias", 2, 2, "addalias <alias> <command>", "Adds an alias to a command.",
            ".addalias", CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
        public async void CommandAddAlias(object sender, CommandEventArgs e) {
            string alias = e.Parameters[1];
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

        [Command("delalias", 2, 2, "delalias <alias>", "Removes an alias from a command.",
            ".delalias", CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
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
            ".addtrigger", CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
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
