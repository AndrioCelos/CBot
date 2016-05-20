using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Timers;

using CBot;
using IRC;

namespace Gender {
    [ApiVersion(3, 3)]
    public class GenderPlugin : Plugin {
        public Dictionary<string, IRC.Gender> Gender;
        public int CheckingConnection;
        public int CheckingChannel;
        public Timer WhoTimer { get; }

        public override string Name => "Gender manager";

        public GenderPlugin(string key) {
            this.Gender = new Dictionary<string, IRC.Gender>(StringComparer.OrdinalIgnoreCase);
            this.CheckingChannel = -1;
            this.CheckingConnection = 1;
            this.WhoTimer = new Timer(90e+3);
            this.WhoTimer.Elapsed += WhoTimer_Elapsed;
            this.WhoTimer.Start();

            this.LoadConfig(key);
            this.LoadData(key + "-data.ini");
        }

        public override void OnSave() {
            this.SaveConfig();
            this.SaveData(this.Key + "-data.ini");
        }

        public override void OnUnload() {
            this.OnSave();
            this.Gender = null;
        }

        public void LoadConfig(string key) {
            string filename = Path.Combine("Config", key + ".ini");
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
                        int value2;

                        switch (field.ToUpper()) {
                            case "WHOINTERVAL":
                                if (int.TryParse(value, out value2) && value2 >= 0) {
                                    if (value2 == 0) {
                                        this.WhoTimer.Stop();
                                    } else {
                                        this.WhoTimer.Interval = value2 * 1000;
                                        this.WhoTimer.Start();
                                    }
                                } else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", key, lineNumber);
                                break;
                            default:
                                if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                break;
                        }
                        break;
                    }
                }
                reader.Close();
            }
        }

        public void LoadData(string filename) {
            if (File.Exists(filename)) {
                using (StreamReader reader = new StreamReader(filename)) {
                    int lineNumber = 0;

                    while (!reader.EndOfStream) {
                        string line = reader.ReadLine();
                        ++lineNumber;
                        if (line.Length == 0) continue;
                        string[] fields = line.Split(new char[] { '=' }, 2);

                        if (fields.Length != 2) {
                            ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the line is not in the correct format.", this.Key, lineNumber);
                        } else {
                            IRC.Gender gender;
                            if (fields[1].Equals("male", StringComparison.InvariantCultureIgnoreCase))
                                gender = IRC.Gender.Male;
                            else if (fields[1].Equals("female", StringComparison.InvariantCultureIgnoreCase))
                                gender = IRC.Gender.Female;
                            else if (fields[1].Equals("bot", StringComparison.InvariantCultureIgnoreCase) || fields[1].Equals("none", StringComparison.InvariantCultureIgnoreCase))
                                gender = IRC.Gender.Bot;
                            else
                                gender = IRC.Gender.Unspecified;
                            this.Gender.Add(fields[0], gender);
                        }
                    }
                    reader.Close();
                }
            }
        }

        public void SaveConfig() {
            if (!Directory.Exists("Config"))
                Directory.CreateDirectory("Config");
            using (StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false)) {
                writer.WriteLine("[Config]");
                writer.WriteLine("WhoInterval={0}", this.WhoTimer.Enabled ? this.WhoTimer.Interval / 1000 : 0);
                writer.Close();
            }
        }

        public void SaveData(string filename) {
            using (StreamWriter writer = new StreamWriter(filename)) {
                foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                    if (entry.Value == IRC.Gender.Male)
                        writer.WriteLine(entry.Key + "=Male");
                    else if (entry.Value == IRC.Gender.Female)
                        writer.WriteLine(entry.Key + "=Female");
                    else if (entry.Value == IRC.Gender.Bot)
                        writer.WriteLine(entry.Key + "=None");
                }
                writer.Close();
            }
        }

        [Command(new string[] { "setgender", "ircsetgender" }, 1, 2, "setgender [<hostmask>|=<nickname>] male|female|none|clear", "Sets a gender for the given user.",
            ".setgender")]
        public void CommandSetGender(object sender, CommandEventArgs e) {
            IRC.Gender gender = IRC.Gender.Unspecified;
            bool valid = false;
            string mask = null;

            if (e.Parameters.Length == 1) {
                valid = true;
                mask = "*!*" + (e.Sender.Ident.StartsWith("~") ? e.Sender.Ident.Substring(1) : e.Sender.Ident) + "@" + e.Sender.Host;
                if (e.Parameters[0].Equals("male", StringComparison.InvariantCultureIgnoreCase) || e.Parameters[00].Equals("m", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Male;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002male\u0002.", mask);
                } else if (e.Parameters[0].Equals("female", StringComparison.InvariantCultureIgnoreCase) || e.Parameters[0].Equals("f", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Female;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002female\u0002.", mask);
                } else if (e.Parameters[0].Equals("none", StringComparison.InvariantCultureIgnoreCase) || e.Parameters[0].Equals("n", StringComparison.InvariantCultureIgnoreCase) ||
                    e.Parameters[0].Equals("bot", StringComparison.InvariantCultureIgnoreCase) || e.Parameters[0].Equals("b", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Bot;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002none\u0002.", mask);
                } else if (e.Parameters[0].Equals("clear", StringComparison.InvariantCultureIgnoreCase) || e.Parameters[0].Equals("c", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Unspecified;
                } else
                    valid = false;
            }
            if (valid) {
                if (gender == IRC.Gender.Unspecified) {
                    if (this.Gender.Remove(mask))
                        e.Reply("Gender for \u0002{0}\u0002 has been cleared.", mask);
                    else {
                        e.Whisper("No gender for \u0002{0}\u0002 was set.", mask);
                        return;
                    }
                } else
                    this.Gender[mask] = gender;
                e.Sender.Gender = gender;
                return;
            } else if (Bot.UserHasPermission(e.Sender, this.Key + ".setgender.others")) {
                if (e.Parameters.Length == 1 || e.Parameters[1].Equals("clear", StringComparison.InvariantCultureIgnoreCase) ||
                                                e.Parameters[1].Equals("c", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Unspecified;
                    if (this.Gender.Remove(e.Parameters[0]))
                        e.Reply("Gender for \u0002{0}\u0002 has been cleared.", e.Parameters[0]);
                    else {
                        e.Whisper("No gender for \u0002{0}\u0002 was set.", e.Parameters[0]);
                        return;
                    }
                } else if (e.Parameters[1].Equals("male", StringComparison.InvariantCultureIgnoreCase) ||
                           e.Parameters[1].Equals("m", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Male;
                    this.Gender[e.Parameters[0]] = IRC.Gender.Male;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002male\u0002.", e.Parameters[0]);
                } else if (e.Parameters[1].Equals("female", StringComparison.InvariantCultureIgnoreCase) ||
                           e.Parameters[1].Equals("f", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Female;
                    this.Gender[e.Parameters[0]] = IRC.Gender.Female;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002female\u0002.", e.Parameters[0]);
                } else if (e.Parameters[1].Equals("none", StringComparison.InvariantCultureIgnoreCase) ||
                           e.Parameters[1].Equals("n", StringComparison.InvariantCultureIgnoreCase) ||
                           e.Parameters[1].Equals("bot", StringComparison.InvariantCultureIgnoreCase) ||
                           e.Parameters[1].Equals("b", StringComparison.InvariantCultureIgnoreCase)) {
                    gender = IRC.Gender.Bot;
                    this.Gender[e.Parameters[0]] = IRC.Gender.Bot;
                    e.Reply("Gender for \u0002{0}\u0002 has been set to \u0002none\u0002.", e.Parameters[0]);
                } else {
                    e.Whisper("'{0}' isn't a valid option. Use 'male', 'female', 'none' or 'clear'.", e.Parameters[1]);
                    return;
                }
            } else {
                e.Whisper("You don't have permission to set a gender for others.");
                return;
            }

            string[] fields = e.Parameters[0].Split(new char[] { '/' }, 2);
            if (fields.Length == 1)
                fields = new string[] { "*", fields[0] };

            int index = fields[1].IndexOf('!');
            int index2 = index == -1 ? 0 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
            if (index2 == -1) {
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (fields[0] == "*" || fields[0].Equals(client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(client.Address, StringComparison.OrdinalIgnoreCase)) {
                        IrcUser user;
                        if (client.Users.TryGetValue(fields[1].Substring(0, index), out user)) {
                            if (Bot.MaskCheck(user.ToString(), fields[1]))
                                user.Gender = gender;
                        }
                    }
                }
            } else {
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (fields[0] == "*" || fields[0].Equals(client.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(client.Address, StringComparison.OrdinalIgnoreCase)) {
                        foreach (IrcUser user in client.Users) {
                            if (Bot.MaskCheck(user.ToString(), fields[1]))
                                user.Gender = gender;
                        }
                    }
                }
            }
        }

        [Command(new string[] { "getgender", "ircgetgender" }, 1, 1, "getgender <hostmask>|=<nickname>", "Returns the gender of the given user.",
            ".getgender")]
        public void CommandGetGender(object sender, CommandEventArgs e) {
            IRC.Gender gender; string header = null;

            if (this.Gender.TryGetValue(e.Parameters[0], out gender)) {
                header = e.Parameters[0];
            } else {
                string[] fields = e.Parameters[0].Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };

                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? 0 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (index2 == -1) {
                    foreach (ClientEntry clientEntry in Bot.Clients) {
                        IrcClient client = clientEntry.Client;
                        if (fields[0] == "*" || fields[0].Equals(client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(client.Address, StringComparison.OrdinalIgnoreCase)) {
                            IrcUser user;
                            if (client.Users.TryGetValue(fields[1].Substring(0, index), out user)) {
                                if (Bot.MaskCheck(user.ToString(), fields[1])) {
                                    gender = user.Gender;
                                    header = user.Nickname + "\u0002 on \u0002" + client.Extensions.NetworkName;
                                    break;
                                }
                            }
                        }
                    }
                } else {
                    foreach (ClientEntry clientEntry in Bot.Clients) {
                        IrcClient client = clientEntry.Client;
                        if (fields[0] == "*" || fields[0].Equals(client.Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(client.Address, StringComparison.OrdinalIgnoreCase)) {
                            foreach (IrcUser user in client.Users) {
                                if (Bot.MaskCheck(user.ToString(), fields[1])) {
                                    gender = user.Gender;
                                    header = user.Nickname + "\u0002 on \u0002" + client.Extensions.NetworkName;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (header == null) {
                    e.Whisper("I didn't find any matching users.");
                    return;
                }
            }
            if (gender == IRC.Gender.Male)
                e.Reply("\u0002{0}\u0002 is \u0002male\u0002.", header);
            else if (gender == IRC.Gender.Female)
                e.Reply("\u0002{0}\u0002 is \u0002female\u0002.", header);
            else if (gender == IRC.Gender.Bot)
                e.Reply("\u0002{0}\u0002 is \u0002none\u0002.", header);
            else
                e.Reply("\u0002{0}\u0002 is \u0002unknown\u0002.", header);
        }

        [Command(new string[] { "set" }, 1, 2, "set [setting] [value]", "Changes settings for this plugin.",
            ".set")]
        public void CommandSet(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 1) {
                switch (e.Parameters[0].ToUpperInvariant()) {
                    case "WHO":
                    case "WHOINTERVAL":
                        if (this.WhoTimer.Enabled)
                            e.Reply("WHO requests \u00039are\u000F being sent every \u0002{0}\u0002 seconds.", this.WhoTimer.Interval / 1000);
                        else
                            e.Reply("WHO requests \u00034are not\u000F being sent.", 0);
                        break;
                    default:
                        e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[1]));
                        break;
                }
            } else {
                switch (e.Parameters[0].ToUpperInvariant()) {
                    case "WHO":
                    case "WHOINTERVAL":
                        int value;
                        if (int.TryParse(e.Parameters[1], out value)) {
                            if (value == 0) {
                                this.WhoTimer.Stop();
                                e.Reply("WHO requests will \u00034no longer\u000F be sent.", 0);
                            } else if (value >= 5) {
                                this.WhoTimer.Interval = value * 1000;
                                this.WhoTimer.Start();
                                e.Reply("WHO requests will \u00039now\u000F be sent every \u0002{0}\u0002 seconds.", value);
                            } else if (value > 0)
                                e.Whisper("That number is too small.", value);
                            else
                                e.Whisper("The number cannot be negative.", value);
                        } else
                            e.Whisper(string.Format("That's not a valid integer.", e.Parameters[1]));
                        break;
                    default:
                        e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[1]));
                        break;
                }
            }
        }

        public override bool OnChannelJoin(object sender, ChannelJoinEventArgs e) {
            foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                string[] fields = entry.Key.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };
                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? -1 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (fields[0] == "*" || fields[0].Equals(((IrcClient) sender).Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(((IrcClient) sender).Address, StringComparison.OrdinalIgnoreCase)) {
                    if (Bot.MaskCheck(e.Sender.ToString(), fields[1])) {
                        e.Sender.Gender = entry.Value;
                        return base.OnChannelJoin(sender, e);
                    }
                }
            }
            return base.OnChannelJoin(sender, e);
        }

        public override bool OnWhoList(object sender, WhoListEventArgs e) {
            foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                string[] fields = entry.Key.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };
                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? -1 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (fields[0] == "*" || fields[0].Equals(((IrcClient) sender).Extensions.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(((IrcClient) sender).Address, StringComparison.OrdinalIgnoreCase)) {
                    if (Bot.MaskCheck(e.Nickname + "!" + e.Username + "@" + e.Host, fields[1])) {
                        ((IrcClient) sender).Users[e.Nickname].Gender = entry.Value;
                        return base.OnWhoList(sender, e);
                    }
                }
            }
            return base.OnWhoList(sender, e);
        }

        private void WhoTimer_Elapsed(object sender, ElapsedEventArgs e) {
            // Find the next channel.
            if (this.CheckingConnection >= Bot.Clients.Count) {
                this.CheckingConnection = 1;
                this.CheckingChannel = -1;
                if (this.CheckingConnection >= Bot.Clients.Count)
                    return;
            }

            int startingConnection = this.CheckingConnection; bool looped = false;

            ++this.CheckingChannel;
            if (this.CheckingChannel >= Bot.Clients[this.CheckingConnection].Client.Channels.Count) {
                do {
                    ++this.CheckingConnection;
                    this.CheckingChannel = 0;
                    if (looped && this.CheckingConnection == startingConnection) return;
                    if (this.CheckingConnection >= Bot.Clients.Count) {
                        this.CheckingConnection = 1;
                        this.CheckingChannel = 0;
                        looped = true;
                        if (this.CheckingConnection >= Bot.Clients.Count)
                            return;
                    }
                } while (this.CheckingChannel >= Bot.Clients[this.CheckingConnection].Client.Channels.Count);
            }

            // Send the WHO request.
            // TODO: use enumerators?
            Bot.Clients[this.CheckingConnection].Client.Send("WHO {0}", Bot.Clients[this.CheckingConnection].Client.Channels.ElementAt(this.CheckingChannel).Name);
        }


    }
}
