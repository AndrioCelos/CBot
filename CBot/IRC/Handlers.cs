using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IRC {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class IRCMessageHandlerAttribute : Attribute {
        public string Command { get; }

        public IRCMessageHandlerAttribute(string command) {
            this.Command = command;
        }
    }

    internal static class Handlers {
        [IRCMessageHandler(Replies.RPL_WELCOME)]
        public static void Handle001(IRCClient client, IRCLine line) {
            client.ServerName = line.Prefix;
            if (client.Me.Nickname != line.Parameters[0]) {
                client.OnNicknameChange(new NicknameChangeEventArgs(client.Me, line.Parameters[0]));
                client.Me.SetNickname(line.Parameters[0]);
            }
            client.Users.Add(client.Me);
            client.State = IRCClientState.Online;
        }

        [IRCMessageHandler(Replies.RPL_ISUPPORT)]
        public static void Handle005(IRCClient client, IRCLine line) {
            if (!(line.Parameters.Length != 0 && line.Parameters[0].StartsWith("Try server"))) {
                // RPL_ISUPPORT
                for (int i = 1; i < (line.HasTrail ? line.Parameters.Length - 1 : line.Parameters.Length); ++i) {
                    string[] fields; string key; string value;
                    fields = line.Parameters[i].Split(new char[] { '=' }, 2);
                    if (fields.Length == 2) {
                        key = fields[0];
                        value = fields[1];
                    } else {
                        key = fields[0];
                        value = "";
                    }

                    if (key.StartsWith("-"))
                        client.Extensions[key.Substring(1)] = null;
                    else
                        client.Extensions[key] = value;
                }
            }
        }

        [IRCMessageHandler(Replies.RPL_UMODEIS)]
        public static void Handle221(IRCClient client, IRCLine line) {
            if (line.Parameters[0] == client.Me.Nickname) client.UserModes = line.Parameters[1];
            client.OnUserModesGet(new UserModesEventArgs(line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.RPL_AWAY)]
        public static void Handle301(IRCClient client, IRCLine line) {
            client.OnWhoIsAwayLine(new WhoisAwayEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        /*
        [IRCMessageHandler(Replies.RPL_ISON)]
        public static void Handle303(IRCClient client, IRCLine line) {
            // TODO: This can be trapped as part of a notify feature.
        }
        */

        [IRCMessageHandler(Replies.RPL_UNAWAY)]
        public static void Handle305(IRCClient client, IRCLine line) {
            client.Me.Away = false;
            client.OnAwayCancelled(new AwayEventArgs(line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.RPL_NOWAWAY)]
        public static void Handle306(IRCClient client, IRCLine line) {
            client.Me.Away = true;
            if (client.Me.AwaySince == null) client.Me.AwaySince = DateTime.Now;
            client.OnAwaySet(new AwayEventArgs(line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.RPL_WHOISREGNICK)]
        public static void Handle307(IRCClient client, IRCLine line) {
            if (client.accountKnown) return;

            IRCUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user))
                user.Account = line.Parameters[1];
        }

        [IRCMessageHandler("310")]
        public static void Handle310(IRCClient client, IRCLine line) {
            client.OnWhoIsHelperLine(new WhoisOperEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_WHOISUSER)]
        public static void Handle311(IRCClient client, IRCLine line) {
            if (client.Users.Contains(line.Parameters[1])) {
                IRCUser _user = client.Users[line.Parameters[1]];
                _user.Ident = line.Parameters[2];
                _user.Host = line.Parameters[3];
                _user.FullName = line.Parameters[5];

                // Parse gender codes.
                MatchCollection matches = Regex.Matches(_user.FullName, @"\G\x03(\d\d?)(?:,(\d\d?))?\x0F");
                foreach (Match match in matches) {
                    if (!match.Groups[2].Success) _user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);
                }
            }
            client.OnWhoIsNameLine(new WhoisNameEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], line.Parameters[5]));
        }

        [IRCMessageHandler(Replies.RPL_WHOISSERVER)]
        public static void Handle312(IRCClient client, IRCLine line) {
            client.OnWhoIsServerLine(new WhoisServerEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3]));
        }

        [IRCMessageHandler(Replies.RPL_WHOISOPERATOR)]
        public static void Handle313(IRCClient client, IRCLine line) {
            client.OnWhoIsOperLine(new WhoisOperEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_WHOWASUSER)]
        public static void Handle314(IRCClient client, IRCLine line) {
            client.OnWhoWasNameLine(new WhoisNameEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], line.Parameters[5]));
        }

        /*
        [IRCMessageHandler(Replies.RPL_ENDOFWHO)]
        public static void Handle315(IRCClient client, IRCLine line) {
            // TODO: respond to 315 similarly to 366.
        }
        */

        [IRCMessageHandler(Replies.RPL_WHOISIDLE)]
        public static void Handle317(IRCClient client, IRCLine line) {
            client.OnWhoIsIdleLine(new WhoisIdleEventArgs(line.Parameters[1], TimeSpan.FromSeconds(double.Parse(line.Parameters[2])), IRCClient.DecodeUnixTime(double.Parse(line.Parameters[3])), line.Parameters[4]));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFWHOIS)]
        public static void Handle318(IRCClient client, IRCLine line) {
            client.accountKnown = false;
            client.OnWhoIsEnd(new WhoisEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_WHOISCHANNELS)]
        public static void Handle319(IRCClient client, IRCLine line) {
            client.OnWhoIsChannelLine(new WhoisChannelsEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_LIST)]
        public static void Handle322(IRCClient client, IRCLine line) {
            client.OnChannelList(new ChannelListEventArgs(line.Parameters[1], int.Parse(line.Parameters[2]), line.Parameters[3]));
        }

        [IRCMessageHandler(Replies.RPL_LISTEND)]
        public static void Handle323(IRCClient client, IRCLine line) {
            client.OnChannelListEnd(new ChannelListEndEventArgs(line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.RPL_CHANNELMODEIS)]
        public static void Handle324(IRCClient client, IRCLine line) {
            string channel = line.Parameters[1]; string modes = line.Parameters[2];
            if (client.Channels.Contains(channel)) client.Channels[channel].Modes = modes;
            client.OnChannelModesGet(new ChannelModesGetEventArgs(channel, modes));
        }

        [IRCMessageHandler(Replies.RPL_CREATIONTIME)]
        public static void Handle329(IRCClient client, IRCLine line) {
            var time = IRCClient.DecodeUnixTime(double.Parse(line.Parameters[2]));
            if (client.Channels.Contains(line.Parameters[1])) client.Channels[line.Parameters[1]].Timestamp = time;
            client.OnChannelTimestamp(new ChannelTimestampEventArgs(line.Parameters[1], time));
        }

        [IRCMessageHandler(Replies.RPL_WHOISACCOUNT)]
        public static void Handle330(IRCClient client, IRCLine line) {
            IRCUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user))
                user.Account = line.Parameters[2];
            client.accountKnown = true;
        }

        [IRCMessageHandler(Replies.RPL_TOPIC)]
        public static void Handle332(IRCClient client, IRCLine line) {
            if (client.Channels.Contains(line.Parameters[1])) client.Channels[line.Parameters[1]].Topic = line.Parameters[2];
            client.OnChannelTopic(new ChannelTopicEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_TOPICWHOTIME)]
        public static void Handle333(IRCClient client, IRCLine line) {
            var time = IRCClient.DecodeUnixTime(double.Parse(line.Parameters[3]));
            IRCChannel channel;
            if (client.Channels.TryGetValue(line.Parameters[1], out channel)) {
                channel.TopicSetter = line.Parameters[2];
                channel.TopicStamp = time;
            }
            client.OnChannelTopicStamp(new ChannelTopicStampEventArgs(line.Parameters[1], line.Parameters[2], time));
        }

        [IRCMessageHandler(Replies.RPL_INVITING)]
        public static void Handle341(IRCClient client, IRCLine line) {
            client.OnInviteSent(new ChannelInviteSentEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_INVITELIST)]
        public static void Handle346(IRCClient client, IRCLine line) {
            var time = IRCClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnInviteExemptList(new ChannelModeListEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], time));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFINVITELIST)]
        public static void Handle347(IRCClient client, IRCLine line) {
            client.OnInviteExemptListEnd(new ChannelModeListEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_EXCEPTLIST)]
        public static void Handle348(IRCClient client, IRCLine line) {
            var time = IRCClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnExemptList(new ChannelModeListEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], time));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFEXCEPTLIST)]
        public static void Handle349(IRCClient client, IRCLine line) {
            client.OnExemptListEnd(new ChannelModeListEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_WHOREPLY)]
        public static void Handle352(IRCClient client, IRCLine line) {
            // TODO: populate the user list?
            string[] fields = line.Parameters[7].Split(new char[] { ' ' }, 2);

            var channelName = line.Parameters[1];
            var ident = line.Parameters[2];
            var host = line.Parameters[3];
            var server = line.Parameters[4];
            var nickname = line.Parameters[5];
            var flags = line.Parameters[6];
            var hops = int.Parse(fields[0]);
            var fullName = fields[1];
            IRCUser user = null; IRCChannel channel = null; IRCChannelUser channelUser = null;

            if (client.IsChannel(channelName) && client.Channels.TryGetValue(channelName, out channel)) {
                // We are in a common channel with this person.
                if (!channel.Users.TryGetValue(nickname, out channelUser)) {
                    channelUser = new IRCChannelUser(client, nickname);
                    channel.Users.Add(channelUser);
                }
            }
            if (!client.Users.TryGetValue(nickname, out user)) {
                if (channelUser != null) {
                    user = new IRCUser(client, nickname, ident, host, null, null) { Client = client };
                    user.Channels.Add(channel);
                }
            } else {
                if (channel != null && !user.Channels.Contains(channelName))
                    user.Channels.Add(channel);
            }

            if (user != null) {
                user.Ident = ident;
                user.Host = host;
                user.FullName = fullName;

                MatchCollection matches = Regex.Matches(user.FullName, @"\G\x03(\d\d?)(?:,(\d\d?))?\x0F");
                foreach (Match match in matches) {
                    if (!match.Groups[2].Success) user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);
                }

                user.Oper = false;
                foreach (char flag in flags) {
                    char mode;
                    if (flag == 'H') {
                        user.Away = false;
                    } else if (flag == 'G') {
                        if (!user.Away) {
                            user.Away = true;
                            user.AwayReason = null;
                            user.AwaySince = DateTime.Now;
                        }
                    } else if (flag == '*')
                        user.Oper = true;
                    else if (channelUser != null && client.Extensions.StatusPrefix.TryGetValue(flag, out mode))
                        channelUser.Status.Add(mode);
                }
            }
            client.OnWhoList(new WhoListEventArgs(channelName, ident, host, server, nickname, flags.ToCharArray(), hops, fullName));
        }

        [IRCMessageHandler(Replies.RPL_NAMREPLY)]
        public static void Handle353(IRCClient client, IRCLine line) {
            string[] names = line.Parameters[3].Split(new char[] { ' ' });
            IRCChannel channel;

            if (client.Channels.TryGetValue(line.Parameters[2], out channel)) {
                // We are online in the channel. Mark all remembered users.
                if (channel.WaitingForNamesList % 2 == 0) {
                    foreach (IRCChannelUser channelUser2 in channel.Users)
                        channelUser2.Status.Add('\0');
                    ++channel.WaitingForNamesList;
                }
            }

            foreach (string name in names) {
                ChannelStatus status = new ChannelStatus(client);
                for (int i = 0; i < name.Length; ++i) {
                    char c = name[i];
                    // Some IRC servers use = to prefix +s channels to opers.
                    if (c != '=' && !client.Extensions.StatusPrefix.ContainsKey(c)) {
                        string nickname = name.Substring(i);
                        if (!client.Users.Contains(nickname)) {
                            IRCUser _user = new IRCUser(client, nickname, "*", "*", null, null);
                            _user.Channels.Add(channel);
                            client.Users.Add(_user);
                        }
                        if (channel.Users.Contains(nickname))
                            channel.Users[nickname].Status = ChannelStatus.FromPrefix(client, name.Take(i));
                        else
                            channel.Users.Add(new IRCChannelUser(client, nickname, ChannelStatus.FromPrefix(client, name.Take(i))));
                        break;
                    }
                }
            }

            client.OnNames(new ChannelNamesEventArgs(line.Parameters[2], line.Parameters[3]));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFNAMES)]
        public static void Handle366(IRCClient client, IRCLine line) {
            if (client.Channels.Contains(line.Parameters[1])) {
                if (client.Channels[line.Parameters[1]].WaitingForNamesList % 2 != 0) {
                    for (int i = client.Channels[line.Parameters[1]].Users.Count - 1; i >= 0; --i) {
                        IRCChannelUser _user = client.Channels[line.Parameters[1]].Users[i];
                        if (_user.Status.Contains('\0')) client.Channels[line.Parameters[1]].Users.Remove(_user.Nickname);
                    }
                    --client.Channels[line.Parameters[1]].WaitingForNamesList;
                }
            }

            client.OnNamesEnd(new ChannelModeListEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_BANLIST)]
        public static void Handle367(IRCClient client, IRCLine line) {
            var time = IRCClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnBanList(new ChannelModeListEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], time));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFBANLIST)]
        public static void Handle368(IRCClient client, IRCLine line) {
            client.OnBanListEnd(new ChannelModeListEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.RPL_ENDOFWHOWAS)]
        public static void Handle369(IRCClient client, IRCLine line) {
            client.OnWhoWasEnd(new WhoisEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.ERR_CANNOTSENDTOCHAN)]
        public static void Handle404(IRCClient client, IRCLine line) {
            client.OnChannelMessageDenied(new ChannelDeniedEventArgs(line.Parameters[1], 0, line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.ERR_ERRONEUSNICKNAME)]
        public static void Handle432(IRCClient client, IRCLine line) {
            client.OnNicknameInvalid(new NicknameEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.ERR_NICKNAMEINUSE)]
        public static void Handle433(IRCClient client, IRCLine line) {
            client.OnNicknameTaken(new NicknameEventArgs(line.Parameters[1], line.Parameters[2]));
            /*
            if (!client.IsRegistered && client.Me.Nicknames.Length > 1) {
                for (int i = 0; i < client.Me.Nicknames.Length - 1; ++i) {
                    if (client.Me.Nicknames[i] == line.Parameters[1]) {
                        client.Me.Nickname = client.Me.Nicknames[i + 1];
                        break;
                    }
                }
            }
            */
        }

        [IRCMessageHandler(Replies.ERR_NICKCOLLISION)]
        public static void Handle436(IRCClient client, IRCLine line) {
            client.OnKilled(new PrivateMessageEventArgs(client.Users.Get(line.Prefix, false), client.Me.Nickname, line.Parameters[2]));
        }

        [IRCMessageHandler(Replies.ERR_CHANNELISFULL)]
        public static void Handle471(IRCClient client, IRCLine line) {
            client.OnChannelJoinDenied(new ChannelDeniedEventArgs(line.Parameters[0], ChannelJoinDeniedReason.Limit, line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.ERR_INVITEONLYCHAN)]
        public static void Handle473(IRCClient client, IRCLine line) {
            client.OnChannelJoinDenied(new ChannelDeniedEventArgs(line.Parameters[0], ChannelJoinDeniedReason.InviteOnly, line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.ERR_BANNEDFROMCHAN)]
        public static void Handle474(IRCClient client, IRCLine line) {
            client.OnChannelJoinDenied(new ChannelDeniedEventArgs(line.Parameters[0], ChannelJoinDeniedReason.Banned, line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.ERR_BADCHANNELKEY)]
        public static void Handle475(IRCClient client, IRCLine line) {
            client.OnChannelJoinDenied(new ChannelDeniedEventArgs(line.Parameters[0], ChannelJoinDeniedReason.KeyFailure, line.Parameters[1]));
        }

        [IRCMessageHandler(Replies.RPL_GONEAWAY)]
        public static void Handle598(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch) {
                if (client.Users.Contains(line.Parameters[0])) {
                    client.Users[line.Parameters[0]].Away = true;
                    client.Users[line.Parameters[0]].AwayReason = line.Parameters[4];
                    client.Users[line.Parameters[0]].AwaySince = DateTime.Now;
                }
            }
        }

        [IRCMessageHandler(Replies.RPL_NOTAWAY)]
        public static void Handle599(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch) {
                if (client.Users.Contains(line.Parameters[0])) {
                    client.Users[line.Parameters[0]].Away = false;
                }
            }
        }

        [IRCMessageHandler(Replies.RPL_WATCHOFF)]
        public static void Handle602(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch) {
                if (client.Users.Contains(line.Parameters[1])) {
                    client.Users[line.Parameters[1]].Watched = false;
                    if (client.Users[line.Parameters[1]].Channels.Count == 0)
                        client.Users.Remove(line.Parameters[1]);
                }
            }
        }

        [IRCMessageHandler(Replies.RPL_NOWON)]
        public static void Handle604(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch) {
                if (client.Users.Contains(line.Parameters[1]))
                    client.Users[line.Parameters[1]].Watched = true;
                else
                    client.Users.Add(new IRCUser(client, line.Parameters[1], line.Parameters[2], line.Parameters[3], null, null) { Watched = true });
            }
        }

        [IRCMessageHandler(Replies.RPL_LOGOFF)]
        public static void Handle601(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch)
                client.Users.Remove(line.Parameters[1]);
        }

        [IRCMessageHandler(Replies.RPL_NOWISAWAY)]
        public static void Handle609(IRCClient client, IRCLine line) {
            if (client.Extensions.SupportsWatch) {
                if (client.Users.Contains(line.Parameters[1])) {
                    client.Users[line.Parameters[1]].Away = true;
                    client.Users[line.Parameters[1]].AwayReason = null;
                    client.Users[line.Parameters[1]].AwaySince = DateTime.Now;
                }
            }
        }

        [IRCMessageHandler(Replies.RPL_LOGGEDIN)]
        public static void Handle900(IRCClient client, IRCLine line) {
            client.Me.Account = line.Parameters[2];
        }

        [IRCMessageHandler(Replies.RPL_LOGGEDOUT)]
        public static void Handle901(IRCClient client, IRCLine line) {
            client.Me.Account = null;
        }

        [IRCMessageHandler(Replies.RPL_SASLSUCCESS)]
        public static void Handle903(IRCClient client, IRCLine line) {
            client.Send("CAP END");
        }

        [IRCMessageHandler(Replies.ERR_NICKLOCKED)]
        [IRCMessageHandler(Replies.ERR_SASLFAIL)]
        [IRCMessageHandler(Replies.ERR_SASLTOOLONG)]
        public static void Handle904(IRCClient client, IRCLine line) {
            client.Send("CAP END");
        }

        [IRCMessageHandler("ACCOUNT")]
        public static void HandleAccount(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (line.Parameters[0] == "*")
                user.Account = null;
            else
                user.Account = line.Parameters[0];
        }

        [IRCMessageHandler("AUTHENTICATE")]
        public static void HandleAuthenticate(IRCClient client, IRCLine line) {
            if (line.Parameters[0] == "+" && client.SASLUsername != null && client.SASLPassword != null) {
                // Authenticate using SASL.
                byte[] responseBytes; string response;
                byte[] usernameBytes; byte[] passwordBytes;

                usernameBytes = Encoding.UTF8.GetBytes(client.SASLUsername);
                passwordBytes = Encoding.UTF8.GetBytes(client.SASLPassword);
                responseBytes = new byte[usernameBytes.Length * 2 + passwordBytes.Length + 2];
                usernameBytes.CopyTo(responseBytes, 0);
                usernameBytes.CopyTo(responseBytes, usernameBytes.Length + 1);
                passwordBytes.CopyTo(responseBytes, (usernameBytes.Length + 1) * 2);

                response = Convert.ToBase64String(responseBytes);
                client.Send("AUTHENTICATE :" + response);
            } else {
                // Unrecognised challenge or no credentials given; abort.
                client.Send("AUTHENTICATE *");
                client.Send("CAP END");
            }
        }

        [IRCMessageHandler("CAP")]
        public static void HandleCap(IRCClient client, IRCLine line) {
            string subcommand = line.Parameters[1];
            switch (subcommand.ToUpperInvariant()) {
                case "LS":
                    List<string> supportedCapabilities = new List<string>();
                    MatchCollection matches = Regex.Matches(line.Parameters[2], @"\G *(-)?(~)?(=)?([^ ]+)");
                    foreach (Match match in matches) {
                        if (match.Groups[4].Value == "multi-prefix" ||
                            match.Groups[4].Value == "extended-join" ||
                            match.Groups[4].Value == "account-notify" ||
                            (client.SASLUsername != null && match.Groups[4].Value == "sasl")) {
                            if (!supportedCapabilities.Contains(match.Groups[4].Value))
                                supportedCapabilities.Add(match.Groups[4].Value);
                        }
                    }
                    if (supportedCapabilities.Count > 0)
                        client.Send("CAP REQ :" + string.Join(" ", supportedCapabilities));
                    else
                        client.Send("CAP END");
                    break;
                case "ACK":
                    if (Regex.IsMatch(line.Parameters[2], @"(?<![^ ])[-~=]*sasl(?![^ ])") && client.SASLUsername != null) {
                        // TODO: SASL authentication
                        client.Send("AUTHENTICATE PLAIN");
                    } else
                        client.Send("CAP END");
                    break;
                case "NAK":
                    client.Send("CAP END");
                    break;
            }
        }

        [IRCMessageHandler("CHGHOST")]
        public static void HandleChgHost(IRCClient client, IRCLine line) {
            IRCUser user;
            string nickname = line.Prefix.Substring(0, line.Prefix.IndexOf('!'));
            if (client.Users.TryGetValue(nickname, out user)) {
                user.Ident = line.Parameters[0];
                user.Host = line.Parameters[1];
            }
        }

        [IRCMessageHandler("ERROR")]
        public static void HandleError(IRCClient client, IRCLine line) {
            client.OnServerError(new ServerErrorEventArgs(line.Parameters[0]));
        }

        [IRCMessageHandler("INVITE")]
        public static void HandleInvite(IRCClient client, IRCLine line) {
            client.OnInvite(new ChannelInviteEventArgs(client.Users.Get(line.Prefix, false), line.Parameters[0], line.Parameters[1]));
        }

        [IRCMessageHandler("JOIN")]
        public static void HandleJoin(IRCClient client, IRCLine line) {
            bool onChannel = client.Channels.Contains(line.Parameters[0]);
            IRCUser user;

            if (line.Parameters.Length == 3) {
                // Extended join
                user = client.Users.Get(line.Prefix, line.Parameters[1], line.Parameters[2], onChannel);
            } else
                user = client.Users.Get(line.Prefix, onChannel);

            if (!onChannel && client.CaseMappingComparer.Equals(user.Nickname, client.Me.Nickname)) {
                var newChannel = new IRCChannel(line.Parameters[0], client);
                newChannel.Users.Add(new IRCChannelUser(client, user.Nickname));
                client.Channels.Add(newChannel);
                user.Channels.Add(newChannel);
            } else {
                client.Channels[line.Parameters[0]].Users.Add(new IRCChannelUser(client, user.Nickname));
                user.Channels.Add(client.Channels[line.Parameters[0]]);
            }
            client.OnChannelJoin(new ChannelJoinEventArgs(user, line.Parameters[0]));
        }

        [IRCMessageHandler("KICK")]
        public static void HandleKick(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (line.Parameters[1].Equals(client.Me.Nickname, StringComparison.OrdinalIgnoreCase)) {
                client.Channels.Remove(line.Parameters[0]);
            }
            client.OnChannelKick(new ChannelKickEventArgs(user, line.Parameters[0], client.Channels[line.Parameters[0]].Users[line.Parameters[1]], line.Parameters.Length >= 3 ? line.Parameters[2] : null));
            if (client.Channels[line.Parameters[0]].Users.Contains(line.Parameters[1]))
                client.Channels[line.Parameters[0]].Users.Remove(line.Parameters[1]);
        }

        [IRCMessageHandler("KILL")]
        public static void HandleKill(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (line.Parameters[0].Equals(client.Me.Nickname, StringComparison.OrdinalIgnoreCase)) {
                client.OnKilled(new PrivateMessageEventArgs(user, client.Me.Nickname, line.Parameters[1]));
            }
        }

        [IRCMessageHandler("MODE")]
        public static void HandleMode(IRCClient client, IRCLine line) {
            if (client.IsChannel(line.Parameters[0])) {
                int index = 2; bool direction = true;
                foreach (char c in line.Parameters[1]) {
                    if (c == '+')
                        direction = true;
                    else if (c == '-')
                        direction = false;
                    else if (client.Extensions.ChanModes.TypeA.Contains(c))
                        client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, line.Parameters[index++]);
                    else if (client.Extensions.ChanModes.TypeB.Contains(c))
                        client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, line.Parameters[index++]);
                    else if (client.Extensions.ChanModes.TypeC.Contains(c)) {
                        if (direction)
                            client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, line.Parameters[index++]);
                        else
                            client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, null);
                    } else if (client.Extensions.ChanModes.TypeD.Contains(c))
                        client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, null);
                    else if (client.Extensions.StatusPrefix.ContainsKey(c))
                        client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, line.Parameters[index++]);
                    else
                        client.HandleChannelMode(line.Prefix, line.Parameters[0], direction, c, null);
                }
            }
        }

        [IRCMessageHandler("NICK")]
        public static void HandleNick(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (user.Nickname.Equals(client.Me.Nickname, StringComparison.OrdinalIgnoreCase)) {
                client.Me.SetNickname(line.Parameters[0]);
            }
            client.OnNicknameChange(new NicknameChangeEventArgs(user, line.Parameters[0]));

            foreach (IRCChannel _channel in client.Channels) {
                if (_channel.Users.Contains(user.Nickname)) {
                    _channel.Users.Remove(user.Nickname);
                    // TODO: Fix this.
                    _channel.Users.Add(new IRCChannelUser(client, line.Parameters[0]));
                }
            }

            if (client.Users.TryGetValue(user.Nickname, out user)) {
                client.Users.Remove(user.Nickname);
                user.Nickname = line.Parameters[0];
                client.Users.Add(user);
            }
        }

        [IRCMessageHandler("NOTICE")]
        public static void HandleNotice(IRCClient client, IRCLine line) {
            if (client.IsChannel(line.Parameters[0])) {
                client.OnChannelNotice(new ChannelMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), line.Parameters[0], line.Parameters[1]));
            } else if (line.Prefix == null || line.Prefix.Split(new char[] { '!' }, 2)[0].Contains(".")) {
                // TODO: fix client
                client.OnServerNotice(new PrivateMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), line.Parameters[0], line.Parameters[1]));
            } else {
                client.OnPrivateNotice(new PrivateMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), line.Parameters[0], line.Parameters[1]));
            }
        }

        [IRCMessageHandler("PART")]
        public static void HandlePart(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            client.OnChannelPart(new ChannelPartEventArgs(user, line.Parameters[0], line.Parameters.Length == 1 ? null : line.Parameters[1]));
            if (user.Nickname.Equals(client.Me.Nickname, StringComparison.OrdinalIgnoreCase)) {
                client.Channels.Remove(line.Parameters[0]);
            }
            if (client.Channels[line.Parameters[0]].Users.Contains(user.Nickname))
                client.Channels[line.Parameters[0]].Users.Remove(user.Nickname);
        }

        [IRCMessageHandler("PING")]
        public static void HandlePing(IRCClient client, IRCLine line) {
            client.OnPingRequest(new PingEventArgs(line.Parameters.Length == 0 ? null : line.Parameters[0]));
            client.Send(line.Parameters.Length == 0 ? "PONG" : "PONG :" + line.Parameters[0]);
        }

        [IRCMessageHandler("PONG")]
        public static void HandlePong(IRCClient client, IRCLine line) {
            client.OnPingReply(new PingEventArgs(line.Prefix));
        }

        [IRCMessageHandler("PRIVMSG")]
        public static void HandlePrivmsg(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);

            if (client.IsChannel(line.Parameters[0])) {
                // It's a channel message.
                if (line.Parameters[1].Length > 1 && line.Parameters[1].StartsWith("\u0001") && line.Parameters[1].EndsWith("\u0001")) {
                    string CTCPMessage = line.Parameters[1].Trim(new char[] { '\u0001' });
                    string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                    if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                        client.OnChannelAction(new ChannelMessageEventArgs(user, line.Parameters[0], fields.ElementAtOrDefault(1) ?? ""));
                    } else {
                        client.OnChannelCTCP(new ChannelMessageEventArgs(user, line.Parameters[0], CTCPMessage));
                    }
                } else {
                    client.OnChannelMessage(new ChannelMessageEventArgs(user, line.Parameters[0], line.Parameters[1]));
                }
            } else {
                // It's a private message.
                if (line.Parameters[1].Length > 1 && line.Parameters[1].StartsWith("\u0001") && line.Parameters[1].EndsWith("\u0001")) {
                    string CTCPMessage = line.Parameters[1].Trim(new char[] { '\u0001' });
                    string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                    if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                        client.OnPrivateAction(new PrivateMessageEventArgs(user, line.Parameters[0], fields.ElementAtOrDefault(1) ?? ""));
                    } else {
                        client.OnPrivateCTCP(new PrivateMessageEventArgs(user, line.Parameters[0], CTCPMessage));
                    }
                } else {
                    client.OnPrivateMessage(new PrivateMessageEventArgs(user, line.Parameters[0], line.Parameters[1]));
                }
            }
        }

        [IRCMessageHandler("QUIT")]
        public static void HandleQuit(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            client.OnUserQuit(new QuitEventArgs(user, line.Parameters[0]));
            if (user.Nickname.Equals(client.Me.Nickname, StringComparison.OrdinalIgnoreCase)) {
                client.Channels.Clear();
            }
            foreach (IRCChannel _channel in client.Channels) {
                if (_channel.Users.Contains(user.Nickname))
                    _channel.Users.Remove(user.Nickname);
            }
        }

        [IRCMessageHandler("TOPIC")]
        public static void HandleTopic(IRCClient client, IRCLine line) {
            var user = client.Users.Get(line.Prefix, false);
            client.OnChannelTopicChange(new ChannelTopicChangeEventArgs(new IRCChannelUser(client, user.Nickname), line.Parameters[0], line.Parameters[1]));
        }
    }
}
