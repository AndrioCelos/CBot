using System;
using System.Linq;
using CBot;
using AnIRC;

namespace BotControl {
    [ApiVersion(3, 6)]
    public class BotControlPlugin : Plugin {
        public override string Name => "Bot Control";

        [Command("connect", 0, 1, "connect [server]", "Connects to a server, or, with no parameter, lists all servers I'm on.",
            "me.connect", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandConnect(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 0) {
                e.Reply("I'm connected to the following servers:");
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    switch (client.State) {
                        case IrcClientState.Disconnected:
                            e.Reply(string.Format("{0} - \u00034offline\u000F.", client.NetworkName));
                            break;
                        case IrcClientState.Connecting:
                            e.Reply(string.Format("{0} - \u00038connecting\u000F.", client.NetworkName));
                            break;
                        case IrcClientState.SslHandshaking:
                            e.Reply(string.Format("{0} - \u00038establishing TLS connection\u000F.", client.NetworkName));
                            break;
                        case IrcClientState.SaslAuthenticating:
                            e.Reply(string.Format("{0} - \u00038authenticating\u000F.", client.NetworkName));
                            break;
                        case IrcClientState.Registering:
                            e.Reply(string.Format("{0} - \u00038logging in\u000F.", client.NetworkName));
                            break;
                        case IrcClientState.Online:
                            if (client.Channels.Count > 1)
                                e.Reply(string.Format("{0} - \u00039online\u000F; on channels \u0002{1}\u000F.", client.NetworkName, string.Join("\u000F, \u0002", client.Channels.Select(c => c.Name))));
                            else if (client.Channels.Count == 1)
                                e.Reply(string.Format("{0} - \u00039online\u000F; on channel \u0002{1}\u000F.", client.NetworkName, client.Channels.First().Name));
                            else
                                e.Reply(string.Format("{0} - \u00039online\u000F.", client.NetworkName));
                            break;
                        default:
                            e.Reply(string.Format("{0} - {1}.", client.NetworkName, client.State));
                            break;
                    }
                }
            } else {
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName != null && client.Extensions.NetworkName.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase))) {
                        if (client.State != IrcClientState.Disconnected)
                            e.Reply(string.Format("I'm already connected to \u0002{0}\u000F.", client.Address));
                        else {
                            e.Reply(string.Format("Reconnecting to \u0002{0}\u000F.", client.Address));
                            client.Connect(clientEntry.Address, clientEntry.Port);
                        }
                        return;
                    }
                }

                e.Reply(string.Format("Connecting to \u0002{0}\u000F.", e.Parameters[0]));

                var network = new ClientEntry(e.Parameters[0]) {
                    Address   = e.Parameters[0],
                    Port      = 6667,
                    Nicknames = new[] { e.Client.Me.Nickname },
                    Ident     = e.Client.Me.Ident,
                    FullName  = e.Client.Me.FullName
                };
                Bot.AddNetwork(network);
                network.Connect();
            }
        }

        [Command("join", 1, 2, "join [server address] <channel>", "Instructs me to join a channel on IRC.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandJoin(object sender, CommandEventArgs e) {
            IrcClient targetConnection; string targetChannel;

            if (e.Parameters.Length == 2) {
                targetConnection = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetChannel = e.Parameters[1];
            } else {
                targetConnection = e.Client;
                targetChannel = e.Parameters[0];
            }

            if (targetConnection.State == IrcClientState.Disconnected) {
                e.Reply(string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (targetConnection.State != IrcClientState.Online) {
                e.Reply(string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (targetConnection.Channels.Contains(targetChannel))
                e.Reply("I'm already on that channel. ^_^");
            else
                e.Reply(string.Format("Attempting to join \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));

            targetConnection.Send("JOIN {0}", targetChannel);
        }

        [Command("part", 1, 3, "part [server address] <channel> [message]", "Instructs me to leave a channel on IRC.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandPart(object sender, CommandEventArgs e) {
            IrcClient targetConnection; string targetAddress; string targetChannel; string message;

            if (e.Parameters.Length == 3) {
                targetAddress = e.Parameters[0];
                targetChannel = e.Parameters[1];
                message = e.Parameters[2];
            } else if (e.Parameters.Length == 2) {
                if (((IrcClient) sender).IsChannel(e.Parameters[0])) {
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
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IrcClientState.Disconnected) {
                e.Reply(string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            if (targetConnection.State != IrcClientState.Online) {
                e.Reply(string.Format("I'm not yet logged in to \u0002{0}\u0002.", targetConnection.Address));
                return;
            }
            if (!targetConnection.Channels.Contains(targetChannel))
                e.Reply("I'm not on that channel.");
            else
                e.Reply(string.Format("Leaving \u0002{1}\u0002 on \u0002{0}\u0002.", targetConnection.Address, targetChannel));

            if (message == null)
                targetConnection.Send("PART {0}", targetChannel);
            else
                targetConnection.Send("PART {0} :{1}", targetChannel, message);
        }

        [Command("quit", 1, 2, "quit [server address] [message]", "Instructs me to quit an IRC server.",
            "me.ircsend", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandQuit(object sender, CommandEventArgs e) {
            IrcClient targetConnection; string targetAddress; string message;

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
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IrcClientState.Disconnected) {
                e.Reply(string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            e.Reply(string.Format("Quitting \u0002{0}\u0002.", targetConnection.Address));
            if (targetConnection.State != IrcClientState.Online) {
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
            IrcClient targetConnection; string targetAddress; string message;

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
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IrcClientState.Disconnected) {
                e.Reply(string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }
            e.Reply(string.Format("Disconnecting from \u0002{0}\u0002.", targetConnection.Address));
            if (targetConnection.State == IrcClientState.Online) {
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
            IrcClient targetConnection; string targetAddress; string command;

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
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetConnection = client;
                        break;
                    }
                }
                if (targetConnection == null) {
                    e.Reply(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
            }

            if (targetConnection.State == IrcClientState.Disconnected) {
                e.Reply(string.Format("My connection to \u0002{0}\u0002 is currently down.", targetConnection.Address));
                return;
            }

            e.Whisper("Acknowledged.");
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
            e.Whisper("Goodbye, {0}.", e.Sender.Nickname);
            foreach (ClientEntry clientEntry in Bot.Clients) {
                IrcClient client = clientEntry.Client;
                if (client.State >= IrcClientState.Registering)
                    client.Send("QUIT :{0}", message);
            }
            System.Threading.Thread.Sleep(2000);
            Environment.Exit(0);
        }

        [Command("reload", 0, 1, "Reloads configuration files.", "reload config|plugins|users",
            "me.reload", CommandScope.Channel | CommandScope.PM | CommandScope.Global)]
        public void CommandReload(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 0 || e.Parameters[0].Equals("config", StringComparison.InvariantCultureIgnoreCase)) {
                e.Reply("Reloading configuration.");
                Bot.LoadConfig();
            } else if (e.Parameters[0].Equals("plugins", StringComparison.InvariantCultureIgnoreCase)) {
                e.Reply("Reloading plugins.");
                Bot.LoadPluginConfig();
            } else if (e.Parameters[0].Equals("users", StringComparison.InvariantCultureIgnoreCase)) {
                e.Reply("Reloading users.");
                Bot.LoadUsers();
            } else {
                e.Reply($"I don't recognise '{e.Parameters[0]}'. Say 'config', 'plugins' or 'users'.");
            }
        }

#if (DEBUG)
        [Command("whois", 1, 1, "whois <nickname>", "Tests asynchronous commands.", ".debug")]
        public async void CommandWhois(object sender, CommandEventArgs e) {
			IrcUser user;
            if (e.Client.Users.TryGetValue(e.Parameters[0], out user)) {
                await user.GetAccountAsync();
                e.Reply(user.Account ?? "0");
            }
        }

        [Command("names", 1, 2, "names [server] <channel>", "Lists users on a channel.", ".debug")]
        public void CommandNames(object sender, CommandEventArgs e) {
            IrcClient targetClient; string targetChannel;

            if (e.Parameters.Length == 2) {
                targetClient = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetClient = client;
                        break;
                    }
                }
                if (targetClient == null) {
                    e.Fail(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetChannel = e.Parameters[1];
            } else {
                targetClient = e.Client;
                targetChannel = e.Parameters[0];
            }

            e.Whisper(string.Join(" ", targetClient.Channels[targetChannel].Users.Select(user => Colours.Gray + user.Status.GetPrefixes() + Colours.Reset + user.Nickname)));
        }

        [Command("who", 1, 2, "who [server] <nickname>", "Returns a user's hostmask.", ".debug")]
        public void CommandWho(object sender, CommandEventArgs e) {
            IrcClient targetClient; string targetNickname;

            if (e.Parameters.Length == 2) {
                targetClient = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetClient = client;
                        break;
                    }
                }
                if (targetClient == null) {
                    e.Fail(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetNickname = e.Parameters[1];
            } else {
                targetClient = e.Client;
                targetNickname = e.Parameters[0];
            }

            e.Whisper(string.Join(" ", targetClient.Users[targetNickname].ToString()));
        }

        [Command("channels", 1, 2, "channels [server] <nickname>", "Lists channels a user is on.", ".debug")]
        public void CommandChannels(object sender, CommandEventArgs e) {
            IrcClient targetClient; string targetNickname;

            if (e.Parameters.Length == 2) {
                targetClient = null;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase) || (client.Extensions.NetworkName ?? "").Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                        targetClient = client;
                        break;
                    }
                }
                if (targetClient == null) {
                    e.Fail(string.Format("I'm not connected to \u0002{0}\u0002.", e.Parameters[0]));
                    return;
                }
                targetNickname = e.Parameters[1];
            } else {
                targetClient = e.Client;
                targetNickname = e.Parameters[0];
            }

            e.Whisper(string.Join(" ", targetClient.Users[targetNickname].Channels.Select(channel => channel.Users[targetNickname].Status.GetPrefixes() + channel.Name)));
        }


#endif
    }
}
