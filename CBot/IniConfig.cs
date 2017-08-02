using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using static System.StringSplitOptions;

namespace CBot {
    public class IniConfig {
        public static void LoadConfig() {
            if (File.Exists("CBotConfig.ini")) {
                var file = IniFile.FromFile("CBotConfig.ini");
                Dictionary<string, string> section; string value;

                if (file.TryGetValue("Me", out section)) {
                    if (section.TryGetValue("Nicknames", out value)) Bot.DefaultNicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
                    else if (section.TryGetValue("Nickname", out value)) Bot.DefaultNicknames = new[] { value };

                    if (section.TryGetValue("Username", out value)) Bot.DefaultIdent = value;
                    if (section.TryGetValue("FullName", out value)) Bot.DefaultFullName = value;
                    if (section.TryGetValue("UserInfo", out value)) Bot.DefaultUserInfo = value;
                    section.TryGetValue("Avatar", out value); Bot.DefaultAvatar = value;  // That *can* be null.

                    file.Remove("Me");
                }

                if (file.TryGetValue("Prefixes", out section)) {
                    if (section.TryGetValue("Default", out value)) {
                        Bot.DefaultCommandPrefixes = value.Split((char[]) null, RemoveEmptyEntries);
                        section.Remove("Default");
                    }

                    foreach (var item in section)
                        Bot.ChannelCommandPrefixes[item.Key] = item.Value.Split((char[]) null, RemoveEmptyEntries);

                    file.Remove("Prefixes");
                }

                Bot.NewClients = new List<ClientEntry>();
                foreach (var network in file) {
                    ClientEntry entry = new ClientEntry(network.Key) { SaveToConfig = true };
                    Bot.NewClients.Add(entry);

                    if (network.Value.TryGetValue("Address", out value)) {
                        var fields = value.Split(new[] { ':' }, 2);
                        if (fields.Length == 2) {
                            entry.Address = fields[0];
                            if (fields[1].StartsWith("+")) {
                                entry.TLS = true;
                                entry.Port = int.Parse(fields[1].Substring(1));
                            } else
                                entry.Port = int.Parse(fields[1]);
                        } else {
                            entry.Address = fields[0];
                        }
                    }
                    if (network.Value.TryGetValue("Port", out value)) entry.Port = int.Parse(value);
                    if (network.Value.TryGetValue("Password", out value)) entry.Password = value;

                    if (network.Value.TryGetValue("Nicknames", out value)) entry.Nicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries);
                    else if (network.Value.TryGetValue("Nickname", out value)) entry.Nicknames = new[] { value };

                    if (network.Value.TryGetValue("Username", out value)) entry.Ident = value;
                    if (network.Value.TryGetValue("FullName", out value)) entry.FullName = value;
                    if (network.Value.TryGetValue("Autojoin", out value)) {
                        foreach (var channel in value.Split(new[] { ',', ' ' }, RemoveEmptyEntries))
                            entry.AutoJoin.Add(new AutoJoinChannel(channel));
                    }
                    if (network.Value.TryGetValue("SSL", out value)) entry.TLS = Bot.ParseBoolean(value);
                    if (network.Value.TryGetValue("AllowInvalidCertificate", out value)) entry.AcceptInvalidTlsCertificate = Bot.ParseBoolean(value);
                    if (network.Value.TryGetValue("SASL-Username", out value)) entry.SaslUsername = value;
                    if (network.Value.TryGetValue("SASL-Password", out value)) entry.SaslPassword = value;

                    bool nickServ = false; var registration = new NickServSettings();
                    if (network.Value.TryGetValue("NickServ-Nicknames", out value)) { nickServ = true; registration.RegisteredNicknames = value.Split(new char[] { ',', ' ' }, RemoveEmptyEntries); }
                    if (network.Value.TryGetValue("NickServ-Password", out value)) { nickServ = true; registration.Password = value; }
                    if (network.Value.TryGetValue("NickServ-AnyNickname", out value)) { nickServ = true; registration.AnyNickname = Bot.ParseBoolean(value); }
                    if (network.Value.TryGetValue("NickServ-UseGhostCommand", out value)) { nickServ = true; registration.UseGhostCommand = Bot.ParseBoolean(value); }
                    if (network.Value.TryGetValue("NickServ-GhostCommand", out value)) { nickServ = true; registration.GhostCommand = value; }
                    if (network.Value.TryGetValue("NickServ-IdentifyCommand", out value)) { nickServ = true; registration.IdentifyCommand = value; }
                    if (network.Value.TryGetValue("NickServ-Hostmask", out value)) { nickServ = true; registration.Hostmask = value; }
                    if (network.Value.TryGetValue("NickServ-RequestMask", out value)) { nickServ = true; registration.RequestMask = value; }
                    if (nickServ) entry.NickServ = registration;
                }
            }
        }

        public static void LoadPlugins() {
            if (!File.Exists("CBotPlugins.ini")) return;

            Bot.NewPlugins = new List<PluginEntry>();
            var file = IniFile.FromFile("CBotPlugins.ini");
            string value;

            foreach (var section in file) {
                var entry = new PluginEntry() { Key = section.Key };
                if (section.Value.TryGetValue("Filename", out value)) entry.Filename = value;
                if (section.Value.TryGetValue("Channels", out value)) entry.Channels = value.Split(new[] { ',', ' ' }, RemoveEmptyEntries);
                Bot.NewPlugins.Add(entry);
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
