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
    public enum SayOptions : short {
        OpsOnly = 9,
        Capitalise = 2,
        NoticeAlways = 8,
        NoticeNever = 4,
    }

    public static class Bot {
        public class NickServData {
            public string[] RegisteredNicknames;
            public bool AnyNickname;
            public bool UseGhostCommand;
            public string GhostCommand;
            public string Password;
            public string IdentifyCommand;
            public string Hostmask;
            public string RequestMask;
            public DateTime IdentifyTime;

            public NickServData() {
                this.RegisteredNicknames = new string[0];
                this.AnyNickname = false;
                this.UseGhostCommand = true;
                this.GhostCommand = "PRIVMSG $target :GHOST $nickname $password";
                this.IdentifyCommand = "PRIVMSG $target :IDENTIFY $password";
                this.Hostmask = "NickServ!*@*";
                this.RequestMask = "*IDENTIFY*";
                this.IdentifyTime = default(DateTime);
            }
        }

        public class WaitData {
            public string Response;
        }

        public static string ClientVersion { get; private set; }
        public static Version Version { get; private set; }

        public static List<IRCClient> Connections = new List<IRCClient>();
        public static Dictionary<string, string[]> AutoJoinChannels = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, Bot.NickServData> NickServ = new Dictionary<string, Bot.NickServData>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, PluginData> Plugins = new Dictionary<string, PluginData>(StringComparer.OrdinalIgnoreCase);

        public static Dictionary<string, Identification> Identifications = new Dictionary<string, Identification>(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, Account> Accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

        public static string[] DefaultCommandPrefixes;
        public static Dictionary<string, string[]> ChannelCommandPrefixes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        internal static string[] dNicknames = new string[] { "CBot" };
        internal static string dUsername = "CBot";
        internal static string dFullName = "CBot by Andrio Celos";
        internal static string dUserInfo = "CBot by Andrio Celos";
        internal static string dAvatar = null;

        private static bool ConfigFileFound;
        private static bool UsersFileFound;
        private static bool PluginsFileFound;

        private static Dictionary<string, Bot.WaitData> Waiting = new Dictionary<string, Bot.WaitData>(StringComparer.OrdinalIgnoreCase);

        public static readonly Version MinPluginVersion = new Version(3, 0);

        public static string[] getCommandPrefixes() {
            return Bot.DefaultCommandPrefixes;
        }
        public static string[] getCommandPrefixes(string Channel) {
            bool flag = Bot.ChannelCommandPrefixes.ContainsKey(Channel);
            string[] CommandPrefixes;
            if (flag) {
                CommandPrefixes = Bot.ChannelCommandPrefixes[Channel];
            } else {
                CommandPrefixes = Bot.DefaultCommandPrefixes;
            }
            return CommandPrefixes;
        }
        public static string[] getCommandPrefixes(IRCClient Connection, string Channel) {
            bool flag = Connection == null;
            string[] CommandPrefixes;
            if (flag) {
                CommandPrefixes = Bot.getCommandPrefixes(Channel.Split(new char[]
					{
						'/'
					})[0] + "/" + Channel.Split(new char[]
					{
						'/'
					})[1]);
            } else {
                flag = Bot.ChannelCommandPrefixes.ContainsKey((Connection.Address + "/" + Channel).ToLower());
                if (flag) {
                    CommandPrefixes = Bot.ChannelCommandPrefixes[(Connection.Address + "/" + Channel).ToLower()];
                } else {
                    CommandPrefixes = Bot.DefaultCommandPrefixes;
                }
            }
            return CommandPrefixes;
        }

        public static IRCClient NewClient(string Address, int Port, string[] Nicknames, string Username, string FullName) {
            IRCClient newClient = new IRCClient {
                Address = Address,
                Port = Port,
                Nickname = Nicknames[0],
                Nicknames = Nicknames,
                Username = Username,
                FullName = FullName,
                ReconnectInterval = 30000,
                ReconnectMaxAttempts = 30
            };
            Bot.SetUpClientEvents(newClient);
            Bot.Connections.Add(newClient);
            return newClient;
        }

        public static void SetUpClientEvents(IRCClient newClient) {
            newClient.LookingUpHost += delegate(IRCClient sender, string Hostname) {
                ConsoleUtils.WriteLine("%cGREENLooking up {0}...%r", Hostname);
            };
            newClient.LookingUpHostFailed += delegate(IRCClient sender, string Hostname, string ErrorMessage) {
                ConsoleUtils.WriteLine("%cREDFailed to look up '{0}': {1}%r", Hostname, ErrorMessage);
            };
            newClient.Connecting += delegate(IRCClient sender, string Host, IPEndPoint Endpoint) {
                ConsoleUtils.WriteLine("%cGREENConnecting to {0} ({1}) on port {2}...%r", Host, Endpoint.Address, Endpoint.Port);
            };
            newClient.ConnectingFailed += delegate(IRCClient sender, Exception Exception) {
                ConsoleUtils.WriteLine("%cREDI was unable to connect to {0}: {1}%r", sender.Address, Exception.Message);
            };
            newClient.Connected += delegate(IRCClient sender) {
                ConsoleUtils.WriteLine("%cGREENConnected to {0}.%r", sender.Address);
            };
            newClient.Disconnected += delegate(IRCClient sender, string Message) {
                ConsoleUtils.WriteLine("%cREDDisconnected from {0}: {1}%r", sender.Address, Message);
            };
            newClient.WaitingToReconnect += delegate(IRCClient sender, decimal Interval, int Attempts, int MaxAttempts) {
                if (MaxAttempts < 0)
                    ConsoleUtils.WriteLine("%cREDWaiting {0} seconds before reconnecting... (Attempt number {1})%r", Interval, Attempts + 1);
                else
                    ConsoleUtils.WriteLine("%cREDWaiting {0} seconds before reconnecting... (Attempt number {1} of {2})%r", Interval, Attempts + 1, MaxAttempts);
            };
            newClient.RawLineReceived += delegate(IRCClient sender, string message) {
                ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKGREEN>>%cDKGRAY {1}%r", sender.Address, message.Replace("%", "%%"));
            };
            newClient.RawLineSent += delegate(IRCClient sender, string message) {
                ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}%r", sender.Address, message.Replace("%", "%%"));
            };

            newClient.Exception += Bot.LogConnectionError;
            newClient.AwayCancelled += Bot.OnAwayCancelled;
            newClient.AwaySet += Bot.OnAway;
            newClient.BanList += Bot.OnBanList;
            newClient.BanListEnd += Bot.OnBanListEnd;
            newClient.ChannelAction += Bot.OnChannelAction;
            newClient.ChannelActionHighlight += Bot.OnChannelActionHighlight;
            newClient.ChannelAdmin += Bot.OnChannelAdmin;
            newClient.ChannelAdminSelf += Bot.OnChannelAdminSelf;
            newClient.ChannelBan += Bot.OnChannelBan;
            newClient.ChannelBanSelf += Bot.OnChannelBanSelf;
            newClient.ChannelTimestamp += Bot.OnChannelTimestamp;
            newClient.ChannelCTCP += Bot.OnChannelCTCP;
            newClient.ChannelDeAdmin += Bot.OnChannelDeAdmin;
            newClient.ChannelDeAdminSelf += Bot.OnChannelDeAdminSelf;
            newClient.ChannelDeHalfOp += Bot.OnChannelDeHalfOp;
            newClient.ChannelDeHalfOpSelf += Bot.OnChannelDeHalfOpSelf;
            newClient.ChannelDeHalfVoice += Bot.OnChannelDeHalfVoice;
            newClient.ChannelDeHalfVoiceSelf += Bot.OnChannelDeHalfVoiceSelf;
            newClient.ChannelDeOp += Bot.OnChannelDeOp;
            newClient.ChannelDeOpSelf += Bot.OnChannelDeOpSelf;
            newClient.ChannelDeOwner += Bot.OnChannelDeOwner;
            newClient.ChannelDeOwnerSelf += Bot.OnChannelDeOwnerSelf;
            newClient.ChannelDeVoice += Bot.OnChannelDeVoice;
            newClient.ChannelDeVoiceSelf += Bot.OnChannelDeVoiceSelf;
            newClient.ChannelExempt += Bot.OnChannelExempt;
            newClient.ChannelExemptSelf += Bot.OnChannelExemptSelf;
            newClient.ChannelHalfOp += Bot.OnChannelHalfOp;
            newClient.ChannelHalfOpSelf += Bot.OnChannelHalfOpSelf;
            newClient.ChannelHalfVoice += Bot.OnChannelHalfVoice;
            newClient.ChannelHalfVoiceSelf += Bot.OnChannelHalfVoiceSelf;
            newClient.ChannelInviteExempt += Bot.OnChannelInviteExempt;
            newClient.ChannelInviteExemptSelf += Bot.OnChannelInviteExemptSelf;
            newClient.ChannelJoin += Bot.OnChannelJoin;
            newClient.ChannelJoinSelf += Bot.OnChannelJoinSelf;
            newClient.ChannelJoinDeniedBanned += Bot.OnChannelJoinDeniedBanned;
            newClient.ChannelJoinDeniedFull += Bot.OnChannelJoinDeniedFull;
            newClient.ChannelJoinDeniedInvite += Bot.OnChannelJoinDeniedInvite;
            newClient.ChannelJoinDeniedKey += Bot.OnChannelJoinDeniedKey;
            newClient.ChannelKick += Bot.OnChannelKick;
            newClient.ChannelKickSelf += Bot.OnChannelKickSelf;
            newClient.ChannelList += Bot.OnChannelList;
            newClient.ChannelMessage += Bot.OnChannelMessage;
            newClient.ChannelMessageSendDenied += Bot.OnChannelMessageSendDenied;
            newClient.ChannelMessageHighlight += Bot.OnChannelMessageHighlight;
            newClient.ChannelMode += Bot.OnChannelMode;
            newClient.ChannelModesGet += Bot.OnChannelModesGet;
            newClient.ChannelOp += Bot.OnChannelOp;
            newClient.ChannelOpSelf += Bot.OnChannelOpSelf;
            newClient.ChannelOwner += Bot.OnChannelOwner;
            newClient.ChannelOwnerSelf += Bot.OnChannelOwnerSelf;
            newClient.ChannelPart += Bot.OnChannelPart;
            newClient.ChannelPartSelf += Bot.OnChannelPartSelf;
            newClient.ChannelQuiet += Bot.OnChannelQuiet;
            newClient.ChannelQuietSelf += Bot.OnChannelQuietSelf;
            newClient.ChannelRemoveExempt += Bot.OnChannelRemoveExempt;
            newClient.ChannelRemoveExemptSelf += Bot.OnChannelRemoveExemptSelf;
            newClient.ChannelRemoveInviteExempt += Bot.OnChannelRemoveInviteExempt;
            newClient.ChannelRemoveInviteExemptSelf += Bot.OnChannelRemoveInviteExemptSelf;
            newClient.ChannelRemoveKey += Bot.OnChannelRemoveKey;
            newClient.ChannelRemoveLimit += Bot.OnChannelRemoveLimit;
            newClient.ChannelSetKey += Bot.OnChannelSetKey;
            newClient.ChannelSetLimit += Bot.OnChannelSetLimit;
            newClient.ChannelTopic += Bot.OnChannelTopic;
            newClient.ChannelTopicChange += Bot.OnChannelTopicChange;
            newClient.ChannelTopicStamp += Bot.OnChannelTopicStamp;
            newClient.ChannelUsers += Bot.OnChannelUsers;
            newClient.ChannelUnBan += Bot.OnChannelUnBan;
            newClient.ChannelUnBanSelf += Bot.OnChannelUnBanSelf;
            newClient.ChannelUnQuiet += Bot.OnChannelUnQuiet;
            newClient.ChannelUnQuietSelf += Bot.OnChannelUnQuietSelf;
            newClient.ChannelVoice += Bot.OnChannelVoice;
            newClient.ChannelVoiceSelf += Bot.OnChannelVoiceSelf;
            newClient.ExemptList += Bot.OnExemptList;
            newClient.ExemptListEnd += Bot.OnExemptListEnd;
            newClient.Invite += Bot.OnInvite;
            newClient.InviteExemptList += Bot.OnInviteExemptList;
            newClient.InviteExemptListEnd += Bot.OnInviteExemptListEnd;
            newClient.Killed += Bot.OnKilled;
            newClient.Names += Bot.OnNames;
            newClient.NamesEnd += Bot.OnNamesEnd;
            newClient.NicknameChange += Bot.OnNicknameChange;
            newClient.NicknameChangeSelf += Bot.OnNicknameChangeSelf;
            newClient.PrivateMessage += Bot.OnPrivateMessage;
            newClient.PrivateAction += Bot.OnPrivateAction;
            newClient.PrivateNotice += Bot.OnPrivateNotice;
            newClient.PrivateCTCP += Bot.OnPrivateCTCP;
            newClient.Quit += Bot.OnQuit;
            newClient.QuitSelf += Bot.OnQuitSelf;
            newClient.RawLineReceived += Bot.OnRawLineReceived;
            newClient.ServerNotice += Bot.OnServerNotice;
            newClient.ServerError += Bot.OnServerError;
            newClient.ServerMessage += Bot.OnServerMessage;
            newClient.ServerMessageUnhandled += Bot.OnServerMessageUnhandled;
            newClient.TimeOut += Bot.OnTimeOut;
            newClient.UserModesSet += Bot.OnUserModesSet;
            newClient.WhoList += Bot.OnWhoList;
        }

        public static void Main() {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Bot.Version = assembly.GetName().Version;

            //Console.TreatControlCAsInput = true;

            string title = ""; string author = "";
            foreach (object attribute in assembly.GetCustomAttributes(false)) {
                if (attribute is AssemblyTitleAttribute)
                    title = ((AssemblyTitleAttribute) attribute).Title;
                else if (attribute is AssemblyCompanyAttribute)
                    author = ((AssemblyCompanyAttribute) attribute).Company;
            }
            Bot.ClientVersion = string.Format("CBot by {1} : version {2}.{3}", title, author, Bot.Version.Major, Bot.Version.Minor, Bot.Version.Revision, Bot.Version.Build);

            Console.ForegroundColor = ConsoleColor.Gray;
            Bot.DefaultCommandPrefixes = new string[] { "!" };

            Bot.Connections.Add(new ConsoleConnection());
            SetUpClientEvents(Bot.Connections[0]);

            Console.Write("Loading configuration file...");
            bool flag = File.Exists("CBotConfig.ini");
            if (flag) {
                Bot.ConfigFileFound = true;
                try {
                    Bot.LoadConfig();
                    Console.WriteLine(" OK");
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(" %cREDFailed%r");
                    ConsoleUtils.WriteLine("%cREDI couldn't load the configuration file: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(0);
                }
                Bot.Connections[0].Nickname = dNicknames[0];
            } else {
                ConsoleUtils.WriteLine(" %cBLUEFile CBotConfig.ini is missing.%r");
            }
            Console.Write("Loading user configuration file...");
            flag = File.Exists("CBotUsers.ini");
            if (flag) {
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
            Console.WriteLine("Loading plugins...");
            flag = File.Exists("CBotPlugins.ini");
            if (flag) {
                Bot.PluginsFileFound = true;
                try {
                    Bot.LoadPlugins();
                } catch (Exception ex) {
                    Console.WriteLine();
                    ConsoleUtils.WriteLine("%cREDI couldn't load the plugins: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
                    if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(0);
                }
            } else {
                ConsoleUtils.WriteLine("%cBLUEFile CBotPlugins.ini is missing.%r");
            }
            Bot.FirstRun();

            foreach (IRCClient client in Bot.Connections) {
                try {
                    client.Connect();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine(string.Concat(new string[]
					{
						"%cREDI could not initialise an IRC connection to ",
						client.Address,
						": ",
						ex.Message,
						"%r"
					}));
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
                            Connections[int.Parse(fields[1])].Send(string.Join(" ", fields.Skip(2)));
                            break;
                        case "CONNECT":
                            IRCClient client = Bot.NewClient(fields[1],
                                fields.Length >= 3 ? int.Parse((fields[2].StartsWith("+") ? fields[2].Substring(1) : fields[2])) : 6667,
                                fields.Length >= 4 ? new string[] { fields[3] } : dNicknames,
                                fields.Length >= 5 ? fields[4] : dUsername,
                                fields.Length >= 6 ? string.Join(" ", fields.Skip(5)) : dFullName);
                            if (fields.Length >= 3 && fields[2].StartsWith("+")) client.IsUsingSSL = true;
                            client.Connect();
                            break;
                        case "DIE":
                            foreach (IRCClient connection in Bot.Connections) {
                                if (connection.IsConnected)
                                    connection.Send("QUIT :{0}", fields.Length >= 2 ? string.Join(" ", fields.Skip(1)) : "Shutting down.");
                            }
                            Thread.Sleep(2000);
                            foreach (IRCClient connection in Bot.Connections) {
                                if (connection.IsConnected)
                                    connection.Disconnect();
                            }
                            Environment.Exit(0);
                            break;
                        case "ENTER":
                            foreach (IRCClient connection in Bot.Connections) {
                                if (connection is ConsoleConnection)
                                    ((ConsoleConnection) connection).Put(string.Join(" ", fields.Skip(1)));
                            }
                            break;
                        default:
                            foreach (IRCClient connection in Bot.Connections) {
                                if (connection is ConsoleConnection)
                                    ((ConsoleConnection) connection).Put(input);
                            }
                            break;
                    }
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cREDThere was a problem processing your request: " + ex.Message + "%r");
                    ConsoleUtils.WriteLine("%cDKRED" + ex.StackTrace + "%r");
                }
            }
        }

        public static bool LoadPlugin(string Key, string Filename, params string[] Channels) {
            Assembly assembly = Assembly.LoadFrom(Filename);
            AssemblyName assemblyName = assembly.GetName();
            Type pluginClass = null;

            foreach (Type type in assembly.GetTypes()) {
                if (typeof(Plugin).IsAssignableFrom(type)) {
                    pluginClass = type;
                    break;
                }
            }
            if (pluginClass == null) throw new EntryPointNotFoundException("This is not a valid plugin (no class was found that inherits from the base plugin class).");

            Version pluginVersion = null;
            foreach (APIVersionAttribute attribute in pluginClass.GetCustomAttributes(typeof(APIVersionAttribute), false)) {
                if (pluginVersion == null || pluginVersion < attribute.Version)
                    pluginVersion = attribute.Version;
            }
            if (pluginVersion == null) {
                ConsoleUtils.WriteLine(" %cREDOutdated plugin – no API version is specified.");
                return false;
            } else if (pluginVersion < Bot.MinPluginVersion) {
                ConsoleUtils.WriteLine(" %cREDOutdated plugin – built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                return false;
            } else if (pluginVersion > Bot.Version) {
                ConsoleUtils.WriteLine(" %cREDOutdated bot – the plugin is built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
                return false;
            }

            int constructorType = -1;
            foreach (ConstructorInfo constructor in pluginClass.GetConstructors()) {
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
                plugin = (Plugin) Activator.CreateInstance(pluginClass);
            else if (constructorType == 1)
                plugin = (Plugin) Activator.CreateInstance(pluginClass, new object[] { Key });
            else if (constructorType == 2)
                plugin = (Plugin) Activator.CreateInstance(pluginClass, new object[] { Key, Channels });
            else
                throw new InvalidCastException("This is not a valid plugin (no compatible constructor was found).");

            plugin.Channels = Channels ?? new string[0];
            ConsoleUtils.Write(" {0} ({1})", new object[] { plugin.Name.Replace("%", "%%"), assemblyName.Version });
            Bot.Plugins.Add(Key, new PluginData() { Filename = Filename, Obj = plugin });
            return true;
        }

        public static void FirstRun() {
            if (!Bot.ConfigFileFound) {
                Console.WriteLine();
                Console.WriteLine("This appears to be the first time I have been run here. Let us take a moment to set up.");
                Console.WriteLine("Please enter the identity details I should use on IRC.");
                Bot.dNicknames = new string[0];
                while (Bot.dNicknames.Length == 0) {
                    Console.Write("Nicknames (comma- or space-separated, in order of preference): ");
                    string Input = Console.ReadLine();
                    Bot.dNicknames = Input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string nickname in Bot.dNicknames) {
                        if (nickname == "") continue;
                        if (nickname[0] >= '0' && nickname[0] <= '9') {
                            Console.WriteLine("A nickname may not begin with a digit.");
                            Bot.dNicknames = new string[0];
                            break;
                        }
                        foreach (char c in nickname) {
                            if ((c < 'A' || c > '}') && (c < '0' && c > '9')) {
                                Console.WriteLine("Nickname '" + nickname + "' contains invalid characters.");
                                Bot.dNicknames = new string[0];
                                break;
                            }
                        }
                    }
                }
                Bot.dUsername = "";
                while (Bot.dUsername == "") {
                    Console.Write("Username: ");
                    Bot.dUsername = Console.ReadLine();
                    foreach (char c in Bot.dUsername) {
                        if ((c < 'A' || c > '}') && (c < '0' && c > '9')) {
                            Console.WriteLine("That username contains invalid characters.");
                            Bot.dUsername = "";
                            break;
                        }
                    }
                }
                Bot.dFullName = "";
                while (Bot.dFullName == "") {
                    Console.Write("Full name: ");
                    Bot.dFullName = Console.ReadLine();
                }
                Bot.dUserInfo = "";
                while (Bot.dUserInfo == "") {
                    Console.Write("User info for CTCP: ");
                    Bot.dUserInfo = Console.ReadLine();
                }
                Bot.DefaultCommandPrefixes = null;
                while (Bot.DefaultCommandPrefixes == null) {
                    Console.Write("What do you want my command prefix to be? ");
                    string Input = Console.ReadLine();
                    if (Input.Length != 1)
                        Console.WriteLine("It must be a single character.");
                    else {
                        Bot.DefaultCommandPrefixes = new string[] { Input };
                    }
                }

                bool SetUpNetwork;
                Console.WriteLine();
                while (true) {
                    Console.Write("Shall I connect to an IRC network? ");
                    string Input = Console.ReadLine();
                    if (Input.Length < 1) return;
                    if (Input[0] == 'Y' || Input[0] == 'y' ||
                        Input[0] == 'S' || Input[0] == 's' ||
                        Input[0] == 'O' || Input[0] == 'o' ||
                        Input[0] == 'J' || Input[0] == 'j') {
                        SetUpNetwork = true;
                        break;
                    } else if (Input[0] == 'N' || Input[0] == 'n' ||
                               Input[0] == 'A' || Input[0] == 'a' ||
                               Input[0] == 'P' || Input[0] == 'p') {
                        SetUpNetwork = false;
                        break;
                    }
                }

                if (SetUpNetwork) {
                    string NetworkName;
                    string NetworkAddress = null;
                    ushort NetworkPort = 0;
                    bool UseSSL = false;
                    bool AcceptInvalidSSLCertificate = false;
                    string[] AutoJoinChannels;

                    do {
                        Console.Write("What is the name of the IRC network? ");
                        NetworkName = Console.ReadLine();
                    } while (NetworkName == "");
                    do {
                        Console.Write("What is the address of the server? ");
                        string Input = Console.ReadLine();
                        if (Input == "") continue;
                        Match match = Regex.Match(Input, @"^(?>([^:]*):((\+)?\d{1,5}))$", RegexOptions.Singleline);
                        if (match.Success) {
                            if (!ushort.TryParse(match.Groups[3].Value, out NetworkPort) || NetworkPort == 0) {
                                Console.WriteLine("That is not a valid port number.");
                                continue;
                            }
                            NetworkAddress = match.Groups[1].Value;
                            UseSSL = match.Groups[2].Success;
                        } else {
                            NetworkAddress = Input;
                        }
                    } while (NetworkAddress == null);
                    while (NetworkPort == 0) {
                        Console.Write("What port number should I connect on? ");
                        string Input = Console.ReadLine();
                        if (Input == "") continue;
                        if (Input[0] == '+') {
                            UseSSL = true;
                            Input = Input.Substring(1);
                        }
                        if (!ushort.TryParse(Input, out NetworkPort) || NetworkPort == 0) {
                            Console.WriteLine("That is not a valid port number.");
                            UseSSL = false;
                        }
                    }
                    if (!UseSSL) {
                        while (true) {
                            Console.Write("Shall I use SSL? ");
                            string Input = Console.ReadLine();
                            if (Input.Length < 1) return;
                            if (Input[0] == 'Y' || Input[0] == 'y' ||
                                Input[0] == 'S' || Input[0] == 's' ||
                                Input[0] == 'O' || Input[0] == 'o' ||
                                Input[0] == 'J' || Input[0] == 'j') {
                                UseSSL = true;
                                break;
                            } else if (Input[0] == 'N' || Input[0] == 'n' ||
                                       Input[0] == 'A' || Input[0] == 'a' ||
                                       Input[0] == 'P' || Input[0] == 'p') {
                                UseSSL = false;
                                break;
                            }
                        }
                    }
                    if (UseSSL) {
                        while (true) {
                            Console.Write("Shall I connect if the server's certificate is invalid? ");
                            string Input = Console.ReadLine();
                            if (Input.Length < 1) return;
                            if (Input[0] == 'Y' || Input[0] == 'y' ||
                                Input[0] == 'S' || Input[0] == 's' ||
                                Input[0] == 'O' || Input[0] == 'o' ||
                                Input[0] == 'J' || Input[0] == 'j') {
                                AcceptInvalidSSLCertificate = true;
                                break;
                            } else if (Input[0] == 'N' || Input[0] == 'n' ||
                                       Input[0] == 'A' || Input[0] == 'a' ||
                                       Input[0] == 'P' || Input[0] == 'p') {
                                AcceptInvalidSSLCertificate = false;
                                break;
                            }
                        }
                    }

                    Bot.NickServData NickServ;
                    Console.WriteLine();
                    while (true) {
                        Console.Write("Is there a NickServ registration for me on " + NetworkName + "? ");
                        string Input = Console.ReadLine();
                        if (Input.Length < 1) return;
                        if (Input[0] == 'Y' || Input[0] == 'y' ||
                            Input[0] == 'S' || Input[0] == 's' ||
                            Input[0] == 'O' || Input[0] == 'o' ||
                            Input[0] == 'J' || Input[0] == 'j') {
                            NickServ = new Bot.NickServData();
                            break;
                        } else if (Input[0] == 'N' || Input[0] == 'n' ||
                                   Input[0] == 'A' || Input[0] == 'a' ||
                                   Input[0] == 'P' || Input[0] == 'p') {
                            NickServ = null;
                            break;
                        }
                    }

                    if (NickServ != null) {
                        do {
                            Console.Write("Grouped nicknames (comma- or space-separated): ");
                            string Input = Console.ReadLine();
                            NickServ.RegisteredNicknames = Input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string nickname in NickServ.RegisteredNicknames) {
                                if (nickname == "") continue;
                                if (nickname[0] >= '0' && nickname[0] <= '9') {
                                    Console.WriteLine("A nickname may not begin with a digit.");
                                    NickServ.RegisteredNicknames = new string[0];
                                    break;
                                }
                                foreach (char c in nickname) {
                                    if ((c < 'A' || c > '}') && (c < '0' && c > '9')) {
                                        Console.WriteLine("Nickname '" + nickname + "' contains invalid characters.");
                                        NickServ.RegisteredNicknames = new string[0];
                                        break;
                                    }
                                }
                            }
                        } while (NickServ.RegisteredNicknames.Length == 0);

                        NickServ.Password = "";
                        do {
                            Console.Write("NickServ account password: ");
                            NickServ.Password = Console.ReadLine();
                        } while (NickServ.Password == "");

                        while (true) {
                            Console.Write("Can I log in from any nickname by including '{0}' in the identify command? ", NickServ.RegisteredNicknames[0]);
                            string Input = Console.ReadLine();
                            if (Input.Length < 1) return;
                            if (Input[0] == 'Y' || Input[0] == 'y' ||
                                Input[0] == 'S' || Input[0] == 's' ||
                                Input[0] == 'O' || Input[0] == 'o' ||
                                Input[0] == 'J' || Input[0] == 'j') {
                                NickServ.AnyNickname = true;
                                break;
                            } else if (Input[0] == 'N' || Input[0] == 'n' ||
                                       Input[0] == 'A' || Input[0] == 'a' ||
                                       Input[0] == 'P' || Input[0] == 'p') {
                                NickServ.AnyNickname = false;
                                break;
                            }
                        }
                    }

                    Console.WriteLine();
                    do {
                        Console.Write("What channels (comma- or space-separated) should I join upon connecting? ");
                        string Input = Console.ReadLine();
                        AutoJoinChannels = Input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    } while (AutoJoinChannels == null);

                    IRCClient client = Bot.NewClient(NetworkAddress, (int) NetworkPort, Bot.dNicknames, Bot.dUsername, Bot.dFullName);
                    client.IsUsingSSL = UseSSL;
                    client.AllowInvalidCertificate = AcceptInvalidSSLCertificate;
                    if (NickServ != null)
                        Bot.NickServ.Add(NetworkAddress.ToLower(), NickServ);
                    if (AutoJoinChannels.Length != 0)
                        Bot.AutoJoinChannels.Add(NetworkAddress.ToLower(), AutoJoinChannels);
                    Console.WriteLine("OK, that's the IRC connection configuration done.");
                    Console.WriteLine("Press any key to continue . . .");
                    Console.ReadKey(true);
                }
            }
            if (!Bot.UsersFileFound) {
                string AccountName; StringBuilder passwordBuilder = null;

                Console.WriteLine();
                string Input = null;
                while (true) {
                    Console.WriteLine("What do you want your account name to be?");
                    if (Input == null)
                        Console.Write("For simplicity, we recommend you use your IRC nickname. ");
                    Input = Console.ReadLine();
                    if (Input == "") continue;
                    if (Input.Contains(" "))
                        Console.WriteLine("It can't contain spaces.");
                    else {
                        AccountName = Input;
                        break;
                    }
                }

                RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider();
                SHA256Managed SHA256M = new SHA256Managed();
                do {
                    Console.Write("Please enter a password.      ");
                    Input = "";
                    while (true) {
                        ConsoleKeyInfo c = Console.ReadKey(true);
                        if (c.Key == ConsoleKey.Enter) break;
                        Input += c.KeyChar.ToString();
                        Console.Write('*');
                    }
                    Console.WriteLine();
                    if (Input == "") continue;
                    if (Input.Contains(" ")) {
                        Console.WriteLine("It can't contain spaces.");
                        continue;
                    }

                    // Hash the password.
                    byte[] Salt = new byte[32];
                    RNG.GetBytes(Salt);
                    byte[] Hash = SHA256M.ComputeHash(Salt.Concat(Encoding.UTF8.GetBytes(Input)).ToArray());
                    Console.Write("Please confirm your password. ");
                    Input = "";
                    while (true) {
                        ConsoleKeyInfo c = Console.ReadKey(true);
                        if (c.Key == ConsoleKey.Enter) break;
                        Input += c.KeyChar.ToString();
                        Console.Write('*');
                    }
                    Console.WriteLine();
                    if (Input == "" || Input.Contains(" ")) {
                        Console.WriteLine("The passwords do not match.");
                        continue;
                    }

                    byte[] ConfirmHash = SHA256M.ComputeHash(Salt.Concat(Encoding.UTF8.GetBytes(Input)).ToArray());
                    if (!Hash.SequenceEqual(ConfirmHash)) {
                        Console.WriteLine("The passwords do not match.");
                        continue;
                    }

                    passwordBuilder = new StringBuilder();
                    int i;
                    for (i = 0; i < 32; ++i)
                        passwordBuilder.Append(Salt[i].ToString("x2"));
                    for (i = 0; i < 32; ++i)
                        passwordBuilder.Append(Hash[i].ToString("x2"));
                    Bot.Accounts.Add(AccountName, new Account {
                        Password = passwordBuilder.ToString(),
                        Permissions = new string[] { "*" }
                    });
                    ConsoleUtils.WriteLine("Thank you. To log in from IRC, enter %cWHITE/msg {0} !id <password>%r or %cWHITE/msg {0} !id {1} <password>%r, without the brackets.", Bot.Nickname(), AccountName);
                    Console.WriteLine("Press any key to continue . . .");
                    Console.ReadKey(true);
                    break;
                } while (passwordBuilder == null);
            }
        }

        public static void LoadConfig() {
            if (File.Exists("CBotConfig.ini")) {
                try {
                    StreamReader Reader = new StreamReader("CBotConfig.ini");
                    string Section = ""; string Field; string Value;
                    bool GotNicknames = false; IRCClient Connection = null;

                    while (!Reader.EndOfStream) {
                        string s = Reader.ReadLine();
                        if (!Regex.IsMatch(s, @"^(?>\s*);")) {  // Comment check
                            Match Match = Regex.Match(s, @"^\s*\[(.*?)\]?\s*$");
                            if (Match.Success) {
                                Section = Match.Groups[1].Value;
                                if (!Section.Equals("Me", StringComparison.OrdinalIgnoreCase) && !Section.Equals("Prefixes", StringComparison.OrdinalIgnoreCase)) {
                                    ushort Port;
                                    string[] ss = Section.Split(new char[] { ':' }, 2);
                                    string Host = ss[0];
                                    if (ss.Length > 1) {
                                        if (!ushort.TryParse(ss[1], out Port) || Port == 0)
                                            ConsoleUtils.WriteLine("%cREDPort number for " + ss[0] + " is invalid.");
                                    } else
                                        Port = 6667;

                                    if (Port != 0)
                                        Connection = Bot.NewClient(Host, (int) Port, Bot.dNicknames, Bot.dUsername, Bot.dFullName);
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
                                        ChannelCommandPrefixes.Add(Field, Value.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                    } else {
                                        if (!NickServ.ContainsKey(Connection.Address) && (
                                            Field.StartsWith("NickServ", StringComparison.OrdinalIgnoreCase) ||
                                            Field.StartsWith("NS", StringComparison.OrdinalIgnoreCase))) {
                                            NickServ.Add(Connection.Address, new NickServData());
                                        }
                                        switch (Field.ToUpper()) {
                                            case "NICKNAME":
                                            case "NICKNAMES":
                                            case "NAME":
                                            case "NAMES":
                                            case "NICK":
                                            case "NICKS":
                                                if (GotNicknames)
                                                    Connection.Nicknames = Connection.Nicknames.Concat(Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)).ToArray();
                                                else {
                                                    Connection.Nicknames = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                                    GotNicknames = true;
                                                }
                                                break;
                                            case "USERNAME":
                                            case "USER":
                                            case "IDENTNAME":
                                            case "IDENT":
                                                Connection.Username = Value;
                                                break;
                                            case "FULLNAME":
                                            case "REALNAME":
                                            case "GECOS":
                                            case "FULL":
                                                Connection.FullName = Value;
                                                break;
                                            case "AUTOJOIN":
                                                AutoJoinChannels.Add(Connection.Address, Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                                                break;
                                            case "SSL":
                                            case "USESSL":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    Connection.IsUsingSSL = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    Connection.IsUsingSSL = false;
                                                }
                                                break;
                                            case "ALLOWINVALIDCERTIFICATE":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    Connection.AllowInvalidCertificate = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    Connection.AllowInvalidCertificate = false;
                                                }
                                                break;
                                            case "NICKSERV-NICKNAMES":
                                            case "NS-NICKNAMES":
                                                Bot.NickServ[Connection.Address].RegisteredNicknames = Value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                                break;
                                            case "NICKSERV-PASSWORD":
                                            case "NS-PASSWORD":
                                                Bot.NickServ[Connection.Address].Password = Value;
                                                break;
                                            case "NICKSERV-ANYNICKNAME":
                                            case "NS-ANYNICKNAME:":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    Bot.NickServ[Connection.Address].AnyNickname = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    Bot.NickServ[Connection.Address].AnyNickname = false;
                                                }
                                                break;
                                            case "NICKSERV-USEGHOSTCOMMAND":
                                            case "NS-USEGHOSTCOMMAND":
                                                if (Value.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                                                    Value.Equals("On", StringComparison.OrdinalIgnoreCase)) {
                                                    Bot.NickServ[Connection.Address].UseGhostCommand = true;
                                                } else if (Value.Equals("No", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                                                           Value.Equals("Off", StringComparison.OrdinalIgnoreCase)) {
                                                    Bot.NickServ[Connection.Address].UseGhostCommand = false;
                                                }
                                                break;
                                            case "NICKSERV-IDENTIFYCOMMAND":
                                            case "NS-IDENTIFYCOMMAND":
                                                Bot.NickServ[Connection.Address].IdentifyCommand = Value;
                                                break;
                                            case "NICKSERV-GHOSTCOMMAND":
                                            case "NS-GHOSTCOMMAND":
                                                Bot.NickServ[Connection.Address].GhostCommand = Value;
                                                break;
                                            case "NICKSERV-HOSTMASK":
                                            case "NS-HOSTMASK":
                                                Bot.NickServ[Connection.Address].Hostmask = Value;
                                                break;
                                            case "NICKSERV-REQUESTMASK":
                                            case "NS-REQUESTMASK":
                                                Bot.NickServ[Connection.Address].RequestMask = Value;
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
                    Bot.Accounts.Add(Section, newUser);
                    Reader.Close();
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve user data from the file: $k04" + ex.Message + "%r");
                }
            }
        }

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
                                    ConsoleUtils.Write("  Loading plugin %cWHITE" + Section + "%r...");
                                    Bot.LoadPlugin(Section, Filename, Channels);
                                    ConsoleUtils.WriteLine(" OK");
                                } catch (Exception ex) {
                                    ConsoleUtils.WriteLine("%cRED Failed%r");
                                    Bot.LogError(Section, "Initialisation", ex);
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
                            ConsoleUtils.Write("  Loading plugin %cWHITE" + Section + "%r...");
                            if (Bot.LoadPlugin(Section, Filename, Channels))
                                ConsoleUtils.WriteLine(" OK");
                            else
                                error = true;
                        } catch (Exception ex) {
                            ConsoleUtils.WriteLine("%cRED Failed%r");
                            Bot.LogError(Section, "Initialisation", ex);
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
                if (Console.ReadKey(true).Key == ConsoleKey.Escape) Environment.Exit(0);
            }
        }

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
            foreach (IRCClient Connection in Bot.Connections) {
                Writer.WriteLine();
                Writer.WriteLine("[" + Connection.Address + "]");
                Writer.WriteLine("Nicknames=" + string.Join(",", Connection.Nicknames));
                Writer.WriteLine("Username=" + Connection.Username);
                Writer.WriteLine("FullName=" + Connection.FullName);
                if (Bot.AutoJoinChannels.ContainsKey(Connection.Address)) {
                    Writer.WriteLine("Autojoin=" + string.Join(",", Bot.AutoJoinChannels[Connection.Address.ToLower()]));
                }
                Writer.WriteLine("SSL=" + (Connection.IsUsingSSL ? "Yes" : "No"));
                Writer.WriteLine("AllowInvalidCertificate=" + (Connection.AllowInvalidCertificate ? "Yes" : "No"));
                if (Bot.NickServ.ContainsKey(Connection.Address)) {
                    Writer.WriteLine("NickServ-Nicknames=" + string.Join(",", Bot.NickServ[Connection.Address.ToLower()].RegisteredNicknames));
                    Writer.WriteLine("NickServ-Password=" + Bot.NickServ[Connection.Address.ToLower()].Password);
                    Writer.WriteLine("NickServ-AnyNickname=" + (Bot.NickServ[Connection.Address.ToLower()].AnyNickname ? "Yes" : "No"));
                    Writer.WriteLine("NickServ-UseGhostCommand=" + (Bot.NickServ[Connection.Address.ToLower()].UseGhostCommand ? "Yes" : "No"));
                    Writer.WriteLine("NickServ-GhostCommand=" + Bot.NickServ[Connection.Address.ToLower()].GhostCommand);
                    Writer.WriteLine("NickServ-IdentifyCommand=" + Bot.NickServ[Connection.Address.ToLower()].IdentifyCommand);
                    Writer.WriteLine("NickServ-Hostmask=" + Bot.NickServ[Connection.Address.ToLower()].Hostmask);
                    Writer.WriteLine("NickServ-RequestMask=" + Bot.NickServ[Connection.Address.ToLower()].RequestMask);
                }
            }
            Writer.WriteLine();
            Writer.WriteLine("[Prefixes]");
            foreach (KeyValuePair<string, string[]> Connection2 in Bot.ChannelCommandPrefixes) {
                Writer.WriteLine(Connection2.Key + "=" + string.Join(" ", Connection2.Value));
            }
            Writer.Close();
        }

        public static void SaveUsers() {
            StreamWriter Writer = new StreamWriter("CBotUsers.ini", false);
            foreach (KeyValuePair<string, Account> User in Bot.Accounts) {
                Writer.WriteLine("[" + User.Key + "]");
                Writer.WriteLine("Password=" + User.Value.Password);
                string[] permissions = User.Value.Permissions;
                for (int i = 0; i < permissions.Length; ++i) {
                    string Permission = permissions[i];
                    Writer.WriteLine(Permission);
                }
                Writer.WriteLine();
            }
            Writer.Close();
        }

        public static void SavePlugins() {
            StreamWriter Writer = new StreamWriter("CBotPlugins.ini", false);
            foreach (KeyValuePair<string, PluginData> Plugin in Bot.Plugins) {
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

        public static void CheckMessage(IRCClient Connection, string Sender, string Channel, string Message) {
            string[] fields = Message.Split(new char[] { ' ' }, 2);

            foreach (string c in Bot.getCommandPrefixes(Connection, Channel))
                if (fields[0].StartsWith(c)) {
                    fields[0] = fields[0].Substring(1);
                    break;
                }

            // Check global commands.
            foreach (KeyValuePair<string, PluginData> plugin in Bot.Plugins) {
                if (fields[0].Equals(plugin.Key, StringComparison.OrdinalIgnoreCase)) {
                    plugin.Value.Obj.RunCommand(Connection, Sender, Channel, Message[0] + fields[1], true);
                }
            }
        }

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

        private static void NickServCheck(IRCClient sender, string User, string Message) {
            if (NickServ.ContainsKey(sender.Address)) {
                Bot.NickServData Data = Bot.NickServ[sender.Address.ToLower()];
                if (Bot.MaskCheck(User, Data.Hostmask) && Bot.MaskCheck(Message, Data.RequestMask)) {
                    Bot.NickServIdentify(sender, User, Data);
                }
            }
        }
        private static void NickServIdentify(IRCClient sender, string User, Bot.NickServData Data) {
            if (Data.IdentifyTime == null || DateTime.Now - Data.IdentifyTime > TimeSpan.FromSeconds(60)) {
                sender.Send(Data.IdentifyCommand.Replace("$target", User.Split(new char[] { '!' })[0]).Replace("$nickname", Data.RegisteredNicknames[0]).Replace("$password", Data.Password));
                Data.IdentifyTime = DateTime.Now;
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
                    Connection.Send("NOTICE {0} :\u0001USERINFO {1}\u0001", Sender, dUserInfo);
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

        public static object Nickname() {
            bool flag = Bot.dNicknames.Count<string>() == 0;
            object Nickname;
            if (flag) {
                Nickname = "CBot";
            } else {
                Nickname = Bot.dNicknames[0];
            }
            return Nickname;
        }
        public static object Nickname(IRCClient Connection) {
            bool flag = Connection == null;
            object Nickname;
            if (flag) {
                Nickname = Bot.Nickname();
            } else {
                Nickname = Connection.Nickname;
            }
            return Nickname;
        }
        public static object Nickname(int Index) {
            bool flag = Bot.Connections.Count <= Index;
            object Nickname;
            if (flag) {
                Nickname = Bot.Nickname();
            } else {
                Nickname = Bot.Connections[Index].Nickname;
            }
            return Nickname;
        }

        public static bool UserHasPermission(IRCClient Connection, string Channel, string User, string Permission) {
            if (Connection == null) throw new ArgumentNullException("Connection");
            if (Permission == null || Permission == "") return true;

            string nickname = User.Split(new char[] { '/' }, 2)[0];

            List<string> UserPermissions = new List<string>();
            string AccountName = null;
            {
                if (Bot.Identifications.ContainsKey(Connection.Address + "/" + nickname)) {
                    AccountName = Bot.Identifications[Connection.Address + "/" + nickname].AccountName;
                }
            }
            foreach (KeyValuePair<string, Account> Account in Bot.Accounts) {
                bool match = false;
                if (Account.Key == "*") match = true;
                else if (Account.Key.StartsWith("$")) {
                    string[] fields = Account.Key.Split(new char[] { ':' }, 2);
                    string[] fields2;
                    IRC.ChannelAccess access = ChannelAccess.Normal;

                    switch (fields[0]) {
                        case "$q":
                            access = ChannelAccess.Owner;
                            break;
                        case "$a":
                            access = ChannelAccess.Admin;
                            break;
                        case "$o":
                            access = ChannelAccess.Op;
                            break;
                        case "$h":
                            access = ChannelAccess.HalfOp;
                            break;
                        case "$v":
                            access = ChannelAccess.Voice;
                            break;
                        case "$V":
                            access = ChannelAccess.HalfVoice;
                            break;
                        default:
                            match = false;
                            break;
                    }

                    if (access != ChannelAccess.Normal) {
                        IRCClient client;

                        if (Channel == null) continue;
                        fields2 = fields[1].Split(new char[] { '/' }, 2);
                        if (fields2.Length == 1) fields2 = new string[] { null, fields2[0] };

                        if (fields2[0] != null) {
                            client = null;
                            foreach (IRCClient _client in Bot.Connections) {
                                if (_client.Address.Equals(fields2[0], StringComparison.OrdinalIgnoreCase) || (_client.NetworkName ?? "").Equals(fields2[0], StringComparison.OrdinalIgnoreCase)) {
                                    Connection = _client;
                                    break;
                                }
                            }
                        } else
                            client = Connection;

                        IRC.Channel channel;
                        if (client == null) {
                            if (fields2[0] != null) match = false;
                            else {
                                match = false;
                                foreach (IRCClient _client in Bot.Connections) {
                                    if (_client.Channels.Contains(fields2[1])) {
                                        channel = _client.Channels[fields2[1]];
                                        if (channel.Users[nickname].Access >= access) {
                                            match = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        } else {
                            match = false;
                            if (Connection.Channels.Contains(fields2[1])) {
                                channel = Connection.Channels[fields2[1]];
                                if (channel.Users[nickname].Access >= access) {
                                    match = true;
                                    break;
                                }
                            }
                        }
                    }

                } else {
                    if (Account.Key.Contains("@")) {
                        User user;
                        if (Connection.Users.TryGetValue(nickname, out user))
                            match = Bot.MaskCheck(user.ToString(), Account.Key);
                    } else
                        match = AccountName != null && Account.Key.Equals(AccountName, StringComparison.OrdinalIgnoreCase);
                }

                if (match)
                    UserPermissions.AddRange(Account.Value.Permissions);
            }

            return Bot.UserHasPermissionSub(UserPermissions.ToArray(), Permission);
        }
        public static bool UserHasPermission(string AccountName, string Permission) {
            return Bot.UserHasPermissionSub(Bot.Accounts[AccountName].Permissions, Permission);
        }
        public static bool UserHasPermissionSub(string[] Permissions, string Permission) {
            int score = 0;

            string[] needleFields = Permission.Split(new char[] { '.' });
            bool IRCPermission = needleFields[0].Equals("irc", StringComparison.OrdinalIgnoreCase);

            foreach (string permission in Permissions) {
                string[] hayFields;
                if (permission == "*" && !IRCPermission) {
                    if (score <= 1) score = 1;
                } else {
                    bool polarity = true;
                    hayFields = permission.Split(new char[] { '.' });
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
                        else
                            break;

                    }
                    if (i == hayFields.Length) {
                        if ((score >> 1) <= matchLevel)
                            score = (matchLevel << 1) | (polarity ? 1 : 0);
                    }
                }
            }

            return ((score & 1) == 1);
        }
        public static T Choose<T>(params T[] args) {
            if (args == null) throw new ArgumentNullException("args");
            if (args.Length == 0) throw new ArgumentException("args must not be empty.");
            Random RNG = new Random();
            return args[RNG.Next(args.Length)];
        }
        public static T Choose<T>(int Seed, params T[] args) {
            if (args == null) throw new ArgumentNullException("args");
            if (args.Length == 0) throw new ArgumentException("args must not be empty.");
            Random RNG = new Random(Seed);
            return args[RNG.Next(args.Length)];
        }
        public static void Die() {
            Environment.Exit(0);
        }
        public static bool ParseBoolean(string s) {
            bool result;
            if (Bot.TryParseBoolean(s, out result)) return result;
            throw new ArgumentException("'" + s + "' is not recognised as true or false.", "value");
        }
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
            Exception RealException;
            if (ex is TargetInvocationException)
                RealException = ex.InnerException;
            else
                RealException = ex;
            ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] occurred in the connection to '%cWHITE{0}%cGRAY!", Server.Address);
            ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cWHITE{0} :%cGRAY {1}%r", RealException.GetType().FullName, RealException.Message);
            string[] array = RealException.StackTrace.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < array.Length; ++i) {
                string Line = array[i];
                ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cGRAY{0}%r", Line);
            }
            StreamWriter ErrorLogWriter = new StreamWriter("CBotErrorLog.txt", true);
            ErrorLogWriter.WriteLine("[{0}] ERROR occurred in the connection to '{1}!", DateTime.Now.ToString(), Server.Address);
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
        public static string WaitForMessage(IRCClient Connection, string Channel, string Nickname, uint Timeout = 0u) {
            Bot.WaitData data = new Bot.WaitData {
                Response = null
            };
            if (Channel == "@" || Channel == "PM")
                Channel = Nickname;

            Bot.Waiting.Add(((Connection == null) ? "" : (Connection.Address + "/")) + Channel + "/" + Nickname, data);
            Stopwatch stwWait = null;
            if (Timeout > 0)
                stwWait = Stopwatch.StartNew();

            while (!(((ulong) Timeout > 0uL && stwWait.ElapsedMilliseconds >= (long) checked(unchecked((ulong) Timeout) * 1000uL)) | data.Response != null))
                Thread.Sleep(100);

            Bot.Waiting.Remove(((Connection == null) ? "" : (Connection.Address + "/")) + Channel + "/" + Nickname);
            return data.Response;
        }

        public static bool Identify(string Target, string AccountName, string Password, out Identification Identification) {
            string text = null;
            return Bot.Identify(Target, AccountName, Password, out Identification, out text);
        }
        public static bool Identify(string Target, string AccountName, string Password, out Identification Identification, out string Message) {
            Account Account;
            bool flag = !Bot.Accounts.TryGetValue(AccountName, out Account);
            checked {
                bool Identify;
                if (flag) {
                    Message = "The account name or password is invalid.";
                    Identification = null;
                    Identify = false;
                } else {
                    flag = Bot.Identifications.TryGetValue(Target, out Identification);
                    if (flag) {
                        Message = "You are already identified as \u000312" + Bot.Identifications[Target].AccountName + "\u000F.";
                        Identify = false;
                    } else {
                        byte[] Salt = new byte[32];
                        byte[] OHash = new byte[32];
                        StringBuilder sbHash = new StringBuilder();
                        int i = 0;
                        int arg_B6_0;
                        int num;
                        do {
                            Salt[i] = Convert.ToByte(Account.Password.Substring(i * 2, 2), 16);
                            ++i;
                            arg_B6_0 = i;
                            num = 31;
                        }
                        while (arg_B6_0 <= num);
                        int j = 0;
                        int arg_E8_0;
                        do {
                            OHash[j] = Convert.ToByte(Account.Password.Substring(j * 2 + 64, 2), 16);
                            ++j;
                            arg_E8_0 = j;
                            num = 31;
                        }
                        while (arg_E8_0 <= num);
                        SHA256Managed SHA256M = new SHA256Managed();
                        byte[] Hash = SHA256M.ComputeHash(Salt.Concat(Encoding.UTF8.GetBytes(Password)).ToArray<byte>());
                        flag = Bot.SlowEquals(OHash, Hash);
                        if (flag) {
                            Identification = new Identification {
                                AccountName = AccountName,
                                Channels = new List<string>()
                            };
                            Bot.Identifications.Add(Target, Identification);
                            Message = "You have identified successfully as \u000309" + AccountName + "\u000F.";
                            Identify = true;
                        } else {
                            Message = "The account name or password is invalid.";
                            Identification = null;
                            Identify = false;
                        }
                    }
                }
                return Identify;
            }
        }
        public static bool SlowEquals(byte[] Data1, byte[] Data2) {
            int diff = Data1.Length ^ Data2.Length;  // The XOr operation returns 0 if, and only if, the operands are identical.
            for (int i = 0; i < 32; ++i)
                diff |= (int) (Data1[i] ^ Data2[i]);
            return (diff == 0);
        }

        public static void Say(this IRCClient connection, string channel, string message, SayOptions options) {
            if (message == null || message == "") return;

            if ((options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(message[0]);
                if (c != message[0]) message = c + message.Substring(1);
            }

            bool notice = false;
            if (channel.StartsWith("#")) {
                if ((options & SayOptions.OpsOnly) != 0) {
                    channel = "@" + channel;
                    notice = true;
                }
            } else
                notice = true;
            if ((options & SayOptions.NoticeAlways) != 0)
                notice = true;
            if ((options & SayOptions.NoticeNever) != 0)
                notice = false;

            connection.Send("{0} {1} :{2}", notice ? "NOTICE" : "PRIVMSG", channel, message);
        }
        public static void Say(this IRCClient connection, string channel, string message) {
            Bot.Say(connection, channel, message, 0);
        }
        public static void Say(this IRCClient connection, string channel, string format, params object[] args) {
            Bot.Say(connection, channel, string.Format(format, args), 0);
        }
        public static void Say(this IRCClient connection, string channel, string format, SayOptions options, params object[] args) {
            Bot.Say(connection, channel, string.Format(format, args), options);
        }
        public static void Say(this Channel channel, string message) {
            Bot.Say(channel.Client, channel.Name, message, 0);
        }
        public static void Say(this Channel channel, string message, SayOptions options) {
            Bot.Say(channel.Client, channel.Name, message, options);
        }
        public static void Say(this Channel channel, string format, params object[] args) {
            Bot.Say(channel.Client, channel.Name, string.Format(format, args), 0);
        }
        public static void Say(this Channel channel, string format, SayOptions options, params object[] args) {
            Bot.Say(channel.Client, channel.Name, string.Format(format, args), options);
        }
        public static void Say(this User user, string message) {
            Bot.Say(user.Client, user.Nickname, message, 0);
        }
        public static void Say(this User user, string message, SayOptions options) {
            Bot.Say(user.Client, user.Nickname, message, options);
        }
        public static void Say(this User user, string format, params object[] args) {
            Bot.Say(user.Client, user.Nickname, string.Format(format, args), 0);
        }
        public static void Say(this User user, string format, SayOptions options, params object[] args) {
            Bot.Say(user.Client, user.Nickname, string.Format(format, args), options);
        }

        public static void EventCheck(IRCClient Connection, string Channel, string Procedure, params object[] Parameters) {
            bool Handled = false;  // TODO: Implement this.
            foreach (KeyValuePair<string, PluginData> i in Bot.Plugins) {
                if (i.Value.Obj.IsActiveChannel(Connection, Channel)) {
                    //Type[] types;
                    //if (Parameters == null) {
                    //    types = new Type[0];
                    //} else {
                    //    types = new Type[Parameters.Length];
                    //    for (int j = 0; j < Parameters.Length; ++j) {
                    //        if (Parameters[j] == null)
                    //            types[j] = typeof(object);
                    //        else
                    //            types[j] = Parameters[j].GetType();
                    //    }
                    //}
                    MethodInfo method = i.Value.Obj.GetType().GetMethod(Procedure);
                    if (method == null) throw new MissingMethodException("No such procedure was found.");
                    try {
                        method.Invoke(i.Value.Obj, Parameters);
                    } catch (Exception ex) {
                        Bot.LogError(i.Key, Procedure, ex);
                    }
                }
                if (Handled) break;
            }
        }

        public static void OnAwayCancelled(IRCClient Connection, string Message) {
            Bot.EventCheck(Connection, "*", "OnAwayCancelled", new object[]
			{
				Connection,
				Message
			});
        }
        public static void OnAway(IRCClient Connection, string Message) {
            Bot.EventCheck(Connection, "*", "OnAway", new object[]
			{
				Connection,
				Message
			});
        }
        public static void OnBanList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) {
            Bot.EventCheck(Connection, "*", "OnBanList", new object[]
			{
				Connection,
				Channel,
				BannedUser,
				BanningUser,
				Time
			});
        }
        public static void OnBanListEnd(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, "*", "OnBanListEnd", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnNicknameChange(IRCClient Connection, string User, string NewNick) {
            bool flag = Bot.Identifications.ContainsKey(Connection.Address + "/" + User.Split(new char[] { '!' })[0]);
            if (flag) {
                Identification Identification = Bot.Identifications[Connection.Address + "/" + User.Split(new char[] { '!' })[0]];
                Bot.Identifications.Remove(Connection.Address + "/" + User.Split(new char[] { '!' })[0]);
                Bot.Identifications.Add(Connection.Address + "/" + NewNick, Identification);
            }
            Bot.EventCheck(Connection, User.Split(new char[] { '!' })[0], "OnNicknameChange", new object[]
			{
				Connection,
				User,
				NewNick
			});
        }
        public static void OnNicknameChangeSelf(IRCClient Connection, string User, string NewNick) {
            Bot.EventCheck(Connection, User.Split(new char[] { '!' })[0], "OnNicknameChangeSelf", new object[]
			{
				Connection,
				User,
				NewNick
			});
        }
        public static void OnChannelAction(IRCClient Connection, string Sender, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnChannelAction", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
        }
        public static void OnChannelActionHighlight(IRCClient Connection, string Sender, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnChannelActionHighlight", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
        }
        public static void OnChannelAdmin(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelAdmin", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelAdminSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelAdminSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelBan(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelBan", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelBanSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelBanSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelTimestamp(IRCClient Connection, string Channel, DateTime Timestamp) {
            Bot.EventCheck(Connection, Channel, "OnChannelTimestamp", new object[]
			{
				Connection,
				Channel,
				Timestamp
			});
        }
        public static void OnChannelCTCP(IRCClient Connection, string Sender, string Channel, string Message) {
            Bot.OnCTCPMessage(Connection, Sender.Split(new char[] { '!' })[0], Message);
            Bot.EventCheck(Connection, Channel, "OnChannelCTCP", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
        }
        public static void OnChannelDeAdmin(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeAdmin", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeAdminSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeAdminSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeHalfOp(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeHalfOp", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeHalfOpSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeHalfOpSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeHalfVoice(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeHalfVoice", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeHalfVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeHalfVoiceSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeOp(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeOp", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeOpSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeOpSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeOwner(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeOwner", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeOwnerSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeOwnerSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeVoice(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeVoice", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelDeVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelDeVoiceSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelExempt", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelExemptSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelExit(IRCClient Connection, string Sender, string Channel, string Reason) {
            bool flag = Bot.Identifications.ContainsKey(Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]);
            if (flag) {
                bool flag2 = Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Channels.Contains(Channel);
                if (flag2) {
                    Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Channels.Remove(Channel);
                    flag2 = (Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Channels.Count == 0);
                    if (!(Connection.SupportsWatch && Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Watched) && Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Channels.Count == 0) {
                        Bot.Identifications.Remove(Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]);
                    }
                }
            }
            Bot.EventCheck(Connection, Channel, "OnChannelExit", new object[]
			{
				Connection,
				Sender,
				Channel,
				Reason
			});
        }
        public static void OnChannelExitSelf(IRCClient Connection, string Sender, string Channel, string Reason) {
            Bot.EventCheck(Connection, Channel, "OnChannelExitSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Reason
			});
        }
        public static void OnChannelHalfOp(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelHalfOp", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelHalfOpSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelHalfOpSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelHalfVoice(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelHalfVoice", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelHalfVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelHalfVoiceSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelInviteExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelInviteExempt", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelInviteExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelInviteExemptSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelJoin(IRCClient Connection, string Sender, string Channel) {
            if (Bot.Identifications.ContainsKey(Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]))
                Bot.Identifications[Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]].Channels.Add(Channel);

            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autohalfvoice." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +V {1}", Channel, Sender.Split(new char[] { '!' })[0]);
            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autovoice." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +v {1}", Channel, Sender.Split(new char[] { '!' })[0]);
            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autohalfop." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +h {1}", Channel, Sender.Split(new char[] { '!' })[0]);
            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autoop." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +o {1}", Channel, Sender.Split(new char[] { '!' })[0]);
            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autoadmin." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +ao {1} {1}", Channel, Sender.Split(new char[] { '!' })[0]);

            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autoquiet." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-')))
                Connection.Send("MODE {0} +q *!*{1}", Channel, Sender.Split(new char[] { '!' }, 2)[1]);
            if (Bot.UserHasPermission(Connection, Channel, Sender, "irc.autoban." + Connection.Address.Replace('.', '-') + "." + Channel.Replace('.', '-'))) {
                Connection.Send("MODE {0} +b *!*{1}", Channel, Sender.Split(new char[] { '!' }, 2)[1]);
                Connection.Send("KICK {0} {1} :You are banned from this channel.", Channel, Sender.Split(new char[] { '!' })[0]);
            }

            Bot.EventCheck(Connection, Channel, "OnChannelJoin", new object[]
			{
				Connection,
				Sender,
				Channel
			});
        }
        public static void OnChannelJoinSelf(IRCClient Connection, string Sender, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelJoinSelf", new object[]
			{
				Connection,
				Sender,
				Channel
			});
        }
        public static void OnChannelJoinDeniedBanned(IRCClient Connection, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelJoinDeniedBanned", new object[]
			{
				Connection,
				Channel
			});
        }
        public static void OnChannelJoinDeniedFull(IRCClient Connection, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelJoinDeniedFull", new object[]
			{
				Connection,
				Channel
			});
        }
        public static void OnChannelJoinDeniedInvite(IRCClient Connection, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelJoinDeniedInvite", new object[]
			{
				Connection,
				Channel
			});
        }
        public static void OnChannelJoinDeniedKey(IRCClient Connection, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelJoinDeniedKey", new object[]
			{
				Connection,
				Channel
			});
        }
        public static void OnChannelKick(IRCClient Connection, string Sender, string Channel, string Target, string Reason) {
            Bot.EventCheck(Connection, Channel, "OnChannelKick", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				Reason
			});
            Bot.OnChannelExit(Connection, Target + "!*@*", Channel, "Kicked by " + Sender.Split(new char[] { '!' })[0] + ": " + Reason);
        }
        public static void OnChannelKickSelf(IRCClient Connection, string Sender, string Channel, string Target, string Reason) {
            Bot.EventCheck(Connection, Channel, "OnChannelKickSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				Reason
			});
            Bot.OnChannelExitSelf(Connection, Target + "!*@*", Channel, "Kicked by " + Sender.Split(new char[] { '!' })[0] + ": " + Reason);
        }
        public static void OnChannelList(IRCClient Connection, string Channel, int Users, string Topic) {
            Bot.EventCheck(Connection, Channel, "OnChannelList", new object[]
			{
				Connection,
				Channel,
				Users,
				Topic
			});
        }
        public static void OnChannelMessage(IRCClient Connection, string Sender, string Channel, string Message) {
            bool flag = Bot.Waiting.ContainsKey(((Sender == null) ? "" : (Connection.Address + "/")) + Channel + "/" + Sender.Split(new char[] { '!' })[0]);
            if (flag) {
                Bot.Waiting[((Sender == null) ? "" : (Connection.Address + "/")) + Channel + "/" + Sender.Split(new char[] { '!' })[0]].Response = Message;
            }
            Bot.EventCheck(Connection, Channel, "OnChannelMessage", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
            Bot.CheckMessage(Connection, Sender, Channel, Message);
        }
        public static void OnChannelMessageSendDenied(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnChannelMessageSendDenied", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnChannelMessageHighlight(IRCClient Connection, string Sender, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnChannelMessageHighlight", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
        }
        public static void OnChannelMode(IRCClient Connection, string Sender, string Channel, bool Direction, string Mode) {
            Bot.EventCheck(Connection, Channel, "OnChannelMode", new object[]
			{
				Connection,
				Sender,
				Channel,
				Direction,
				Mode
			});
        }
        public static void OnChannelModesGet(IRCClient Connection, string Channel, string Modes) {
            Bot.EventCheck(Connection, Channel, "OnChannelModesGet", new object[]
			{
				Connection,
				Channel,
				Modes
			});
        }
        public static void OnChannelNotice(IRCClient Connection, string Sender, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnChannelNotice", new object[]
			{
				Connection,
				Sender,
				Channel,
				Message
			});
        }
        public static void OnChannelOp(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelOp", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelOpSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelOpSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelOwner(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelOwner", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelOwnerSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelOwnerSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelPart(IRCClient Connection, string Sender, string Channel, string Reason) {
            Bot.EventCheck(Connection, Channel, "OnChannelPart", new object[]
			{
				Connection,
				Sender,
				Channel,
				Reason
			});
            Bot.OnChannelExit(Connection, Sender, Channel, (Reason == null ? "Left" : ("Left: " + Reason)));
        }
        public static void OnChannelPartSelf(IRCClient Connection, string Sender, string Channel, string Reason) {
            Bot.EventCheck(Connection, Channel, "OnChannelPartSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Reason
			});
            Bot.OnChannelExitSelf(Connection, (string) Sender, Channel, (Reason == null ? "Left" : ("Left: " + Reason)));
        }
        public static void OnChannelQuiet(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelQuiet", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelQuietSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelQuietSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelRemoveExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveExempt", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelRemoveExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveExemptSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelRemoveInviteExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveInviteExempt", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelRemoveInviteExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveInviteExemptSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelRemoveKey(IRCClient Connection, string Sender, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveKey", new object[]
			{
				Connection,
				Sender,
				Channel
			});
        }
        public static void OnChannelRemoveLimit(IRCClient Connection, string Sender, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnChannelRemoveLimit", new object[]
			{
				Connection,
				Sender,
				Channel
			});
        }
        public static void OnChannelSetKey(IRCClient Connection, string Sender, string Channel, string Key) {
            Bot.EventCheck(Connection, Channel, "OnChannelSetKey", new object[]
			{
				Connection,
				Sender,
				Channel,
				Key
			});
        }
        public static void OnChannelSetLimit(IRCClient Connection, string Sender, string Channel, int Limit) {
            Bot.EventCheck(Connection, Channel, "OnChannelSetLimit", new object[]
			{
				Connection,
				Sender,
				Channel,
				Limit
			});
        }
        public static void OnChannelTopic(IRCClient Connection, string Channel, string Topic) {
            Bot.EventCheck(Connection, Channel, "OnChannelTopic", new object[]
			{
				Connection,
				Channel,
				Topic
			});
        }
        public static void OnChannelTopicChange(IRCClient Connection, string Sender, string Channel, string NewTopic) {
            Bot.EventCheck(Connection, Channel, "OnChannelTopicChange", new object[]
			{
				Connection,
				Sender,
				Channel,
				NewTopic
			});
        }
        public static void OnChannelTopicStamp(IRCClient Connection, string Channel, string Setter, DateTime SetDate) {
            Bot.EventCheck(Connection, Channel, "OnChannelTopicStamp", new object[]
			{
				Connection,
				Channel,
				Setter,
				SetDate
			});
        }
        public static void OnChannelUsers(IRCClient Connection, string Channel, string Names) {
            Bot.EventCheck(Connection, Channel, "OnChannelUsers", new object[]
			{
				Connection,
				Channel,
				Names
			});
        }
        public static void OnChannelUnBan(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelUnBan", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelUnBanSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelUnBanSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelUnQuiet(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelUnQuiet", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelUnQuietSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) {
            Bot.EventCheck(Connection, Channel, "OnChannelUnQuietSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target,
				MatchedUsers
			});
        }
        public static void OnChannelVoice(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelVoice", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnChannelVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) {
            Bot.EventCheck(Connection, Channel, "OnChannelVoiceSelf", new object[]
			{
				Connection,
				Sender,
				Channel,
				Target
			});
        }
        public static void OnExemptList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) {
            Bot.EventCheck(Connection, Channel, "OnExemptList", new object[]
			{
				Connection,
				Channel,
				BannedUser,
				BanningUser,
				Time
			});
        }
        public static void OnExemptListEnd(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, "*", "OnExemptListEnd", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnInvite(IRCClient Connection, string Sender, string Channel) {
            Bot.EventCheck(Connection, Channel, "OnInvite", new object[]
			{
				Connection,
				Sender,
				Channel
			});
        }
        public static void OnInviteExemptList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) {
            Bot.EventCheck(Connection, Channel, "OnInviteExemptList", new object[]
			{
				Connection,
				Channel,
				BannedUser,
				BanningUser,
				Time
			});
        }
        public static void OnInviteExemptListEnd(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, "*", "OnInviteExemptListEnd", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnKilled(IRCClient Connection, string Sender, string Reason) {
            Bot.EventCheck(Connection, "*", "OnKilled", new object[]
			{
				Connection,
				Sender,
				Reason
			});
        }
        public static void OnNames(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnNames", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnNamesEnd(IRCClient Connection, string Channel, string Message) {
            Bot.EventCheck(Connection, Channel, "OnNamesEnd", new object[]
			{
				Connection,
				Channel,
				Message
			});
        }
        public static void OnPrivateMessage(IRCClient Connection, string Sender, string Message) {
            Bot.NickServCheck(Connection, Sender, Message);
            bool flag = Bot.Waiting.ContainsKey(((Sender == null) ? "" : (Connection.Address + "/")) + Sender.Split(new char[] { '!' })[0] + "/" + Sender.Split(new char[] { '!' })[0]);
            if (flag) {
                Bot.Waiting[((Sender == null) ? "" : (Connection.Address + "/")) + Sender.Split(new char[] { '!' })[0] + "/" + Sender.Split(new char[] { '!' })[0]].Response = Message;
            }
            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnPrivateMessage", new object[]
			{
				Connection,
				Sender,
				Message
			});
            Bot.CheckMessage(Connection, Sender, Sender.Split(new char[] { '!' })[0], Message);
        }
        public static void OnPrivateAction(IRCClient Connection, string Sender, string Message) {
            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnPrivateAction", new object[]
			{
				Connection,
				Sender,
				Message
			});
        }
        public static void OnPrivateNotice(IRCClient Connection, string Sender, string Message) {
            Bot.NickServCheck(Connection, Sender, Message);
            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnPrivateNotice", new object[]
			{
				Connection,
				Sender,
				Message
			});
        }
        public static void OnPrivateCTCP(IRCClient Connection, string Sender, string Message) {
            Bot.OnCTCPMessage(Connection, Sender.Split(new char[] { '!' })[0], Message);
            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnPrivateCTCP", new object[]
			{
				Connection,
				Sender,
				Message
			});
        }
        public static void OnQuit(IRCClient Connection, string Sender, string Reason) {
            bool flag = Bot.Identifications.ContainsKey(Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]);
            if (flag) {
                Bot.Identifications.Remove(Connection.Address + "/" + Sender.Split(new char[] { '!' })[0]);
            }
            foreach (Channel Channel in Connection.Channels) {
                flag = Channel.Users.Contains(Sender.Split(new char[] { '!' })[0]);
                if (flag) {
                    Bot.OnChannelExit(Connection, Sender, Channel.Name, (Reason.StartsWith("Quit:") ? "Quit: " : "Disconnected: ") + Reason);
                }
            }

            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnQuit", new object[]
			{
				Connection,
				Sender,
				Reason
			});

            if (Connection.CaseMappingComparer.Equals(Sender.Split(new char[] { '!' })[0], Connection.Nicknames[0]))
                Connection.Send("NICK {0}", Connection.Nicknames[0]);
        }
        public static void OnQuitSelf(IRCClient Connection, string Sender, string Reason) {
            foreach (Channel Channel in Connection.Channels)
                Bot.OnChannelExitSelf(Connection, (string) Sender, Channel.Name, (Reason.StartsWith("Quit:") ? "Quit: " : "Disconnected: ") + Reason);

            Bot.EventCheck(Connection, Sender.Split(new char[] { '!' })[0], "OnQuitSelf", new object[]
			{
				Connection,
				Sender,
				Reason
			});
        }
        public static void OnRawLineReceived(IRCClient Connection, string Message) {
            Bot.EventCheck(Connection, "*", "OnRawLineReceived", new object[]
			{
				Connection,
				Message
			});
        }
        public static void OnTimeOut(IRCClient Connection) {
            ConsoleUtils.WriteLine("%cREDPing timeout at " + Connection.Address + "%r");
            Bot.EventCheck(Connection, "*", "OnTimeOut", new object[]
			{
				Connection
			});
        }
        public static void OnUserModesSet(IRCClient Connection, string Sender, string Modes) {
            Bot.EventCheck(Connection, "*", "OnUserModesSet", new object[]
			{
				Connection,
				Sender,
				Modes
			});
        }
        public static void OnServerNotice(IRCClient Connection, string Sender, string Message) {
            Bot.EventCheck(Connection, "*", "OnServerNotice", new object[]
			{
				Connection,
				Sender,
				Message
			});
        }
        public static void OnServerError(IRCClient Connection, string Message) {
            Bot.EventCheck(Connection, "*", "OnServerError", new object[]
			{
				Connection,
				Message
			});
        }
        public static void OnServerMessage(IRCClient Connection, string Sender, string Numeric, string[] Parameters, string Message) {
            if (Numeric == "001") {
                // Identify with NickServ.
                if (Bot.NickServ.ContainsKey(Connection.Address)) {
                    Match match = null;
                    Bot.NickServData Data = Bot.NickServ[Connection.Address.ToLower()];
                    if (Data.AnyNickname || Data.RegisteredNicknames.Contains(Connection.Nickname)) {
                        // Identify to NickServ.
                        match = Regex.Match(Data.Hostmask, "^([A-}]+)(?![^!])");
                        Bot.NickServIdentify(Connection, match.Success ? match.Groups[1].Value : "NickServ", Data);
                    }

                    // If we're not on our main nickname, use the GHOST command.
                    if (Data.UseGhostCommand && Connection.Nickname != Connection.Nicknames[0]) {
                        if (match == null) match = Regex.Match(Data.Hostmask, "^([A-}]+)(?![^!])");
                        Connection.Send(Data.GhostCommand.Replace("$target", match.Success ? match.Groups[1].Value : "NickServ")
                                                            .Replace("$password", Data.Password));
                        Thread.Sleep(1000);
                        Connection.Send("NICK {0}", Connection.Nicknames[0]);
                    }
                }

                // Join channels.
                Thread autoJoinThread = new Thread(Bot.AutoJoin);
                autoJoinThread.Start(Connection);

            } else if (Numeric == "604") {  // Watched user is online
                Identification id;
                if (Bot.Identifications.TryGetValue(Connection.Address + "/" + Parameters[1], out id))
                    id.Watched = true;
            } else if (Numeric == "601") {  // Watched user went offline
                Bot.Identifications.Remove(Connection.Address + "/" + Parameters[1]);
            } else if (Numeric == "605") {  // Watched user is offline
                Bot.Identifications.Remove(Connection.Address + "/" + Parameters[1]);
            } else if (Numeric == "602") {  // Stopped watching
                Identification id;
                if (Bot.Identifications.TryGetValue(Connection.Address + "/" + Parameters[1], out id)) {
                    id.Watched = false;
                    if (id.Channels.Count == 0) Bot.Identifications.Remove(Connection.Address + "/" + Parameters[1]);
                }
            }

            Bot.EventCheck(Connection, "*", "OnServerMessage", new object[]
			{
				Connection,
				Sender,
				Numeric,
				Message
			});
        }
        public static void OnServerMessageUnhandled(IRCClient Connection, string Sender, string Numeric, string[] Parameters, string Message) {
            Bot.EventCheck(Connection, "*", "OnServerMessageUnhandled", new object[]
			{
				Connection,
				Sender,
				Numeric,
				Message
			});
        }
        public static void OnWhoList(IRCClient Connection, string Channel, string Username, string Address, string Server, string Nickname, string Flags, int Hops, string FullName) {
            Bot.EventCheck(Connection, Channel, "OnWhoList", new object[]
			{
				Connection,
				Channel,
				Username,
				Address,
				Server,
				Nickname,
				Flags,
				Hops,
				FullName
			});
        }

        private static void AutoJoin(object _client) {
            IRCClient client = (IRCClient) _client;
            Thread.Sleep(5000);
            if (client.IsConnected) {
                if (Bot.AutoJoinChannels.ContainsKey(client.Address)) {
                    string[] array = Bot.AutoJoinChannels[client.Address.ToLower()];
                    for (int i = 0; i < array.Length; ++i) {
                        string c = array[i];
                        ConsoleUtils.WriteLine("%cGRAYTrying to join the channel %cWHITE{0}%cGRAY on %cWHITE{1}%r", c, client.Address);
                        client.Send("JOIN :{0}", c);
                    }
                }
            }
        }
    }
}
