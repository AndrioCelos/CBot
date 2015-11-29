using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace IRC {
    public delegate void IRCMessageHandler(IRCClient client, IRCLine line);

    /// <summary>
    /// Manages a connection to an IRC network.
    /// </summary>
    public class IRCClient {

        #region Events
        // TODO: Remove/reorganise/merge some of these?
        /// <summary>Raised when the local user ceases to be marked as away.</summary>
        public event EventHandler<AwayEventArgs> AwayCancelled;
        /// <summary>Raised when the local user is marked as away.</summary>
        public event EventHandler<AwayEventArgs> AwaySet;
        /// <summary>Raised when a channel ban list entry is received.</summary>
        public event EventHandler<ChannelModeListEventArgs> BanList;
        /// <summary>Raised when the end of a channel ban list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> BanListEnd;
        /// <summary>Raised when a user describes an action on a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelAction;
        /// <summary>Raised when a user gains administrator status (+a) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelAdmin;
        /// <summary>Raised when a ban is set (+b) on a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelBan;
        /// <summary>Raised when a channel timestamp is received.</summary>
        public event EventHandler<ChannelTimestampEventArgs> ChannelTimestamp;
        /// <summary>Raised when a CTCP request is received to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelCTCP;
        /// <summary>Raised when a user loses administrator status (-a) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeAdmin;
        /// <summary>Raised when a user loses half-operator status (-h) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfOp;
        /// <summary>Raised when a user loses half-voice (-V) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfVoice;
        /// <summary>Raised when a user loses operator status (-o) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOp;
        /// <summary>Raised when a user loses owner status (-q) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOwner;
        /// <summary>Raised when a user loses voice (-v) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeVoice;
        /// <summary>Raised when a ban exception is set (+e) on a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelExempt;
        /// <summary>Raised when a user gains half-operator status (+h) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfOp;
        /// <summary>Raised when a user gains half-voice (+V) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfVoice;
        /// <summary>Raised when an invite exemption is set (+I) on a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelInviteExempt;
        /// <summary>Raised when a user, including the local user, joins a channel.</summary>
        public event EventHandler<ChannelJoinEventArgs> ChannelJoin;
        /// <summary>Raised when a join attempt fails.</summary>
        public event EventHandler<ChannelDeniedEventArgs> ChannelJoinDenied;
        /// <summary>Raised when a user, including the local user, is kicked out of a channel.</summary>
        public event EventHandler<ChannelKickEventArgs> ChannelKick;
        /// <summary>Raised when a user sends a PRIVMSG to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelMessage;
        /// <summary>Raised when a PRIVMSG attempt fails.</summary>
        public event EventHandler<ChannelDeniedEventArgs> ChannelMessageDenied;
        /// <summary>Raised when modes are set on a channel, once for each mode.</summary>
        public event EventHandler<ChannelModeEventArgs> ChannelModeSet;
        /// <summary>Raised when modes are set on a channel that aren't handled by other events, once for each mode.</summary>
        public event EventHandler<ChannelModeEventArgs> ChannelModeUnhandled;
        /// <summary>Raised when modes are set on a channel, after other channel mode events.</summary>
        public event EventHandler<ChannelModesSetEventArgs> ChannelModesSet;
        /// <summary>Raised when a channel's modes are received.</summary>
        public event EventHandler<ChannelModesGetEventArgs> ChannelModesGet;
        /// <summary>Raised when a user sends a NOTICE to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelNotice;
        /// <summary>Raised when a user gains operator status (+o) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOp;
        /// <summary>Raised when a user gains owner status (+q) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOwner;
        /// <summary>Raised when a user, including the local user, parts a channel.</summary>
        public event EventHandler<ChannelPartEventArgs> ChannelPart;
        /// <summary>Raised when a quiet is set (+q) on a channel/</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelQuiet;
        /// <summary>Raised when a ban exception is removed (-e) on a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveExempt;
        /// <summary>Raised when an invite exemption is removed (-I) on a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveInviteExempt;
        /// <summary>Raised when a channel's key is removed (-k).</summary>
        public event EventHandler<ChannelEventArgs> ChannelRemoveKey;
        /// <summary>Raised when a channel's user limit is removed (-l).</summary>
        public event EventHandler<ChannelEventArgs> ChannelRemoveLimit;
        /// <summary>Raised when a key is set (+k) on a channel.</summary>
        public event EventHandler<ChannelKeyEventArgs> ChannelSetKey;
        /// <summary>Raised when a user limit is set (+l) on a channel.</summary>
        public event EventHandler<ChannelLimitEventArgs> ChannelSetLimit;
        /// <summary>Raised when a channel topic is received.</summary>
        public event EventHandler<ChannelTopicEventArgs> ChannelTopic;
        /// <summary>Raised when a channel topic is changed.</summary>
        public event EventHandler<ChannelTopicChangeEventArgs> ChannelTopicChange;
        /// <summary>Raised when a channel topic stamp is received.</summary>
        public event EventHandler<ChannelTopicStampEventArgs> ChannelTopicStamp;
        /// <summary>Raised when a ban is removed (-b) from a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelUnBan;
        /// <summary>Raised when a quiet is removed (-q) from a channel.</summary>
        public event EventHandler<ChannelListModeEventArgs> ChannelUnQuiet;
        /// <summary>Raised when a user gains voice (+v) on a channel.</summary>
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelVoice;
        /// <summary>Raised when the IRC connection is lost.</summary>
        public event EventHandler<DisconnectEventArgs> Disconnected;
        /// <summary>Raised when an exception occurs in the connection.</summary>
        public event EventHandler<ExceptionEventArgs> Exception;
        /// <summary>Raised when a channel ban exception list entry is received.</summary>
        public event EventHandler<ChannelModeListEventArgs> ExemptList;
        /// <summary>Raised when the end of a channel ban exception list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> ExemptListEnd;
        /// <summary>Raised when the local user is invited to a channel.</summary>
        public event EventHandler<ChannelInviteEventArgs> Invite;
        /// <summary>Raised when a channel invite is sent.</summary>
        public event EventHandler<ChannelInviteSentEventArgs> InviteSent;
        /// <summary>Raised when a channel invite exemption list entry is received. </summary>
        public event EventHandler<ChannelModeListEventArgs> InviteExemptList;
        /// <summary>Raised when the end of a channel invite exemption list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> InviteExemptListEnd;
        /// <summary>Raised when the local user is killed.</summary>
        public event EventHandler<PrivateMessageEventArgs> Killed;
        /// <summary>Raised when a channel list entry is seen.</summary>
        public event EventHandler<ChannelListEventArgs> ChannelList;
        /// <summary>Raised when the end of the channel list is seen.</summary>
        public event EventHandler<ChannelListEndEventArgs> ChannelListEnd;
        /// <summary>Raised when part of the MOTD is seen.</summary>
        public event EventHandler<MOTDEventArgs> MOTD;
        /// <summary>Raised when part of a channel names list is seen.</summary>
        public event EventHandler<ChannelNamesEventArgs> Names;
        /// <summary>Raised when the end of a channel names list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> NamesEnd;
        /// <summary>Raised when a user's nickname changes.</summary>
        public event EventHandler<NicknameChangeEventArgs> NicknameChange;
        /// <summary>Raised when a nickname change attempt fails.</summary>
        public event EventHandler<NicknameEventArgs> NicknameChangeFailed;
        /// <summary>Raised when a nickname change attempt fails because the nickname is invalid.</summary>
        public event EventHandler<NicknameEventArgs> NicknameInvalid;
        /// <summary>Raised when a nickname change attempt fails because the nickname is taken.</summary>
        public event EventHandler<NicknameEventArgs> NicknameTaken;
        /// <summary>Raised when a PONG is received.</summary>
        public event EventHandler<PingEventArgs> PingReply;
        /// <summary>Raised when a PING is received.</summary>
        public event EventHandler<PingEventArgs> PingRequest;
        /// <summary>Raised when a user describes an action in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateAction;
        /// <summary>Raised when a CTCP request is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateCTCP;
        /// <summary>Raised when a PRIVMSG is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateMessage;
        /// <summary>Raised when a NOTICE is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateNotice;
        /// <summary>Raised when a line is received from the server, before any other processing.</summary>
        public event EventHandler<IRCLineEventArgs> RawLineReceived;
        /// <summary>Raised when a line is received from the server that isn't handled.</summary>
        public event EventHandler<IRCLineEventArgs> RawLineUnhandled;
        /// <summary>Raised when a line is sent.</summary>
        public event EventHandler<RawEventArgs> RawLineSent;
        /// <summary>Raised when user modes are received.</summary>
        public event EventHandler<UserModesEventArgs> UserModesGet;
        /// <summary>Raised when user modes are set.</summary>
        public event EventHandler<UserModesEventArgs> UserModesSet;
        /// <summary>Raised when a WALLOPS message is received.</summary>
        public event EventHandler<PrivateMessageEventArgs> Wallops;
        /// <summary>Raised when a NOTICE message is received from a server.</summary>
        public event EventHandler<PrivateMessageEventArgs> ServerNotice;
        /// <summary>Raised when an ERROR message is received.</summary>
        public event EventHandler<ServerErrorEventArgs> ServerError;
        /// <summary>Raised when the State property changes.</summary>
        public event EventHandler<StateEventArgs> StateChanged;
        /// <summary>Raised when a user, including the local user, quits the IRC network.</summary>
        public event EventHandler<QuitEventArgs> UserQuit;
        /// <summary>Raised when the server presents an untrusted TLS certificate. Set e.Valid to true to allow the connection.</summary>
        public event EventHandler<ValidateCertificateEventArgs> ValidateCertificate;
        /// <summary>Raised when a WHO list entry is received.</summary>
        public event EventHandler<WhoListEventArgs> WhoList;
        /// <summary>Raised when a WHOIS authentication line is received.</summary>
        public event EventHandler<WhoisAuthenticationEventArgs> WhoIsAuthenticationLine;
        /// <summary>Raised when a WHOIS away line is received.</summary>
        public event EventHandler<WhoisAwayEventArgs> WhoIsAwayLine;
        /// <summary>Raised when a WHOIS channels line is received.</summary>
        public event EventHandler<WhoisChannelsEventArgs> WhoIsChannelLine;
        /// <summary>Raised when the end of a WHOIS listing is received.</summary>
        public event EventHandler<WhoisEndEventArgs> WhoIsEnd;
        /// <summary>Raised when a WHOIS idle line is received.</summary>
        public event EventHandler<WhoisIdleEventArgs> WhoIsIdleLine;
        /// <summary>Raised when a WHOIS name line is received.</summary>
        public event EventHandler<WhoisNameEventArgs> WhoIsNameLine;
        /// <summary>Raised when a WHOIS oper line is received.</summary>
        public event EventHandler<WhoisOperEventArgs> WhoIsOperLine;
        /// <summary>Raised when a WHOIS helper line is received.</summary>
        public event EventHandler<WhoisOperEventArgs> WhoIsHelperLine;
        /// <summary>Raised when a WHOIS real host line is received.</summary>
        public event EventHandler<WhoisRealHostEventArgs> WhoIsRealHostLine;
        /// <summary>Raised when a WHOIS server line is received.</summary>
        public event EventHandler<WhoisServerEventArgs> WhoIsServerLine;
        /// <summary>Raised when a WHOWAS name line is received.</summary>
        public event EventHandler<WhoisNameEventArgs> WhoWasNameLine;
        /// <summary>Raised when the end of a WHOWAS list is received.</summary>
        public event EventHandler<WhoisEndEventArgs> WhoWasEnd;
        #endregion

        #region Event methods
        protected internal void OnAwayCancelled(AwayEventArgs e) {
            this.AwayCancelled?.Invoke(this, e);
        }
        protected internal void OnAwaySet(AwayEventArgs e) {
            this.AwaySet?.Invoke(this, e);
        }
        protected internal void OnBanList(ChannelModeListEventArgs e) {
            this.BanList?.Invoke(this, e);
        }
        protected internal void OnBanListEnd(ChannelModeListEndEventArgs e) {
            this.BanListEnd?.Invoke(this, e);
        }
        protected internal void OnChannelAction(ChannelMessageEventArgs e) {
            this.ChannelAction?.Invoke(this, e);
        }
        protected internal void OnChannelAdmin(ChannelNicknameModeEventArgs e) {
            this.ChannelAdmin?.Invoke(this, e);
        }
        protected internal void OnChannelBan(ChannelListModeEventArgs e) {
            this.ChannelBan?.Invoke(this, e);
        }
        protected internal void OnChannelTimestamp(ChannelTimestampEventArgs e) {
            this.ChannelTimestamp?.Invoke(this, e);
        }
        protected internal void OnChannelCTCP(ChannelMessageEventArgs e) {
            this.ChannelCTCP?.Invoke(this, e);
        }
        protected internal void OnChannelDeAdmin(ChannelNicknameModeEventArgs e) {
            this.ChannelDeAdmin?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfOp(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfOp?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfVoice?.Invoke(this, e);
        }
        protected internal void OnChannelDeOp(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOp?.Invoke(this, e);
        }
        protected internal void OnChannelDeOwner(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOwner?.Invoke(this, e);
        }
        protected internal void OnChannelDeVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelDeVoice?.Invoke(this, e);
        }
        protected internal void OnChannelExempt(ChannelListModeEventArgs e) {
            this.ChannelExempt?.Invoke(this, e);
        }
        protected internal void OnChannelHalfOp(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfOp?.Invoke(this, e);
        }
        protected internal void OnChannelHalfVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfVoice?.Invoke(this, e);
        }
        protected internal void OnChannelInviteExempt(ChannelListModeEventArgs e) {
            this.ChannelInviteExempt?.Invoke(this, e);
        }
        protected internal void OnChannelJoin(ChannelJoinEventArgs e) {
            this.ChannelJoin?.Invoke(this, e);
        }
        protected internal void OnChannelJoinDenied(ChannelDeniedEventArgs e) {
            this.ChannelJoinDenied?.Invoke(this, e);
        }
        protected internal void OnChannelKick(ChannelKickEventArgs e) {
            this.ChannelKick?.Invoke(this, e);
        }
        protected internal void OnChannelList(ChannelListEventArgs e) {
            this.ChannelList?.Invoke(this, e);
        }
        protected internal void OnChannelListEnd(ChannelListEndEventArgs e) {
            this.ChannelListEnd?.Invoke(this, e);
        }
        protected internal void OnChannelMessage(ChannelMessageEventArgs e) {
            this.ChannelMessage?.Invoke(this, e);
        }
        protected internal void OnChannelMessageDenied(ChannelDeniedEventArgs e) {
            this.ChannelMessageDenied?.Invoke(this, e);
        }
        protected internal void OnChannelModeSet(ChannelModeEventArgs e) {
            this.ChannelModeSet?.Invoke(this, e);
        }
        protected internal void OnChannelModeUnhandled(ChannelModeEventArgs e) {
            this.ChannelModeUnhandled?.Invoke(this, e);
        }
        protected internal void OnChannelModesSet(ChannelModesSetEventArgs e) {
            this.ChannelModesSet?.Invoke(this, e);
        }
        protected internal void OnChannelModesGet(ChannelModesGetEventArgs e) {
            this.ChannelModesGet?.Invoke(this, e);
        }
        protected internal void OnChannelNotice(ChannelMessageEventArgs e) {
            this.ChannelNotice?.Invoke(this, e);
        }
        protected internal void OnChannelOp(ChannelNicknameModeEventArgs e) {
            this.ChannelOp?.Invoke(this, e);
        }
        protected internal void OnChannelOwner(ChannelNicknameModeEventArgs e) {
            this.ChannelOwner?.Invoke(this, e);
        }
        protected internal void OnChannelPart(ChannelPartEventArgs e) {
            this.ChannelPart?.Invoke(this, e);
        }
        protected internal void OnChannelQuiet(ChannelListModeEventArgs e) {
            this.ChannelQuiet?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveExempt(ChannelListModeEventArgs e) {
            this.ChannelRemoveExempt?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveInviteExempt(ChannelListModeEventArgs e) {
            this.ChannelRemoveInviteExempt?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveKey(ChannelEventArgs e) {
            this.ChannelRemoveKey?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveLimit(ChannelEventArgs e) {
            this.ChannelRemoveLimit?.Invoke(this, e);
        }
        protected internal void OnChannelSetKey(ChannelKeyEventArgs e) {
            this.ChannelSetKey?.Invoke(this, e);
        }
        protected internal void OnChannelSetLimit(ChannelLimitEventArgs e) {
            this.ChannelSetLimit?.Invoke(this, e);
        }
        protected internal void OnChannelTopic(ChannelTopicEventArgs e) {
            this.ChannelTopic?.Invoke(this, e);
        }
        protected internal void OnChannelTopicChange(ChannelTopicChangeEventArgs e) {
            this.ChannelTopicChange?.Invoke(this, e);
        }
        protected internal void OnChannelTopicStamp(ChannelTopicStampEventArgs e) {
            this.ChannelTopicStamp?.Invoke(this, e);
        }
        protected internal void OnChannelUnBan(ChannelListModeEventArgs e) {
            this.ChannelUnBan?.Invoke(this, e);
        }
        protected internal void OnChannelUnQuiet(ChannelListModeEventArgs e) {
            this.ChannelUnQuiet?.Invoke(this, e);
        }
        protected internal void OnChannelVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelVoice?.Invoke(this, e);
        }
        protected internal void OnDisconnected(DisconnectEventArgs e) {
            this.Disconnected?.Invoke(this, e);
        }
        protected internal void OnException(ExceptionEventArgs e) {
            this.Exception?.Invoke(this, e);
        }
        protected internal void OnExemptList(ChannelModeListEventArgs e) {
            this.ExemptList?.Invoke(this, e);
        }
        protected internal void OnExemptListEnd(ChannelModeListEndEventArgs e) {
            this.ExemptListEnd?.Invoke(this, e);
        }
        protected internal void OnInvite(ChannelInviteEventArgs e) {
            this.Invite?.Invoke(this, e);
        }
        protected internal void OnInviteSent(ChannelInviteSentEventArgs e) {
            this.InviteSent?.Invoke(this, e);
        }
        protected internal void OnInviteExemptList(ChannelModeListEventArgs e) {
            this.InviteExemptList?.Invoke(this, e);
        }
        protected internal void OnInviteExemptListEnd(ChannelModeListEndEventArgs e) {
            this.InviteExemptListEnd?.Invoke(this, e);
        }
        protected internal void OnKilled(PrivateMessageEventArgs e) {
            this.Killed?.Invoke(this, e);
        }
        protected internal void OnMOTD(MOTDEventArgs e) {
            this.MOTD?.Invoke(this, e);
        }
        protected internal void OnNames(ChannelNamesEventArgs e) {
            this.Names?.Invoke(this, e);
        }
        protected internal void OnNamesEnd(ChannelModeListEndEventArgs e) {
            this.NamesEnd?.Invoke(this, e);
        }
        protected internal void OnNicknameChange(NicknameChangeEventArgs e) {
            this.NicknameChange?.Invoke(this, e);
        }
        protected internal void OnNicknameChangeFailed(NicknameEventArgs e) {
            this.NicknameChangeFailed?.Invoke(this, e);
        }
        protected internal void OnNicknameInvalid(NicknameEventArgs e) {
            this.NicknameInvalid?.Invoke(this, e);
        }
        protected internal void OnNicknameTaken(NicknameEventArgs e) {
            this.NicknameTaken?.Invoke(this, e);
        }
        protected internal void OnPingRequest(PingEventArgs e) {
            this.PingRequest?.Invoke(this, e);
        }
        protected internal void OnPingReply(PingEventArgs e) {
            this.PingReply?.Invoke(this, e);
        }
        protected internal void OnPrivateAction(PrivateMessageEventArgs e) {
            this.PrivateAction?.Invoke(this, e);
        }
        protected internal void OnPrivateCTCP(PrivateMessageEventArgs e) {
            this.PrivateCTCP?.Invoke(this, e);
        }
        protected internal void OnPrivateMessage(PrivateMessageEventArgs e) {
            this.PrivateMessage?.Invoke(this, e);
        }
        protected internal void OnPrivateNotice(PrivateMessageEventArgs e) {
            this.PrivateNotice?.Invoke(this, e);
        }
        protected internal void OnUserQuit(QuitEventArgs e) {
            this.UserQuit?.Invoke(this, e);
        }
        protected internal void OnRawLineReceived(IRCLineEventArgs e) {
            this.RawLineReceived?.Invoke(this, e);
        }
        protected internal void OnRawLineUnhandled(IRCLineEventArgs e) {
            this.RawLineUnhandled?.Invoke(this, e);
        }
        protected internal void OnRawLineSent(RawEventArgs e) {
            this.RawLineSent?.Invoke(this, e);
        }
        protected internal void OnUserModesGet(UserModesEventArgs e) {
            this.UserModesGet?.Invoke(this, e);
        }
        protected internal void OnUserModesSet(UserModesEventArgs e) {
            this.UserModesSet?.Invoke(this, e);
        }
        protected internal void OnWallops(PrivateMessageEventArgs e) {
            this.Wallops?.Invoke(this, e);
        }
        protected internal void OnServerNotice(PrivateMessageEventArgs e) {
            this.ServerNotice?.Invoke(this, e);
        }
        protected internal void OnServerError(ServerErrorEventArgs e) {
            this.ServerError?.Invoke(this, e);
        }
        protected internal void OnStateChanged(StateEventArgs e) {
            this.StateChanged?.Invoke(this, e);
        }
        protected internal void OnValidateCertificate(ValidateCertificateEventArgs e) {
            this.ValidateCertificate?.Invoke(this, e);
        }
        protected internal void OnWhoList(WhoListEventArgs e) {
            this.WhoList?.Invoke(this, e);
        }
        protected internal void OnWhoIsAuthenticationLine(WhoisAuthenticationEventArgs e) {
            this.WhoIsAuthenticationLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsAwayLine(WhoisAwayEventArgs e) {
            this.WhoIsAwayLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsChannelLine(WhoisChannelsEventArgs e) {
            this.WhoIsChannelLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsEnd(WhoisEndEventArgs e) {
            this.WhoIsEnd?.Invoke(this, e);
        }
        protected internal void OnWhoIsIdleLine(WhoisIdleEventArgs e) {
            this.WhoIsIdleLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsNameLine(WhoisNameEventArgs e) {
            this.WhoIsNameLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsOperLine(WhoisOperEventArgs e) {
            this.WhoIsOperLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsHelperLine(WhoisOperEventArgs e) {
            this.WhoIsHelperLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsRealHostLine(WhoisRealHostEventArgs e) {
            this.WhoIsRealHostLine?.Invoke(this, e);
        }
        protected internal void OnWhoIsServerLine(WhoisServerEventArgs e) {
            this.WhoIsServerLine?.Invoke(this, e);
        }
        protected internal void OnWhoWasNameLine(WhoisNameEventArgs e) {
            this.WhoWasNameLine?.Invoke(this, e);
        }
        protected internal void OnWhoWasEnd(WhoisEndEventArgs e) {
            this.WhoWasEnd?.Invoke(this, e);
        }
        #endregion

        // Server information
        /// <summary>The IP address to connect to.</summary>
        public IPAddress IP;
        /// <summary>The port number to connect on.</summary>
        public int Port { get; set; }
        /// <summary>The address to connect to.</summary>
        public string Address { get; set; }
        /// <summary>The password to use when logging in, or null if no password is needed.</summary>
        public string Password { get; set; }
        /// <summary>The server's self-proclaimed name or address.</summary>
        public string ServerName { get; protected internal set; }
        /// <summary>The name of the IRC network, if known.</summary>
        public string NetworkName => this.Extensions.NetworkName;

        /// <summary>A list of all user modes the server supports.</summary>
        public string SupportedUserModes { get; private set; }
        /// <summary>A list of all channel modes the server supports.</summary>
        public string SupportedChannelModes { get; private set; }

        /// <summary>A list of all users we can see on the network.</summary>
        public IRCUserCollection Users { get; protected set; }
        /// <summary>A User object representing the local user.</summary>
        public IRCLocalUser Me { get; protected internal set; }

        /// <summary>The username to use with SASL authentication.</summary>
        public string SASLUsername { get; set; }
        /// <summary>The password to use with SASL authentication.</summary>
        public string SASLPassword { get; set; }

        public Extensions Extensions { get; protected internal set; }

        /// <summary>A StringComparer that emulates the comparison the server uses, as specified in the RPL_ISUPPORT message.</summary>
        public StringComparer CaseMappingComparer { get; protected internal set; } = IRCStringComparer.RFC1459;

        /// <summary>The time we last sent a PRIVMSG.</summary>
        public DateTime LastSpoke { get; protected internal set; }
        /// <summary>Our current user modes.</summary>
        public string UserModes { get; protected internal set; }
        /// <summary>The list of channels we are on.</summary>
        public IRCChannelCollection Channels { get; }
        /// <summary>True if we are connected; false otherwise.</summary>
        public IRCClientState State {
            get { return this.state; }
            protected internal set {
                IRCClientState oldState = this.state;
                this.state = value;
                this.OnStateChanged(new StateEventArgs(oldState, value));
            }
        }

        /// <summary>Contains SHA-256 hashes of TLS certificates that should be accepted.</summary>
        public List<string> TrustedCertificates { get; private set; }
        public bool AllowInvalidCertificate;

        /// <summary>Returns or sets the encoding used to interpret data.</summary>
        public Encoding Encoding { get; set; }

        private TcpClient tcpClient;
        private bool ssl;
        private SslStream SSLStream;
        private StreamReader reader;
        private StreamWriter _writer;
        private StreamWriter writer {
            get { return _writer; }
            set { _writer = value; }
        }
        private Thread ReadThread;
        private int _PingTimeout;
        private bool Pinged;
        private System.Timers.Timer PingTimer;
        private object Lock;

        private IRCClientState state;
        private DisconnectReason disconnectReason;
        internal bool accountKnown;  // Some servers send both 330 and 307 in WHOIS replies. We need to ignore the 307 in that case.

        /// <summary>Contains functions used to handle replies received from the server.</summary>
        protected internal Dictionary<string, IRCMessageHandler> MessageHandlers;

        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        public IRCClient(IRCLocalUser localUser) : this(localUser, null, new UTF8Encoding(false, false), 60) { }  // Don't want a byte order mark. That messes things up.
        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="networkName">The name of the IRC network.</param>
        public IRCClient(IRCLocalUser localUser, string networkName) : this(localUser, networkName, new UTF8Encoding(false, false), 60) { }
        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="encoding">The encoding to use to send and receive data.</param>
        public IRCClient(IRCLocalUser localUser, Encoding encoding) : this(localUser, null, encoding, 60) { }
        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="networkName">The name of the IRC network.</param>
        /// <param name="encoding">The encoding to use to send and receive data.</param>
        public IRCClient(IRCLocalUser localUser, string networkName, Encoding encoding) : this(localUser, networkName, encoding, 60) { }
        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="networkName">The name of the IRC network.</param>
        /// <param name="encoding">The encoding to use to send and receive data.</param>
        /// <param name="PingTimeout">The time to wait for a response from the server, in seconds.</param>
        public IRCClient(IRCLocalUser localUser, string networkName, Encoding encoding, int PingTimeout) {
            if (localUser == null) throw new ArgumentNullException("localUser");
            if (encoding == null) throw new ArgumentNullException("encoding");

            this.LastSpoke = default(DateTime);
            this.Extensions = new Extensions(networkName);
            this.Channels = new IRCChannelCollection(this);
            this.Users = new IRCUserCollection(this);
            this.TrustedCertificates = new List<string>();
            this.Encoding = encoding;
            this.Lock = new object();

            if (localUser.Client != null && localUser.Client != this) throw new ArgumentException("The IRCLocalUser object is already bound to another IRCClient.", "localUser");
            Me = localUser;
            localUser.Client = this;

            this.MessageHandlers = new Dictionary<string, IRCMessageHandler>(StringComparer.OrdinalIgnoreCase);
            this.RegisterHandlers(typeof(Handlers));

            this._PingTimeout = PingTimeout;
            if (PingTimeout <= 0)
                this.PingTimer = new System.Timers.Timer();
            else
                this.PingTimer = new System.Timers.Timer(PingTimeout * 1000);
        }

        /// <summary>Adds handlers marked by IRCMessageHandlerAttributes from the given type to this IRCClient.</summary>
        public void RegisterHandlers(Type type) {
            foreach (var method in type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static)) {
                foreach (var attribute in method.GetCustomAttributes<IRCMessageHandlerAttribute>()) {
                    this.MessageHandlers.Add(attribute.Command, (IRCMessageHandler) method.CreateDelegate(typeof(IRCMessageHandler)));
                }
            }
        }

        /// <summary>Returns or sets a value specifying whether the connection is or is to be made via TLS.</summary>
        /// <exception cref="InvalidOperationException">An attempt was made to set this property, and the client is connected.</exception>
        public bool SSL {
            get { return this.ssl; }
            set {
                if (this.State >= IRCClientState.SSLHandshaking)
                    throw new InvalidOperationException("This property cannot be set while the client is connected.");
                this.ssl = value;
            }
        }

        /// <summary>Returns or sets the ping timeout, in seconds.</summary>
        public int PingTimeout {
            get { return this._PingTimeout; }
            set {
                this._PingTimeout = value;
                bool flag = value == 0;
                if (value == 0)
                    this.PingTimer.Enabled = false;
                else {
                    this.PingTimer.Interval = value * 1000;
                    if (this.State >= IRCClientState.Connecting) this.PingTimer.Enabled = true;
                }
            }
        }

        /// <summary>Returns or sets the quit message that will be sent in the event of a ping timeout.</summary>
        public string PingTimeoutMessage { get; set; } = "Ping timeout";

        private void PingTimeout_Elapsed(object sender, ElapsedEventArgs e) {
            lock (this.PingTimer) {
                if (this.Pinged) {
                    this.disconnectReason = DisconnectReason.PingTimeout;
                    this.Send("QUIT :" + this.PingTimeoutMessage);
                    this.writer.Close();
                    this.PingTimer.Stop();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.PingTimeout, null));
                    this.State = IRCClientState.Disconnected;
                } else {
                    this.Send("PING :Keep-alive");
                    this.Pinged = true;
                }
            }
        }

        /// <summary>Connects and logs in to an IRC network.</summary>
        public virtual void Connect(string host, int port) {
            this.disconnectReason = 0;
            this.accountKnown = false;

            this.Address = host;
            // Connect to the server.
            this.tcpClient = new TcpClient() { ReceiveBufferSize = 1024, SendBufferSize = 1024 };
            this.State = IRCClientState.Connecting;
            this.tcpClient.BeginConnect(host, port, this.onConnected, host);

            if (this._PingTimeout != 0) this.PingTimer.Start();
            this.Pinged = false;
        }
        /// <summary>Connects and logs in to an IRC network.</summary>
        public virtual void Connect(IPAddress ip, int port) {
            this.disconnectReason = 0;
            this.accountKnown = false;

            this.Address = ip.ToString();
            // Connect to the server.
            this.tcpClient = new TcpClient() { ReceiveBufferSize = 1024, SendBufferSize = 1024 };
            this.State = IRCClientState.Connecting;
            this.tcpClient.BeginConnect(ip, port, this.onConnected, ip.ToString());

            if (this._PingTimeout != 0) this.PingTimer.Start();
            this.Pinged = false;
        }

        protected virtual void onConnected(IAsyncResult result) {
            try {
                this.tcpClient.EndConnect(result);
            } catch (SocketException ex) {
                this.OnException(new ExceptionEventArgs(ex, true));
                this.State = IRCClientState.Disconnected;
                return;
            }

            if (this.ssl) {
                // Make the SSL handshake.
                this.State = IRCClientState.SSLHandshaking;
                this.SSLStream = new SslStream(this.tcpClient.GetStream(), false, this.validateCertificate, null);

                try {
                    var protocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;  // SSLv3 has gone to the dogs.
                    this.SSLStream.AuthenticateAsClient((string) result.AsyncState, null, protocols, true);

                    this.reader = new StreamReader(this.SSLStream, Encoding);
                    this.writer = new StreamWriter(this.SSLStream, Encoding);

                    this.State = IRCClientState.Registering;

                    this.ReadThread = new Thread(this.ReadLoop) { Name = "IRCClient read thread: " + (this.NetworkName ?? this.Address) };
                    this.ReadThread.Start();

                    this.Register();
                } catch (AuthenticationException ex) {
                    this.OnException(new ExceptionEventArgs(ex, true));
                    this.tcpClient.Close();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.TlsAuthenticationFailed, ex));
                    this.State = IRCClientState.Disconnected;
                    return;
                } catch (IOException ex) {
                    this.OnException(new ExceptionEventArgs(ex, true));
                    this.tcpClient.Close();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.Exception, ex));
                    this.State = IRCClientState.Disconnected;
                    return;
                }
            } else {
                this.reader = new StreamReader(this.tcpClient.GetStream(), Encoding);
                this.writer = new StreamWriter(this.tcpClient.GetStream(), Encoding);

                this.ReadThread = new Thread(this.ReadLoop) { Name = "IRCClient read thread: " + (this.NetworkName ?? this.Address) };
                this.ReadThread.Start();

                this.State = IRCClientState.Registering;
                this.Register();
            }
        }

        protected virtual bool validateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            // If the certificate is valid, continue.
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            // If the certificate is trusted, continue.
            var sha256Hash = string.Join(null, new SHA256Managed().ComputeHash(certificate.GetRawCertData()).Select(b => b.ToString("X2")));
            if (this.TrustedCertificates.Contains(sha256Hash, StringComparer.OrdinalIgnoreCase)) return true;

            // Raise the event.
            ValidateCertificateEventArgs e = new ValidateCertificateEventArgs(certificate, chain, sslPolicyErrors, this.AllowInvalidCertificate);
            this.OnValidateCertificate(e);
            return e.Valid;
        }

        protected virtual void Register() {
            if (this.Password != null)
                this.Send("PASS :" + this.Password);
            this.Send("CAP LS");
            this.Send("NICK " + Me.Nickname);
            this.Send("USER " + Me.Ident + " 4 * :" + Me.FullName);
        }

        /// <summary>Ungracefully closes the connection to the IRC network.</summary>
        public virtual void Disconnect() {
            this.writer.Close();
            this.PingTimer.Stop();
            this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.ClientDisconnected, null));
            this.State = IRCClientState.Disconnected;
        }

        protected virtual void ReadLoop() {
            // Read data.
            while (true) {
                string line;
                try {
                    if (this._PingTimeout != 0) this.PingTimer.Start();
                    line = reader.ReadLine();
                } catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException) {
                    if (this.State == IRCClientState.Disconnected) break;
                    this.writer.Close();
                    this.PingTimer.Stop();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.Exception, ex));
                    break;
                }

                if (this.State == IRCClientState.Disconnected) break;
                if (line == null) {  // Server disconnected.
                    if (this.disconnectReason == 0) this.disconnectReason = DisconnectReason.ServerDisconnected;
                    this.OnDisconnected(new DisconnectEventArgs(this.disconnectReason, null));
                    this.State = IRCClientState.Disconnected;
                    break;
                }
                this.ReceivedLine(line);
            }
        }

        /// <summary>The UNIX epoch, used for timestamps on IRC, which is midnight UTC of 1 January 1970.</summary>
        public static DateTime Epoch => new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        /// <summary>Decodes a UNIX timestamp into a DateTime value.</summary>
        /// <param name="unixTime">The UNIX timestamp to decode.</param>
        /// <returns>The DateTime represented by the specified UNIX timestamp.</returns>
        public static DateTime DecodeUnixTime(double unixTime) {
            return Epoch.AddSeconds(unixTime);
        }
        /// <summary>Encodes a DateTime value into a UNIX timestamp.</summary>
        /// <param name="time">The DateTime value to encode.</param>
        /// <returns>The UNIX timestamp representation of the specified DateTime value.</returns>
        public static double EncodeUnixTime(DateTime time) {
            return (time.ToUniversalTime() - Epoch).TotalSeconds;
        }

        /// <summary>Handles or simulates a message received from the IRC server.</summary>
        /// <param name="data">The message received or to simulate.</param>
        public virtual void ReceivedLine(string data) {
            lock (this.Lock) {
                var line = IRCLine.Parse(data);
                this.OnRawLineReceived(new IRCLineEventArgs(data, line));

                IRCMessageHandler handler;
                if (this.MessageHandlers.TryGetValue(line.Command, out handler))
                    handler?.Invoke(this, line);
                else
                    this.OnRawLineUnhandled(new IRCLineEventArgs(data, line));
            }
        }

        /// <summary>Sends a raw message to the IRC server.</summary>
        /// <param name="data">The message to send.</param>
        /// <exception cref="InvalidOperationException">This IRCClient is not connected to a server.</exception>
        public virtual void Send(string data) {
            lock (this.Lock) {
                if (!tcpClient.Connected) throw new InvalidOperationException("The client is not connected.");

                this.OnRawLineSent(new RawEventArgs(data));

                this.writer.Write(data);
                this.writer.Write("\r\n");
                this.writer.Flush();

                if (this.disconnectReason == 0 && data.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    this.disconnectReason = DisconnectReason.Quit;
                else if (data.StartsWith("PRIVMSG ", StringComparison.OrdinalIgnoreCase))
                    this.LastSpoke = DateTime.Now;
            }
        }

        /// <summary>Sends a raw message to the IRC server.</summary>
        /// <param name="format">The format of the message, as per string.Format.</param>
        /// <param name="parameters">The parameters to include in the message.</param>
        /// <exception cref="InvalidOperationException">This IRCClient is not connected to a server.</exception>
        public virtual void Send(string format, params object[] parameters) {
            this.Send(string.Format(format, parameters));
        }

        /// <summary>Removes mIRC formatting codes from a string.</summary>
        /// <param name="message">The string to strip.</param>
        /// <returns>A copy of the string with mIRC formatting codes removed.</returns>
        public static string RemoveCodes(string message) {
            Regex regex = new Regex(@"\x02|\x0F|\x16|\x1C|\x1F|\x03(\d{0,2}(,\d{1,2})?)?");
            message = regex.Replace(message.Trim(), "");
            return message;
        }
        /// <summary>Removes a leading colon from a string, if one is present.</summary>
        /// <param name="data">The string to strip.</param>
        /// <returns>If the string starts with a colon, a copy of the string without that colon; otherwise, the specified string.</returns>
        public static string RemoveColon(string data) {
            if (data.StartsWith(":"))
                return data.Substring(1);
            else
                return data;
        }

        protected internal void HandleChannelMode(string sender, string target, bool direction, char mode, string parameter) {
            switch (mode) {
                case 'I':
                    this.HandleChannelModeList(sender, target, direction, mode, parameter, this.ChannelRemoveInviteExempt, this.ChannelInviteExempt);
                    break;
                case 'V':
                    this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeHalfVoice, this.ChannelHalfVoice);
                    break;
                case 'a':
                    this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeAdmin, this.ChannelAdmin);
                    break;
                case 'b':
                    this.HandleChannelModeList(sender, target, direction, mode, parameter, this.ChannelUnBan, this.ChannelBan);
                    break;
                case 'e':
                    this.HandleChannelModeList(sender, target, direction, mode, parameter, this.ChannelRemoveExempt, this.ChannelExempt);
                    break;
                case 'h':
                    this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeHalfOp, this.ChannelHalfOp);
                    break;
                case 'o':
                    this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeOp, this.ChannelOp);
                    break;
                case 'q':
                    if (this.Extensions.StatusPrefix.ContainsKey(mode)) {
                        // Owner mode
                        this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeOwner, this.ChannelOwner);
                    } else if (this.Extensions.ChanModes.TypeA.Contains(mode)) {
                        // Quiet mode
                        this.HandleChannelModeList(sender, target, direction, mode, parameter, this.ChannelUnQuiet, this.ChannelQuiet);
                    }
                    break;
                case 'v':
                    this.HandleChannelModeNickname(sender, target, direction, mode, parameter, this.ChannelDeVoice, this.ChannelVoice);
                    break;
            }
        }
        protected internal void HandleChannelModeList(string sender, string target, bool direction, char mode, string parameter,
            EventHandler<ChannelListModeEventArgs> downEvent, EventHandler<ChannelListModeEventArgs> upEvent) {
            if (!this.Extensions.ChanModes.TypeA.Contains(mode)) return;
            var matchedUsers = FindMatchingUsers(target, parameter);
            if (direction)
                upEvent?.Invoke(this, new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
            else
                downEvent?.Invoke(this, new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
        }
        protected internal void HandleChannelModeNickname(string sender, string target, bool direction, char mode, string parameter,
            EventHandler<ChannelNicknameModeEventArgs> downEvent, EventHandler<ChannelNicknameModeEventArgs> upEvent) {
            if (!this.Extensions.StatusPrefix.ContainsKey(mode)) return;
            if (direction) {
                this.Channels[target].Users[parameter].Status.Add(mode);
                upEvent?.Invoke(this, new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new IRCChannelUser(this, parameter)));
            } else {
                this.Channels[target].Users[parameter].Status.Remove(mode);
                downEvent?.Invoke(this, new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new IRCChannelUser(this, parameter)));
            }
        }

        /// <summary>Searches the users on a channel for those matching a specified hostmask.</summary>
        /// <param name="Channel">The channel to search.</param>
        /// <param name="Mask">The hostmask to search for.</param>
        /// <returns>A list of ChannelUser objects representing the matching users.</returns>
        public IRCChannelUser[] FindMatchingUsers(string Channel, string Mask) {
            List<IRCChannelUser> MatchedUsers = new List<IRCChannelUser>();
            StringBuilder exBuilder = new StringBuilder();

            foreach (char c in Mask) {
                if (c == '*') exBuilder.Append(".*");
                else if (c == '?') exBuilder.Append(".");
                else exBuilder.Append(Regex.Escape(c.ToString()));
            }
            Mask = exBuilder.ToString();

            foreach (IRCChannelUser user in this.Channels[Channel].Users) {
                if (Regex.IsMatch(user.User.ToString(), Mask)) MatchedUsers.Add(user);
            }

            return MatchedUsers.ToArray();
        }

        /// <summary>Determines whether the speciied string is a valid channel name.</summary>
        /// <param name="target">The string to check.</param>
        /// <returns>True if the specified string is a valid channel name; false if it is not.</returns>
        public bool IsChannel(string target) {
            if (target == null || target == "") return false;
            return this.Extensions.ChannelTypes.Contains(target[0]);
        }
    }
}
