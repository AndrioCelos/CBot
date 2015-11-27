/* General to-do list:
 *   TODO: Spam proof commands.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

using IRC;


namespace CBot {
    /// <summary>
    /// The main class of CBot.
    /// </summary>
    public static class Bot {
        /// <summary>Returns the version of the bot, as returned to a CTCP VERSION request.</summary>
        public static string ClientVersion { get; private set; }
        /// <summary>Returns the version of the bot.</summary>
        public static Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>The list of IRC connections the bot has.</summary>
        public static List<ClientEntry> Clients = new List<ClientEntry>();
        /// <summary>The list of loaded plugins.</summary>
        public static Dictionary<string, PluginEntry> Plugins = new Dictionary<string, PluginEntry>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The list of users who are identified.</summary>
        public static Dictionary<string, Identification> Identifications = new Dictionary<string, Identification>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The list of user accounts that are known to the bot.</summary>
        public static Dictionary<string, Account> Accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The list of default command prefixes. A command line can start with any of these if not in a channel that has its own set.</summary>
        public static string[] DefaultCommandPrefixes = new string[] { "!" };
        /// <summary>The collection of channel command prefixes. The keys are channel names in the form NetworkName/#channel, and the corresponding value is the array of command prefixes for that channel.</summary>
        public static Dictionary<string, string[]> ChannelCommandPrefixes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        internal static string[] dNicknames = new string[] { "CBot" };
        internal static string dUsername = "CBot";
        internal static string dFullName = "CBot by Andrio Celos";
        internal static string dUserInfo = "CBot by Andrio Celos";
        internal static string dAvatar = null;

        internal static string ConfigPath = "Config";
        internal static string LanguagesPath = "Languages";
        internal static string PluginsPath = "plugins";
        internal static string Language = "Default";

        private static bool ConfigFileFound;
        private static bool UsersFileFound;
        private static bool PluginsFileFound;
        private static Random rng;

        /// <summary>The minimum compatible plugin API version with this version of CBot.</summary>
        public static readonly Version MinPluginVersion = new Version(3, 2);

        private static readonly Regex commandMaskRegex  = new Regex("^((?:PASS|AUTHENTICATE|OPER|DIE|RESTART) *:?).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex commandMaskRegex2 = new Regex("^((?:PRIVMSG *)(?:NICKSERV|CHANSERV|NS|CS) *:?(?:ID(?:ENTIFY)?|GHOST|REGAIN|REGISTER) *).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);


        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(string channel) {
            string[] prefixes;
            if (!Bot.ChannelCommandPrefixes.TryGetValue(channel, out prefixes))
                prefixes = Bot.DefaultCommandPrefixes;
            return prefixes;
        }
        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="client">The IRC connection to the network on which the channel to check is.</param>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(ClientEntry client, string channel) {
            if (client == null) {
                return Bot.GetCommandPrefixes(channel);
            } else {
                return Bot.GetCommandPrefixes(client.Name + "/" + channel);
            }
        }
        /// <summary>Returns the command prefixes in use in a specified channel.</summary>
        /// <param name="client">The IRC connection to the network on which the channel to check is.</param>
        /// <param name="channel">The name of the channel to check.</param>
        /// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
        public static string[] GetCommandPrefixes(IRCClient client, string channel) {
            if (client == null) {
                return Bot.GetCommandPrefixes(channel);
            } else {
                return Bot.GetCommandPrefixes(client.Extensions.NetworkName + "/" + channel);
            }
        }

        /// <summary>
        /// Creates and adds a new IRCClient object, but does not connect to any IRC networks.
        /// </summary>
        /// <param name="name">The IRC network name.</param>
        /// <param name="address">The address to connect to.</param>
        /// <param name="port">The remote port number to connect on.</param>
        /// <param name="nicknames">A list of nicknames to use on IRC, in order of preference.</param>
        /// <param name="username">The identd username to use on IRC.</param>
        /// <param name="fullName">The full name to use on IRC.</param>
        /// <returns>The new ClientEntry object.</returns>
        public static ClientEntry NewClient(string name, string address, int port, string[] nicknames, string username, string fullName) {
            IRCClient newClient = new IRCClient(new IRCLocalUser(nicknames[0], username, fullName), name);
            ClientEntry newEntry = new ClientEntry(name, address, port, newClient);

            Bot.SetUpClientEvents(newClient);
            Bot.Clients.Add(newEntry);
            return newEntry;
        }

        public static ClientEntry GetClientEntry(IRCClient client) {
            return Clients.FirstOrDefault(c => c.Client == client);
        }

        /// <summary>Adds CBot's event handlers to an IRCClient object. This can be called by plugins creating their own IRCClient objects.</summary>
        /// <param name="newClient">The IRCClient object to add event handlers to.</param>
        public static void SetUpClientEvents(IRCClient newClient) {
            newClient.RawLineReceived += delegate(object sender, IRCLineEventArgs e) {
                ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKGREEN>>%cDKGRAY {1}%r", ((IRCClient) sender).NetworkName, e.Data);
            };
            newClient.RawLineSent += delegate(object sender, RawEventArgs e) {
                Match m;
                m = commandMaskRegex.Match(e.Data);
                if (!m.Success) m = commandMaskRegex2.Match(e.Data);
                if (m.Success)
                    ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}***%r", ((IRCClient) sender).NetworkName, m.Groups[1]);
                else
                    ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}%r", ((IRCClient) sender).NetworkName, e.Data);
            };

            newClient.AwayCancelled += Bot.OnAwayCancelled;
            newClient.AwaySet += Bot.OnAwaySet;
            newClient.BanList += Bot.OnBanList;
            newClient.BanListEnd += Bot.OnBanListEnd;
            newClient.ChannelAction += Bot.OnChannelAction;
            newClient.ChannelAdmin += Bot.OnChannelAdmin;
            newClient.ChannelBan += Bot.OnChannelBan;
            newClient.ChannelTimestamp += Bot.OnChannelTimestamp;
            newClient.ChannelCTCP += Bot.OnChannelCTCP;
            newClient.ChannelDeAdmin += Bot.OnChannelDeAdmin;
            newClient.ChannelDeHalfOp += Bot.OnChannelDeHalfOp;
            newClient.ChannelDeHalfVoice += Bot.OnChannelDeHalfVoice;
            newClient.ChannelDeOp += Bot.OnChannelDeOp;
            newClient.ChannelDeOwner += Bot.OnChannelDeOwner;
            newClient.ChannelDeVoice += Bot.OnChannelDeVoice;
            newClient.ChannelExempt += Bot.OnChannelExempt;
            newClient.ChannelHalfOp += Bot.OnChannelHalfOp;
            newClient.ChannelHalfVoice += Bot.OnChannelHalfVoice;
            newClient.ChannelInviteExempt += Bot.OnChannelInviteExempt;
            newClient.ChannelJoin += Bot.OnChannelJoin;
            newClient.ChannelJoinDenied += Bot.OnChannelJoinDenied;
            newClient.ChannelKick += Bot.OnChannelKick;
            newClient.ChannelMessage += Bot.OnChannelMessage;
            newClient.ChannelMessageDenied += Bot.OnChannelMessageDenied;
            newClient.ChannelModeSet += Bot.OnChannelModeSet;
            newClient.ChannelModeUnhandled += Bot.OnChannelModeUnhandled;
            newClient.ChannelModesSet += Bot.OnChannelModesSet;
            newClient.ChannelModesGet += Bot.OnChannelModesGet;
            newClient.ChannelNotice += Bot.OnChannelNotice;
            newClient.ChannelOp += Bot.OnChannelOp;
            newClient.ChannelOwner += Bot.OnChannelOwner;
            newClient.ChannelPart += Bot.OnChannelPart;
            newClient.ChannelQuiet += Bot.OnChannelQuiet;
            newClient.ChannelRemoveExempt += Bot.OnChannelRemoveExempt;
            newClient.ChannelRemoveInviteExempt += Bot.OnChannelRemoveInviteExempt;
            newClient.ChannelRemoveKey += Bot.OnChannelRemoveKey;
            newClient.ChannelRemoveLimit += Bot.OnChannelRemoveLimit;
            newClient.ChannelSetKey += Bot.OnChannelSetKey;
            newClient.ChannelSetLimit += Bot.OnChannelSetLimit;
            newClient.ChannelTopic += Bot.OnChannelTopic;
            newClient.ChannelTopicChange += Bot.OnChannelTopicChange;
            newClient.ChannelTopicStamp += Bot.OnChannelTopicStamp;
            newClient.ChannelUnBan += Bot.OnChannelUnBan;
            newClient.ChannelUnQuiet += Bot.OnChannelUnQuiet;
            newClient.ChannelVoice += Bot.OnChannelVoice;
            newClient.PrivateCTCP += Bot.OnPrivateCTCP;
            newClient.Disconnected += Bot.OnDisconnected;
            newClient.Exception += Bot.OnException;
            newClient.ExemptList += Bot.OnExemptList;
            newClient.ExemptListEnd += Bot.OnExemptListEnd;
            newClient.Invite += Bot.OnInvite;
            newClient.InviteSent += Bot.OnInviteSent;
            newClient.InviteExemptList += Bot.OnInviteExemptList;
            newClient.InviteExemptListEnd += Bot.OnInviteExemptListEnd;
            newClient.Killed += Bot.OnKilled;
            newClient.ChannelList += Bot.OnChannelList;
            newClient.ChannelListEnd += Bot.OnChannelListEnd;
            newClient.MOTD += Bot.OnMOTD;
            newClient.Names += Bot.OnNames;
            newClient.NamesEnd += Bot.OnNamesEnd;
            newClient.NicknameChange += Bot.OnNicknameChange;
            newClient.NicknameChangeFailed += Bot.OnNicknameChangeFailed;
            newClient.NicknameInvalid += Bot.OnNicknameInvalid;
            newClient.NicknameTaken += Bot.OnNicknameTaken;
            newClient.PrivateNotice += Bot.OnPrivateNotice;
            newClient.PingRequest += Bot.OnPingRequest;
            newClient.PingReply += Bot.OnPingReply;
            newClient.PrivateMessage += Bot.OnPrivateMessage;
            newClient.PrivateAction += Bot.OnPrivateAction;
            newClient.UserQuit += Bot.OnUserQuit;
            newClient.RawLineReceived += Bot.OnRawLineReceived;
            newClient.RawLineSent += Bot.OnRawLineSent;
            newClient.UserModesGet += Bot.OnUserModesGet;
            newClient.UserModesSet += Bot.OnUserModesSet;
            newClient.Wallops += Bot.OnWallops;
            newClient.ServerNotice += Bot.OnServerNotice;
            newClient.ServerError += Bot.OnServerError;
            newClient.StateChanged += Bot.OnStateChanged;
            newClient.WhoList += Bot.OnWhoList;
            newClient.WhoIsAuthenticationLine += Bot.OnWhoIsAuthenticationLine;
            newClient.WhoIsAwayLine += Bot.OnWhoIsAwayLine;
            newClient.WhoIsChannelLine += Bot.OnWhoIsChannelLine;
            newClient.WhoIsEnd += Bot.OnWhoIsEnd;
            newClient.WhoIsIdleLine += Bot.OnWhoIsIdleLine;
            newClient.WhoIsNameLine += Bot.OnWhoIsNameLine;
            newClient.WhoIsOperLine += Bot.OnWhoIsOperLine;
            newClient.WhoIsHelperLine += Bot.OnWhoIsHelperLine;
            newClient.WhoIsRealHostLine += Bot.OnWhoIsRealHostLine;
            newClient.WhoIsServerLine += Bot.OnWhoIsServerLine;
            newClient.WhoWasNameLine += Bot.OnWhoWasNameLine;
            newClient.WhoWasEnd += Bot.OnWhoWasEnd;
        }

        /// <summary>The program's entry point.</summary>
        public static void Main() {
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

            Console.ForegroundColor = ConsoleColor.Gray;

            // Add the console client.
            ConsoleConnection consoleClient = new ConsoleConnection();
            Bot.Clients.Add(new ClientEntry("!Console", "!Console", 0, consoleClient) { SaveToConfig = false });
            SetUpClientEvents(consoleClient);

            Console.Write("Loading configuration file...");
            if (File.Exists("CBotConfig.ini")) {
                Bot.ConfigFileFound = true;
                try {
                    Bot.LoadConfig();
                    Console.WriteLine(" OK");
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(" %cREDFailed%r");
                    ConsoleUtils.WriteLine("%cREDI couldn't load the configuration file: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(2);
                }
                Bot.Clients[0].Client.Me.Nickname = dNicknames[0];
            } else {
                ConsoleUtils.WriteLine(" %cBLUEFile CBotConfig.ini is missing.%r");
            }

            Console.Write("Loading user configuration file...");
            if (File.Exists("CBotUsers.ini")) {
                Bot.UsersFileFound = true;
                try {
                    Bot.LoadUsers();
                    Console.WriteLine(" OK");
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(" %cREDFailed%r");
                    ConsoleUtils.WriteLine("%cREDI couldn't load the user configuration file: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress any key to continue, or close this window to cancel initialisation . . .");
                    Console.ReadKey(true);
                }
            } else {
                ConsoleUtils.WriteLine(" %cBLUEFile CBotUsers.ini is missing.%r");
            }

            Console.Write("Loading plugins...");
            if (File.Exists("CBotPlugins.ini")) {
                Bot.PluginsFileFound = true;
                Console.WriteLine();
                try {
                    Bot.LoadPlugins();
                } catch (Exception ex) {
                    Console.WriteLine();
                    ConsoleUtils.WriteLine("%cREDI couldn't load the plugins: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(2);
                }
            } else {
                ConsoleUtils.WriteLine(" %cBLUEFile CBotPlugins.ini is missing.%r");
            }
            Bot.FirstRun();
            if (!Bot.PluginsFileFound) Bot.LoadPlugins();

            foreach (ClientEntry client in Bot.Clients) {
                try {
                    if (client.Nicknames == null) client.Nicknames = Bot.dNicknames;
                    if (client.Name != "!Console")
                        ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", client.Client.NetworkName, client.Address, client.Port);
                    client.Client.Connect(client.Address, client.Port);
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", client.Client.NetworkName, ex.Message);
                    client.StartReconnect();
                }
            }

            while (true) {
                string input = Console.ReadLine();
                string[] fields = input.Split(new char[] { ' ' });
                if (fields.Length == 0) continue;

                try {
                    switch (fields[0].ToUpper()) {
                        case "LOAD":
                            // TODO: Fix this after fixing LoadPlugin.
                            break;
                        case "SEND":
                            Bot.Clients[int.Parse(fields[1])].Client.Send(string.Join(" ", fields.Skip(2)));
                            break;
                        case "CONNECT":
                            ClientEntry client = Bot.NewClient(fields[1], fields[1],
                                fields.Length >= 3 ? int.Parse((fields[2].StartsWith("+") ? fields[2].Substring(1) : fields[2])) : 6667,
                                fields.Length >= 4 ? new string[] { fields[3] } : dNicknames,
                                fields.Length >= 5 ? fields[4] : dUsername,
                                fields.Length >= 6 ? string.Join(" ", fields.Skip(5)) : dFullName);
                            if (fields.Length >= 3 && fields[2].StartsWith("+")) client.Client.SSL = true;
                            try {
                                ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", client.Client.NetworkName, client.Address, client.Port);
                                client.Client.Connect(client.Address, client.Port);
                            } catch (Exception ex) {
                                ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", client.Client.NetworkName, ex.Message);
                                client.StartReconnect();
                            }
                            break;
                        case "DIE":
                            foreach (ClientEntry _client in Bot.Clients) {
                                if (_client.Client.State >= IRCClientState.Registering)
                                    _client.Client.Send("QUIT :{0}", fields.Length >= 2 ? string.Join(" ", fields.Skip(1)) : "Shutting down.");
                            }
                            Thread.Sleep(2000);
                            foreach (ClientEntry _client in Bot.Clients) {
                                if (_client.Client.State >= IRCClientState.Registering)
                                    _client.Client.Disconnect();
                            }
                            Environment.Exit(0);
                            break;
                        case "ENTER":
                            foreach (ClientEntry _client in Bot.Clients) {
                                if (_client.Client is ConsoleConnection)
                                    ((ConsoleConnection) _client.Client).Put(string.Join(" ", fields.Skip(1)));
                            }
                            break;
                        default:
                            foreach (ClientEntry _client in Bot.Clients) {
                                if (_client.Client is ConsoleConnection)
                                    ((ConsoleConnection) _client.Client).Put(input);
                            }
                            break;
                    }
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cREDThere was a problem processing your request: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cDKRED" + ex.StackTrace + "%r");
                }
            }
        }

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
                Bot.dNicknames = input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string nickname in Bot.dNicknames) {
                    if (nickname[0] >= '0' && nickname[0] <= '9') {
                        Console.WriteLine("A nickname can't begin with a digit.");
                        Bot.dNicknames = null;
                        break;
                    }
                    foreach (char c in nickname) {
                        if ((c < 'A' || c > '}') && (c < '0' || c > '9') && c != '-') {
                            Console.WriteLine("'" + nickname + "' contains invalid characters.");
                            Bot.dNicknames = null;
                            break;
                        }
                    }
                }
            } while (Bot.dNicknames == null);

            do {
                Console.Write("Ident username: ");
                Bot.dUsername = Console.ReadLine();
                foreach (char c in Bot.dUsername) {
                    if ((c < 'A' || c > '}') && (c < '0' && c > '9') && c != '-') {
                        Console.WriteLine("That username contains invalid characters.");
                        Bot.dUsername = null;
                        break;
                    }
                }
            } while (Bot.dUsername == string.Empty);

            do {
                Console.Write("Full name: ");
                Bot.dFullName = Console.ReadLine();
            } while (Bot.dFullName == string.Empty);

            Console.Write("User info for CTCP (blank entry for the default): ");
            Bot.dUserInfo = Console.ReadLine();
            if (Bot.dUserInfo == "") Bot.dUserInfo = "CBot by Andrio Celos";

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

                ClientEntry client = Bot.NewClient(networkName, address, port, Bot.dNicknames, Bot.dUsername, Bot.dFullName);
                client.Client.Password = password;
                client.Client.SSL = tls;
                client.Client.AllowInvalidCertificate = acceptInvalidCertificate;
                client.NickServ = nickServ;
                client.AutoJoin.AddRange(autoJoinChannels);
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
            ConsoleUtils.WriteLine("%cWHITEB%r: With a NickServ account");
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
                    prompt = "What is your NickServ account name? ";
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

                        ConsoleUtils.WriteLine("Thank you. To log in from IRC, enter %cWHITE/msg {0} !id <password>%r or %cWHITE/msg {0} !id {1} <password>%r, without the brackets.", Bot.Nickname(), accountName);
                        break;
                    }
                    break;
                case 1:
                    // Add the account and give all permissions.
                    Bot.Accounts.Add("$a:" + accountName, new Account { Permissions = new string[] { "*" } });
                    ConsoleUtils.WriteLine("Thank you. Don't forget to log in to your NickServ account.", Bot.Nickname(), accountName);
                    break;
                case 2:
                    // Add the account and give all permissions.
                    Bot.Accounts.Add(accountName, new Account { Permissions = new string[] { "*" } });
                    ConsoleUtils.WriteLine("Thank you. Don't forget to enable your vHost, if needed.", Bot.Nickname(), accountName);
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
                    var assembly = Assembly.LoadFrom(file);

                    foreach (Type type in assembly.GetTypes()) {
                        if (typeof(Plugin).IsAssignableFrom(type)) {
                            // Check the version attribute.
                            var attribute = type.GetCustomAttribute<APIVersionAttribute>(false);

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
                    Console.Write("What channels should this instancebe active in? (Blank entry for all channels) ");
                    input = Console.ReadLine().Trim();
                    if (input.Length == 0) {
                        channels = new string[] { "*" };
                    } else {
                        channels = input.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    }

                    selected.Add(new Tuple<string, string, string[]>(key, file, channels));
                }
            } while (!done);

            // Write out the config file.
            using (var writer = new StreamWriter(File.Open("CBotPlugins.ini", FileMode.CreateNew, FileAccess.Write))) {
                foreach (var entry in selected) {
                    writer.WriteLine("[" + entry.Item1 + "]");
                    writer.WriteLine("Filename=" + entry.Item2);
                    writer.WriteLine("Channels=" + string.Join(",", entry.Item3));
                    writer.WriteLine();
                }
            }

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

        /// <summary>Loads a plugin and adds it to CBot's list of active plugins.</summary>
        /// <param name="Key">A key to identify the newly loaded plugin.</param>
        /// <param name="Filename">The file to load.</param>
        /// <param name="Channels">A list of channels in which this plugin should be active.</param>
        /// <exception cref="System.ArgumentException">A plugin with the specified key is already loaded.</exception>
        /// <exception cref="CBot.InvalidPluginException">The plugin could not be constructed.</exception>
        public static void LoadPlugin(string Key, string Filename, params string[] Channels) {
            Assembly assembly;
            AssemblyName assemblyName;
            Type pluginType = null;
            string errorMessage = null;

            if (Bot.Plugins.ContainsKey(Key)) throw new ArgumentException(string.Format("A plugin with key {0} is already loaded.", Key), "key");

            ConsoleUtils.Write("  Loading plugin %cWHITE" + Key + "%r...");
            int x = Console.CursorLeft; int y = Console.CursorTop; int x2; int y2;
            Console.WriteLine();

            try {
                assembly = Assembly.LoadFrom(Filename);
                assemblyName = assembly.GetName();

                foreach (Type type in assembly.GetTypes()) {
                    if (typeof(Plugin).IsAssignableFrom(type)) {
                        pluginType = type;
                        break;
                    }
                }
                if (pluginType == null) {
                    errorMessage = "Invalid – no valid plugin class.";
                    throw new InvalidPluginException(Filename, string.Format("The file '{0}' does not contain a class that inherits from the base plugin class.", Filename));
                }

                Version pluginVersion = null;
                foreach (APIVersionAttribute attribute in pluginType.GetCustomAttributes(typeof(APIVersionAttribute), false)) {
                    if (pluginVersion == null || pluginVersion < attribute.Version)
                        pluginVersion = attribute.Version;
                }
                if (pluginVersion == null) {
                    errorMessage = "Outdated plugin – no API version is specified.";
                    throw new InvalidPluginException(Filename, string.Format("The class '{0}' in '{1}' does not specify the version of CBot for which it was built.", pluginType.Name, Filename));
                } else if (pluginVersion < Bot.MinPluginVersion) {
                    errorMessage = string.Format("Outdated plugin – built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                    throw new InvalidPluginException(Filename, string.Format("The class '{0}' in '{1}' was built for older version {2}.{3}.", pluginType.Name, Filename, pluginVersion.Major, pluginVersion.Minor));
                } else if (pluginVersion > Bot.Version) {
                    errorMessage = string.Format("Outdated bot – the plugin is built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                    throw new InvalidPluginException(Filename, string.Format("The class '{0}' in '{1}' was built for newer version {2}.{3}.", pluginType.Name, Filename, pluginVersion.Major, pluginVersion.Minor));
                }

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
                    plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { Key });
                else if (constructorType == 2)
                    plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { Key, Channels });
                else {
                    errorMessage = "Invalid – no valid constructor on the plugin class.";
                    throw new InvalidPluginException(Filename, string.Format("The class '{0}' in '{1}' does not contain a supported constructor.\n" +
                                                                             "It should be defined as 'public SamplePlugin()'", pluginType.Name, Filename));
                }

                plugin.Key = Key;
                plugin.Channels = Channels ?? new string[0];
                Bot.Plugins.Add(Key, new PluginEntry() { Filename = Filename, Obj = plugin });
                plugin.LoadLanguage();
                plugin.Initialize();

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
                Bot.LogError(Key, "Loading", ex);

                throw ex;
            }
        }

        /// <summary>Loads configuration data from the file CBotConfig.ini if it is present.</summary>
        public static void LoadConfig() {
            if (File.Exists("CBotConfig.ini")) {
                try {
                    StreamReader Reader = new StreamReader("CBotConfig.ini");
                    string Section = ""; string Field; string Value;
                    bool GotNicknames = false; ClientEntry client = null;

                    while (!Reader.EndOfStream) {
                        string s = Reader.ReadLine();
                        if (!Regex.IsMatch(s, @"^(?>\s*);")) {  // Comment check
                            Match Match = Regex.Match(s, @"^\s*\[(.*?)\]?\s*$");
                            if (Match.Success) {
                                Section = Match.Groups[1].Value;
                                if (!Section.Equals("Me", StringComparison.OrdinalIgnoreCase) && !Section.Equals("Prefixes", StringComparison.OrdinalIgnoreCase)) {
                                    string[] ss = Section.Split(new char[] { ':' }, 2);
                                    if (ss.Length > 1) {
                                        ushort port;
                                        if (ushort.TryParse(ss[1], out port) && port != 0)
                                            client = Bot.NewClient(ss[0], ss[0], port, Bot.dNicknames, Bot.dUsername, Bot.dFullName);
                                        else
                                            ConsoleUtils.WriteLine("%cREDPort number for " + ss[0] + " is invalid.");
                                    } else
                                        client = Bot.NewClient(Section, Section, 6667, Bot.dNicknames, Bot.dUsername, Bot.dFullName);
                                }
                                GotNicknames = false;
                            } else {
                                Match = Regex.Match(s, @"^\s*((?>[^=]*))=(.*)$");
                                if (Match.Success) {
                                    Field = Match.Groups[1].Value;
                                    Value = Match.Groups[2].Value;
                                    if (Section.Equals("me", StringComparison.OrdinalIgnoreCase)) {
                                        switch (Field.ToUpper()) {
                                            case "NICKNAME":
                                            case "NICKNAMES":
                                            case "NAME":
                                            case "NAMES":
                                            case "NICK":
                                            case "NICKS":
                                                if (GotNicknames)
                                                    Bot.dNicknames = Bot.dNicknames.Concat(Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
                                                else {
                                                    Bot.dNicknames = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                                    GotNicknames = true;
                                                }
                                                break;
                                            case "USERNAME":
                                            case "USER":
                                            case "IDENTNAME":
                                            case "IDENT":
                                                Bot.dUsername = Value;
                                                break;
                                            case "FULLNAME":
                                            case "REALNAME":
                                            case "GECOS":
                                            case "FULL":
                                                Bot.dFullName = Value;
                                                break;
                                            case "USERINFO":
                                            case "CTCPINFO":
                                            case "INFO":
                                                Bot.dUserInfo = Value;
                                                break;
                                            case "AVATAR":
                                            case "AVATARURL":
                                            case "AVATAR-URL":
                                                dAvatar = Value;
                                                break;
                                        }
                                    } else if (Section.Equals("prefixes", StringComparison.OrdinalIgnoreCase)) {
                                        if (Field.Equals("Default", StringComparison.CurrentCultureIgnoreCase))
                                            DefaultCommandPrefixes = Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        else
                                            ChannelCommandPrefixes.Add(Field, Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                    } else {
                                        if (client.NickServ == null && (
                                            Field.StartsWith("NickServ", StringComparison.OrdinalIgnoreCase) ||
                                            Field.StartsWith("NS", StringComparison.OrdinalIgnoreCase))) {
                                            client.NickServ = new NickServSettings();
                                        }
                                        switch (Field.ToUpper()) {
                                            case "ADDRESS":
                                                string[] ss = Value.Split(new char[] { ':' }, 2);
                                                client.Address = ss[0];
                                                client.Client.Address = ss[0];
                                                if (ss.Length > 1) {
                                                    ushort port;
                                                    if (ushort.TryParse(ss[1], out port) && port != 0) {
                                                        client.Port = port;
                                                        client.Client.Port = port;
                                                    } else
                                                        ConsoleUtils.WriteLine("%cREDPort number for " + ss[0] + " is invalid.");
                                                } else {
                                                    client.Client.Port = 6667;
                                                    client.Port = 6667;
                                                }
                                                break;
                                            case "PASSWORD":
                                            case "PASS":
                                                client.Client.Password = Value;
                                                break;
                                            case "NICKNAME":
                                            case "NICKNAMES":
                                            case "NAME":
                                            case "NAMES":
                                            case "NICK":
                                            case "NICKS":
                                                if (GotNicknames)
                                                    client.Nicknames = client.Nicknames.Concat(Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
                                                else {
                                                    client.Nicknames = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                                    GotNicknames = true;
                                                }
                                                break;
                                            case "USERNAME":
                                            case "USER":
                                            case "IDENTNAME":
                                            case "IDENT":
                                                client.Client.Me.Ident = Value;
                                                break;
                                            case "FULLNAME":
                                            case "REALNAME":
                                            case "GECOS":
                                            case "FULL":
                                                client.Client.Me.FullName = Value;
                                                break;
                                            case "AUTOJOIN":
                                                client.AutoJoin.AddRange(Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(c => new AutoJoinChannel(c)));
                                                break;
                                            case "SSL":
                                            case "USESSL":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    client.Client.SSL = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    client.Client.SSL = false;
                                                }
                                                break;
                                            case "ALLOWINVALIDCERTIFICATE":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    client.Client.AllowInvalidCertificate = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    client.Client.AllowInvalidCertificate = false;
                                                }
                                                break;
                                            case "SASL-USERNAME":
                                                client.Client.SASLUsername = Value;
                                                break;
                                            case "SASL-PASSWORD":
                                                client.Client.SASLPassword = Value;
                                                break;
                                            case "NICKSERV-NICKNAMES":
                                            case "NS-NICKNAMES":
                                                client.NickServ.RegisteredNicknames = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                                break;
                                            case "NICKSERV-PASSWORD":
                                            case "NS-PASSWORD":
                                                client.NickServ.Password = Value;
                                                break;
                                            case "NICKSERV-ANYNICKNAME":
                                            case "NS-ANYNICKNAME:":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    client.NickServ.AnyNickname = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    client.NickServ.AnyNickname = false;
                                                }
                                                break;
                                            case "NICKSERV-USEGHOSTCOMMAND":
                                            case "NS-USEGHOSTCOMMAND":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    client.NickServ.UseGhostCommand = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    client.NickServ.UseGhostCommand = false;
                                                }
                                                break;
                                            case "NICKSERV-IDENTIFYCOMMAND":
                                            case "NS-IDENTIFYCOMMAND":
                                                client.NickServ.IdentifyCommand = Value;
                                                break;
                                            case "NICKSERV-GHOSTCOMMAND":
                                            case "NS-GHOSTCOMMAND":
                                                client.NickServ.GhostCommand = Value;
                                                break;
                                            case "NICKSERV-HOSTMASK":
                                            case "NS-HOSTMASK":
                                                client.NickServ.Hostmask = Value;
                                                break;
                                            case "NICKSERV-REQUESTMASK":
                                            case "NS-REQUESTMASK":
                                                client.NickServ.RequestMask = Value;
                                                break;
                                        }

                                    }
                                }
                            }
                        }
                    }

                    Reader.Close();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve config data from the file: %cRED" + ex.Message + "%r");
                }
            }
        }

        /// <summary>Loads user data from the file CBotUsers.ini if it is present.</summary>
        public static void LoadUsers() {
            if (File.Exists("CBotUsers.ini")) {
                try {
                    StreamReader Reader = new StreamReader("CBotUsers.ini");
                    string Section = null;
                    Account newUser = null;

                    while (!Reader.EndOfStream) {
                        string s = Reader.ReadLine();
                        if (Regex.IsMatch(s, @"^(?>\s*);")) continue;  // Comment check

                        Match Match = Regex.Match(s, @"^\s*\[(?<Section>.*?)\]?\s*$");
                        if (Match.Success) {
                            if (Section != null) Bot.Accounts.Add(Section, newUser);
                            Section = Match.Groups["Section"].Value;
                            if (!Bot.Accounts.ContainsKey(Section)) {
                                newUser = new Account { Permissions = new string[0] };
                            }
                        } else {
                            if (Section != null) {
                                Match = Regex.Match(s, @"^\s*((?>[^=]*))=(.*)$");
                                if (Match.Success) {
                                    string Field = Match.Groups[1].Value;
                                    string Value = Match.Groups[2].Value;
                                    switch (Field.ToUpper()) {
                                        case "PASSWORD":
                                        case "PASS":
                                            newUser.Password = Value;
                                            break;
                                        case "HASHTYPE":
                                            newUser.HashType = (HashType) Enum.Parse(typeof(HashType), Value, true);
                                            break;
                                    }
                                } else if (s.Trim() != "") {
                                    string[] array = newUser.Permissions;
                                    newUser.Permissions = new string[array.Length + 1];
                                    Array.Copy(array, newUser.Permissions, array.Length);
                                    newUser.Permissions[array.Length] = s.Trim();
                                }
                            }
                        }
                    }
                    if (newUser.HashType == HashType.None && newUser.Password != null)
                        // Old format
                        newUser.HashType = (newUser.Password.Length == 128 ? HashType.SHA256Salted : HashType.PlainText);

                    Bot.Accounts.Add(Section, newUser);
                    Reader.Close();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve user data from the file: $k04" + ex.Message + "%r");
                }
            }
        }

        /// <summary>Loads active plugin data from the file CBotPlugins.ini if it is present.</summary>
        public static void LoadPlugins() {
            bool error = false;
            if (File.Exists("CBotPlugins.ini")) {
                try {
                    StreamReader Reader = new StreamReader("CBotPlugins.ini");

                    string Section = "";

                    string Filename = null;
                    string[] Channels = null;
                    string[] MinorChannels = null;
                    string Label = null;

                    while (!Reader.EndOfStream) {
                        string s = Reader.ReadLine();
                        if (Regex.IsMatch(s, @"^(?>\s*);")) continue;  // Comment check

                        Match Match = Regex.Match(s, @"^\s*\[(?<Section>.*?)\]?\s*$");
                        if (Match.Success) {
                            if (Filename != null) {
                                try {
                                    Bot.LoadPlugin(Section, Filename, Channels);
                                } catch (Exception) {
                                    // LoadPlugin already reports the exception.
                                    error = true;
                                }
                            }
                            Section = Match.Groups["Section"].Value;
                        } else {
                            if (Section != "") {
                                Match = Regex.Match(s, @"^\s*((?>[^=]*))=(.*)$");
                                if (Match.Success) {
                                    string Field = Match.Groups[1].Value;
                                    string Value = Match.Groups[2].Value;

                                    switch (Field.ToUpper()) {
                                        case "FILENAME":
                                        case "FILE":
                                            Filename = Value;
                                            break;
                                        case "CHANNELS":
                                        case "MAJOR":
                                            Channels = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                            break;
                                        case "MINORCHANNELS":
                                        case "MINOR":
                                            MinorChannels = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                            break;
                                        case "LABEL":
                                        case "MINORLABEL":
                                            Label = Value;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    if (Filename != null) {
                        try {
                            Bot.LoadPlugin(Section, Filename, Channels);
                        } catch (Exception) {
                            error = true;
                        }
                    }
                    Reader.Close();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve plugin data from the file: %cRED" + ex.Message + "%r");
                    error = true;
                }
            }

            if (error) {
                ConsoleUtils.WriteLine("%cREDSome plugins failed to load.%r");
                ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(2);
            }
        }

        /// <summary>Writes configuration data to the file CBotConfig.ini.</summary>
        public static void SaveConfig() {
            StreamWriter Writer = new StreamWriter("CBotConfig.ini", false);
            Writer.WriteLine("[Me]");
            Writer.WriteLine("Nicknames=" + string.Join(",", Bot.dNicknames));
            Writer.WriteLine("Username=" + Bot.dUsername);
            Writer.WriteLine("FullName=" + Bot.dFullName);
            Writer.WriteLine("UserInfo=" + Bot.dUserInfo);
            if (Bot.dAvatar != null) {
                Writer.WriteLine("Avatar=" + Bot.dAvatar);
            }
            foreach (ClientEntry client in Bot.Clients) {
                if (client.SaveToConfig) {
                    Writer.WriteLine();
                    Writer.WriteLine("[" + client.Name + "]");
                    Writer.WriteLine("Address=" + client.Address + ":" + client.Port);
                    if (client.Client.Password != null)
                        Writer.WriteLine("Password=" + client.Client.Password);
                    if (client.Nicknames != null)
                        Writer.WriteLine("Nicknames=" + string.Join(",", client.Nicknames));
                    Writer.WriteLine("Username=" + client.Client.Me.Ident);
                    Writer.WriteLine("FullName=" + client.Client.Me.FullName);
                    if (client.AutoJoin.Count != 0) {
                        Writer.WriteLine("Autojoin=" + string.Join(",", client.AutoJoin.Select(c => c.Channel)));
                    }
                    Writer.WriteLine("SSL=" + (client.Client.SSL ? "Yes" : "No"));
                    if (client.Client.SASLUsername != null && client.Client.SASLPassword != null) {
                        Writer.WriteLine("SASL-Username=" + client.Client.SASLUsername);
                        Writer.WriteLine("SASL-Password=" + client.Client.SASLPassword);
                    }
                    Writer.WriteLine("AllowInvalidCertificate=" + (client.Client.AllowInvalidCertificate ? "Yes" : "No"));
                    if (client.NickServ != null) {
                        Writer.WriteLine("NickServ-Nicknames=" + string.Join(",", client.NickServ.RegisteredNicknames));
                        Writer.WriteLine("NickServ-Password=" + client.NickServ.Password);
                        Writer.WriteLine("NickServ-AnyNickname=" + (client.NickServ.AnyNickname ? "Yes" : "No"));
                        Writer.WriteLine("NickServ-UseGhostCommand=" + (client.NickServ.UseGhostCommand ? "Yes" : "No"));
                        Writer.WriteLine("NickServ-GhostCommand=" + client.NickServ.GhostCommand);
                        Writer.WriteLine("NickServ-IdentifyCommand=" + client.NickServ.IdentifyCommand);
                        Writer.WriteLine("NickServ-Hostmask=" + client.NickServ.Hostmask);
                        Writer.WriteLine("NickServ-RequestMask=" + client.NickServ.RequestMask);
                    }
                }
            }
            Writer.WriteLine();
            Writer.WriteLine("[Prefixes]");
            Writer.WriteLine("Default=" + string.Join(" ", DefaultCommandPrefixes));
            foreach (KeyValuePair<string, string[]> Connection2 in Bot.ChannelCommandPrefixes) {
                Writer.WriteLine(Connection2.Key + "=" + string.Join(" ", Connection2.Value));
            }
            Writer.Close();
        }

        /// <summary>Writes user data to the file CBotUsers.ini.</summary>
        public static void SaveUsers() {
            StreamWriter Writer = new StreamWriter("CBotUsers.ini", false);
            foreach (KeyValuePair<string, Account> User in Bot.Accounts) {
                Writer.WriteLine("[" + User.Key + "]");
                if (User.Value.HashType != HashType.None) {
                    Writer.WriteLine("HashType=" + User.Value.HashType);
                    Writer.WriteLine("Password=" + User.Value.Password);
                }
                string[] permissions = User.Value.Permissions;
                for (int i = 0; i < permissions.Length; ++i) {
                    string Permission = permissions[i];
                    Writer.WriteLine(Permission);
                }
                Writer.WriteLine();
            }
            Writer.Close();
        }

        /// <summary>Writes active plugin data to the file CBotPlugins.ini.</summary>
        public static void SavePlugins() {
            StreamWriter Writer = new StreamWriter("CBotPlugins.ini", false);
            foreach (KeyValuePair<string, PluginEntry> Plugin in Bot.Plugins) {
                Writer.WriteLine("[" + Plugin.Key + "]");
                Writer.WriteLine("Filename=" + Plugin.Value.Filename);
                bool flag = Plugin.Value.Obj.Channels != null;
                if (flag) {
                    Writer.WriteLine("Channels=" + string.Join(",", Plugin.Value.Obj.Channels));
                }
                Writer.WriteLine();
                Plugin.Value.Obj.OnSave();
            }
            Writer.Close();
        }

        /// <summary>Handles a message from an IRC user. This includes checking for commands.</summary>
        /// <param name="connection">The IRC connection on which the message was received.</param>
        /// <param name="sender">The user sending the message.</param>
        /// <param name="channel">The channel in which the message was received, or the user's nickname if it was private.</param>
        /// <param name="message">The message text.</param>
        /// <returns></returns>
        public static bool CheckMessage(IRCClient connection, IRCUser sender, string channel, string message) {
            string[] fields = message.Split(new char[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length <= 1) return false;

            foreach (string c in Bot.GetCommandPrefixes(connection, channel))
                if (fields[0].StartsWith(c)) {
                    fields[0] = fields[0].Substring(1);
                    break;
                }

            // Check global commands.
            foreach (KeyValuePair<string, PluginEntry> plugin in Bot.Plugins) {
                if (fields[0].Equals(plugin.Key, StringComparison.OrdinalIgnoreCase)) {
                    if (plugin.Value.Obj.RunCommand(connection, sender, channel, fields[1], true))
                        return true;
                }
            }
            return false;
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

            foreach (char c in mask) {
                if (c == '*') exBuilder.Append(".*");
                else if (c == '?') exBuilder.Append(".");
                else exBuilder.Append(Regex.Escape(c.ToString()));
            }
            mask = exBuilder.ToString();

            return Regex.IsMatch(input, mask, RegexOptions.IgnoreCase);
        }

        private static void NickServCheck(IRCClient sender, IRCUser User, string Message) {
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

        private static void OnCTCPMessage(IRCClient Connection, string Sender, string Message) {
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

                    Connection.Send("NOTICE {0} :\u0001FINGER {1}: {3}; idle for {2}.\u0001", Sender, dNicknames[0], readableIdleTime.ToString(), dUserInfo);
                    break;
                case "USERINFO":
                    Connection.Send("NOTICE {0} :\u0001USERINFO {1}\u0001", Sender, dUserInfo);
                    break;
                case "AVATAR":
                    Connection.Send("NOTICE {0} :\u0001AVATAR {1}\u0001", Sender, dAvatar);
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
        public static object Nickname() {
            if (Bot.dNicknames.Length == 0) return "CBot";
            return Bot.dNicknames[0];
        }

        /// <summary>Determines whether a user on IRC has a specified permission.</summary>
        /// <param name="connection">The IRC connection on which the user is.</param>
        /// <param name="channel">The channel in which the user is attempting to perform a command or similar action.</param>
        /// <param name="user">The user's nickname.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>true if permission is null or empty, or if the user has the permission; false otherwise.</returns>
        /// <exception cref="System.ArgumentNullException">connection is null.</exception>
        public static bool UserHasPermission(IRCClient connection, string channel, IRCUser user, string permission) {
            if (connection == null) throw new ArgumentNullException("connection");
            if (permission == null || permission == "") return true;
            return Bot.UserHasPermissionSub(Bot.GetPermissions(connection, channel, user.Nickname), permission);
        }
        /// <summary>Determines whether an account has a specified permission.</summary>
        /// <param name="accountName">The name of the account to check.</param>
        /// <param name="permission">The permission to check for.</param>
        /// <returns>true if permission is null or empty, or if the account has the permission; false otherwise.</returns>
        public static bool UserHasPermission(string accountName, string permission) {
            if (permission == null || permission == "") return true;
            // TODO: check the * account too.
            return Bot.UserHasPermissionSub(Bot.Accounts[accountName].Permissions, permission);
        }

        /// <summary>Returns the list of permissions that a user has.</summary>
        /// <param name="connection">The IRC connection on which the user is.</param>
        /// <param name="channel">The channel in which the user is attempting to perform a command or similar action.</param>
        /// <param name="nickname">The user's nickname.</param>
        /// <returns>An array containing the perimissions the specified user has.</returns>
        /// <exception cref="System.ArgumentNullException">connection is null.</exception>
        // TODO: cache this?
        public static string[] GetPermissions(IRCClient connection, string channel, string nickname) {
            if (connection == null) throw new ArgumentNullException("connection");

            List<string> permissions = new List<string>();
            string accountName = null;

            Identification ID;
            if (Bot.Identifications.TryGetValue(connection.NetworkName + "/" + nickname, out ID))
                accountName = ID.AccountName;

            foreach (KeyValuePair<string, Account> account in Bot.Accounts) {
                bool match = false;
                if (account.Key == "*") match = true;
                else if (account.Key.StartsWith("$")) {
                    string[] fields = account.Key.Split(new char[] { ':' }, 2);
                    string[] fields2;
                    ChannelStatus status = null;

                    switch (fields[0]) {
                        case "$q":
                            status = ChannelStatus.Owner;
                            break;
                        case "$a":
                            status = ChannelStatus.Admin;
                            break;
                        case "$o":
                            status = ChannelStatus.Op;
                            break;
                        case "$h":
                            status = ChannelStatus.Halfop;
                            break;
                        case "$v":
                            status = ChannelStatus.Voice;
                            break;
                        case "$V":
                            status = ChannelStatus.HalfVoice;
                            break;
                        default:
                            match = false;
                            break;
                    }

                    if (status != null) {
                        // Check that the user has the required access on the given "network/channel".
                        IRCClient client = null;

                        if (channel == null) continue;
                        fields2 = fields[1].Split(new char[] { '/' }, 2);
                        if (fields2.Length == 1) fields2 = new string[] { null, fields2[0] };

                        // Find the network.
                        if (fields2[0] != null) {
                            foreach (ClientEntry _client in Bot.Clients) {
                                if ((_client.NetworkName ?? "").Equals(fields2[0], StringComparison.OrdinalIgnoreCase)) {
                                    client = _client.Client;
                                    break;
                                }
                            }
                        }

                        // Find the channel.
                        IRCChannel channel2;
                        IRCChannelUser user;
                        if (client == null) {
                            if (fields2[0] != null) match = false;
                            else {
                                match = false;
                                foreach (ClientEntry _client in Bot.Clients) {
                                    if (_client.Client.Channels.TryGetValue(fields2[1], out channel2) && channel2.Users.TryGetValue(nickname, out user) &&
                                        user.Status >= status) {
                                        match = true;
                                        break;
                                    }
                                }
                            }
                        } else {
                            match = false;
                            if (client.Channels.TryGetValue(fields2[1], out channel2) && channel2.Users.TryGetValue(nickname, out user) &&
                                user.Status >= status) {
                                match = true;
                            }
                        }
                    }

                } else {
                    // Check for a hostmask match.
                    if (account.Key.Contains("@")) {
                        IRCUser user;
                        if (connection.Users.TryGetValue(nickname, out user))
                            match = Bot.MaskCheck(user.ToString(), account.Key);
                    } else
                        match = (accountName != null && account.Key.Equals(accountName, StringComparison.OrdinalIgnoreCase));
                }

                if (match)
                    permissions.AddRange(account.Value.Permissions);
            }

            return permissions.ToArray();
        }
        /// <summary>Returns the list of permissions that an account has.</summary>
        /// <param name="accountName">The name of the account to check.</param>
        /// <returns>An array containing the perimissions the specified account has, or the permissions of * if there is no such account.</returns>
        public static string[] GetPermissions(string AccountName) {
            Account account;
            if (Bot.Accounts.TryGetValue(AccountName, out account)) return account.Permissions.ToArray();
            // Calling ToArray on an array actually creates a deep copy.
            if (Bot.Accounts.TryGetValue("*", out account)) return account.Permissions.ToArray();
            return new string[0];
        }

        /// <summary>Determines whether a list of permissions grants a specified condition, checking wildcards.</summary>
        /// <param name="permissions">The list of permissions to search.</param>
        /// <param name="permission">The permission to search for.</param>
        /// <returns>true if the specified permission is in the given list; false otherwise.</returns>
        public static bool UserHasPermissionSub(IEnumerable<string> permissions, string permission) {
            int score = 0;

            string[] needleFields = permission.Split(new char[] { '.' });
            bool IRCPermission = needleFields[0].Equals("irc", StringComparison.OrdinalIgnoreCase);

            foreach (string permission2 in permissions) {
                string[] hayFields;
                if (permission2 == "*" && !IRCPermission) {
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
        /// <summary>Returns one of the parameters, selected psuedo-randomly by initialising the random number generator to a specified seed.</summary>
        /// <param name="seed">The seed to use to select a parameter.</param>
        /// <param name="args">The list of parameters to choose between.</param>
        /// <returns>One of the parameters, chosen at random.</returns>
        /// <exception cref="System.ArgumentNullException">args is null.</exception>
        /// <exception cref="System.ArgumentException">args is empty.</exception>
        public static T Choose<T>(int seed, params T[] args) {
            if (args == null) throw new ArgumentNullException("args");
            if (args.Length == 0) throw new ArgumentException("args must not be empty.");
            Random rng = new Random(seed);
            return args[rng.Next(args.Length)];
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

        internal static void LogConnectionError(IRCClient Server, Exception ex) {
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
        public static bool Identify(string target, string accountName, string password, out Identification identification) {
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
        public static bool Identify(string target, string accountName, string password, out Identification identification, out string message) {
            bool success; Account account;

            if (!Bot.Accounts.TryGetValue(accountName, out account)) {
                // No such account.
                message = "The account name or password is invalid.";
                identification = null;
                success = false;
            } else {
                if (Bot.Identifications.TryGetValue(target, out identification) && identification.AccountName == accountName) {
                    // The user is already identified.
                    message = string.Format("You are already identified as \u000312{0}\u000F.", identification.AccountName);
                    success = false;
                } else {
                    if (account.VerifyPassword(password)) {
                        identification = new Identification { AccountName = accountName, Channels = new List<string>() };
                        Bot.Identifications.Add(target, identification);
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
        /// <param name="connection">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="message">The message to send.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IRCClient connection, string channel, string message, SayOptions options) {
            if (message == null || message == "") return;

            if ((options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(message[0]);
                if (c != message[0]) message = c + message.Substring(1);
            }

            bool notice = false;
            if (connection.IsChannel(channel)) {
                if ((options & (SayOptions) 1) != 0) {
                    channel = "@" + channel;
                    notice = true;
                }
            } else
                notice = true;
            if ((options & SayOptions.NoticeAlways) != 0)
                notice = true;
            if ((options & SayOptions.NoticeNever) != 0)
                notice = false;

            foreach (string line in message.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)) {
                connection.Send("{0} {1} :{2}", notice ? "NOTICE" : "PRIVMSG", channel, line);
                //Thread.Sleep(600);
            }
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="connection">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="message">The message to send.</param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IRCClient connection, string channel, string message) {
            Bot.Say(connection, channel, message, 0);
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="connection">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="format">The format of the message to send, as per string.Format.</param>
        /// <param name="args">The parameters to include in the message text./param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IRCClient connection, string channel, string format, params object[] args) {
            Bot.Say(connection, channel, string.Format(format, args), 0);
        }
        /// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
        /// <param name="connection">The IRC connection to send to.</param>
        /// <param name="channel">The name of the channel or user to send to.</param>
        /// <param name="format">The format of the message to send, as per string.Format.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <param name="args">The parameters to include in the message text./param>
        /// <remarks>
        ///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
        ///   can override this behaviour.
        /// </remarks>
        public static void Say(this IRCClient connection, string channel, string format, SayOptions options, params object[] args) {
            Bot.Say(connection, channel, string.Format(format, args), options);
        }

        /// <summary>Replaces commands prefixed with a ! in the given text with the correct command prefix.</summary>
        /// <param name="text">The text to edit.</param>
        /// <param name="client">The IRC connection on which the channel to use a command prefix for is.</param>
        /// <param name="channel">The channel to use a command prefix for.</param>
        /// <returns>A copy of text with commands prefixed with a ! replaced with the correct command prefix.</returns>
        /// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by a !.</remarks>
        public static string ReplaceCommands(this string text, IRCClient client, string channel) {
            return Bot.ReplaceCommands(text, client, channel, "!");
        }
        /// <summary>Replaces commands in the given text with the correct command prefix.</summary>
        /// <param name="text">The text to edit.</param>
        /// <param name="client">The IRC connection on which the channel to use a command prefix for is.</param>
        /// <param name="channel">The channel to use a command prefix for.</param>
        /// <param name="prefix">The command prefix to replace in the text.</param>
        /// <returns>A copy of text with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
        /// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
        public static string ReplaceCommands(this string text, IRCClient client, string channel, string prefix) {
            string replace = Bot.GetCommandPrefixes(client, channel)[0].ToString();
            if (replace == "$") replace = "$$";
            return Regex.Replace(text, @"(?<=(?:^|[\s\x00-\x20])(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?)" + Regex.Escape(prefix) + @"(?=(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?\w)", replace);
        }

        private static bool EventCheck(IRCClient client, string channel, string procedureName, params object[] parameters) {
            foreach (KeyValuePair<string, PluginEntry> i in Bot.Plugins) {
                if (channel == null || !client.IsChannel(channel) || i.Value.Obj.IsActiveChannel(client, channel)) {
                    MethodInfo method = i.Value.Obj.GetType().GetMethod(procedureName);
                    if (method == null) throw new MissingMethodException("No such procedure was found.");
                    try {
                        if ((bool) method.Invoke(i.Value.Obj, parameters))
                            return true;
                    } catch (Exception ex) {
                        Bot.LogError(i.Key, procedureName, ex);
                    }
                }
            }
            return false;
        }

        private static void OnAwayCancelled(object sender, AwayEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnAwayCancelled", new object[] { sender, e });
        }
        private static void OnAwaySet(object sender, AwayEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnAwaySet", new object[] { sender, e });
        }
        private static void OnBanList(object sender, ChannelModeListEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnBanList", new object[] { sender, e });
        }
        private static void OnBanListEnd(object sender, ChannelModeListEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnBanListEnd", new object[] { sender, e });
        }
        private static void OnChannelAction(object sender, ChannelMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelAction", new object[] { sender, e });
        }
        private static void OnChannelAdmin(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelAdmin", new object[] { sender, e });
        }
        private static void OnChannelBan(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelBan", new object[] { sender, e });
        }
        private static void OnChannelTimestamp(object sender, ChannelTimestampEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelTimestamp", new object[] { sender, e });
        }
        private static void OnChannelCTCP(object sender, ChannelMessageEventArgs e) {
            if (Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelCTCP", new object[] { sender, e }))
                return;
            Bot.OnCTCPMessage((IRCClient) sender, e.Sender.Nickname, e.Message);
        }
        private static void OnChannelDeAdmin(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeAdmin", new object[] { sender, e });
        }
        private static void OnChannelDeHalfOp(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeHalfOp", new object[] { sender, e });
        }
        private static void OnChannelDeHalfVoice(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeHalfVoice", new object[] { sender, e });
        }
        private static void OnChannelDeOp(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeOp", new object[] { sender, e });
        }
        private static void OnChannelDeOwner(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeOwner", new object[] { sender, e });
        }
        private static void OnChannelDeVoice(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelDeVoice", new object[] { sender, e });
        }
        private static void OnChannelExempt(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelExempt", new object[] { sender, e });
        }
        private static void OnChannelHalfOp(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelHalfOp", new object[] { sender, e });
        }
        private static void OnChannelHalfVoice(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelHalfVoice", new object[] { sender, e });
        }
        private static void OnChannelInviteExempt(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelInviteExempt", new object[] { sender, e });
        }
        private static void OnChannelJoin(object sender, ChannelJoinEventArgs e) {
            bool cancel = Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelJoin", new object[] { sender, e });

            Identification id;
            if (Bot.Identifications.TryGetValue(((IRCClient) sender).NetworkName + "/" + e.Sender.Nickname, out id))
                id.Channels.Add(e.Channel);

            if (cancel) return;

            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autohalfvoice." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +V {1}", e.Channel, e.Sender.Nickname);
            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autovoice." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +v {1}", e.Channel, e.Sender.Nickname);
            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autohalfop." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +h {1}", e.Channel, e.Sender.Nickname);
            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autoop." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +o {1}", e.Channel, e.Sender.Nickname);
            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autoadmin." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +ao {1} {1}", e.Channel, e.Sender.Nickname);

            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autoquiet." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-')))
                ((IRCClient) sender).Send("MODE {0} +q *!*{1}", e.Channel, e.Sender.UserAndHost);
            if (Bot.UserHasPermission((IRCClient) sender, e.Channel, e.Sender, "irc.autoban." + ((IRCClient) sender).NetworkName.Replace('.', '-') + "." + e.Channel.Replace('.', '-'))) {
                ((IRCClient) sender).Send("MODE {0} +b *!*{1}", e.Channel, e.Sender.Nickname);
                ((IRCClient) sender).Send("KICK {0} {1} :You are banned from this channel.", e.Channel, e.Sender.Nickname);
            }

        }
        private static void OnChannelJoinDenied(object sender, ChannelDeniedEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelJoinDenied", new object[] { sender, e });
        }
        private static void OnChannelKick(object sender, ChannelKickEventArgs e) {
            ChannelPartEventArgs e2 = new ChannelPartEventArgs(e.Sender, e.Channel, "Kicked out by " + e.Sender.Nickname + ": " + e.Reason);
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelKick", new object[] { sender, e });
            Bot.OnChannelLeave(sender, e2);
        }
        private static void OnChannelMessage(object sender, ChannelMessageEventArgs e) {
            if (Bot.CheckMessage((IRCClient) sender, e.Sender, e.Channel, e.Message))
                return;
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelMessage", new object[] { sender, e });
        }
        private static void OnChannelMessageDenied(object sender, ChannelDeniedEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelMessageDenied", new object[] { sender, e });
        }
        private static void OnChannelModeSet(object sender, ChannelModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelModeSet", new object[] { sender, e });
        }
        private static void OnChannelModeUnhandled(object sender, ChannelModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelModeUnhandled", new object[] { sender, e });
        }
        private static void OnChannelModesSet(object sender, ChannelModesSetEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelModesSet", new object[] { sender, e });
        }
        private static void OnChannelModesGet(object sender, ChannelModesGetEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelModesGet", new object[] { sender, e });
        }
        private static void OnChannelNotice(object sender, ChannelMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelNotice", new object[] { sender, e });
        }
        private static void OnChannelOp(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelOp", new object[] { sender, e });
        }
        private static void OnChannelOwner(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelOwner", new object[] { sender, e });
        }
        private static void OnChannelPart(object sender, ChannelPartEventArgs e) {
            ChannelPartEventArgs e2 = new ChannelPartEventArgs(e.Sender, e.Channel, e.Message);
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelPart", new object[] { sender, e });
            Bot.OnChannelLeave(sender, e2);
        }
        private static void OnChannelQuiet(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelQuiet", new object[] { sender, e });
        }
        private static void OnChannelRemoveExempt(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelRemoveExempt", new object[] { sender, e });
        }
        private static void OnChannelRemoveInviteExempt(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelRemoveInviteExempt", new object[] { sender, e });
        }
        private static void OnChannelRemoveKey(object sender, ChannelEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelRemoveKey", new object[] { sender, e });
        }
        private static void OnChannelRemoveLimit(object sender, ChannelEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelRemoveLimit", new object[] { sender, e });
        }
        private static void OnChannelSetKey(object sender, ChannelKeyEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelSetKey", new object[] { sender, e });
        }
        private static void OnChannelSetLimit(object sender, ChannelLimitEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelSetLimit", new object[] { sender, e });
        }
        private static void OnChannelTopic(object sender, ChannelTopicEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelTopic", new object[] { sender, e });
        }
        private static void OnChannelTopicChange(object sender, ChannelTopicChangeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelTopicChange", new object[] { sender, e });
        }
        private static void OnChannelTopicStamp(object sender, ChannelTopicStampEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelTopicStamp", new object[] { sender, e });
        }
        private static void OnChannelUnBan(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelUnBan", new object[] { sender, e });
        }
        private static void OnChannelUnQuiet(object sender, ChannelListModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelUnQuiet", new object[] { sender, e });
        }
        private static void OnChannelVoice(object sender, ChannelNicknameModeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelVoice", new object[] { sender, e });
        }
        private static void OnDisconnected(object sender, DisconnectEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnDisconnected", new object[] { sender, e });
            if (e.Exception == null)
                ConsoleUtils.WriteLine("%cREDDisconnected from {0}.%r", ((IRCClient) sender).NetworkName);
            else
                ConsoleUtils.WriteLine("%cREDDisconnected from {0}: {1}%r", ((IRCClient) sender).NetworkName, e.Exception.Message);
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
            if (Bot.EventCheck((IRCClient) sender, null, "OnException", new object[] { sender, e }))
                return;
            Bot.LogConnectionError((IRCClient) sender, e.Exception);
        }
        private static void OnExemptList(object sender, ChannelModeListEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnExemptList", new object[] { sender, e });
        }
        private static void OnExemptListEnd(object sender, ChannelModeListEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnExemptListEnd", new object[] { sender, e });
        }
        private static void OnInvite(object sender, ChannelInviteEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnInvite", new object[] { sender, e });
        }
        private static void OnInviteSent(object sender, ChannelInviteSentEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnInviteSent", new object[] { sender, e });
        }
        private static void OnInviteExemptList(object sender, ChannelModeListEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnInviteExemptList", new object[] { sender, e });
        }
        private static void OnInviteExemptListEnd(object sender, ChannelModeListEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnInviteExemptListEnd", new object[] { sender, e });
        }
        private static void OnKilled(object sender, PrivateMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnKilled", new object[] { sender, e });
        }
        private static void OnChannelList(object sender, ChannelListEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnChannelList", new object[] { sender, e });
        }
        private static void OnChannelListEnd(object sender, ChannelListEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnChannelListEnd", new object[] { sender, e });
        }
        private static void OnMOTD(object sender, MOTDEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnMOTD", new object[] { sender, e });
        }
        private static void OnNames(object sender, ChannelNamesEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnNames", new object[] { sender, e });
        }
        private static void OnNamesEnd(object sender, ChannelModeListEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnNamesEnd", new object[] { sender, e });
        }
        private static void OnNicknameChange(object sender, NicknameChangeEventArgs e) {
            string key = ((IRCClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Identification id;
            if (Bot.Identifications.TryGetValue(key, out id)) {
                Bot.Identifications.Remove(key);
                Bot.Identifications.Add(((IRCClient) sender).NetworkName + "/" + e.NewNickname, id);
            }

            Bot.EventCheck((IRCClient) sender, null, "OnNicknameChange", new object[] { sender, e });
        }
        private static void OnNicknameChangeSelf(object sender, NicknameChangeEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnNicknameChangeSelf", new object[] { sender, e });
        }
        private static void OnNicknameChangeFailed(object sender, NicknameEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnNicknameChangeFailed", new object[] { sender, e });
        }
        private static void OnNicknameInvalid(object sender, NicknameEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnNicknameInvalid", new object[] { sender, e });
        }
        private static void OnNicknameTaken(object sender, NicknameEventArgs e) {
            if (Bot.EventCheck((IRCClient) sender, null, "OnNicknameTaken", new object[] { sender, e })) return;

            // Cycle through the list.
            var entry = GetClientEntry((IRCClient) sender);
            if (entry.Client.State <= IRCClientState.Registering && entry.Nicknames.Length > 1) {
                for (int i = 0; i < entry.Nicknames.Length - 1; ++i) {
                    if (entry.Nicknames[i] == e.Nickname) {
                        entry.Client.Me.Nickname = entry.Nicknames[i + 1];
                        break;
                    }
                }
            }
        }
        private static void OnPingRequest(object sender, PingEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnPingRequest", new object[] { sender, e });
        }
        private static void OnPingReply(object sender, PingEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnPingReply", new object[] { sender, e });
        }
        private static void OnPrivateCTCP(object sender, PrivateMessageEventArgs e) {
            if (Bot.EventCheck((IRCClient) sender, e.Sender.Nickname, "OnPrivateCTCP", new object[] { sender, e }))
                return;
            Bot.OnCTCPMessage((IRCClient) sender, e.Sender.Nickname, e.Message);
        }
        private static void OnPrivateMessage(object sender, PrivateMessageEventArgs e) {
            if (Bot.CheckMessage((IRCClient) sender, e.Sender, e.Sender.Nickname, e.Message))
                return;
            if (Bot.EventCheck((IRCClient) sender, e.Sender.Nickname, "OnPrivateMessage", new object[] { sender, e }))
                return;
            Bot.NickServCheck((IRCClient) sender, e.Sender, e.Message);
        }
        private static void OnPrivateNotice(object sender, PrivateMessageEventArgs e) {
            if (Bot.EventCheck((IRCClient) sender, e.Sender.Nickname, "OnPrivateNotice", new object[] { sender, e }))
                return;
            Bot.NickServCheck((IRCClient) sender, e.Sender, e.Message);
        }
        private static void OnPrivateAction(object sender, PrivateMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Sender.Nickname, "OnPrivateAction", new object[] { sender, e });
        }
        private static void OnUserQuit(object sender, QuitEventArgs e) {
            string key = ((IRCClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Bot.Identifications.Remove(key);

            bool cancel = Bot.EventCheck((IRCClient) sender, null, "OnUserQuit", new object[] { sender, e });

            foreach (IRCChannel channel in ((IRCClient) sender).Channels) {
                if (channel.Users.Contains(e.Sender.Nickname))
                    Bot.OnChannelLeave(sender, new ChannelPartEventArgs(e.Sender, channel.Name, (e.Message.StartsWith("Quit:") ? "Quit: " : "Disconnected: ") + e.Message));
            }

            if (cancel) return;

            foreach (var entry in Bot.Clients) {
                if (entry.Client == sender) {
                    if (((IRCClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, entry.Nicknames[0]))
                        ((IRCClient) sender).Send("NICK {0}", entry.Nicknames[0]);
                    break;
                }
            }
        }
        private static void OnRawLineReceived(object sender, IRCLineEventArgs e) {
            if (Bot.EventCheck((IRCClient) sender, null, "OnRawLineReceived", new object[] { sender, e }))
                return;

            IRCClient client = (IRCClient) sender;

            if (e.Line.Command == "001") {  // Login complete
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
                        Thread autoJoinThread = new Thread(Bot.AutoJoin);
                        autoJoinThread.Start(clientEntry);
                        break;
                    }
                }
            } else if (e.Line.Command == "604") {  // Watched user is online
                Identification id;
                if (Bot.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out id))
                    id.Watched = true;
            } else if (e.Line.Command == "601") {  // Watched user went offline
                Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
            } else if (e.Line.Command == "605") {  // Watched user is offline
                Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
            } else if (e.Line.Command == "602") {  // Stopped watching
                Identification id;
                if (Bot.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out id)) {
                    id.Watched = false;
                    if (id.Channels.Count == 0) Bot.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
                }
            }
        }
        private static void OnRawLineSent(object sender, RawEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnRawLineSent", new object[] { sender, e });
        }
        private static void OnUserModesGet(object sender, UserModesEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnUserModesGet", new object[] { sender, e });
        }
        private static void OnUserModesSet(object sender, UserModesEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnUserModesSet", new object[] { sender, e });
        }
        private static void OnWallops(object sender, PrivateMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Sender.Nickname, "OnWallops", new object[] { sender, e });
        }
        private static void OnServerNotice(object sender, PrivateMessageEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnServerNotice", new object[] { sender, e });
        }
        private static void OnServerError(object sender, ServerErrorEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnServerError", new object[] { sender, e });
        }
        private static void OnStateChanged(object sender, StateEventArgs e) {
            Bot.EventCheck((IRCClient) sender, null, "OnStateChanged", new object[] { sender, e });
        }
        private static void OnWhoList(object sender, WhoListEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnWhoList", new object[] { sender, e });
        }
        private static void OnWhoIsAuthenticationLine(object sender, WhoisAuthenticationEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsAuthenticationLine", new object[] { sender, e });
        }
        private static void OnWhoIsAwayLine(object sender, WhoisAwayEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsAwayLine", new object[] { sender, e });
        }
        private static void OnWhoIsChannelLine(object sender, WhoisChannelsEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsChannelLine", new object[] { sender, e });
        }
        private static void OnWhoIsEnd(object sender, WhoisEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsEnd", new object[] { sender, e });
        }
        private static void OnWhoIsIdleLine(object sender, WhoisIdleEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsIdleLine", new object[] { sender, e });
        }
        private static void OnWhoIsNameLine(object sender, WhoisNameEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsNameLine", new object[] { sender, e });
        }
        private static void OnWhoIsOperLine(object sender, WhoisOperEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsOperLine", new object[] { sender, e });
        }
        private static void OnWhoIsHelperLine(object sender, WhoisOperEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsHelperLine", new object[] { sender, e });
        }
        private static void OnWhoIsRealHostLine(object sender, WhoisRealHostEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsRealHostLine", new object[] { sender, e });
        }
        private static void OnWhoIsServerLine(object sender, WhoisServerEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoIsServerLine", new object[] { sender, e });
        }
        private static void OnWhoWasNameLine(object sender, WhoisNameEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoWasNameLine", new object[] { sender, e });
        }
        private static void OnWhoWasEnd(object sender, WhoisEndEventArgs e) {
            Bot.EventCheck((IRCClient) sender, e.Nickname, "OnWhoWasEnd", new object[] { sender, e });
        }

        public static void OnChannelLeave(object sender, ChannelPartEventArgs e) {
            string key = ((IRCClient) sender).NetworkName + "/" + e.Sender.Nickname;
            Identification id;
            if (Bot.Identifications.TryGetValue(key, out id)) {
                if (id.Channels.Remove(e.Channel)) {
                    if (id.Channels.Count == 0 && !(((IRCClient) sender).Extensions.SupportsWatch && id.Watched))
                        Bot.Identifications.Remove(key);
                }
            }
            Bot.EventCheck((IRCClient) sender, e.Channel, "OnChannelLeave", new object[] { sender, e });
        }

        private static void AutoJoin(object _client) {
            ClientEntry client = (ClientEntry) _client;
            Thread.Sleep(3000);
            if (client.Client.State == IRCClientState.Online) {
                foreach (AutoJoinChannel channel in client.AutoJoin)
                    if (channel.Key == null)
                        client.Client.Send("JOIN {0}", channel.Channel);
                    else
                        client.Client.Send("JOIN {0} {1}", channel.Key);
            }
        }
    }
}
