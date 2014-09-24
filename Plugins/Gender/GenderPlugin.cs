using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

using CBot;
using IRC;

namespace Gender
{
    [APIVersion(3, 0)]
    public class GenderPlugin : Plugin
    {
        public Dictionary<string, IRC.Gender> Gender;
        public int CheckingConnection;
        public int CheckingChannel;
        public Timer WhoTimer { get; private set; }

        public override string Name {
            get {
                return "Gender manager";
            }
        }

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
            this.SaveData(MyKey + "-data.ini");
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
                                if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", MyKey, lineNumber);
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
                        string[] fields = reader.ReadLine().Split(new char[] { '=' }, 2);
                        ++lineNumber;

                        if (fields.Length != 2) {
                            ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the line is not in the correct format.", MyKey, lineNumber);
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
            using (StreamWriter writer = new StreamWriter(Path.Combine("Config", MyKey + ".ini"), false)) {
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

        [Command(new string[] { "setgender", "ircsetgender" }, 1, 2, "setgender <hostmask>|=<nickname> male|female|none|clear", "Sets a gender for the given user.",
            ".setgender")]
        public void CommandSetGender(object sender, CommandEventArgs e) {
            IRC.Gender gender;

            if (e.Parameters.Length == 1 || e.Parameters[1].Equals("clear", StringComparison.InvariantCultureIgnoreCase) ||
                                            e.Parameters[1].Equals("c", StringComparison.InvariantCultureIgnoreCase)) {
                gender = IRC.Gender.Unspecified;
                if (this.Gender.Remove(e.Parameters[0]))
                    Bot.Say(e.Connection, e.Channel, "Gender for \u0002{0}\u0002 has been cleared.", e.Parameters[0]);
                else {
                    Bot.Say(e.Connection, e.Sender.Nickname, "No gender for \u0002{0}\u0002 was set.", e.Parameters[0]);
                    return;
                }
            } else if (e.Parameters[1].Equals("male", StringComparison.InvariantCultureIgnoreCase) ||
                       e.Parameters[1].Equals("m", StringComparison.InvariantCultureIgnoreCase)) {
                gender = IRC.Gender.Male;
                this.Gender[e.Parameters[0]] = IRC.Gender.Male;
                Bot.Say(e.Connection, e.Channel, "Gender for \u0002{0}\u0002 has been set to \u0002male\u0002.", e.Parameters[0]);
            } else if (e.Parameters[1].Equals("female", StringComparison.InvariantCultureIgnoreCase) ||
                       e.Parameters[1].Equals("f", StringComparison.InvariantCultureIgnoreCase)) {
                gender = IRC.Gender.Female;
                this.Gender[e.Parameters[0]] = IRC.Gender.Female;
                Bot.Say(e.Connection, e.Channel, "Gender for \u0002{0}\u0002 has been set to \u0002female\u0002.", e.Parameters[0]);
            } else if (e.Parameters[1].Equals("none", StringComparison.InvariantCultureIgnoreCase) ||
                       e.Parameters[1].Equals("n", StringComparison.InvariantCultureIgnoreCase) ||
                       e.Parameters[1].Equals("bot", StringComparison.InvariantCultureIgnoreCase) ||
                       e.Parameters[1].Equals("b", StringComparison.InvariantCultureIgnoreCase)) {
                gender = IRC.Gender.Bot;
                this.Gender[e.Parameters[0]] = IRC.Gender.Bot;
                Bot.Say(e.Connection, e.Channel, "Gender for \u0002{0}\u0002 has been set to \u0002none\u0002.", e.Parameters[0]);
            } else {
                Bot.Say(e.Connection, e.Sender.Nickname, "'{0}' isn't a valid option. Use 'male', 'female', 'none' or 'clear'.", e.Parameters[1]);
                return;
            }

            string[] fields = e.Parameters[0].Split(new char[] { '/' }, 2);
            if (fields.Length == 1)
                fields = new string[] { "*", fields[0] };

            int index = fields[1].IndexOf('!');
            int index2 = index == -1 ? 0 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
            if (index2 == -1) {
                foreach (IRCClient connection in Bot.Connections) {
                    if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                        User user;
                        if (connection.Users.TryGetValue(fields[1].Substring(0, index), out user)) {
                            if (Bot.MaskCheck(user.ToString(), fields[1]))
                                user.Gender = gender;
                        }
                    }
                }
            } else {
                foreach (IRCClient connection in Bot.Connections) {
                    if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                        foreach (User user in connection.Users) {
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
                    foreach (IRCClient connection in Bot.Connections) {
                        if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                            User user;
                            if (connection.Users.TryGetValue(fields[1].Substring(0, index), out user)) {
                                if (Bot.MaskCheck(user.ToString(), fields[1])) {
                                    gender = user.Gender;
                                    header = user.Nickname + "\u0002 on \u0002" + connection.NetworkName;
                                    break;
                                }
                            }
                        }
                    }
                } else {
                    foreach (IRCClient connection in Bot.Connections) {
                        if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                            foreach (User user in connection.Users) {
                                if (Bot.MaskCheck(user.ToString(), fields[1])) {
                                    gender = user.Gender;
                                    header = user.Nickname + "\u0002 on \u0002" + connection.NetworkName;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (header == null) {
                    Bot.Say(e.Connection, e.Sender.Nickname, "I didn't find any matching users.");
                    return;
                }
            }
            if (gender == IRC.Gender.Male)
                Bot.Say(e.Connection, e.Channel, "\u0002{0}\u0002 is \u0002male\u0002.", header);
            else if (gender == IRC.Gender.Female)
                Bot.Say(e.Connection, e.Channel, "\u0002{0}\u0002 is \u0002female\u0002.", header);
            else if (gender == IRC.Gender.Bot)
                Bot.Say(e.Connection, e.Channel, "\u0002{0}\u0002 is \u0002none\u0002.", header);
            else
                Bot.Say(e.Connection, e.Channel, "\u0002{0}\u0002 is \u0002unknown\u0002.", header);
        }

        [Command(new string[] { "set" }, 1, 2, "set [setting] [value]", "Changes settings for this plugin.",
            ".set")]
        public void CommandSet(object sender, CommandEventArgs e) {
            if (e.Parameters.Length == 1) {
                switch (e.Parameters[0].ToUpperInvariant()) {
                    case "WHO":
                    case "WHOINTERVAL":
                        if (this.WhoTimer.Enabled)
                            Bot.Say(e.Connection, e.Channel, "WHO requests \u00039are\u000F being sent every \u0002{0}\u0002 seconds.", this.WhoTimer.Interval / 1000);
                        else
                            Bot.Say(e.Connection, e.Channel, "WHO requests \u00034are not\u000F being sent.", 0);
                        break;
                    default:
                        this.Say(e.Connection, e.Sender.Nickname, string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[1]));
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
                                Bot.Say(e.Connection, e.Channel, "WHO requests will \u00034no longer\u000F be sent.", 0);
                            } else if (value >= 5) {
                                this.WhoTimer.Interval = value * 1000;
                                this.WhoTimer.Start();
                                Bot.Say(e.Connection, e.Channel, "WHO requests will \u00039now\u000F be sent every \u0002{0}\u0002 seconds.", value);
                            } else if (value > 0)
                                Bot.Say(e.Connection, e.Sender.Nickname, "That number is too small.", value);
                            else
                                Bot.Say(e.Connection, e.Sender.Nickname, "The number cannot be negative.", value);
                        } else
                            this.Say(e.Connection, e.Sender.Nickname, string.Format("That's not a valid integer.", e.Parameters[1]));
                        break;
                    default:
                        this.Say(e.Connection, e.Sender.Nickname, string.Format("I don't manage a setting named \u0002{0}\u0002.", e.Parameters[1]));
                        break;
                }
            }
        }


        public override void OnChannelJoin(IRCClient Connection, string Sender, string Channel) {
            base.OnChannelJoin(Connection, Sender, Channel);

            foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                string[] fields = entry.Key.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };
                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? -1 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (fields[0] == "*" || fields[0].Equals(Connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(Connection.Address, StringComparison.OrdinalIgnoreCase)) {
                    if (Bot.MaskCheck(Sender, fields[1])) {
                        Connection.Users[Sender.Split(new char[] { '!' }, 2)[0]].Gender = entry.Value;
                        return;
                    }
                }
            }
        }

        public override void OnChannelJoinSelf(IRCClient Connection, string Sender, string Channel) {
            base.OnChannelJoinSelf(Connection, Sender, Channel);

            foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                string[] fields = entry.Key.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };
                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? -1 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (fields[0] == "*" || fields[0].Equals(Connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(Connection.Address, StringComparison.OrdinalIgnoreCase)) {
                    if (Bot.MaskCheck(Sender, fields[1])) {
                        Connection.Users[Sender.Split(new char[] { '!' }, 2)[0]].Gender = entry.Value;
                        return;
                    }
                }
            }
        }

        public override void OnWhoList(IRCClient Connection, string Channel, string Username, string Address, string Server, string Nickname, string Flags, int Hops, string FullName) {
            base.OnWhoList(Connection, Channel, Username, Address, Server, Nickname, Flags, Hops, FullName);

            foreach (KeyValuePair<string, IRC.Gender> entry in this.Gender) {
                string[] fields = entry.Key.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };
                int index = fields[1].IndexOf('!');
                int index2 = index == -1 ? -1 : fields[1].IndexOfAny(new char[] { '*', '?' }, 0, index);
                if (fields[0] == "*" || fields[0].Equals(Connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(Connection.Address, StringComparison.OrdinalIgnoreCase)) {
                    if (Bot.MaskCheck(Nickname + "!" + Username + "@" + Address, fields[1])) {
                        Connection.Users[Nickname].Gender = entry.Value;
                        return;
                    }
                }
            }
        }

        void WhoTimer_Elapsed(object sender, ElapsedEventArgs e) {
            // Find the next channel.
            if (this.CheckingConnection >= Bot.Connections.Count) {
                this.CheckingConnection = 1;
                this.CheckingChannel = -1;
                if (this.CheckingConnection >= Bot.Connections.Count)
                    return;
            }

            int startingConnection = this.CheckingConnection; bool looped = false;

            ++this.CheckingChannel;
            if (this.CheckingChannel >= Bot.Connections[this.CheckingConnection].Channels.Count) {
                do {
                    ++this.CheckingConnection;
                    this.CheckingChannel = 0;
                    if (looped && this.CheckingConnection == startingConnection) return;
                    if (this.CheckingConnection >= Bot.Connections.Count) {
                        this.CheckingConnection = 1;
                        this.CheckingChannel = 0;
                        looped = true;
                        if (this.CheckingConnection >= Bot.Connections.Count)
                            return;
                    }
                } while (this.CheckingChannel >= Bot.Connections[this.CheckingConnection].Channels.Count);
            }

            // Send the WHO request.
            Bot.Connections[this.CheckingConnection].Send("WHO {0}", Bot.Connections[this.CheckingConnection].Channels[this.CheckingChannel].Name);
        }


    }
}
