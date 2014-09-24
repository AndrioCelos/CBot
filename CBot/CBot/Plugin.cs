using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using IRC;

namespace CBot {
    public class Plugin {
        private string[] _Channels;
        public virtual string[] Channels {
            get { return this._Channels; }
            set { this._Channels = value; }
        }

        public virtual string Name {
            get {
                return "New Plugin";
            }
        }

        public string MyKey {
            get {
                foreach (KeyValuePair<string, PluginData> plugin in Bot.Plugins) {
                    if (plugin.Value.Obj == this)
                        return plugin.Key;
                }
                return null;
            }
        }

        public Plugin() {
        }

        public virtual string Help(string Topic) {
            return null;
        }

        public bool IsActiveChannel(IRCClient connection, string channel) {
            if (!connection.IsChannel(channel)) return IsActivePM(connection, channel);

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

        public bool RunCommand(IRCClient Connection, string Sender, string Channel, string InputLine, bool IsMinorChannelCommand = false) {
            string command = InputLine.Split(new char[] { ' ' })[0];

            foreach (string c in Bot.getCommandPrefixes(Connection, Channel))
                if (command.StartsWith(c)) {
                    command = command.Substring(1);
                    break;
                }

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
            if ((attribute.Scope & CommandScope.PM) == 0 && !Channel.StartsWith("#")) return false;
            if ((attribute.Scope & CommandScope.Channel) == 0 && Channel.StartsWith("#")) return false;

            // Check for permissions.
            string permission;
            if (attribute.Permission == null)
                permission = null;
            else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
                permission = MyKey + attribute.Permission;
            else
                permission = attribute.Permission;

            if (permission != null && !Bot.UserHasPermission(Connection, Channel, Sender.Split(new char[] { '!' })[0], permission)) {
                if (attribute.NoPermissionsMessage != null) this.Say(Connection, Sender.Split(new char[] { '!' })[0], attribute.NoPermissionsMessage);
                return true;
            }

            // Parse the parameters.
            string[] fields = InputLine.Split(new char[] { ' ' }, attribute.MaxArgumentCount + 1).Skip(1).ToArray();
            if (fields.Length < attribute.MinArgumentCount) {
                this.Say(Connection, Sender.Split(new char[] { '!' })[0], "Not enough parameters.");
                this.Say(Connection, Sender.Split(new char[] { '!' })[0], string.Format("The correct syntax is \u000312{0}\u000F.", attribute.Syntax));
                return true;
            }

            // Run the command.
            // TODO: Run it on a separate thread.
            try {
                User user;
                if (Connection.Users.Contains(Sender.Split(new char[] { '!' })[0]))
                    user = Connection.Users[Sender.Split(new char[] { '!' })[0]];
                else
                    user = new User(Sender);
                CommandEventArgs e = new CommandEventArgs(Connection, Channel, user, fields);
                method.Invoke(this, new object[] { this, e });
            } catch (Exception ex) {
                Bot.LogError(MyKey, method.Name, ex);
            }
            return true;
        }

        public bool RunRegex(IRCClient Connection, string Sender, string Channel, string InputLine, bool UsedMyNickname) {
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
            if ((attribute.Scope & CommandScope.PM) == 0 && !Channel.StartsWith("#")) return false;
            if ((attribute.Scope & CommandScope.Channel) == 0 && Channel.StartsWith("#")) return false;

            // Check for permissions.
            string permission;
            if (attribute.Permission == null)
                permission = null;
            else if (attribute.Permission != "" && attribute.Permission.StartsWith("."))
                permission = MyKey + attribute.Permission;
            else
                permission = attribute.Permission;

            if (permission != null && !Bot.UserHasPermission(Connection, Channel, Sender.Split(new char[] { '!' })[0], permission)) {
                if (attribute.NoPermissionsMessage != null) this.Say(Connection, Sender.Split(new char[] { '!' })[0], attribute.NoPermissionsMessage);
                return true;
            }

            // Check the parameters.
            ParameterInfo[] parameterTypes = method.GetParameters();
            object[] parameters;
            bool handled = false;

            if (parameterTypes.Length == 2) {
                User user;
                if (Connection.Users.Contains(Sender.Split(new char[] { '!' })[0]))
                    user = Connection.Users[Sender.Split(new char[] { '!' })[0]];
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
                Bot.LogError(MyKey, method.Name, ex);
            }
            return true;
        }

        public void Say(IRCClient Connection, string Channel, string Message, SayOptions Options = 0) {
            if (Message == null || Message == "") return;

            if ((Options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(Message[0]);
                if (c != Message[0]) Message = c + Message.Substring(1);
            }

            bool notice = false;
            if (Channel.StartsWith("#")) {
                if ((Options & SayOptions.OpsOnly) != 0) {
                    Channel = "@" + Channel;
                    notice = true;
                }
            } else
                notice = true;
            if ((Options & SayOptions.NoticeAlways) != 0)
                notice = true;
            if ((Options & SayOptions.NoticeNever) != 0)
                notice = false;

            Connection.Send("{0} {1} :{2}", notice ? "NOTICE" : "PRIVMSG", Channel, Message);
        }

        public void SayToAllChannels(string Message, SayOptions Options = 0, string[] Exclude = null) {
            if (Message == null || Message == "") return;

            if ((Options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(Message[0]);
                if (c != Message[0]) Message = c + Message.Substring(1);
            }

            List<string>[] privmsgTarget = new List<string>[Bot.Connections.Count];
            List<string>[] noticeTarget = new List<string>[Bot.Connections.Count];

            foreach (string channel2 in this.Channels) {
                string address;
                string channel;

                string[] fields = channel2.Split(new char[] { '/' }, 2);
                if (fields.Length == 2) {
                    address = fields[0];
                    channel = fields[1];
                } else {
                    address = null;
                    channel = fields[0];
                }

                bool notice = false;
                string target = channel;

                for (int index = 0; index < Bot.Connections.Count; ++index) {
                    if (address == null || address == "*" || address.Equals(Bot.Connections[index].Address, StringComparison.OrdinalIgnoreCase)) {
                        if (channel == "*") {
                            target = null;
                        } else if (channel.StartsWith("#")) {
                            if ((Options & SayOptions.OpsOnly) != 0) {
                                target = "@" + channel;
                                notice = true;
                            }
                        } else
                            notice = true;

                        if ((Options & SayOptions.NoticeAlways) != 0)
                            notice = true;
                        if ((Options & SayOptions.NoticeNever) != 0)
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

                        if (target == null) {
                            selectedTarget.Clear();
                            selectedTarget.AddRange(Bot.Connections[index].Channels.Select(c => ((Options & SayOptions.OpsOnly) != 0 ? "@" + c.Name : c.Name)));
                        } else {
                            if (!selectedTarget.Contains(target))
                                selectedTarget.Add(target);
                        }
                    }
                }

            }

            for (int index = 0; index < Bot.Connections.Count; ++index) {
                if (privmsgTarget[index] != null)
                    Bot.Connections[index].Send("PRIVMSG {0} :{1}", string.Join(",", privmsgTarget[index]), Message);
                if (noticeTarget[index] != null)
                    Bot.Connections[index].Send("NOTICE {0} :{1}", string.Join(",", noticeTarget[index]), Message);
            }
        }

        public virtual void OnSave() {
        }

        public virtual void OnUnload() {
            this.OnSave();
        }

        public void LogError(string Procedure, Exception ex) {
            Bot.LogError(this.MyKey, Procedure, ex);
        }

        public virtual void OnAwayCancelled(IRCClient Connection, string Message) { }
        public virtual void OnAway(IRCClient Connection, string Message) { }
        public virtual void OnBanList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) { }
        public virtual void OnBanListEnd(IRCClient Connection, string Message) { }
        public virtual void OnNicknameChange(IRCClient sender, string User, string NewNick) { }
        public virtual void OnNicknameChangeSelf(IRCClient sender, string User, string NewNick) { }
        public virtual void OnChannelAction(IRCClient Connection, string Sender, string Channel, string Message) {
            this.RunRegex(Connection, Sender, Channel, "\u0001ACTION " + Message + "\u0001", false);
        }
        public virtual void OnChannelActionHighlight(IRCClient Connection, string Sender, string Channel, string Message) {
            this.RunRegex(Connection, Sender, Channel, "\u0001ACTION " + Message + "\u0001", false);
        }
        public virtual void OnChannelAdmin(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelAdminSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelBan(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelBanSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelTimestamp(IRCClient Connection, string Channel, DateTime Timestamp) { }
        public virtual void OnChannelCTCP(IRCClient Connection, string Sender, string Channel, string Message) { }
        public virtual void OnChannelDeAdmin(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeAdminSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeHalfOp(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeHalfOpSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeHalfVoice(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeHalfVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeOp(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeOpSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeOwner(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeOwnerSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeVoice(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelDeVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelExit(IRCClient Connection, string Sender, string Channel, string Reason) { }
        public virtual void OnChannelExitSelf(IRCClient Connection, string Sender, string Channel, string Reason) { }
        public virtual void OnChannelHalfOp(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelHalfOpSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelHalfVoice(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelHalfVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelInviteExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelInviteExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelJoin(IRCClient Connection, string Sender, string Channel) { }
        public virtual void OnChannelJoinSelf(IRCClient Connection, string Sender, string Channel) { }
        public virtual void OnChannelJoinDeniedBanned(IRCClient Connection, string Channel) { }
        public virtual void OnChannelJoinDeniedFull(IRCClient Connection, string Channel) { }
        public virtual void OnChannelJoinDeniedInvite(IRCClient Connection, string Channel) { }
        public virtual void OnChannelJoinDeniedKey(IRCClient Connection, string Channel) { }
        public virtual void OnChannelKick(IRCClient Connection, string Sender, string Channel, string Target, string Reason) { }
        public virtual void OnChannelKickSelf(IRCClient Connection, string Sender, string Channel, string Target, string Reason) { }
        public virtual void OnChannelList(IRCClient Connection, string Channel, int Users, string Topic) { }
        public virtual void OnChannelMessage(IRCClient Connection, string Sender, string Channel, string Message) {
            Match match = Regex.Match(Message, @"^" + Regex.Escape(Connection.Nickname) + @"\.*[:,-]? (.*)", RegexOptions.IgnoreCase);
            if (match.Success)
                Message = match.Groups[0].Value;

            bool Handled = false;
            if (Message != "") {
                if (Bot.getCommandPrefixes(Connection, Channel).Contains(Message[0].ToString()))
                    Handled = this.RunCommand(Connection, Sender, Channel, Message, false);
                else {
                    Handled = this.RunCommand(Connection, Sender, Channel, Bot.getCommandPrefixes(Connection, Channel)[0] + Message, false);
                }
            }
            if (!Handled)
                Handled = this.RunRegex(Connection, Sender, Channel, Message, match.Success);
        }
        public virtual void OnChannelMessageSendDenied(IRCClient Connection, string Channel, string Message) { }
        public virtual void OnChannelMessageHighlight(IRCClient Connection, string Sender, string Channel, string Message) { }
        public virtual void OnChannelMode(IRCClient Connection, string Sender, string Channel, bool Direction, string Mode) { }
        public virtual void OnChannelModesGet(IRCClient Connection, string Channel, string Modes) { }
        public virtual void OnChannelOp(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelOpSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelOwner(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelOwnerSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelPart(IRCClient Connection, string Sender, string Channel, string Reason) { }
        public virtual void OnChannelPartSelf(IRCClient Connection, string Sender, string Channel, string Reason) { }
        public virtual void OnChannelQuiet(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelQuietSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelRemoveExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelRemoveExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelRemoveInviteExempt(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelRemoveInviteExemptSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelRemoveKey(IRCClient Connection, string Sender, string Channel) { }
        public virtual void OnChannelRemoveLimit(IRCClient Connection, string Sender, string Channel) { }
        public virtual void OnChannelSetKey(IRCClient Connection, string Sender, string Channel, string Key) { }
        public virtual void OnChannelSetLimit(IRCClient Connection, string Sender, string Channel, int Limit) { }
        public virtual void OnChannelTopic(IRCClient Connection, string Channel, string Topic) { }
        public virtual void OnChannelTopicChange(IRCClient Connection, string Sender, string Channel, string NewTopic) { }
        public virtual void OnChannelTopicStamp(IRCClient Connection, string Channel, string Setter, DateTime SetDate) { }
        public virtual void OnChannelUsers(IRCClient Connection, string Channel, string Names) { }
        public virtual void OnChannelUnBan(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelUnBanSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelUnQuiet(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelUnQuietSelf(IRCClient Connection, string Sender, string Channel, string Target, string[] MatchedUsers) { }
        public virtual void OnChannelVoice(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnChannelVoiceSelf(IRCClient Connection, string Sender, string Channel, string Target) { }
        public virtual void OnPrivateCTCP(IRCClient Connection, string Sender, string Message) { }
        public virtual void OnExemptList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) { }
        public virtual void OnExemptListEnd(IRCClient Connection, string Message) { }
        public virtual void OnInvite(IRCClient Connection, string Sender, string Channel) { }
        public virtual void OnInviteExemptList(IRCClient Connection, string Channel, string BannedUser, string BanningUser, DateTime Time) { }
        public virtual void OnInviteExemptListEnd(IRCClient Connection, string Message) { }
        public virtual void OnKilled(IRCClient Connection, string Sender, string Reason) { }
        public virtual void OnNames(IRCClient Connection, string Channel, string Message) { }
        public virtual void OnNamesEnd(IRCClient Connection, string Channel, string Message) { }
        public virtual void OnPrivateMessage(IRCClient Connection, string Sender, string Message) {
            Match match = Regex.Match(Message, @"^" + Regex.Escape(Connection.Nickname) + @"\.*[:,-]? (.*)", RegexOptions.IgnoreCase);
            if (match.Success)
                Message = match.Groups[0].Value;

            bool Handled = false;
            if (Message != "") {
                if (Bot.getCommandPrefixes(Connection, Sender.Split(new char[] { '!' })[0]).Contains(Message[0].ToString()))
                    Handled = this.RunCommand(Connection, Sender, Sender.Split(new char[] { '!' })[0], Message, false);
                else {
                    Handled = this.RunCommand(Connection, Sender, Sender.Split(new char[] { '!' })[0], Bot.getCommandPrefixes(Connection, Sender.Split(new char[] { '!' })[0])[0] + Message, false);
                }
            }
            if (!Handled)
                Handled = this.RunRegex(Connection, Sender, Sender.Split(new char[] { '!' })[0], Message, match.Success);
        }
        public virtual void OnPrivateAction(IRCClient Connection, string Sender, string Message) {
            this.RunRegex(Connection, Sender, Sender.Split(new char[] { '!' })[0], "\u0001ACTION " + Message + "\u0001", false);
        }
        public virtual void OnPrivateNotice(IRCClient Connection, string Sender, string Message) { }
        public virtual void OnQuit(IRCClient Connection, string Sender, string Reason) { }
        public virtual void OnQuitSelf(IRCClient Connection, string Sender, string Reason) { }
        public virtual void OnRawLineReceived(IRCClient Connection, string Message) { }
        public virtual void OnTimeOut(IRCClient Connection) { }
        public virtual void OnUserModesSet(IRCClient Connection, string Sender, string Modes) { }
        public virtual void OnServerNotice(IRCClient Connection, string Sender, string Message) { }
        public virtual void OnServerError(IRCClient Connection, string Message) { }
        public virtual void OnServerMessage(IRCClient Connection, string Sender, string Numeric, string Message) { }
        public virtual void OnServerMessageUnhandled(IRCClient Connection, string Sender, string Numeric, string Message) { }
        public virtual void OnWhoList(IRCClient Connection, string Channel, string Username, string Address, string Server, string Nickname, string Flags, int Hops, string FullName) { }
    }
}
