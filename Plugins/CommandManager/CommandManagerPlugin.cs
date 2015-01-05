using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CBot;
using IRC;

namespace CommandManager {
    [APIVersion(3, 1)]
    public class CommandManagerPlugin : Plugin {
        private Dictionary<string, Tuple<string, DateTime>> commandListCache = new Dictionary<string, Tuple<string, DateTime>>();

        [Command("cmdlist", 0, 1, "cmdlist [plugin]", "Returns a list of my commands.",
            null, CommandScope.Global | CommandScope.Channel | CommandScope.PM)]
        public void CommandCommandList(object sender, CommandEventArgs e) {
            StringBuilder generalBuilder = new StringBuilder("\u0002General commands:\u0002 ");
            StringBuilder channelBuilder = new StringBuilder(string.Format("\u0002Commands for {0}:\u0002 ", e.Channel));
            int channelMinLength = channelBuilder.Length;

            bool isChannel = e.Connection.IsChannel(e.Channel);

            foreach (KeyValuePair<string, PluginEntry> pluginEntry in Bot.Plugins) {
                List<string> commands = new List<string>(16);

                bool isGeneral = false; bool found = false;
                foreach (string channel in pluginEntry.Value.Obj.Channels) {
                    string[] fields = channel.Split(new char[] { '/' }, 2);
                    string channelSub;
                    if (fields.Length == 1) channelSub = fields[0];
                    else {
                        if (fields[0] != "*" && !fields[0].Equals(e.Connection.NetworkName, StringComparison.OrdinalIgnoreCase) && !fields[0].Equals(e.Connection.Address, StringComparison.OrdinalIgnoreCase))
                            continue;
                        channelSub = fields[1];
                    }
                    if (channelSub == "*" || channelSub == "*#") { found = true; isGeneral = true; }
                    else if (e.Connection.CaseMappingComparer.Equals(channelSub, e.Channel)) { found = true; }
                    else continue;
                }
                if (!found) continue;

                foreach (MethodInfo method in pluginEntry.Value.Obj.GetType().GetMethods()) {
                    foreach (CommandAttribute attribute in method.GetCustomAttributes(typeof(CommandAttribute), true)) {
                        // Check the scope.
                        if ((attribute.Scope & CommandScope.PM) == 0 && !isChannel) continue;
                        if ((attribute.Scope & CommandScope.Channel) == 0 && isChannel) continue;

                        // Check for permissions.
                        string permission;
                        if (attribute.Permission == null)
                            permission = null;
                        else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
                            permission = pluginEntry.Key + attribute.Permission;
                        else
                            permission = attribute.Permission;

                        if (permission != null && !Bot.UserHasPermission(e.Connection, e.Channel, e.Sender.Nickname, permission)) continue;

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

            if (generalBuilder.Length > "\u0002General commands:\u0002 ".Length) {
                int pos = 0; int pos2; int i;
                while (pos < generalBuilder.Length - 410) {
                    pos2 = pos + 410;
                    for (i = pos2 - 1; i > pos; --i) {
                        if (generalBuilder[i] == '\u0003') break;
                    }
                    if (i == pos) {
                        for (i = pos2 - 1; i > pos; --i) {
                            if (generalBuilder[i] == ' ') break;
                        }
                        if (i == pos) i = pos2 + 1;
                        else ++i;
                    }
                    Bot.Say(e.Connection, e.Sender.Nickname, generalBuilder.ToString(pos, i - pos - 1));
                    pos = i;
                }
                Bot.Say(e.Connection, e.Sender.Nickname, generalBuilder.ToString(pos, generalBuilder.Length - pos));
            }
            if (channelBuilder.Length > channelMinLength)
                Bot.Say(e.Connection, e.Sender.Nickname, channelBuilder.ToString());

        }
    }
}
