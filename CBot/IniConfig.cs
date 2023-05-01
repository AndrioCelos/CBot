using System.Text.RegularExpressions;
using AnIRC;
using Newtonsoft.Json.Linq;
using static System.StringSplitOptions;

namespace CBot;
public static class IniConfig {
	public static void LoadConfig(Bot bot, Config config) {
		if (File.Exists("CBotConfig.ini")) {
			var file = IniFile.FromFile("CBotConfig.ini");

			if (file.TryGetValue("Me", out var section)) {
				if (section.TryGetValue("Nicknames", out var value)) config.Nicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
				else if (section.TryGetValue("Nickname", out value)) config.Nicknames = new[] { value };

				if (section.TryGetValue("Username", out value)) config.Ident = value;
				if (section.TryGetValue("FullName", out value)) config.FullName = value;
				if (section.TryGetValue("UserInfo", out value)) config.UserInfo = value;
				section.TryGetValue("Avatar", out value); config.Avatar = value;  // That *can* be null.

				file.Remove("Me");
			}

			if (file.TryGetValue("Prefixes", out section)) {
				if (section.TryGetValue("Default", out var value)) {
					config.CommandPrefixes = value.Split((char[]?) null, RemoveEmptyEntries);
					section.Remove("Default");
				}

				foreach (var item in section)
					config.ChannelCommandPrefixes[item.Key] = item.Value.Split((char[]?) null, RemoveEmptyEntries);

				file.Remove("Prefixes");
			}

			foreach (var section2 in file) {
				string address; int port; bool tls = false;

				if (section2.Value.TryGetValue("Address", out var value)) {
					var fields = value.Split(new[] { ':' }, 2);
					if (fields.Length == 2) {
						address = fields[0];
						if (fields[1].StartsWith("+")) {
							tls = true;
							port = int.Parse(fields[1][1..]);
						} else
							port = int.Parse(fields[1]);
					} else {
						address = fields[0];
					}
				} else continue;
				port = section2.Value.TryGetValue("Port", out value) ? int.Parse(value)
					: tls ? 6697 : 6667;

				var network = new ClientEntry(section2.Key, address, port) { Tls = tls ? TlsMode.Tls : TlsMode.StartTlsOptional };

				if (section2.Value.TryGetValue("Password", out value)) network.Password = value;

				if (section2.Value.TryGetValue("Nicknames", out value)) network.Nicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
				else if (section2.Value.TryGetValue("Nickname", out value)) network.Nicknames = new[] { value };

				if (section2.Value.TryGetValue("Username", out value)) network.Ident = value;
				if (section2.Value.TryGetValue("FullName", out value)) network.FullName = value;
				if (section2.Value.TryGetValue("Autojoin", out value)) {
					foreach (var channel in value.Split(new[] { ',', ' ' }, RemoveEmptyEntries))
						network.AutoJoin.Add(new AutoJoinChannel(channel));
				}
				if (section2.Value.TryGetValue("SSL", out value)) network.Tls = Bot.ParseBoolean(value) ? TlsMode.Tls : TlsMode.StartTlsOptional;
				if (section2.Value.TryGetValue("AllowInvalidCertificate", out value)) network.AcceptInvalidTlsCertificate = Bot.ParseBoolean(value);
				if (section2.Value.TryGetValue("SASL-Username", out value)) network.SaslUsername = value;
				if (section2.Value.TryGetValue("SASL-Password", out value)) network.SaslPassword = value;

				if (section2.Value.TryGetValue("NickServ-Nicknames", out var servicesNicknamesString) &&
					section2.Value.TryGetValue("NickServ-Password", out var servicesPassword)) {
					var registration = new NickServSettings(servicesNicknamesString.Split(new char[] { ',', ' ' }, RemoveEmptyEntries), servicesPassword,
						section2.Value.TryGetValue("NickServ-AnyNickname", out value) && Bot.ParseBoolean(value),
						section2.Value.TryGetValue("NickServ-UseGhostCommand", out value) && Bot.ParseBoolean(value));
					if (section2.Value.TryGetValue("NickServ-GhostCommand", out value)) registration.GhostCommand = value;
					if (section2.Value.TryGetValue("NickServ-IdentifyCommand", out value)) registration.IdentifyCommand = value;
					if (section2.Value.TryGetValue("NickServ-Hostmask", out value)) registration.Hostmask = value;
					if (section2.Value.TryGetValue("NickServ-RequestMask", out value)) registration.RequestMask = value;
					network.NickServ = registration;
				}

				config.Networks.Add(JObject.FromObject(network));
			}
		}
	}

	public static void LoadPlugins(Bot bot) {
		if (!File.Exists("CBotPlugins.ini")) return;

		bot.NewPlugins = new Dictionary<string, PluginEntry>();
		var file = IniFile.FromFile("CBotPlugins.ini");

		foreach (var section in file) {
			var channels = section.Value.TryGetValue("Channels", out var value)
				? value.Split(new[] { ',', ' ' }, RemoveEmptyEntries)
				: Array.Empty<string>();
			var entry = new PluginEntry(section.Key, section.Value["Filename"], channels);

			bot.NewPlugins.Add(section.Key, entry);
		}
	}

	public static void LoadUsers(Bot bot) {
		// This is not a strict INI file, so the IniFile class cannot be used here.
		bot.commandCallbackNeeded = false;
		bot.Accounts.Clear();
		if (File.Exists("CBotUsers.ini")) {
			try {
				using var Reader = new StreamReader("CBotUsers.ini"); string? Section = null;
				Account? newUser = null;

				while (true) {
					var s = Reader.ReadLine();
					if (s is null) break;
					if (Regex.IsMatch(s, @"^(?>\s*);")) continue;  // Comment check

					var Match = Regex.Match(s, @"^\s*\[(?<Section>.*?)\]?\s*$");
					if (Match.Success) {
						if (Section is not null && newUser is not null) bot.Accounts.Add(Section, newUser);
						Section = Match.Groups["Section"].Value;
						if (!bot.Accounts.ContainsKey(Section)) {
							newUser = new(Array.Empty<string>());
							if (Section.StartsWith("$a")) bot.commandCallbackNeeded = true;
						} else newUser = null;
					} else {
						if (newUser is not null) {
							Match = Regex.Match(s, @"^\s*((?>[^=]*))=(.*)$");
							if (Match.Success) {
								string Field = Match.Groups[1].Value;
								string Value = Match.Groups[2].Value;
								switch (Field.ToUpper()) {
									case "PASSWORD":
									case "PASS":
										newUser.Password = Value;
										if (newUser.HashType == HashType.None && newUser.Password != null)
											// Old format
											newUser.HashType = newUser.Password.Length == 128 ? HashType.SHA256Salted : HashType.PlainText;
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

				if (Section is not null && newUser is not null) bot.Accounts.Add(Section, newUser);
			} catch (Exception ex) {
				ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve user data from the file: $k04" + ex.Message + "%r");
			}
		}
	}
}
