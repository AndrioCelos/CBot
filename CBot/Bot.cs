/* General to-do list:
 *   TODO: Spam proof commands.
 *   TODO: Implement JSON configuration.
 */

#pragma warning disable 4014  // Async method call without await

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using AnIRC;
using Newtonsoft.Json;

namespace CBot {
    /// <summary>
    /// The main class of CBot.
    /// </summary>
    public static class Bot {
        /// <summary>Returns the version of the bot, as returned to a CTCP VERSION request.</summary>
        public static string ClientVersion { get; private set; }
        /// <summary>Returns the version of the bot.</summary>
        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		public static Config Config = new Config();
        /// <summary>The list of IRC connections the bot has.</summary>
        public static List<ClientEntry> Clients = new List<ClientEntry>();
        /// <summary>The list of loaded plugins.</summary>
        public static PluginCollection Plugins = new PluginCollection();
		/// <summary>Acts as a staging area to compare the plugin configuration file with the currently loaded plugins.</summary>
		internal static Dictionary<string, PluginEntry> NewPlugins;
		/// <summary>The list of users who are identified.</summary>
		public static Dictionary<string, Identification> Identifications = new Dictionary<string, Identification>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The list of user accounts that are known to the bot.</summary>
        public static Dictionary<string, Account> Accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The list of default command prefixes. A command line can start with any of these if not in a channel that has its own set.</summary>
        public static string[] DefaultCommandPrefixes = new string[] { "!" };
        /// <summary>The collection of channel command prefixes. The keys are channel names in the form NetworkName/#channel, and the corresponding value is the array of command prefixes for that channel.</summary>
        public static Dictionary<string, string[]> ChannelCommandPrefixes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        internal static string[] DefaultNicknames = new string[] { "CBot" };
        internal static string DefaultIdent = "CBot";
        internal static string DefaultFullName = "CBot by Andrio Celos";
        internal static string DefaultUserInfo = "CBot by Andrio Celos";
        internal static string DefaultAvatar = null;

        internal static string ConfigPath = "Config";
        internal static string LanguagesPath = "Languages";
        internal static string PluginsPath = "plugins";
        internal static string Language = "Default";

        private static bool ConfigFileFound;
        private static bool UsersFileFound;
        private static bool PluginsFileFound;
        private static Random rng;
        private static ConsoleClient consoleClient;

        /// <summary>The minimum compatible plugin API version with this version of CBot.</summary>
        public static readonly Version MinPluginVersion = new Version(3, 7);

        /// <summary>Indicates whether there are any NickServ-based permissions.</summary>
        internal static bool commandCallbackNeeded;

        private static readonly Regex commandMaskRegex  = new Regex(@"^((?:PASS|AUTHENTICATE|OPER|DIE|RESTART) *:?)(?!\*$|\+$|PLAIN|EXTERNAL|DH-).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex commandMaskRegex2 = new Regex(@"^((?:PRIVMSG *)?(?:NICKSERV|CHANSERV|NS|CS) *:?(?:ID(?:ENTIFY)?|GHOST|REGAIN|REGISTER) *).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static event EventHandler<IrcClientEventArgs> IrcClientAdded;
        public static event EventHandler<IrcClientEventArgs> IrcClientRemoved;

        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(string channel) {
            string[] prefixes;
            if (channel == null || !Bot.ChannelCommandPrefixes.TryGetValue(channel, out prefixes))
                prefixes = Bot.DefaultCommandPrefixes;
            return prefixes;
        }
        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="channel">The channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(IrcChannel channel) {
            if (channel?.Client == null)
                return Bot.GetCommandPrefixes(channel?.Name);
            else
                return Bot.GetCommandPrefixes(channel.Client.NetworkName + "/" + channel.Name);
        }
        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="client">The IRC connection to the network on which the channel to check is.</param>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(ClientEntry client, string channel) {
            if (client == null)
                return Bot.GetCommandPrefixes(channel);
            else
                return Bot.GetCommandPrefixes(client.Name + "/" + channel);
        }
        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="client">The IRC connection to the network on which the channel to check is.</param>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(IrcClient client, string channel) {
            if (client == null) {
                return Bot.GetCommandPrefixes(channel);
            } else {
                return Bot.GetCommandPrefixes(client.NetworkName + "/" + channel);
            }
        }
		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="target">The channel or query target to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public static string[] GetCommandPrefixes(IrcMessageTarget target) {
			return Bot.GetCommandPrefixes(target.Client.NetworkName + "/" + target.Target);
		}

		/// <summary>Sets up an IRC network configuration and adds it to the list of loaded networks.</summary>
		public static void AddNetwork(ClientEntry network) {
            SetUpNetwork(network);
            IrcClientAdded?.Invoke(null, new IrcClientEventArgs(network));
            Clients.Add(network);
        }

        /// <summary>Sets up an IRC network configuration, including adding CBot's event handlers.</summary>
        public static void SetUpNetwork(ClientEntry network) {
            IrcClient newClient = new IrcClient(new IrcLocalUser((network.Nicknames ?? DefaultNicknames)[0], network.Ident ?? DefaultIdent, network.FullName ?? DefaultFullName), network.Name);
            SetUpClientEvents(newClient);
            network.Client = newClient;
        }

        public static void RemoveNetwork(ClientEntry network) {
            if (Clients.Remove(network))
                IrcClientRemoved?.Invoke(null, new IrcClientEventArgs(network));
        }

        /// <summary>Gets the IRC network a given <see cref="IrcClient"/> belongs to, or null if it is not known.</summary>
        public static ClientEntry GetClientEntry(IrcClient client) => Bot.Clients.FirstOrDefault(c => c.Client == client);

        /// <summary>Adds CBot's event handlers to an <see cref="IrcClient"/> object. This can be called by plugins creating their own <see cref="IrcClient"/> objects.</summary>
        /// <param name="newClient">The IRCClient object to add event handlers to.</param>
        public static void SetUpClientEvents(IrcClient newClient) {
            newClient.RawLineReceived += delegate(object sender, IrcLineEventArgs e) {
                ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKGREEN>>%cDKGRAY {1}%r", ((IrcClient) sender).NetworkName, e.Data);
            };
            newClient.RawLineSent += delegate(object sender, RawLineEventArgs e) {
                Match m;
                m = commandMaskRegex.Match(e.Data);
                if (!m.Success) m = commandMaskRegex2.Match(e.Data);
                if (m.Success)
                    ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}***%r", ((IrcClient) sender).NetworkName, m.Groups[1]);
                else
                    ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}%r", ((IrcClient) sender).NetworkName, e.Data);
            };

            newClient.AwayCancelled += Bot.OnAwayCancelled;
            newClient.AwayMessage += Bot.OnAwayMessage;
            newClient.AwaySet += Bot.OnAwaySet;
			newClient.CapabilitiesAdded += Bot.OnCapabilitiesAdded;
			newClient.CapabilitiesDeleted += Bot.OnCapabilitiesDeleted;
			newClient.ChannelAction += Bot.OnChannelAction;
            newClient.ChannelAdmin += Bot.OnChannelAdmin;
            newClient.ChannelBan += Bot.OnChannelBan;
            newClient.ChannelBanList += Bot.OnChannelBanList;
            newClient.ChannelBanListEnd += Bot.OnChannelBanListEnd;
            newClient.ChannelBanRemoved += Bot.OnChannelBanRemoved;
            newClient.ChannelCTCP += Bot.OnChannelCTCP;
            newClient.ChannelDeAdmin += Bot.OnChannelDeAdmin;
            newClient.ChannelDeHalfOp += Bot.OnChannelDeHalfOp;
            newClient.ChannelDeHalfVoice += Bot.OnChannelDeHalfVoice;
            newClient.ChannelDeOp += Bot.OnChannelDeOp;
            newClient.ChannelDeOwner += Bot.OnChannelDeOwner;
            newClient.ChannelDeVoice += Bot.OnChannelDeVoice;
            newClient.ChannelExempt += Bot.OnChannelExempt;
            newClient.ChannelExemptRemoved += Bot.OnChannelExemptRemoved;
            newClient.ChannelHalfOp += Bot.OnChannelHalfOp;
            newClient.ChannelHalfVoice += Bot.OnChannelHalfVoice;
            newClient.ChannelInviteExempt += Bot.OnChannelInviteExempt;
            newClient.ChannelInviteExemptList += Bot.OnChannelInviteExemptList;
            newClient.ChannelInviteExemptListEnd += Bot.OnChannelInviteExemptListEnd;
            newClient.ChannelInviteExemptRemoved += Bot.OnChannelInviteExemptRemoved;
            newClient.ChannelJoin += Bot.OnChannelJoin;
            newClient.ChannelJoinDenied += Bot.OnChannelJoinDenied;
            newClient.ChannelKeyRemoved += Bot.OnChannelKeyRemoved;
            newClient.ChannelKeySet += Bot.OnChannelKeySet;
            newClient.ChannelKick += Bot.OnChannelKick;
            newClient.ChannelLeave += Bot.OnChannelLeave;
            newClient.ChannelLimitRemoved += Bot.OnChannelLimitRemoved;
            newClient.ChannelLimitSet += Bot.OnChannelLimitSet;
            newClient.ChannelList += Bot.OnChannelList;
            newClient.ChannelListChanged += Bot.OnChannelListChanged;
            newClient.ChannelListEnd += Bot.OnChannelListEnd;
            newClient.ChannelMessage += Bot.OnChannelMessage;
            newClient.ChannelMessageDenied += Bot.OnChannelMessageDenied;
            newClient.ChannelModeChanged += Bot.OnChannelModeChanged;
            newClient.ChannelModesGet += Bot.OnChannelModesGet;
            newClient.ChannelModesSet += Bot.OnChannelModesSet;
            newClient.ChannelNotice += Bot.OnChannelNotice;
            newClient.ChannelOp += Bot.OnChannelOp;
            newClient.ChannelOwner += Bot.OnChannelOwner;
            newClient.ChannelPart += Bot.OnChannelPart;
            newClient.ChannelQuiet += Bot.OnChannelQuiet;
            newClient.ChannelQuietRemoved += Bot.OnChannelQuietRemoved;
            newClient.ChannelStatusChanged += Bot.OnChannelStatusChanged;
            newClient.ChannelTimestamp += Bot.OnChannelTimestamp;
            newClient.ChannelTopicChanged += Bot.OnChannelTopicChanged;
            newClient.ChannelTopicReceived += Bot.OnChannelTopicReceived;
            newClient.ChannelTopicStamp += Bot.OnChannelTopicStamp;
            newClient.ChannelVoice += Bot.OnChannelVoice;
            newClient.Disconnected += Bot.OnDisconnected;
            newClient.Exception += Bot.OnException;
            newClient.ExemptList += Bot.OnExemptList;
            newClient.ExemptListEnd += Bot.OnExemptListEnd;
            newClient.Invite += Bot.OnInvite;
            newClient.InviteSent += Bot.OnInviteSent;
            newClient.Killed += Bot.OnKilled;
            newClient.MOTD += Bot.OnMOTD;
            newClient.Names += Bot.OnNames;
            newClient.NamesEnd += Bot.OnNamesEnd;
            newClient.NicknameChange += Bot.OnNicknameChange;
            newClient.NicknameChangeFailed += Bot.OnNicknameChangeFailed;
            newClient.NicknameInvalid += Bot.OnNicknameInvalid;
            newClient.NicknameTaken += Bot.OnNicknameTaken;
            newClient.Pong += Bot.OnPingReply;
            newClient.PingReceived += Bot.OnPingRequest;
            newClient.PrivateAction += Bot.OnPrivateAction;
            newClient.PrivateCTCP += Bot.OnPrivateCTCP;
            newClient.PrivateMessage += Bot.OnPrivateMessage;
            newClient.PrivateNotice += Bot.OnPrivateNotice;
            newClient.RawLineReceived += Bot.OnRawLineReceived;
            newClient.RawLineSent += Bot.OnRawLineSent;
            newClient.RawLineUnhandled += Bot.OnRawLineUnhandled;
            newClient.Registered += Bot.OnRegistered;
            newClient.ServerError += Bot.OnServerError;
            newClient.ServerNotice += Bot.OnServerNotice;
            newClient.StateChanged += Bot.OnStateChanged;
            newClient.UserDisappeared += Bot.OnUserDisappeared;
            newClient.UserModesGet += Bot.OnUserModesGet;
            newClient.UserModesSet += Bot.OnUserModesSet;
            newClient.UserQuit += Bot.OnUserQuit;
            newClient.ValidateCertificate += Bot.OnValidateCertificate;
            newClient.Wallops += Bot.OnWallops;
            newClient.WhoIsAuthenticationLine += Bot.OnWhoIsAuthenticationLine;
            newClient.WhoIsChannelLine += Bot.OnWhoIsChannelLine;
            newClient.WhoIsEnd += Bot.OnWhoIsEnd;
            newClient.WhoIsHelperLine += Bot.OnWhoIsHelperLine;
            newClient.WhoIsIdleLine += Bot.OnWhoIsIdleLine;
            newClient.WhoIsNameLine += Bot.OnWhoIsNameLine;
            newClient.WhoIsOperLine += Bot.OnWhoIsOperLine;
            newClient.WhoIsRealHostLine += Bot.OnWhoIsRealHostLine;
            newClient.WhoIsServerLine += Bot.OnWhoIsServerLine;
            newClient.WhoList += Bot.OnWhoList;
            newClient.WhoWasEnd += Bot.OnWhoWasEnd;
            newClient.WhoWasNameLine += Bot.OnWhoWasNameLine;
        }

        /// <summary>The program's entry point.</summary>
        /// <returns>
        ///   0 if the program terminates normally (as a result of the die command);
        ///   2 if the program terminates because of an error during loading.
        /// </returns>
        public static int Main() {
            if (Environment.OSVersion.Platform >= PlatformID.Unix)
                Console.TreatControlCAsInput = true;  // There is a bug in Windows that occurs when this is set.

            Assembly assembly = Assembly.GetExecutingAssembly();

            string title = null; string author = null;
            foreach (object attribute in assembly.GetCustomAttributes(false)) {
                if (attribute is AssemblyTitleAttribute)
                    title = ((AssemblyTitleAttribute) attribute).Title;
                else if (attribute is AssemblyCompanyAttribute)
                    author = ((AssemblyCompanyAttribute) attribute).Company;
            }
            Bot.ClientVersion = string.Format("{0} by {1} : version {2}.{3}", title, author, Bot.Version.Major, Bot.Version.Minor, Bot.Version.Revision, Bot.Version.Build);
            Bot.rng = new Random();

            Console.Write("Loading configuration file...");
            if (File.Exists("config.json") || File.Exists("CBotConfig.ini")) {
                Bot.ConfigFileFound = true;
                try {
                    Bot.LoadConfig(false);

                    // Add the console client. (Default identity settings must be loaded before doing this.)
                    consoleClient = new ConsoleClient();
                    Bot.Clients.Add(new ClientEntry("!Console", "!Console", 0, consoleClient) { SaveToConfig = false });
                    SetUpClientEvents(consoleClient);

                    foreach (var network in Config.Networks) {
						network.SaveToConfig = true;
                        AddNetwork(network);
                    }

                    Console.WriteLine(" OK");
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(" %cREDFailed%r");
                    ConsoleUtils.WriteLine("%cREDI couldn't load the configuration file: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) return 2;
                }
            } else {
                ConsoleUtils.WriteLine(" %cBLUEconfig.json is missing.%r");
            }

            Console.Write("Loading user configuration file...");
            if (File.Exists("users.json") || File.Exists("CBotUsers.ini")) {
                Bot.UsersFileFound = true;
                try {
                    Bot.LoadUsers();
                    Console.WriteLine(" OK");
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(" %cREDFailed%r");
                    ConsoleUtils.WriteLine("%cREDI couldn't load the user configuration file: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) return 2;
                }
            } else {
                ConsoleUtils.WriteLine(" %cBLUEusers.json is missing.%r");
            }

            Console.Write("Loading plugins...");
            if (File.Exists("plugins.json") || File.Exists("CBotPlugins.ini")) {
                Bot.PluginsFileFound = true;
                Console.WriteLine();
                bool success = Bot.LoadPluginConfig();
                if (!success) {
                    Console.WriteLine();
                    ConsoleUtils.WriteLine("Some plugins failed to load.");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) return 2;
                }
            } else {
                ConsoleUtils.WriteLine(" %cBLUEplugins.json is missing.%r");
            }
            Bot.FirstRun();
            if (!Bot.PluginsFileFound) Bot.LoadPluginConfig();

            foreach (ClientEntry client in Bot.Clients) {
                try {
                    if (client.Name != "!Console")
                        ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", client.Name, client.Address, client.Port);
                    client.Connect();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", client.Name, ex.Message);
                    client.StartReconnect();
                }
            }

			if (Config.Networks.Count == 0)
                ConsoleUtils.WriteLine("%cYELLOWNo IRC networks are defined in the configuration file. Delete config.json and restart to set one up.%r");
			ConsoleUtils.WriteLine("Type 'help' to list built-in console commands.");

            while (true) {
                string input = Console.ReadLine();
                string[] fields = input.Split((char[]) null, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 0) continue;

                try {
                    if (fields[0].Equals("help", StringComparison.CurrentCultureIgnoreCase)) {
                        ConsoleUtils.WriteLine("%cWHITECBot built-in console commands:%r");
                        ConsoleUtils.WriteLine("  %cWHITEconnect <address> [[+]port] [nickname] [ident] [fullname...]%r : Connects to a new IRC network.");
                        ConsoleUtils.WriteLine("  %cWHITEdie [message...]%r : Quits all IRC networks and shuts down CBot.");
                        ConsoleUtils.WriteLine("  %cWHITEenter <message...>%r : Sends a chat message that would otherwise look like a command.");
                        ConsoleUtils.WriteLine("  %cWHITEload [key] <file path...>%r : Loads a plugin.");
                        ConsoleUtils.WriteLine("  %cWHITEreload [config|users|plugins]%r : Reloads configuration files.");
						ConsoleUtils.WriteLine("  %cWHITEsave [config|users|plugins]%r : Saves configuration files.");
                        ConsoleUtils.WriteLine("  %cWHITEsend <network> <message...>%r : Sends a raw IRC command.");
                        ConsoleUtils.WriteLine("Anything else is treated as a chat message.");
                    } else if (fields[0].Equals("connect", StringComparison.CurrentCultureIgnoreCase)) {
                        if (fields.Length >= 2) {
                            var network = new ClientEntry(fields[1]) {
                                Address   = fields[1],
                                Port      = fields.Length >= 3 ? int.Parse((fields[2].StartsWith("+") ? fields[2].Substring(1) : fields[2])) : 6667,
                                Nicknames = fields.Length >= 4 ? new[] { fields[3] } : DefaultNicknames,
                                Ident     = fields.Length >= 5 ? fields[4] : DefaultIdent,
                                FullName  = fields.Length >= 6 ? string.Join(" ", fields.Skip(5)) : DefaultFullName,
                                TLS       = fields.Length >= 3 && fields[2].StartsWith("+")
                            };
                            Bot.AddNetwork(network);
                            try {
                                ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", network.Name, network.Address, network.Port);
                                network.Connect();
                            } catch (Exception ex) {
                                ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", network.Name, ex.Message);
                                network.StartReconnect();
                            }
                        } else
                            ConsoleUtils.WriteLine("%cREDUsage: connect <address> [[+]port] [nickname] [ident] [fullname...]%r");

                    } else if (fields[0].Equals("die", StringComparison.CurrentCultureIgnoreCase)) {
                        foreach (ClientEntry _client in Bot.Clients) {
                            if (_client.Client.State >= IrcClientState.Registering)
                                _client.Client.Send("QUIT :{0}", fields.Length >= 2 ? string.Join(" ", fields.Skip(1)) : "Shutting down.");
                        }
                        Thread.Sleep(2000);
                        foreach (ClientEntry _client in Bot.Clients) {
                            if (_client.Client.State >= IrcClientState.Registering)
                                _client.Client.Disconnect();
                        }
                        return 0;

                    } else if (fields[0].Equals("enter", StringComparison.CurrentCultureIgnoreCase)) {
                        if (fields.Length >= 2) {
                            consoleClient.Put(input.Substring(6).TrimStart());
                        } else
                            ConsoleUtils.WriteLine("%cREDUsage: enter <message...>%r");

                    } else if (fields[0].Equals("load", StringComparison.CurrentCultureIgnoreCase)) {
                        if (fields.Length == 2) {
                            LoadPlugin(Path.GetFileNameWithoutExtension(fields[1]), fields[1], new[] { "*" });
                        } else if (fields.Length > 2) {
                            LoadPlugin(fields[1], string.Join(" ", fields.Skip(2)), new[] { "*" });
                        } else
                            ConsoleUtils.WriteLine("%cREDUsage: load [key] <file path...>%r");

                    } else if (fields[0].Equals("reload", StringComparison.CurrentCultureIgnoreCase)) {
						bool badSyntax = false;
						if (fields.Length == 1) {
							LoadConfig();
							LoadPluginConfig();
							LoadUsers();
						} else if (fields[1].Equals("config", StringComparison.CurrentCultureIgnoreCase)) LoadConfig();
						else if (fields[1].Equals("plugins", StringComparison.CurrentCultureIgnoreCase)) LoadPluginConfig();
						else if (fields[1].Equals("users", StringComparison.CurrentCultureIgnoreCase)) LoadUsers();
						else {
							ConsoleUtils.WriteLine("%cREDUsage: reload [config|plugins|users]%r");
							badSyntax = true;
						}
						if (!badSyntax) ConsoleUtils.WriteLine("Configuration reloaded successfully.");

					} else if (fields[0].Equals("save", StringComparison.CurrentCultureIgnoreCase)) {
						bool badSyntax = false, savePlugins = false;
						if (fields.Length == 1) {
							SaveConfig();
							savePlugins = true;
							SaveUsers();
						} else if (fields[1].Equals("config", StringComparison.CurrentCultureIgnoreCase)) SaveConfig();
						else if (fields[1].Equals("plugins", StringComparison.CurrentCultureIgnoreCase)) savePlugins = true;
						else if (fields[1].Equals("users", StringComparison.CurrentCultureIgnoreCase)) SaveUsers();
						else {
							ConsoleUtils.WriteLine("%cREDUsage: save [config|plugins|users]%r");
							badSyntax = true;
						}
						if (savePlugins) {
							SavePlugins();
							foreach (var plugin in Plugins)
								plugin.Obj.OnSave();
						}
						if (!badSyntax) ConsoleUtils.WriteLine("Configuration saved successfully.");

					} else if (fields[0].Equals("send", StringComparison.CurrentCultureIgnoreCase)) {
                        if (fields.Length < 3)
                            ConsoleUtils.WriteLine("%cREDUsage: send <network> <command...>%r");
                        else {
                            IrcClient client = null; int i;
                            if (int.TryParse(fields[1], out i) && i >= 0 && i < Bot.Clients.Count)
                                client = Bot.Clients[i].Client;
                            else {
                                foreach (var entry in Bot.Clients) {
                                    if (fields[1].Equals(entry.Name, StringComparison.CurrentCultureIgnoreCase) ||
                                        fields[1].Equals(entry.Address, StringComparison.CurrentCultureIgnoreCase)) {
                                        client = entry.Client;
                                        break;
                                    }
                                }
                            }

                            if (client == null)
                                ConsoleUtils.WriteLine("%cREDThere is no such connection.%r");
                            else
                                client.Send(string.Join(" ", fields.Skip(2)));
                        }

                    } else {
                        consoleClient.Put(input);
                    }
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cREDThe command failed: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cDKRED" + ex.StackTrace + "%r");
                }
            }
        }

        #region First-run config
        private static void FirstRun() {
            if (!Bot.ConfigFileFound)
                FirstRunConfig();
            if (!Bot.UsersFileFound)
                FirstRunUsers();
            if (!Bot.PluginsFileFound)
                FirstRunPlugins();
        }

        private static void FirstRunConfig() {
            Console.WriteLine();
            Console.WriteLine("This appears to be the first time I have been run here. Let us take a moment to set up.");

            Console.WriteLine("Please enter the identity details I should use on IRC.");
            do {
                Console.Write("Nicknames (comma- or space-separated, in order of preference): ");
                string input = Console.ReadLine();
                Bot.DefaultNicknames = input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string nickname in Bot.DefaultNicknames) {
                    if (nickname[0] >= '0' && nickname[0] <= '9') {
                        Console.WriteLine("A nickname can't begin with a digit.");
                        Bot.DefaultNicknames = null;
                        break;
                    }
                    foreach (char c in nickname) {
                        if ((c < 'A' || c > '}') && (c < '0' || c > '9') && c != '-') {
                            Console.WriteLine("'" + nickname + "' contains invalid characters.");
                            Bot.DefaultNicknames = null;
                            break;
                        }
                    }
                }
            } while (Bot.DefaultNicknames == null);

            do {
                Console.Write("Ident username: ");
                Bot.DefaultIdent = Console.ReadLine();
                foreach (char c in Bot.DefaultIdent) {
                    if ((c < 'A' || c > '}') && (c < '0' && c > '9') && c != '-') {
                        Console.WriteLine("That username contains invalid characters.");
                        Bot.DefaultIdent = null;
                        break;
                    }
                }
            } while (Bot.DefaultIdent == string.Empty);

            do {
                Console.Write("Full name: ");
                Bot.DefaultFullName = Console.ReadLine();
            } while (Bot.DefaultFullName == string.Empty);

            Console.Write("User info for CTCP (blank entry for the default): ");
            Bot.DefaultUserInfo = Console.ReadLine();
            if (Bot.DefaultUserInfo == "") Bot.DefaultUserInfo = "CBot by Andrio Celos";

            Bot.DefaultCommandPrefixes = null;
            do {
                Console.Write("What do you want my command prefix to be? ");
                string input = Console.ReadLine();
                if (input.Length != 1)
                    Console.WriteLine("It must be a single character.");
                else {
                    Bot.DefaultCommandPrefixes = new string[] { input };
                }
            } while (Bot.DefaultCommandPrefixes == null);

            Console.WriteLine();

            if (boolPrompt("Shall I connect to an IRC network? ")) {
                string networkName;
                string address = null;
                string password = null;
                ushort port = 0;
                bool tls = false;
                bool acceptInvalidCertificate = false;
                IEnumerable<AutoJoinChannel> autoJoinChannels;

                do {
                    Console.Write("What is the name of the IRC network? ");
                    networkName = Console.ReadLine();
                } while (networkName == "");
                do {
                    Console.Write("What is the address of the server? ");
                    string input = Console.ReadLine();
                    if (input == "") continue;
                    Match match = Regex.Match(input, @"^(?>([^:]*):(?:(\+)?(\d{1,5})))$", RegexOptions.Singleline);
                    if (match.Success) {
                        // Allow entries that include a port number, of the form irc.esper.net:+6697
                        if (!ushort.TryParse(match.Groups[3].Value, out port) || port == 0) {
                            Console.WriteLine("That isn't a valid port number.");
                            continue;
                        }
                        address = match.Groups[1].Value;
                        tls = match.Groups[2].Success;
                    } else {
                        address = input;
                    }
                } while (address == null);

                while (port == 0) {
                    Console.Write("What port number should I connect on? ");
                    string input = Console.ReadLine();
                    if (input.Length == 0) continue;
                    if (input[0] == '+') {
                        tls = true;
                        input = input.Substring(1);
                    }
                    if (!ushort.TryParse(input, out port) || port == 0) {
                        Console.WriteLine("That is not a valid port number.");
                        tls = false;
                    }
                }

                if (!tls)
                    tls = boolPrompt("Should I use TLS? ");
                if (tls)
                    acceptInvalidCertificate = boolPrompt("Should I connect if the server's certificate is invalid? ");

                if (boolPrompt("Do I need to use a password to register to the IRC server? ")) {
                    Console.Write("What is it? ");
                    password = passwordPrompt();
                }

                NickServSettings nickServ = null;
                Console.WriteLine();
                if (boolPrompt("Is there a NickServ registration for me on " + networkName + "? ")) {
                    nickServ = new NickServSettings();
                    do {
                        Console.Write("Grouped nicknames (comma- or space-separated): ");
                        nickServ.RegisteredNicknames = Console.ReadLine().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string nickname in nickServ.RegisteredNicknames) {
                            if (nickname[0] >= '0' && nickname[0] <= '9') {
                                Console.WriteLine("A nickname can't begin with a digit.");
                                nickServ.RegisteredNicknames = null;
                                break;
                            }
                            foreach (char c in nickname) {
                                if ((c < 'A' || c > '}') && (c < '0' && c > '9') && c != '-') {
                                    Console.WriteLine("'" + nickname + "' contains invalid characters.");
                                    nickServ.RegisteredNicknames = null;
                                    break;
                                }
                            }
                        }
                    } while (nickServ.RegisteredNicknames == null);

                    do {
                        Console.Write("NickServ account password: ");
                        nickServ.Password = passwordPrompt();
                    } while (nickServ.Password.Length == 0);

                    nickServ.AnyNickname = boolPrompt(string.Format("Can I log in from any nickname by including '{0}' in the identify command? ", nickServ.RegisteredNicknames[0]));
                }

                Console.WriteLine();
                Console.Write("What channels (comma- or space-separated) should I join upon connecting? ");
                autoJoinChannels = Console.ReadLine().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(c => new AutoJoinChannel(c));

                var network = new ClientEntry(networkName) {
                    Address = address,
                    Port = port,
                    Password = password,
                    TLS = tls,
                    AcceptInvalidTlsCertificate = acceptInvalidCertificate,
                    NickServ = nickServ,
					SaveToConfig = true
                };
                network.AutoJoin.AddRange(autoJoinChannels);
                AddNetwork(network);
            }

            Bot.SaveConfig();

            Console.WriteLine("OK, that's the IRC connection configuration done.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void FirstRunUsers() {
            int method;
            string accountName;
            string input;

            Console.WriteLine();
            Console.WriteLine("How do you want to identify yourself to me?");
            ConsoleUtils.WriteLine("%cWHITEA%r: With a password, via PM, through the Identify plugin");
            ConsoleUtils.WriteLine("%cWHITEB%r: With a services account");
            ConsoleUtils.WriteLine("%cWHITEC%r: With a hostmask or vHost");
            ConsoleUtils.WriteLine("%cWHITED%r: Skip this step");
            Console.WriteLine();

            while (true) {
                Console.Write("Your choice (enter the letter): ");
                input = Console.ReadLine();
                if (input.Length == 0) continue;

                if (input[0] >= 'a' && input[0] <= 'c') {
                    method = input[0] - 'a';
                    break;
                } else if (input[0] >= 'A' && input[0] <= 'C') {
                    method = input[0] - 'A';
                    break;
                } else if (input[0] == 'd' || input[0] == 'D')
                    return;
                else
                    Console.WriteLine("There was no such option yet.");
            }

            string prompt;
            switch (method) {
                case 0:
                    prompt = "What do you want your account name to be? ";
                    break;
                case 1:
                    prompt = "What is your services account name? ";
                    break;
                case 2:
                    prompt = "What hostmask should identify you? (Example: *!*you@your.vHost) ";
                    break;
                default:
                    prompt = null;
                    break;
            }

            while (true) {
                if (input == null && method == 0) {
                    Console.WriteLine(prompt);
                    Console.Write("For simplicity, we recommend you use your IRC nickname. ");
                } else
                    Console.Write(prompt);

                input = Console.ReadLine();
                if (input.Length == 0) continue;

                if (input.Contains(" "))
                    Console.WriteLine("It can't contain spaces.");
                else if (method == 0 && input.Contains("@"))
                    Console.WriteLine("It can't contain '@'.");
                else if (method == 2 && !input.Contains("@"))
                    Console.WriteLine("That doesn't look like a hostmask. Please include an '@'.");
                else if (method == 2 && input.EndsWith("@*")) {
                    Console.WriteLine("This would allow anyone using your nickname to pretend to be you!");
                    if (boolPrompt("Are you really sure you want to use a wildcard host? ")) {
                        accountName = input;
                        break;
                    }
                } else {
                    accountName = input;
                    break;
                }
            }

            switch (method) {
                case 0:
                    // Prompt for a password.
                    RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider();
                    SHA256Managed SHA256M = new SHA256Managed();
                    while (true) {
                        var builder = new StringBuilder();

                        Console.Write("Please enter a password. ");
                        input = passwordPrompt();
                        if (input == "") continue;
                        if (input.Contains(" ")) {
                            Console.WriteLine("It can't contain spaces.");
                            continue;
                        }

                        // Hash the password.
                        byte[] salt = new byte[32];
                        RNG.GetBytes(salt);
                        byte[] hash = SHA256M.ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(input)).ToArray());

                        Console.Write("Please confirm your password. ");
                        input = passwordPrompt();
                        byte[] confirmHash = SHA256M.ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(input)).ToArray());
                        if (!hash.SequenceEqual(confirmHash)) {
                            Console.WriteLine("The passwords don't match.");
                            continue;
                        }

                        // Add the account and give all permissions.
                        Bot.Accounts.Add(accountName, new Account {
                            Password = string.Join(null, salt.Select(b => b.ToString("x2"))) + string.Join(null, hash.Select(b => b.ToString("x2"))),
                            HashType = HashType.SHA256Salted,
                            Permissions = new string[] { "*" }
                        });

                        ConsoleUtils.WriteLine("Thank you. To log in from IRC, enter %cWHITE/msg {0} !id <password>%r or %cWHITE/msg {0} !id {1} <password>%r, without the brackets.", Bot.Nickname, accountName);
                        break;
                    }
                    break;
                case 1:
                    // Add the account and give all permissions.
                    Bot.Accounts.Add("$a:" + accountName, new Account { Permissions = new string[] { "*" } });
                    ConsoleUtils.WriteLine("Thank you. Don't forget to log in to your NickServ account.", Bot.Nickname, accountName);
                    break;
                case 2:
                    // Add the account and give all permissions.
                    Bot.Accounts.Add(accountName, new Account { Permissions = new string[] { "*" } });
                    ConsoleUtils.WriteLine("Thank you. Don't forget to enable your vHost, if needed.", Bot.Nickname, accountName);
                    break;
            }

            Bot.SaveUsers();

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void FirstRunPlugins() {
            Console.WriteLine();
            Console.WriteLine("Now we will select plugins to use.");
            Console.WriteLine("Each plugin instance will be identified by a name, or key. This must be unique, and should be alphanumeric.");
            Console.WriteLine("Thus, you can load multiple instances of the same plugin, perhaps for different channels.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);

            var pluginList = new List<Tuple<string, string, ConsoleColor>>();
            var pluginFiles = new List<string>();
            var selected = new List<Tuple<string, string, string[]>>();  // key, filename, channels

            // Find a plugins directory.
            if (!Directory.Exists("plugins")) {
                ConsoleUtils.WriteLine("The default 'plugins' directory does not seem to exist.");
                while (true) {
                    ConsoleUtils.Write("Where should I look for plugins? (Blank entry for nowhere) ");
                    string input = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(input)) {
                        // If the user doesn't enter a path, assume that there is no specific directory containing plugins.
                        // We will ask them for full paths later.
                        Bot.PluginsPath = null;
                        break;
                    }

                    if (Directory.Exists(input)) {
                        Bot.PluginsPath = input;
                        break;
                    }
                    ConsoleUtils.WriteLine("There is no such directory.");
                    if (!boolPrompt("Try again? ")) {
                        Bot.PluginsPath = null;
                        break;
                    }
                }
            }

            // Prepare the menu.
            if (Bot.PluginsPath != null) {
                // List all DLL files in the plugins directory and show which ones are valid plugins.
                foreach (string file in Directory.GetFiles(Bot.PluginsPath, "*.dll")) {
                    // Look for a plugin class.
                    bool found = false;
                    string message = null;

                    try {
                        var assembly = Assembly.LoadFrom(file);
                        foreach (Type type in assembly.GetTypes()) {
                            if (typeof(Plugin).IsAssignableFrom(type)) {
                                // Check the version attribute.
                                var attribute = type.GetCustomAttribute<ApiVersionAttribute>(false);

                                if (attribute == null) {
                                    message = "Outdated plugin – no API version is specified.";
                                } else if (attribute.Version < Bot.MinPluginVersion) {
                                    message = string.Format("Outdated plugin – built for version {0}.{1}.", attribute.Version.Major, attribute.Version.Minor);
                                } else if (attribute.Version > Bot.Version) {
                                    message = string.Format("Outdated bot – the plugin is built for version {0}.{1}.", attribute.Version.Major, attribute.Version.Minor);
                                } else {
                                    found = true;
                                    pluginFiles.Add(Path.Combine(Bot.PluginsPath, Path.GetFileName(file)));
                                    pluginList.Add(new Tuple<string, string, ConsoleColor>(pluginFiles.Count.ToString().PadLeft(2), ": " + Path.GetFileName(file), ConsoleColor.Gray));
                                    break;
                                }
                            }
                        }
                    } catch (ReflectionTypeLoadException ex) {
                        message = "Reflection failed: " + string.Join(", ", ex.LoaderExceptions.Select(ex2 => ex2.Message));
                    } catch (Exception ex) {
                        message = "Reflection failed: " + ex.Message;
                    }

                    if (!found) {
                        if (message != null) {
                            pluginList.Add(new Tuple<string, string, ConsoleColor>("  ", "  " + Path.GetFileName(file) + " - " + message, ConsoleColor.DarkRed));
                        } else {
                            pluginList.Add(new Tuple<string, string, ConsoleColor>("  ", "  " + Path.GetFileName(file) + " - no valid plugin class.", ConsoleColor.DarkGray));
                        }
                    }
                }
            }

            // Show the menu.
            bool done = false;
            do {
                Console.Clear();
                if (pluginList.Count != 0) {
                    Console.WriteLine("Available plugins:");
                    foreach (var entry in pluginList) {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write(entry.Item1);
                        Console.ForegroundColor = entry.Item3;
                        Console.WriteLine(entry.Item2);
                    }
                    Console.ResetColor();
                    Console.WriteLine();
                    ConsoleUtils.WriteLine(" %cWHITEA%r: Select all unselected plugins listed above for all channels");
                }
                ConsoleUtils.WriteLine(" %cWHITEB%r: Specify another file");
                ConsoleUtils.WriteLine(" %cWHITEQ%r: Finish");
                Console.WriteLine();
                if (selected.Count != 0) {
                    Console.WriteLine("Currently selected plugins:");
                    foreach (var entry in selected) {
                        Console.WriteLine("    " + entry.Item1 + ": " + entry.Item2);
                    }
                    Console.WriteLine();
                }
                Console.Write("Select what? (enter the letter or number) ");

                // Get the user's selection.
                string input; int input2;
                while (true) {
                    input = Console.ReadLine().Trim();
                    if (input == string.Empty) {
                        --Console.CursorTop;
                        Console.Write("Select what? (enter the letter or number) ");
                        continue;
                    }
                    break;
                }

                string file = null;

                if (input[0] == 'a' || input[0] == 'A') {
                    // Select all plugins that aren't already selected.
                    foreach (string file2 in pluginFiles) {
                        string key = Path.GetFileNameWithoutExtension(file2);
                        if (!selected.Any(entry => entry.Item2 == file2))
                            selected.Add(new Tuple<string, string, string[]>(key, file2, new string[] { "*" }));
                    }
                } else if (input[0] == 'b' || input[0] == 'B') {
                    do {
                        Console.Write("File path: ");
                        input = Console.ReadLine();
                        if (File.Exists(input)) {
                            file = input;
                            break;
                        }
                        ConsoleUtils.WriteLine("There is no such file.");
                    } while (boolPrompt("Try again? "));
                } else if (input[0] == 'q' || input[0] == 'Q') {
                    if (selected.Count == 0) {
                        bool input3;
                        Console.Write("You haven't selected any plugins. Cancel anyway? ");
                        if (TryParseBoolean(Console.ReadLine(), out input3) && input3) done = true;
                    } else
                        done = true;
                } else if (int.TryParse(input, out input2) && input2 >= 1 && input2 <= pluginFiles.Count) {
                    file = pluginFiles[input2 - 1];
                }

                if (file != null) {
                    string key; string[] channels;
                    // A file was selected.
                    Console.Write("What key should identify this instance? (Blank entry for " + Path.GetFileNameWithoutExtension(file) + ") ");
                    input = Console.ReadLine().Trim();
                    if (input.Length == 0) {
                        key = Path.GetFileNameWithoutExtension(file);
                    } else {
                        key = input;
                    }

                    ConsoleUtils.WriteLine("You may enter one or more channels, separated by spaces or commas, in the format %cWHITE#channel%r, %cWHITENetworkName/#channel%r or %cWHITENetworkName/*%r.");
                    Console.Write("What channels should this instance be active in? (Blank entry for all channels) ");
                    input = Console.ReadLine().Trim();
                    if (input.Length == 0) {
                        channels = new string[] { "*" };
                    } else {
                        channels = input.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    selected.Add(new Tuple<string, string, string[]>(key, file, channels));
                }
            } while (!done);

			foreach (var entry in selected) {
				Plugins.Add(new PluginEntry(entry.Item1, entry.Item2, entry.Item3));
			}

			// Write out the config file.
			SavePlugins();

            Console.WriteLine();
            Console.WriteLine("Configuration is now complete.");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static bool boolPrompt(string message) {
            string input;
            while (true) {
                Console.Write(message);
                input = Console.ReadLine();
                if (input.Length != 0) {
                    switch (input[0]) {
                        case 'y': case 'Y':
                        case 's': case 'S':
                        case 'o': case 'O':
                        case 'j': case 'J':
                            return true;
                        case 'n': case 'N':
                        case 'a': case 'A':
                        case 'p': case 'P':
                            return false;
                        default:
                            Console.WriteLine("Please enter 'yes' or 'no'.");
                            break;
                    }
                }
            }
        }

        private static string passwordPrompt() {
            var builder = new StringBuilder();
            while (true) {
                ConsoleKeyInfo c = Console.ReadKey(true);
                if (c.Key == ConsoleKey.Enter) break;
                if (c.Key == ConsoleKey.Backspace) {
                    if (builder.Length != 0) builder.Remove(builder.Length - 1, 0);
                } else if (c.KeyChar != '\0')  // if they typed a character, and not F1, Home or something similar...
                    builder.Append(c.KeyChar);
                else
                    Console.Beep();
            }
            Console.WriteLine();
            return builder.ToString();
        }
        #endregion

        /// <summary>Loads a plugin and adds it to CBot's list of active plugins.</summary>
        /// <param name="key">A key to identify the newly loaded plugin.</param>
        /// <param name="filename">The file to load.</param>
        /// <param name="channels">A list of channels in which this plugin should be active.</param>
        /// <exception cref="ArgumentException">A plugin with the specified key is already loaded.</exception>
        /// <exception cref="InvalidPluginException">The plugin could not be constructed.</exception>
		public static PluginEntry LoadPlugin(string key, string filename, params string[] channels) {
			var entry = new PluginEntry(key, filename, channels);
			LoadPlugin(entry);
			return entry;
		}
        public static void LoadPlugin(PluginEntry entry) {
            Type pluginType = null;
            string errorMessage = null;

            if (Plugins.Contains(entry.Key)) throw new ArgumentException(string.Format("A plugin with key {0} is already loaded.", entry.Key), nameof(entry));

            ConsoleUtils.Write("  Loading plugin %cWHITE" + entry.Key + "%r...");
            int x = Console.CursorLeft; int y = Console.CursorTop; int x2; int y2;
            Console.WriteLine();

            try {
				// Find the plugin class.
                var assembly = Assembly.LoadFrom(entry.Filename);
                var assemblyName = assembly.GetName();

                foreach (Type type in assembly.GetTypes()) {
                    if (!type.IsAbstract && typeof(Plugin).IsAssignableFrom(type)) {
                        pluginType = type;
                        break;
                    }
                }
                if (pluginType == null) {
                    errorMessage = "Invalid – no valid plugin class.";
                    throw new InvalidPluginException(entry.Filename, string.Format("The file '{0}' does not contain a class that inherits from the base plugin class.", entry.Filename));
                }

				// Check the version number.
                Version pluginVersion = null;
                foreach (ApiVersionAttribute attribute in pluginType.GetCustomAttributes(typeof(ApiVersionAttribute), false)) {
                    if (pluginVersion == null || pluginVersion < attribute.Version)
                        pluginVersion = attribute.Version;
                }
                if (pluginVersion == null) {
                    errorMessage = "Outdated plugin – no API version is specified.";
                    throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' does not specify the version of CBot for which it was built.", pluginType.Name, entry.Filename));
                } else if (pluginVersion < Bot.MinPluginVersion) {
                    errorMessage = string.Format("Outdated plugin – built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                    throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' was built for older version {2}.{3}.", pluginType.Name, entry.Filename, pluginVersion.Major, pluginVersion.Minor));
                } else if (pluginVersion > Bot.Version) {
                    errorMessage = string.Format("Outdated bot – the plugin is built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                    throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' was built for newer version {2}.{3}.", pluginType.Name, entry.Filename, pluginVersion.Major, pluginVersion.Minor));
                }

				// Construct the plugin.
                int constructorType = -1;
                foreach (ConstructorInfo constructor in pluginType.GetConstructors()) {
                    ParameterInfo[] parameters = constructor.GetParameters();
                    if (parameters.Length == 0) {
                        if (constructorType < 0) constructorType = 0;
                    } else if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(string))) {
                        if (constructorType < 1) constructorType = 1;
                    } else if (parameters.Length == 2 && parameters[0].ParameterType.IsAssignableFrom(typeof(string)) && parameters[1].ParameterType.IsArray && parameters[1].ParameterType.GetElementType() == typeof(string)) {
                        constructorType = 2;
                        break;
                    }
                }

                Plugin plugin;
                if (constructorType == 0)
                    plugin = (Plugin) Activator.CreateInstance(pluginType);
                else if (constructorType == 1)
                    plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { entry.Key });
                else if (constructorType == 2)
                    plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { entry.Key, entry.Channels });
                else {
                    errorMessage = "Invalid – no valid constructor on the plugin class.";
                    throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' does not contain a supported constructor.\n" +
                                                                                   "It should be defined as 'public SamplePlugin()'", pluginType.Name, entry.Filename));
                }

				plugin.Key = entry.Key;
                plugin.Channels = entry.Channels ?? new string[0];
				entry.Obj = plugin;

				foreach (var command in plugin.Commands.Values) {
					command.Attribute.plugin = plugin;
					try {
						command.Attribute.SetPriorityHandler();
					} catch (Exception ex) {
						throw new InvalidPluginException(entry.Filename, $"Could not resolve the command priority handler of {command.Attribute.Names[0]}.", ex);
					}
				}

                Plugins.Add(entry);

                x2 = Console.CursorLeft; y2 = Console.CursorTop;
                Console.SetCursorPosition(x, y);
                ConsoleUtils.WriteLine(" {0} ({1}) OK", plugin.Name, assemblyName.Version);
                Console.SetCursorPosition(x2, y2);
			} catch (Exception ex) {
                x2 = Console.CursorLeft; y2 = Console.CursorTop;
                Console.SetCursorPosition(x, y);
                ConsoleUtils.Write(" %cRED");
                ConsoleUtils.Write(errorMessage ?? "Failed");
                ConsoleUtils.WriteLine("%r");
                Console.SetCursorPosition(x2, y2);
                LogError(entry.Key, "Loading", ex);

                throw ex;
            }
        }
		public static void EnablePlugin(PluginEntry plugin) {
			LoadPlugin(plugin);
			ConsoleUtils.Write("  Enabling plugin %cWHITE" + plugin.Key + "%r...");
			int x = Console.CursorLeft; int y = Console.CursorTop; int x2; int y2;
			Console.WriteLine();

			try {
				plugin.Obj.LoadLanguage();
				plugin.Obj.Initialize();

				x2 = Console.CursorLeft; y2 = Console.CursorTop;
				Console.SetCursorPosition(x, y);
				ConsoleUtils.WriteLine(" {0} ({1}) OK", plugin.Obj.Name, plugin.Obj.GetType().Assembly.GetName().Version);
				Console.SetCursorPosition(x2, y2);
			} catch (Exception ex) {
				x2 = Console.CursorLeft; y2 = Console.CursorTop;
				Console.SetCursorPosition(x, y);
				ConsoleUtils.Write(" %cRED");
				ConsoleUtils.Write("Failed");
				ConsoleUtils.WriteLine("%r");
				Console.SetCursorPosition(x2, y2);
				LogError(plugin.Key, "Initialize", ex);
				throw;
			}
		}
		public static void EnablePlugins(List<PluginEntry> plugins) {
			var exceptions = new List<Exception>();

			foreach (var plugin in plugins) {
				try {
					LoadPlugin(plugin);
				} catch (Exception ex) {
					exceptions.Add(ex);
				}
			}
			foreach (var plugin in plugins) {
				if (plugin.Obj != null) {  // It loaded successfully.
					ConsoleUtils.Write("  Enabling plugin %cWHITE" + plugin.Key + "%r...");
					int x = Console.CursorLeft; int y = Console.CursorTop; int x2; int y2;
					Console.WriteLine();

					try {
						plugin.Obj.LoadLanguage();
						plugin.Obj.Initialize();

						x2 = Console.CursorLeft; y2 = Console.CursorTop;
						Console.SetCursorPosition(x, y);
						ConsoleUtils.WriteLine(" {0} ({1}) OK", plugin.Obj.Name, plugin.Obj.GetType().Assembly.GetName().Version);
						Console.SetCursorPosition(x2, y2);
					} catch (Exception ex) {
						x2 = Console.CursorLeft; y2 = Console.CursorTop;
						Console.SetCursorPosition(x, y);
						ConsoleUtils.Write(" %cRED");
						ConsoleUtils.Write("Failed");
						ConsoleUtils.WriteLine("%r");
						Console.SetCursorPosition(x2, y2);
						LogError(plugin.Key, "Initialize", ex);
						exceptions.Add(ex);
					}
				}
			}

			if (exceptions.Count != 0) {
				throw new AggregateException(exceptions);
			}
		}

        public static void DropPlugin(string key) {
            var plugin = Bot.Plugins[key];
            plugin.Obj.OnUnload();
            plugin.Obj.Channels = new string[0];
            Bot.Plugins.Remove(key);
        }

        /// <summary>Loads configuration data from the file CBotConfig.ini if it is present.</summary>
        public static void LoadConfig() => LoadConfig(true);
        private static void LoadConfig(bool update) {
			if (File.Exists("config.json")) {
				Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json"));
				foreach (var network in Config.Networks) {
					if (network.Nicknames == null) network.Nicknames = Config.Nicknames;
					if (network.Ident == null) network.Ident = Config.Ident;
					if (network.FullName == null) network.FullName = Config.FullName;
				}
				DefaultCommandPrefixes = Config.CommandPrefixes;
				ChannelCommandPrefixes = Config.ChannelCommandPrefixes;
			} else if (File.Exists("CBotConfig.ini")) {
				Config = new Config();
				IniConfig.LoadConfig(Config);
			}

            if (update) UpdateNetworks();
        }
        /// <summary>Compares and applies changes in IRC network configuration.</summary>
        public static void UpdateNetworks() {
            if (Config.Networks == null) return;  // Nothing to do.

            Dictionary<string, int> oldNetworks = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
            List<ClientEntry> reconnectNeeded = new List<ClientEntry>();

            for (int i = 0; i < Clients.Count; ++i)
                oldNetworks[Clients[i].Name] = i;

			foreach (var network in Config.Networks) {
                ClientEntry oldNetwork; int index;
                if (oldNetworks.TryGetValue(network.Name, out index)) {
                    oldNetwork = Clients[index];
                    oldNetworks.Remove(network.Name);

                    network.Client = oldNetwork.Client;
                    Clients[index] = network;

                    // Reconnect if the address was changed.
                    if (network.Address != oldNetwork.Address ||
                        network.Port != oldNetwork.Port ||
                        network.TLS != oldNetwork.TLS) {
                        ConsoleUtils.WriteLine("Address of {0} has changed.", network.Name);
                        if (network.Client.State != IrcClientState.Disconnected) {
                            oldNetwork.Client.Send("QUIT :Changing server.");
                            oldNetwork.Client.Disconnect();
                        }
                        reconnectNeeded.Add(network);
                    }

                } else {
					network.SaveToConfig = true;
                    AddNetwork(network);
                    reconnectNeeded.Add(network);
                }
            }

            foreach (var index in oldNetworks.Values) {
                var network = Clients[index];
                if (network.SaveToConfig) {
                    ConsoleUtils.WriteLine("{0} was removed.", network.Name);
                    network.Client.Send("QUIT :Network dropped from configuration.");
                    network.Client.Disconnect();

                    Clients.RemoveAt(index);
                }
            }

            // Reconnect to networks.
            foreach (var network in reconnectNeeded) {
                ConsoleUtils.WriteLine("Connecting to {0} on port {1}.", network.Address, network.Port);
                network.Connect();
            }
        }

        /// <summary>Loads user data from the file CBotUsers.ini if it is present.</summary>
        public static void LoadUsers() => LoadUsers(true);
        public static void LoadUsers(bool update) {
			if (File.Exists("users.json")) {
				Accounts = JsonConvert.DeserializeObject<Dictionary<string, Account>>(File.ReadAllText("users.json"));
				commandCallbackNeeded = Accounts.Any(a => a.Key.StartsWith("$a"));
			} else if (File.Exists("CBotUsers.ini")) {
				IniConfig.LoadUsers();
			}

            if (update) {
                // Remove links to deleted accounts.
                var idsToRemove = new List<string>();

                foreach (var user in Identifications) {
                    if (!Accounts.ContainsKey(user.Value.AccountName))
                        idsToRemove.Add(user.Key);
                }
                foreach (var user in idsToRemove)
                    Identifications.Remove(user);
            }
        }

        /// <summary>Loads active plugin data from the file CBotPlugins.ini if it is present.</summary>
        public static bool LoadPluginConfig() => LoadPluginConfig(true);
		public static bool LoadPluginConfig(bool update) {
			if (File.Exists("plugins.json")) {
				NewPlugins = JsonConvert.DeserializeObject<Dictionary<string, PluginEntry>>(File.ReadAllText("plugins.json"));
			} else if (File.Exists("CBotPlugins.ini")) {
				IniConfig.LoadPlugins();
			}

            if (update) return UpdatePlugins();
            return true;
        }
        /// <summary>Compares and applies changes in plugin configuration.</summary>
        public static bool UpdatePlugins() {
            if (NewPlugins == null) return true;  // Nothing to do.

            bool success = true;
            HashSet<string> oldPlugins = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            List<PluginEntry> reloadNeeded = new List<PluginEntry>();
			List<PluginEntry> newPlugins = new List<PluginEntry>();

            foreach (var plugin in Plugins) oldPlugins.Add(plugin.Key);

            foreach (var plugin in NewPlugins) {
				plugin.Value.Key = plugin.Key;

				PluginEntry oldPlugin;
				if (Plugins.TryGetValue(plugin.Key, out oldPlugin) && plugin.Value.Filename == oldPlugin.Filename) {
                    oldPlugins.Remove(plugin.Key);
                    oldPlugin.Channels = plugin.Value.Channels;
                } else {
                    reloadNeeded.Add(plugin.Value);
                }
            }

            NewPlugins = null;

            foreach (var key in oldPlugins) {
                Plugins[key]?.Obj?.OnUnload();
                Plugins.Remove(key);
                ConsoleUtils.WriteLine("Dropped plugin {0}.", key);
            }

			// Load new plugins.
			try {
				EnablePlugins(reloadNeeded);
			} catch (Exception) {
				success = false;
			}

            return success;
        }

        /// <summary>Writes configuration data to the file `config.json`.</summary>
		public static void SaveConfig() {
			Config.Nicknames = DefaultNicknames;
			Config.Ident = DefaultIdent;
			Config.FullName = DefaultFullName;
			Config.UserInfo = DefaultUserInfo;
			Config.Avatar = DefaultAvatar;
			Config.CommandPrefixes = DefaultCommandPrefixes;
			Config.ChannelCommandPrefixes = ChannelCommandPrefixes;
			Config.Nicknames = DefaultNicknames;

			Config.Networks.Clear();
			Config.Networks.AddRange(Clients.Where(n => n.SaveToConfig));

			Config.CommandPrefixes = DefaultCommandPrefixes;
			Config.ChannelCommandPrefixes = ChannelCommandPrefixes;

			var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
			File.WriteAllText("config.json", json);
		}

        /// <summary>Writes user data to the file `users.json`.</summary>
		public static void SaveUsers() {
			var json = JsonConvert.SerializeObject(Accounts, Formatting.Indented);
			File.WriteAllText("users.json", json);
		}

		/// <summary>Writes plugin data to the file `plugins.json`.</summary>
		public static void SavePlugins() {
			NewPlugins = new Dictionary<string, PluginEntry>();
			foreach (var plugin in Plugins) {
				NewPlugins.Add(plugin.Key, plugin);
			}
			var json = JsonConvert.SerializeObject(NewPlugins, Formatting.Indented);
			File.WriteAllText("plugins.json", json);
			NewPlugins = null;
		}

		public static async Task<(Plugin plugin, Command command)?> GetCommand(IrcUser sender, IrcMessageTarget target, string pluginKey, string label, string parameters) {
			IEnumerable<PluginEntry> plugins;

			if (pluginKey != null) {
				PluginEntry plugin;
				if (Bot.Plugins.TryGetValue(pluginKey, out plugin)) {
					plugins = new[] { plugin };
				} else
					return null;
			} else
				plugins = Bot.Plugins.Where(p => p.Obj.IsActiveTarget(target));

			// Find matching commands.
			var e = new CommandEventArgs(sender.Client, target, sender, null);
			var commands = new Heap<(PluginEntry plugin, Command command, int priority)>(Comparer<(PluginEntry plugin, Command command, int priority)>.Create((c1, c2) =>
				c1.priority.CompareTo(c2.priority)
			));

			foreach (var plugin in plugins) {
				var commands2 = await plugin.Obj.CheckCommands(sender, target, label, parameters, pluginKey != null);
				foreach (var command2 in commands2) {
					commands.Enqueue((plugin, command2, command2.Attribute.PriorityHandler.Invoke(e)));
				}
			}

			if (commands.Count == 0) return null;

			// Execute the command with highest priority.
			var commandEntry = commands.Peek();
			return (commandEntry.plugin.Obj, commandEntry.command);
		}

		/// <summary>Runs a command (/command or /plugin:command) in a message.</summary>
		/// <param name="sender">The user sending the message.</param>
		/// <param name="target">The target of the event: the sender or a channel.</param>
		/// <param name="message">The message text.</param>
		private static async Task<bool> CheckCommands(IrcUser sender, IrcMessageTarget target, string message) {
			if (!IsCommand(target, message, target is IrcChannel, out var pluginKey, out var label, out var prefix, out var parameters)) return false;

			var command = await GetCommand(sender, target, pluginKey, label, parameters);
			if (command == null) return false;

			// Check for permissions.
			var attribute = command.Value.command.Attribute;
			string permission;
			if (attribute.Permission == null)
				permission = null;
			else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
				permission = command.Value.plugin.Key + attribute.Permission;
			else
				permission = attribute.Permission;

			try {
				if (permission != null && !await Bot.CheckPermissionAsync(sender, permission)) {
					if (attribute.NoPermissionsMessage != null) Bot.Say(sender.Client, sender.Nickname, attribute.NoPermissionsMessage);
					return true;
				}

				// Parse the parameters.
				string[] fields = parameters?.Split((char[]) null, attribute.MaxArgumentCount, StringSplitOptions.RemoveEmptyEntries)
									  ?? new string[0];
				if (fields.Length < attribute.MinArgumentCount) {
					Bot.Say(sender.Client, sender.Nickname, "Not enough parameters.");
					Bot.Say(sender.Client, sender.Nickname, string.Format("The correct syntax is \u0002{0}\u000F.", attribute.Syntax.ReplaceCommands(target)));
					return true;
				}

				// Run the command.
				// TODO: Run it on a separate thread?
				var entry = Bot.GetClientEntry(sender.Client);
				try {
					entry.CurrentPlugin = command.Value.plugin;
					entry.CurrentProcedure = command.Value.command.Handler.GetMethodInfo();
					CommandEventArgs e = new CommandEventArgs(sender.Client, target, sender, fields);
					command.Value.command.Handler.Invoke(command.Value.plugin, e);
				} catch (Exception ex) {
					Bot.LogError(command.Value.plugin.Key, command.Value.command.Handler.GetMethodInfo().Name, ex);
					while (ex is TargetInvocationException || ex is AggregateException) ex = ex.InnerException;
					Bot.Say(sender.Client, target.Target, "\u00034The command failed. This incident has been logged. ({0})", ex.Message.Replace('\n', ' '));
				}
				entry.CurrentPlugin = null;
				entry.CurrentProcedure = null;
			} catch (AsyncRequestDisconnectedException) {
			} catch (AsyncRequestErrorException ex) {
				sender.Say("\u00034There was a problem looking up your account name: " + ex.Message);
			}

			return true;
		}

		/// <summary>Runs triggers matched by a message.</summary>
		/// <param name="sender">The user sending the message.</param>
		/// <param name="target">The target of the event: the sender or a channel.</param>
		/// <param name="message">The message text.</param>
		private static async Task<bool> CheckTriggers(IrcUser sender, IrcMessageTarget target, string message) {
			foreach (var pluginEntry in Bot.Plugins.Where(p => p.Obj.IsActiveTarget(target))) {
				var result = await pluginEntry.Obj.CheckTriggers(sender, target, message);
				if (result) return true;
			}
			return false;
		}

		public static bool IsCommand(IrcMessageTarget target, string message, bool requirePrefix) => Bot.IsCommand(target, message, requirePrefix, out _, out _, out _, out _);
        public static bool IsCommand(IrcMessageTarget target, string message, bool requirePrefix, out string plugin, out string label, out string prefix, out string parameters) {
            Match match = Regex.Match(message, @"^" + Regex.Escape(target?.Client?.Me?.Nickname ?? Bot.DefaultNicknames[0]) + @"\.*[:,-]? ", RegexOptions.IgnoreCase);
            if (match.Success) message = message.Substring(match.Length);

			prefix = null;
            foreach (string p in Bot.GetCommandPrefixes(target as IrcChannel)) {
                if (message.StartsWith(p)) {
                    message = message.Substring(p.Length);
                    prefix = p;
					break;
                }
            }

            if (prefix == null && !match.Success && requirePrefix) {
				label = null;
				plugin = null;
				parameters = null;
				return false;
            }

			var pos = message.IndexOf(' ');
			if (pos >= 0) {
				label = message.Substring(0, pos);
				do {
					++pos;
				} while (pos < message.Length && message[pos] == ' ');
				parameters = message.Substring(pos);
			} else {
				parameters = null;
				label = message;
			}

			pos = label.IndexOf(":");
			if (pos >= 0) {
				plugin = label.Substring(0, pos);
				label = label.Substring(pos + 1);
			} else
				plugin = null;

            return true;
        }

        /// <summary>
        /// Determines whether a specified string matches a specified pattern.
        /// The wildcards * and ? are used.
        /// </summary>
        /// <param name="input">The string to check.</param>
        /// <param name="mask">The pattern to check the given string against.</param>
        /// <returns>true if the input matches the mask; false otherwise.</returns>
        public static bool MaskCheck(string input, string mask) {
            StringBuilder exBuilder = new StringBuilder();
            exBuilder.Append('^');

            foreach (char c in mask) {
                if (c == '*') exBuilder.Append(".*");
                else if (c == '?') exBuilder.Append(".");
                else exBuilder.Append(Regex.Escape(c.ToString()));
            }
            exBuilder.Append('$');
            mask = exBuilder.ToString();

            return Regex.IsMatch(input, mask, RegexOptions.IgnoreCase);
        }

        private static void NickServCheck(IrcClient sender, IrcUser User, string Message) {
            foreach (ClientEntry client in Bot.Clients) {
                if (client.Client == sender) {
                    if (client.NickServ != null) {
                        if (Bot.MaskCheck(User.ToString(), client.NickServ.Hostmask) && Bot.MaskCheck(Message, client.NickServ.RequestMask)) {
                            Bot.NickServIdentify(client, User.Nickname);
                        }
                    }
                }
            }
        }
        private static void NickServIdentify(ClientEntry client, string User) {
            if (client.NickServ.IdentifyTime == null || DateTime.Now - client.NickServ.IdentifyTime > TimeSpan.FromSeconds(60)) {
                client.Client.Send(client.NickServ.IdentifyCommand.Replace("$target", User).Replace("$nickname", client.NickServ.RegisteredNicknames[0]).Replace("$password", client.NickServ.Password));
                client.NickServ.IdentifyTime = DateTime.Now;
            }
        }

        private static void OnCTCPMessage(IrcClient Connection, string Sender, string Message) {
            string[] fields = Message.Split(' ');

            switch (fields[0].ToUpper()) {
                case "PING":
                    if (fields.Length > 1)
                        Connection.Send("NOTICE {0} :\u0001PING {1}\u0001", Sender, string.Join(" ", fields.Skip(1)));
                    else
                        Connection.Send("NOTICE {0} :\u0001PING\u0001", Sender);
                    break;
                case "ERRMSG":
                    if (fields.Length > 1)
                        Connection.Send("NOTICE {0} :\u0001ERRMSG No error: {1}\u0001", Sender, string.Join(" ", fields.Skip(1)));
                    else
                        Connection.Send("NOTICE {0} :\u0001ERRMSG No error\u0001", Sender);
                    break;
                case "VERSION":
                    Connection.Send("NOTICE {0} :\u0001VERSION {1}\u0001", Sender, ClientVersion);
                    break;
                case "SOURCE":
                    Connection.Send("NOTICE {0} :\u0001SOURCE {1}\u0001", Sender, "CBot: https://github.com/AndrioCelos/CBot");
                    break;
                case "TIME":
                    Connection.Send("NOTICE {0} :\u0001TIME {1:dddd d MMMM yyyy HH:mm:ss zzz}\u0001", Sender, DateTime.Now);
                    break;
                case "FINGER":
                    StringBuilder readableIdleTime = new StringBuilder(); TimeSpan idleTime;
                    idleTime = DateTime.Now - Connection.LastSpoke;
                    if (idleTime.Days > 0) {
                        if (readableIdleTime.Length > 0) readableIdleTime.Append(", ");
                        readableIdleTime.Append(idleTime.Days);
                        if (idleTime.Days == 1)
                            readableIdleTime.Append("day");
                        else
                            readableIdleTime.Append("days");
                    }
                    if (idleTime.Hours > 0) {
                        if (readableIdleTime.Length > 0) readableIdleTime.Append(", ");
                        readableIdleTime.Append(idleTime.Hours);
                        if (idleTime.Days == 1)
                            readableIdleTime.Append("hour");
                        else
                            readableIdleTime.Append("hours");
                    }
                    if (idleTime.Minutes > 0) {
                        if (readableIdleTime.Length > 0) readableIdleTime.Append(", ");
                        readableIdleTime.Append(idleTime.Minutes);
                        if (idleTime.Days == 1)
                            readableIdleTime.Append("minute");
                        else
                            readableIdleTime.Append("minutes");
                    }
                    if (readableIdleTime.Length == 0 || idleTime.Seconds > 0) {
                        if (readableIdleTime.Length > 0) readableIdleTime.Append(", ");
                        readableIdleTime.Append(idleTime.Seconds);
                        if (idleTime.Days == 1)
                            readableIdleTime.Append("second");
                        else
                            readableIdleTime.Append("seconds");
                    }

                    Connection.Send("NOTICE {0} :\u0001FINGER {1}: {3}; idle for {2}.\u0001", Sender, DefaultNicknames[0], readableIdleTime.ToString(), DefaultUserInfo);
                    break;
                case "USERINFO":
                    Connection.Send("NOTICE {0} :\u0001USERINFO {1}\u0001", Sender, DefaultUserInfo);
                    break;
                case "AVATAR":
                    Connection.Send("NOTICE {0} :\u0001AVATAR {1}\u0001", Sender, DefaultAvatar);
                    break;
                case "CLIENTINFO":
                    string message;
                    if (fields.Length == 1) {
                        message = "CBot: https://github.com/AndrioCelos/CBot – I recognise the following CTCP queries: CLENTINFO, FINGER, PING, TIME, USERINFO, VERSION, AVATAR";
                    } else
                        switch (fields[1].ToUpper()) {
                            case "PING":
                                message = "PING <token>: Echoes the token back to verify that I am receiving your message. This is often used with a timestamp to establish the connection latency.";
                                break;
                            case "ERRMSG":
                                message = "ERRMSG <message>: This is the general response to an unknown query. A query of ERRMSG will return the same message back.";
                                break;
                            case "VERSION":
                                message = "VERSION: Returns the name and version of my client.";
                                break;
                            case "SOURCE":
                                message = "SOURCE: Returns information about where to get my client.";
                                break;
                            case "TIME":
                                message = "TIME: Returns my local date and time.";
                                break;
                            case "FINGER":
                                message = "FINGER: Returns my user info and the amount of time I have been idle.";
                                break;
                            case "USERINFO":
                                message = "USERINFO: Returns information about me.";
                                break;
                            case "CLIENTINFO":
                                message = "CLIENTINFO [query]: Returns information about my client, and CTCP queries I recognise.";
                                break;
                            case "AVATAR":
                                message = "AVATAR: Returns a URL to my avatar, if one is set.";
                                break;
                            default:
                                message = string.Format("I don't recognise {0} as a CTCP query.", fields[1]);
                                break;
                        }
                    Connection.Send("NOTICE {0} :\u0001CLIENTINFO {1}\u0001", Sender, message);
                    break;
                default:
                    Connection.Send("NOTICE {0} :\u0001ERRMSG I don't recognise {1} as a CTCP query.\u0001", Sender, fields[0]);
                    break;
            }
        }

        /// <summary>Returns the bot's default nickname, even if none is specified in configuration.</summary>
        /// <returns>The first default nickname, or 'CBot' if none are set.</returns>
        public static string Nickname => (Bot.DefaultNicknames.Length == 0 ? "CBot" : Bot.DefaultNicknames[0]);

		/// <summary>
		/// Determines whether the specified user has the specified permission.
		/// This method does not perform WHOIS requests; await <see cref="CheckPermissionAsync(IrcUser, string)"/> to do that.
		/// </summary>
		public static bool CheckPermission(IrcUser user, string permission) {
			foreach (var account in getAccounts(user)) {
				if (CheckPermission(account, permission)) return true;
			}
			return false;
		}
        /// <summary>Determines whether the specified account has the specified permission.</summary>
		public static bool CheckPermission(Account account, string permission) {
			int score = 0;

			string[] needleFields = permission.Split(new char[] { '.' });
			bool IRCPermission = needleFields[0].Equals("irc", StringComparison.OrdinalIgnoreCase);

			foreach (string permission2 in account.Permissions) {
				string[] hayFields;
				if (permission2 == "*") {
					if (IRCPermission) continue;
					if (score <= 1) score = 1;
				} else {
					bool polarity = true;
					hayFields = permission2.Split(new char[] { '.' });
					if (hayFields[0].StartsWith("-")) {
						polarity = false;
						hayFields[0] = hayFields[0].Substring(1);
					}
					int matchLevel = 0; int i;
					for (i = 0; i < hayFields.Length; ++i) {
						if (i == hayFields.Length - 1 && hayFields[i] == "*")
							break;
						else if (i < needleFields.Length && needleFields[i].Equals(hayFields[i], StringComparison.OrdinalIgnoreCase))
							++matchLevel;
						else {
							matchLevel = -1;
							break;
						}
					}
					if (matchLevel != -1 && i < hayFields.Length || (i == hayFields.Length && i == needleFields.Length)) {
						if ((score >> 1) <= matchLevel)
							score = (matchLevel << 1) | (polarity ? 1 : 0);
					}
				}
			}

			return ((score & 1) == 1);
		}
		/// <summary>Determines whether the specified user has the specified permission, awaiting a WHOIS request to look up their account name if necessary.</summary>
		public static async Task<bool> CheckPermissionAsync(IrcUser user, string permission) {
			if (CheckPermission(user, permission)) return true;
			if (user.Account == null && commandCallbackNeeded) {
				await user.GetAccountAsync();
				return CheckPermission(user, permission);
			}
			return false;
		}
		/// <summary>Returns all accounts matched by the specified user.</summary>
		private static IEnumerable<Account> getAccounts(IrcUser user) {
			if (user == null) throw new ArgumentNullException("user");

			Identification id;
			Bot.Identifications.TryGetValue(user.Client.NetworkName + "/" + user.Nickname, out id);

			foreach (var account in Bot.Accounts) {
				bool match = false;

				if (account.Key == "*") match = true;
				else if (account.Key.StartsWith("$")) {
					string[] fields = account.Key.Split(new char[] { ':' }, 2);
					string[] fields2;
					ChannelStatus status = null;

					switch (fields[0]) {
						case "$q": status = ChannelStatus.Owner; break;
						case "$s": status = ChannelStatus.Admin; break;
						case "$o": status = ChannelStatus.Op; break;
						case "$h": status = ChannelStatus.Halfop; break;
						case "$v": status = ChannelStatus.Voice; break;
						case "$V": status = ChannelStatus.HalfVoice; break;
						case "$a":
							// NickServ account match
							fields = fields[1].Split(new char[] { '/', ':' }, 2);
							if (fields.Length == 1) fields = new string[] { null, fields[0] };

							match = false;
							if (fields[0] == null || fields[0].Equals(user.Client.NetworkName, StringComparison.CurrentCultureIgnoreCase)) {
								if (user.Account != null && user.Account.Equals(fields[1], StringComparison.OrdinalIgnoreCase))
									match = true;
							}

							break;
						default:
							match = false;
							break;
					}

					if (status != null) {
						// Check that the user has the required access on the given channel.
						IrcClient client = null;

						fields2 = fields[1].Split(new char[] { '/' }, 2);
						if (fields2.Length == 1) fields2 = new string[] { null, fields2[0] };

						// Find the network.
						if (fields2[0] != null) {
							foreach (ClientEntry _client in Bot.Clients) {
								if (_client.Name.Equals(fields2[0], StringComparison.OrdinalIgnoreCase)) {
									client = _client.Client;
									break;
								}
							}
						}

						// Find the channel.
						IrcChannel channel2;
						IrcChannelUser channelUser;
						if (client == null) {
							if (fields2[0] != null) match = false;
							else {
								match = false;
								foreach (ClientEntry _client in Bot.Clients) {
									if (_client.Client.Channels.TryGetValue(fields2[1], out channel2) && channel2.Users.TryGetValue(user.Nickname, out channelUser) &&
										channelUser.Status >= status) {
										match = true;
										break;
									}
								}
							}
						} else {
							match = false;
							if (client.Channels.TryGetValue(fields2[1], out channel2) && channel2.Users.TryGetValue(user.Nickname, out channelUser) &&
								channelUser.Status >= status) {
								match = true;
							}
						}
					}
				} else {
					// Check for a hostmask match.
					if (account.Key.Contains("@")) {
						match = Bot.MaskCheck(user.ToString(), account.Key);
					} else
						match = (id != null && account.Key.Equals(id.AccountName, StringComparison.OrdinalIgnoreCase));
				}

				if (match) yield return account.Value;
			}
		}

		/// <summary>Returns one of the parameters, selected at random.</summary>
		/// <param name="args">The list of parameters to choose between.</param>
		/// <returns>One of the parameters, chosen at random.</returns>
		/// <exception cref="System.ArgumentNullException">args is null.</exception>
		/// <exception cref="System.ArgumentException">args is empty.</exception>
		public static T Choose<T>(params T[] args) {
            if (args == null) throw new ArgumentNullException("args");
            if (args.Length == 0) throw new ArgumentException("args must not be empty.");
            return args[Bot.rng.Next(args.Length)];
        }

        /// <summary>Immediately shuts down CBot.</summary>
        public static void Die() {
            Environment.Exit(0);
        }

        /// <summary>Parses a string representing a Boolean value.</summary>
        /// <param name="s">The string to parse.</param>
        /// <returns>The Boolean value represented by the given string.</returns>
        /// <exception cref="System.ArgumentException">The string was not recognised as a Boolean value.</exception>
        /// <remarks>
        ///   The following values are recognised as true:  'true', 't', 'yes', 'y', 'on'.
        ///   The following values are recognised as false: 'false', 'f', 'no', 'n', 'off'.
        ///   The checks are case-insensitive.
        /// </remarks>
        public static bool ParseBoolean(string s) {
            bool result;
            if (Bot.TryParseBoolean(s, out result)) return result;
            throw new ArgumentException("'" + s + "' is not recognised as true or false.", "value");
        }
        /// <summary>
        ///   Parses a string representing a Boolean value, and returns a value indicating whether it succeeded.
        ///   This overload will not throw an exception if the operation fails.
        /// </summary>
        /// <param name="s">The string to parse.</param>
        /// <param name="result">Returns the Boolean value represented by the given string, if the operation succeeded.</param>
        /// <returns>true if the string was recognised; false otherwise.</returns>
        /// <remarks>
        ///   The following values are recognised as true:  'true', 't', 'yes', 'y', 'on'.
        ///   The following values are recognised as false: 'false', 'f', 'no', 'n', 'off'.
        ///   The checks are case-insensitive.
        /// </remarks>
        public static bool TryParseBoolean(string s, out bool result) {
            if (s.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("T", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                result = true;
                return true;
            }
            if (s.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("F", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("N", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                result = false;
                return true;
            }
            result = default(bool);
            return false;
        }

        internal static void LogConnectionError(IrcClient Server, Exception ex) {
            Exception RealException = (ex is TargetInvocationException) ? ex.InnerException : ex;

            ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] occurred in the connection to '%cWHITE{0}%cGRAY!", Server.NetworkName);
            ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cWHITE{0} :%cGRAY {1}%r", RealException.GetType().FullName, RealException.Message);
            string[] array = RealException.StackTrace.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < array.Length; ++i) {
                string Line = array[i];
                ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cGRAY{0}%r", Line);
            }
            StreamWriter ErrorLogWriter = new StreamWriter("CBotErrorLog.txt", true);
            ErrorLogWriter.WriteLine("[{0}] ERROR occurred in the connection to '{1}!", DateTime.Now.ToString(), Server.NetworkName);
            ErrorLogWriter.WriteLine("        " + RealException.Message);
            for (int j = 0; j < array.Length; ++j) {
                string Line2 = array[j];
                ErrorLogWriter.WriteLine("        " + Line2);
            }
            ErrorLogWriter.WriteLine();
            ErrorLogWriter.Close();
        }
        internal static void LogError(string PluginKey, string Procedure, Exception ex) {
            bool flag = ex is TargetInvocationException;
            Exception RealException;
            if (ex is TargetInvocationException)
                RealException = ex.InnerException;
            else
                RealException = ex;
            ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] occurred in plugin '%cWHITE{0}%cGRAY' in procedure %cWHITE{1}%cGRAY!", PluginKey, Procedure);
            ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cWHITE{0} :%cGRAY {1}%r", RealException.GetType().FullName, RealException.Message);
            string[] array = RealException.StackTrace.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < array.Length; ++i) {
                string Line = array[i];
                ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cGRAY{0}%r", Line);
            }
            StreamWriter ErrorLogWriter = new StreamWriter("CBotErrorLog.txt", true);
            ErrorLogWriter.WriteLine("[{0}] ERROR occurred in plugin '{0}' in procedure {1}!", PluginKey, Procedure);
            ErrorLogWriter.WriteLine("        " + RealException.Message);
            for (int j = 0; j < array.Length; ++j) {
                string Line2 = array[j];
                ErrorLogWriter.WriteLine("        " + Line2);
            }
            ErrorLogWriter.WriteLine();
            ErrorLogWriter.Close();
        }

        /// <summary>Returns a hash and salt for a password.</summary>
        public static byte[] HashPassword(string password) {
            byte[] salt = new byte[32], hash = new byte[32];

            // Generate random salt using a cryptographically-secure psuedo-random number generator.
            new RNGCryptoServiceProvider().GetBytes(salt);

            // Use SHA-256 to generate the hash.
            hash = new SHA256Managed().ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(password)).ToArray());

            byte[] result = new byte[64];
            salt.CopyTo(result,  0);
            hash.CopyTo(result, 32);
            return result;
        }

        /// <summary>Attempts to log in a user with a given password.</summary>
        /// <param name="target">The name and location of the user, in the form NetworkName/Nickname.</param>
        /// <param name="accountName">The name of the account to identify to.</param>
        /// <param name="password">The given password.</param>
        /// <param name="identification">If the identification succeeds, returns the identification data. Otherwise, returns null.</param>
        /// <returns>true if the identification succeeded; false otherwise.</returns>
        public static bool Identify(IrcUser target, string accountName, string password, out Identification identification) {
            string text = null;
            return Bot.Identify(target, accountName, password, out identification, out text);
        }
        /// <summary>Attempts to log in a user with a given password.</summary>
        /// <param name="target">The name and location of the user, in the form NetworkName/Nickname.</param>
        /// <param name="accountName">The name of the account to identify to.</param>
        /// <param name="password">The given password.</param>
        /// <param name="identification">If the identification succeeds, returns the identification data. Otherwise, returns null.</param>
        /// <param name="message">Returns a status message to be shown to the user.</param>
        /// <returns>true if the identification succeeded; false otherwise.</returns>
        public static bool Identify(IrcUser target, string accountName, string password, out Identification identification, out string message) {
            bool success; Account account;

            if (!Bot.Accounts.TryGetValue(accountName, out account)) {
                // No such account.
                message = "The account name or password is invalid.";
                identification = null;
                success = false;
            } else {
                if (Bot.Identifications.TryGetValue(target.Client.NetworkName + "/" + target.Nickname, out identification) && identification.AccountName == accountName) {
                    // The user is already identified.
                    message = string.Format("You are already identified as \u000312{0}\u000F.", identification.AccountName);
                    success = false;
                } else {
                    if (account.VerifyPassword(password)) {
                        identification = new Identification { AccountName = accountName, Channels = new HashSet<string>() };
                        Bot.Identifications.Add(target.Client.NetworkName + "/" + target.Nickname, identification);
                        message = string.Format("You have identified successfully as \u000309{0}\u000F.", accountName);
                        success = true;
                    } else {
                        message = string.Format("The account name or password is invalid.", accountName);
                        identification = null;
                        success = false;
                    }
                }
            }
            return success;
        }

        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="client">The IRC connection to send to.</param>
        /// <param name="target">The name of the channel or user to send to.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IrcClient client, string target, string message, SayOptions options) {
            if (message == null || message == "") return;

            if ((options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(message[0]);
                if (c != message[0]) message = c + message.Substring(1);
            }

            bool notice = false;
            if (client.IsChannel(target)) {
                if ((options & (SayOptions) 1) != 0) {
                    target = "@" + target;
                    notice = true;
                }
            } else
                notice = true;
            if ((options & SayOptions.NoticeAlways) != 0)
                notice = true;
            if ((options & SayOptions.NoticeNever) != 0)
                notice = false;

            var target2 = new IrcMessageTarget(client, target);

            foreach (string line in message.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (notice) target2.Notice(line);
                else target2.Say(line);
            }
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="client">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="message">The message to send.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IrcClient client, string channel, string message) {
            Bot.Say(client, channel, message, 0);
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="client">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="format">The format of the message to send, as per string.Format.</param>
        /// <param name="args">The parameters to include in the message text.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IrcClient client, string channel, string format, params object[] args) {
            Bot.Say(client, channel, string.Format(format, args), 0);
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="client">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="format">The format of the message to send, as per string.Format.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <param name="args">The parameters to include in the message text.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IrcClient client, string channel, string format, SayOptions options, params object[] args) {
            Bot.Say(client, channel, string.Format(format, args), options);
        }

		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="target">The channel to use a command prefix for.</param>
		/// <returns>A copy of text with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public static string ReplaceCommands(this string text, IrcMessageTarget target)
			=> ReplaceCommands(text, "!", Bot.GetCommandPrefixes(target)[0].ToString());
		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="target">The channel to use a command prefix for.</param>
		/// <param name="oldPrefix">The command prefix to replace in the text.</param>
		/// <returns>A copy of text with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public static string ReplaceCommands(this string text, IrcMessageTarget target, string oldPrefix)
			=> ReplaceCommands(text, oldPrefix, Bot.GetCommandPrefixes(target)[0].ToString());
		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="oldPrefix">The command prefix to replace in the text.</param>
		/// <param name="newPrefix">The command prefix to substitute.</param>
		/// <returns>A copy of <paramref name="text"/> with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public static string ReplaceCommands(this string text, string oldPrefix, string newPrefix) {
			if (newPrefix == "$") newPrefix = "$$";  // '$' must be escaped in the regex substitution.
			return Regex.Replace(text, @"(?<=(?:^|[\s\x00-\x20])(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?)" + Regex.Escape(oldPrefix) + @"(?=(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?\w)", newPrefix);
		}

		#region Event handlers
		private static void OnAwayCancelled(object sender, AwayEventArgs e)                            { foreach (var entry in Bot.Plugins) if (entry.Obj.OnAwayCancelled(sender, e)) return; }
        private static void OnAwayMessage(object sender, AwayMessageEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnAwayMessage(sender, e)) return; }
        private static void OnAwaySet(object sender, AwayEventArgs e)                                  { foreach (var entry in Bot.Plugins) if (entry.Obj.OnAwaySet(sender, e)) return; }
		private static void OnCapabilitiesAdded(object sender, CapabilitiesAddedEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnCapabilitiesAdded(sender, e)) return; }
        private static void OnCapabilitiesDeleted(object sender, CapabilitiesEventArgs e)              { foreach (var entry in Bot.Plugins) if (entry.Obj.OnCapabilitiesDeleted(sender, e)) return; }
		private static async void OnChannelAction(object sender, ChannelMessageEventArgs e) {
			foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelAction(sender, e)) return;
			if (await Bot.CheckTriggers(e.Sender, e.Channel, "ACTION " + e.Message)) return;
		}
		private static void OnChannelAdmin(object sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelAdmin(sender, e)) return; }
        private static void OnChannelBan(object sender, ChannelListChangedEventArgs e)                 { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelBan(sender, e)) return; }
        private static void OnChannelBanList(object sender, ChannelModeListEventArgs e)                { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelBanList(sender, e)) return; }
        private static void OnChannelBanListEnd(object sender, ChannelModeListEndEventArgs e)          { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelBanListEnd(sender, e)) return; }
        private static void OnChannelBanRemoved(object sender, ChannelListChangedEventArgs e)          { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelBanRemoved(sender, e)) return; }
        private static void OnChannelCTCP(object sender, ChannelMessageEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelCTCP(sender, e)) return;
            Bot.OnCTCPMessage((IrcClient) sender, e.Sender.Nickname, e.Message);
        }
        private static void OnChannelDeAdmin(object sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeAdmin(sender, e)) return; }
        private static void OnChannelDeHalfOp(object sender, ChannelStatusChangedEventArgs e)          { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeHalfOp(sender, e)) return; }
        private static void OnChannelDeHalfVoice(object sender, ChannelStatusChangedEventArgs e)       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeHalfVoice(sender, e)) return; }
        private static void OnChannelDeOp(object sender, ChannelStatusChangedEventArgs e)              { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeOp(sender, e)) return; }
        private static void OnChannelDeOwner(object sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeOwner(sender, e)) return; }
        private static void OnChannelDeVoice(object sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelDeVoice(sender, e)) return; }
        private static void OnChannelExempt(object sender, ChannelListChangedEventArgs e)              { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelExempt(sender, e)) return; }
        private static void OnChannelExemptRemoved(object sender, ChannelListChangedEventArgs e)       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelExemptRemoved(sender, e)) return; }
        private static void OnChannelHalfOp(object sender, ChannelStatusChangedEventArgs e)            { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelHalfOp(sender, e)) return; }
        private static void OnChannelHalfVoice(object sender, ChannelStatusChangedEventArgs e)         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelHalfVoice(sender, e)) return; }
        private static void OnChannelInviteExempt(object sender, ChannelListChangedEventArgs e)        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelInviteExempt(sender, e)) return; }
        private static void OnChannelInviteExemptList(object sender, ChannelModeListEventArgs e)       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelInviteExemptList(sender, e)) return; }
        private static void OnChannelInviteExemptListEnd(object sender, ChannelModeListEndEventArgs e) { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelInviteExemptListEnd(sender, e)) return; }
        private static void OnChannelInviteExemptRemoved(object sender, ChannelListChangedEventArgs e) { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelInviteExemptRemoved(sender, e)) return; }
        private static void OnChannelJoin(object sender, ChannelJoinEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelJoin(sender, e)) return;

            var client = (IrcClient) sender;

            Identification id;
            if (Bot.Identifications.TryGetValue(client.NetworkName + "/" + e.Sender.Nickname, out id))
                id.Channels.Add(e.Channel.Name);

            if (e.Sender == client.Me) {
                // Send a WHOX request to get account names.
                if (client.Extensions.ContainsKey("WHOX"))
                    client.Send("WHO {0} %tna,1", e.Channel);
            } else {
                if (Bot.CheckPermission(e.Sender, "irc.autohalfvoice." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +V {1}", e.Channel, e.Sender.Nickname);
                if (Bot.CheckPermission(e.Sender, "irc.autovoice." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +v {1}", e.Channel, e.Sender.Nickname);
                if (Bot.CheckPermission(e.Sender, "irc.autohalfop." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +h {1}", e.Channel, e.Sender.Nickname);
                if (Bot.CheckPermission(e.Sender, "irc.autoop." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +o {1}", e.Channel, e.Sender.Nickname);
                if (Bot.CheckPermission(e.Sender, "irc.autoadmin." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +ao {1} {1}", e.Channel, e.Sender.Nickname);

                if (Bot.CheckPermission(e.Sender, "irc.autoquiet." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
                    client.Send("MODE {0} +q *!*{1}", e.Channel, e.Sender.UserAndHost);
                if (Bot.CheckPermission(e.Sender, "irc.autoban." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-'))) {
                    client.Send("MODE {0} +b *!*{1}", e.Channel, e.Sender.UserAndHost);
                    client.Send("KICK {0} {1} :You are banned from this channel.", e.Channel, e.Sender.Nickname);
                }
            }
        }
        private static void OnChannelJoinDenied(object sender, ChannelJoinDeniedEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelJoinDenied(sender, e)) return; }
        private static void OnChannelKeyRemoved(object sender, ChannelChangeEventArgs e)               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelKeyRemoved(sender, e)) return; }
        private static void OnChannelKeySet(object sender, ChannelKeyEventArgs e)                      { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelKeySet(sender, e)) return; }
        private static void OnChannelKick(object sender, ChannelKickEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelKick(sender, e)) return; }
        private static void OnChannelLeave(object sender, ChannelPartEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelLeave(sender, e)) return;
            string key = ((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Identification id;
            if (Bot.Identifications.TryGetValue(key, out id)) {
                if (id.Channels.Remove(e.Channel.Name)) {
                    if (id.Channels.Count == 0 && !(((IrcClient) sender).Extensions.SupportsMonitor && id.Monitoring))
                        Bot.Identifications.Remove(key);
                }
            }
        }
        private static void OnChannelLimitRemoved(object sender, ChannelChangeEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelLimitRemoved(sender, e)) return; }
        private static void OnChannelLimitSet(object sender, ChannelLimitEventArgs e)                  { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelLimitSet(sender, e)) return; }
        private static void OnChannelList(object sender, ChannelListEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelList(sender, e)) return; }
        private static void OnChannelListChanged(object sender, ChannelListChangedEventArgs e)         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelListChanged(sender, e)) return; }
        private static void OnChannelListEnd(object sender, ChannelListEndEventArgs e)                 { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelListEnd(sender, e)) return; }
        private static async void OnChannelMessage(object sender, ChannelMessageEventArgs e) {
            foreach (var entry in Bot.Plugins) {
                if (entry.Obj.OnChannelMessage(sender, e)) return;
            }
			if (await Bot.CheckCommands(e.Sender, e.Channel, e.Message)) return;
			if (await Bot.CheckTriggers(e.Sender, e.Channel, e.Message)) return;
        }
        private static void OnChannelMessageDenied(object sender, ChannelJoinDeniedEventArgs e)        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelMessageDenied(sender, e)) return; }
        private static void OnChannelModeChanged(object sender, ChannelModeChangedEventArgs e)         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelModeChanged(sender, e)) return; }
        private static void OnChannelModesGet(object sender, ChannelModesSetEventArgs e)               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelModesGet(sender, e)) return; }
        private static void OnChannelModesSet(object sender, ChannelModesSetEventArgs e)               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelModesSet(sender, e)) return; }
        private static void OnChannelNotice(object sender, ChannelMessageEventArgs e)                  { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelNotice(sender, e)) return; }
        private static void OnChannelOp(object sender, ChannelStatusChangedEventArgs e)                { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelOp(sender, e)) return; }
        private static void OnChannelOwner(object sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelOwner(sender, e)) return; }
        private static void OnChannelPart(object sender, ChannelPartEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelPart(sender, e)) return; }
        private static void OnChannelQuiet(object sender, ChannelListChangedEventArgs e)               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelQuiet(sender, e)) return; }
        private static void OnChannelQuietRemoved(object sender, ChannelListChangedEventArgs e)        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelQuietRemoved(sender, e)) return; }
        private static void OnChannelStatusChanged(object sender, ChannelStatusChangedEventArgs e)     { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelStatusChanged(sender, e)) return; }
        private static void OnChannelTimestamp(object sender, ChannelTimestampEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelTimestamp(sender, e)) return; }
        private static void OnChannelTopicChanged(object sender, ChannelTopicChangeEventArgs e)        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelTopicChanged(sender, e)) return; }
        private static void OnChannelTopicReceived(object sender, ChannelTopicEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelTopicReceived(sender, e)) return; }
        private static void OnChannelTopicStamp(object sender, ChannelTopicStampEventArgs e)           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelTopicStamp(sender, e)) return; }
        private static void OnChannelVoice(object sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnChannelVoice(sender, e)) return; }
        private static void OnDisconnected(object sender, DisconnectEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnDisconnected(sender, e)) return;

            if (e.Exception == null)
                ConsoleUtils.WriteLine("%cREDDisconnected from {0}.%r", ((IrcClient) sender).NetworkName);
            else
                ConsoleUtils.WriteLine("%cREDDisconnected from {0}: {1}%r", ((IrcClient) sender).NetworkName, e.Exception.Message);
            if (e.Reason > DisconnectReason.Quit) {
                foreach (ClientEntry client in Bot.Clients) {
                    if (client.Client == sender) {
                        client.StartReconnect();
                        break;
                    }
                }
            }
        }
        private static void OnException(object sender, ExceptionEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnException(sender, e)) return;
            Bot.LogConnectionError((IrcClient) sender, e.Exception);
        }
        private static void OnExemptList(object sender, ChannelModeListEventArgs e)                    { foreach (var entry in Bot.Plugins) if (entry.Obj.OnExemptList(sender, e)) return; }
        private static void OnExemptListEnd(object sender, ChannelModeListEndEventArgs e)              { foreach (var entry in Bot.Plugins) if (entry.Obj.OnExemptListEnd(sender, e)) return; }
        private static void OnInvite(object sender, InviteEventArgs e)                                 { foreach (var entry in Bot.Plugins) if (entry.Obj.OnInvite(sender, e)) return; }
        private static void OnInviteSent(object sender, InviteSentEventArgs e)                         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnInviteSent(sender, e)) return; }
        private static void OnKilled(object sender, PrivateMessageEventArgs e)                         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnKilled(sender, e)) return; }
        private static void OnMOTD(object sender, MotdEventArgs e)                                     { foreach (var entry in Bot.Plugins) if (entry.Obj.OnMOTD(sender, e)) return; }
        private static void OnNames(object sender, ChannelNamesEventArgs e)                            { foreach (var entry in Bot.Plugins) if (entry.Obj.OnNames(sender, e)) return; }
        private static void OnNamesEnd(object sender, ChannelModeListEndEventArgs e)                   { foreach (var entry in Bot.Plugins) if (entry.Obj.OnNamesEnd(sender, e)) return; }
        private static void OnNicknameChange(object sender, NicknameChangeEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnNicknameChange(sender, e)) return;
            string key = ((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Identification id;
            if (Bot.Identifications.TryGetValue(key, out id)) {
                Bot.Identifications.Remove(key);
                Bot.Identifications.Add(((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname, id);
            }
        }
        private static void OnNicknameChangeFailed(object sender, NicknameEventArgs e)                 { foreach (var entry in Bot.Plugins) if (entry.Obj.OnNicknameChangeFailed(sender, e)) return; }
        private static void OnNicknameInvalid(object sender, NicknameEventArgs e)                      { foreach (var entry in Bot.Plugins) if (entry.Obj.OnNicknameInvalid(sender, e)) return; }
        private static void OnNicknameTaken(object sender, NicknameEventArgs e) {
            foreach (var pluginEntry in Bot.Plugins) if (pluginEntry.Obj.OnNicknameTaken(sender, e)) return;
            // Cycle through the list.
            var entry = GetClientEntry((IrcClient) sender);
			var nicknames = entry.Nicknames ?? DefaultNicknames;
            if (entry.Client.State <= IrcClientState.Registering && nicknames.Length > 1) {
                for (int i = 0; i < nicknames.Length - 1; ++i) {
                    if (nicknames[i] == e.Nickname) {
                        entry.Client.Me.Nickname = nicknames[i + 1];
                        break;
                    }
                }
            }
        }
        private static void OnPingReply(object sender, PingEventArgs e)                                { foreach (var entry in Bot.Plugins) if (entry.Obj.OnPingReply(sender, e)) return; }
        private static void OnPingRequest(object sender, PingEventArgs e)                              { foreach (var entry in Bot.Plugins) if (entry.Obj.OnPingRequest(sender, e)) return; }
        private static async void OnPrivateAction(object sender, PrivateMessageEventArgs e) {
			foreach (var entry in Bot.Plugins) if (entry.Obj.OnPrivateAction(sender, e)) return;
			if (await Bot.CheckTriggers(e.Sender, e.Sender, "ACTION " + e.Message)) return;
		}
		private static void OnPrivateCTCP(object sender, PrivateMessageEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnPrivateCTCP(sender, e)) return;
            Bot.OnCTCPMessage((IrcClient) sender, e.Sender.Nickname, e.Message);
        }
        private static async void OnPrivateMessage(object sender, PrivateMessageEventArgs e) {
            foreach (var entry in Bot.Plugins) {
                if (entry.Obj.OnPrivateMessage(sender, e)) return;
            }
			if (await Bot.CheckCommands(e.Sender, e.Sender, e.Message)) return;
			if (await Bot.CheckTriggers(e.Sender, e.Sender, e.Message)) return;
			Bot.NickServCheck((IrcClient) sender, e.Sender, e.Message);
        }
        private static void OnPrivateNotice(object sender, PrivateMessageEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnPrivateNotice(sender, e)) return;
            Bot.NickServCheck((IrcClient) sender, e.Sender, e.Message);
        }
        private static void OnRawLineReceived(object sender, IrcLineEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnRawLineReceived(sender, e)) return;

            IrcClient client = (IrcClient) sender;

            switch (e.Line.Message) {
                case Replies.RPL_WHOSPCRPL:
                    if (e.Line.Parameters.Length == 4 && e.Line.Parameters[1] == "1") {  // This identifies our WHOX request.
                        IrcUser user;
                        if (client.Users.TryGetValue(e.Line.Parameters[2], out user)) {
							if (e.Line.Parameters[3] == "0")
								client.ReceivedLine(":" + user.ToString() + " ACCOUNT *");
                            else
								client.ReceivedLine(":" + user.ToString() + " ACCOUNT " + e.Line.Parameters[3]);
                        }
                    }
                    break;
                case Replies.RPL_NOWON:
                    Identification id;
                    if (Bot.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out id))
                        id.Monitoring = true;
                    break;
                case Replies.RPL_LOGOFF:
                    Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
                    break;
                case Replies.RPL_NOWOFF:
                    Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
                    break;
                case Replies.RPL_WATCHOFF:
                    if (Bot.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out id)) {
                        id.Monitoring = false;
                        if (id.Channels.Count == 0) Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
                    }
                    break;
            }
        }
        private static void OnRawLineSent(object sender, RawLineEventArgs e)                           { foreach (var entry in Bot.Plugins) if (entry.Obj.OnRawLineSent(sender, e)) return; }
        private static void OnRawLineUnhandled(object sender, IrcLineEventArgs e)                      { foreach (var entry in Bot.Plugins) if (entry.Obj.OnRawLineUnhandled(sender, e)) return; }
        private static void OnRegistered(object sender, RegisteredEventArgs e)                         { foreach (var entry in Bot.Plugins) if (entry.Obj.OnRegistered(sender, e)) return; }
        private static void OnServerError(object sender, ServerErrorEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnServerError(sender, e)) return; }
        private static void OnServerNotice(object sender, PrivateMessageEventArgs e)                   { foreach (var entry in Bot.Plugins) if (entry.Obj.OnServerNotice(sender, e)) return; }
        private static void OnStateChanged(object sender, StateEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnStateChanged(sender, e)) return;

            IrcClient client = (IrcClient) sender;

            if (e.NewState == IrcClientState.Online) {
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    if (clientEntry.Client == client) {
                        // Identify with NickServ.
                        if (clientEntry.NickServ != null) {
                            Match match = null;
                            if (client.Me.Account == null && (clientEntry.NickServ.AnyNickname || clientEntry.NickServ.RegisteredNicknames.Contains(client.Me.Nickname))) {
                                // Identify to NickServ.
                                match = Regex.Match(clientEntry.NickServ.Hostmask, "^([A-}]+)(?![^!])");
                                Bot.NickServIdentify(clientEntry, match.Success ? match.Groups[1].Value : "NickServ");
                            }

                            // If we're not on our main nickname, use the GHOST command.
                            if (clientEntry.NickServ.UseGhostCommand && client.Me.Nickname != clientEntry.Nicknames[0]) {
                                if (match == null) match = Regex.Match(clientEntry.NickServ.Hostmask, "^([A-}]+)(?![^!])");
                                client.Send(clientEntry.NickServ.GhostCommand.Replace("$target", match.Success ? match.Groups[1].Value : "NickServ")
                                                                             .Replace("$nickname", clientEntry.NickServ.RegisteredNicknames[0])
                                                                             .Replace("$password", clientEntry.NickServ.Password));
                                client.Send("NICK {0}", clientEntry.Nicknames[0]);
                            }
                        }

                        // Join channels.
                        AutoJoin(clientEntry);
                        break;
                    }
                }

            }
        }
        private static void OnUserDisappeared(object sender, IrcUserEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnUserDisappeared(sender, e)) return; }
        private static void OnUserModesGet(object sender, UserModesEventArgs e)                        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnUserModesGet(sender, e)) return; }
        private static void OnUserModesSet(object sender, UserModesEventArgs e)                        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnUserModesSet(sender, e)) return; }
        private static void OnUserQuit(object sender, QuitEventArgs e) {
            foreach (var entry in Bot.Plugins) if (entry.Obj.OnUserQuit(sender, e)) return;
            string key = ((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Bot.Identifications.Remove(key);

            foreach (var entry in Bot.Clients) {
                if (entry.Client == sender) {
                    if (((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, entry.Nicknames[0]))
                        ((IrcClient) sender).Send("NICK {0}", entry.Nicknames[0]);
                    break;
                }
            }
        }
        private static void OnValidateCertificate(object sender, ValidateCertificateEventArgs e)       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnValidateCertificate(sender, e)) return; }
        private static void OnWallops(object sender, PrivateMessageEventArgs e)                        { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWallops(sender, e)) return; }
        private static void OnWhoIsAuthenticationLine(object sender, WhoisAuthenticationEventArgs e)   { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsAuthenticationLine(sender, e)) return; }
        private static void OnWhoIsChannelLine(object sender, WhoisChannelsEventArgs e)                { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsChannelLine(sender, e)) return; }
        private static void OnWhoIsEnd(object sender, WhoisEndEventArgs e)                             { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsEnd(sender, e)) return; }
        private static void OnWhoIsHelperLine(object sender, WhoisOperEventArgs e)                     { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsHelperLine(sender, e)) return; }
        private static void OnWhoIsIdleLine(object sender, WhoisIdleEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsIdleLine(sender, e)) return; }
        private static void OnWhoIsNameLine(object sender, WhoisNameEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsNameLine(sender, e)) return; }
        private static void OnWhoIsOperLine(object sender, WhoisOperEventArgs e)                       { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsOperLine(sender, e)) return; }
        private static void OnWhoIsRealHostLine(object sender, WhoisRealHostEventArgs e)               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsRealHostLine(sender, e)) return; }
        private static void OnWhoIsServerLine(object sender, WhoisServerEventArgs e)                   { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoIsServerLine(sender, e)) return; }
        private static void OnWhoList(object sender, WhoListEventArgs e)                               { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoList(sender, e)) return; }
        private static void OnWhoWasEnd(object sender, WhoisEndEventArgs e)                            { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoWasEnd(sender, e)) return; }
        private static void OnWhoWasNameLine(object sender, WhoisNameEventArgs e)                      { foreach (var entry in Bot.Plugins) if (entry.Obj.OnWhoWasNameLine(sender, e)) return; }
        #endregion

        private static async Task AutoJoin(ClientEntry client) {
            if (client.Client.Me.Account == null) await Task.Delay(3000);
            if (client.Client.State == IrcClientState.Online) {
                foreach (AutoJoinChannel channel in client.AutoJoin)
                    if (channel.Key == null)
                        client.Client.Send("JOIN {0}", channel.Channel);
                    else
                        client.Client.Send("JOIN {0} {1}", channel.Key);
            }
        }
    }
}
