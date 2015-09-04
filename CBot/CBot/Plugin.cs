using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using IRC;

namespace CBot {
    /// <summary>Provides a base class for CBot plugin main classes.</summary>
    public abstract class Plugin {
        private static Regex languageEscapeRegex = new Regex(@"\\(?:(n)|(r)|(t)|(\\)|(u)([0-9a-f]{4})?|($))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private string[] _Channels = new string[0];
        /// <summary>
        /// Sets or returns the list of channels that this plugin will receive events for.
        /// This property can be overridden.
        /// </summary>
        public virtual string[] Channels {
            get { return this._Channels; }
            set {
                if (value == null) this._Channels = new string[0];
                else this._Channels = value;
            }
        }

        /// <summary>
        /// When overridden, returns the name of the plugin.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns the key used to refer to this plugin.
        /// </summary>
        public string Key { get; internal set; }

        /// <summary>
        /// Returns the path to this plugin file.
        /// </summary>
        public string FilePath { get; internal set; }

        /// <summary>
        /// Returns the key used to refer to this plugin.
        /// </summary>
        [Obsolete("This property is deprecated because it is insecure and inefficient. Use Plugin.Key instead.")]
        public string MyKey {
            get {
                return this.Key;
            }
        }

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
        protected Dictionary<string, string> language = new Dictionary<string,string>();
        /// <summary>
        /// Contains the default message formats currently in use by this plugin.
        /// When an entry is not found in the Language list, the function should fall back to this list.
        /// </summary>
        protected Dictionary<string, string> defaultLanguage = new Dictionary<string, string>();
        private Random random = new Random();

        /// <summary>
        /// Creates a new instance of the Plugin class.
        /// </summary>
        protected Plugin() { }

        /// <summary>
        /// When overridden, returns help text on a specific user-specified topic, if it is available and relevant to this plugin; otherwise, null.
        /// If no topic was specified, the implementation may return a brief description of what this plugin is doing.
        /// </summary>
        /// <param name="Topic">The topic the user asked for help on, or null if none was specified.</param>
        public virtual string Help(string Topic) {
            return null;
        }

        /// <summary>
        /// Returns true if the specified channel is in this plugin's Channels list.
        /// </summary>
        /// <param name="connection">The connection to the place where the channel is.</param>
        /// <param name="channel">The channel to check.</param>
        /// <returns>True if the specified channel is in the Channels list; false otherwise.</returns>
        public bool IsActiveChannel(IRCClient connection, string channel) {
            if (!connection.IsChannel(channel)) return false; //return IsActivePM(connection, channel);

            foreach (string channelName in this.Channels) {
                string[] fields = channelName.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };

                if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                    if (fields[1] == "*" || fields[1] == "#*" || fields[1] == "*#" || connection.CaseMappingComparer.Equals(fields[1], channel)) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the specified PM target is in this plugin's Channels list.
        /// Whether or not the user can be seen, or is online, is not relevant.
        /// </summary>
        /// <param name="connection">The connection to the place where the channel is.</param>
        /// <param name="sender">The nickname of the user to check.</param>
        /// <returns>True if the specified user is in the Channels list; false otherwise.</returns>
        public bool IsActivePM(IRCClient connection, string sender) {
            foreach (string channelName in this.Channels) {
                string[] fields = channelName.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { "*", fields[0] };

                if (fields[0] == "*" || fields[0].Equals(connection.NetworkName, StringComparison.OrdinalIgnoreCase) || fields[0].Equals(connection.Address, StringComparison.OrdinalIgnoreCase)) {
                    if (fields[1] == "*" || fields[1] == "*?" || connection.CaseMappingComparer.Equals(fields[1], sender)) return true;
                    // Respond to PMs from users in an active channel.
                    if (connection.IsChannel(fields[1])) {
                        Channel channel;
                        if (connection.Channels.TryGetValue(fields[1], out channel)) {
                            if (channel.Users.Contains(sender)) return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Processes a command line and runs a matching command if one exists and the user has access to it.
        /// </summary>
        /// <param name="Connection">The connection from whence the command came.</param>
        /// <param name="Sender">The user sending the command.</param>
        /// <param name="Channel">The channel in which the command was sent, or the sender's nickname if it was in a private message.</param>
        /// <param name="InputLine">The message text.</param>
        /// <param name="globalCommand">True if the global command syntax was used; false otherwise.</param>
        /// <returns>True if a command was matched (even if it was denied); false otherwise.</returns>
        public bool RunCommand(IRCClient Connection, User Sender, string Channel, string InputLine, bool globalCommand = false) {
            string command = InputLine.Split(new char[] { ' ' })[0];

            MethodInfo method = null; CommandAttribute attribute = null;

            foreach (MethodInfo _method in this.GetType().GetMethods()) {
                foreach (CommandAttribute _attribute in _method.GetCustomAttributes(typeof(CommandAttribute), true)) {
                    if (_attribute.Names.Contains(command, StringComparer.OrdinalIgnoreCase)) {
                        method = _method;
                        attribute = _attribute;
                        break;
                    }
                }
                if (method != null) break;
            }
            if (method == null) return false;

            // Check the scope.
            if ((attribute.Scope & CommandScope.PM) == 0 && !Connection.IsChannel(Channel)) return false;
            if ((attribute.Scope & CommandScope.Channel) == 0 && Connection.IsChannel(Channel)) return false;

            // Check for permissions.
            string permission;
            if (attribute.Permission == null)
                permission = null;
            else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
                permission = this.Key + attribute.Permission;
            else
                permission = attribute.Permission;

            if (permission != null && !Bot.UserHasPermission(Connection, Channel, Sender.Nickname, permission)) {
                if (attribute.NoPermissionsMessage != null) Bot.Say(Connection, Sender.Nickname, attribute.NoPermissionsMessage);
                return true;
            }

            // Parse the parameters.
            string[] fields = InputLine.Split(new char[] { ' ' }, attribute.MaxArgumentCount + 1, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            if (fields.Length < attribute.MinArgumentCount) {
                Bot.Say(Connection, Sender.Nickname, "Not enough parameters.");
                Bot.Say(Connection, Sender.Nickname, string.Format("The correct syntax is \u000312{0}\u000F.", attribute.Syntax.ReplaceCommands(Connection, Channel)));
                return true;
            }

            // Run the command.
            // TODO: Run it on a separate thread.
            try {
                User user;
                if (Connection.Users.Contains(Sender.Nickname))
                    user = Connection.Users[Sender.Nickname];
                else
                    user = new User(Sender);
                CommandEventArgs e = new CommandEventArgs(Connection, Channel, user, fields);
                method.Invoke(this, new object[] { this, e });
            } catch (Exception ex) {
                Bot.LogError(this.Key, method.Name, ex);
                while (ex is TargetInvocationException || ex is AggregateException) ex = ex.InnerException;
                Bot.Say(Connection, Channel, "\u00034The command failed. This incident has been logged. ({0})", ex.Message.Replace('\n', ' '));
            }
            return true;
        }

        /// <summary>
        /// Processes a command line and runs any matching regex-bound procedures that the user has access to.
        /// </summary>
        /// <param name="Connection">The connection from whence the command came.</param>
        /// <param name="Sender">The user sending the command.</param>
        /// <param name="Channel">The channel in which the command was sent, or the sender's nickname if it was in a private message.</param>
        /// <param name="InputLine">The message text, excluding the bot's nickname if it started with such.</param>
        /// <param name="UsedMyNickname">True if the message was prefixed with the bot's nickname; false otherwise.</param>
        /// <returns>True if a command was matched (even if it was denied); false otherwise.</returns>
        public bool RunRegex(IRCClient Connection, User Sender, string Channel, string InputLine, bool UsedMyNickname) {
            MethodInfo method = null; RegexAttribute attribute = null; Match match = null;

            foreach (MethodInfo _method in this.GetType().GetMethods()) {
                foreach (RegexAttribute _attribute in _method.GetCustomAttributes(typeof(RegexAttribute), true)) {
                    foreach (string pattern in _attribute.Expressions) {
                        match = Regex.Match(InputLine, pattern, RegexOptions.IgnoreCase);
                        if (match.Success) {
                            method = _method;
                            attribute = _attribute;
                            break;
                        }
                    }
                }
                if (method != null) break;
            }
            if (method == null) return false;

            // Check the scope.
            if ((attribute.Scope & CommandScope.PM) == 0 && !Connection.IsChannel(Channel)) return false;
            if ((attribute.Scope & CommandScope.Channel) == 0 && Connection.IsChannel(Channel)) return false;

            // Check for permissions.
            string permission;
            if (attribute.Permission == null)
                permission = null;
            else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
                permission = this.Key + attribute.Permission;
            else
                permission = attribute.Permission;

            if (permission != null && !Bot.UserHasPermission(Connection, Channel, Sender.Nickname, permission)) {
                if (attribute.NoPermissionsMessage != null) Bot.Say(Connection, Sender.Nickname, attribute.NoPermissionsMessage);
                return true;
            }

            // Check the parameters.
            ParameterInfo[] parameterTypes = method.GetParameters();
            object[] parameters;
            bool handled = false;

            if (parameterTypes.Length == 2) {
                User user;
                if (Connection.Users.Contains(Sender.Nickname))
                    user = Connection.Users[Sender.Nickname];
                else
                    user = new User(Sender);
                RegexEventArgs e = new RegexEventArgs(Connection, Channel, user, match);
                parameters = new object[] { this, e };
            } else if (parameterTypes.Length == 5)
                parameters = new object[] { Connection, Sender, Channel, match, handled };
            else if (parameterTypes.Length == 4)
                parameters = new object[] { Connection, Sender, Channel, match };
            else
                throw new TargetParameterCountException("The regex-bound procedure " + method.Name + " has an invalid signature. Expected parameters: (object sender, RegexEventArgs e)");

            // Run the command.
            // TODO: Run it on a separate thread.
            try {
                method.Invoke(this, parameters);
            } catch (Exception ex) {
                Bot.LogError(this.Key, method.Name, ex);
                while (ex is TargetInvocationException || ex is AggregateException) ex = ex.InnerException;
                Bot.Say(Connection, Channel, "\u00034The command failed. This incident has been logged. ({0})", ex.Message);
            }
            return true;
        }

        /// <summary>
        /// Sends a message to all channels in which the bot is active. Channel names containing wildcards are excluded.
        /// </summary>
        /// <param name="message">The text to send.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <param name="exclude">A list of channel names that should be excloded. May be null or empty to exclude nothing.</param>
        public void SayToAllChannels(string message, SayOptions options = 0, string[] exclude = null) {
            this.SayToAllChannels(message, options, exclude, false, null, null, null);
        }
        /// <summary>
        /// Invokes GetMessage and sends the result to all channels in which the bot is active. Channel names containing wildcards are excluded.
        /// </summary>
        /// <param name="key">The key to retrieve.</param>
        /// <param name="nickname">The nickname of the user who sent the command that this message is response to, or null if not applicable.</param>
        /// <param name="channel">The channel that the command was given in, or is otherwise relevant to this message, or null if none is.</param>
        /// <param name="options">A SayOptions value specifying how to send the message.</param>
        /// <param name="exclude">A list of channel names that should be excloded. May be null or empty to exclude nothing.</param>
        /// <param name="args">Implementation-defined elements to be included in the formatted message.</param>
        public void SayLanguageToAllChannels(string key, string nickname, string channel, SayOptions options = 0, string[] exclude = null, params object[] args) {
            this.SayToAllChannels(key, options, exclude, true, nickname, channel, args);
        }

        private void SayToAllChannels(string message, SayOptions options, string[] exclude, bool isLanguage, string nickname, string channel, params object[] args) {
            if (message == null || message == "") return;
            if (this.Channels == null) return;

            if ((options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(message[0]);
                if (c != message[0]) message = c + message.Substring(1);
            }

            List<string>[] privmsgTarget = new List<string>[Bot.Clients.Count];
            List<string>[] noticeTarget = new List<string>[Bot.Clients.Count];

            foreach (string channel2 in this.Channels) {
                string address;
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

                for (int index = 0; index < Bot.Clients.Count; ++index) {
                    if (address == null || address == "*" || address.Equals(Bot.Clients[index].Client.Address, StringComparison.OrdinalIgnoreCase) || address.Equals(Bot.Clients[index].Name, StringComparison.OrdinalIgnoreCase)) {
                        if (Bot.Clients[index].Client.IsChannel(channel3)) {
                            if ((address == null || address == "*") && !Bot.Clients[index].Client.Channels.Contains(channel3)) continue;
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

                        List<string> selectedTarget;
                        if (notice)
                            selectedTarget = noticeTarget[index];
                        else
                            selectedTarget = privmsgTarget[index];

                        if (!selectedTarget.Contains(target))
                            selectedTarget.Add(target);
                    }
                }

            }

            string key = message;
            for (int index = 0; index < Bot.Clients.Count; ++index) {
                if (isLanguage)
                    message = this.GetMessage(key, nickname, channel, args);

                if (privmsgTarget[index] != null)
                    Bot.Clients[index].Client.Send("PRIVMSG {0} :{1}", string.Join(",", privmsgTarget[index]), message);
                if (noticeTarget[index] != null)
                    Bot.Clients[index].Client.Send("NOTICE {0} :{1}", string.Join(",", noticeTarget[index]), message);
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
        public string GetMessage(string key, string nickname, string channel, params object[] args) {
            string format;
            if (!this.language.TryGetValue(key, out format))
                if (!this.defaultLanguage.TryGetValue(key, out format))
                    return null;

            return string.Format(this.ProcessMessage(format, nickname, channel), args ?? new object[0]);
        }
        private string ProcessMessage(string format, string nickname, string channel) {
            int braceLevel = 0;
            StringBuilder builder = new StringBuilder(format.Length);

            for (int i = 0; i < format.Length; ++i) {
                    if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{(}") {
                        builder.Append("(");
                        i += 2;
                    } else if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{)}") {
                        builder.Append(")");
                        i += 2;
                    } else if (i <= format.Length - 3 && format[i] == '{' && format.Substring(i, 3) == "{|}") {
                        builder.Append("|");
                        i += 2;
                    } else if (i <= format.Length - 2 && format[i] == '(' && format[i + 1] == '(') {
                        i += 2;
                        int start = i;
                        braceLevel = 1;
                        List<Tuple<int, int>> options = new List<Tuple<int, int>>();

                        for (; i < format.Length; ++i) {
                            if (i < format.Length - 1) {
                                if (format[i] == '{' && i <= format.Length - 3) {
                                    if (format.Substring(i, 3) == "{(}") {
                                        builder.Append("(");
                                        i += 2;
                                    } else if (format.Substring(i, 3) == "{)}") {
                                        builder.Append(")");
                                        i += 2;
                                    } else if (format.Substring(i, 3) == "{|}") {
                                        builder.Append("|");
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
                        Tuple<int, int> choice = options[this.random.Next(options.Count)];
                        builder.Append(ProcessMessage(format.Substring(choice.Item1, choice.Item2 - choice.Item1), nickname, channel));
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
            string path = Path.Combine(Bot.LanguagesPath, Bot.Language, this.Key + ".properties");
            if (File.Exists(path)) {
                this.LoadLanguage(path);
            } else {
                path = Path.Combine(Bot.LanguagesPath, "Default", this.Key + ".properties");
                if (File.Exists(path)) {
                    this.LoadLanguage(path);
                }
            }
        }
        internal void LoadLanguage(string filePath) {
            using (StreamReader reader = new StreamReader(File.Open(filePath, FileMode.Open, FileAccess.Read))) {
                this.language.Clear();
                while (!reader.EndOfStream) {
                    string s = reader.ReadLine().TrimStart();
                    if (s.Length == 0 || s[0] == '#' || s[0] == '!') continue;  // Ignore blank lines and comments.

                    string[] fields = s.Split(new char[] { '=' }, 2);
                    if (fields.Length == 2) {
                        StringBuilder formatBuilder = new StringBuilder(fields[1].Length);
                        int pos = 0; Match m; bool nextLine;
                        do {
                            nextLine = false;
                            while ((m = languageEscapeRegex.Match(fields[1], pos)).Success) {
                                formatBuilder.Append(fields[1].Substring(pos, m.Index - pos));
                                if (m.Groups[1].Success) {
                                    formatBuilder.Append("\n");
                                } else if (m.Groups[2].Success) {
                                    formatBuilder.Append("\r");
                                } else if (m.Groups[3].Success) {
                                    formatBuilder.Append("\t");
                                } else if (m.Groups[4].Success) {
                                    formatBuilder.Append("\\");
                                } else if (m.Groups[5].Success) {
                                    // Unicode escape.
                                    if (m.Groups[6].Success) {
                                        formatBuilder.Append((char) Convert.ToInt32(m.Groups[6].Value, 16));
                                    } else {
                                        throw new FormatException("Invalid unicode (\\u) escape sequence at '" + fields[0] + "' in " + filePath + ".");
                                    }
                                } else if (m.Groups[7].Success) {
                                    // Escaped newline; read another line and append that.
                                    if (reader.EndOfStream) throw new FormatException("Backslash with nothing after it at '" + fields[0] + "' in " + filePath + ".");
                                    fields[1] = reader.ReadLine().TrimStart();
                                    nextLine = true;
                                }
                                pos += m.Length;
                            }
                        } while (nextLine);
                        formatBuilder.Append(fields[1].Substring(pos));

                        this.language.Add(fields[0], formatBuilder.ToString());
                    }
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
        public virtual void OnUnload() {
            this.OnSave();
        }

        /// <summary>
        /// Reports an exception to the user, and logs it.
        /// </summary>
        /// <param name="Procedure">The human-readable name of the procedure that had the problem.</param>
        /// <param name="ex">The exception that was thrown.</param>
        protected void LogError(string Procedure, Exception ex) {
            Bot.LogError(this.Key, Procedure, ex);
        }

        public virtual bool OnAwayCancelled(object sender, AwayEventArgs e) { return false; }
        public virtual bool OnAwaySet(object sender, AwayEventArgs e) { return false; }
        public virtual bool OnBanList(object sender, ChannelModeListEventArgs e) { return false; }
        public virtual bool OnBanListEnd(object sender, ChannelModeListEndEventArgs e) { return false; }
        public virtual bool OnChannelAction(object sender, ChannelMessageEventArgs e) {
            return this.RunRegex((IRCClient) sender, e.Sender, e.Channel, "\u0001ACTION " + e.Message + "\u0001", false);
        }
        public virtual bool OnChannelAdmin(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelAdminSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelBan(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelBanSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelTimestamp(object sender, ChannelTimestampEventArgs e) { return false; }
        public virtual bool OnChannelCTCP(object sender, ChannelMessageEventArgs e) { return false; }
        public virtual bool OnChannelDeAdmin(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeAdminSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeHalfOp(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeHalfOpSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeHalfVoice(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeHalfVoiceSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeOp(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeOpSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeOwner(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeOwnerSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeVoice(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelDeVoiceSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelExempt(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelExemptSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelHalfOp(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelHalfOpSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelHalfVoice(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelHalfVoiceSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelInviteExempt(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelInviteExemptSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelJoin(object sender, ChannelJoinEventArgs e) { return false; }
        public virtual bool OnChannelJoinSelf(object sender, ChannelJoinEventArgs e) { return false; }
        public virtual bool OnChannelJoinDenied(object sender, ChannelDeniedEventArgs e) { return false; }
        public virtual bool OnChannelKick(object sender, ChannelKickEventArgs e) { return false; }
        public virtual bool OnChannelKickSelf(object sender, ChannelKickEventArgs e) { return false; }
        public virtual bool OnChannelMessage(object sender, ChannelMessageEventArgs e) {
            string message = e.Message;
            Match match = Regex.Match(e.Message, @"^" + Regex.Escape(((IRCClient) sender).Nickname) + @"\.*[:,-]? (.*)", RegexOptions.IgnoreCase);
            if (match.Success)
                message = match.Groups[1].Value;
            else
                message = e.Message;

            bool handled = false;
            if (e.Message != "") {
                if (Bot.GetCommandPrefixes((IRCClient) sender, e.Channel).Contains(message[0].ToString()))
                    handled = this.RunCommand((IRCClient) sender, e.Sender, e.Channel, message.Substring(1), false);
                else if (match.Success) {
                    handled = this.RunCommand((IRCClient) sender, e.Sender, e.Channel, message, false);
                }
            }
            if (!handled)
                handled = this.RunRegex((IRCClient) sender, e.Sender, e.Channel, message, match.Success);
            return handled;
        }
        public virtual bool OnChannelMessageDenied(object sender, ChannelDeniedEventArgs e) { return false; }
        public virtual bool OnChannelModeSet(object sender, ChannelModeEventArgs e) { return false; }
        public virtual bool OnChannelModeSetSelf(object sender, ChannelModeEventArgs e) { return false; }
        public virtual bool OnChannelModeUnhandled(object sender, ChannelModeEventArgs e) { return false; }
        public virtual bool OnChannelModesSet(object sender, ChannelModesSetEventArgs e) { return false; }
        public virtual bool OnChannelModesGet(object sender, ChannelModesGetEventArgs e) { return false; }
        public virtual bool OnChannelNotice(object sender, ChannelMessageEventArgs e) { return false; }
        public virtual bool OnChannelOp(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelOpSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelOwner(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelOwnerSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelPart(object sender, ChannelPartEventArgs e) { return false; }
        public virtual bool OnChannelPartSelf(object sender, ChannelPartEventArgs e) { return false; }
        public virtual bool OnChannelQuiet(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelQuietSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelRemoveExempt(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelRemoveExemptSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelRemoveInviteExempt(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelRemoveInviteExemptSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelRemoveKey(object sender, ChannelEventArgs e) { return false; }
        public virtual bool OnChannelRemoveLimit(object sender, ChannelEventArgs e) { return false; }
        public virtual bool OnChannelSetKey(object sender, ChannelKeyEventArgs e) { return false; }
        public virtual bool OnChannelSetLimit(object sender, ChannelLimitEventArgs e) { return false; }
        public virtual bool OnChannelTopic(object sender, ChannelTopicEventArgs e) { return false; }
        public virtual bool OnChannelTopicChange(object sender, ChannelTopicChangeEventArgs e) { return false; }
        public virtual bool OnChannelTopicStamp(object sender, ChannelTopicStampEventArgs e) { return false; }
        public virtual bool OnChannelUsers(object sender, ChannelNamesEventArgs e) { return false; }
        public virtual bool OnChannelUnBan(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelUnBanSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelUnQuiet(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelUnQuietSelf(object sender, ChannelListModeEventArgs e) { return false; }
        public virtual bool OnChannelVoice(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnChannelVoiceSelf(object sender, ChannelNicknameModeEventArgs e) { return false; }
        public virtual bool OnPrivateCTCP(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnDisconnected(object sender, ExceptionEventArgs e) { return false; }
        public virtual bool OnException(object sender, ExceptionEventArgs e) { return false; }
        public virtual bool OnExemptList(object sender, ChannelModeListEventArgs e) { return false; }
        public virtual bool OnExemptListEnd(object sender, ChannelModeListEndEventArgs e) { return false; }
        public virtual bool OnInvite(object sender, ChannelInviteEventArgs e) { return false; }
        public virtual bool OnInviteSent(object sender, ChannelInviteSentEventArgs e) { return false; }
        public virtual bool OnInviteList(object sender, ChannelModeListEventArgs e) { return false; }
        public virtual bool OnInviteListEnd(object sender, ChannelModeListEndEventArgs e) { return false; }
        public virtual bool OnInviteExemptList(object sender, ChannelModeListEventArgs e) { return false; }
        public virtual bool OnInviteExemptListEnd(object sender, ChannelModeListEndEventArgs e) { return false; }
        public virtual bool OnKilled(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnChannelList(object sender, ChannelListEventArgs e) { return false; }
        public virtual bool OnChannelListEnd(object sender, ChannelListEndEventArgs e) { return false; }
        public virtual bool OnMOTD(object sender, MOTDEventArgs e) { return false; }
        public virtual bool OnNames(object sender, ChannelNamesEventArgs e) { return false; }
        public virtual bool OnNamesEnd(object sender, ChannelModeListEndEventArgs e) { return false; }
        public virtual bool OnNicknameChange(object sender, NicknameChangeEventArgs e) { return false; }
        public virtual bool OnNicknameChangeSelf(object sender, NicknameChangeEventArgs e) { return false; }
        public virtual bool OnNicknameChangeFailed(object sender, NicknameEventArgs e) { return false; }
        public virtual bool OnNicknameInvalid(object sender, NicknameEventArgs e) { return false; }
        public virtual bool OnNicknameTaken(object sender, NicknameEventArgs e) { return false; }
        public virtual bool OnPrivateNotice(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnPing(object sender, PingEventArgs e) { return false; }
        public virtual bool OnPingReply(object sender, PingEventArgs e) { return false; }
        public virtual bool OnPrivateMessage(object sender, PrivateMessageEventArgs e) {
            string message = e.Message;
            Match match = Regex.Match(e.Message, @"^" + Regex.Escape(((IRCClient) sender).Nickname) + @"\.*[:,-]? (.*)", RegexOptions.IgnoreCase);
            if (match.Success)
                message = match.Groups[1].Value;
            else
                message = e.Message;

            bool handled = false;
            if (e.Message != "") {
                if (Bot.GetCommandPrefixes((IRCClient) sender, e.Sender.Nickname).Contains(message[0].ToString()))
                    handled = this.RunCommand((IRCClient) sender, e.Sender, e.Sender.Nickname, message.Substring(1), false);
                else if (match.Success) {
                    handled = this.RunCommand((IRCClient) sender, e.Sender, e.Sender.Nickname, message, false);
                }
            }
            if (!handled)
                handled = this.RunRegex((IRCClient) sender, e.Sender, e.Sender.Nickname, message, match.Success);
            return handled;
        }
        public virtual bool OnPrivateAction(object sender, PrivateMessageEventArgs e) {
            return this.RunRegex((IRCClient) sender, e.Sender, e.Sender.Nickname, "\u0001ACTION " + e.Message + "\u0001", false);
        }
        public virtual bool OnQuit(object sender, QuitEventArgs e) { return false; }
        public virtual bool OnQuitSelf(object sender, QuitEventArgs e) { return false; }
        public virtual bool OnRawLineReceived(object sender, RawParsedEventArgs e) { return false; }
        public virtual bool OnRawLineSent(object sender, RawEventArgs e) { return false; }
        public virtual bool OnUserModesGet(object sender, UserModesEventArgs e) { return false; }
        public virtual bool OnUserModesSet(object sender, UserModesEventArgs e) { return false; }
        public virtual bool OnWallops(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnServerNotice(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnServerError(object sender, ServerErrorEventArgs e) { return false; }
        public virtual bool OnSSLHandshakeComplete(object sender, EventArgs e) { return false; }
        public virtual bool OnTimeOut(object sender, EventArgs e) { return false; }
        public virtual bool OnWhoList(object sender, WhoListEventArgs e) { return false; }
        public virtual bool OnWhoIsAuthenticationLine(object sender, WhoisAuthenticationEventArgs e) { return false; }
        public virtual bool OnWhoIsAwayLine(object sender, WhoisAwayEventArgs e) { return false; }
        public virtual bool OnWhoIsChannelLine(object sender, WhoisChannelsEventArgs e) { return false; }
        public virtual bool OnWhoIsEnd(object sender, WhoisEndEventArgs e) { return false; }
        public virtual bool OnWhoIsIdleLine(object sender, WhoisIdleEventArgs e) { return false; }
        public virtual bool OnWhoIsNameLine(object sender, WhoisNameEventArgs e) { return false; }
        public virtual bool OnWhoIsOperLine(object sender, WhoisOperEventArgs e) { return false; }
        public virtual bool OnWhoIsHelperLine(object sender, WhoisOperEventArgs e) { return false; }
        public virtual bool OnWhoIsRealHostLine(object sender, WhoisRealHostEventArgs e) { return false; }
        public virtual bool OnWhoIsServerLine(object sender, WhoisServerEventArgs e) { return false; }
        public virtual bool OnWhoWasNameLine(object sender, WhoisNameEventArgs e) { return false; }
        public virtual bool OnWhoWasEnd(object sender, WhoisEndEventArgs e) { return false; }

        public virtual bool OnChannelLeave(object sender, ChannelPartEventArgs e) { return false; }
        public virtual bool OnChannelLeaveSelf(object sender, ChannelPartEventArgs e) { return false; }
    }
}
