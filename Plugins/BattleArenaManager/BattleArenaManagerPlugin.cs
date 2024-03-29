﻿using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Timers;
using AnIRC;
using CBot;
using FileMode = System.IO.FileMode;
using Timer = System.Timers.Timer;

namespace BattleArenaManager;
[ApiVersion(4, 0)]
public class BattleArenaManagerPlugin : Plugin {
	public IrcClient ArenaConnection;
	public string ArenaChannel;
	public string ArenaNickname;
	public string ArenaDirectory;

	public bool CheckForUpdates = true;
	public bool ReviveBot = false;
	public bool ListenForErrors = false;

	public string RepositoryUrl = "https://github.com/Iyouboushi/mIRC-BattleArena.git";
	public string RepositoryBranch = "master";
	public string BackupExecutable;
	public string BackupPath = "backups/{0:yyyy-MM-dd}{1}.zip";

	public string ArenaLogPath = "logs/EsperNet/status.{0:yyyyMMdd}.log";
	public string[] ErrorNotificationTargets;

	private Thread? logListenThread;

	public DateTime LastCommitTime = DateTime.Now;
	private DateTime LastFetchTime;
	private Timer? checkTimer;

	private TaskCompletionSource<int>? ApplyUpdateTaskSource;

	public bool UpdateNextBattle { get; private set; }

	public DateTime LastBattle { get; private set; }
	public bool BattleOff { get; private set; }

	public override string Name => "Battle Arena Manager";

	public override string[] Channels {
		get { return base.Channels; }
		set {
			base.Channels = value;
			this.CheckChannels();
		}
	}

	private void CheckChannels() {
		this.ArenaConnection = null;
		this.ArenaChannel = null;
		foreach (string channel in this.Channels) {
			string[] fields = channel.Split(new char[] { '/' }, 2);
			if (fields.Length == 1)
				fields = new string[] { null, fields[0] };
			if (fields[1] == "*") continue;
			foreach (ClientEntry clientEntry in Bot.Clients) {
				IrcClient client = clientEntry.Client;
				if (client.Address == "!Console") continue;
				if (fields[0] == null || fields[0] == "*" ||
					fields[0].Equals(clientEntry.Name, StringComparison.InvariantCultureIgnoreCase) ||
					fields[0].Equals(clientEntry.Address, StringComparison.InvariantCultureIgnoreCase)) {
					if (client.Channels.Contains(fields[1])) {
						this.ArenaConnection = client;
						this.ArenaChannel = fields[1];
						return;
					}
				}
			}
		}
	}

	public override void Initialize() {
		this.LoadConfig();
		this.LoadData();

		this.defaultLanguage.Add("UpToDate", "We're up to date.");
		this.defaultLanguage.Add("NewCommit", "I found {0} new commit: '{1}'");
		this.defaultLanguage.Add("NewCommits", "I found {0} new commits, including '{1}'");
		this.defaultLanguage.Add("ApplyAfterNextBattle", "I'll apply the update after the next battle.");
		this.defaultLanguage.Add("ApplyAfterBattle", "I'll apply the update after this battle.");
		this.defaultLanguage.Add("BackupFailure", "The backup failed: sh exited with code {0}. Aborting.");
		this.defaultLanguage.Add("DownloadFailure", "The download failed: {0}");
		this.defaultLanguage.Add("UnpackFailure", "Unpacking failed: tar exited with code {0}.");
		this.defaultLanguage.Add("BadArchiveStructure", "Unpacking failed: I wasn't trained to deal with this archive.");
		this.defaultLanguage.Add("UpdateComplete", "Update complete.");
		this.defaultLanguage.Add("UpdateFailure", "Houston, we have a problem: {0}");
		this.defaultLanguage.Add("TerminationFailure", "Termination failed. sh exited with code {0}. Aborting.");
		this.defaultLanguage.Add("RestartFailure", "Battle Arena remains lifeless. bash exited with code {0}. Aborting.");
		this.defaultLanguage.Add("ArenaScriptError", "\u00034Battle Arena has encountered a problem: {0}");

		if (this.CheckForUpdates) {
			if (this.ArenaDirectory == null) throw new InvalidOperationException("The Arena working directory must be set in order to check for updates.");

			this.checkTimer = new Timer(3600e3);  // 1 hour
			this.checkTimer.Elapsed += checkTimer_Elapsed;
			this.checkTimer.Start();
		}

		if (this.ListenForErrors) {
			this.logListenThread = new Thread(this.LogListen) { Name = this.Key + " log listener thread" };
			this.logListenThread.Start();
		}
	}

	public override void OnUnload() {
		this.logListenThread?.Abort();
		base.OnUnload();
	}

	private void LogListen() {
		try {
			while (true) {
				DateTime date = DateTime.Now.Date;
				string path = string.Format(this.ArenaLogPath, date);
				while (!File.Exists(path)) Thread.Sleep(60000);

				using (var reader = new StreamReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) {
					reader.ReadToEnd();  // Skip past existing data.
					while (DateTime.Now.Date == date) {
						while (!reader.EndOfStream) {
							string s = reader.ReadLine();
							var m = Regex.Match(s, @"^\x03\d\d?\[\d\d:\d\d:\d\d\] \* (.* \(line \d+, [^ )]+\))$");
							if (m.Success) {
								foreach (string target in this.ErrorNotificationTargets) {
									string message = this.GetMessage("ArenaScriptError", this.ArenaNickname, this.ArenaChannel, m.Groups[1].Value);
									if (this.ArenaConnection.IsChannel(target) || this.ArenaConnection.Channels[ArenaChannel].Users.Contains(target)) {
										Bot.Say(this.ArenaConnection, target, message, SayOptions.NoticeNever);
									}
								}
							}
						}
						while (reader.EndOfStream && DateTime.Now.Date == date) Thread.Sleep(10000);  // Wait for more data.
					}
				}
			}
		} catch (ThreadAbortException) { }
	}

	public override bool OnChannelJoin(object? sender, ChannelJoinEventArgs e) {
		if (e.Sender.Nickname == ((IrcClient) sender).Me.Nickname) {
			BattleOff = false;
			if (this.ArenaConnection == null) this.CheckChannels();
		}
		return base.OnChannelJoin(sender, e);
	}

	public void LoadConfig() {
		string filename = Path.Combine("Config", this.Key + ".ini");
		if (!File.Exists(filename)) return;

		using (StreamReader reader = new StreamReader(filename)) {
			int lineNumber = 0;

			while (!reader.EndOfStream) {
				string line = reader.ReadLine();
				++lineNumber;
				if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

				Match match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
				if (match.Success) {
					string field = match.Groups[1].Value;
					string value = match.Groups[2].Value;
					DateTime value2;
					bool value3;

					switch (field.ToUpper()) {
						case "ARENADIRECTORY":
							this.ArenaDirectory = value;
							break;
						case "ARENANICKNAME":
							this.ArenaNickname = value;
							break;
						case "LASTUPDATE":
							if (DateTime.TryParse(value, out value2)) {
								this.LastCommitTime = value2;
							} else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is not recognised as a valid date.", this.Key, lineNumber);
							break;
						case "CHECKFORUPDATES":
							if (Bot.TryParseBoolean(value, out value3)) {
								this.CheckForUpdates = value3;
							} else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is not recognised as a valid Boolean value.", this.Key, lineNumber);
							break;
						case "REPOSITORYURL":
							this.RepositoryUrl = value;
							break;
						case "REPOSITORYBRANCH":
							this.RepositoryBranch = value;
							break;
						case "BACKUPEXECUTABLE":
							this.BackupExecutable = value;
							break;
						case "BACKUPPATH":
							this.BackupPath = value;
							break;
						case "LISTENFORERRORS":
							if (Bot.TryParseBoolean(value, out value3)) {
								this.ListenForErrors = value3;
							} else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is not recognised as a valid Boolean value.", this.Key, lineNumber);
							break;
						case "ERRORNOTIFICATIONTARGETS":
							this.ErrorNotificationTargets = value.Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
							break;
						case "LOGPATH":
							this.ArenaLogPath = value;
							break;
						case "REVIVEBOT":
							if (Bot.TryParseBoolean(value, out value3)) {
								this.ReviveBot = value3;
							} else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is not recognised as a valid Boolean value.", this.Key, lineNumber);
							break;

						default:
							if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
							break;
					}
				}
			}
			reader.Close();
		}
	}

	public void LoadData() {
		if (File.Exists(Path.Combine("data", this.Key, "LastUpdate.txt"))) {
			var text = File.ReadAllText(Path.Combine("data", this.Key, "LastUpdate.txt"));
			this.LastCommitTime = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
		}
	}

	public void SaveConfig() {
		Directory.CreateDirectory("Config");
		using (StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false)) {
			writer.WriteLine("[Config]");
			writer.WriteLine("ArenaNickname={0}", this.ArenaNickname);
			writer.WriteLine("ArenaDirectory={0}", this.ArenaDirectory);
			writer.WriteLine("CheckForUpdates={0}", this.CheckForUpdates ? "Yes" : "No");
			writer.WriteLine("RepositoryUrl={0}", this.RepositoryUrl);
			writer.WriteLine("RepositoryBranch={0}", this.RepositoryBranch);
			writer.WriteLine("ListenForErrors={0}", this.ListenForErrors ? "Yes" : "No");
			writer.WriteLine("LogPath={0}", this.ArenaLogPath);
			if (this.ErrorNotificationTargets != null)
				writer.WriteLine("ErrorNotificationTargets={0}", string.Join(",", this.ErrorNotificationTargets));
			writer.WriteLine("ReviveBot={0}", this.ReviveBot ? "Yes" : "No");
			writer.Close();
		}
	}

	public void SaveData() {
		Directory.CreateDirectory(Path.Combine("data", this.Key));
		File.WriteAllText(Path.Combine("data", this.Key, "LastUpdate.txt"), this.LastCommitTime.ToString("u"));
	}

	public override bool OnChannelMessage(object? sender, ChannelMessageEventArgs e) {
		if (((IrcClient) sender).Address.EndsWith(".DCC") || (sender == this.ArenaConnection && ((IrcClient) sender).CaseMappingComparer.Equals(e.Channel.Name, this.ArenaChannel) &&
															((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname)))
			this.RunArenaRegex((IrcClient) sender, e.Channel, e.Sender, e.Message);
		return base.OnChannelMessage(sender, e);
	}

	public override bool OnPrivateNotice(object? sender, PrivateMessageEventArgs e) {
		if (this.ApplyUpdateTaskSource != null && e.Sender.Nickname == this.ArenaNickname && e.Sender.Client == this.ArenaConnection && e.Message == "OK") {
			this.ApplyUpdateTaskSource.SetResult(0);
			this.ApplyUpdateTaskSource = null;
		}
		return base.OnPrivateNotice(sender, e);
	}

	public bool RunArenaRegex(IrcClient client, IrcMessageTarget channel, IrcUser sender, string message) {
		foreach (System.Reflection.MethodInfo method in this.GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)) {
			foreach (Attribute attribute in method.GetCustomAttributes(typeof(ArenaRegexAttribute), false)) {
				foreach (string expression in ((ArenaRegexAttribute) attribute).Expressions) {
					Match match = Regex.Match(message, expression);
					if (match.Success) {
						try {
							method.Invoke(this, new object[] { this, new TriggerEventArgs(client, channel, sender, match) });
						} catch (Exception ex) {
							this.LogError(method.Name, ex);
						}
						return true;
					}
				}
			}
		}
		return false;
	}

	[ArenaRegex(@"^\x034(?:(A dimensional portal has been detected\. The enemy force will arrive)|(A powerful dimensional rift has been detected\. The enemy force will arrive)|(The Allied Forces have detected an orb fountain! The party will be sent to destroy it)|(The Allied Forces have opened the coliseum to allow players to fight one another. The PVP battle will begin)|(A Manual battle has been started. Bot Admins will need to add monsters, npcs and bosses individually\. The battle will begin)|(An outpost of the Allied Forces HQ \x02is under attack\x02! Reinforcements are requested immediately! The reinforcements will depart)) in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\. (?:Players \S+ )?[Tt]ype \x02!enter\x02 (?:if you wish to join the battle|if they wish to join the battle|to join)")]
	[ArenaRegex(@"^\x034The doors to the \x02gauntlet\x02 are open\. Anyone willing to brave the gauntlet has(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)? to enter before the doors close\. Type \x02!enter\x02 if you wish to join the battle!")]
	[ArenaRegex(@"^\x0314\x02The President of the Allied Forces\x02 has been \x02kidnapped by monsters\x02! Are you a bad enough dude to save the president\? \x034The rescue party will depart in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\. Type \x02!enter\x02 if you wish to join the battle!")]
	[ArenaRegex(@"^\x034An \x02evil treasure chest Mimic\x02 is ready to fight\S? The battle will begin in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\. Type \x02!enter\x02 if you wish to join the battle!")]
	[ArenaRegex(@"\x034A \x021 vs 1 AI Match\x02 is about to begin! The battle will begin in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\.")]
	internal void OnBattleOpen(object? sender, TriggerEventArgs e) {
		BattleOff = false;
		LastBattle = DateTime.Now;
		ConsoleUtils.WriteLine("[" + this.Key + "] A battle is starting.");
	}

	public override bool OnUserQuit(object? sender, QuitEventArgs e) {
		if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX) {
			if (this.ReviveBot && sender == this.ArenaConnection && ((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname) &&
				e.Message.StartsWith("Ping timeout"))
				// The bot has crashed. We'd better revive it.
				Task.Run(new Action(this.ReviveArenaBot));
		}
		return base.OnUserQuit(sender, e);
	}

	[ArenaRegex(new string[] { @"^\x030?4The Battle is Over!",
		@"^\x030?4There were no players to meet the monsters on the battlefield! \x02The battle is over\x02."})]
	internal async void OnBattleEnd(object? sender, TriggerEventArgs e) {
		BattleOff = true;
		ConsoleUtils.WriteLine("[" + this.Key + "] A battle has ended.");

		if (this.UpdateNextBattle) {
			this.UpdateNextBattle = false;

			try {
				await this.ApplyUpdate(false, e.Channel);
				e.Reply("Update complete.");
			} catch (Exception ex) {
				this.LogError("ApplyUpdate", ex);
				e.Reply("There was a problem installing the update: " + ex.Message);
			}

			this.checkTimer.Start();
		}
	}

	[Command("check", 0, 0, "!check", "Checks for a Battle Arena update.", Permission = ".check")]
	public async void CommandCheck(object? sender, CommandEventArgs e) {
		e.Whisper("Checking for an update...");

		try {
			var result = await CheckUpdate(e.Channel, true);
		} catch (Exception ex) {
			this.LogError("CheckUpdate", ex);
			e.Reply("There was a problem checking for updates: " + ex.Message);
		}
	}

	[Command("update", 0, 0, "!update", "Updates the Battle Arena bot.", Permission = ".update")]
	public async void CommandUpdate(object? sender, CommandEventArgs e) {
		e.Whisper("Installing the latest version...");
		try {
			var result = await this.ApplyUpdate(true, e.Channel);
			if (!result) {
				e.Reply("Already up to date.");
			} else {
				e.Reply("Update complete.");
			}
		} catch (Exception ex) {
			e.Reply("There was a problem installing the update: " + ex.Message);
			this.LogError("ApplyUpdate", ex);
		}
	}

	private async void checkTimer_Elapsed(object? sender, ElapsedEventArgs e) {
		try {
			await this.CheckUpdate(null);
		} catch (Exception ex) {
			this.LogError("CheckUpdate", ex);
		}
	}

	private async Task<string> GitCommand(string workingDirectory, params string[] args) {
		Console.WriteLine($"[{this.Key}] git {string.Join(' ', args)}");
		var startInfo = new ProcessStartInfo("git") { WorkingDirectory = workingDirectory, RedirectStandardOutput = true };
		foreach (var s in args)
			startInfo.ArgumentList.Add(s);
		var process = new Process() { StartInfo = startInfo };
		process.Start();

		using var reader = process.StandardOutput;
		var task = reader.ReadToEndAsync();
		await process.WaitForExitAsync();
		return process.ExitCode == 0 ? await task : throw new GitException(args.FirstOrDefault(), process.ExitCode);
	}

	public async Task<int> CheckUpdate(IrcChannel? channel, bool forceAnnounce = false) {
		if (this.ArenaDirectory == null) throw new InvalidOperationException("Repository is not loaded.");
		await this.GitCommand(this.ArenaDirectory, "fetch");
		this.LastFetchTime = DateTime.UtcNow;

		var commitList = (await this.GitCommand(this.ArenaDirectory, "rev-list", "--format=oneline", "HEAD..FETCH_HEAD")).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		Console.WriteLine($"[{this.Key}] Commits behind: {commitList.Length}");
		if (commitList.Length == 0) {
			if (forceAnnounce)
				Bot.Say(channel?.Client ?? this.ArenaConnection, channel?.Target ?? this.ArenaChannel, "We're up to date.");
			return 0;
		}

		// Get the longest commit message to show to the user.
		var longestMessage = "";
		foreach (var line in commitList) {
			var message = line[(line.IndexOf(' ') + 1)..];
			if (message.Length > longestMessage.Length)
				longestMessage = message;
		}
		Bot.Say(channel?.Client ?? this.ArenaConnection, channel?.Target ?? this.ArenaChannel,
			$"I found {commitList.Length} new {(commitList.Length == 1 ? "commit" : "commits")}, including '{longestMessage}'.");

		// Should we update now?
		if (BattleOff) {
			if ((DateTime.Now - this.LastBattle).TotalMinutes >= 15) {
				// The automated battle system is probably off; update immediately.
				await this.ApplyUpdate(false, channel);
			} else {
				Bot.Say(this.ArenaConnection, this.ArenaChannel, this.GetMessage("ApplyAfterNextBattle", null, this.ArenaChannel, commitList.Length, longestMessage));
				this.UpdateNextBattle = true;
				this.checkTimer?.Stop();
			}
		} else {
			Bot.Say(this.ArenaConnection, this.ArenaChannel, this.GetMessage("ApplyAfterBattle", null, this.ArenaChannel, commitList.Length, longestMessage));
			this.UpdateNextBattle = true;
			this.checkTimer?.Stop();
		}

		return commitList.Length;
	}

	public async Task<bool> ApplyUpdate(bool fetch, IrcChannel? channel) {
		if (this.ArenaDirectory == null) throw new InvalidOperationException("Repository is not loaded.");

		if (fetch) {
			await this.GitCommand(this.ArenaDirectory, "fetch");
			this.LastFetchTime = DateTime.UtcNow;
		}

		var r = await this.GitCommand(this.ArenaDirectory, "rev-list", "--count", "HEAD..FETCH_HEAD");
		if (r.Trim() == "0") {
			Console.WriteLine($"[{this.Key}] Repository is not behind the remote repository.");
			return false;
		}
		r = await this.GitCommand(this.ArenaDirectory, "rev-list", "--count", "FETCH_HEAD..HEAD");
		if (r.Trim() != "0") {
			throw new InvalidOperationException("Cannot update the repository because it is ahead of the remote repository.");
		}

		if (this.BackupExecutable != null) {
			// Backup existing data.
			ConsoleUtils.WriteLine("Creating backup...");
			using var process = new Process() { StartInfo = new ProcessStartInfo(this.BackupExecutable, this.BackupPath) { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = this.ArenaDirectory } };
			process.Start();
			process.StandardOutput.ReadToEnd();
			process.WaitForExit();
			if (process.ExitCode != 0) {
				Bot.Say(this.ArenaConnection, this.ArenaChannel, this.GetMessage("BackupFailure", null, this.ArenaChannel, process.ExitCode));
				throw new Exception("Backup failed (the process exited with code " + process.ExitCode + ").");
			}
		}

		r = await this.GitCommand(this.ArenaDirectory, "stash");
		var stashWasCreated = r.StartsWith("Saved");

		try {
			await this.GitCommand(this.ArenaDirectory, "merge", "--ff-only", "FETCH_HEAD");
		} finally {
			// Roll back the stash even if the pull fails.
			if (stashWasCreated)
				await this.GitCommand(this.ArenaDirectory, "stash", "pop");
		}

		// Load scripts.
		// This will require the following script to be loaded by the Arena bot: https://gist.github.com/AndrioCelos/c040c03119f3029f535f
		var filesToReload = from f in Directory.GetFiles(this.ArenaDirectory) where f.EndsWith(".mrc", StringComparison.InvariantCultureIgnoreCase) || f.EndsWith(".als", StringComparison.InvariantCultureIgnoreCase)
							select Path.GetFileName(f);
		Bot.Say(this.ArenaConnection, this.ArenaNickname, "!!reload " + string.Join(" ", filesToReload), SayOptions.NoticeNever);
		this.ApplyUpdateTaskSource = new TaskCompletionSource<int>();

		var result = this.ApplyUpdateTaskSource.Task.Wait(TimeSpan.FromSeconds(30));
		if (!result) throw new TimeoutException("The Arena bot is not responding.");

		Console.WriteLine($"[{this.Key}] Done.");
		return true;
	}

	public void ReviveArenaBot() {
		if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
			throw new PlatformNotSupportedException("Reviving the Arena bot is currently only supported on UNIX systems.");

		Process process;

		// Terminate mIRC.
		process = new Process() {
			StartInfo = new ProcessStartInfo(Path.Combine("/", "bin", "bash"),
				@"-c ""screen -S bots -p 2 -X stuff $'\cC'""")
		};
		process.Start();
		process.WaitForExit();

		if (process.ExitCode != 0) {
			ConsoleUtils.WriteLine("Termination failed (bash exited with code " + process.ExitCode + "). Aborting.");
			Bot.Say(this.ArenaConnection, this.ArenaChannel, this.GetMessage("TerminationFailure", null, this.ArenaChannel, process.ExitCode));
			return;
		}

		Thread.Sleep(5000);

		// Start mIRC anew.
		process = new Process() {
			StartInfo = new ProcessStartInfo(Path.Combine("/", "bin", "bash"),
				@"-c ""screen -S bots -p 2 -X stuff $'wine mIRC.exe\n'""")
		};
		process.Start();
		process.WaitForExit();

		if (process.ExitCode != 0) {
			ConsoleUtils.WriteLine("Restart failed (bash exited with code " + process.ExitCode + "). Aborting.");
			Bot.Say(this.ArenaConnection, this.ArenaChannel, this.GetMessage("RestartFailure", null, this.ArenaChannel, process.ExitCode));
			return;
		}
	}
}

public class GitException : Exception {
	public GitException(string? command, int exitCode) : base($"git {command} exited with code {exitCode}") { }
}
