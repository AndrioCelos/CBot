using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CBot;
using IRC;

namespace CommandManager {
    [ApiVersion(3, 3)]
    public class CommandManagerPlugin : Plugin {
        private Dictionary<string, Tuple<string, DateTime>> commandListCache = new Dictionary<string, Tuple<string, DateTime>>();

        public override string Name => "Command Manager";

        [Command("cmdlist", 0, 1, "cmdlist [plugin]", "Returns a list of my commands.",
            null, CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
        public void CommandCommandList(object sender, CommandEventArgs e) {
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
                    if (channelSub == "*" || channelSub == "*#") { found = true; isGeneral = true; }
                    else if (e.Client.CaseMappingComparer.Equals(channelSub, e.Target?.Target)) { found = true; } else continue;
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

                        if (permission != null && !Bot.UserHasPermission(e.Sender, permission)) continue;

                        // Add the command.
                        commands.Add(attribute.Names[0]);
                    }
                }

                if (commands.Count != 0) {
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

        [Command("cmdinfo", 1, 1, "cmdinfo <command>", "Returns information on a command.",
            null, CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
        public async void CommandCommandInfo(object sender, CommandEventArgs e) {
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
                    if (!Bot.UserHasPermission(e.Sender, permission)) {
                        // If the user's account name is unknown, they might actually have permission.
                        // We'll need to send a WHOIS request asynchronously to check for this, though.
                        await e.Sender.GetAccountAsync();
                        if (e.Sender.Account == null) await e.Sender.GetAccountAsync();
                        if (e.Sender.Account == null || !Bot.UserHasPermission(e.Sender, permission)) {
                            if (command.Attribute.NoPermissionsMessage != null)
                                e.Whisper(command.Attribute.NoPermissionsMessage);
                            return;
                        }
                    }
                }

                e.Whisper(Colours.Bold + "Aliases:" + Colours.Bold + " " + string.Join(" ", command.Attribute.Names));
                e.Whisper(Colours.Bold + "Syntax:" + Colours.Bold + " " + command.Attribute.Syntax);
                e.Whisper(command.Attribute.Description);

                return;
            }
        }

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
                    if (!Bot.UserHasPermission(e.Sender, permission)) {
                        // If the user's account name is unknown, they might actually have permission.
                        // We'll need to send a WHOIS request asynchronously to check for this, though.
                        if (e.Sender.Account == null) await e.Sender.GetAccountAsync();
                        if (e.Sender.Account == null || !Bot.UserHasPermission(e.Sender, permission)) {
                            if (command.Attribute.NoPermissionsMessage != null)
                                e.Whisper(command.Attribute.NoPermissionsMessage);
                            return;
                        }
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
                    if (!Bot.UserHasPermission(e.Sender, permission)) {
                        // If the user's account name is unknown, they might actually have permission.
                        // We'll need to send a WHOIS request asynchronously to check for this, though.
                        if (e.Sender.Account == null) await e.Sender.GetAccountAsync();
                        if (e.Sender.Account == null || !Bot.UserHasPermission(e.Sender, permission)) {
                            if (command.Attribute.NoPermissionsMessage != null)
                                e.Whisper(command.Attribute.NoPermissionsMessage);
                            return;
                        }
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
                    if (!Bot.UserHasPermission(e.Sender, permission)) {
                        // If the user's account name is unknown, they might actually have permission.
                        // We'll need to send a WHOIS request asynchronously to check for this, though.
                        if (e.Sender.Account == null) await e.Sender.GetAccountAsync();
                        if (e.Sender.Account == null || !Bot.UserHasPermission(e.Sender, permission)) {
                            if (command.Attribute.NoPermissionsMessage != null)
                                e.Whisper(command.Attribute.NoPermissionsMessage);
                            return;
                        }
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

    }
}
