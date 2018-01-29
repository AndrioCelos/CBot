using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using static System.StringSplitOptions;

namespace CBot {
    public class IniConfig {
		public static void LoadConfig(Config config) {
            if (File.Exists("CBotConfig.ini")) {
                var file = IniFile.FromFile("CBotConfig.ini");
                Dictionary<string, string> section; string value;

                if (file.TryGetValue("Me", out section)) {
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
								network.Port = int.Parse(fields[1].Substring(1));
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

		public static void LoadPlugins() {
			if (!File.Exists("CBotPlugins.ini")) return;

			Bot.NewPlugins = new Dictionary<string, PluginEntry>();
			var file = IniFile.FromFile("CBotPlugins.ini");
			string value;

			foreach (var section in file) {
				string[] channels;
				if (section.Value.TryGetValue("Channels", out value)) channels = value.Split(new[] { ',', ' ' }, RemoveEmptyEntries);
				else channels = new string[0];
				var entry = new PluginEntry(section.Key, section.Value["Filename"], channels);

				Bot.NewPlugins.Add(section.Key, entry);
			}
		}

		public static void LoadUsers() {
            // This is not a strict INI file, so the IniFile class cannot be used here.
            Bot.commandCallbackNeeded = false;
            Bot.Accounts.Clear();
            if (File.Exists("CBotUsers.ini")) {
                try {
                    using (var Reader = new StreamReader("CBotUsers.ini")) {
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
                                    if (Section.StartsWith("$a")) Bot.commandCallbackNeeded = true;
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
                                                    newUser.HashType = (newUser.Password.Length == 128 ? HashType.SHA256Salted : HashType.PlainText);
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

                        Bot.Accounts.Add(Section, newUser);
                    }
                } catch (Exception ex) {
                    ConsoleUtils.WriteLine("%cGRAY[%cREDERROR%cGRAY] %cWHITEI was unable to retrieve user data from the file: $k04" + ex.Message + "%r");
                }
            }
        }

        /// <summary>Writes configuration data to the file CBotConfig.ini.</summary>
		[Obsolete("The JSON configuration file format is preferred.")]
        public static void SaveConfig() {
            using (var writer = new StreamWriter("CBotConfig.ini", false)) {
                writer.WriteLine("[Me]");
                writer.WriteLine("Nicknames=" + string.Join(",", Bot.DefaultNicknames));
                writer.WriteLine("Username=" + Bot.DefaultIdent);
                writer.WriteLine("FullName=" + Bot.DefaultFullName);
                writer.WriteLine("UserInfo=" + Bot.DefaultUserInfo);
                if (Bot.DefaultAvatar != null)
                    writer.WriteLine("Avatar=" + Bot.DefaultAvatar);

                foreach (var network in Bot.Clients) {
                    if (network.SaveToConfig) {
                        writer.WriteLine();
                        writer.WriteLine("[" + network.Name + "]");
                        writer.WriteLine("Address=" + network.Address + ":" + network.Port);
                        if (network.Client.Password != null)
                            writer.WriteLine("Password=" + network.Password);
                        if (network.Nicknames != null)
                            writer.WriteLine("Nicknames=" + string.Join(",", network.Nicknames));
                        writer.WriteLine("Username=" + network.Ident);
                        writer.WriteLine("FullName=" + network.FullName);
                        if (network.AutoJoin.Count != 0)
                            writer.WriteLine("Autojoin=" + string.Join(",", network.AutoJoin.Select(c => c.Channel)));
                        writer.WriteLine("SSL=" + (network.TLS ? "Yes" : "No"));
                        if (network.SaslUsername != null && network.SaslPassword != null) {
                            writer.WriteLine("SASL-Username=" + network.SaslUsername);
                            writer.WriteLine("SASL-Password=" + network.SaslPassword);
                        }
                        writer.WriteLine("AllowInvalidCertificate=" + (network.AcceptInvalidTlsCertificate ? "Yes" : "No"));
                        if (network.NickServ != null) {
                            writer.WriteLine("NickServ-Nicknames=" + string.Join(",", network.NickServ.RegisteredNicknames));
                            writer.WriteLine("NickServ-Password=" + network.NickServ.Password);
                            writer.WriteLine("NickServ-AnyNickname=" + (network.NickServ.AnyNickname ? "Yes" : "No"));
                            writer.WriteLine("NickServ-UseGhostCommand=" + (network.NickServ.UseGhostCommand ? "Yes" : "No"));
                            writer.WriteLine("NickServ-GhostCommand=" + network.NickServ.GhostCommand);
                            writer.WriteLine("NickServ-IdentifyCommand=" + network.NickServ.IdentifyCommand);
                            writer.WriteLine("NickServ-Hostmask=" + network.NickServ.Hostmask);
                            writer.WriteLine("NickServ-RequestMask=" + network.NickServ.RequestMask);
                        }
                    }
                }
                writer.WriteLine();
                writer.WriteLine("[Prefixes]");
                writer.WriteLine("Default=" + string.Join(" ", Bot.DefaultCommandPrefixes));
                foreach (var network in Bot.ChannelCommandPrefixes)
                    writer.WriteLine(network.Key + "=" + string.Join(" ", network.Value));
            }
        }

		/// <summary>Writes user data to the file CBotUsers.ini.</summary>
		[Obsolete("The JSON configuration file format is preferred.")]
		public static void SaveUsers() {
            using (var writer = new StreamWriter("CBotUsers.ini", false)) {
                foreach (var user in Bot.Accounts) {
                    writer.WriteLine("[" + user.Key + "]");
                    if (user.Value.HashType != HashType.None) {
                        writer.WriteLine("HashType=" + user.Value.HashType);
                        writer.WriteLine("Password=" + user.Value.Password);
                    }
                    string[] permissions = user.Value.Permissions;
                    for (int i = 0; i < permissions.Length; ++i) {
                        string Permission = permissions[i];
                        writer.WriteLine(Permission);
                    }
                    writer.WriteLine();
                }
            }
        }

		/// <summary>Writes active plugin data to the file CBotPlugins.ini.</summary>
		[Obsolete("The JSON configuration file format is preferred.")]
		public static void SavePlugins() {
            using (var writer = new StreamWriter("CBotPlugins.ini", false)) {
                foreach (var plugin in Bot.Plugins) {
                    writer.WriteLine("[" + plugin.Key + "]");
                    writer.WriteLine("Filename=" + plugin.Filename);
                    bool flag = plugin.Obj.Channels != null;
                    if (flag) {
                        writer.WriteLine("Channels=" + string.Join(",", plugin.Obj.Channels));
                    }
                    writer.WriteLine();
                }
            }
        }
    }
}
