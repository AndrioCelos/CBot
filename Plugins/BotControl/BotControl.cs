using System;
using System.Linq;

using CBot;
using IRC;

namespace BotControl {
    [APIVersion(3, 2)]
    public class BotControlPlugin : Plugin {
        public override string Name {
            get {
                return "Bot Control";
            }
        }

        [Command("connect", 0, 1, "connect [server]", "Connects to a server, or, with no parameter, lists all servers I'm on.",
            "me.connect", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandConnect(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 0) {
                Bot.Say(e.Client, e.Channel, "I'm connected to the following servers:");
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    switch (client.State) {
                        case IRCClientState.Disconnected:
                            Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00034offline\u000F.", client.Address));
                            break;
                        case IRCClientState.Connecting:
                            Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00038connecting\u000F.", client.Address));
                            break;
                        case IRCClientState.SSLHandshaking:
                            Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00038establishing TLS connection\u000F.", client.Address));
                            break;
                        case IRCClientState.SASLAuthenticating:
                            Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00038authenticating\u000F.", client.Address));
                            break;
                        case IRCClientState.Registering:
                            Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00038logging in\u000F.", client.Address));
                            break;
                        case IRCClientState.Online:
                            if (client.Channels.Count > 1)
                                Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00039online\u000F; on channels \u0002{1}\u000F.", client.Address, string.Join("\u000F, \u0002", client.Channels.Select(c => c.Name))));
                            else if (client.Channels.Count == 1)
                                Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00039online\u000F; on channel \u0002{1}\u000F.", client.Address, client.Channels[0].Name));
                            else
                                Bot.Say(e.Client, e.Channel, string.Format("{0} - \u00039online\u000F.", client.Address));
                            break;
                    }
                }
            } else {
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName != null && client.Extensions.NetworkName.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase))) {
                        if (client.State != IRCClientState.Disconnected)
                            Bot.Say(e.Client, e.Channel, string.Format("I'm already connected to \u0002{0}\u000F.", client.Address));
                        else {
                            Bot.Say(e.Client, e.Channel, string.Format("Reconnecting to \u0002{0}\u000F.", client.Address));
                            client.Connect(clientEntry.Address, clientEntry.Port);
                        }
                        return;
                    }
                }
                Bot.Say(e.Client, e.Channel, string.Format("Connecting to \u0002{0}\u000F.", e.Parameters[0]));
                ClientEntry newClient = Bot.NewClient(e.Parameters[0], e.Parameters[0], 6667, new string[] { e.Client.Me.Nickname }, e.Client.Me.Ident, e.Client.Me.FullName);
                newClient.Client.Connect(e.Parameters[0], 6667);
            }
        }

        [Command("join", 1, 2, "join [server address] <channel>", "Instructs me to join a channel on IRC.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandJoin(object sender, CommandEventArgs e) {
            IRCClient targetConnection; string targetChannel;

            if (e.Parameters.Length == 2) {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    Bot.Say(e.Client, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetChannel = e.Parameters[1];
            } else {
                targetConnection = e.Client;
                targetChannel = e.Parameters[0];
            }

            if (targetConnection.State == IRCClientState.Disconnected) {
                Bot.Say(e.Client, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (targetConnection.State != IRCClientState.Online) {
                Bot.Say(e.Client, e.Channel, string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (targetConnection.Channels.Contains(targetChannel))
                Bot.Say(e.Client, e.Channel, "I'm already on that channel. ^_^");
            else
                Bot.Say(e.Client, e.Channel, string.Format("Attempting to join \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));

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
                if (((IRCClient) sender).IsChannel(e.Parameters[0])) {
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
                targetConnection = e.Client;
            else {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    Bot.Say(e.Client, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IRCClientState.Disconnected) {
                Bot.Say(e.Client, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (targetConnection.State != IRCClientState.Online) {
                Bot.Say(e.Client, e.Channel, string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (!targetConnection.Channels.Contains(targetChannel))
                Bot.Say(e.Client, e.Channel, "I'm not on that channel.");
            else
                Bot.Say(e.Client, e.Channel, string.Format("Leaving \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));

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
                targetConnection = e.Client;
            else {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    Bot.Say(e.Client, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IRCClientState.Disconnected) {
                Bot.Say(e.Client, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            Bot.Say(e.Client, e.Channel, string.Format("Quitting \u0002{0}\u0002.", targetConnection.Address));
            if (targetConnection.State != IRCClientState.Online) {
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
                targetConnection = e.Client;
            else {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    Bot.Say(e.Client, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IRCClientState.Disconnected) {
                Bot.Say(e.Client, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            Bot.Say(e.Client, e.Channel, string.Format("Disconnecting from \u0002{0}\u0002.", targetConnection.Address));
            if (targetConnection.State == IRCClientState.Online) {
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
                targetConnection = e.Client;
            else {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IRCClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    Bot.Say(e.Client, e.Channel, string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IRCClientState.Disconnected) {
                Bot.Say(e.Client, e.Channel, string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }

            Bot.Say(e.Client, e.Sender.Nickname, "Acknowledged.");
            targetConnection.Send(command);
        }

        [Command("die", 1, 1, "die [message]", "Shuts me down",
            "me.die", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandDie(object sender, CommandEventArgs e) {
            string message;

            if (e.Parameters.Length == 1) {
                message = e.Parameters[0];
            } else {
                message = "Shutting down";
            }
            Bot.Say(e.Client, e.Sender.Nickname, "Goodbye, {0}.", e.Sender.Nickname);
            foreach (ClientEntry clientEntry in Bot.Clients) {
                IRCClient client = clientEntry.Client;
                if (client.State >= IRCClientState.Registering)
                    client.Send("QUIT :{0}", message);
            }
            System.Threading.Thread.Sleep(2000);
            System.Environment.Exit(0);
        }
    }
}
