using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IRC {
    /// <summary>
    /// Stores the list of RPL_ISUPPORT parameters advertised by an IRC server.
    /// For more information, see https://tools.ietf.org/html/draft-brocklesby-irc-isupport-03
    /// This class is implemented as a ReadOnlyDictionary&lt;string, string&gt;.
    /// </summary>
    public class Extensions : ReadOnlyDictionary<string, string> {
        /// <summary>The RPL_ISUPPORT specification of the case mapping this server uses to compare nicknames and channel names.</summary>
        /// <remarks>The value is case sensitive. There are three known values: ascii, rfc1459 (the default) and strict-rfc1459.</remarks>
        public string CaseMapping { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of each type of channel we may be on.</summary>
        /// <remarks>Each key contains one of more channel prefixes, and the corresponding value is the limit for all of those channel types combined.</remarks>
        public ReadOnlyDictionary<string, int> ChannelLimit { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the channel modes this server supports.</summary>
        /// <remarks>The value consists of four or more comma-separated categories, each containing zero or more mode characters. They are described in detail in http://www.irc.org/tech_docs/draft-brocklesby-irc-isupport-03.txt</remarks>
        public ChannelModes ChanModes { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a channel name.</summary>
        public int ChannelLength { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the channel types supported by this server.</summary>
        public ReadOnlyCollection<char> ChannelTypes { get; protected internal set; }
        /// <summary>True if the server supports channel ban exceptions.</summary>
        public bool SupportsBanExceptions { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the mode character used for channel ban exceptions.</summary>
        public char BanExceptionsMode { get; protected internal set; }
        /// <summary>True if the server supports channel invite exceptions.</summary>
        public bool SupportsInviteExceptions { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the mode character used for channel invite exceptions.</summary>
        public char InviteExceptionsMode { get; protected internal set; }
        /// <summary>True if the server supports the WATCH command.</summary>
        /// <remarks>If true, we will use the WATCH list to monitor users in the Users list.</remarks>
        public bool SupportsWatch { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a kick message.</summary>
        public int KickMessageLength { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of entries that may be added to a channel list mode.</summary>
        /// <remarks>Each key contains one of more mode characters, and the corresponding value is the limit for all of those modes combined.</remarks>
        public ReadOnlyDictionary<string, int> ListModeLength { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of modes that can be set with a single command.</summary>
        public int Modes { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the name of the IRC network.</summary>
        /// <remarks>Note that this is not known until, and unless, the RPL_ISUPPORT message is received.</remarks>
        public string NetworkName { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a nickname we may use.</summary>
        public int NicknameLength { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the channel status modes this server supports.</summary>
        /// <remarks>Each entry contains a prefix as the key, and the corresponding mode character as the value. They are given in order from highest to lowest status.</remarks>
        public ReadOnlyDictionary<char, char> StatusPrefix { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the status prefixes we may use to only talk to users on a channel with that status.</summary>
        /// <remarks>Note that many servers require we also have that status to do this.</remarks>
        public char[] StatusMessage { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of targets we may give for certain commands.</summary>
        /// <remarks>Each entry consists of the command and the corresponding limit. Any command that's not listed does not support multiple targets.</remarks>
        public ReadOnlyDictionary<string, int> MaxTargets { get; protected internal set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a channel topic.</summary>
        public int TopicLength { get; protected internal set; }

        private static Extensions _default = new Extensions();
        /// <summary>Returns an Extensions object with the default parameters.</summary>
        public static Extensions Default => _default;

        /// <summary>Returns true if all parameters have the default values.</summary>
        public bool IsDefault => this.Count == 0;

        /// <summary>Serves as the backing field for the ChannelLimit property.</summary>
        protected Dictionary<string, int> channelLimit = new Dictionary<string, int>() { { "#&", int.MaxValue } };
        /// <summary>Serves as the backing field for the ListModeLength property.</summary>
        protected Dictionary<string, int> listModeLength = new Dictionary<string, int>();
        /// <summary>Serves as the backing field for the StatusPrefix property.</summary>
        protected Dictionary<char, char> statusPrefix = new Dictionary<char, char>() { { '@', 'o' }, { '+', 'v' } };
        /// <summary>Serves as the backing field for the MaxTargets property.</summary>
        protected Dictionary<string, int> maxTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a new instance of the Extensions class with the default values.</summary>
        public Extensions() : this(null) { }
        /// <summary>Creates a new instance of the Extensions class with the default values and a specified network name.</summary>
        public Extensions(string networkName) : base(new Dictionary<string, string>()) {
            this.CaseMapping = "rfc1459";
            this.ChannelLimit = new ReadOnlyDictionary<string, int>(this.channelLimit);
            this.ChannelLength = 200;
            this.ChannelTypes = new ReadOnlyCollection<char>(new char[] { '#' });
            this.SupportsBanExceptions = false;
            this.SupportsInviteExceptions = false;
            this.KickMessageLength = int.MaxValue;
            this.ListModeLength = new ReadOnlyDictionary<string, int>(this.listModeLength);
            this.Modes = 3;
            this.NetworkName = networkName;
            this.NicknameLength = 9;
            this.StatusPrefix = new ReadOnlyDictionary<char, char>(this.statusPrefix);
            this.StatusMessage = new char[0];
            this.MaxTargets = new ReadOnlyDictionary<string, int>(this.maxTargets);
            this.TopicLength = int.MaxValue;
        }

        /// <summary>Gets or sets the value of a parameter. To unset a parameter, set its value to null.</summary>
        /// <param name="key">The name of the parameter to get or set. Case sensitive.</param>
        public new string this[string key] {
            get {
                return base[key];
            }
            protected internal set {
                string[] fields;

                if (value == null) {
                    // A value is being removed.
                    this.Dictionary.Remove(key);
                } else
                    this.Dictionary[key] = value;

                switch (key) {  // Parameter names are case sensitive.
                    case "CASEMAPPING":
                        this.CaseMapping = value ?? "rfc1459";
                        break;
                    case "CHANLIMIT":
                        this.channelLimit.Clear();
                        if (value == null) {
                            this.channelLimit.Add("#&+!", int.MaxValue);
                        } else {
                            foreach (string field in value.Split(new char[] { ',' })) {
                                fields = field.Split(new char[] { ':' });
                                this.channelLimit.Add(fields[0], int.Parse(fields[1]));
                            }
                        }
                        break;
                    case "CHANMODES":
                        if (value == null) {
                            this.ChanModes = ChannelModes.RFC2812;
                        } else {
                            fields = value.Split(new char[] { ',' });
                            this.ChanModes = new ChannelModes(fields[0].ToCharArray(), fields[1].ToCharArray(), fields[2].ToCharArray(), fields[3].ToCharArray());
                        }
                        break;
                    case "CHANNELLEN": this.ChannelLength = (value == null ? 200 : int.Parse(value)); break;
                    case "CHANTYPES": this.ChannelTypes = new ReadOnlyCollection<char>((value ?? "#").ToCharArray()); break;
                    case "EXCEPTS":
                        if (value == null) {
                            this.SupportsBanExceptions = false;
                            this.BanExceptionsMode = '\0';
                        } else {
                            this.SupportsBanExceptions = true;
                            this.BanExceptionsMode = (value == "" ? 'e' : value[0]);
                        }
                        break;
                    case "INVEX":
                        if (value == null) {
                            this.SupportsInviteExceptions = false;
                            this.InviteExceptionsMode = '\0';
                        } else {
                            this.SupportsInviteExceptions = true;
                            this.InviteExceptionsMode = (value == "" ? 'I' : value[0]);
                        }
                        break;
                    case "KICKLEN": this.KickMessageLength = (value == null ? int.MaxValue : int.Parse(value)); break;
                    case "MAXBANS":  // Obsolete form of MAXLIST
                        this.listModeLength.Clear();
                        if (value != null) {
                            foreach (string entry in value.Split(new char[] { ',' })) {
                                this.listModeLength.Add("b" + this.BanExceptionsMode + this.InviteExceptionsMode, int.Parse(value));
                            }
                        }
                        break;
                    case "MAXCHANNELS":  // Obsolete form of CHANLIMIT
                        this.channelLimit.Clear();
                        this.channelLimit.Add(this["CHANTYPES"] ?? "#&+!", int.Parse(value));
                        break;
                    case "MAXLIST":
                        this.listModeLength.Clear();
                        if (value != null) {
                            foreach (string entry in value.Split(new char[] { ',' })) {
                                fields = entry.Split(new char[] { ':' }, 2);
                                this.listModeLength.Add(fields[0], int.Parse(fields[1]));
                            }
                        }
                        break;
                    case "MODES": this.Modes = (value == null ? 3 : int.Parse(value)); break;
                    case "NETWORK": this.NetworkName = value; break;
                    case "NICKLEN": this.NicknameLength = (value == null ? 9 : int.Parse(value)); break;
                    case "PREFIX":
                        this.statusPrefix.Clear();
                        if (value == null) {
                            this.statusPrefix.Add('@', 'o');
                            this.statusPrefix.Add('+', 'v');
                        } else if (value != "") {
                            Match m = Regex.Match(value, @"^\(([a-zA-Z]*)\)(.*)$");
                            for (int j = 0; j < m.Groups[1].Value.Length; ++j)
                                this.statusPrefix.Add(m.Groups[2].Value[j], m.Groups[1].Value[j]);
                        }
                        break;
                    case "STATUSMSG": this.StatusMessage = value?.ToCharArray(); break;
                    case "TARGMAX":
                        this.maxTargets.Clear();
                        if (value != null) {
                            foreach (string field in value.Split(new char[] { ',' })) {
                                fields = field.Split(new char[] { ':' }, 2);
                                if (fields[1] == "")
                                    this.maxTargets.Remove(fields[0]);
                                else
                                    this.maxTargets.Add(fields[0], int.Parse(fields[1]));
                            }
                        }
                        break;
                    case "TOPICLEN": this.TopicLength = (string.IsNullOrEmpty(value) ? int.MaxValue : int.Parse(value)); break;
                    case "WATCH": this.SupportsWatch = (value != null); break;
                }
            }
        }

        /// <summary>Escapes certain characters in a string for sending as a RPL_ISUPPORT value.</summary>
        /// <returns>The input string with certain characters escaped.</returns>
        /// <remarks>
        /// The escape code is "\xHH", where HH is the numeric representation of a byte.
        /// Multibyte characters can be escaped as multiple \x sequences.
        /// This method escapes the following characters: null, carriage return, line feed, space, backslash.
        /// </remarks>
        public static string EscapeValue(string value) {
            // TODO: Make this also escape multibyte characters where one byte happens to be 0x00, 0x0A, 0x0D, 0x20 or 0x5C.
            // This isn't possible in UTF-8, as all bytes of multibyte characters have bit 7 set, but may be for other encodings.
            StringBuilder builder = new StringBuilder();
            foreach (char c in value) {
                if (c == '\0' || c == '\r' || c == '\n' || c == ' ' || c == '\\') {
                    builder.Append(@"\x");
                    builder.Append(((int) c).ToString("X2"));
                } else
                    builder.Append(c);
            }
            return builder.ToString();
        }
        /// <summary>Decodes escape sequences in a RPL_ISUPPORT value.</summary>
        /// <returns>A copy of the input string with escape sequences decoded, or the input string itself if no escape sequences are present.</returns>
        /// <remarks>
        /// The escape code is "\xHH", where HH is the numeric representation of a byte.
        /// Multibyte characters can be escaped as multiple \x sequences.
        /// </remarks>
        public static string UnescapeValue(string value) {
            int pos = value.IndexOf(@"\x"); int pos2 = 0;
            if (pos == -1) {
                // Nothing to decode.
                return value;
            }

            var bytes = new List<byte>(value.Length + value.Length / 4);
            do {
                int b = 0;
                if (pos != pos2)
                    bytes.AddRange(Encoding.UTF8.GetBytes(value.ToCharArray(), pos2, pos - pos2));

                if (pos <= value.Length - 4) {
                    if (value[pos + 2] >= '0' && value[pos + 2] <= '9')
                        b |= (value[pos + 2] - '0') << 4;
                    else if (value[pos + 2] >= 'A' && value[pos + 2] <= 'F')
                        b |= (value[pos + 2] - ('A' - 10)) << 4;
                    else if (value[pos + 2] >= 'a' && value[pos + 2] <= 'f')
                        b |= (value[pos + 2] - ('a' - 10)) << 4;
                    else
                        b = -1;

                    if (value[pos + 3] >= '0' && value[pos + 3] <= '9')
                        b |= (value[pos + 3] - '0');
                    else if (value[pos + 3] >= 'A' && value[pos + 3] <= 'F')
                        b |= (value[pos + 3] - ('A' - 10));
                    else if (value[pos + 3] >= 'a' && value[pos + 3] <= 'f')
                        b |= (value[pos + 3] - ('a' - 10));
                    else
                        b = -1;
                } else
                    b = -1;

                if (b == -1)
                    // Invalid escape sequence.
                    bytes.AddRange(Encoding.UTF8.GetBytes(value.ToCharArray(), pos, value.Length - pos));
                else
                    bytes.Add((byte) b);

                pos2 = pos + 4;
                pos = value.IndexOf(@"\x", pos2);
            } while (pos != -1);

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        /// <summary>Returns the parameters contained in this Extensions object as a string, in the form used by RPL_ISUPPORT.</summary>
        public override string ToString() {
            StringBuilder builder = new StringBuilder();
            foreach (var parameter in this) {
                if (builder.Length != 0) builder.Append(' ');
                builder.Append(parameter.Key);
                builder.Append('=');
                builder.Append(EscapeValue(parameter.Value));
            }
            return builder.ToString();
        }
    }
}
