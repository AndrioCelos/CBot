using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CBot;
using IRC;

namespace BotControl {
    [APIVersion(3, 0)]
    public class BotControlPlugin : Plugin {
        [Command("connect", 0, 1, "connect [server]", "Connects to a server, or, with no parameter, lists all servers I'm on.",
            "me.connect", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandConnect(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 0) {
                this.Say(e.Connection, e.Channel, "I'm connected to the following servers:");
                foreach (IRCClient client in Bot.Connections) {
                    if (client.IsConnected) {
                        if (client.IsRegistered) {
                            if (client.Channels.Count > 1)
                                this.Say(e.Connection, e.Channel, string.Format("{0} - \u00039online\u000F; on channels \u0002{1}\u000F.", client.Address, string.Join("\u000F, \u0002", client.Channels.Select(c => c.Name))));
                            else if (client.Channels.Count == 1)
                                this.Say(e.Connection, e.Channel, string.Format("{0} - \u00039online\u000F; on channel \u0002{1}\u000F.", client.Address, client.Channels[0].Name));
                            else
                                this.Say(e.Connection, e.Channel, string.Format("{0} - \u00039online\u000F.", client.Address));
                        } else
                            this.Say(e.Connection, e.Channel, string.Format("{0} - \u0003logging in\u000F.", client.Address));
                    } else
                        this.Say(e.Connection, e.Channel, string.Format("{0} - \u00034offline\u000F.", client.Address));
                }
            } else {
                foreach (IRCClient client in Bot.Connections) {
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || client.NetworkName.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        if (client.IsConnected)
                            this.Say(e.Connection, e.Channel, string.Format("I'm already connected to \u0002{0}\u000F.", client.Address));
                        else {
                            this.Say(e.Connection, e.Channel, string.Format("Reconnecting to \u0002{0}\u000F.", client.Address));
                            client.Connect();
                        }
                        return;
                    }
                }
                this.Say(e.Connection, e.Channel, string.Format("Connecting to \u0002{0}\u000F.", e.Parameters[0]));
                IRCClient newClient = Bot.NewClient(e.Parameters[0], 6667, e.Connection.Nicknames, e.Connection.Username, e.Connection.FullName);
                newClient.Connect();
            }
        }

        [Command("join", 1, 2, "join [server address] <channel>", "Instructs me to join a channel on IRC.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandJoin(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetChannel;

            if (e.Parameters.Length == 2) {
                targetConnection = null;
                foreach (IRCClient connection in Bot.Connections) {
                    if (connection.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (connection.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = connection;
                        break;
                    }
                }
                if (targetConnection == null) {
                    this.Say(e.Connection, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetChannel = e.Parameters[1];
            } else {
                targetConnection = e.Connection;
                targetChannel = e.Parameters[0];
            }

            if (!targetConnection.IsConnected) {
                this.Say(e.Connection, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (!targetConnection.IsRegistered) {
                this.Say(e.Connection, e.Channel, string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (targetConnection.Channels.Contains(targetChannel)) {
                this.Say(e.Connection, e.Channel, "I'm already on that channel. ^_^");
                return;
            }
            this.Say(e.Connection, e.Channel, string.Format("Attempting to join \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));
            targetConnection.Send("JOIN {0}", targetChannel);
        }

        [Command("part", 1, 3, "part [server address] <channel> [message]", "Instructs me to leave a channel on IRC.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandPart(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetAddress; string targetChannel; string message;

            if (e.Parameters.Length == 3) {
                targetAddress = e.Parameters[0];
                targetChannel = e.Parameters[1];
                message = e.Parameters[2];
            } else if (e.Parameters.Length == 2) {
                if (e.Parameters[0].StartsWith("#")) {
                    targetAddress = null;
                    targetChannel = e.Parameters[0];
                    message = e.Parameters[1];
                } else {
                    targetAddress = e.Parameters[0];
                    targetChannel = e.Parameters[1];
                    message = null;
                }
                targetChannel = e.Parameters[1];
            } else {
                targetAddress = null;
                targetChannel = e.Parameters[0];
                message = null;
            }

            if (targetAddress == null || targetAddress == ".")
                targetConnection = e.Connection;
            else {
                targetConnection = null;
                foreach (IRCClient connection in Bot.Connections) {
                    if (connection.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (connection.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = connection;
                        break;
                    }
                }
                if (targetConnection == null) {
                    this.Say(e.Connection, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (!targetConnection.IsConnected) {
                this.Say(e.Connection, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (!targetConnection.IsRegistered) {
                this.Say(e.Connection, e.Channel, string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (!targetConnection.Channels.Contains(targetChannel)) {
                this.Say(e.Connection, e.Channel, "I'm not on that channel.");
                return;
            }

            this.Say(e.Connection, e.Channel, string.Format("Leaving \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));
            if (message == null)
                targetConnection.Send("PART {0}", targetChannel);
            else
                targetConnection.Send("PART {0} :{1}", targetChannel, message);
        }

        [Command("quit", 1, 2, "quit [server address] [message]", "Instructs me to quit an IRC server.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandQuit(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetAddress; string message;

            if (e.Parameters.Length == 2) {
                targetAddress = e.Parameters[0];
                message = e.Parameters[1];
            } else if (e.Parameters.Length == 1) {
                targetAddress = null;
                message = e.Parameters[0];
            } else {
                targetAddress = null;
                message = null;
            }

            if (targetAddress == null || targetAddress == ".")
                targetConnection = e.Connection;
            else {
                targetConnection = null;
                foreach (IRCClient connection in Bot.Connections) {
                    if (connection.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (connection.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = connection;
                        break;
                    }
                }
                if (targetConnection == null) {
                    this.Say(e.Connection, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (!targetConnection.IsConnected) {
                this.Say(e.Connection, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            this.Say(e.Connection, e.Channel, string.Format("Quitting \u0002{0}\u0002.", targetConnection.Address));
            if (!targetConnection.IsRegistered) {
                targetConnection.Disconnect();
            } else {
                if (message == null)
                    targetConnection.Send("QUIT");
                else
                    targetConnection.Send("QUIT :{0}", message);
            }
        }

        [Command("disconnect", 1, 2, "disconnect [server address] [message]", "Instructs me to drop my connection to an IRC server.",
    "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandDisconnect(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetAddress; string message;

            if (e.Parameters.Length == 2) {
                targetAddress = e.Parameters[0];
                message = e.Parameters[1];
            } else if (e.Parameters.Length == 1) {
                targetAddress = null;
                message = e.Parameters[0];
            } else {
                targetAddress = null;
                message = null;
            }

            if (targetAddress == null || targetAddress == ".")
                targetConnection = e.Connection;
            else {
                targetConnection = null;
                foreach (IRCClient connection in Bot.Connections) {
                    if (connection.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (connection.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = connection;
                        break;
                    }
                }
                if (targetConnection == null) {
                    this.Say(e.Connection, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (!targetConnection.IsConnected) {
                this.Say(e.Connection, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            this.Say(e.Connection, e.Channel, string.Format("Disconnecting from \u0002{0}\u0002.", targetConnection.Address));
            if (targetConnection.IsRegistered) {
                if (message == null)
                    targetConnection.Send("QUIT");
                else
                    targetConnection.Send("QUIT :{0}", message);
            }
            targetConnection.Disconnect();
        }

        [Command("raw", 1, 2, "raw [server] <message>", "Sends a raw command to the server.",
        "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandRaw(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetAddress; string command;

            if (e.Parameters.Length == 2) {
                targetAddress = e.Parameters[0];
                command = e.Parameters[1];
            } else {
                targetAddress = null;
                command = e.Parameters[0];
            }

            if (targetAddress == null || targetAddress == ".")
                targetConnection = e.Connection;
            else {
                targetConnection = null;
                foreach (IRCClient connection in Bot.Connections) {
                    if (connection.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (connection.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = connection;
                        break;
                    }
                }
                if (targetConnection == null) {
                    this.Say(e.Connection, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (!targetConnection.IsConnected) {
                this.Say(e.Connection, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }

            this.Say(e.Connection, e.Sender.Nickname, "Acknowledged.");
            targetConnection.Send(command);
        }
    }
}
