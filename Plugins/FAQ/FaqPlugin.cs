﻿using System.Text;
using System.Text.RegularExpressions;
using AnIRC;
using CBot;

namespace FAQ;
[ApiVersion(4, 0)]
public class FaqPlugin : Plugin {
	public List<string> NoShortcutChannels;
	public string LabelFormat = "{4}";
	public SortedDictionary<string, Factoid> Factoids;
	public SortedDictionary<string, string> Aliases;
	public SortedDictionary<string, string[]> Contexts;
	public Dictionary<string, string> Targets;

	public override string Name => "FAQ";

	public FaqPlugin(string Key) {
		this.NoShortcutChannels = new List<string>();
		this.Factoids = new SortedDictionary<string, Factoid>(StringComparer.InvariantCultureIgnoreCase);
		this.Contexts = new SortedDictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase);
		this.Aliases = new SortedDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		this.Targets = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
		this.LoadConfig(Key);
		this.LoadData(Key == "FAQ" ? "FAQ.ini" : "FAQ-" + Key + ".ini");
	}

	public override void OnSave() {
		this.SaveConfig();
		this.SaveData(this.Key == "FAQ" ? "FAQ.ini" : "FAQ-" + this.Key + ".ini");
	}

	public void LoadConfig(string key) {
		string filename = Path.Combine("Config", key + ".ini");
		if (!File.Exists(filename)) return;
		using var reader = new StreamReader(filename);
		string? section = null;

		while (true) {
			var line = reader.ReadLine();
			if (line == null) break;
			if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

			var match = Regex.Match(line, @"^\s*\[(.*?)\]?\s*$");
			if (match.Success) {
				section = match.Groups[1].Value;
			} else {
				match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
				if (match.Success) {
					string field = match.Groups[1].Value;
					string value = match.Groups[2].Value;

					if (section == null) continue;
					switch (section.ToUpper()) {
						case "CONFIG":
							switch (field.ToUpper()) {
								case "LABELFORMAT":
									this.LabelFormat = value;
									break;
								case "NOSHORTCUT":
									this.NoShortcutChannels = new List<string>(value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
									break;
							}
							break;
					}
				}
			}
		}
		reader.Close();
	}

	public void SaveConfig() {
		if (!Directory.Exists("Config"))
			Directory.CreateDirectory("Config");
		using var writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false);
		writer.WriteLine("[Config]");
		writer.WriteLine("LabelFormat={0}", string.Join(",", this.LabelFormat));
		writer.WriteLine("NoShortcut={0}", string.Join(",", this.NoShortcutChannels));
		writer.Close();
	}

	public void LoadData(string filename) {
		if (!File.Exists(filename)) return;
		using var reader = new StreamReader(filename);
		string? section = null; short sectionType = -1;
		Factoid? factoid = null;
		int lineNumber = 0;

		while (true) {
			++lineNumber;
			var line = reader.ReadLine();
			if (line == null) break;
			if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

			var match = Regex.Match(line, @"^\s*\[(.*?)\]?\s*$");
			if (match.Success) {
				section = match.Groups[1].Value;
				if (section.Equals("contexts", StringComparison.InvariantCultureIgnoreCase)) {
					sectionType = 1;
				} else if (section.Equals("aliases", StringComparison.InvariantCultureIgnoreCase)) {
					sectionType = 2;
				} else {
					sectionType = 0;
					if (this.Factoids.TryGetValue(section, out factoid))
						 ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): duplicate factoid key '{2}'.", this.Key, lineNumber, section);
					else {
						if (this.Aliases.ContainsKey(section))
							ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the factoid key '{2}' hides an alias.", this.Key, lineNumber, section);
						factoid = new Factoid();
						this.Factoids.Add(section, factoid);
					}
				}
			} else {
				match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
				if (match.Success) {
					string field = match.Groups[1].Value;
					string value = match.Groups[2].Value;

					if (section == null) continue;
					if (sectionType == 1) {
						if (this.Contexts.ContainsKey(field))
							ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): duplicate context key '{2}'.", this.Key, lineNumber, field);
						else
							this.Contexts.Add(field, value.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
					} else if (sectionType == 2) {
						if (this.Aliases.ContainsKey(field))
							ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): duplicate alias key '{2}'.", this.Key, lineNumber, field);
						else if (this.Factoids.ContainsKey(field))
							ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the alias key '{2}' conflicts with a factoid.", this.Key, lineNumber, field);
						else
							this.Aliases.Add(field, value);
					} else if (sectionType == 0 && factoid != null) {
						int value2; bool value3;
						switch (field.ToUpper()) {
							case "DATA":
								if (factoid.Data == null) factoid.Data = value;
								else factoid.Data += "\r\n" + value;
								break;
							case "REGEX":
								factoid.Expressions.Add(value);
								break;
							case "HIDDEN":
								if (Bot.TryParseBoolean(value, out value3)) factoid.Hidden = value3;
								else ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
								break;
							case "HIDELABEL":
								if (Bot.TryParseBoolean(value, out value3)) factoid.HideLabel = value3;
								else ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
								break;
							case "NOTICEONJOIN":
								if (Bot.TryParseBoolean(value, out value3)) factoid.NoticeOnJoin = value3;
								else ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
								break;
							case "RATELIMITCOUNT":
								if (int.TryParse(value, out value2) && value2 >= 0) factoid.RateLimitCount = value2;
								else ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
								break;
							case "RATELIMITTIME":
							case "RATELIMITINTERVAL":
								if (int.TryParse(value, out value2) && value2 >= 0) factoid.RateLimitTime = value2;
								else ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
								break;
							default:
								if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): the field name is unknown.", this.Key, lineNumber);
								break;
						}
					} else if (sectionType == -1) {
						ConsoleUtils.WriteLine("[{0}] Problem loading the FAQ data (line {1}): found a stray field.", this.Key, lineNumber);
						sectionType = -2;
					}
				}
			}
		}
		reader.Close();
	}

	public void SaveData(string filename) {
		if (!Directory.Exists("Config"))
			Directory.CreateDirectory("Config");
		using var writer = new StreamWriter(filename, false);

		// Header
		writer.WriteLine("; This file contains all of the FAQ data.");
		writer.WriteLine("; Feel free to edit it using your text editor of choice.");
		writer.WriteLine("; If you do this while the bot is running, use the command !faqload to reload the data.");
		writer.WriteLine();

		// Contexts
		writer.WriteLine("[Contexts]");
		foreach (var context in this.Contexts)
			writer.WriteLine("{0}={1}", context.Key, string.Join(",", context.Value));
		writer.WriteLine();

		// Factoids
		foreach (var factoid in this.Factoids) {
			writer.WriteLine("[{0}]", factoid.Key);
			foreach (string line in factoid.Value.Data.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
				writer.WriteLine("Data={0}", line);
			foreach (string regex in factoid.Value.Expressions)
				writer.WriteLine("Regex={0}", regex);
			if (factoid.Value.Hidden) writer.WriteLine("Hidden=Yes");
			if (factoid.Value.HideLabel) writer.WriteLine("HideLabel=Yes");
			if (!factoid.Value.NoticeOnJoin) writer.WriteLine("NoticeOnJoin=No");
			if (factoid.Value.RateLimitCount != 1) writer.WriteLine("RateLimitCount={0}", factoid.Value.RateLimitCount);
			if (factoid.Value.RateLimitTime != 120) writer.WriteLine("RateLimitTime={0}", factoid.Value.RateLimitTime);
			writer.WriteLine();
		}

		// Aliases
		if (this.Aliases.Count > 0) {
			writer.WriteLine("[Aliases]");
			foreach (var alias in this.Aliases)
				writer.WriteLine("{0}={1}", alias.Key, alias.Value);
		}

		writer.Close();
	}

	public bool ShortcutCheck(IrcMessageTarget target) {
		foreach (string _channel in this.NoShortcutChannels) {
			string[] fields2 = _channel.Split(new char[] { '/' }, 2);
			if (fields2.Length == 1)
				fields2 = new string[] { "*", fields2[0] };

			if (fields2[0] == "*" || fields2[0].Equals(target.Client.NetworkName, StringComparison.OrdinalIgnoreCase) || fields2[0].Equals(target.Client.Address, StringComparison.OrdinalIgnoreCase)) {
				if (fields2[1] == "*" || ((fields2[1] == "#*" || fields2[1] == "*#") && target is IrcChannel)) {
					return false;
				} else if (target.Client.CaseMappingComparer.Equals(fields2[1], target.Target)) {
					return false;
				}
			}
		}
		return true;
	}

	public async void ExpressionCheckAsync(IrcClient connection, string? channel, IrcUser user, string action, string? parameter) {
		string userKey = connection.Extensions.NetworkName + "/" + user.Nickname;

		foreach (var factoid in this.Factoids) {
			if (factoid.Value.Expressions == null) continue;
			Match? match = null;
			bool specificChannel = false;  // If this is set to true, we'll show the factoid context name.

			// Check the rate limit.
			if (factoid.Value.RateLimitCount > 0 && factoid.Value.RateLimitTime > 0) {
				if (factoid.Value.HitTimes.TryGetValue(userKey, out var hitTimes) && hitTimes.Count >= factoid.Value.RateLimitCount) {
					if (hitTimes.Peek() > DateTime.Now.AddSeconds(-factoid.Value.RateLimitTime))
						continue;
				}
			}

			string[] fields = this.GetContextAndKey(factoid.Key);

			// Check that the context includes this channel.
			string[] channels;
			if (fields[0] == null) channels = new string[] { "*" };
			else channels = this.Contexts[fields[0]];

			bool found = false;
			foreach (string _channel in channels) {
				string[] fields2 = _channel.Split(new char[] { '/' }, 2);
				if (fields2.Length == 1)
					fields2 = new string[] { "*", fields2[0] };

				if (fields2[0] == "*" || fields2[0].Equals(connection.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields2[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
					if (fields2[1] == "*" || ((fields2[1] == "#*" || fields2[1] == "*#") && channel != null && connection.IsChannel(channel))) {
						found = true;
					} else if (connection.CaseMappingComparer.Equals(fields2[1], channel)) {
						found = true;
						specificChannel = true;
						break;
					}
				}
			}
			if (!found) continue;

			// Check that a regular expression matches.
			foreach (string expression in factoid.Value.Expressions) {
				if (expression == null || expression == "") continue;

				string matchAction; string? mask = null; string? matchChannel = null; string? permission = null; string? regex = null;
				string[] fields3 = expression.Split(new char[] { ':' }, 5);

				if (fields3[0].Equals("MSG", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("ACTION", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("JOIN", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("PART", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("KICK", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("QUIT", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("LEAVE", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("NICK", StringComparison.InvariantCultureIgnoreCase) ||
					fields3[0].Equals("INVITE", StringComparison.InvariantCultureIgnoreCase)) {
					matchAction = fields3[0];
					if (!matchAction.Equals(action, StringComparison.InvariantCultureIgnoreCase))
						continue;

					int i;
					for (i = 1; i < fields3.Length - 1; ++i) {
						if (fields3[i] == "") {
							++i;
							break;
						}
						if (fields3[i].Contains("!")) {
							mask = fields3[i];
							if (!Bot.MaskCheck(user.ToString(), mask)) {
								i = int.MaxValue;
								break;
							}
						} else if (fields3[i].Contains("#")) {
							matchChannel = fields3[i];
							string[] fields2 = matchChannel.Split(new char[] { '/' }, 2);
							if (fields2.Length == 1)
								fields2 = new string[] { "*", fields2[0] };
							if ((fields2[0] != "*" && !fields2[0].Equals(connection.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) && !fields3[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) ||
								(fields2[1] != "*" && ((fields2[1] != "#*" && fields2[1] != "*#") || channel == null || !connection.IsChannel(channel)) &&
								 !connection.CaseMappingComparer.Equals(fields2[1], channel))) {
								i = int.MaxValue;
								break;
							}
						} else if (fields3[i] != "*") {
							permission = fields3[i];
							if (!await this.Bot.CheckPermissionAsync(user, permission)) {
								i = int.MaxValue;
								break;
							}
						}
					}
					if (i == int.MaxValue) continue;
					regex = string.Join(":", fields3.Skip(i));
				} else {
					if (action != "MSG") continue;
					regex = expression;
				}
				if (parameter != null) {
					var m = Regex.Match(parameter, regex, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
					if (m.Success) {
						match = m;
						break;
					}
				} else
					match = Regex.Match("", "");
			}

			// Display the factoid.
			if (match != null) {
				if (factoid.Value.HitTimes.TryGetValue(userKey, out var hitTimes)) {
					if (hitTimes.Count >= factoid.Value.RateLimitCount)
						hitTimes.Dequeue();
					hitTimes.Enqueue(DateTime.Now);
				} else {
					hitTimes = new Queue<DateTime>(factoid.Value.RateLimitCount);
					hitTimes.Enqueue(DateTime.Now);
					factoid.Value.HitTimes.Add(userKey, hitTimes);
				}

				string target = user.Nickname;
				if (match.Groups["target"].Success) {
					target = match.Groups["target"].Value;
				}

				this.DisplayFactoid(connection, user.Nickname, factoid.Value.NoticeOnJoin && (action == "JOIN" || action == "INVITE") ? user.Nickname : channel, target, fields[0], fields[1], factoid.Value.Data, factoid.Value.NoticeOnJoin && action == "JOIN", !factoid.Value.HideLabel, !specificChannel);
			}
		}
	}

	public void DisplayFactoid(IrcClient connection, string sender, string? channel, string nickname, string context, string key, string text, bool notice, bool showKey, bool showContext) {
		foreach (string line in text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)) {
			var messageBuilder = new StringBuilder();

			// Parse substitution codes.
			int pos = 0; int pos2;
			while (pos < line.Length) {
				pos2 = line.IndexOf('$', pos);
				if (pos2 < 0) {
					messageBuilder.Append(line.Substring(pos));
					break;
				}
				messageBuilder.Append(line.Substring(pos, pos2 - pos));
				if (pos < line.Length - 8 && line.Substring(pos2 + 1, 8).Equals("nickname", StringComparison.InvariantCultureIgnoreCase)) {
					messageBuilder.Append(nickname);
					pos = pos2 + 9;
				} else if (pos < line.Length - 7 && line.Substring(pos2 + 1, 7).Equals("channel", StringComparison.InvariantCultureIgnoreCase)) {
					messageBuilder.Append(channel);
					pos = pos2 + 8;
				} else if (pos < line.Length - 2 && line.Substring(pos2 + 1, 2).Equals("me", StringComparison.InvariantCultureIgnoreCase)) {
					messageBuilder.Append(connection.Me.Nickname);
					pos = pos2 + 3;
				} else if (pos < line.Length - 6 && line.Substring(pos2 + 1, 6).Equals("sender", StringComparison.InvariantCultureIgnoreCase)) {
					messageBuilder.Append(sender);
					pos = pos2 + 7;
				} else if (pos < line.Length - 1 && line[pos2 + 1] == '$') {
					messageBuilder.Append('$');
					pos = pos2 + 2;
				} else {
					messageBuilder.Append('$');
					++pos;
				}
			}

			if (showKey) {
				if (showContext) context += "/";
				else context = "";

				connection.Send(string.Format("{0} {1} :" + this.LabelFormat, notice ? "NOTICE" : "PRIVMSG", channel, context, key, messageBuilder.ToString()));
			} else
				connection.Send(notice ? "NOTICE" : "PRIVMSG", channel, messageBuilder.ToString());

			System.Threading.Thread.Sleep(600);
		}
	}

	public override bool OnChannelMessage(object? sender, ChannelMessageEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "MSG", e.Message);
		return false;
	}

	public override bool OnChannelAction(object? sender, ChannelMessageEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "ACTION", e.Message);
		return false;
	}

	public override bool OnChannelJoin(object? sender, ChannelJoinEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "JOIN", null);
		return false;
	}

	public override bool OnChannelPart(object? sender, ChannelPartEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "PART", e.Message);
		return false;
	}

	public override bool OnChannelKick(object? sender, ChannelKickEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "KICK", e.Target + ":" + e.Reason);
		return false;
	}

	public override bool OnUserQuit(object? sender, QuitEventArgs e) {
		this.ExpressionCheckAsync((IrcClient) sender!, null, e.Sender, "QUIT", e.Message);
		return false;
	}

	public override bool OnNicknameChange(object? sender, NicknameChangeEventArgs e) {
		this.ExpressionCheckAsync((IrcClient) sender!, null, e.Sender, "NICK", e.NewNickname);
		return false;
	}

	public override bool OnChannelLeave(object? sender, ChannelPartEventArgs e) {
		if (!this.IsActiveChannel(e.Channel)) return false;
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel.Name, e.Sender, "LEAVE", e.Message);
		return false;
	}

	public override bool OnInvite(object? sender, InviteEventArgs e) {
		this.ExpressionCheckAsync((IrcClient) sender!, e.Channel, e.Sender, "INVITE", null);
		return false;
	}

	public Factoid FindFactoid(IrcMessageTarget channel, IrcUser sender, ref string request) {
		string userKey;
		bool dotTarget = false;
		var results = new List<string>();

		if (sender is object && request == ".") {
			dotTarget = true;
			userKey = channel.Client.Extensions.NetworkName + "/" + sender.Nickname;
			if (!this.Targets.TryGetValue(userKey, out request!))
				throw new BrokenAliasException("The dot target has not yet been set for this user.");
		}

		if (this.Factoids.TryGetValue(request, out var result))
			return result;
		if (this.Aliases.TryGetValue(request, out var target)) {
			request = target;
			if (this.Factoids.TryGetValue(request, out result))
				return result;
			throw new BrokenAliasException();
		}

		foreach (var context in this.Contexts) {
			if (!context.Value.Contains(channel.Client.Extensions.NetworkName + "/" + channel.Target, StringComparer.InvariantCultureIgnoreCase) &&
				!context.Value.Contains("*/" + channel.Target, StringComparer.InvariantCultureIgnoreCase))
				continue;

			string fullKey = context.Key + "/" + request;
			if (this.Factoids.ContainsKey(fullKey)) {
				results.Add(fullKey);
			} else if (this.Aliases.TryGetValue(fullKey, out fullKey!)) {
				results.Add(fullKey);
			}
		}

		if (results.Count > 1)
			throw new MultipleContextException(results.ToArray());
		else if (results.Count == 0)
			throw new KeyNotFoundException();

		if ((object) sender! != null && !dotTarget) {
			userKey = channel.Client.Extensions.NetworkName + "/" + sender.Nickname;
			this.Targets[userKey] = results[0];
		}
		request = results[0];
		return this.Factoids[results[0]];
	}

	public string FindContext(IrcMessageTarget channel) {
		var results = new List<string>();
		foreach (var context in this.Contexts) {
			if (context.Value.Contains(channel.Client.Extensions.NetworkName + "/" + channel.Target) ||
				context.Value.Contains("*/" + channel.Target))
				results.Add(context.Key);
		}
		if (results.Count > 1)
			throw new MultipleContextException(results.ToArray());
		else if (results.Count == 0)
			throw new KeyNotFoundException();
		return results[0];
	}

	public string[] GetContextAndKey(string key) {
		if (key == null) throw new ArgumentNullException("key");
		string result;
		int pos = key.Length - 1;
		while (pos > 0) {
			pos = key.LastIndexOf('/', pos);
			if (pos == -1) break;
			result = key.Substring(0, pos);
			if (this.Contexts.ContainsKey(result))
				return new string[] { result, key.Substring(pos + 1) };
			--pos;
		}
		return new string[] { null, key };
	}

#region Commands
	[Trigger(@"^\?\s+(\S+)(?:\s+([^\s@]+(?:(?=\s*(@@?)))?))?")]
	public void RegexFactoid(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		if (e.Match.Groups[2].Success) {
			if (e.Match.Groups[3].Success) {
				this.CommandFactoid(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value, e.Match.Groups[3].Value
				}));
			} else {
				this.CommandFactoid(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value
				}));
			}
		} else {
			this.CommandFactoid(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value
				}));
		}
	}
	[Command("faq", 1, 3, "faq <key> [target][@|@@]", "Displays the named factoid.")]
	public void CommandFactoid(object? sender, CommandEventArgs e) {
		string key; string target; bool PM;

		key = e.Parameters[0];
		if (e.Parameters.Length == 3) {
			target = e.Parameters[1];
			PM = (e.Parameters[2] == "@@");
		} else if (e.Parameters.Length == 2) {
			target = e.Parameters[1];
			if (target.EndsWith("@@")) {
				target = target.Substring(0, target.Length - 2);
				PM = true;
			} else if (target.EndsWith("@")) {
				target = target.Substring(0, target.Length - 1);
				PM = false;
			} else
				PM = false;
		} else {
			target = null;
			PM = false;
		}

		Factoid factoid;
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref key);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (key == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", key));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", key));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string text = factoid.Data;
		if (target != null) {
			text = Colours.Bold + target + Colours.Bold + ": " + text;
		}

		string[] fields = this.GetContextAndKey(key);
		this.DisplayFactoid(e.Client, e.Sender.Nickname, e.Target.Target, target ?? e.Sender.Nickname, fields[0], fields[1], text, PM, true, e.Parameters[0].Equals(key, StringComparison.InvariantCultureIgnoreCase));
	}

	[Command("tell", 2, 2, "give <target> <key>", "Displays the named factoid.")]
	public void CommandFactoidGive(object? sender, CommandEventArgs e) {
		string key; string target; bool PM;

		key = e.Parameters[1];
		target = e.Parameters[0];
		PM = false;

		Factoid factoid;
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref key);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (key == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", key));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", key));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string text = Colours.Bold + target + Colours.Bold + ": " + factoid.Data;

		string[] fields = this.GetContextAndKey(key);
		this.DisplayFactoid(e.Client, e.Sender.Nickname, e.Target.Target, target ?? e.Sender.Nickname, fields[0], fields[1], text, PM, true, e.Parameters[0].Equals(key, StringComparison.InvariantCultureIgnoreCase));
	}

	[Trigger(@"^\?:(?:\s+(\S+))?", Permission = ".list")]
	public void RegexFactoidList(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		if (e.Match.Groups[1].Success) {
				this.CommandFactoidList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value
				}));
		} else {
			this.CommandFactoidList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[0]));
		}
	}
	[Command("faqlist", 0, 1, "faqlist [context]", "Displays a list of factoids.", Permission = ".list")]
	public async void CommandFactoidList(object? sender, CommandEventArgs e) {
		if (e.Parameters.Length == 0) {
			try {
				string context = this.FindContext(e.Target);
				this.CommandFactoidList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] { context }));
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				foreach (string context in ex.matches)
					this.CommandFactoidList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] { context }));
				return;
			}
		} else {
			if (!this.Contexts.ContainsKey(e.Parameters[0])) {
				e.Whisper("That context isn't defined.");
				return;
			}
			if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".list." + e.Parameters[0].Replace('/', '.'))) {
				e.Whisper(string.Format("You don't have permission to list the context of \u0002{0}\u0002.", e.Parameters[0]));
				return;
			}
			bool listHidden = await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".listhidden." + e.Parameters[0].Replace('/', '.'));

			var matches = new List<string>();
			foreach (var factoid in this.Factoids) {
				string[] fields = this.GetContextAndKey(factoid.Key);
				if ((fields[0] ?? "*").Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase)) {
					if (!factoid.Value.Hidden)
						matches.Add("\u000308" + fields[1] + "\u000F");
					else if (listHidden)
						matches.Add("\u000315" + fields[1] + "\u000F");
				}
			}

			if (matches.Count == 0) {
				e.Whisper(string.Format("There are no factoids in the context of \u0002{0}\u0002.", e.Parameters[0]));
			} else {
				e.Whisper(string.Format("The following factoids have been set for the context of \u0002{0}\u0002:", e.Parameters[0]));
				e.Whisper(string.Join(", ", matches));
			}
		}
	}

	[Trigger(@"^\?\+\s+(\S+)\s+(.+)", Permission = ".add")]
	public void RegexFactoidAdd(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandFactoidAdd(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value,
			e.Match.Groups[2].Value
		}));
	}
	[Command("faqadd", 2, 2, "faqadd <key> <data>", "Adds a factoid.")]
	public async void CommandFactoidAdd(object? sender, CommandEventArgs e) {
		bool dotTarget = false; string userKey = null; string key;
		if (e.Parameters[0] == ".") {
			dotTarget = true;
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			if (!this.Targets.TryGetValue(userKey, out key)) {
				e.Whisper("You haven't yet set a target for the dot.");
				return;
			}
		} else
			key = e.Parameters[0];

		string[] fields = this.GetContextAndKey(key);
		if (fields[0] == null) {
			try {
				fields[0] = this.FindContext(e.Target);
				key = fields[0] + "/" + fields[1];
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				e.Whisper("There is more than one FAQ context assigned to this channel.");
				e.Whisper(string.Format("Please specify one of: \u000307{0}\u000F.", string.Join("\u000F, \u000307", ex.matches.Select(s => s + "/\u000308" + key))));
				return;
			}
		}

		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".add." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to add to the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		if (this.Factoids.TryGetValue(key, out var factoid)) {
			factoid.Data += "\r\n" + e.Parameters[1];
			e.Whisper(string.Format("Appended text to the factoid \u000307{0}/\u000308{1}\u000F.", fields[0], fields[1]));
		} else {
			factoid = new Factoid() { Data = e.Parameters[1] };
			this.Factoids.Add(key, factoid);
			e.Whisper(string.Format("Added a factoid \u000307{0}/\u000308{1}\u000F.", fields[0], fields[1]));
		}

		if (!dotTarget) {
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			this.Targets[userKey] = key;
		}
	}

	[Trigger(@"^\?@\+\s+(\S+)\s+(\S+)", Permission = ".add")]
	public void RegexAliasAdd(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandAliasAdd(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value,
			e.Match.Groups[2].Value
		}));
	}
	[Command("faqaliasadd", 2, 2, "faqaliasadd <key> <target>", "Adds a factoid.")]
	public async void CommandAliasAdd(object? sender, CommandEventArgs e) {
		bool dotTarget = false; string userKey = null; string key; string target;
		if (e.Parameters[0] == ".") {
			if (e.Parameters[1] == ".") {
				e.Whisper("You can't use two dots like this.");
				return;
			}
			dotTarget = true;
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			if (!this.Targets.TryGetValue(userKey, out key)) {
				e.Whisper("You haven't yet set a target for the dot.");
				return;
			}
			target = e.Parameters[1];
		} else {
			key = e.Parameters[0];

			if (e.Parameters[1] == ".") {
				userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
				if (!this.Targets.TryGetValue(userKey, out target)) {
					e.Whisper("You haven't yet set a target for the dot.");
					return;
				}
			} else
				target = e.Parameters[1];
		}

	   string[] fields = this.GetContextAndKey(key);
		if (fields[0] == null) {
			try {
				fields[0] = this.FindContext(e.Target);
				key = fields[0] + "/" + fields[1];
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				e.Whisper("There is more than one FAQ context assigned to this channel.");
				e.Whisper(string.Format("Please specify one of: \u000307{0}\u000F.", string.Join("\u000F, \u000307", ex.matches.Select(s => s + "/\u000308" + key))));
				return;
			}
		}

		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".add." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to add to the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string[] fields2 = this.GetContextAndKey(target);
		if (fields2[0] == null) {
			try {
				fields2[0] = this.FindContext(e.Target);
				target = fields2[0] + "/" + fields2[1];
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				e.Whisper("There is more than one FAQ context assigned to this channel.");
				e.Whisper(string.Format("Please specify one of: \u000307{0}\u000F.", string.Join("\u000F, \u000307", ex.matches.Select(s => s + "/\u000308" + key))));
				return;
			}
		}
		if (!this.Factoids.ContainsKey(target)) {
			e.Whisper(string.Format("The factoid \u000307{0}/\u000308{1}\u000F isn't defined.", fields2[0], fields2[1]));
			return;
		}

		if (this.Factoids.ContainsKey(key)) {
			e.Whisper(string.Format("A factoid \u000307{0}/\u000308{1}\u000F already exists.", fields[0], fields[1]));
			return;
		} else if (this.Aliases.ContainsKey(key)) {
			e.Whisper(string.Format("An alias \u000307{0}/\u000308{1}\u000F already exists.", fields[0], fields[1]));
			return;
		} else {
			this.Aliases.Add(key, e.Parameters[1]);
			e.Whisper(string.Format("Added an alias \u000307{0}/\u000308{1}\u000F.", fields[0], fields[1]));
		}

		if (!dotTarget) {
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			this.Targets[userKey] = key;
		}
	}

	[Trigger(@"^\?@:(?:\s+(\S+))?", Permission = ".list")]
	public void RegexAliasList(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		if (e.Match.Groups[1].Success) {
			this.CommandAliasList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value
				}));
		} else {
			this.CommandAliasList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[0]));
		}
	}
	[Command("faqaliaslist", 0, 1, "faqaliaslist [key|context]", "List all aliases for a specified factoid, or in a specified context.",
		Permission = ".list")]
	public async void CommandAliasList(object? sender, CommandEventArgs e) {
		string target;  string displayKey; bool found; List<string> matches;
		if (e.Parameters.Length == 1) {
			target = e.Parameters[0];
			try {
				this.FindFactoid(e.Target, e.Sender, ref target);
				found = true;
			} catch (KeyNotFoundException) {
				found = false;
			} catch (BrokenAliasException) {
				if (target == ".")
					e.Whisper("You haven't yet set a target for the dot.");
				else
					e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
				return;
			} catch (MultipleContextException ex) {
				e.Whisper(string.Format("Multiple maching factoids were found."));
				e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
				return;
			}

			if (found) {
				// List aliases of a factoid.
				string[] fields = this.GetContextAndKey(target);
				bool displayFullKey = e.Parameters[0].Equals(target, StringComparison.InvariantCultureIgnoreCase);

				matches = new List<string>();
				foreach (var alias in this.Aliases) {
					if (alias.Value.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase)) {
						string[] fields2 = this.GetContextAndKey(alias.Key);
						if (displayFullKey || !fields[0].Equals(fields2[0], StringComparison.InvariantCultureIgnoreCase))
							displayKey = "\u000307" + fields2[0] + "/\u000308" + fields2[1] + "\u000F";
						else
							displayKey = "\u000308" + fields2[1] + "\u000F";
						matches.Add(displayKey);
					}
				}

				if (displayFullKey)
					displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
				else
					displayKey = "\u000308" + fields[1] + "\u000F";

				if (matches.Count == 0) {
					e.Whisper(string.Format("{0} has no aliases.", string.Join(", ", matches)));
				} else {
					e.Whisper(string.Format("{0} has the following aliases:", displayKey));
					e.Whisper(string.Join(", ", matches));
				}
				return;
			}
		}

		// List aliases in a context.
		if (e.Parameters.Length == 0) {
			try {
				target = this.FindContext(e.Target);
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				e.Whisper("There is more than one FAQ context assigned to this channel.");
				e.Whisper(string.Format("Please specify one of: \u000307{0}\u000F.", string.Join("\u000F, \u000307", ex.matches)));
				return;
			}
		} else {
			target = e.Parameters[0];
			if (!this.Contexts.ContainsKey(target)) {
				e.Whisper("That context isn't defined.");
				return;
			}
		}

		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".list." + target.Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to list the context of \u0002{0}\u0002.", target));
			return;
		}
		bool listHidden = await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".listhidden." + target.Replace('/', '.'));

		matches = new List<string>();
		foreach (var alias in this.Aliases) {
			string[] fields = this.GetContextAndKey(alias.Key);

			if ((fields[0] ?? "*").Equals(target, StringComparison.InvariantCultureIgnoreCase)) {
				if (this.Factoids.TryGetValue(alias.Value, out var factoid)) {
					if (!factoid.Hidden)
						matches.Add("\u000308" + fields[1] + "\u000F");
					else if (listHidden)
						matches.Add("\u000315" + fields[1] + "\u000F");
				} else
					matches.Add("\u000304" + fields[1] + "\u000F");
			}
		}

		if (matches.Count == 0) {
			e.Whisper(string.Format("There are no aliases in the context of \u0002{0}\u0002.", target));
		} else {
			e.Whisper(string.Format("The following aliases have been set for the context of \u0002{0}\u0002:", target));
			e.Whisper(string.Join(", ", matches));
		}
	}

	[Trigger(@"^\?-\s+(\S+)", Permission = ".delete")]
	public void RegexFactoidDelete(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandFactoidDelete(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value
		}));
	}
	[Command("faqdelete", 1, 3, "faqdelete <key> [target][@|@@]", "Displays the named factoid.")]
	public async void CommandFactoidDelete(object? sender, CommandEventArgs e) {
		string target = e.Parameters[0];

		Factoid factoid;
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found."));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}
		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".delete." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to delete from the context of \u0002{0}\u0002.", fields[0]));
			return;
		}
		this.Factoids.Remove(target);

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";
		e.Whisper(string.Format("Deleted the factoid {0}.", displayKey));
	}

	[Trigger(@"^\?@-\s+(\S+)", Permission = ".delete")]
	public void RegexAliasDelete(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandAliasDelete(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value,
		}));
	}
	[Command("faqaliasdelete", 1, 1, "faqaliasdelete <key>", "Deletes an alias")]
	public void CommandAliasDelete(object? sender, CommandEventArgs e) {
		string target; bool dotTarget = false; string userKey;
		if (e.Parameters[0] == ".") {
			dotTarget = true;
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			if (!this.Targets.TryGetValue(userKey, out target)) {
				e.Whisper("You haven't yet set a target for the dot.");
				return;
			}
		} else
			target = e.Parameters[0];

		string[] fields = this.GetContextAndKey(target);
		if (fields[0] == null) {
			try {
				fields[0] = this.FindContext(e.Target);
			} catch (KeyNotFoundException) {
				e.Whisper("There is no FAQ context assigned to this channel.");
				return;
			} catch (MultipleContextException ex) {
				e.Whisper("There is more than one FAQ context assigned to this channel.");
				e.Whisper(string.Format("Please specify one of: \u000307{0}\u000F.", string.Join("\u000F, \u000307", ex.matches.Select(s => s + "/\u000308" + fields[1]))));
				return;
			}
		}

		if (this.Aliases.Remove(target)) {
			e.Whisper(string.Format("Removed the alias \u000307{0}/\u000308{1}\u000F.", fields[0], fields[1]));
		} else {
			e.Whisper(string.Format("The alias \u000307{0}/\u000308{1}\u000F isn't defined.", fields[0], fields[1]));
			return;
		}

		if (!dotTarget) {
			userKey = e.Client.Extensions.NetworkName + "/" + e.Sender.Nickname;
			this.Targets[userKey] = target;
		}
	}

	[Command("faqset", 2, 3, "faqset <key> [setting] [value]", "Changes settings for a factoid.", Permission = ".faqset")]
	public async void CommandFactoidSet(object? sender, CommandEventArgs e) {
		string target = e.Parameters[0];

		Factoid factoid;
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found."));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}
		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".set." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to modify factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		if (e.Parameters.Length == 2) {
			switch (e.Parameters[1].ToUpperInvariant()) {
				case "RATELIMITCOUNT":
					if (factoid.RateLimitCount == 0)
						e.Reply(string.Format("The rate limit for {0} is disabled.", displayKey, factoid.RateLimitCount));
					else if (factoid.RateLimitCount == 1)
						e.Reply(string.Format("The rate limit for {0} is set to \u0002{1}\u0002 trigger.", displayKey, factoid.RateLimitCount));
					else
						e.Reply(string.Format("The rate limit for {0} is set to \u0002{1}\u0002 triggers.", displayKey, factoid.RateLimitCount));
					break;
				case "RATELIMITTIME":
					if (factoid.RateLimitTime == 0)
						e.Reply(string.Format("The rate limit for {0} is disabled.", displayKey, factoid.RateLimitTime));
					else if (factoid.RateLimitTime == 1)
						e.Reply(string.Format("The rate limit for {0} is set to \u0002{1}\u0002 second.", displayKey, factoid.RateLimitTime));
					else
						e.Reply(string.Format("The rate limit for {0} is set to \u0002{1}\u0002 seconds.", displayKey, factoid.RateLimitTime));
					break;
				case "HIDELABEL":
					if (factoid.HideLabel)
						e.Reply(string.Format("{0} is set to \u00039hide\u000F the label when it is triggered.", displayKey));
					else
						e.Reply(string.Format("{0} is set to \u00034show\u000F the label when it is triggered.", displayKey));
					break;
				case "HIDDEN":
					if (factoid.HideLabel)
						e.Reply(string.Format("{0} is \u00039hidden\u000F.", displayKey));
					else
						e.Reply(string.Format("{0} is \u00034listed\u000F.", displayKey));
					break;
				case "NOTICEONJOIN":
					if (factoid.HideLabel)
						e.Reply(string.Format("{0} is set to \u00039NOTICE the user\u000F when triggered by joins.", displayKey));
					else
						e.Reply(string.Format("{0} is set to \u00034PRIVMSG the channel\u000F when triggered by joins.", displayKey));
					break;
				default:
					e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002 for factoids.", e.Parameters[1]));
					break;
			}
		} else {
			int value;
			switch (e.Parameters[1].ToUpperInvariant()) {
				case "RATELIMITCOUNT":
					if (int.TryParse(e.Parameters[2], out value)) {
						if (value < 0)
							e.Reply(string.Format("That's not a valid value. Use a non-negative number.", displayKey, value));
						else {
							factoid.RateLimitCount = value;
							if (value == 0)
								e.Reply(string.Format("The rate limit for {0} is now disabled.", displayKey, value));
							else if (value == 1)
								e.Reply(string.Format("The rate limit for {0} is now set to \u0002{1}\u0002 trigger.", displayKey, value));
							else
								e.Reply(string.Format("The rate limit for {0} is now set to \u0002{1}\u0002 triggers.", displayKey, value));
						}
					} else
						e.Reply(string.Format("That's not a valid integer.", displayKey, value));
					break;
				case "RATELIMITTIME":
					if (int.TryParse(e.Parameters[2], out value)) {
						if (value < 0)
							e.Reply(string.Format("That's not a valid value. Use a non-negative number.", displayKey, value));
						else {
							factoid.RateLimitTime = value;
							if (value == 0)
								e.Reply(string.Format("The rate limit for {0} is now disabled.", displayKey, value));
							else if (value == 1)
								e.Reply(string.Format("The rate limit for {0} is now set to \u0002{1}\u0002 second.", displayKey, value));
							else
								e.Reply(string.Format("The rate limit for {0} is now set to \u0002{1}\u0002 seconds.", displayKey, value));
						}
					}
					break;
				case "HIDELABEL":
					if (Bot.TryParseBoolean(e.Parameters[2], out factoid.HideLabel)) {
						if (factoid.HideLabel)
							e.Reply(string.Format("{0} is now set to \u00039hide\u000F the label when it is triggered.", displayKey));
						else
							e.Reply(string.Format("{0} is now set to \u00034show\u000F the label when it is triggered.", displayKey));
					} else
						e.Whisper(string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'true' or 'false'.", e.Parameters[2]));
					break;
				case "HIDDEN":
					if (Bot.TryParseBoolean(e.Parameters[2], out factoid.Hidden)) {
						if (factoid.Hidden)
							e.Reply(string.Format("{0} is now \u00039hidden\u000F.", displayKey));
						else
							e.Reply(string.Format("{0} is now \u00034listed\u000F.", displayKey));
					} else
						e.Whisper(string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'true' or 'false'.", e.Parameters[2]));
					break;
				case "NOTICEONJOIN":
					if (Bot.TryParseBoolean(e.Parameters[2], out factoid.HideLabel)) {
						if (factoid.NoticeOnJoin)
							e.Reply(string.Format("{0} is now set to \u00039NOTICE the user\u000F when triggered by joins.", displayKey));
						else
							e.Reply(string.Format("{0} is now set to \u00034PRIVMSG the channel\u000F when triggered by joins.", displayKey));
					} else
						e.Whisper(string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'true' or 'false'.", e.Parameters[2]));
					break;
				default:
					e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002 for factoids.", e.Parameters[1]));
					break;
			}
		}
	}

	[Command("contextadd", 1, 2, "contextadd <name> [channels]", "Defines a FAQ context.\r\nA FAQ context lets you organise the FAQ data better, and also defines which channels I should listen in for regular expression triggers.",
		Permission = ".contextadd")]
	public void CommandContextAdd(object? sender, CommandEventArgs e) {
		var channels = new List<string>();

		if (e.Parameters[0] == ".") {
			e.Whisper("You can't use the dot here.");
			return;
		}
		if (this.Contexts.ContainsKey(e.Parameters[0]))
			e.Whisper(string.Format("The context\u00037 {0}\u000F has already been set. Changing its associated channels.", e.Parameters[0]));

		if (e.Parameters.Length == 1)
			channels.Add("*");
		else
			channels.AddRange(e.Parameters[1].Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));

		this.Contexts[e.Parameters[0]] = channels.ToArray();
		e.Whisper(string.Format("Added a context \u0002{0}\u0002.", e.Parameters[0]));
	}

	[Command("contextlist", 0, 1, "contextlist [name]", "Lists all FAQ contexts, or all channels associated with a given context.",
		Permission = ".contextlist")]
	public void CommandContextList(object? sender, CommandEventArgs e) {
		if (e.Parameters.Length == 1) {
			if (this.Contexts.TryGetValue(e.Parameters[0], out var channels))
				e.Whisper(string.Format("\u0002{0}\u0002 is assigned to: \u000307{1}", e.Parameters[0], string.Join("\u000F, \u000307", channels)));
			else
				e.Whisper(string.Format("\u0002{0}\u0002 hasn't been defined.", e.Parameters[0]));
		} else {
			if (this.Contexts.Count == 0) {
				e.Whisper("No contexts have been defined.");
			} else {
				e.Whisper("The following contexts have been defined:");

				IEnumerator<KeyValuePair<string, string[]>> enumerator = this.Contexts.GetEnumerator();
				var messageBuilder = new StringBuilder();
				bool EOF = false;

				while (!EOF) {
					messageBuilder.Clear();
					while (true) {
						if (enumerator.MoveNext()) {
							if (messageBuilder.Length != 0)
								messageBuilder.Append("\u000F, ");
							messageBuilder.Append("\u000307" + enumerator.Current.Key);

							if (messageBuilder.Length >= 300)
								break;
						} else {
							EOF = true;
							break;
						}
					}
					e.Whisper(messageBuilder.ToString());
				}
			}
		}
	}

	[Command("contextdelete", 1, 1, "contextdelete <name>", "Deletes a FAQ context", Permission = ".contextdelete")]
	public void CommandContextDelete(object? sender, CommandEventArgs e) {
		if (this.Contexts.Remove(e.Parameters[0]))
			e.Whisper(string.Format("Deleted the context \u0002{0}\u0002.", e.Parameters[0]));
		else
			e.Whisper(string.Format("\u0002{0}\u0002 hasn't been defined.", e.Parameters[0]));
	}

	[Trigger(@"^\?=\s+(\S+)(?:\s+(\+?\d+))?(?:\s+(.*))?", Permission = ".edit")]
	public void RegexFactoidEdit(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		if (e.Match.Groups[2].Success) {
			if (e.Match.Groups[3].Success) {
				this.CommandFactoidEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value, e.Match.Groups[3].Value
				}));
			} else {
				this.CommandFactoidEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value
				}));
			}
		} else if (e.Match.Groups[3].Success) {
			this.CommandFactoidEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
				e.Match.Groups[1].Value, e.Match.Groups[3].Value
			}));
		} else {
			this.CommandFactoidEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
				e.Match.Groups[1].Value
			}));
		}
	}
	[Command("faqedit", 1, 3, "faqedit <key> [[+]<line>] [replacement line]",
		"Allows you to edit a factoid. With only a key, shows you the numbered lines.\r\n" +
		"Specify the line number to replace it, or +number to insert a line.\r\n" +
		"You can omit the replacement line to delete a line.",
		Permission = ".edit")]
	public async void CommandFactoidEdit(object? sender, CommandEventArgs e) {
		string target;
		Factoid factoid;

		target = e.Parameters[0];
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", target));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string[] lines = factoid.Data.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".edit." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to edit factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		if (e.Parameters.Length == 1) {
			e.Whisper(displayKey + ":");
			for (int i = 0; i < lines.Length; ++i) {
				e.Whisper("\u000308" + (i + 1) + "\u00037:\u000F " + lines[i]);
				System.Threading.Thread.Sleep(600);
			}
		} else if (e.Parameters.Length == 2) {
			if (int.TryParse(e.Parameters[1], out int line)) {
				--line;
				// Delete the line specified.
				if (line >= 0 && line < lines.Length) {
					factoid.Data = null;
					for (int i = 0; i < lines.Length; ++i) {
						if (i != line) {
							if (factoid.Data != null) factoid.Data += "\n";
							factoid.Data += lines[i];
						}
					}
					e.Whisper(string.Format("Removed line {1} from {0}.", displayKey, line + 1));
				} else {
					e.Whisper(string.Format("{0} has no line number {1}.", displayKey, line + 1));
				}
			} else {
				// If the line number is omitted and the factoid content is only one line, we will replace it.
				if (lines.Length == 1) {
					factoid.Data = e.Parameters[1];
					e.Whisper(string.Format("Changed the content of {0}.", displayKey, e.Parameters[1]));
				} else {
					e.Fail(string.Format("Because {0} has more than one line, you must specify a line number to affect after the factoid key.", displayKey));
				}
			}
		} else {
			string lineString = e.Parameters[1];
			bool insert;

			if (lineString.StartsWith("+")) {
				insert = true;
				lineString = lineString.Substring(1);
			} else
				insert = false;

			if (int.TryParse(lineString, out int line)) {
				if (line >= 0 && line <= lines.Length) {
					if (insert || line == lines.Length) {
						if (line == lines.Length) {
							factoid.Data += "\n" + e.Parameters[2];
						} else {
							factoid.Data = null;
							for (int i = 0; i < lines.Length; ++i) {
								if (i == line) {
									if (factoid.Data != null) factoid.Data += "\n";
									factoid.Data += e.Parameters[2];
								}
								if (factoid.Data != null) factoid.Data += "\n";
								factoid.Data += lines[i];
							}
						}
						e.Whisper(string.Format("Added text as line {1} to {0}.", displayKey, e.Parameters[1]));
					} else {
						factoid.Data = null;
						for (int i = 0; i < lines.Length; ++i) {
							if (i == line) {
								if (factoid.Data != null) factoid.Data += "\n";
								factoid.Data += e.Parameters[2];
							} else {
								if (factoid.Data != null) factoid.Data += "\n";
								factoid.Data += lines[i];
							}
						}
						e.Whisper(string.Format("Changed line {1} in {0}.", displayKey, e.Parameters[1]));
					}
				} else {
					e.Whisper(string.Format("{0} doesn't have a line number {1}.", displayKey, e.Parameters[1]));
				}
			} else
				e.Whisper(string.Format("That's not a valid integer.", e.Parameters[1]));
		}
	}

	[Trigger(@"^\?\*\+\s+(\S+)\s+(.+)", Permission = ".regex")]
	public void RegexFactoidRegexAdd(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandFactoidRegexAdd(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value, e.Match.Groups[2].Value
		}));
	}
	[Command("faqregexadd", 2, 2, "faqregexadd <key> [[MSG:|ACTION:|JOIN:|PART:|QUIT:|KICK:|LEAVE:|NICK:|INVITE:]<user mask>:<channel mask>:<permission>]<regex>", "Assigns a regular expression to a FAQ entry. When someone sends a message to a channel within the FAQ's context that matches the regex, the FAQ data will be displayed in the channel.",
		Permission = ".regex")]
	public async void CommandFactoidRegexAdd(object? sender, CommandEventArgs e) {
		string target;
		Factoid factoid;

		target = e.Parameters[0];
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", target));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".regex." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to assign expressions to factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		factoid.Expressions.Add(e.Parameters[1]);
		e.Whisper(string.Format("Added a regular expression {0}.", displayKey));
	}

	[Trigger(@"^\?\*:\s+(\S+)", Permission = ".regex")]
	public void RegexFactoidRegexList(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandFactoidRegexList(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value
		}));
	}
	[Command("faqregexlist", 2, 2, "faqregexlist <key> [page]", "Assigns a regular expression to a FAQ entry. When someone sends a message to a channel within the FAQ's context that matches the regex, the FAQ data will be displayed in the channel.",
		Permission = ".regex")]
	public async void CommandFactoidRegexList(object? sender, CommandEventArgs e) {
		string target;
		Factoid factoid;

		target = e.Parameters[0];
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", target));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".regex." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to assign expressions to factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		if (factoid.Expressions.Count == 0)
			e.Reply(string.Format("{0} has no regular expressions.", displayKey));
		else {
			e.Reply(string.Format("The following regular expressions are assigned to {0}:", displayKey));
			int bound; int i;
			if (e.Parameters.Length == 2) {
				if (int.TryParse(e.Parameters[1], out bound))
					bound *= 8;
				else
					bound = 0;
			} else
				bound = 0;
			i = bound;
			bound += 8;
			for (; i < factoid.Expressions.Count && i < bound; ++i)
				e.Reply("\u000308" + i + "\u000307:\u000F " + factoid.Expressions[i]);
		}
	}

	[Trigger(@"^\?\*=\s+(\S+)(?:\s+(\d+)(?:\s+(.*))?)?")]
	public void RegexFactoidRegexEdit(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		if (e.Match.Groups[2].Success) {
			if (e.Match.Groups[3].Success) {
				this.CommandFactoidRegexEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value, e.Match.Groups[3].Value
				}));
			} else {
				this.CommandFactoidRegexEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
					e.Match.Groups[1].Value, e.Match.Groups[2].Value
				}));
			}
		} else {
			this.CommandFactoidRegexEdit(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
				e.Match.Groups[1].Value
			}));
		}
	}
	[Command("faqregexedit", 1, 3, "faqregexedit <key> [number] [replacement]", "Allows you to edit a factoid's assigned regular expressions. With only a key, shows you the numbered expressions.",
		Permission = ".regex")]
	public async void CommandFactoidRegexEdit(object? sender, CommandEventArgs e) {
		string target;
		Factoid factoid;

		if (e.Parameters.Length == 1) {
			this.CommandFactoidRegexList(sender, e);
			return;
		}

		target = e.Parameters[0];
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", target));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".regex." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to assign expressions to factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		if (e.Parameters.Length == 2) {
			if (int.TryParse(e.Parameters[1], out int line)) {
				if (line >= 0 && line < factoid.Expressions.Count) {
					factoid.Expressions.RemoveAt(line);
					e.Whisper(string.Format("Removed expression {1} from {0}.", displayKey, e.Parameters[1]));
				} else {
					e.Whisper(string.Format("{0} has no expression number {1}.", displayKey, e.Parameters[1]));
				}
			} else
				e.Whisper(string.Format("That's not a valid integer.", e.Parameters[1]));
		} else {
			if (int.TryParse(e.Parameters[1], out int line)) {
				if (line >= 0 && line <= factoid.Expressions.Count) {
					if (line == factoid.Expressions.Count) {
						factoid.Expressions.Add(e.Parameters[2]);
						e.Whisper(string.Format("Added an expression to {0}.", displayKey, e.Parameters[1]));
					} else {
						factoid.Expressions[line] = e.Parameters[2];
						e.Whisper(string.Format("Changed expression {1} to {0}.", displayKey, e.Parameters[1]));
					}
				} else {
					e.Whisper(string.Format("{0} doesn't have an expression number {1}.", displayKey, e.Parameters[1]));
				}
			} else
				e.Whisper(string.Format("That's not a valid integer.", e.Parameters[1]));
		}
	}

	[Trigger(@"^\?\*-\s+(\S+)\s+(\d+)")]
	public void RegexFactoidRegexDelete(object? sender, TriggerEventArgs e) {
		if (!this.ShortcutCheck(e.Target)) return;
		this.CommandFactoidRegexDelete(sender, new CommandEventArgs(e.Client, e.Target, e.Sender, new string[] {
			e.Match.Groups[1].Value, e.Match.Groups[2].Value
		}));
	}
	[Command("faqregexdelete", 2, 2, "faqregexdelete <key> <number>", "Removes a regular expression from a factoid",
		Permission = ".regex")]
	public async void CommandFactoidRegexDelete(object? sender, CommandEventArgs e) {
		string target;
		Factoid factoid;

		if (e.Parameters.Length == 1) {
			this.CommandFactoidRegexList(sender, e);
			return;
		}

		target = e.Parameters[0];
		try {
			factoid = this.FindFactoid(e.Target, e.Sender, ref target);
		} catch (KeyNotFoundException) {
			e.Whisper("That factoid isn't defined.");
			return;
		} catch (BrokenAliasException) {
			if (target == ".")
				e.Whisper("You haven't yet set a target for the dot.");
			else
				e.Whisper(string.Format("The alias \u0002{0}\u0002 seems to be broken.", target));
			return;
		} catch (MultipleContextException ex) {
			e.Whisper(string.Format("Multiple maching factoids were found.", target));
			e.Whisper(string.Format("Please specify one of:\u0002 {0}\u0002.", string.Join("\u0002, \u0002", ex.matches)));
			return;
		}

		string[] fields = this.GetContextAndKey(target);
		if (!await this.Bot.CheckPermissionAsync(e.Sender, this.Key + ".regex." + fields[0].Replace('/', '.'))) {
			e.Whisper(string.Format("You don't have permission to assign expressions to factoids in the context of \u0002{0}\u0002.", fields[0]));
			return;
		}

		string displayKey;
		if (target.Equals(e.Parameters[0], StringComparison.InvariantCultureIgnoreCase))
			displayKey = "\u000307" + fields[0] + "/\u000308" + fields[1] + "\u000F";
		else
			displayKey = "\u000308" + fields[1] + "\u000F";

		if (int.TryParse(e.Parameters[1], out int line)) {
			if (line >= 0 && line < factoid.Expressions.Count) {
				factoid.Expressions.RemoveAt(line);
				e.Whisper(string.Format("Removed expression {1} from {0}.", displayKey, e.Parameters[1]));
			} else {
				e.Whisper(string.Format("{0} has no expression number {1}.", displayKey, e.Parameters[1]));
			}
		} else
			e.Whisper(string.Format("That's not a valid integer.", e.Parameters[1]));
	}

	[Command(new string[] { "globalset", "set" }, 1, 2, "set [setting] [value]", "Changes settings for this plugin.",
		Permission = ".set")]
	public void CommandSet(object? sender, CommandEventArgs e) {
		if (e.Parameters.Length == 1) {
			switch (e.Parameters[0].ToUpperInvariant()) {
				case "NOSHORTCUT":
					e.Reply(string.Format("Shortcuts are disabled in the following channels: {0}", string.Join("\u0002, \u0002", this.NoShortcutChannels)));
					break;
				case "LABELFORMAT":
					e.Reply(string.Format("The label format is: {0}", string.Join("\u0002, \u0002", this.LabelFormat)));
					break;
				default:
					e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[0]));
					break;
			}
		} else {
			switch (e.Parameters[0].ToUpperInvariant()) {
				case "NOSHORTCUT":
					this.NoShortcutChannels = new List<string>(e.Parameters[1].Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
					e.Reply(string.Format("Shortcuts are now disabled in the following channels: {0}", string.Join("\u0002, \u0002", this.NoShortcutChannels)));
					break;
				case "LABELFORMAT":
					this.LabelFormat = e.Parameters[1];
					e.Reply(string.Format("The label format was set to: {0}", string.Join("\u0002, \u0002", this.LabelFormat)));
					break;
				default:
					e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[0]));
					break;
			}
		}
	}

	[Command("faqload", 0, 0, "faqload", "Reloads FAQ data from the file.",
		Permission = ".reload")]
	public void CommandLoad(object? sender, CommandEventArgs e) {
		try {
			this.Factoids.Clear();
			this.Contexts.Clear();
			this.Aliases.Clear();
			this.LoadData(this.Key == "FAQ" ? "FAQ.ini" : "FAQ-" + this.Key + ".ini");
			e.Whisper("FAQ data has been reloaded successfully.");
		} catch (Exception ex) {
			e.Whisper(string.Format("I couldn't reload FAQ data: {0}", ex.Message));
		}
	}

	[Command("faqsave", 0, 0, "faqsave", "Saves FAQ data to the file.",
		Permission = ".save")]
	public void CommandSave(object? sender, CommandEventArgs e) {
		try {
			this.SaveData(this.Key == "FAQ" ? "FAQ.ini" : "FAQ-" + this.Key + ".ini");
			e.Whisper("FAQ data has been saved successfully.");
		} catch (Exception ex) {
			e.Whisper(string.Format("I couldn't save FAQ data: {0}", ex.Message));
		}
	}
#endregion
}

[Serializable]
public class MultipleContextException : Exception {
	internal string[] matches;

	public string[] GetMatches() {
		return (string[]) this.matches.Clone();
	}

	public MultipleContextException(string[] matches) : base("The command is ambiguous because more than one FAQ context is assigned to the channel.") {
		this.matches = matches;
	}
	public MultipleContextException(string[] matches, string message) : base(message) {
		this.matches = matches;
	}
	public MultipleContextException(string[] matches, string message, Exception inner) : base(message, inner) {
		this.matches = matches;
	}
	protected MultipleContextException(
	  System.Runtime.Serialization.SerializationInfo info,
	  System.Runtime.Serialization.StreamingContext context)
		: base(info, context) {
			this.matches = (string[]) info.GetValue("Matches", typeof(string[]));
	}

	public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) {
		if (info == null) throw new ArgumentNullException("info");
		info.AddValue("Matches", this.matches, typeof(string[]));
		base.GetObjectData(info, context);
	}
}

[Serializable]
public class BrokenAliasException : Exception {
	public BrokenAliasException() : base("The target of the FAQ alias is missing.") { }
	public BrokenAliasException(string message) : base(message) { }
	public BrokenAliasException(string message, Exception inner) : base(message, inner) { }
	protected BrokenAliasException(
	  System.Runtime.Serialization.SerializationInfo info,
	  System.Runtime.Serialization.StreamingContext context)
		: base(info, context) { }
}
