/* General to-do list:
 *   TODO: Spam proof commands.
 *   TODO: Implement JSON configuration.
 */

#pragma warning disable 4014  // Async method call without await

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using AnIRC;
using Newtonsoft.Json;

namespace CBot {
	/// <summary>
	/// The main class of CBot.
	/// </summary>
	public class Bot {
		/// <summary>Returns the version of the bot, as returned to a CTCP VERSION request.</summary>
		public static string ClientVersion { get; } = $"CBot by Andrio Celos: version {Assembly.GetExecutingAssembly().GetName().Version}";
		public Config Config { get; private set; } = new Config();
		/// <summary>The list of IRC connections the bot has.</summary>
		public List<ClientEntry> Clients { get; } = new();
		/// <summary>The list of loaded plugins.</summary>
		public PluginCollection Plugins { get; } = new();
		/// <summary>Acts as a staging area to compare the plugin configuration file with the currently loaded plugins.</summary>
		internal Dictionary<string, PluginEntry>? NewPlugins;
		/// <summary>The list of users who are identified.</summary>
		public Dictionary<string, Identification> Identifications { get; } = new(StringComparer.InvariantCultureIgnoreCase);
		/// <summary>The list of user accounts that are known to the bot.</summary>
		public Dictionary<string, Account> Accounts { get; private set; } = new(StringComparer.InvariantCultureIgnoreCase);

		/// <summary>The list of default command prefixes. A command line can start with any of these if not in a channel that has its own set.</summary>
		public string[] DefaultCommandPrefixes { get; set; } = new string[] { "!" };
		/// <summary>The collection of channel command prefixes. The keys are channel names in the form NetworkName/#channel, and the corresponding value is the array of command prefixes for that channel.</summary>
		public Dictionary<string, string[]> ChannelCommandPrefixes { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);

		public string[] DefaultNicknames { get; set; } = new string[] { "CBot" };
		public string DefaultIdent { get; set; } = "CBot";
		public string DefaultFullName { get; set; } = "CBot by Andrio Celos";
		public string DefaultUserInfo { get; set; } = "CBot by Andrio Celos";
		public string? DefaultAvatar { get; set; } = null;

		public string ConfigPath { get; set; } = "config";
		public string LanguagesPath { get; set; } = "lang";
		public string? PluginsPath { get; set; } = "plugins";
		public string Language { get; set; } = "Default";

		private bool ConfigFileFound;
		private bool UsersFileFound;
		private bool PluginsFileFound;
		private readonly Random rng = new();
		private readonly ConsoleClient consoleClient;

		/// <summary>The minimum compatible plugin API version with this version of CBot.</summary>
		public static readonly Version MinPluginVersion = new(4, 0);

		/// <summary>Indicates whether there are any NickServ-based permissions.</summary>
		internal bool commandCallbackNeeded;

		private readonly Regex commandMaskRegex  = new(@"^((?:PASS|AUTHENTICATE|OPER|DIE|RESTART) *:?)(?!\*$|\+$|PLAIN|EXTERNAL|DH-).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private readonly Regex commandMaskRegex2 = new(@"^((?:PRIVMSG *)?(?:NICKSERV|CHANSERV|NS|CS) *:?(?:ID(?:ENTIFY)?|GHOST|REGAIN|REGISTER) *).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		public event EventHandler<IrcClientEventArgs>? IrcClientAdded;
		public event EventHandler<IrcClientEventArgs>? IrcClientRemoved;

		public Bot() => this.consoleClient = new(this);

		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="channel">The name of the channel to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public string[] GetCommandPrefixes(string? channel) {
			if (channel == null || !this.ChannelCommandPrefixes.TryGetValue(channel, out var prefixes))
				prefixes = this.DefaultCommandPrefixes;
			return prefixes;
		}
		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="channel">The channel to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public string[] GetCommandPrefixes(IrcChannel? channel) => this.GetCommandPrefixes(channel?.Client == null ? channel?.Name : channel.Client.NetworkName + "/" + channel.Name);
		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="client">The IRC connection to the network on which the channel to check is.</param>
		/// <param name="channel">The name of the channel to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public string[] GetCommandPrefixes(ClientEntry? client, string? channel) => this.GetCommandPrefixes(client == null ? channel : client.Name + "/" + channel);
		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="client">The IRC connection to the network on which the channel to check is.</param>
		/// <param name="channel">The name of the channel to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public string[] GetCommandPrefixes(IrcClient? client, string? channel) => this.GetCommandPrefixes(client == null ? channel : client.NetworkName + "/" + channel);
		/// <summary>Returns the command prefixes in use in a specified channel.</summary>
		/// <param name="target">The channel or query target to check.</param>
		/// <returns>The specified channel's command prefixes, or the default set if no custom set is present.</returns>
		public string[] GetCommandPrefixes(IrcMessageTarget? target) => this.GetCommandPrefixes(target is not null ? target.Client.NetworkName + "/" + target.Target : null);

		/// <summary>Sets up an IRC network configuration and adds it to the list of loaded networks.</summary>
		public void AddNetwork(ClientEntry network) {
			this.SetUpNetwork(network);
			IrcClientAdded?.Invoke(null, new IrcClientEventArgs(network));
			this.Clients.Add(network);
		}

		/// <summary>Sets up an IRC network configuration, including adding CBot's event handlers.</summary>
		public void SetUpNetwork(ClientEntry network) {
			var newClient = new IrcClient(new IrcLocalUser((network.Nicknames ?? this.DefaultNicknames)[0], network.Ident ?? this.DefaultIdent, network.FullName ?? this.DefaultFullName), network.Name);
			this.SetUpClientEvents(newClient);
			network.Client = newClient;
		}

		public void RemoveNetwork(ClientEntry network) {
			if (this.Clients.Remove(network))
				IrcClientRemoved?.Invoke(null, new IrcClientEventArgs(network));
		}

		/// <summary>Gets the IRC network a given <see cref="IrcClient"/> belongs to, or null if it is not known.</summary>
		public ClientEntry? GetClientEntry(IrcClient client) => this.Clients.FirstOrDefault(c => c.Client == client);

		/// <summary>Adds CBot's event handlers to an <see cref="IrcClient"/> object. This can be called by plugins creating their own <see cref="IrcClient"/> objects.</summary>
		/// <param name="newClient">The IRCClient object to add event handlers to.</param>
		public void SetUpClientEvents(IrcClient newClient) {
			newClient.RawLineReceived += delegate(object? sender, IrcLineEventArgs e) {
				ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKGREEN>>%cDKGRAY {1}%r", ((IrcClient) sender!).NetworkName, e.Data);
			};
			newClient.RawLineSent += delegate(object? sender, RawLineEventArgs e) {
				Match m;
				m = this.commandMaskRegex.Match(e.Data);
				if (!m.Success) m = this.commandMaskRegex2.Match(e.Data);
				if (m.Success)
					ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}***%r", ((IrcClient) sender!).NetworkName, m.Groups[1]);
				else
					ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}%r", ((IrcClient) sender!).NetworkName, e.Data);
			};

			newClient.AwayCancelled += this.OnAwayCancelled;
			newClient.AwayMessage += this.OnAwayMessage;
			newClient.AwaySet += this.OnAwaySet;
			newClient.CapabilitiesAdded += this.OnCapabilitiesAdded;
			newClient.CapabilitiesDeleted += this.OnCapabilitiesDeleted;
			newClient.ChannelAction += this.OnChannelAction;
			newClient.ChannelAdmin += this.OnChannelAdmin;
			newClient.ChannelBan += this.OnChannelBan;
			newClient.ChannelBanList += this.OnChannelBanList;
			newClient.ChannelBanListEnd += this.OnChannelBanListEnd;
			newClient.ChannelBanRemoved += this.OnChannelBanRemoved;
			newClient.ChannelCTCP += this.OnChannelCTCP;
			newClient.ChannelDeAdmin += this.OnChannelDeAdmin;
			newClient.ChannelDeHalfOp += this.OnChannelDeHalfOp;
			newClient.ChannelDeHalfVoice += this.OnChannelDeHalfVoice;
			newClient.ChannelDeOp += this.OnChannelDeOp;
			newClient.ChannelDeOwner += this.OnChannelDeOwner;
			newClient.ChannelDeVoice += this.OnChannelDeVoice;
			newClient.ChannelExempt += this.OnChannelExempt;
			newClient.ChannelExemptRemoved += this.OnChannelExemptRemoved;
			newClient.ChannelHalfOp += this.OnChannelHalfOp;
			newClient.ChannelHalfVoice += this.OnChannelHalfVoice;
			newClient.ChannelInviteExempt += this.OnChannelInviteExempt;
			newClient.ChannelInviteExemptList += this.OnChannelInviteExemptList;
			newClient.ChannelInviteExemptListEnd += this.OnChannelInviteExemptListEnd;
			newClient.ChannelInviteExemptRemoved += this.OnChannelInviteExemptRemoved;
			newClient.ChannelJoin += this.OnChannelJoin;
			newClient.ChannelJoinDenied += this.OnChannelJoinDenied;
			newClient.ChannelKeyRemoved += this.OnChannelKeyRemoved;
			newClient.ChannelKeySet += this.OnChannelKeySet;
			newClient.ChannelKick += this.OnChannelKick;
			newClient.ChannelLeave += this.OnChannelLeave;
			newClient.ChannelLimitRemoved += this.OnChannelLimitRemoved;
			newClient.ChannelLimitSet += this.OnChannelLimitSet;
			newClient.ChannelList += this.OnChannelList;
			newClient.ChannelListChanged += this.OnChannelListChanged;
			newClient.ChannelListEnd += this.OnChannelListEnd;
			newClient.ChannelMessage += this.OnChannelMessage;
			newClient.ChannelMessageDenied += this.OnChannelMessageDenied;
			newClient.ChannelModeChanged += this.OnChannelModeChanged;
			newClient.ChannelModesGet += this.OnChannelModesGet;
			newClient.ChannelModesSet += this.OnChannelModesSet;
			newClient.ChannelNotice += this.OnChannelNotice;
			newClient.ChannelOp += this.OnChannelOp;
			newClient.ChannelOwner += this.OnChannelOwner;
			newClient.ChannelPart += this.OnChannelPart;
			newClient.ChannelQuiet += this.OnChannelQuiet;
			newClient.ChannelQuietRemoved += this.OnChannelQuietRemoved;
			newClient.ChannelStatusChanged += this.OnChannelStatusChanged;
			newClient.ChannelTimestamp += this.OnChannelTimestamp;
			newClient.ChannelTopicChanged += this.OnChannelTopicChanged;
			newClient.ChannelTopicReceived += this.OnChannelTopicReceived;
			newClient.ChannelTopicStamp += this.OnChannelTopicStamp;
			newClient.ChannelVoice += this.OnChannelVoice;
			newClient.Disconnected += this.OnDisconnected;
			newClient.Exception += this.OnException;
			newClient.ExemptList += this.OnExemptList;
			newClient.ExemptListEnd += this.OnExemptListEnd;
			newClient.Invite += this.OnInvite;
			newClient.InviteSent += this.OnInviteSent;
			newClient.Killed += this.OnKilled;
			newClient.MOTD += this.OnMOTD;
			newClient.Names += this.OnNames;
			newClient.NamesEnd += this.OnNamesEnd;
			newClient.NicknameChange += this.OnNicknameChange;
			newClient.NicknameChangeFailed += this.OnNicknameChangeFailed;
			newClient.NicknameInvalid += this.OnNicknameInvalid;
			newClient.NicknameTaken += this.OnNicknameTaken;
			newClient.Pong += this.OnPingReply;
			newClient.PingReceived += this.OnPingRequest;
			newClient.PrivateAction += this.OnPrivateAction;
			newClient.PrivateCTCP += this.OnPrivateCTCP;
			newClient.PrivateMessage += this.OnPrivateMessage;
			newClient.PrivateNotice += this.OnPrivateNotice;
			newClient.RawLineReceived += this.OnRawLineReceived;
			newClient.RawLineSent += this.OnRawLineSent;
			newClient.RawLineUnhandled += this.OnRawLineUnhandled;
			newClient.Registered += this.OnRegistered;
			newClient.ServerError += this.OnServerError;
			newClient.ServerNotice += this.OnServerNotice;
			newClient.StateChanged += this.OnStateChanged;
			newClient.UserDisappeared += this.OnUserDisappeared;
			newClient.UserModesGet += this.OnUserModesGet;
			newClient.UserModesSet += this.OnUserModesSet;
			newClient.UserQuit += this.OnUserQuit;
			newClient.ValidateCertificate += this.OnValidateCertificate;
			newClient.Wallops += this.OnWallops;
			newClient.WhoIsAuthenticationLine += this.OnWhoIsAuthenticationLine;
			newClient.WhoIsChannelLine += this.OnWhoIsChannelLine;
			newClient.WhoIsEnd += this.OnWhoIsEnd;
			newClient.WhoIsHelperLine += this.OnWhoIsHelperLine;
			newClient.WhoIsIdleLine += this.OnWhoIsIdleLine;
			newClient.WhoIsNameLine += this.OnWhoIsNameLine;
			newClient.WhoIsOperLine += this.OnWhoIsOperLine;
			newClient.WhoIsRealHostLine += this.OnWhoIsRealHostLine;
			newClient.WhoIsServerLine += this.OnWhoIsServerLine;
			newClient.WhoList += this.OnWhoList;
			newClient.WhoWasEnd += this.OnWhoWasEnd;
			newClient.WhoWasNameLine += this.OnWhoWasNameLine;
		}

		/// <summary>The program's entry point.</summary>
		/// <returns>
		///   0 if the program terminates normally (as a result of the die command);
		///   2 if the program terminates because of an error during loading.
		/// </returns>
		public int Main() {
			if (Environment.OSVersion.Platform >= PlatformID.Unix)
				Console.TreatControlCAsInput = true;  // There is a bug in Windows that occurs when this is set.

			var assembly = Assembly.GetExecutingAssembly();
			var version = assembly.GetName().Version;

			Console.Write("Loading configuration file...");
			if (File.Exists("config.json") || File.Exists("CBotConfig.ini")) {
				this.ConfigFileFound = true;
				try {
					this.LoadConfig(false);

					// Add the console client. (Default identity settings must be loaded before doing this.)
					this.Clients.Add(new ClientEntry("!Console", "!Console", 0, this.consoleClient) { SaveToConfig = false });
					this.SetUpClientEvents(this.consoleClient);

					foreach (var network in this.Config.Networks) {
						network.SaveToConfig = true;
						this.AddNetwork(network);
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
				this.UsersFileFound = true;
				try {
					this.LoadUsers();
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
				this.PluginsFileFound = true;
				Console.WriteLine();
				bool success = this.LoadPluginConfig();
				if (!success) {
					Console.WriteLine();
					ConsoleUtils.WriteLine("Some plugins failed to load.");
					ConsoleUtils.WriteLine("%cWHITEPress Escape to exit, or any other key to continue . . .");
					if (Console.ReadKey(true).Key == ConsoleKey.Escape) return 2;
				}
			} else {
				ConsoleUtils.WriteLine(" %cBLUEplugins.json is missing.%r");
			}
			this.FirstRun();
			if (!this.PluginsFileFound) this.LoadPluginConfig();

			this.consoleClient.Me.Nickname = this.DefaultNicknames[0];
			this.consoleClient.Me.Ident = this.DefaultIdent;
			this.consoleClient.Me.FullName = this.DefaultFullName;

			foreach (var client in this.Clients) {
				try {
					if (client.Name != "!Console")
						ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", client.Name, client.Address, client.Port);
					this.Connect(client);
				} catch (Exception ex) {
					ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", client.Name, ex.Message);
					client.StartReconnect();
				}
			}

			if (this.Config.Networks.Count == 0)
				ConsoleUtils.WriteLine("%cYELLOWNo IRC networks are defined in the configuration file. Delete config.json and restart to set one up.%r");
			ConsoleUtils.WriteLine("Type 'help' to list built-in console commands.");

			while (true) {
				var input = Console.ReadLine();
				if (input == null) {
					ConsoleUtils.WriteLine("EOF on input; shutting down...");
					foreach (var _client in this.Clients) {
						if (_client.Client.State >= IrcClientState.Registering)
							_client.Client.Send("QUIT :Shutting down.");
					}
					Thread.Sleep(2000);
					foreach (var _client in this.Clients) {
						if (_client.Client.State >= IrcClientState.Registering)
							_client.Client.Disconnect();
					}
					return 0;
				}
				string[] tokens = input.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
				if (tokens.Length == 0) continue;

				try {
					if (tokens[0].Equals("help", StringComparison.CurrentCultureIgnoreCase)) {
						ConsoleUtils.WriteLine("%cWHITECBot built-in console commands:%r");
						ConsoleUtils.WriteLine("  %cWHITEconnect <address> [[+]port] [nickname] [ident] [fullname...]%r : Connects to a new IRC network.");
						ConsoleUtils.WriteLine("  %cWHITEdie [message...]%r : Quits all IRC networks and shuts down CBot.");
						ConsoleUtils.WriteLine("  %cWHITEenter <message...>%r : Sends a chat message that would otherwise look like a command.");
						ConsoleUtils.WriteLine("  %cWHITEload [key] <file path...>%r : Loads a plugin.");
						ConsoleUtils.WriteLine("  %cWHITEreload [config|users|plugins]%r : Reloads configuration files.");
						ConsoleUtils.WriteLine("  %cWHITEsave [config|users|plugins]%r : Saves configuration files.");
						ConsoleUtils.WriteLine("  %cWHITEsend <network> <message...>%r : Sends a raw IRC command.");
						ConsoleUtils.WriteLine("Anything else is treated as a chat message.");
					} else if (tokens[0].Equals("connect", StringComparison.CurrentCultureIgnoreCase)) {
						if (tokens.Length >= 2) {
							var network = new ClientEntry(tokens[1], tokens[1], tokens.Length > 2 ? int.Parse(tokens[2].StartsWith("+") ? tokens[2][1..] : tokens[2]) : 6667) {
								Nicknames = tokens.Length > 3 ? new[] { tokens[3] } : this.DefaultNicknames,
								Ident     = tokens.Length > 4 ? tokens[4] : this.DefaultIdent,
								FullName  = tokens.Length > 5 ? string.Join(" ", tokens.Skip(5)) : this.DefaultFullName,
								TLS       = tokens.Length > 2 && tokens[2].StartsWith("+")
							};
							this.AddNetwork(network);
							try {
								ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", network.Name, network.Address, network.Port);
								this.Connect(network);
							} catch (Exception ex) {
								ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", network.Name, ex.Message);
								network.StartReconnect();
							}
						} else
							ConsoleUtils.WriteLine("%cREDUsage: connect <address> [[+]port] [nickname] [ident] [fullname...]%r");

					} else if (tokens[0].Equals("die", StringComparison.CurrentCultureIgnoreCase)) {
						foreach (var _client in this.Clients) {
							if (_client.Client.State >= IrcClientState.Registering)
								_client.Client.Send("QUIT :{0}", tokens.Length >= 2 ? string.Join(" ", tokens.Skip(1)) : "Shutting down.");
						}
						Thread.Sleep(2000);
						foreach (var _client in this.Clients) {
							if (_client.Client.State >= IrcClientState.Registering)
								_client.Client.Disconnect();
						}
						return 0;

					} else if (tokens[0].Equals("enter", StringComparison.CurrentCultureIgnoreCase)) {
						if (tokens.Length >= 2) {
							this.consoleClient.Put(input[6..].TrimStart());
						} else
							ConsoleUtils.WriteLine("%cREDUsage: enter <message...>%r");

					} else if (tokens[0].Equals("load", StringComparison.CurrentCultureIgnoreCase)) {
						if (tokens.Length == 2) {
							this.LoadPlugin(Path.GetFileNameWithoutExtension(tokens[1]), tokens[1], new[] { "*" });
						} else if (tokens.Length > 2) {
							this.LoadPlugin(tokens[1], string.Join(" ", tokens.Skip(2)), new[] { "*" });
						} else
							ConsoleUtils.WriteLine("%cREDUsage: load [key] <file path...>%r");

					} else if (tokens[0].Equals("reload", StringComparison.CurrentCultureIgnoreCase)) {
						bool badSyntax = false;
						if (tokens.Length == 1) {
							this.LoadConfig();
							this.LoadPluginConfig();
							this.LoadUsers();
						} else if (tokens[1].Equals("config", StringComparison.CurrentCultureIgnoreCase)) this.LoadConfig();
						else if (tokens[1].Equals("plugins", StringComparison.CurrentCultureIgnoreCase)) this.LoadPluginConfig();
						else if (tokens[1].Equals("users", StringComparison.CurrentCultureIgnoreCase)) this.LoadUsers();
						else {
							ConsoleUtils.WriteLine("%cREDUsage: reload [config|plugins|users]%r");
							badSyntax = true;
						}
						if (!badSyntax) ConsoleUtils.WriteLine("Configuration reloaded successfully.");

					} else if (tokens[0].Equals("save", StringComparison.CurrentCultureIgnoreCase)) {
						bool badSyntax = false, savePlugins = false;
						if (tokens.Length == 1) {
							this.SaveConfig();
							savePlugins = true;
							this.SaveUsers();
						} else if (tokens[1].Equals("config", StringComparison.CurrentCultureIgnoreCase)) this.SaveConfig();
						else if (tokens[1].Equals("plugins", StringComparison.CurrentCultureIgnoreCase)) savePlugins = true;
						else if (tokens[1].Equals("users", StringComparison.CurrentCultureIgnoreCase)) this.SaveUsers();
						else {
							ConsoleUtils.WriteLine("%cREDUsage: save [config|plugins|users]%r");
							badSyntax = true;
						}
						if (savePlugins) {
							this.SavePlugins();
							foreach (var plugin in this.Plugins)
								plugin.Obj.OnSave();
						}
						if (!badSyntax) ConsoleUtils.WriteLine("Configuration saved successfully.");

					} else if (tokens[0].Equals("send", StringComparison.CurrentCultureIgnoreCase)) {
						if (tokens.Length < 3)
							ConsoleUtils.WriteLine("%cREDUsage: send <network> <command...>%r");
						else {
							IrcClient? client = null; 
							if (int.TryParse(tokens[1], out int i) && i >= 0 && i < this.Clients.Count)
								client = this.Clients[i].Client;
							else {
								foreach (var entry in this.Clients) {
									if (tokens[1].Equals(entry.Name, StringComparison.CurrentCultureIgnoreCase) ||
										tokens[1].Equals(entry.Address, StringComparison.CurrentCultureIgnoreCase)) {
										client = entry.Client;
										break;
									}
								}
							}

							if (client == null)
								ConsoleUtils.WriteLine("%cREDThere is no such connection.%r");
							else
								client.Send(string.Join(" ", tokens.Skip(2)));
						}

					} else {
						this.consoleClient.Put(input);
					}
				} catch (Exception ex) {
					ConsoleUtils.WriteLine("%cREDThe command failed: " + ex.Message + "%r");
					ConsoleUtils.WriteLine("%cDKRED" + ex.StackTrace + "%r");
				}
			}
		}

		#region First-run config
		private static string ReadLineOrExit() {
			var s = Console.ReadLine();
			if (s is null) Environment.Exit(1);
			return s;
		}

		private void FirstRun() {
			if (!this.ConfigFileFound)
				this.FirstRunConfig();
			if (!this.UsersFileFound)
				this.FirstRunUsers();
			if (!this.PluginsFileFound)
				this.FirstRunPlugins();
		}

		private void FirstRunConfig() {
			Console.WriteLine();
			Console.WriteLine("This appears to be the first time I have been run here. Let us take a moment to set up.");

			Console.WriteLine("Please enter the identity details I should use on IRC.");
			string[]? nicknames;
			do {
				Console.Write("Nicknames (comma- or space-separated, in order of preference): ");
				string input = ReadLineOrExit();
				nicknames = input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string nickname in nicknames) {
					if (nickname[0] is >= '0' and <= '9') {
						Console.WriteLine("A nickname can't begin with a digit.");
						nicknames = null;
						break;
					}
					foreach (char c in nickname) {
						if (c is (< 'A' or > '}') and (< '0' or > '9') and not '-') {
							Console.WriteLine("'" + nickname + "' contains invalid characters.");
							nicknames = null;
							break;
						}
					}
				}
			} while (nicknames == null);
			this.DefaultNicknames = nicknames;

			string? ident;
			do {
				Console.Write("Ident username: ");
				ident = ReadLineOrExit();
				foreach (char c in ident) {
					if (c is (< 'A' or > '}') and (< '0' or > '9') and not '-') {
						Console.WriteLine("That username contains invalid characters.");
						ident = null;
						break;
					}
				}
			} while (string.IsNullOrEmpty(ident));
			this.DefaultIdent = ident;

			do {
				Console.Write("Full name: ");
				this.DefaultFullName = ReadLineOrExit();
			} while (this.DefaultFullName == string.Empty);

			Console.Write("User info for CTCP (blank entry for the default): ");
			this.DefaultUserInfo = ReadLineOrExit();
			if (this.DefaultUserInfo == "") this.DefaultUserInfo = "CBot by Andrio Celos";

			while (true) {
				Console.Write("What do you want my command prefix to be? ");
				string input = ReadLineOrExit();
				if (input.Length != 1)
					Console.WriteLine("It must be a single character.");
				else {
					this.DefaultCommandPrefixes = new string[] { input };
					break;
				}
			}

			Console.WriteLine();

			if (BoolPrompt("Shall I connect to an IRC network? ")) {
				string networkName;
				string? address = null;
				string? password = null;
				ushort port = 0;
				bool tls = false;
				bool acceptInvalidCertificate = false;
				IEnumerable<AutoJoinChannel> autoJoinChannels;

				do {
					Console.Write("What is the name of the IRC network? ");
					networkName = ReadLineOrExit();
				} while (networkName == "");
				do {
					Console.Write("What is the address of the server? ");
					string input = ReadLineOrExit();
					if (input == "") continue;
					var match = Regex.Match(input, @"^(?>([^:]*):(?:(\+)?(\d{1,5})))$", RegexOptions.Singleline);
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
					string input = ReadLineOrExit();
					if (input.Length == 0) continue;
					if (input[0] == '+') {
						tls = true;
						input = input[1..];
					}
					if (!ushort.TryParse(input, out port) || port == 0) {
						Console.WriteLine("That is not a valid port number.");
						tls = false;
					}
				}

				if (!tls)
					tls = BoolPrompt("Should I use TLS? ");
				if (tls)
					acceptInvalidCertificate = BoolPrompt("Should I connect if the server's certificate is invalid? ");

				if (BoolPrompt("Do I need to use a password to register to the IRC server? ")) {
					Console.Write("What is it? ");
					password = PasswordPrompt();
				}

				NickServSettings? nickServ = null;
				Console.WriteLine();
				if (BoolPrompt("Is there a NickServ registration for me on " + networkName + "? ")) {
					string[]? servicesNicknames;
					do {
						Console.Write("Grouped nicknames (comma- or space-separated): ");
						servicesNicknames = ReadLineOrExit().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
						foreach (string nickname in servicesNicknames) {
							if (nickname[0] is >= '0' and <= '9') {
								Console.WriteLine("A nickname can't begin with a digit.");
								servicesNicknames = null;
								break;
							}
							foreach (char c in nickname) {
								if (c is (< 'A' or > '}') and (< '0' or > '9') and not '-') {
									Console.WriteLine("'" + nickname + "' contains invalid characters.");
									servicesNicknames = null;
									break;
								}
							}
						}
					} while (servicesNicknames == null);

					string servicesPassword;
					do {
						Console.Write("NickServ account password: ");
						servicesPassword = PasswordPrompt();
					} while (servicesPassword.Length == 0);

					var anyNickname = BoolPrompt(string.Format("Can I log in from any nickname by including '{0}' in the identify command? ", servicesNicknames[0]));
					nickServ = new(servicesNicknames, servicesPassword, anyNickname, true);
				}

				Console.WriteLine();
				Console.Write("What channels (comma- or space-separated) should I join upon connecting? ");
				autoJoinChannels = ReadLineOrExit().Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(c => new AutoJoinChannel(c));

				var network = new ClientEntry(networkName, address, port) {
					Password = password,
					TLS = tls,
					AcceptInvalidTlsCertificate = acceptInvalidCertificate,
					NickServ = nickServ,
					SaveToConfig = true
				};
				network.AutoJoin.AddRange(autoJoinChannels);
				this.AddNetwork(network);
			}

			this.SaveConfig();

			Console.WriteLine("OK, that's the IRC connection configuration done.");
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey(true);
		}

		private void FirstRunUsers() {
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
				input = ReadLineOrExit();
				if (input.Length == 0) continue;

				if (input[0] is >= 'a' and <= 'c') {
					method = input[0] - 'a';
					break;
				} else if (input[0] is >= 'A' and <= 'C') {
					method = input[0] - 'A';
					break;
				} else if (input[0] is 'd' or 'D')
					return;
				else
					Console.WriteLine("There was no such option yet.");
			}

			string prompt = method switch {
				0 => "What do you want your account name to be? ",
				1 => "What is your services account name? ",
				2 => "What hostmask should identify you? (Example: *!*you@your.vHost) ",
				_ => ""
			};
			while (true) {
				if (input == null && method == 0) {
					Console.WriteLine(prompt);
					Console.Write("For simplicity, we recommend you use your IRC nickname. ");
				} else
					Console.Write(prompt);

				input = ReadLineOrExit();
				if (input.Length == 0) continue;

				if (input.Contains(" "))
					Console.WriteLine("It can't contain spaces.");
				else if (method == 0 && input.Contains("@"))
					Console.WriteLine("It can't contain '@'.");
				else if (method == 2 && !input.Contains("@"))
					Console.WriteLine("That doesn't look like a hostmask. Please include an '@'.");
				else if (method == 2 && input.EndsWith("@*")) {
					Console.WriteLine("This would allow anyone using your nickname to pretend to be you!");
					if (BoolPrompt("Are you really sure you want to use a wildcard host? ")) {
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
					var RNG = new RNGCryptoServiceProvider();
					var SHA256M = new SHA256Managed();
					while (true) {
						var builder = new StringBuilder();

						Console.Write("Please enter a password. ");
						input = PasswordPrompt();
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
						input = PasswordPrompt();
						byte[] confirmHash = SHA256M.ComputeHash(salt.Concat(Encoding.UTF8.GetBytes(input)).ToArray());
						if (!hash.SequenceEqual(confirmHash)) {
							Console.WriteLine("The passwords don't match.");
							continue;
						}

						// Add the account and give all permissions.
						this.Accounts.Add(accountName, new Account(HashType.SHA256Salted,
							string.Join(null, salt.Select(b => b.ToString("x2"))) + string.Join(null, hash.Select(b => b.ToString("x2"))),
							new[] { "*" }));

						ConsoleUtils.WriteLine("Thank you. To log in from IRC, enter %cWHITE/msg {0} !id <password>%r or %cWHITE/msg {0} !id {1} <password>%r, without the brackets.", this.Nickname, accountName);
						break;
					}
					break;
				case 1:
					// Add the account and give all permissions.
					this.Accounts.Add("$a:" + accountName, new(new[] { "*" }));
					ConsoleUtils.WriteLine("Thank you. Don't forget to log in to your NickServ account.", this.Nickname, accountName);
					break;
				case 2:
					// Add the account and give all permissions.
					this.Accounts.Add(accountName, new(new[] { "*" }));
					ConsoleUtils.WriteLine("Thank you. Don't forget to enable your vHost, if needed.", this.Nickname, accountName);
					break;
			}

			this.SaveUsers();

			Console.WriteLine("Press any key to continue...");
			Console.ReadKey(true);
		}

		private void FirstRunPlugins() {
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
					string input = ReadLineOrExit();
					if (string.IsNullOrWhiteSpace(input)) {
						// If the user doesn't enter a path, assume that there is no specific directory containing plugins.
						// We will ask them for full paths later.
						this.PluginsPath = null;
						break;
					}

					if (Directory.Exists(input)) {
						this.PluginsPath = input;
						break;
					}
					ConsoleUtils.WriteLine("There is no such directory.");
					if (!BoolPrompt("Try again? ")) {
						this.PluginsPath = null;
						break;
					}
				}
			}

			// Prepare the menu.
			if (this.PluginsPath != null) {
				// List all DLL files in the plugins directory and show which ones are valid plugins.
				foreach (string file in Directory.GetFiles(this.PluginsPath, "*.dll")) {
					// Look for a plugin class.
					bool found = false;
					string? message = null;

					try {
						var assembly = Assembly.LoadFrom(file);
						foreach (var type in assembly.GetTypes()) {
							if (typeof(Plugin).IsAssignableFrom(type)) {
								// Check the version attribute.
								var attribute = type.GetCustomAttribute<ApiVersionAttribute>(false);

								if (attribute == null) {
									message = "Outdated plugin – no API version is specified.";
								} else if (attribute.Version < MinPluginVersion) {
									message = string.Format("Outdated plugin – built for version {0}.{1}.", attribute.Version.Major, attribute.Version.Minor);
								} else if (attribute.Version > Assembly.GetExecutingAssembly().GetName().Version) {
									message = string.Format("Outdated bot – the plugin is built for version {0}.{1}.", attribute.Version.Major, attribute.Version.Minor);
								} else {
									found = true;
									pluginFiles.Add(Path.Combine(this.PluginsPath, Path.GetFileName(file)));
									pluginList.Add(new Tuple<string, string, ConsoleColor>(pluginFiles.Count.ToString().PadLeft(2), ": " + Path.GetFileName(file), ConsoleColor.Gray));
									break;
								}
							}
						}
					} catch (ReflectionTypeLoadException ex) {
						message = "Reflection failed: " + string.Join(", ", ex.LoaderExceptions.Select(ex2 => ex2?.Message));
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
				string input; 
				while (true) {
					input = ReadLineOrExit().Trim();
					if (input == string.Empty) {
						--Console.CursorTop;
						Console.Write("Select what? (enter the letter or number) ");
						continue;
					}
					break;
				}

				string? file = null;

				if (input[0] is 'a' or 'A') {
					// Select all plugins that aren't already selected.
					foreach (string file2 in pluginFiles) {
						string key = Path.GetFileNameWithoutExtension(file2);
						if (!selected.Any(entry => entry.Item2 == file2))
							selected.Add(new Tuple<string, string, string[]>(key, file2, new string[] { "*" }));
					}
				} else if (input[0] is 'b' or 'B') {
					do {
						Console.Write("File path: ");
						input = ReadLineOrExit();
						if (File.Exists(input)) {
							file = input;
							break;
						}
						ConsoleUtils.WriteLine("There is no such file.");
					} while (BoolPrompt("Try again? "));
				} else if (input[0] is 'q' or 'Q') {
					if (selected.Count == 0) {
						Console.Write("You haven't selected any plugins. Cancel anyway? ");
						if (TryParseBoolean(ReadLineOrExit(), out bool input3) && input3) done = true;
					} else
						done = true;
				} else if (int.TryParse(input, out int input2) && input2 >= 1 && input2 <= pluginFiles.Count) {
					file = pluginFiles[input2 - 1];
				}

				if (file != null) {
					string key; string[] channels;
					// A file was selected.
					Console.Write("What key should identify this instance? (Blank entry for " + Path.GetFileNameWithoutExtension(file) + ") ");
					input = ReadLineOrExit().Trim();
					key = input == "" ? Path.GetFileNameWithoutExtension(file) : input;

					ConsoleUtils.WriteLine("You may enter one or more channels, separated by spaces or commas, in the format %cWHITE#channel%r, %cWHITENetworkName/#channel%r or %cWHITENetworkName/*%r.");
					Console.Write("What channels should this instance be active in? (Blank entry for all channels) ");
					input = ReadLineOrExit().Trim();
					channels = input == "" ? (new string[] { "*" }) : input.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

					selected.Add(new Tuple<string, string, string[]>(key, file, channels));
				}
			} while (!done);

			foreach (var entry in selected) {
				this.Plugins.Add(new PluginEntry(entry.Item1, entry.Item2, entry.Item3));
			}

			// Write out the config file.
			this.SavePlugins();

			Console.WriteLine();
			Console.WriteLine("Configuration is now complete.");
			Console.WriteLine("Press any key to continue...");
			Console.ReadKey(true);
		}

		private static bool BoolPrompt(string message) {
			string input;
			while (true) {
				Console.Write(message);
				input = ReadLineOrExit();
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

		private static string PasswordPrompt() {
			var builder = new StringBuilder();
			while (true) {
				var c = Console.ReadKey(true);
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
		public PluginEntry LoadPlugin(string key, string filename, params string[] channels) {
			var entry = new PluginEntry(key, filename, channels);
			this.LoadPlugin(entry);
			return entry;
		}
		public void LoadPlugin(PluginEntry entry) {
			Type? pluginType = null;
			string? errorMessage = null;

			if (this.Plugins.Contains(entry.Key)) throw new ArgumentException(string.Format("A plugin with key {0} is already loaded.", entry.Key), nameof(entry));

			ConsoleUtils.Write("  Loading plugin %cWHITE" + entry.Key + "%r...");
			int x = Console.CursorLeft; int y = Console.CursorTop; int x2; int y2;
			Console.WriteLine();

			try {
				// Find the plugin class.
				var assembly = Assembly.LoadFrom(entry.Filename);
				var assemblyName = assembly.GetName();

				foreach (var type in assembly.GetTypes()) {
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
				Version? pluginVersion = null;
				foreach (ApiVersionAttribute attribute in pluginType.GetCustomAttributes(typeof(ApiVersionAttribute), false)) {
					if (pluginVersion == null || pluginVersion < attribute.Version)
						pluginVersion = attribute.Version;
				}
				if (pluginVersion == null) {
					errorMessage = "Outdated plugin – no API version is specified.";
					throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' does not specify the version of CBot for which it was built.", pluginType.Name, entry.Filename));
				} else if (pluginVersion < MinPluginVersion) {
					errorMessage = string.Format("Outdated plugin – built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
					throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' was built for older version {2}.{3}.", pluginType.Name, entry.Filename, pluginVersion.Major, pluginVersion.Minor));
				} else if (pluginVersion > Assembly.GetExecutingAssembly().GetName().Version) {
					errorMessage = string.Format("Outdated bot – the plugin is built for version {0}.{1}.", pluginVersion.Major, pluginVersion.Minor);
					throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' was built for newer version {2}.{3}.", pluginType.Name, entry.Filename, pluginVersion.Major, pluginVersion.Minor));
				}

				// Construct the plugin.
				int constructorType = -1;
				foreach (var constructor in pluginType.GetConstructors()) {
					var parameters = constructor.GetParameters();
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
					plugin = (Plugin) Activator.CreateInstance(pluginType)!;
				else if (constructorType == 1)
					plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { entry.Key })!;
				else if (constructorType == 2)
					plugin = (Plugin) Activator.CreateInstance(pluginType, new object[] { entry.Key, entry.Channels })!;
				else {
					errorMessage = "Invalid – no valid constructor on the plugin class.";
					throw new InvalidPluginException(entry.Filename, string.Format("The class '{0}' in '{1}' does not contain a supported constructor.\n" +
																				   "It should be defined as 'public SamplePlugin()'", pluginType.Name, entry.Filename));
				}

				plugin.Bot = this;
				plugin.Key = entry.Key;
				plugin.Channels = entry.Channels ?? Array.Empty<string>();
				entry.Obj = plugin;

				foreach (var command in plugin.Commands.Values) {
					command.Attribute.plugin = plugin;
					try {
						command.Attribute.SetPriorityHandler();
					} catch (Exception ex) {
						throw new InvalidPluginException(entry.Filename, $"Could not resolve the command priority handler of {command.Attribute.Names[0]}.", ex);
					}
				}

				this.Plugins.Add(entry);

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

				throw;
			}
		}
		public void EnablePlugin(PluginEntry plugin) {
			this.LoadPlugin(plugin);
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
		public void EnablePlugins(List<PluginEntry> plugins) {
			var exceptions = new List<Exception>();

			foreach (var plugin in plugins) {
				try {
					this.LoadPlugin(plugin);
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

		public void DropPlugin(string key) {
			var plugin = this.Plugins[key];
			plugin.Obj.OnUnload();
			plugin.Obj.Channels = Array.Empty<string>();
			this.Plugins.Remove(key);
		}

		/// <summary>Loads configuration data from the file config.json if it is present.</summary>
		public void LoadConfig() => this.LoadConfig(true);
		private void LoadConfig(bool update) {
			if (File.Exists("config.json")) {
				this.Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText("config.json")) ?? this.Config;
				this.DefaultNicknames = this.Config.Nicknames;
				this.DefaultIdent = this.Config.Ident;
				this.DefaultFullName = this.Config.FullName;
				this.DefaultCommandPrefixes = this.Config.CommandPrefixes;
				this.ChannelCommandPrefixes = this.Config.ChannelCommandPrefixes;
			} else if (File.Exists("CBotConfig.ini")) {
				this.Config = new Config();
				IniConfig.LoadConfig(this, this.Config);
			}

			if (update) this.UpdateNetworks();
		}
		/// <summary>Compares and applies changes in IRC network configuration.</summary>
		public void UpdateNetworks() {
			if (this.Config.Networks == null) return;  // Nothing to do.

			var oldNetworks = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
			var reconnectNeeded = new List<ClientEntry>();

			for (int i = 0; i < this.Clients.Count; ++i)
				oldNetworks[this.Clients[i].Name] = i;

			foreach (var network in this.Config.Networks) {
				ClientEntry oldNetwork; 
				if (oldNetworks.TryGetValue(network.Name, out int index)) {
					oldNetwork = this.Clients[index];
					oldNetworks.Remove(network.Name);

					network.Client = oldNetwork.Client;
					this.Clients[index] = network;

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
					this.AddNetwork(network);
					reconnectNeeded.Add(network);
				}
			}

			foreach (var index in oldNetworks.Values) {
				var network = this.Clients[index];
				if (network.SaveToConfig) {
					ConsoleUtils.WriteLine("{0} was removed.", network.Name);
					network.Client.Send("QUIT :Network dropped from configuration.");
					network.Client.Disconnect();

					this.Clients.RemoveAt(index);
				}
			}

			// Reconnect to networks.
			foreach (var network in reconnectNeeded) {
				ConsoleUtils.WriteLine("Connecting to {0} on port {1}.", network.Address, network.Port);
				this.Connect(network);
			}
		}

		internal void Connect(ClientEntry network) {
			this.UpdateNetworkSettings(network);
			network.StopReconnect();
			network.Client.Connect(network.Address, network.Port);
		}

		internal void UpdateNetworkSettings(ClientEntry network) {
			network.Client.Address = network.Address;
			network.Client.Password = network.Password;
			network.Client.SSL = network.TLS;
			network.Client.AllowInvalidCertificate = network.AcceptInvalidTlsCertificate;
			network.Client.SaslUsername = network.SaslUsername;
			network.Client.SaslPassword = network.SaslPassword;
			network.Client.Me.Nickname = (network.Nicknames ?? this.DefaultNicknames)[0];
			network.Client.Me.Ident = network.Ident ?? this.DefaultIdent;
			network.Client.Me.FullName = network.FullName ?? this.DefaultFullName;
		}

		internal void IrcNetwork_ReconnectTimerElapsed(object? sender, ElapsedEventArgs e) {
			var network = (ClientEntry) sender!;
			if (network.Client.State != IrcClientState.Disconnected) return;
			try {
				ConsoleUtils.WriteLine("Connecting to {0} ({1}) on port {2}.", network.Name, network.Address, network.Port);
				this.UpdateNetworkSettings(network);
				network.Client.Connect(network.Address, network.Port);
			} catch (Exception ex) {
				ConsoleUtils.WriteLine("%cREDConnection to {0} failed: {1}%r", network.Name, ex.Message);
				network.StartReconnect();
			}
		}


		/// <summary>Loads user data from the file CBotUsers.ini if it is present.</summary>
		public void LoadUsers() => this.LoadUsers(true);
		public void LoadUsers(bool update) {
			if (File.Exists("users.json")) {
				this.Accounts = JsonConvert.DeserializeObject<Dictionary<string, Account>>(File.ReadAllText("users.json")) ?? this.Accounts;
				this.commandCallbackNeeded = this.Accounts.Any(a => a.Key.StartsWith("$a"));
			} else if (File.Exists("CBotUsers.ini")) {
				IniConfig.LoadUsers(this);
			}

			if (update) {
				// Remove links to deleted accounts.
				var idsToRemove = new List<string>();

				foreach (var user in this.Identifications) {
					if (!this.Accounts.ContainsKey(user.Value.AccountName))
						idsToRemove.Add(user.Key);
				}
				foreach (var user in idsToRemove)
					this.Identifications.Remove(user);
			}
		}

		/// <summary>Loads active plugin data from the file CBotPlugins.ini if it is present.</summary>
		public bool LoadPluginConfig() => this.LoadPluginConfig(true);
		public bool LoadPluginConfig(bool update) {
			if (File.Exists("plugins.json")) {
				this.NewPlugins = JsonConvert.DeserializeObject<Dictionary<string, PluginEntry>>(File.ReadAllText("plugins.json"));
			} else if (File.Exists("CBotPlugins.ini")) {
				IniConfig.LoadPlugins(this);
			}

			return !update || this.UpdatePlugins();
		}
		/// <summary>Compares and applies changes in plugin configuration.</summary>
		public bool UpdatePlugins() {
			if (this.NewPlugins == null) return true;  // Nothing to do.

			bool success = true;
			var oldPlugins = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
			var reloadNeeded = new List<PluginEntry>();

			foreach (var plugin in this.Plugins) oldPlugins.Add(plugin.Key);

			foreach (var plugin in this.NewPlugins) {
				plugin.Value.Key = plugin.Key;

				if (this.Plugins.TryGetValue(plugin.Key, out var oldPlugin) && plugin.Value.Filename == oldPlugin.Filename) {
					oldPlugins.Remove(plugin.Key);
					oldPlugin.Channels = plugin.Value.Channels;
				} else {
					reloadNeeded.Add(plugin.Value);
				}
			}

			this.NewPlugins = null;

			foreach (var key in oldPlugins) {
				this.Plugins[key]?.Obj?.OnUnload();
				this.Plugins.Remove(key);
				ConsoleUtils.WriteLine("Dropped plugin {0}.", key);
			}

			// Load new plugins.
			try {
				this.EnablePlugins(reloadNeeded);
			} catch (Exception) {
				success = false;
			}

			return success;
		}

		/// <summary>Writes configuration data to the file `config.json`.</summary>
		public void SaveConfig() {
			this.Config.Nicknames = this.DefaultNicknames;
			this.Config.Ident = this.DefaultIdent;
			this.Config.FullName = this.DefaultFullName;
			this.Config.UserInfo = this.DefaultUserInfo;
			this.Config.Avatar = this.DefaultAvatar;
			this.Config.CommandPrefixes = this.DefaultCommandPrefixes;
			this.Config.ChannelCommandPrefixes = this.ChannelCommandPrefixes;
			this.Config.Nicknames = this.DefaultNicknames;

			this.Config.Networks.Clear();
			this.Config.Networks.AddRange(this.Clients.Where(n => n.SaveToConfig));

			this.Config.CommandPrefixes = this.DefaultCommandPrefixes;
			this.Config.ChannelCommandPrefixes = this.ChannelCommandPrefixes;

			var json = JsonConvert.SerializeObject(this.Config, Formatting.Indented);
			File.WriteAllText("config.json", json);
		}

		/// <summary>Writes user data to the file `users.json`.</summary>
		public void SaveUsers() {
			var json = JsonConvert.SerializeObject(this.Accounts, Formatting.Indented);
			File.WriteAllText("users.json", json);
		}

		/// <summary>Writes plugin data to the file `plugins.json`.</summary>
		public void SavePlugins() {
			this.NewPlugins = new Dictionary<string, PluginEntry>();
			foreach (var plugin in this.Plugins) {
				this.NewPlugins.Add(plugin.Key, plugin);
			}
			var json = JsonConvert.SerializeObject(this.NewPlugins, Formatting.Indented);
			File.WriteAllText("plugins.json", json);
			this.NewPlugins = null;
		}

		public async Task<(Plugin plugin, Command command)?> GetCommand(IrcUser sender, IrcMessageTarget target, string? pluginKey, string label, string? parameters) {
			IEnumerable<PluginEntry> plugins;

			if (pluginKey != null) {
				if (this.Plugins.TryGetValue(pluginKey, out var plugin)) {
					plugins = new[] { plugin };
				} else
					return null;
			} else
				plugins = this.Plugins.Where(p => p.Obj.IsActiveTarget(target));

			// Find matching commands.
			var e = new CommandEventArgs(sender.Client, target, sender, Array.Empty<string>());
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
		private async Task<bool> CheckCommands(IrcUser sender, IrcMessageTarget target, string message) {
			if (!this.IsCommand(target, message, target is IrcChannel, out var pluginKey, out var label, out var prefix, out var parameters)) return false;

			var command = await this.GetCommand(sender, target, pluginKey, label, parameters);
			if (command == null) return false;

			// Check for permissions.
			var attribute = command.Value.command.Attribute;
			var permission = attribute.Permission == null ? null
				: attribute.Permission.StartsWith(".") ? command.Value.plugin.Key + attribute.Permission
				: attribute.Permission;
			try {
				if (permission != null && !await this.CheckPermissionAsync(sender, permission)) {
					if (attribute.NoPermissionsMessage != null) Say(sender.Client, sender.Nickname, attribute.NoPermissionsMessage);
					return true;
				}

				// Parse the parameters.
				string[] fields = parameters?.Split((char[]?) null, attribute.MaxArgumentCount, StringSplitOptions.RemoveEmptyEntries)
									  ?? Array.Empty<string>();
				if (fields.Length < attribute.MinArgumentCount) {
					Say(sender.Client, sender.Nickname, "Not enough parameters.");
					Say(sender.Client, sender.Nickname, string.Format("The correct syntax is \u0002{0}\u000F.", this.ReplaceCommands(attribute.Syntax, target)));
					return true;
				}

				// Run the command.
				// TODO: Run it on a separate thread?
				var entry = this.GetClientEntry(sender.Client);
				try {
					if (entry is not null) {
						entry.CurrentPlugin = command.Value.plugin;
						entry.CurrentProcedure = command.Value.command.Handler.GetMethodInfo();
					}
					var e = new CommandEventArgs(sender.Client, target, sender, fields);
					command.Value.command.Handler.Invoke(command.Value.plugin, e);
				} catch (Exception ex) {
					LogError(command.Value.plugin.Key, command.Value.command.Handler.GetMethodInfo().Name, ex);
					while (ex is TargetInvocationException or AggregateException && ex.InnerException is not null) ex = ex.InnerException;
					Say(sender.Client, target.Target, "\u00034The command failed. This incident has been logged. ({0})", ex.Message.Replace('\n', ' '));
				}
				if (entry is not null) {
					entry.CurrentPlugin = null;
					entry.CurrentProcedure = null;
				}
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
		private async Task<bool> CheckTriggers(IrcUser sender, IrcMessageTarget target, string message) {
			foreach (var pluginEntry in this.Plugins.Where(p => p.Obj.IsActiveTarget(target))) {
				var result = await pluginEntry.Obj.CheckTriggers(sender, target, message);
				if (result) return true;
			}
			return false;
		}

		public bool IsCommand(IrcMessageTarget target, string message, bool requirePrefix) => this.IsCommand(target, message, requirePrefix, out _, out _, out _, out _);
		public bool IsCommand(IrcMessageTarget target, string message, bool requirePrefix,
			out string? plugin, [MaybeNullWhen(false)] out string label, [MaybeNullWhen(false)] out string prefix, out string? parameters) {
			var match = Regex.Match(message, @"^" + Regex.Escape(target?.Client?.Me?.Nickname ?? this.DefaultNicknames[0]) + @"\.*[:,-]? ", RegexOptions.IgnoreCase);
			if (match.Success) message = message[match.Length..];

			prefix = null;
			foreach (string p in this.GetCommandPrefixes(target as IrcChannel)) {
				if (message.StartsWith(p)) {
					message = message[p.Length..];
					prefix = p;
					break;
				}
			}

			if (prefix is null) {
				if (!match.Success && requirePrefix) {
					label = null;
					plugin = null;
					parameters = null;
					return false;
				} else {
					prefix = "";
				}
			}

			var pos = message.IndexOf(' ');
			if (pos >= 0) {
				label = message.Substring(0, pos);
				do {
					++pos;
				} while (pos < message.Length && message[pos] == ' ');
				parameters = message[pos..];
			} else {
				parameters = null;
				label = message;
			}

			pos = label.IndexOf(":");
			if (pos >= 0) {
				plugin = label.Substring(0, pos);
				label = label[(pos + 1)..];
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
			var exBuilder = new StringBuilder();
			exBuilder.Append('^');

			foreach (char c in mask) {
				if (c == '*') exBuilder.Append(".*");
				else if (c == '?') exBuilder.Append('.');
				else exBuilder.Append(Regex.Escape(c.ToString()));
			}
			exBuilder.Append('$');
			mask = exBuilder.ToString();

			return Regex.IsMatch(input, mask, RegexOptions.IgnoreCase);
		}

		private void NickServCheck(IrcClient sender, IrcUser User, string Message) {
			foreach (var client in this.Clients) {
				if (client.Client == sender) {
					if (client.NickServ is not null) {
						if (MaskCheck(User.ToString(), client.NickServ.Hostmask) && MaskCheck(Message, client.NickServ.RequestMask)) {
							NickServIdentify(client, User.Nickname);
						}
					}
				}
			}
		}
		private static void NickServIdentify(ClientEntry client, string User) {
			if (client.NickServ is null) return;
			if (client.NickServ.IdentifyTime == default || DateTime.Now - client.NickServ.IdentifyTime > TimeSpan.FromSeconds(60)) {
				client.Client.Send(client.NickServ.IdentifyCommand.Replace("$target", User).Replace("$nickname", client.NickServ.RegisteredNicknames[0]).Replace("$password", client.NickServ.Password));
				client.NickServ.IdentifyTime = DateTime.Now;
			}
		}

		private void OnCTCPMessage(IrcClient client, string sender, bool isChannelMessage, string message) {
			string[] fields = message.Split(' ');

			switch (fields[0].ToUpper()) {
				case "PING":
					if (fields.Length > 1)
						client.Send("NOTICE {0} :\u0001PING {1}\u0001", sender, string.Join(" ", fields.Skip(1)));
					else
						client.Send("NOTICE {0} :\u0001PING\u0001", sender);
					break;
				case "ERRMSG":
					if (fields.Length > 1)
						client.Send("NOTICE {0} :\u0001ERRMSG No error: {1}\u0001", sender, string.Join(" ", fields.Skip(1)));
					else
						client.Send("NOTICE {0} :\u0001ERRMSG No error\u0001", sender);
					break;
				case "VERSION":
					client.Send("NOTICE {0} :\u0001VERSION {1}\u0001", sender, ClientVersion);
					break;
				case "SOURCE":
					client.Send("NOTICE {0} :\u0001SOURCE {1}\u0001", sender, "CBot: https://github.com/AndrioCelos/CBot");
					break;
				case "TIME":
					client.Send("NOTICE {0} :\u0001TIME {1:dddd d MMMM yyyy HH:mm:ss zzz}\u0001", sender, DateTime.Now);
					break;
				case "FINGER":
					var readableIdleTime = new StringBuilder(); TimeSpan idleTime;
					idleTime = DateTime.Now - client.LastSpoke;
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

					client.Send("NOTICE {0} :\u0001FINGER {1}: {3}; idle for {2}.\u0001", sender, this.DefaultNicknames[0], readableIdleTime.ToString(), this.DefaultUserInfo);
					break;
				case "USERINFO":
					client.Send("NOTICE {0} :\u0001USERINFO {1}\u0001", sender, this.DefaultUserInfo);
					break;
				case "AVATAR":
					client.Send("NOTICE {0} :\u0001AVATAR {1}\u0001", sender, this.DefaultAvatar);
					break;
				case "CLIENTINFO":
					message = fields.Length == 1
						? "CBot: https://github.com/AndrioCelos/CBot – I recognise the following CTCP queries: CLENTINFO, FINGER, PING, TIME, USERINFO, VERSION, AVATAR"
						: fields[1].ToUpper() switch {
							"PING"       => "PING <token>: Echoes the token back to verify that I am receiving your message. This is often used with a timestamp to establish the connection latency.",
							"ERRMSG"     => "ERRMSG <message>: This is the general response to an unknown query. A query of ERRMSG will return the same message back.",
							"VERSION"    => "VERSION: Returns the name and version of my client.",
							"SOURCE"     => "SOURCE: Returns information about where to get my client.",
							"TIME"       => "TIME: Returns my local date and time.",
							"FINGER"     => "FINGER: Returns my user info and the amount of time I have been idle.",
							"USERINFO"   => "USERINFO: Returns information about me.",
							"CLIENTINFO" => "CLIENTINFO [query]: Returns information about my client, and CTCP queries I recognise.",
							"AVATAR"     => "AVATAR: Returns a URL to my avatar, if one is set.",
							_ => string.Format("I don't recognise {0} as a CTCP query.", fields[1]),
						};
					client.Send("NOTICE {0} :\u0001CLIENTINFO {1}\u0001", sender, message);
					break;
				default:
					if (!isChannelMessage) client.Send("NOTICE {0} :\u0001ERRMSG I don't recognise {1} as a CTCP query.\u0001", sender, fields[0]);
					break;
			}
		}

		/// <summary>Returns the bot's default nickname, even if none is specified in configuration.</summary>
		/// <returns>The first default nickname, or 'CBot' if none are set.</returns>
		public string Nickname => this.DefaultNicknames.Length == 0 ? "CBot" : this.DefaultNicknames[0];

		/// <summary>
		/// Determines whether the specified user has the specified permission.
		/// This method does not perform WHOIS requests; await <see cref="CheckPermissionAsync(IrcUser, string)"/> to do that.
		/// </summary>
		public bool CheckPermission(IrcUser user, string permission) {
			foreach (var account in this.GetAccounts(user)) {
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
						hayFields[0] = hayFields[0][1..];
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

			return (score & 1) != 0;
		}
		/// <summary>Determines whether the specified user has the specified permission, awaiting a WHOIS request to look up their account name if necessary.</summary>
		public async Task<bool> CheckPermissionAsync(IrcUser user, string permission) {
			if (this.CheckPermission(user, permission)) return true;
			if (user.Account == null && this.commandCallbackNeeded) {
				await user.GetAccountAsync();
				return this.CheckPermission(user, permission);
			}
			return false;
		}
		/// <summary>Returns all accounts matched by the specified user.</summary>
		private IEnumerable<Account> GetAccounts(IrcUser user) {
			if (user == null) throw new ArgumentNullException(nameof(user));

			this.Identifications.TryGetValue(user.Client.NetworkName + "/" + user.Nickname, out var id);

			foreach (var account in this.Accounts) {
				bool match = false;

				if (account.Key == "*") match = true;
				else if (account.Key.StartsWith("$")) {
					string[] fields = account.Key.Split(new char[] { ':' }, 2);
					string[] fields2;
					ChannelStatus? status = null;

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
							if (fields.Length == 1) fields = new string[] { "", fields[0] };

							match = false;
							if (string.IsNullOrEmpty(fields[0]) || fields[0].Equals(user.Client.NetworkName, StringComparison.CurrentCultureIgnoreCase)) {
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
						IrcClient? client = null;

						fields2 = fields[1].Split(new char[] { '/' }, 2);
						if (fields2.Length == 1) fields2 = new string[] { "", fields2[0] };

						// Find the network.
						if (fields2[0] != null) {
							foreach (var _client in this.Clients) {
								if (_client.Name.Equals(fields2[0], StringComparison.OrdinalIgnoreCase)) {
									client = _client.Client;
									break;
								}
							}
						}

						// Find the channel.
						if (client == null) {
							if (!string.IsNullOrEmpty(fields2[0])) match = false;
							else {
								match = false;
								foreach (var _client in this.Clients) {
									if (_client.Client.Channels.TryGetValue(fields2[1], out var channel2) && channel2.Users.TryGetValue(user.Nickname, out var channelUser) &&
										channelUser.Status >= status) {
										match = true;
										break;
									}
								}
							}
						} else {
							match = false;
							if (client.Channels.TryGetValue(fields2[1], out var channel2) && channel2.Users.TryGetValue(user.Nickname, out var channelUser) &&
								channelUser.Status >= status) {
								match = true;
							}
						}
					}
				} else {
					// Check for a hostmask match.
					match = account.Key.Contains("@")
						? MaskCheck(user.ToString(), account.Key)
						: id != null && account.Key.Equals(id.AccountName, StringComparison.OrdinalIgnoreCase);
				}

				if (match) yield return account.Value;
			}
		}

		/// <summary>Returns one of the parameters, selected at random.</summary>
		/// <param name="args">The list of parameters to choose between.</param>
		/// <returns>One of the parameters, chosen at random.</returns>
		/// <exception cref="ArgumentNullException">args is null.</exception>
		/// <exception cref="ArgumentException">args is empty.</exception>
		public T Choose<T>(params T[] args)
			=> args == null
				? throw new ArgumentNullException(nameof(args))
				: args.Length > 0 ? args[this.rng.Next(args.Length)] : throw new ArgumentException("args must not be empty.");

		/// <summary>Immediately shuts down CBot.</summary>
		public static void Die() => Environment.Exit(0);

		/// <summary>Parses a string representing a Boolean value.</summary>
		/// <param name="s">The string to parse.</param>
		/// <returns>The Boolean value represented by the given string.</returns>
		/// <exception cref="ArgumentException">The string was not recognised as a Boolean value.</exception>
		/// <remarks>
		///   The following values are recognised as true:  'true', 't', 'yes', 'y', 'on'.
		///   The following values are recognised as false: 'false', 'f', 'no', 'n', 'off'.
		///   The checks are case-insensitive.
		/// </remarks>
		public static bool ParseBoolean(string s) {
			return TryParseBoolean(s, out bool result)
				? result
				: throw new ArgumentException("'" + s + "' is not recognised as true or false.");
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
			result = default;
			return false;
		}

		internal static void LogConnectionError(IrcClient Server, Exception ex) {
			var RealException = (ex is TargetInvocationException && ex.InnerException is not null) ? ex.InnerException : ex;

			ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] occurred in the connection to '%cWHITE{0}%cGRAY!", Server.NetworkName);
			ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cWHITE{0} :%cGRAY {1}%r", RealException.GetType().FullName, RealException.Message);
			string[] array = (RealException.StackTrace ?? "").Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for (int i = 0; i < array.Length; ++i) {
				string Line = array[i];
				ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cGRAY{0}%r", Line);
			}
			var ErrorLogWriter = new StreamWriter("CBotErrorLog.txt", true);
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
			var RealException = ex is TargetInvocationException && ex.InnerException is not null ? ex.InnerException : ex;
			ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] occurred in plugin '%cWHITE{0}%cGRAY' in procedure %cWHITE{1}%cGRAY!", PluginKey, Procedure);
			ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cWHITE{0} :%cGRAY {1}%r", RealException.GetType().FullName, RealException.Message);
			string[] array = (RealException.StackTrace ?? "").Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
			for (int i = 0; i < array.Length; ++i) {
				string Line = array[i];
				ConsoleUtils.WriteLine("%cGRAY[%cDKREDERROR%cGRAY] %cGRAY{0}%r", Line);
			}
			var ErrorLogWriter = new StreamWriter("CBotErrorLog.txt", true);
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
			byte[] salt = new byte[32], hash;

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
		public bool Identify(IrcUser target, string accountName, string password, [MaybeNullWhen(false)] out Identification identification)
			=> this.Identify(target, accountName, password, out identification, out _);
		/// <summary>Attempts to log in a user with a given password.</summary>
		/// <param name="target">The name and location of the user, in the form NetworkName/Nickname.</param>
		/// <param name="accountName">The name of the account to identify to.</param>
		/// <param name="password">The given password.</param>
		/// <param name="identification">If the identification succeeds, returns the identification data. Otherwise, returns null.</param>
		/// <param name="message">Returns a status message to be shown to the user.</param>
		/// <returns>true if the identification succeeded; false otherwise.</returns>
		public bool Identify(IrcUser target, string accountName, string password, [MaybeNullWhen(false)] out Identification identification, out string message) {
			bool success; 

			if (!this.Accounts.TryGetValue(accountName, out var account)) {
				// No such account.
				message = "The account name or password is invalid.";
				identification = null;
				success = false;
			} else {
				if (this.Identifications.TryGetValue(target.Client.NetworkName + "/" + target.Nickname, out identification) && identification.AccountName == accountName) {
					// The user is already identified.
					message = string.Format("You are already identified as \u000312{0}\u000F.", identification.AccountName);
					success = false;
				} else {
					if (account.VerifyPassword(password)) {
						identification = new Identification(target.Client, target.Nickname, accountName, target.Monitoring, new(target.Channels.Select(c => c.Name)));
						this.Identifications.Add(target.Client.NetworkName + "/" + target.Nickname, identification);
						message = string.Format("You have identified successfully as \u000309{0}\u000F.", accountName);
						success = true;
					} else {
						message = "The account name or password is invalid.";
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
		public static void Say(IrcClient client, string target, string message, SayOptions options) {
			if (string.IsNullOrEmpty(message)) return;

			if ((options & SayOptions.Capitalise) != 0) {
				char c = char.ToUpper(message[0]);
				if (c != message[0]) message = c + message[1..];
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
		public static void Say(IrcClient client, string channel, string message)
			=> Say(client, channel, message, 0);
		/// <summary>Sends a message to a channel or user on IRC using an appropriate command.</summary>
		/// <param name="client">The IRC connection to send to.</param>
		/// <param name="channel">The name of the channel or user to send to.</param>
		/// <param name="format">The format of the message to send, as per string.Format.</param>
		/// <param name="args">The parameters to include in the message text.</param>
		/// <remarks>
		///   By default, PRIVMSG is used for channels, and NOTICE is used for private messages. The options parameter
		///   can override this behaviour.
		/// </remarks>
		public static void Say(IrcClient client, string channel, string format, params object[] args)
			=> Say(client, channel, string.Format(format, args), 0);
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
		public static void Say(IrcClient client, string channel, string format, SayOptions options, params object[] args)
			=> Say(client, channel, string.Format(format, args), options);

		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="target">The channel to use a command prefix for.</param>
		/// <returns>A copy of text with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public string ReplaceCommands(string text, IrcMessageTarget target)
			=> ReplaceCommands(text, "!", this.GetCommandPrefixes(target)[0].ToString());
		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="target">The channel to use a command prefix for.</param>
		/// <param name="oldPrefix">The command prefix to replace in the text.</param>
		/// <returns>A copy of text with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public string ReplaceCommands(string text, IrcMessageTarget target, string oldPrefix)
			=> ReplaceCommands(text, oldPrefix, this.GetCommandPrefixes(target)[0].ToString());
		/// <summary>Replaces commands in the given text with the correct command prefix.</summary>
		/// <param name="text">The text to edit.</param>
		/// <param name="oldPrefix">The command prefix to replace in the text.</param>
		/// <param name="newPrefix">The command prefix to substitute.</param>
		/// <returns>A copy of <paramref name="text"/> with commands prefixed with the given prefix replaced with the correct command prefix.</returns>
		/// <remarks>This method will also correctly replace commands prefixed with an IRC formatting code followed by the given prefix.</remarks>
		public static string ReplaceCommands(string text, string oldPrefix, string newPrefix) {
			if (newPrefix == "$") newPrefix = "$$";  // '$' must be escaped in the regex substitution.
			return Regex.Replace(text, @"(?<=(?:^|[\s\x00-\x20])(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?)" + Regex.Escape(oldPrefix) + @"(?=(?:\x03(\d{0,2}(,\d{1,2})?)?|[\x00-\x1F])?\w)", newPrefix);
		}

#region Event handlers
		private void OnAwayCancelled(object? sender, AwayEventArgs e)                            { foreach (var entry in this.Plugins) if (entry.Obj.OnAwayCancelled(sender, e)) return; }
		private void OnAwayMessage(object? sender, AwayMessageEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnAwayMessage(sender, e)) return; }
		private void OnAwaySet(object? sender, AwayEventArgs e)                                  { foreach (var entry in this.Plugins) if (entry.Obj.OnAwaySet(sender, e)) return; }
		private void OnCapabilitiesAdded(object? sender, CapabilitiesAddedEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnCapabilitiesAdded(sender, e)) return; }
		private void OnCapabilitiesDeleted(object? sender, CapabilitiesEventArgs e)              { foreach (var entry in this.Plugins) if (entry.Obj.OnCapabilitiesDeleted(sender, e)) return; }
		private async void OnChannelAction(object? sender, ChannelMessageEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnChannelAction(sender, e)) return;
			if (await this.CheckTriggers(e.Sender, e.Channel, "ACTION " + e.Message)) return;
		}
		private void OnChannelAdmin(object? sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelAdmin(sender, e)) return; }
		private void OnChannelBan(object? sender, ChannelListChangedEventArgs e)                 { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelBan(sender, e)) return; }
		private void OnChannelBanList(object? sender, ChannelModeListEventArgs e)                { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelBanList(sender, e)) return; }
		private void OnChannelBanListEnd(object? sender, ChannelModeListEndEventArgs e)          { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelBanListEnd(sender, e)) return; }
		private void OnChannelBanRemoved(object? sender, ChannelListChangedEventArgs e)          { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelBanRemoved(sender, e)) return; }
		private void OnChannelCTCP(object? sender, ChannelMessageEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnChannelCTCP(sender, e)) return;
			this.OnCTCPMessage((IrcClient) sender!, e.Sender.Nickname, true, e.Message);
		}
		private void OnChannelDeAdmin(object? sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeAdmin(sender, e)) return; }
		private void OnChannelDeHalfOp(object? sender, ChannelStatusChangedEventArgs e)          { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeHalfOp(sender, e)) return; }
		private void OnChannelDeHalfVoice(object? sender, ChannelStatusChangedEventArgs e)       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeHalfVoice(sender, e)) return; }
		private void OnChannelDeOp(object? sender, ChannelStatusChangedEventArgs e)              { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeOp(sender, e)) return; }
		private void OnChannelDeOwner(object? sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeOwner(sender, e)) return; }
		private void OnChannelDeVoice(object? sender, ChannelStatusChangedEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelDeVoice(sender, e)) return; }
		private void OnChannelExempt(object? sender, ChannelListChangedEventArgs e)              { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelExempt(sender, e)) return; }
		private void OnChannelExemptRemoved(object? sender, ChannelListChangedEventArgs e)       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelExemptRemoved(sender, e)) return; }
		private void OnChannelHalfOp(object? sender, ChannelStatusChangedEventArgs e)            { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelHalfOp(sender, e)) return; }
		private void OnChannelHalfVoice(object? sender, ChannelStatusChangedEventArgs e)         { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelHalfVoice(sender, e)) return; }
		private void OnChannelInviteExempt(object? sender, ChannelListChangedEventArgs e)        { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelInviteExempt(sender, e)) return; }
		private void OnChannelInviteExemptList(object? sender, ChannelModeListEventArgs e)       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelInviteExemptList(sender, e)) return; }
		private void OnChannelInviteExemptListEnd(object? sender, ChannelModeListEndEventArgs e) { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelInviteExemptListEnd(sender, e)) return; }
		private void OnChannelInviteExemptRemoved(object? sender, ChannelListChangedEventArgs e) { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelInviteExemptRemoved(sender, e)) return; }
		private void OnChannelJoin(object? sender, ChannelJoinEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnChannelJoin(sender, e)) return;

			var client = (IrcClient) sender!;

			if (this.Identifications.TryGetValue(client.NetworkName + "/" + e.Sender.Nickname, out var id))
				id.Channels.Add(e.Channel.Name);

			if (e.Sender == client.Me) {
				// Send a WHOX request to get account names.
				if (client.Extensions.ContainsKey("WHOX"))
					client.Send("WHO {0} %tna,1", e.Channel);
			} else {
				if (this.CheckPermission(e.Sender, "irc.autohalfvoice." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +V {1}", e.Channel, e.Sender.Nickname);
				if (this.CheckPermission(e.Sender, "irc.autovoice." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +v {1}", e.Channel, e.Sender.Nickname);
				if (this.CheckPermission(e.Sender, "irc.autohalfop." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +h {1}", e.Channel, e.Sender.Nickname);
				if (this.CheckPermission(e.Sender, "irc.autoop." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +o {1}", e.Channel, e.Sender.Nickname);
				if (this.CheckPermission(e.Sender, "irc.autoadmin." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +ao {1} {1}", e.Channel, e.Sender.Nickname);

				if (this.CheckPermission(e.Sender, "irc.autoquiet." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-')))
					client.Send("MODE {0} +q *!*{1}", e.Channel, e.Sender.UserAndHost);
				if (this.CheckPermission(e.Sender, "irc.autoban." + client.NetworkName.Replace('.', '-') + "." + e.Channel.Name.Replace('.', '-'))) {
					client.Send("MODE {0} +b *!*{1}", e.Channel, e.Sender.UserAndHost);
					client.Send("KICK {0} {1} :You are banned from this channel.", e.Channel, e.Sender.Nickname);
				}
			}
		}
		private void OnChannelJoinDenied(object? sender, ChannelJoinDeniedEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelJoinDenied(sender, e)) return; }
		private void OnChannelKeyRemoved(object? sender, ChannelChangeEventArgs e)               { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelKeyRemoved(sender, e)) return; }
		private void OnChannelKeySet(object? sender, ChannelKeyEventArgs e)                      { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelKeySet(sender, e)) return; }
		private void OnChannelKick(object? sender, ChannelKickEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelKick(sender, e)) return; }
		private void OnChannelLeave(object? sender, ChannelPartEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnChannelLeave(sender, e)) return;
			string key = ((IrcClient) sender!).NetworkName + "/" + e.Sender.Nickname;
			if (this.Identifications.TryGetValue(key, out var id)) {
				if (id.Channels.Remove(e.Channel.Name)) {
					if (id.Channels.Count == 0 && !(((IrcClient) sender).Extensions.SupportsMonitor && id.Monitoring))
						this.Identifications.Remove(key);
				}
			}
		}
		private void OnChannelLimitRemoved(object? sender, ChannelChangeEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelLimitRemoved(sender, e)) return; }
		private void OnChannelLimitSet(object? sender, ChannelLimitEventArgs e)                  { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelLimitSet(sender, e)) return; }
		private void OnChannelList(object? sender, ChannelListEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelList(sender, e)) return; }
		private void OnChannelListChanged(object? sender, ChannelListChangedEventArgs e)         { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelListChanged(sender, e)) return; }
		private void OnChannelListEnd(object? sender, ChannelListEndEventArgs e)                 { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelListEnd(sender, e)) return; }
		private async void OnChannelMessage(object? sender, ChannelMessageEventArgs e) {
			foreach (var entry in this.Plugins) {
				if (entry.Obj.OnChannelMessage(sender, e)) return;
			}
			if (await this.CheckCommands(e.Sender, e.Channel, e.Message)) return;
			if (await this.CheckTriggers(e.Sender, e.Channel, e.Message)) return;
		}
		private void OnChannelMessageDenied(object? sender, ChannelJoinDeniedEventArgs e)        { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelMessageDenied(sender, e)) return; }
		private void OnChannelModeChanged(object? sender, ChannelModeChangedEventArgs e)         { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelModeChanged(sender, e)) return; }
		private void OnChannelModesGet(object? sender, ChannelModesSetEventArgs e)               { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelModesGet(sender, e)) return; }
		private void OnChannelModesSet(object? sender, ChannelModesSetEventArgs e)               { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelModesSet(sender, e)) return; }
		private void OnChannelNotice(object? sender, ChannelMessageEventArgs e)                  { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelNotice(sender, e)) return; }
		private void OnChannelOp(object? sender, ChannelStatusChangedEventArgs e)                { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelOp(sender, e)) return; }
		private void OnChannelOwner(object? sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelOwner(sender, e)) return; }
		private void OnChannelPart(object? sender, ChannelPartEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelPart(sender, e)) return; }
		private void OnChannelQuiet(object? sender, ChannelListChangedEventArgs e)               { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelQuiet(sender, e)) return; }
		private void OnChannelQuietRemoved(object? sender, ChannelListChangedEventArgs e)        { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelQuietRemoved(sender, e)) return; }
		private void OnChannelStatusChanged(object? sender, ChannelStatusChangedEventArgs e)     { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelStatusChanged(sender, e)) return; }
		private void OnChannelTimestamp(object? sender, ChannelTimestampEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelTimestamp(sender, e)) return; }
		private void OnChannelTopicChanged(object? sender, ChannelTopicChangeEventArgs e)        { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelTopicChanged(sender, e)) return; }
		private void OnChannelTopicReceived(object? sender, ChannelTopicEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelTopicReceived(sender, e)) return; }
		private void OnChannelTopicStamp(object? sender, ChannelTopicStampEventArgs e)           { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelTopicStamp(sender, e)) return; }
		private void OnChannelVoice(object? sender, ChannelStatusChangedEventArgs e)             { foreach (var entry in this.Plugins) if (entry.Obj.OnChannelVoice(sender, e)) return; }
		private void OnDisconnected(object? sender, DisconnectEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnDisconnected(sender, e)) return;

			if (e.Exception == null)
				ConsoleUtils.WriteLine("%cREDDisconnected from {0}.%r", ((IrcClient) sender!).NetworkName);
			else
				ConsoleUtils.WriteLine("%cREDDisconnected from {0}: {1}%r", ((IrcClient) sender!).NetworkName, e.Exception.Message);
			if (e.Reason > DisconnectReason.Quit) {
				foreach (var client in this.Clients) {
					if (client.Client == sender) {
						client.StartReconnect();
						break;
					}
				}
			}
		}
		private void OnException(object? sender, ExceptionEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnException(sender, e)) return;
			LogConnectionError((IrcClient) sender!, e.Exception);
		}
		private void OnExemptList(object? sender, ChannelModeListEventArgs e)                    { foreach (var entry in this.Plugins) if (entry.Obj.OnExemptList(sender, e)) return; }
		private void OnExemptListEnd(object? sender, ChannelModeListEndEventArgs e)              { foreach (var entry in this.Plugins) if (entry.Obj.OnExemptListEnd(sender, e)) return; }
		private void OnInvite(object? sender, InviteEventArgs e)                                 { foreach (var entry in this.Plugins) if (entry.Obj.OnInvite(sender, e)) return; }
		private void OnInviteSent(object? sender, InviteSentEventArgs e)                         { foreach (var entry in this.Plugins) if (entry.Obj.OnInviteSent(sender, e)) return; }
		private void OnKilled(object? sender, PrivateMessageEventArgs e)                         { foreach (var entry in this.Plugins) if (entry.Obj.OnKilled(sender, e)) return; }
		private void OnMOTD(object? sender, MotdEventArgs e)                                     { foreach (var entry in this.Plugins) if (entry.Obj.OnMOTD(sender, e)) return; }
		private void OnNames(object? sender, ChannelNamesEventArgs e)                            { foreach (var entry in this.Plugins) if (entry.Obj.OnNames(sender, e)) return; }
		private void OnNamesEnd(object? sender, ChannelModeListEndEventArgs e)                   { foreach (var entry in this.Plugins) if (entry.Obj.OnNamesEnd(sender, e)) return; }
		private void OnNicknameChange(object? sender, NicknameChangeEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnNicknameChange(sender, e)) return;
			string key = ((IrcClient) sender!).NetworkName + "/" + e.Sender.Nickname;
			if (this.Identifications.TryGetValue(key, out var id)) {
				this.Identifications.Remove(key);
				this.Identifications.Add(((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname, id);
			}
		}
		private void OnNicknameChangeFailed(object? sender, NicknameEventArgs e)                 { foreach (var entry in this.Plugins) if (entry.Obj.OnNicknameChangeFailed(sender, e)) return; }
		private void OnNicknameInvalid(object? sender, NicknameEventArgs e)                      { foreach (var entry in this.Plugins) if (entry.Obj.OnNicknameInvalid(sender, e)) return; }
		private void OnNicknameTaken(object? sender, NicknameEventArgs e) {
			foreach (var pluginEntry in this.Plugins) if (pluginEntry.Obj.OnNicknameTaken(sender, e)) return;
			// Cycle through the list.
			var entry = this.GetClientEntry((IrcClient) sender!);
			if (entry is null) return;
			var nicknames = entry.Nicknames ?? this.DefaultNicknames;
			if (entry.Client.State <= IrcClientState.Registering && nicknames.Length > 1) {
				for (int i = 0; i < nicknames.Length - 1; ++i) {
					if (nicknames[i] == e.Nickname) {
						entry.Client.Me.Nickname = nicknames[i + 1];
						break;
					}
				}
			}
		}
		private void OnPingReply(object? sender, PingEventArgs e)                                { foreach (var entry in this.Plugins) if (entry.Obj.OnPingReply(sender, e)) return; }
		private void OnPingRequest(object? sender, PingEventArgs e)                              { foreach (var entry in this.Plugins) if (entry.Obj.OnPingRequest(sender, e)) return; }
		private async void OnPrivateAction(object? sender, PrivateMessageEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnPrivateAction(sender, e)) return;
			if (await this.CheckTriggers(e.Sender, e.Sender, "ACTION " + e.Message)) return;
		}
		private void OnPrivateCTCP(object? sender, PrivateMessageEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnPrivateCTCP(sender, e)) return;
			this.OnCTCPMessage((IrcClient) sender!, e.Sender.Nickname, false, e.Message);
		}
		private async void OnPrivateMessage(object? sender, PrivateMessageEventArgs e) {
			foreach (var entry in this.Plugins) {
				if (entry.Obj.OnPrivateMessage(sender, e)) return;
			}
			if (await this.CheckCommands(e.Sender, e.Sender, e.Message)) return;
			if (await this.CheckTriggers(e.Sender, e.Sender, e.Message)) return;
			this.NickServCheck((IrcClient) sender!, e.Sender, e.Message);
		}
		private void OnPrivateNotice(object? sender, PrivateMessageEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnPrivateNotice(sender, e)) return;
			this.NickServCheck((IrcClient) sender!, e.Sender, e.Message);
		}
		private void OnRawLineReceived(object? sender, IrcLineEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnRawLineReceived(sender, e)) return;

			var client = (IrcClient) sender!;

			switch (e.Line.Message) {
				case Replies.RPL_WHOSPCRPL:
					if (e.Line.Parameters.Length == 4 && e.Line.Parameters[1] == "1") {  // This identifies our WHOX request.
						if (client.Users.TryGetValue(e.Line.Parameters[2], out var user)) {
							if (e.Line.Parameters[3] == "0")
								client.ReceivedLine(":" + user.ToString() + " ACCOUNT *");
							else
								client.ReceivedLine(":" + user.ToString() + " ACCOUNT " + e.Line.Parameters[3]);
						}
					}
					break;
				case Replies.RPL_NOWON:
					if (this.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out var id))
						id.Monitoring = true;
					break;
				case Replies.RPL_LOGOFF:
					this.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
					break;
				case Replies.RPL_NOWOFF:
					this.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
					break;
				case Replies.RPL_WATCHOFF:
					if (this.Identifications.TryGetValue(client.NetworkName + "/" + e.Line.Parameters[1], out id)) {
						id.Monitoring = false;
						if (id.Channels.Count == 0) this.Identifications.Remove(client.NetworkName + "/" + e.Line.Parameters[1]);
					}
					break;
			}
		}
		private void OnRawLineSent(object? sender, RawLineEventArgs e)                           { foreach (var entry in this.Plugins) if (entry.Obj.OnRawLineSent(sender, e)) return; }
		private void OnRawLineUnhandled(object? sender, IrcLineEventArgs e)                      { foreach (var entry in this.Plugins) if (entry.Obj.OnRawLineUnhandled(sender, e)) return; }
		private void OnRegistered(object? sender, RegisteredEventArgs e)                         { foreach (var entry in this.Plugins) if (entry.Obj.OnRegistered(sender, e)) return; }
		private void OnServerError(object? sender, ServerErrorEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnServerError(sender, e)) return; }
		private void OnServerNotice(object? sender, PrivateMessageEventArgs e)                   { foreach (var entry in this.Plugins) if (entry.Obj.OnServerNotice(sender, e)) return; }
		private void OnStateChanged(object? sender, StateEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnStateChanged(sender, e)) return;

			var client = (IrcClient) sender!;

			if (e.NewState == IrcClientState.Online) {
				foreach (var clientEntry in this.Clients) {
					if (clientEntry.Client == client) {
						// Identify with NickServ.
						if (clientEntry.NickServ != null) {
							Match? match = null;
							if (client.Me.Account == null && (clientEntry.NickServ.AnyNickname || clientEntry.NickServ.RegisteredNicknames.Contains(client.Me.Nickname))) {
								// Identify to NickServ.
								match = Regex.Match(clientEntry.NickServ.Hostmask, "^([A-}]+)(?![^!])");
								NickServIdentify(clientEntry, match.Success ? match.Groups[1].Value : "NickServ");
							}

							// If we're not on our main nickname, use the GHOST command.
							var primaryNickname = (clientEntry.Nicknames ?? this.DefaultNicknames)[0];
							if (clientEntry.NickServ.UseGhostCommand && client.Me.Nickname != primaryNickname) {
								if (match == null) match = Regex.Match(clientEntry.NickServ.Hostmask, "^([A-}]+)(?![^!])");
								client.Send(clientEntry.NickServ.GhostCommand.Replace("$target", match.Success ? match.Groups[1].Value : "NickServ")
																			 .Replace("$nickname", clientEntry.NickServ.RegisteredNicknames[0])
																			 .Replace("$password", clientEntry.NickServ.Password));
								client.Send("NICK {0}", primaryNickname);
							}
						}

						// Join channels.
						AutoJoin(clientEntry);
						break;
					}
				}

			}
		}
		private void OnUserDisappeared(object? sender, IrcUserEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnUserDisappeared(sender, e)) return; }
		private void OnUserModesGet(object? sender, UserModesEventArgs e)                        { foreach (var entry in this.Plugins) if (entry.Obj.OnUserModesGet(sender, e)) return; }
		private void OnUserModesSet(object? sender, UserModesEventArgs e)                        { foreach (var entry in this.Plugins) if (entry.Obj.OnUserModesSet(sender, e)) return; }
		private void OnUserQuit(object? sender, QuitEventArgs e) {
			foreach (var entry in this.Plugins) if (entry.Obj.OnUserQuit(sender, e)) return;
			string key = ((IrcClient) sender!).NetworkName + "/" + e.Sender.Nickname;
			this.Identifications.Remove(key);

			foreach (var entry in this.Clients) {
				if (entry.Client == sender) {
					string primaryNickname = (entry.Nicknames ?? this.DefaultNicknames)[0];
					if (((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, primaryNickname))
						((IrcClient) sender).Send("NICK {0}", primaryNickname);
					break;
				}
			}
		}
		private void OnValidateCertificate(object? sender, ValidateCertificateEventArgs e)       { foreach (var entry in this.Plugins) if (entry.Obj.OnValidateCertificate(sender, e)) return; }
		private void OnWallops(object? sender, PrivateMessageEventArgs e)                        { foreach (var entry in this.Plugins) if (entry.Obj.OnWallops(sender, e)) return; }
		private void OnWhoIsAuthenticationLine(object? sender, WhoisAuthenticationEventArgs e)   { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsAuthenticationLine(sender, e)) return; }
		private void OnWhoIsChannelLine(object? sender, WhoisChannelsEventArgs e)                { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsChannelLine(sender, e)) return; }
		private void OnWhoIsEnd(object? sender, WhoisEndEventArgs e)                             { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsEnd(sender, e)) return; }
		private void OnWhoIsHelperLine(object? sender, WhoisOperEventArgs e)                     { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsHelperLine(sender, e)) return; }
		private void OnWhoIsIdleLine(object? sender, WhoisIdleEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsIdleLine(sender, e)) return; }
		private void OnWhoIsNameLine(object? sender, WhoisNameEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsNameLine(sender, e)) return; }
		private void OnWhoIsOperLine(object? sender, WhoisOperEventArgs e)                       { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsOperLine(sender, e)) return; }
		private void OnWhoIsRealHostLine(object? sender, WhoisRealHostEventArgs e)               { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsRealHostLine(sender, e)) return; }
		private void OnWhoIsServerLine(object? sender, WhoisServerEventArgs e)                   { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoIsServerLine(sender, e)) return; }
		private void OnWhoList(object? sender, WhoListEventArgs e)                               { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoList(sender, e)) return; }
		private void OnWhoWasEnd(object? sender, WhoisEndEventArgs e)                            { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoWasEnd(sender, e)) return; }
		private void OnWhoWasNameLine(object? sender, WhoisNameEventArgs e)                      { foreach (var entry in this.Plugins) if (entry.Obj.OnWhoWasNameLine(sender, e)) return; }
#endregion

		private static async Task AutoJoin(ClientEntry client) {
			if (client.Client.Me.Account == null) await Task.Delay(3000);
			if (client.Client.State == IrcClientState.Online) {
				foreach (var channel in client.AutoJoin)
					if (channel.Key == null)
						client.Client.Send("JOIN {0}", channel.Channel);
					else
						client.Client.Send("JOIN {0} {1}", channel.Key);
			}
		}
	}
}
