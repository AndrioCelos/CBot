using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AnIRC;
using WebSocketSharp.Server;

namespace CBot;
public delegate void PluginCommandHandler(object? sender, CommandEventArgs e);
public delegate int PluginCommandPriorityHandler(CommandEventArgs e);
public delegate void PluginTriggerHandler(object? sender, TriggerEventArgs e);

/// <summary>Provides a base class for CBot plugin main classes.</summary>
public abstract class Plugin {
	private static readonly Regex languageEscapeRegex = new(@"\\(?:(n)|(r)|(t)|(\\)|(u)([0-9a-f]{4})?|($))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

	/// <summary>
	/// Returns the bot that loaded this plugin. Not valid before Initialize.
	/// </summary>
	public Bot Bot { get; internal set; }

	private string[] _Channels = Array.Empty<string>();
	/// <summary>
	/// Sets or returns the list of channels that this plugin will receive events for.
	/// This property can be overridden.
	/// </summary>
	public virtual string[] Channels {
		get => this._Channels;
		set => this._Channels = value ?? Array.Empty<string>();
	}

	/// <summary>
	/// When overridden, returns the name of the plugin.
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// Returns the key used to refer to this plugin. Not valid before Initialize.
	/// </summary>
	public string Key { get; internal set; }

	/// <summary>
	/// Returns the path to this plugin file. Not valid before Initialize.
	/// </summary>
	public string FilePath { get; internal set; }

	public Dictionary<string, Command> Commands { get; } = new Dictionary<string, Command>(StringComparer.CurrentCultureIgnoreCase);
	public List<Trigger> Triggers { get; } = new List<Trigger>();

	/// <summary>Indicates whether this plugin would suffer disruption if the bot were to shut down now.</summary>
	/// <remarks>May cause a process to wait before shutting down the bot, but does not guarantee this.</remarks>
	public bool ShouldInhibitShutdown { get; protected set; }

	/// <summary>
	/// Contains the message formats currently in use by this plugin.
	/// </summary>
	/// <remarks>
	/// Message formats can contain the following sequences:
	///     {...}   The same sequences as in string.Format
	///     {nick}  The relevant nickname, usually that of the user executing a command.
	///     {chan}  The relevant channel, usually that in which the command is executed or the plugin is active.
	///     {(} {)} {|} Escape, respectively, a (, ) or | character. (To escape a { or }, double it.)
	///     ((option1||option2||option3))   Chooses one of the options at random each time. Can be nested and can contain other sequences.
	/// </remarks>
	protected Dictionary<string, string> language = new();
	/// <summary>
	/// Contains the default message formats currently in use by this plugin.
	/// When an entry is not found in the Language list, the function should fall back to this list.
	/// </summary>
	protected Dictionary<string, string> defaultLanguage = new();
	private readonly Random random = new();

	/// <summary>
	/// Creates a new instance of the Plugin class.
	/// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	protected Plugin() {
						  // Register commands and triggers.
		foreach (var method in this.GetType().GetMethods()) {
			foreach (var attribute in method.GetCustomAttributes()) {
				if (attribute is CommandAttribute commandAttribute) {
					foreach (var alias in commandAttribute.Names)
						this.Commands.Add(alias, new Command(commandAttribute, (PluginCommandHandler) method.CreateDelegate(typeof(PluginCommandHandler), this)));
				} else if (attribute is TriggerAttribute triggerAttribute) {
					this.Triggers.Add(new Trigger(triggerAttribute, (PluginTriggerHandler) method.CreateDelegate(typeof(PluginTriggerHandler), this)));
				}
			}
		}
	}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

	/// <summary>
	/// When overridden, returns help text on a specific user-specified topic, if it is available and relevant to this plugin; otherwise, null.
	/// If no topic was specified, the implementation may return a brief description of what this plugin is doing.
	/// Command strings should be written using the '!' prefix; the help command plugin will replace it with the actual prefix.
	/// </summary>
	/// <param name="topic">The topic the user asked for help on, or null if none was specified.</param>
	/// <param name="target">The channel or query target in which the request was issued.</param>
	public virtual string? Help(string topic, IrcMessageTarget target) => null;

	/// <summary>
	/// Returns true if the specified channel is in this plugin's Channels list.
	/// </summary>
	/// <param name="target">The channel to check.</param>
	/// <returns>True if the specified channel is in the Channels list; false otherwise.</returns>
	public bool IsActiveTarget(IrcMessageTarget target) {
		if (target is IrcUser user) return this.IsActivePM(user);
		foreach (string channelName in this.Channels) {
			string[] fields = channelName.Split(new char[] { '/' }, 2);
			if (fields.Length == 1)
				fields = new string[] { "*", fields[0] };

			if (fields[0] == "*" || fields[0].Equals(target.Client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(target.Client.Address, StringComparison.OrdinalIgnoreCase)) {
				if (fields[1] == "*" || fields[1] == "#*" || fields[1] == "*#" || target.Client.CaseMappingComparer.Equals(fields[1], target.Target)) return true;
			}
		}
		return false;
	}
	/// <summary>
	/// Returns true if the specified channel is in this plugin's Channels list.
	/// </summary>
	/// <param name="channel">The channel to check.</param>
	/// <returns>True if the specified channel is in the Channels list; false otherwise.</returns>
	public bool IsActiveChannel(IrcChannel channel) => this.IsActiveTarget(channel);
	/// <summary>
	/// Returns true if the specified PM target is in this plugin's Channels list.
	/// Whether or not the user can be seen, or is online, is not relevant.
	/// </summary>
	/// <param name="sender">The user to check.</param>
	/// <returns>True if the specified user is in the Channels list; false otherwise.</returns>
	public bool IsActivePM(IrcUser sender) {
		foreach (string channelName in this.Channels) {
			string[] fields = channelName.Split(new char[] { '/' }, 2);
			if (fields.Length == 1)
				fields = new string[] { "*", fields[0] };

			if (fields[0] == "*" || fields[0].Equals(sender.Client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(sender.Client.Address, StringComparison.OrdinalIgnoreCase)) {
				if (fields[1] == "*" || fields[1] == "*?" || sender.Client.CaseMappingComparer.Equals(fields[1], sender.Nickname)) return true;
				if (fields[1] == "*#" && sender.Channels.Count != 0) return true;
				// Respond to PMs from users in an active channel.
				if (sender.Channels.Contains(fields[1])) return true;
			}
		}
		return false;
	}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
	public virtual async Task<IEnumerable<Command>> CheckCommands(IrcUser sender, IrcMessageTarget target, string label, string? parameters, bool isGlobalCommand) {
		var command = this.GetCommand(target, label, isGlobalCommand);
		return command != null ? new[] { command } : Enumerable.Empty<Command>();
	}
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

	public virtual async Task<bool> CheckTriggers(IrcUser sender, IrcMessageTarget target, string message) {
		if (this.Triggers.Count != 0) {
			var trigger = this.GetTrigger(target, message, out var match);
			if (trigger != null) {
				await this.RunTrigger(sender, target, trigger, match!);
				return true;
			}
		}
		return false;
	}

	/// <summary>Returns the command that matches the specified label and target, if any; otherwise, returns null.</summary>
	public Command? GetCommand(IrcMessageTarget target, string label) => this.GetCommand(target, label, false);
	/// <summary>Returns the command that matches the specified label and target, if any; otherwise, returns null.</summary>
	public Command? GetCommand(IrcMessageTarget target, string label, bool globalCommand) {
		string alias = label.Split(new char[] { ' ' })[0];
		if (!this.Commands.TryGetValue(alias, out var command)) return null;

		// Check the scope.
		return ((command.Attribute.Scope & CommandScope.PM) == 0 && !(target is IrcChannel)) ||
			((command.Attribute.Scope & CommandScope.Channel) == 0 && target is IrcChannel) ? null : command;
	}

	/// <summary>
	/// Processes a command line and runs a matching command if one exists and the user has access to it.
	/// </summary>
	/// <param name="sender">The user sending the command.</param>
	/// <param name="target">The channel in which the command was sent, or the sender nickname if it was in a private message.</param>
	/// <param name="command">The command that is being used.</param>
	/// <param name="parameters">The part of the command after the label.</param>
	/// <param name="globalCommand">True if the global command syntax was used; false otherwise.</param>
	/// <returns>True if a command was matched (even if it was denied); false otherwise.</returns>
	public async Task RunCommand(IrcUser sender, IrcMessageTarget target, Command command, string parameters, bool globalCommand = false) {
		// Check for permissions.
		var permission = command.Attribute.Permission == null ? null
			: command.Attribute.Permission.StartsWith(".") ? this.Key + command.Attribute.Permission
			: command.Attribute.Permission;
		try {
			if (permission != null && !await this.Bot.CheckPermissionAsync(sender, permission)) {
				if (command.Attribute.NoPermissionsMessage != null) Bot.Say(sender.Client, sender.Nickname, command.Attribute.NoPermissionsMessage);
				return;
			}

			// Parse the parameters.
			string[] fields = parameters?.Split((char[]?) null, command.Attribute.MaxArgumentCount, StringSplitOptions.RemoveEmptyEntries)
								  ?? Array.Empty<string>();
			if (fields.Length < command.Attribute.MinArgumentCount) {
				Bot.Say(sender.Client, sender.Nickname, "Not enough parameters.");
				Bot.Say(sender.Client, sender.Nickname, string.Format("The correct syntax is \u000312{0}\u000F.", this.Bot.ReplaceCommands(command.Attribute.Syntax, target)));
				return;
			}

			// Run the command.
			// TODO: Run it on a separate thread?
			var entry = this.Bot.GetClientEntry(sender.Client);
			try {
				if (entry != null) {
					entry.CurrentPlugin = this;
					entry.CurrentProcedure = command.Handler.GetMethodInfo();
				}
				var e = new CommandEventArgs(sender.Client, target, sender, fields);
				command.Handler.Invoke(this, e);
			} catch (Exception ex) {
				Bot.LogError(this.Key, command.Handler.GetMethodInfo().Name, ex);
				while (ex is TargetInvocationException or AggregateException && ex.InnerException is not null) ex = ex.InnerException;
				Bot.Say(sender.Client, target.Target, "\u00034The command failed. This incident has been logged. ({0})", ex.Message.Replace('\n', ' '));
			}
			if (entry != null) {
				entry.CurrentPlugin = null;
				entry.CurrentProcedure = null;
			}
		} catch (AsyncRequestDisconnectedException) {
		} catch (AsyncRequestErrorException ex) {
			sender.Say("\u00034There was a problem looking up your account name: " + ex.Message);
		}
	}

	/// <summary>Returns the command that matches the specified label and target, if any; otherwise, returns null.</summary>
	public Trigger? GetTrigger(IrcMessageTarget target, string message, out Match? match) {
		var highlightMatch = Regex.Match(message, @"^" + Regex.Escape(target.Client.Me.Nickname) + @"\.*[:,-]? ", RegexOptions.IgnoreCase);
		if (highlightMatch.Success) message = message[highlightMatch.Length..];

		foreach (var trigger in this.Triggers) {
			if (trigger.Attribute.MustUseNickname && !highlightMatch.Success) continue;

			foreach (var regex in trigger.Attribute.Patterns) {
				match = regex.Match(message);
				if (match.Success) {
					// Check the scope.
					if ((trigger.Attribute.Scope & CommandScope.PM) == 0 && !(target is IrcChannel)) continue;
					if ((trigger.Attribute.Scope & CommandScope.Channel) == 0 && target is IrcChannel) continue;

					return trigger;
				}
			}
		}
		match = null;
		return null;
	}

	/// <summary>Processes a command line and runs any matching triggers that the user has access to.</summary>
	/// <param name="sender">The user sending the command.</param>
	/// <param name="target">The channel in which the command was sent, or the sender's nickname if it was in a private message.</param>
	/// <param name="trigger">The trigger that triggered.</param>
	/// <param name="match">The <see cref="Match"/> object that describes the match.</param>
	/// <returns>True if a command was matched (even if it was denied); false otherwise.</returns>
	public async Task RunTrigger(IrcUser sender, IrcMessageTarget target, Trigger trigger, Match match) {
		// Check for permissions.
		var permission = trigger.Attribute.Permission == null ? null
			: trigger.Attribute.Permission.StartsWith(".") ? this.Key + trigger.Attribute.Permission
			: trigger.Attribute.Permission;
		try {
			if (permission != null && !await this.Bot.CheckPermissionAsync(sender, permission)) {
				if (trigger.Attribute.NoPermissionsMessage != null) Bot.Say(sender.Client, sender.Nickname, trigger.Attribute.NoPermissionsMessage);
				return;
			}

			// Run the command.
			// TODO: Run it on a separate thread.
			var entry = this.Bot.GetClientEntry(sender.Client);
			try {
				if (entry != null) {
					entry.CurrentPlugin = this;
					entry.CurrentProcedure = trigger.Handler.GetMethodInfo();
				}
				trigger.Handler.Invoke(this, new TriggerEventArgs(sender.Client, target, sender, match));
			} catch (Exception ex) {
				Bot.LogError(this.Key, trigger.Handler.GetMethodInfo().Name, ex);
				while (ex is TargetInvocationException or AggregateException && ex.InnerException is not null) ex = ex.InnerException;
				Bot.Say(sender.Client, target.Target, "\u00034The command failed. This incident has been logged. ({0})", ex.Message);
			}
			if (entry != null) {
				entry.CurrentPlugin = null;
				entry.CurrentProcedure = null;
			}
		} catch (AsyncRequestDisconnectedException) {
		} catch (AsyncRequestErrorException ex) {
			sender.Say("\u00034There was a problem looking up your account name: " + ex.Message);
		}
	}

	/// <summary>
	/// Sends a message to all channels in which the bot is active. Channel names containing wildcards are excluded.
	/// </summary>
	/// <param name="message">The text to send.</param>
	/// <param name="options">A SayOptions value specifying how to send the message.</param>
	/// <param name="exclude">A list of channel names that should be excloded. May be null or empty to exclude nothing.</param>
	public void SayToAllChannels(string message, SayOptions options = 0, string[]? exclude = null)
		=> this.SayToAllChannels(message, options, exclude, false, null, null, null);
	/// <summary>
	/// Invokes GetMessage and sends the result to all channels in which the bot is active. Channel names containing wildcards are excluded.
	/// </summary>
	/// <param name="key">The key to retrieve.</param>
	/// <param name="nickname">The nickname of the user who sent the command that this message is response to, or null if not applicable.</param>
	/// <param name="channel">The channel that the command was given in, or is otherwise relevant to this message, or null if none is.</param>
	/// <param name="options">A SayOptions value specifying how to send the message.</param>
	/// <param name="exclude">A list of channel names that should be excloded. May be null or empty to exclude nothing.</param>
	/// <param name="args">Implementation-defined elements to be included in the formatted message.</param>
	public void SayLanguageToAllChannels(string key, string nickname, string channel, SayOptions options = 0, string[]? exclude = null, params object[]? args)
		=> this.SayToAllChannels(key, options, exclude, true, nickname, channel, args);

	private void SayToAllChannels(string message, SayOptions options, string[]? exclude, bool isLanguage, string? nickname, string? channel, params object[]? args) {
		if (message is null or "") return;
		if (this.Channels == null) return;

		if ((options & SayOptions.Capitalise) != 0) {
			char c = char.ToUpper(message[0]);
			if (c != message[0]) message = c + message[1..];
		}

		var privmsgTarget = new List<string>[this.Bot.Clients.Count];
		var noticeTarget = new List<string>[this.Bot.Clients.Count];

		foreach (string channel2 in this.Channels) {
			string? address;
			string channel3;

			string[] fields = channel2.Split(new char[] { '/' }, 2);
			if (fields.Length == 2) {
				address = fields[0];
				channel3 = fields[1];
			} else {
				address = null;
				channel3 = fields[0];
			}
			if (channel3 == "*") continue;

			bool notice = false;
			string target = channel3;

			for (int index = 0; index < this.Bot.Clients.Count; ++index) {
				if (address == null || address == "*" || address.Equals(this.Bot.Clients[index].Client.Address, StringComparison.OrdinalIgnoreCase) || address.Equals(this.Bot.Clients[index].Name, StringComparison.OrdinalIgnoreCase)) {
					if (this.Bot.Clients[index].Client.IsChannel(channel3)) {
						if ((address == null || address == "*") && !this.Bot.Clients[index].Client.Channels.Contains(channel3)) continue;
						if ((options & SayOptions.OpsOnly) != 0) {
							target = "@" + channel3;
							notice = true;
						}
					} else
						notice = true;

					if ((options & SayOptions.NoticeAlways) != 0)
						notice = true;
					if ((options & SayOptions.NoticeNever) != 0)
						notice = false;

					if (notice) {
						if (noticeTarget[index] == null) noticeTarget[index] = new List<string>();
					} else {
						if (privmsgTarget[index] == null) privmsgTarget[index] = new List<string>();
					}

					var selectedTarget = notice ? noticeTarget[index] : privmsgTarget[index];
					if (!selectedTarget.Contains(target)) selectedTarget.Add(target);
				}
			}

		}

		string key = message;
		for (int index = 0; index < this.Bot.Clients.Count; ++index) {
			if (isLanguage)
				message = this.GetMessage(key, nickname, channel, args) ?? key;

			if (privmsgTarget[index] != null)
				this.Bot.Clients[index].Client.Send("PRIVMSG", string.Join(",", privmsgTarget[index]), message);
			if (noticeTarget[index] != null)
				this.Bot.Clients[index].Client.Send("NOTICE", string.Join(",", noticeTarget[index]), message);
		}
	}

	/// <summary>
	/// Returns the formatted message corresponding to the given key in the Language list.
	/// </summary>
	/// <param name="key">The key to retrieve.</param>
	/// <param name="nickname">The nickname of the user who sent the command that this message is response to, or null if not applicable.</param>
	/// <param name="channel">The channel that the command was given in, or is otherwise relevant to this message, or null if none is.</param>
	/// <param name="args">Implementation-defined elements to be included in the formatted message.</param>
	/// <returns>The formatted message, or null if the key given is not in either Language list.</returns>
	public string? GetMessage(string key, string? nickname, string? channel, params object[]? args) {
		if (!this.language.TryGetValue(key, out var format))
			if (!this.defaultLanguage.TryGetValue(key, out format))
				return null;

		return string.Format(this.ProcessMessage(format, nickname, channel), args ?? Array.Empty<object>());
	}
	private string ProcessMessage(string format, string? nickname, string? channel) {
		int braceLevel;
		var builder = new StringBuilder(format.Length);

		for (int i = 0; i < format.Length; ++i) {
				if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{(}") {
					builder.Append('(');
					i += 2;
				} else if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{)}") {
					builder.Append(')');
					i += 2;
				} else if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{|}") {
					builder.Append('|');
					i += 2;
				} else if (i <= format.Length - 2 && format[i] == '(' && format[i + 1] == '(') {
					i += 2;
					int start = i;
					braceLevel = 1;
					var options = new List<Tuple<int, int>>();

					for (; i < format.Length; ++i) {
						if (i < format.Length - 1) {
							if (format[i] == '{' && i <= format.Length - 3) {
								if (format.Substring(i, 3) == "{(}") {
									builder.Append('(');
									i += 2;
								} else if (format.Substring(i, 3) == "{)}") {
									builder.Append(')');
									i += 2;
								} else if (format.Substring(i, 3) == "{|}") {
									builder.Append('|');
									i += 2;
								}
							} else if (braceLevel == 1 && format[i] == '|' && format[i + 1] == '|') {
								options.Add(new Tuple<int, int>(start, i));
								i += 1;
								start = i + 1;
							} else if (format[i] == ')' && format[i + 1] == ')') {
								--braceLevel;
								if (braceLevel == 0) {
									options.Add(new Tuple<int, int>(start, i));
									i += 1;
									break;
								}
								i += 1;
							} else if (format[i] == '(' && format[i + 1] == '(') {
								++braceLevel;
								i += 1;
							}
						} else {
							throw new FormatException("The format string contains unclosed braces.");
						}
					}

					// Pick an option at random.
					var choice = options[this.random.Next(options.Count)];
					builder.Append(this.ProcessMessage(format[choice.Item1..choice.Item2], nickname, channel));
				} else if (i <= format.Length - 6 && format[i] == '{' && format.Substring(i, 6) == "{nick}") {
					builder.Append(nickname);
					i += 5;
				} else if (i <= format.Length - 6 && format[i] == '{' && format.Substring(i, 6) == "{chan}") {
					builder.Append(channel);
					i += 5;
				} else {
					builder.Append(format[i]);
				}
		}

		return builder.ToString();
	}

	internal void LoadLanguage() {
		string path = Path.Combine(this.Bot.LanguagesPath, this.Bot.Language, this.Key + ".properties");
		if (File.Exists(path)) {
			this.LoadLanguage(path);
		} else {
			path = Path.Combine(this.Bot.LanguagesPath, "Default", this.Key + ".properties");
			if (File.Exists(path)) {
				this.LoadLanguage(path);
			}
		}
	}
	internal void LoadLanguage(string filePath) {
		using var reader = new StreamReader(File.Open(filePath, FileMode.Open, FileAccess.Read));
		this.language.Clear();
		while (true) {
			var s = reader.ReadLine();
			if (s == null) break;
			s = s.TrimStart();
			if (s == "" || s[0] is '#' or '!') continue;  // Ignore blank lines and comments.

			string[] fields = s.Split(new char[] { '=' }, 2);
			if (fields.Length == 2) {
				var formatBuilder = new StringBuilder(fields[1].Length);
				int pos = 0; Match m; bool nextLine;
				do {
					nextLine = false;
					while ((m = languageEscapeRegex.Match(fields[1], pos)).Success) {
						formatBuilder.Append(fields[1][pos..m.Index]);
						if (m.Groups[1].Success) {
							formatBuilder.Append('\n');
						} else if (m.Groups[2].Success) {
							formatBuilder.Append('\r');
						} else if (m.Groups[3].Success) {
							formatBuilder.Append('\t');
						} else if (m.Groups[4].Success) {
							formatBuilder.Append('\\');
						} else if (m.Groups[5].Success) {
							// Unicode escape.
							if (m.Groups[6].Success) {
								formatBuilder.Append((char) Convert.ToInt32(m.Groups[6].Value, 16));
							} else {
								throw new FormatException("Invalid unicode (\\u) escape sequence at '" + fields[0] + "' in " + filePath + ".");
							}
						} else if (m.Groups[7].Success) {
							// Escaped newline; read another line and append that.
							s = reader.ReadLine();
							if (s == null) throw new FormatException("Backslash with nothing after it at '" + fields[0] + "' in " + filePath + ".");
							fields[1] = s.TrimStart();
							nextLine = true;
						}
						pos += m.Length;
					}
				} while (nextLine);
				formatBuilder.Append(fields[1][pos..]);

				this.language.Add(fields[0], formatBuilder.ToString());
			}
		}
	}

	/// <summary>
	/// When overridden, runs after all plugins are instantiated by CBot during startup, or after this plugin is instantiated at run time.
	/// </summary>
	public virtual void Initialize() { }

	/// <summary>
	/// When overridden, saves any configuration and other data needed by this plugin.
	/// </summary>
	/// <remarks>
	/// This method is intended to be used to provide a standard means to tell plugins to save data.
	/// A plugin might call this method in all loaded plugins on command from a user.
	/// </remarks>
	public virtual void OnSave() { }

	/// <summary>
	/// When overridden, runs just before this plugin is removed. The default implementation simply calls OnSave.
	/// </summary>
	/// <remarks>
	/// This method is intended to be used to provide a standard means to tell plugins to clean up.
	/// Failing to do so may cause unintended continued interaction.
	/// </remarks>
	public virtual void OnUnload() => this.OnSave();

	/// <summary>
	/// Reports an exception to the user, and logs it.
	/// </summary>
	/// <param name="Procedure">The human-readable name of the procedure that had the problem.</param>
	/// <param name="ex">The exception that was thrown.</param>
	protected void LogError(string Procedure, Exception ex) => Bot.LogError(this.Key, Procedure, ex);

	public virtual void OnHttpRequest(HttpRequestEventArgs e) { }

	/// <summary>When overridden, handles the AwayCancelled event. Return true to stop further processing of the event.</summary>
	public virtual bool OnAwayCancelled(object? sender, EventArgs e) => false;
	/// <summary>When overridden, handles the AwayMessage event. Return true to stop further processing of the event.</summary>
	public virtual bool OnAwayMessage(object? sender, AwayMessageEventArgs e) => false;
	/// <summary>When overridden, handles the AwaySet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnAwaySet(object? sender, EventArgs e) => false;
	/// <summary>When overridden, handles the BroadcastMessage event. Return true to stop further processing of the event.</summary>
	public virtual bool OnBroadcastMessage(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the BroadcastNotice event. Return true to stop further processing of the event.</summary>
	public virtual bool OnBroadcastNotice(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the BroadcastTagMessage event. Return true to stop further processing of the event.</summary>
	public virtual bool OnBroadcastTagMessage(object? sender, PrivateTagMessageEventArgs e) => false;
	/// <summary>When overridden, handles the CapabilitiesAdded event. Return true to stop further processing of the event.</summary>
	public virtual bool OnCapabilitiesAdded(object? sender, CapabilitiesAddedEventArgs e) => false;
	/// <summary>When overridden, handles the CapabilitiesDeleted event. Return true to stop further processing of the event.</summary>
	public virtual bool OnCapabilitiesDeleted(object? sender, CapabilitiesEventArgs e) => false;
	/// <summary>Handles the ChannelAction event, including running triggers. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelAction(object? sender, ChannelMessageEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelBan event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelBan(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelBanList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelBanList(object? sender, ChannelModeListEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelBanListEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelBanListEnd(object? sender, ChannelModeListEndEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelBanRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelBanRemoved(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelCTCP event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelCTCP(object? sender, ChannelMessageEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelExempt event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelExempt(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelExemptRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelExemptRemoved(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelInviteExempt event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelInviteExempt(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelInviteExemptList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelInviteExemptList(object? sender, ChannelModeListEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelInviteExemptListEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelInviteExemptListEnd(object? sender, ChannelModeListEndEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelInviteExemptRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelInviteExemptRemoved(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelJoin event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelJoin(object? sender, ChannelJoinEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelJoinDenied event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelJoinDenied(object? sender, ChannelJoinDeniedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelKeyRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelKeyRemoved(object? sender, ChannelChangeEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelKeySet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelKeySet(object? sender, ChannelKeyEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelKick event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelKick(object? sender, ChannelKickEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelLeave event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelLeave(object? sender, ChannelPartEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelLimitRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelLimitRemoved(object? sender, ChannelChangeEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelLimitSet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelLimitSet(object? sender, ChannelLimitEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelList(object? sender, ChannelListEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelListChanged event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelListChanged(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelListEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelListEnd(object? sender, ChannelListEndEventArgs e) => false;
	/// <summary>Handles the ChannelMessage event, including running commands and triggers. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelMessage(object? sender, ChannelMessageEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelMessageDenied event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelMessageDenied(object? sender, ChannelJoinDeniedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelModesGet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelModesGet(object? sender, ChannelModesSetEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelModesSet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelModesSet(object? sender, ChannelModesSetEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelNotice event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelNotice(object? sender, ChannelMessageEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelPart event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelPart(object? sender, ChannelPartEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelQuiet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelQuiet(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelQuietRemoved event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelQuietRemoved(object? sender, ChannelListChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelStatusChanged event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelStatusChanged(object? sender, ChannelStatusChangedEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelTagMessage event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelTagMessage(object? sender, ChannelTagMessageEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelTimestamp event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelTimestamp(object? sender, ChannelTimestampEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelTopicChanged event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelTopicChanged(object? sender, ChannelTopicChangeEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelTopicReceived event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelTopicReceived(object? sender, ChannelTopicEventArgs e) => false;
	/// <summary>When overridden, handles the ChannelTopicStamp event. Return true to stop further processing of the event.</summary>
	public virtual bool OnChannelTopicStamp(object? sender, ChannelTopicStampEventArgs e) => false;
	/// <summary>When overridden, handles the Disconnected event. Return true to stop further processing of the event.</summary>
	public virtual bool OnDisconnected(object? sender, DisconnectEventArgs e) => false;
	/// <summary>When overridden, handles the Exception event. Return true to stop further processing of the event.</summary>
	public virtual bool OnException(object? sender, ExceptionEventArgs e) => false;
	/// <summary>When overridden, handles the ExemptList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnExemptList(object? sender, ChannelModeListEventArgs e) => false;
	/// <summary>When overridden, handles the ExemptListEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnExemptListEnd(object? sender, ChannelModeListEndEventArgs e) => false;
	/// <summary>When overridden, handles the Invite event. Return true to stop further processing of the event.</summary>
	public virtual bool OnInvite(object? sender, InviteEventArgs e) => false;
	/// <summary>When overridden, handles the InviteSent event. Return true to stop further processing of the event.</summary>
	public virtual bool OnInviteSent(object? sender, InviteSentEventArgs e) => false;
	/// <summary>When overridden, handles the Killed event. Return true to stop further processing of the event.</summary>
	public virtual bool OnKilled(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the MonitorOffline event. Return true to stop further processing of the event.</summary>
	public virtual bool OnMonitorOffline(object? sender, IrcUserLineEventArgs e) => false;
	/// <summary>When overridden, handles the MonitorOnline event. Return true to stop further processing of the event.</summary>
	public virtual bool OnMonitorOnline(object? sender, IrcUserLineEventArgs e) => false;
	/// <summary>When overridden, handles the MOTD event. Return true to stop further processing of the event.</summary>
	public virtual bool OnMOTD(object? sender, MotdEventArgs e) => false;
	/// <summary>When overridden, handles the Names event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNames(object? sender, ChannelNamesEventArgs e) => false;
	/// <summary>When overridden, handles the NamesEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNamesEnd(object? sender, ChannelModeListEndEventArgs e) => false;
	/// <summary>When overridden, handles the NicknameChange event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNicknameChange(object? sender, NicknameChangeEventArgs e) => false;
	/// <summary>When overridden, handles the NicknameChangeFailed event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNicknameChangeFailed(object? sender, NicknameEventArgs e) => false;
	/// <summary>When overridden, handles the NicknameInvalid event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNicknameInvalid(object? sender, NicknameEventArgs e) => false;
	/// <summary>When overridden, handles the NicknameTaken event. Return true to stop further processing of the event.</summary>
	public virtual bool OnNicknameTaken(object? sender, NicknameEventArgs e) => false;
	/// <summary>When overridden, handles the PingReceived event. Return true to stop further processing of the event.</summary>
	public virtual bool OnPingReceived(object? sender, PingEventArgs e) => false;
	/// <summary>When overridden, handles the Pong event. Return true to stop further processing of the event.</summary>
	public virtual bool OnPong(object? sender, PingEventArgs e) => false;
	/// <summary>Handles the PrivateAction event, including running triggers. Return true to stop further processing of the event.</summary>
	public virtual bool OnPrivateAction(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the PrivateCTCP event. Return true to stop further processing of the event.</summary>
	public virtual bool OnPrivateCTCP(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>Handles the PrivateMessage event, including running commands and triggers. Return true to stop further processing of the event.</summary>
	public virtual bool OnPrivateMessage(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the PrivateNotice event. Return true to stop further processing of the event.</summary>
	public virtual bool OnPrivateNotice(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the PrivateTagMessage event. Return true to stop further processing of the event.</summary>
	public virtual bool OnPrivateTagMessage(object? sender, PrivateTagMessageEventArgs e) => false;
	/// <summary>When overridden, handles the RawLineReceived event. Return true to stop further processing of the event.</summary>
	public virtual bool OnRawLineReceived(object? sender, IrcLineEventArgs e) => false;
	/// <summary>When overridden, handles the RawLineSent event. Return true to stop further processing of the event.</summary>
	public virtual bool OnRawLineSent(object? sender, RawLineEventArgs e) => false;
	/// <summary>When overridden, handles the RawLineUnhandled event. Return true to stop further processing of the event.</summary>
	public virtual bool OnRawLineUnhandled(object? sender, IrcLineEventArgs e) => false;
	/// <summary>When overridden, handles the Registered event. Return true to stop further processing of the event.</summary>
	public virtual bool OnRegistered(object? sender, RegisteredEventArgs e) => false;
	/// <summary>When overridden, handles the SelectCertificate event. Return true to stop further processing of the event.</summary>
	public virtual bool OnSelectCertificate(object? sender, SelectCertificateEventArgs e) => false;
	/// <summary>When overridden, handles the ServerError event. Return true to stop further processing of the event.</summary>
	public virtual bool OnServerError(object? sender, ServerErrorEventArgs e) => false;
	/// <summary>When overridden, handles the StateChanged event. Return true to stop further processing of the event.</summary>
	public virtual bool OnStateChanged(object? sender, StateEventArgs e) => false;
	/// <summary>When overridden, handles the StsPolicyChanged event. Return true to stop further processing of the event.</summary>
	public virtual bool OnStsPolicyChanged(object? sender, EventArgs e) => false;
	/// <summary>When overridden, handles the StsUpgrade event. Return true to stop further processing of the event.</summary>
	public virtual bool OnStsUpgrade(object? sender, StsUpgradeEventArgs e) => false;
	/// <summary>When overridden, handles the UserAppeared event. Return true to stop further processing of the event.</summary>
	public virtual bool OnUserAppeared(object? sender, IrcUserEventArgs e) => false;
	/// <summary>When overridden, handles the UserDisappeared event. Return true to stop further processing of the event.</summary>
	public virtual bool OnUserDisappeared(object? sender, IrcUserEventArgs e) => false;
	/// <summary>When overridden, handles the UserModesGet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnUserModesGet(object? sender, UserModesEventArgs e) => false;
	/// <summary>When overridden, handles the UserModesSet event. Return true to stop further processing of the event.</summary>
	public virtual bool OnUserModesSet(object? sender, UserModesEventArgs e) => false;
	/// <summary>When overridden, handles the UserQuit event. Return true to stop further processing of the event.</summary>
	public virtual bool OnUserQuit(object? sender, QuitEventArgs e) => false;
	/// <summary>When overridden, handles the ValidateCertificate event. Return true to stop further processing of the event.</summary>
	public virtual bool OnValidateCertificate(object? sender, ValidateCertificateEventArgs e) => false;
	/// <summary>When overridden, handles the Wallops event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWallops(object? sender, PrivateMessageEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsAuthenticationLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsAuthenticationLine(object? sender, WhoisAuthenticationEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsChannelLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsChannelLine(object? sender, WhoisChannelsEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsEnd(object? sender, WhoisEndEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsHelperLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsHelperLine(object? sender, WhoisOperEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsIdleLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsIdleLine(object? sender, WhoisIdleEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsNameLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsNameLine(object? sender, WhoisNameEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsOperLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsOperLine(object? sender, WhoisOperEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsRealHostLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsRealHostLine(object? sender, WhoisRealHostEventArgs e) => false;
	/// <summary>When overridden, handles the WhoIsServerLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoIsServerLine(object? sender, WhoisServerEventArgs e) => false;
	/// <summary>When overridden, handles the WhoList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoList(object? sender, WhoListEventArgs e) => false;
	/// <summary>When overridden, handles the WhoWasEnd event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoWasEnd(object? sender, WhoisEndEventArgs e) => false;
	/// <summary>When overridden, handles the WhoWasNameLine event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoWasNameLine(object? sender, WhoisNameEventArgs e) => false;
	/// <summary>When overridden, handles the WhoxList event. Return true to stop further processing of the event.</summary>
	public virtual bool OnWhoxList(object? sender, WhoxListEventArgs e) => false;
}

public abstract class ProtocolIntegrationPlugin : Plugin {
	public virtual Type ClientEntryType => typeof(ClientEntry);
	public abstract IrcClient? GetIrcClient(IrcLocalUser localUser, string networkName, ClientEntry clientEntry);
}

public class Command {
	public CommandAttribute Attribute { get; }
	public PluginCommandHandler Handler { get; }

	public Command(CommandAttribute attribute, PluginCommandHandler handler) {
		this.Attribute = attribute;
		this.Handler = handler;
	}
}

public class Trigger {
	public TriggerAttribute Attribute { get; }
	public PluginTriggerHandler Handler { get; }

	public Trigger(TriggerAttribute attribute, PluginTriggerHandler handler) {
		this.Attribute = attribute;
		this.Handler = handler;
	}
}
