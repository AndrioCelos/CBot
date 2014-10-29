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
                foreach (KeyValuePair<string, PluginEntry> plugin in Bot.Plugins) {
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
                permission = MyKey + attribute.Permission;
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
                Bot.LogError(MyKey, method.Name, ex);
                Bot.Say(Connection, Channel, "\u00034The command failed. This incident has been logged. ({0})", ex.Message);
            }
            return true;
        }

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
                permission = MyKey + attribute.Permission;
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
                Bot.LogError(MyKey, method.Name, ex);
                Bot.Say(Connection, Channel, "\u00034The command failed. This incident has been logged. ({0})", ex.Message);
            }
            return true;
        }

        public void SayToAllChannels(string Message, SayOptions Options = 0, string[] Exclude = null) {
            if (Message == null || Message == "") return;
            if (this.Channels == null) return;

            if ((Options & SayOptions.Capitalise) != 0) {
                char c = char.ToUpper(Message[0]);
                if (c != Message[0]) Message = c + Message.Substring(1);
            }

            List<string>[] privmsgTarget = new List<string>[Bot.Clients.Count];
            List<string>[] noticeTarget = new List<string>[Bot.Clients.Count];

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
                if (channel == "*") continue;

                bool notice = false;
                string target = channel;

                for (int index = 0; index < Bot.Clients.Count; ++index) {
                    if (address == null || address == "*" || address.Equals(Bot.Clients[index].Client.Address, StringComparison.OrdinalIgnoreCase) || address.Equals(Bot.Clients[index].Name, StringComparison.OrdinalIgnoreCase)) {
                        if (Bot.Clients[index].Client.IsChannel(channel)) {
                            if ((address == null || address == "*") && !Bot.Clients[index].Client.Channels.Contains(channel)) continue;
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

                            if (!selectedTarget.Contains(target))
                                selectedTarget.Add(target);
                    }
                }

            }

            for (int index = 0; index < Bot.Clients.Count; ++index) {
                if (privmsgTarget[index] != null)
                    Bot.Clients[index].Client.Send("PRIVMSG {0} :{1}", string.Join(",", privmsgTarget[index]), Message);
                if (noticeTarget[index] != null)
                    Bot.Clients[index].Client.Send("NOTICE {0} :{1}", string.Join(",", noticeTarget[index]), Message);
            }
        }

        public virtual void OnSave() {
        }

        public virtual void OnUnload() {
            this.OnSave();
        }

        protected void LogError(string Procedure, Exception ex) {
            Bot.LogError(this.MyKey, Procedure, ex);
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
        public virtual bool OnChannelJoinDeniedBanned(object sender, ChannelDeniedEventArgs e) { return false; }
        public virtual bool OnChannelJoinDeniedFull(object sender, ChannelDeniedEventArgs e) { return false; }
        public virtual bool OnChannelJoinDeniedInvite(object sender, ChannelDeniedEventArgs e) { return false; }
        public virtual bool OnChannelJoinDeniedKey(object sender, ChannelDeniedEventArgs e) { return false; }
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
                if (Bot.getCommandPrefixes((IRCClient) sender, e.Channel).Contains(message[0].ToString()))
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
                if (Bot.getCommandPrefixes((IRCClient) sender, e.Sender.Nickname).Contains(message[0].ToString()))
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
        public virtual bool OnRawLineReceived(object sender, RawEventArgs e) { return false; }
        public virtual bool OnRawLineSent(object sender, RawEventArgs e) { return false; }
        public virtual bool OnUserModesGet(object sender, UserModesEventArgs e) { return false; }
        public virtual bool OnUserModesSet(object sender, UserModesEventArgs e) { return false; }
        public virtual bool OnWallops(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnServerNotice(object sender, PrivateMessageEventArgs e) { return false; }
        public virtual bool OnServerError(object sender, ServerErrorEventArgs e) { return false; }
        public virtual bool OnServerMessage(object sender, ServerMessageEventArgs e) { return false; }
        public virtual bool OnServerMessageUnhandled(object sender, ServerMessageEventArgs e) { return false; }
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
