using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using static IRC.Replies;

namespace IRC {
    /// <summary>
    /// Indicates that a method is an IRC message handler.
    /// </summary>
    /// <seealso cref="IrcClient.RegisterHandlers(Type)"/>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class IrcMessageHandlerAttribute : Attribute {
        /// <summary>The reply or numeric that this procedure should handle.</summary>
        public string Reply { get; }

        /// <summary>Initializes a new <see cref="IrcMessageHandlerAttribute"/> for the specified reply.</summary>
        /// <param name="reply">The reply or numeric that should be handled.</param>
        public IrcMessageHandlerAttribute(string reply) {
            this.Reply = reply;
        }
    }

    internal static class Handlers {
        [IrcMessageHandler(Replies.RPL_WELCOME)]
        public static void HandleWelcome(IrcClient client, IrcLine line) {  // 001
            client.ServerName = line.Prefix;
            if (client.Me.Nickname != line.Parameters[0]) {
                client.OnNicknameChange(new NicknameChangeEventArgs(client.Me, line.Parameters[0]));
                client.Users.Remove(client.Me);
                ((IrcUser) client.Me).Nickname = line.Parameters[0];
                client.Users.Add(client.Me);
            }

            bool continuing = !(client.RequireSaslAuthentication && client.Me.Account == null);
            client.State = IrcClientState.ReceivingServerInfo;
            client.OnRegistered(new RegisteredEventArgs(continuing));

            if (!continuing) {
                client.disconnectReason = DisconnectReason.SaslAuthenticationFailed;
                client.Send("QUIT :SASL authentication failed.");
            }
        }

        [IrcMessageHandler(Replies.RPL_MYINFO)]
        public static void HandleMyInfo(IrcClient client, IrcLine line) {  // 004
            client.ServerName = line.Parameters[1];

            // Get supported modes.
            client.SupportedUserModes.Clear();
            foreach (char c in line.Parameters[3])
                client.SupportedUserModes.Add(c);

            // We can only assume that channel modes not defined in RFC 2811 are type D at this point.
            List<char> modesA = new List<char>(), modesB = new List<char>(), modesC = new List<char>(), modesD = new List<char>(), modesS = new List<char>();
            foreach (char c in line.Parameters[4]) {
                switch (ChannelModes.RFC2811.ModeType(c)) {
                    case 'A': modesA.Add(c); break;
                    case 'B': modesB.Add(c); break;
                    case 'C': modesC.Add(c); break;
                    case 'D': modesD.Add(c); break;
                    case 'S': modesS.Add(c); break;
                    default: modesD.Add(c); break;
                }
            }
            client.Extensions.ChanModes = new ChannelModes(modesA, modesB, modesC, modesD, modesS);
        }

        [IrcMessageHandler(Replies.RPL_ISUPPORT)]
        public static void HandleISupport(IrcClient client, IrcLine line) {  // 005
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

        [IrcMessageHandler(Replies.RPL_UMODEIS)]
        public static void HandleUserMode(IrcClient client, IrcLine line) {  // 221
            client.UserModes.Clear();

            bool direction = true;
            foreach (char c in line.Parameters[1]) {
                if (c == '+') direction = true;
                else if (c == '-') direction = false;
                else {
                    if (direction) client.UserModes.Add(c);
                    else client.UserModes.Remove(c);
                }
            }
            client.OnUserModesGet(new UserModesEventArgs(line.Parameters[1]));
        }

        [IrcMessageHandler(Replies.RPL_AWAY)]
        public static void HandleAway(IrcClient client, IrcLine line) {  // 301
            client.OnAwayMessage(new AwayMessageEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        /*
        [IRCMessageHandler(Replies.RPL_ISON)]
        public static void HandleIson(IRCClient client, IRCLine line) {  // 303
            // TODO: This can be trapped as part of a notify feature.
        }
        */

        [IrcMessageHandler(Replies.RPL_UNAWAY)]
        public static void HandleUnAway(IrcClient client, IrcLine line) {  // 305
            client.Me.Away = false;
            client.OnAwayCancelled(new AwayEventArgs(line.Parameters[1]));
        }

        [IrcMessageHandler(Replies.RPL_NOWAWAY)]
        public static void HandleNowAway(IrcClient client, IrcLine line) {  // 306
            client.Me.Away = true;
            if (client.Me.AwaySince == null) client.Me.AwaySince = DateTime.Now;
            client.OnAwaySet(new AwayEventArgs(line.Parameters[1]));
        }

        [IrcMessageHandler(Replies.RPL_WHOISREGNICK)]
        public static void HandleWhoisRegNick(IrcClient client, IrcLine line) {  // 307
            // This reply only says that the user is registered to services, but not the account name.
            // RPL_WHOISACCOUNT (330) gives the account name, but some servers send both.
            // If only RPL_WHOISREGNICK is sent, the account name is the user's nickname.
            if (client.accountKnown) return;

            IrcUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user))
                user.Account = line.Parameters[1];
        }

        [IrcMessageHandler("310")]
        public static void HandleWhoisHelper(IrcClient client, IrcLine line) {  // 310
            client.OnWhoIsHelperLine(new WhoisOperEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_WHOISUSER)]
        public static void HandleWhoisName(IrcClient client, IrcLine line) {  // 311
            IrcUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user)) {
                user.Ident = line.Parameters[2];
                user.Host = line.Parameters[3];
                user.FullName = line.Parameters[5];

                // Parse gender codes.
                var match = Regex.Match(user.FullName, @"^\x03(\d\d?)\x0F");
                if (match.Success) user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);
            }
            client.OnWhoIsNameLine(new WhoisNameEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], line.Parameters[5]));
        }

        [IrcMessageHandler(Replies.RPL_WHOISSERVER)]
        public static void HandleWhoisServer(IrcClient client, IrcLine line) {  // 312
            client.OnWhoIsServerLine(new WhoisServerEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3]));
        }

        [IrcMessageHandler(Replies.RPL_WHOISOPERATOR)]
        public static void HandleWhoisOper(IrcClient client, IrcLine line) {  // 313
            IrcUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user))
                user.Oper = true;

            client.OnWhoIsOperLine(new WhoisOperEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_WHOWASUSER)]
        public static void HandleWhowasName(IrcClient client, IrcLine line) {  // 314
            client.OnWhoWasNameLine(new WhoisNameEventArgs(line.Parameters[1], line.Parameters[2], line.Parameters[3], line.Parameters[5]));
        }

        /*
        [IRCMessageHandler(Replies.RPL_ENDOFWHO)]
        public static void HandleWhoEnd(IRCClient client, IRCLine line) {  // 315
            // TODO: respond to 315 similarly to 366.
        }
        */

        [IrcMessageHandler(Replies.RPL_WHOISIDLE)]
        public static void HandleWhoisIdle(IrcClient client, IrcLine line) {  // 317
            client.OnWhoIsIdleLine(new WhoisIdleEventArgs(line.Parameters[1], TimeSpan.FromSeconds(double.Parse(line.Parameters[2])), IrcClient.DecodeUnixTime(double.Parse(line.Parameters[3])), line.Parameters[4]));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFWHOIS)]
        public static void HandleWhoisEnd(IrcClient client, IrcLine line) {  // 318
            client.accountKnown = false;
            client.OnWhoIsEnd(new WhoisEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_WHOISCHANNELS)]
        public static void HandleWhoisChannels(IrcClient client, IrcLine line) {  // 319
            client.OnWhoIsChannelLine(new WhoisChannelsEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_LIST)]
        public static void HandleList(IrcClient client, IrcLine line) {  // 322
            client.OnChannelList(new ChannelListEventArgs(line.Parameters[1], int.Parse(line.Parameters[2]), line.Parameters[3]));
        }

        [IrcMessageHandler(Replies.RPL_LISTEND)]
        public static void HandleListEnd(IrcClient client, IrcLine line) {  // 323
            client.OnChannelListEnd(new ChannelListEndEventArgs(line.Parameters[1]));
        }

        [IrcMessageHandler(Replies.RPL_CHANNELMODEIS)]
        public static void HandleChannelModes(IrcClient client, IrcLine line) {  // 324
            var user = client.Users.Get(line.Prefix, false);
            var channel = client.Channels.Get(line.Parameters[1]);
            channel.Modes.Clear();
            client.HandleChannelModes(user, channel, line.Parameters[2], line.Parameters.Skip(3), false);
        }

        [IrcMessageHandler(Replies.RPL_CREATIONTIME)]
        public static void HandleChannelCreationTime(IrcClient client, IrcLine line) {  // 329
            var time = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[2]));
            if (client.Channels.Contains(line.Parameters[1])) client.Channels[line.Parameters[1]].Timestamp = time;
            client.OnChannelTimestamp(new ChannelTimestampEventArgs(client.Channels.Get(line.Parameters[1]), time));
        }

        [IrcMessageHandler(Replies.RPL_WHOISACCOUNT)]
        public static void HandleWhoisAccount(IrcClient client, IrcLine line) {  // 330
            IrcUser user;
            if (client.Users.TryGetValue(line.Parameters[1], out user))
                user.Account = line.Parameters[2];
            client.accountKnown = true;
        }

        [IrcMessageHandler(Replies.RPL_TOPIC)]
        public static void HandleChannelTopic(IrcClient client, IrcLine line) {  // 332
            if (client.Channels.Contains(line.Parameters[1])) client.Channels[line.Parameters[1]].Topic = line.Parameters[2];
            client.OnChannelTopic(new ChannelTopicEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_TOPICWHOTIME)]
        public static void HandleTopicStamp(IrcClient client, IrcLine line) {  // 333
            var time = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[3]));
            IrcChannel channel;
            if (client.Channels.TryGetValue(line.Parameters[1], out channel)) {
                channel.TopicSetter = line.Parameters[2];
                channel.TopicStamp = time;
            }
            client.OnChannelTopicStamp(new ChannelTopicStampEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2], time));
        }

        [IrcMessageHandler(Replies.RPL_INVITING)]
        public static void HandleInviting(IrcClient client, IrcLine line) {  // 341
            client.OnInviteSent(new InviteSentEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_INVITELIST)]
        public static void HandleInviteList(IrcClient client, IrcLine line) {  // 346
            var time = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnInviteExemptList(new ChannelModeListEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2], line.Parameters[3], time));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFINVITELIST)]
        public static void HandleInviteListEnd(IrcClient client, IrcLine line) {  // 347
            client.OnInviteExemptListEnd(new ChannelModeListEndEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_EXCEPTLIST)]
        public static void HandleExceptionList(IrcClient client, IrcLine line) {  // 348
            var time = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnExemptList(new ChannelModeListEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2], line.Parameters[3], time));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFEXCEPTLIST)]
        public static void HandleExceptionListEnd(IrcClient client, IrcLine line) {  // 349
            client.OnExemptListEnd(new ChannelModeListEndEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_WHOREPLY)]
        public static void HandleWhoReply(IrcClient client, IrcLine line) {  // 352
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
            IrcUser user = null; IrcChannel channel = null; IrcChannelUser channelUser = null;

            if (client.IsChannel(channelName) && client.Channels.TryGetValue(channelName, out channel)) {
                // We are in a common channel with this person.
                if (!channel.Users.TryGetValue(nickname, out channelUser)) {
                    channelUser = new IrcChannelUser(client, channel, nickname);
                    channel.Users.Add(channelUser);
                }
            }
            if (!client.Users.TryGetValue(nickname, out user)) {
                if (channelUser != null) {
                    user = new IrcUser(client, nickname, ident, host, null, fullName);
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

                var match = Regex.Match(user.FullName, @"^\x03(\d\d?)\x0F");
                if (match.Success) user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);

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

        [IrcMessageHandler(Replies.RPL_NAMREPLY)]
        public static void HandleNamesReply(IrcClient client, IrcLine line) {  // 353
            if (line.Parameters[2] != "*") {
                IrcChannel channel;
                client.Channels.TryGetValue(line.Parameters[2], out channel);

                HashSet<string> pendingNames;
                if (!client.pendingNames.TryGetValue(line.Parameters[2], out pendingNames)) {
                    // Make a set of the remembered users, so we can check for any not listed.
                    pendingNames = new HashSet<string>(channel.Users.Select(user => user.Nickname));
                    client.pendingNames[line.Parameters[2]] = pendingNames;
                }

                if (channel != null) {
                    foreach (var name in line.Parameters[3].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) {
                        // Some servers include a space after the last name.
                        for (int i = 0; i < name.Length; ++i) {
                            char c = name[i];
                            // Some IRC servers use = to prefix +s channels to opers.
                            // TODO: Find a better way to distinguish prefixes. Some networks allow wacky characters like '$' in nicknames.
                            if (c != '=' && !client.Extensions.StatusPrefix.ContainsKey(c)) {
                                var user = client.Users.Get(name.Substring(i), true);
                                // client.Users.Get will update the user with the hostmask from userhost-in-names, if present.
                                if (!user.Channels.Contains(channel)) user.Channels.Add(channel);

                                IrcChannelUser channelUser;
                                if (channel.Users.TryGetValue(user.Nickname, out channelUser)) {
                                    channelUser.Status = ChannelStatus.FromPrefix(client, name.Take(i));
                                    pendingNames.Remove(user.Nickname);
                                } else
                                    channel.Users.Add(new IrcChannelUser(client, channel, user.Nickname, ChannelStatus.FromPrefix(client, name.Take(i))));

                                break;
                            }
                        }
                    }
                }
            }

            client.OnNames(new ChannelNamesEventArgs(client.Channels.Get(line.Parameters[2]), line.Parameters[3]));
        }

        [IrcMessageHandler(RPL_ENDOFNAMES)]
        public static void HandleNamesEnd(IrcClient client, IrcLine line) {  // 366
            if (line.Parameters[1] != "*") {
                IrcChannel channel; HashSet<string> pendingNames;
                if (client.pendingNames.TryGetValue(line.Parameters[1], out pendingNames) && client.Channels.TryGetValue(line.Parameters[1], out channel)) {
                    // Remove any users who weren't in the NAMES list.
                    foreach (string name in pendingNames)
                        channel.Users.Remove(name);

                    client.pendingNames.Remove(line.Parameters[1]);
                }
            }

            client.OnNamesEnd(new ChannelModeListEndEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_BANLIST)]
        public static void HandleBanList(IrcClient client, IrcLine line) {  // 367
            var time = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
            client.OnBanList(new ChannelModeListEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2], line.Parameters[3], time));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFBANLIST)]
        public static void HandleBanListEnd(IrcClient client, IrcLine line) {  // 368
            client.OnBanListEnd(new ChannelModeListEndEventArgs(client.Channels.Get(line.Parameters[1]), line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFWHOWAS)]
        public static void HandleWhowasEnd(IrcClient client, IrcLine line) {  // 369
            client.OnWhoWasEnd(new WhoisEndEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_ENDOFMOTD)]
        public static void HandleEndOfMotd(IrcClient client, IrcLine line) {  // 376
            client.State = IrcClientState.Online;
        }

        [IrcMessageHandler(Replies.ERR_CANNOTSENDTOCHAN)]
        public static void HandleCannotSendToChan(IrcClient client, IrcLine line) {  // 404
            client.OnChannelMessageDenied(new ChannelJoinDeniedEventArgs(line.Parameters[1], 0, line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_NOMOTD)]
        public static void HandleNoMotd(IrcClient client, IrcLine line) {  // 422
            client.State = IrcClientState.Online;
        }

        [IrcMessageHandler(Replies.ERR_ERRONEUSNICKNAME)]
        public static void HandleErroneousNickname(IrcClient client, IrcLine line) {  // 432
            client.OnNicknameInvalid(new NicknameEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_NICKNAMEINUSE)]
        public static void HandleNicknameInUse(IrcClient client, IrcLine line) {  // 433
            client.OnNicknameTaken(new NicknameEventArgs(line.Parameters[1], line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_CHANNELISFULL)]
        public static void HandleChannelFull(IrcClient client, IrcLine line) {  // 471
            client.OnChannelJoinDenied(new ChannelJoinDeniedEventArgs(line.Parameters[1], ChannelJoinDeniedReason.Limit, line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_INVITEONLYCHAN)]
        public static void HandleChannelInviteOnly(IrcClient client, IrcLine line) {  // 473
            client.OnChannelJoinDenied(new ChannelJoinDeniedEventArgs(line.Parameters[1], ChannelJoinDeniedReason.InviteOnly, line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_BANNEDFROMCHAN)]
        public static void HandleChannelBanned(IrcClient client, IrcLine line) {  // 474
            client.OnChannelJoinDenied(new ChannelJoinDeniedEventArgs(line.Parameters[1], ChannelJoinDeniedReason.Banned, line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.ERR_BADCHANNELKEY)]
        public static void HandleChannelKeyFailure(IrcClient client, IrcLine line) {  // 475
            client.OnChannelJoinDenied(new ChannelJoinDeniedEventArgs(line.Parameters[1], ChannelJoinDeniedReason.KeyFailure, line.Parameters[2]));
        }

        [IrcMessageHandler(Replies.RPL_GONEAWAY)]
        public static void HandleWatchAway(IrcClient client, IrcLine line) {  // 598
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user)) {
                    user.Away = true;
                    user.AwayReason = line.Parameters[5];
                    user.AwaySince = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
                }
            }
        }

        [IrcMessageHandler(Replies.RPL_NOTAWAY)]
        public static void HandleWatchBack(IrcClient client, IrcLine line) {  // 599
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user)) {
                    user.Away = false;
                }
            }
        }

        [IrcMessageHandler(Replies.RPL_WATCHOFF)]
        public static void HandleWatchRemoved(IrcClient client, IrcLine line) {  // 602
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user)) {
                    user.Watched = false;
                    if (user.Channels.Count == 0) {
                        client.Users.Remove(line.Parameters[1]);
                        client.OnUserDisappeared(new IrcUserEventArgs(user));
                    }
                }
            }
        }

        [IrcMessageHandler(Replies.RPL_LOGON)]
        [IrcMessageHandler(Replies.RPL_NOWON)]
        public static void HandleWatchOnline(IrcClient client, IrcLine line) {  // 600, 604
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user))
                    user.Watched = true;
                else
                    client.Users.Add(new IrcUser(client, line.Parameters[1], line.Parameters[2], line.Parameters[3], null, null) { Watched = true });
            }
        }

        [IrcMessageHandler(Replies.RPL_LOGOFF)]
        [IrcMessageHandler(Replies.RPL_NOWOFF)]
        public static void HandleWatchOffline(IrcClient client, IrcLine line) {  // 601, 605
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user) && user.Channels.Count == 0) {
                    // Some IRC servers send RPL_LOGOFF before the QUIT message.
                    user.Watched = false;
                    client.Users.Remove(line.Parameters[1]);
                    client.OnUserQuit(new QuitEventArgs(user, null));
                }
            }
        }

        [IrcMessageHandler(Replies.RPL_NOWISAWAY)]
        public static void HandleWatchIsAway(IrcClient client, IrcLine line) {  // 609
            if (client.Extensions.SupportsWatch) {
                IrcUser user;
                if (client.Users.TryGetValue(line.Parameters[1], out user)) {
                    user.Away = true;
                    user.AwayReason = null;
                    user.AwaySince = IrcClient.DecodeUnixTime(double.Parse(line.Parameters[4]));
                }
            }
        }

        [IrcMessageHandler(Replies.RPL_LOGGEDIN)]
        public static void HandleLoggedIn(IrcClient client, IrcLine line) {  // 900
            client.Me.Account = line.Parameters[2];
        }

        [IrcMessageHandler(Replies.RPL_LOGGEDOUT)]
        public static void HandleLoggedOut(IrcClient client, IrcLine line) {  // 901
            client.Me.Account = null;
        }

        [IrcMessageHandler(Replies.RPL_SASLSUCCESS)]
        public static void HandleSaslSuccess(IrcClient client, IrcLine line) {  // 903
            client.Send("CAP END");
        }

        [IrcMessageHandler(Replies.ERR_NICKLOCKED)]
        [IrcMessageHandler(Replies.ERR_SASLFAIL)]
        [IrcMessageHandler(Replies.ERR_SASLTOOLONG)]
        public static void HandleSaslFailure(IrcClient client, IrcLine line) {  // 902, 904, 905
            if (client.RequireSaslAuthentication) {
                client.disconnectReason = DisconnectReason.SaslAuthenticationFailed;
                client.Send("QUIT :SASL authentication failed.");
            } else
                client.Send("CAP END");
        }

        [IrcMessageHandler("ACCOUNT")]
        public static void HandleAccount(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (line.Parameters[0] == "*")
                user.Account = null;
            else
                user.Account = line.Parameters[0];
        }

        [IrcMessageHandler("AUTHENTICATE")]
        public static void HandleAuthenticate(IrcClient client, IrcLine line) {
            // TODO: support other authentication mechanisms.
            if (line.Parameters[0] == "+" && client.SaslUsername != null && client.SaslPassword != null) {
                // Authenticate using SASL.
                byte[] responseBytes; string response;
                byte[] usernameBytes; byte[] passwordBytes;

                usernameBytes = Encoding.UTF8.GetBytes(client.SaslUsername);
                passwordBytes = Encoding.UTF8.GetBytes(client.SaslPassword);
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

        [IrcMessageHandler("CAP")]
        public static void HandleCap(IrcClient client, IrcLine line) {
            string subcommand = line.Parameters[1];
            switch (subcommand.ToUpperInvariant()) {
                case "LS":
                    if (client.State < IrcClientState.ReceivingServerInfo) {
                        bool sasl = false;
                        List<string> supportedCapabilities = new List<string>();
                        MatchCollection matches = Regex.Matches(line.Parameters[2], @"\G *(-)?(~)?(=)?([^ ]+)");
                        foreach (Match match in matches) {
                            if (match.Groups[4].Value == "multi-prefix" ||
                                match.Groups[4].Value == "extended-join" ||
                                match.Groups[4].Value == "account-notify") {

                                if (!supportedCapabilities.Contains(match.Groups[4].Value))
                                    supportedCapabilities.Add(match.Groups[4].Value);
                            } else if (client.SaslUsername != null && match.Groups[4].Value == "sasl") {
                                sasl = true;
                                if (!supportedCapabilities.Contains(match.Groups[4].Value))
                                    supportedCapabilities.Add(match.Groups[4].Value);
                            }
                        }

                        if (client.RequireSaslAuthentication && !sasl) {
                            client.disconnectReason = DisconnectReason.SaslAuthenticationFailed;
                            client.Send("QUIT :SASL is not supported.");
                        } else if (supportedCapabilities.Count > 0)
                            client.Send("CAP REQ :" + string.Join(" ", supportedCapabilities));
                        else
                            client.Send("CAP END");
                    }

                    break;
                case "ACK":
                    if (client.State < IrcClientState.ReceivingServerInfo &&
                        Regex.IsMatch(line.Parameters[2], @"(?<![^ ])[-~=]*sasl(?![^ ])") && client.SaslUsername != null) {
                        // TODO: SASL authentication
                        client.Send("AUTHENTICATE PLAIN");
                    } else
                        client.Send("CAP END");
                    break;
                case "NAK":
                    if (client.State < IrcClientState.ReceivingServerInfo)
                        client.Send("CAP END");
                    break;
            }
        }

        [IrcMessageHandler("CHGHOST")]
        public static void HandleChgHost(IrcClient client, IrcLine line) {
            IrcUser user;
            string nickname = Hostmask.GetNickname(line.Prefix);
            if (client.Users.TryGetValue(nickname, out user)) {
                user.Ident = line.Parameters[0];
                user.Host = line.Parameters[1];
            }
        }

        [IrcMessageHandler("ERROR")]
        public static void HandleError(IrcClient client, IrcLine line) {
            client.OnServerError(new ServerErrorEventArgs(line.Parameters[0]));
        }

        [IrcMessageHandler("INVITE")]
        public static void HandleInvite(IrcClient client, IrcLine line) {
            client.OnInvite(new InviteEventArgs(client.Users.Get(line.Prefix, false), line.Parameters[0], line.Parameters[1]));
        }

        [IrcMessageHandler("JOIN")]
        public static void HandleJoin(IrcClient client, IrcLine line) {
            IrcUser user; IrcChannel channel; Task namesTask;
            bool onChannel = client.Channels.TryGetValue(line.Parameters[0], out channel);

            if (line.Parameters.Length == 3) {
                // Extended join
                user = client.Users.Get(line.Prefix, line.Parameters[1], line.Parameters[2], onChannel);
            } else
                user = client.Users.Get(line.Prefix, onChannel);

            if (!onChannel && user.IsMe) {
                if (client.Users.Count == 0) client.Users.Add(client.Me);

                channel = new IrcChannel(client, line.Parameters[0]);
                channel.Users.Add(new IrcChannelUser(client, channel, user.Nickname) { JoinTime = DateTime.Now });
                client.Channels.Add(channel);

                var asyncRequest = new AsyncRequest.VoidAsyncRequest(client, null, RPL_ENDOFNAMES, new[] { null, line.Parameters[0] });
                client.AddAsyncRequest(asyncRequest);
                namesTask = asyncRequest.Task;
            } else {
                if (!user.Channels.Contains(line.Parameters[0])) {
                    if (channel == null) channel = new IrcChannel(client, line.Parameters[0]);
                    channel.Users.Add(new IrcChannelUser(client, channel, user.Nickname) { JoinTime = DateTime.Now });
                    user.Channels.Add(channel);
                }
                namesTask = null;
            }
            client.OnChannelJoin(new ChannelJoinEventArgs(user, channel, namesTask));
        }

        [IrcMessageHandler("KICK")]
        public static void HandleKick(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            IrcChannel channel = client.Channels.Get(line.Parameters[0]);
            IrcChannelUser target;
            if (!channel.Users.TryGetValue(line.Parameters[1], out target)) target = new IrcChannelUser(client, channel, line.Parameters[1]);

            var targetUser = target.User;
            IrcUser[] disappearedUsers;
            if (targetUser != null) disappearedUsers = client.RemoveUserFromChannel(channel, targetUser);
            else disappearedUsers = null;

            client.OnChannelKick(new ChannelKickEventArgs(user, channel, target, line.Parameters.Length >= 3 ? line.Parameters[2] : null));
            client.OnChannelLeave(new ChannelPartEventArgs(user, channel, "Kicked out by " + user.Nickname + ": " + (line.Parameters.Length >= 3 ? line.Parameters[2] : null)));

            if (disappearedUsers != null) {
                foreach (var disappearedUser in disappearedUsers) {
                    client.Users.Remove(disappearedUser);
                    client.OnUserDisappeared(new IrcUserEventArgs(disappearedUser));
                }
            }
        }

        [IrcMessageHandler("KILL")]
        public static void HandleKill(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (client.CaseMappingComparer.Equals(line.Parameters[0], client.Me.Nickname)) {
                client.OnKilled(new PrivateMessageEventArgs(user, client.Me.Nickname, line.Parameters[1]));
            }
        }

        [IrcMessageHandler("MODE")]
        public static void HandleMode(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            if (client.IsChannel(line.Parameters[0])) {
                var channel = client.Channels.Get(line.Parameters[0]);
                client.HandleChannelModes(user, channel, line.Parameters[1], line.Parameters.Skip(2), true);
            } else if (client.CaseMappingComparer.Equals(line.Parameters[0], client.Me.Nickname)) {
                bool direction = true;
                foreach (char c in line.Parameters[1]) {
                    if (c == '+') direction = true;
                    else if (c == '-') direction = false;
                    else {
                        if (direction) client.UserModes.Add(c);
                        else client.UserModes.Remove(c);
                    }
                }
                client.OnUserModesSet(new UserModesEventArgs(line.Parameters[1]));
            }
        }

        [IrcMessageHandler("NICK")]
        public static void HandleNick(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            var oldNickname = user.Nickname;

            client.OnNicknameChange(new NicknameChangeEventArgs(user, line.Parameters[0]));

            if (client.Users.TryGetValue(user.Nickname, out user)) {
                client.Users.Remove(user);
                user.Nickname = line.Parameters[0];
                client.Users.Add(user);

                foreach (IrcChannel channel in user.Channels) {
                    IrcChannelUser channelUser = channel.Users[oldNickname];
                    channel.Users.Remove(channelUser);
                    channelUser.Nickname = line.Parameters[0];
                    channel.Users.Add(channelUser);
                }
            }
        }

        [IrcMessageHandler("NOTICE")]
        public static void HandleNotice(IrcClient client, IrcLine line) {
            if (client.IsChannel(line.Parameters[0])) {
                client.OnChannelNotice(new ChannelMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), client.Channels.Get(line.Parameters[0]), line.Parameters[1]));
            } else if (line.Prefix == null || line.Prefix.Split(new char[] { '!' }, 2)[0].Contains(".")) {
                // TODO: perhaps handle server notices better.
                client.OnServerNotice(new PrivateMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), line.Parameters[0], line.Parameters[1]));
            } else {
                client.OnPrivateNotice(new PrivateMessageEventArgs(client.Users.Get(line.Prefix ?? client.Address, false), line.Parameters[0], line.Parameters[1]));
            }
        }

        [IrcMessageHandler("PART")]
        public static void HandlePart(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            var channel = client.Channels.Get(line.Parameters[0]);
            var disappearedUsers = client.RemoveUserFromChannel(channel, user);

            client.OnChannelPart(new ChannelPartEventArgs(user, channel, line.Parameters.Length == 1 ? null : line.Parameters[1]));
            client.OnChannelLeave(new ChannelPartEventArgs(user, channel, line.Parameters.Length == 1 ? null : line.Parameters[1]));

            if (disappearedUsers != null) {
                foreach (var disappearedUser in disappearedUsers) {
                    client.Users.Remove(disappearedUser);
                    client.OnUserDisappeared(new IrcUserEventArgs(disappearedUser));
                }
            }
        }

        [IrcMessageHandler("PING")]
        public static void HandlePing(IrcClient client, IrcLine line) {
            client.OnPingReceived(new PingEventArgs(line.Parameters.Length == 0 ? null : line.Parameters[0]));
            client.Send(line.Parameters.Length == 0 ? "PONG" : "PONG :" + line.Parameters[0]);
        }

        [IrcMessageHandler("PONG")]
        public static void HandlePong(IrcClient client, IrcLine line) {
            client.OnPong(new PingEventArgs(line.Prefix));
        }

        [IrcMessageHandler("PRIVMSG")]
        public static void HandlePrivmsg(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);

            if (client.IsChannel(line.Parameters[0])) {
                // It's a channel message.
                if (line.Parameters[1].Length > 1 && line.Parameters[1].StartsWith("\u0001") && line.Parameters[1].EndsWith("\u0001")) {
                    string ctcpMessage = line.Parameters[1].Trim(new char[] { '\u0001' });
                    string[] fields = ctcpMessage.Split(new char[] { ' ' }, 2);
                    if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                        client.OnChannelAction(new ChannelMessageEventArgs(user, client.Channels.Get(line.Parameters[0]), fields.ElementAtOrDefault(1) ?? ""));
                    } else {
                        client.OnChannelCTCP(new ChannelMessageEventArgs(user, client.Channels.Get(line.Parameters[0]), ctcpMessage));
                    }
                } else {
                    client.OnChannelMessage(new ChannelMessageEventArgs(user, client.Channels.Get(line.Parameters[0]), line.Parameters[1]));
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

        [IrcMessageHandler("QUIT")]
        public static void HandleQuit(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            client.Users.Remove(user);
            foreach (IrcChannel channel in user.Channels)
                channel.Users.Remove(user.Nickname);

            var message = (line.Parameters.Length >= 1 ? line.Parameters[0] : null);
            client.OnUserQuit(new QuitEventArgs(user, message));
            foreach (IrcChannel channel in user.Channels)
                client.OnChannelLeave(new ChannelPartEventArgs(user, channel, (message != null && message.StartsWith("Quit:") ? "Quit: " : "Disconnected: ") + message));

            user.Channels.Clear();
        }

        [IrcMessageHandler("TOPIC")]
        public static void HandleTopic(IrcClient client, IrcLine line) {
            var user = client.Users.Get(line.Prefix, false);
            var channel = client.Channels.Get(line.Parameters[0]);

            var oldTopic = channel.Topic;
            var oldTopicSetter = channel.TopicSetter;
            var oldTopicStamp = channel.TopicStamp;

            if (line.Parameters.Length >= 2 && line.Parameters[1] != "")
                channel.Topic = line.Parameters[1];
            else
                channel.Topic = null;

            channel.TopicSetter = user.ToString();
            channel.TopicStamp = DateTime.Now;

            client.OnChannelTopicChange(new ChannelTopicChangeEventArgs(user, channel, oldTopic, oldTopicSetter, oldTopicStamp));
        }
    }
}
