using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using static System.StringSplitOptions;

namespace CBot {
	public static class IniConfig {
		public static void LoadConfig(Bot bot, Config config) {
			if (File.Exists("CBotConfig.ini")) {
				var file = IniFile.FromFile("CBotConfig.ini");
				string value;

				if (file.TryGetValue("Me", out var section)) {
					if (section.TryGetValue("Nicknames", out value)) config.Nicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
					else if (section.TryGetValue("Nickname", out value)) config.Nicknames = new[] { value };

					if (section.TryGetValue("Username", out value)) config.Ident = value;
					if (section.TryGetValue("FullName", out value)) config.FullName = value;
					if (section.TryGetValue("UserInfo", out value)) config.UserInfo = value;
					section.TryGetValue("Avatar", out value); config.Avatar = value;  // That *can* be null.

					file.Remove("Me");
				}

				if (file.TryGetValue("Prefixes", out section)) {
					if (section.TryGetValue("Default", out value)) {
						config.CommandPrefixes = value.Split((char[]) null, RemoveEmptyEntries);
						section.Remove("Default");
					}

					foreach (var item in section)
						config.ChannelCommandPrefixes[item.Key] = item.Value.Split((char[]) null, RemoveEmptyEntries);

					file.Remove("Prefixes");
				}

				foreach (var section2 in file) {
					var network = new ClientEntry(section2.Key);

					if (section2.Value.TryGetValue("Address", out value)) {
						var fields = value.Split(new[] { ':' }, 2);
						if (fields.Length == 2) {
							network.Address = fields[0];
							if (fields[1].StartsWith("+")) {
								network.TLS = true;
								network.Port = int.Parse(fields[1][1..]);
							} else
								network.Port = int.Parse(fields[1]);
						} else {
							network.Address = fields[0];
						}
					}
					if (section2.Value.TryGetValue("Port", out value)) network.Port = int.Parse(value);
					if (section2.Value.TryGetValue("Password", out value)) network.Password = value;

					if (section2.Value.TryGetValue("Nicknames", out value)) network.Nicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
					else if (section2.Value.TryGetValue("Nickname", out value)) network.Nicknames = new[] { value };

					if (section2.Value.TryGetValue("Username", out value)) network.Ident = value;
					if (section2.Value.TryGetValue("FullName", out value)) network.FullName = value;
					if (section2.Value.TryGetValue("Autojoin", out value)) {
						foreach (var channel in value.Split(new[] { ',', ' ' }, RemoveEmptyEntries))
							network.AutoJoin.Add(new AutoJoinChannel(channel));
					}
					if (section2.Value.TryGetValue("SSL", out value)) network.TLS = Bot.ParseBoolean(value);
					if (section2.Value.TryGetValue("AllowInvalidCertificate", out value)) network.AcceptInvalidTlsCertificate = Bot.ParseBoolean(value);
					if (section2.Value.TryGetValue("SASL-Username", out value)) network.SaslUsername = value;
					if (section2.Value.TryGetValue("SASL-Password", out value)) network.SaslPassword = value;

					bool nickServ = false; var registration = new NickServSettings();
					if (section2.Value.TryGetValue("NickServ-Nicknames", out value)) { nickServ = true; registration.RegisteredNicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries); }
					if (section2.Value.TryGetValue("NickServ-Password", out value)) { nickServ = true; registration.Password = value; }
					if (section2.Value.TryGetValue("NickServ-AnyNickname", out value)) { nickServ = true; registration.AnyNickname = Bot.ParseBoolean(value); }
					if (section2.Value.TryGetValue("NickServ-UseGhostCommand", out value)) { nickServ = true; registration.UseGhostCommand = Bot.ParseBoolean(value); }
					if (section2.Value.TryGetValue("NickServ-GhostCommand", out value)) { nickServ = true; registration.GhostCommand = value; }
					if (section2.Value.TryGetValue("NickServ-IdentifyCommand", out value)) { nickServ = true; registration.IdentifyCommand = value; }
					if (section2.Value.TryGetValue("NickServ-Hostmask", out value)) { nickServ = true; registration.Hostmask = value; }
					if (section2.Value.TryGetValue("NickServ-RequestMask", out value)) { nickServ = true; registration.RequestMask = value; }
					if (nickServ) network.NickServ = registration;

					config.Networks.Add(network);
				}
			}
		}

		public static void LoadPlugins(Bot bot) {
			if (!File.Exists("CBotPlugins.ini")) return;

			bot.NewPlugins = new Dictionary<string, PluginEntry>();
			var file = IniFile.FromFile("CBotPlugins.ini");

			foreach (var section in file) {
				var channels = section.Value.TryGetValue("Channels", out string value)
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
					using var Reader = new StreamReader("CBotUsers.ini"); string Section = null;
					Account newUser = null;

					while (!Reader.EndOfStream) {
						string s = Reader.ReadLine();
						if (Regex.IsMatch(s, @"^(?>\s*);")) continue;  // Comment check

						var Match = Regex.Match(s, @"^\s*\[(?<Section>.*?)\]?\s*$");
						if (Match.Success) {
							if (Section != null) bot.Accounts.Add(Section, newUser);
							Section = Match.Groups["Section"].Value;
							if (!bot.Accounts.ContainsKey(Section)) {
								newUser = new Account { Permissions = Array.Empty<string>() };
								if (Section.StartsWith("$a")) bot.commandCallbackNeeded = true;
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

					bot.Accounts.Add(Section, newUser);
				} catch (Exception ex) {
					ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve user data from the file: $k04" + ex.Message + "%r");
				}
			}
		}
	}
}
