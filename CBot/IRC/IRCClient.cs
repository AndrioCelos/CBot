using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Threading.Tasks;
using System.Timers;

using Timer = System.Timers.Timer;

using static IRC.Replies;
using System.Diagnostics;

namespace IRC {
    /// <summary>Represents a method that handles a client-bound IRC message.</summary>
    /// <param name="client">The <see cref="IrcClient"/> receiving the message.</param>
    /// <param name="line">The content of the message.</param>
    public delegate void IrcMessageHandler(IrcClient client, IrcLine line);

    /// <summary>
    /// Manages a connection to an IRC network.
    /// </summary>
    public class IrcClient {
        #region Events
        // TODO: Remove/reorganise/merge some of these?
        /// <summary>Raised when the local user ceases to be marked as away.</summary>
        public event EventHandler<AwayEventArgs> AwayCancelled;
        /// <summary>Raised when an away message for another user is received.</summary>
        public event EventHandler<AwayMessageEventArgs> AwayMessage;
        /// <summary>Raised when the local user is marked as away.</summary>
        public event EventHandler<AwayEventArgs> AwaySet;
        /// <summary>Raised when a user describes an action on a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelAction;
        /// <summary>Raised when a user gains administrator status (+a) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelAdmin;
        /// <summary>Raised when a ban is set (+b) on a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelBan;
        /// <summary>Raised when a channel ban list entry is received.</summary>
        public event EventHandler<ChannelModeListEventArgs> ChannelBanList;
        /// <summary>Raised when the end of a channel ban list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> ChannelBanListEnd;
        /// <summary>Raised when a ban is removed (-b) from a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelBanRemoved;
        /// <summary>Raised when a CTCP request is received to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelCTCP;
        /// <summary>Raised when a user loses administrator status (-a) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeAdmin;
        /// <summary>Raised when a user loses half-operator status (-h) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeHalfOp;
        /// <summary>Raised when a user loses half-voice (-V) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeHalfVoice;
        /// <summary>Raised when a user loses operator status (-o) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeOp;
        /// <summary>Raised when a user loses owner status (-q) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeOwner;
        /// <summary>Raised when a user loses voice (-v) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelDeVoice;
        /// <summary>Raised when a ban exception is set (+e) on a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelExempt;
        /// <summary>Raised when a ban exception is removed (-e) on a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelExemptRemoved;
        /// <summary>Raised when a user gains half-operator status (+h) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelHalfOp;
        /// <summary>Raised when a user gains half-voice (+V) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelHalfVoice;
        /// <summary>Raised when an invite exemption is set (+I) on a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelInviteExempt;
        /// <summary>Raised when a channel invite exemption list entry is received. </summary>
        public event EventHandler<ChannelModeListEventArgs> ChannelInviteExemptList;
        /// <summary>Raised when the end of a channel invite exemption list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> ChannelInviteExemptListEnd;
        /// <summary>Raised when an invite exemption is removed (-I) on a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelInviteExemptRemoved;
        /// <summary>Raised when a user, including the local user, joins a channel.</summary>
        public event EventHandler<ChannelJoinEventArgs> ChannelJoin;
        /// <summary>Raised when a join attempt fails.</summary>
        public event EventHandler<ChannelJoinDeniedEventArgs> ChannelJoinDenied;
        /// <summary>Raised when a channel's key is removed (-k).</summary>
        public event EventHandler<ChannelChangeEventArgs> ChannelKeyRemoved;
        /// <summary>Raised when a key is set (+k) on a channel.</summary>
        public event EventHandler<ChannelKeyEventArgs> ChannelKeySet;
        /// <summary>Raised when a user, including the local user, is kicked out of a channel.</summary>
        public event EventHandler<ChannelKickEventArgs> ChannelKick;
        /// <summary>Raised after a more specific event when a user leaves a channel by any means.</summary>
        public event EventHandler<ChannelPartEventArgs> ChannelLeave;
        /// <summary>Raised when a channel's user limit is removed (-l).</summary>
        public event EventHandler<ChannelChangeEventArgs> ChannelLimitRemoved;
        /// <summary>Raised when a user limit is set (+l) on a channel.</summary>
        public event EventHandler<ChannelLimitEventArgs> ChannelLimitSet;
        /// <summary>Raised when a channel list entry is seen.</summary>
        public event EventHandler<ChannelListEventArgs> ChannelList;
        /// <summary>Raised when a non-standard status mode has been set or removed on a channel user.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelListChanged;
        /// <summary>Raised when the end of the channel list is seen.</summary>
        public event EventHandler<ChannelListEndEventArgs> ChannelListEnd;
        /// <summary>Raised when a user sends a PRIVMSG to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelMessage;
        /// <summary>Raised when a PRIVMSG attempt fails.</summary>
        public event EventHandler<ChannelJoinDeniedEventArgs> ChannelMessageDenied;
        /// <summary>Raised when modes are set on a channel, once for each mode.</summary>
        public event EventHandler<ChannelModeChangedEventArgs> ChannelModeChanged;
        /// <summary>Raised when a channel's modes are received.</summary>
        public event EventHandler<ChannelModesSetEventArgs> ChannelModesGet;
        /// <summary>Raised when modes are set on a channel, after other channel mode events.</summary>
        public event EventHandler<ChannelModesSetEventArgs> ChannelModesSet;
        /// <summary>Raised when a user sends a NOTICE to a channel.</summary>
        public event EventHandler<ChannelMessageEventArgs> ChannelNotice;
        /// <summary>Raised when a user gains operator status (+o) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelOp;
        /// <summary>Raised when a user gains owner status (+q) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelOwner;
        /// <summary>Raised when a user, including the local user, parts a channel.</summary>
        public event EventHandler<ChannelPartEventArgs> ChannelPart;
        /// <summary>Raised when a quiet is set (+q) on a channel/</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelQuiet;
        /// <summary>Raised when a quiet is removed (-q) from a channel.</summary>
        public event EventHandler<ChannelListChangedEventArgs> ChannelQuietRemoved;
        /// <summary>Raised when a non-standard status mode has been set or removed on a channel user.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelStatusChanged;
        /// <summary>Raised when a channel timestamp is received.</summary>
        public event EventHandler<ChannelTimestampEventArgs> ChannelTimestamp;
        /// <summary>Raised when a channel topic is changed.</summary>
        public event EventHandler<ChannelTopicChangeEventArgs> ChannelTopicChanged;
        /// <summary>Raised when a channel topic is received.</summary>
        public event EventHandler<ChannelTopicEventArgs> ChannelTopicReceived;
        /// <summary>Raised when a channel topic stamp is received.</summary>
        public event EventHandler<ChannelTopicStampEventArgs> ChannelTopicStamp;
        /// <summary>Raised when a user gains voice (+v) on a channel.</summary>
        public event EventHandler<ChannelStatusChangedEventArgs> ChannelVoice;
        /// <summary>Raised when the IRC connection is lost.</summary>
        public event EventHandler<DisconnectEventArgs> Disconnected;
        /// <summary>Raised when an exception occurs in the connection.</summary>
        public event EventHandler<ExceptionEventArgs> Exception;
        /// <summary>Raised when a channel ban exception list entry is received.</summary>
        public event EventHandler<ChannelModeListEventArgs> ExemptList;
        /// <summary>Raised when the end of a channel ban exception list is seen.</summary>
        public event EventHandler<ChannelModeListEndEventArgs> ExemptListEnd;
        /// <summary>Raised when the local user is invited to a channel.</summary>
        public event EventHandler<InviteEventArgs> Invite;
        /// <summary>Raised when a channel invite is sent.</summary>
        public event EventHandler<InviteSentEventArgs> InviteSent;
        /// <summary>Raised when the local user is killed.</summary>
        public event EventHandler<PrivateMessageEventArgs> Killed;
        /// <summary>Raised when part of the MOTD is seen.</summary>
        public event EventHandler<MotdEventArgs> MOTD;
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
        public event EventHandler<PingEventArgs> Pong;
        /// <summary>Raised when a PING is received.</summary>
        public event EventHandler<PingEventArgs> PingReceived;
        /// <summary>Raised when a user describes an action in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateAction;
        /// <summary>Raised when a CTCP request is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateCTCP;
        /// <summary>Raised when a PRIVMSG is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateMessage;
        /// <summary>Raised when a NOTICE is received in private.</summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateNotice;
        /// <summary>Raised when a line is received from the server, before any other processing.</summary>
        public event EventHandler<IrcLineEventArgs> RawLineReceived;
        /// <summary>Raised when a line is sent.</summary>
        public event EventHandler<RawLineEventArgs> RawLineSent;
        /// <summary>Raised when a line is received from the server that isn't handled.</summary>
        public event EventHandler<IrcLineEventArgs> RawLineUnhandled;
        /// <summary>Raised when registration completes.</summary>
        public event EventHandler<RegisteredEventArgs> Registered;
        /// <summary>Raised when an ERROR message is received.</summary>
        public event EventHandler<ServerErrorEventArgs> ServerError;
        /// <summary>Raised when a NOTICE message is received from a server.</summary>
        public event EventHandler<PrivateMessageEventArgs> ServerNotice;
        /// <summary>Raised when the State property changes.</summary>
        public event EventHandler<StateEventArgs> StateChanged;
        /// <summary>Raised when we lose sight of a user, but they did not leave the network.</summary>
        public event EventHandler<IrcUserEventArgs> UserDisappeared;
        /// <summary>Raised when user modes are received.</summary>
        public event EventHandler<UserModesEventArgs> UserModesGet;
        /// <summary>Raised when user modes are set.</summary>
        public event EventHandler<UserModesEventArgs> UserModesSet;
        /// <summary>Raised when a user, including the local user, quits the IRC network.</summary>
        public event EventHandler<QuitEventArgs> UserQuit;
        /// <summary>Raised when the server presents an untrusted TLS certificate. Set e.Valid to true to allow the connection.</summary>
        public event EventHandler<ValidateCertificateEventArgs> ValidateCertificate;
        /// <summary>Raised when a WALLOPS message is received.</summary>
        public event EventHandler<PrivateMessageEventArgs> Wallops;
        /// <summary>Raised when a WHOIS authentication line is received.</summary>
        public event EventHandler<WhoisAuthenticationEventArgs> WhoIsAuthenticationLine;
        /// <summary>Raised when a WHOIS channels line is received.</summary>
        public event EventHandler<WhoisChannelsEventArgs> WhoIsChannelLine;
        /// <summary>Raised when the end of a WHOIS listing is received.</summary>
        public event EventHandler<WhoisEndEventArgs> WhoIsEnd;
        /// <summary>Raised when a WHOIS helper line is received.</summary>
        public event EventHandler<WhoisOperEventArgs> WhoIsHelperLine;
        /// <summary>Raised when a WHOIS idle line is received.</summary>
        public event EventHandler<WhoisIdleEventArgs> WhoIsIdleLine;
        /// <summary>Raised when a WHOIS name line is received.</summary>
        public event EventHandler<WhoisNameEventArgs> WhoIsNameLine;
        /// <summary>Raised when a WHOIS oper line is received.</summary>
        public event EventHandler<WhoisOperEventArgs> WhoIsOperLine;
        /// <summary>Raised when a WHOIS real host line is received.</summary>
        public event EventHandler<WhoisRealHostEventArgs> WhoIsRealHostLine;
        /// <summary>Raised when a WHOIS server line is received.</summary>
        public event EventHandler<WhoisServerEventArgs> WhoIsServerLine;
        /// <summary>Raised when a WHO list entry is received.</summary>
        public event EventHandler<WhoListEventArgs> WhoList;
        /// <summary>Raised when the end of a WHOWAS list is received.</summary>
        public event EventHandler<WhoisEndEventArgs> WhoWasEnd;
        /// <summary>Raised when a WHOWAS name line is received.</summary>
        public event EventHandler<WhoisNameEventArgs> WhoWasNameLine;
        #endregion

        #region Event methods
        protected internal virtual void OnAwayCancelled(AwayEventArgs e) => this.AwayCancelled?.Invoke(this, e);
        protected internal virtual void OnAwayMessage(AwayMessageEventArgs e) => this.AwayMessage?.Invoke(this, e);
        protected internal virtual void OnAwaySet(AwayEventArgs e) => this.AwaySet?.Invoke(this, e);
        protected internal virtual void OnBanList(ChannelModeListEventArgs e) => this.ChannelBanList?.Invoke(this, e);
        protected internal virtual void OnBanListEnd(ChannelModeListEndEventArgs e) => this.ChannelBanListEnd?.Invoke(this, e);
        protected internal virtual void OnChannelAction(ChannelMessageEventArgs e) => this.ChannelAction?.Invoke(this, e);
        protected internal virtual void OnChannelAdmin(ChannelStatusChangedEventArgs e) => this.ChannelAdmin?.Invoke(this, e);
        protected internal virtual void OnChannelBan(ChannelListChangedEventArgs e) => this.ChannelBan?.Invoke(this, e);
        protected internal virtual void OnChannelTimestamp(ChannelTimestampEventArgs e) => this.ChannelTimestamp?.Invoke(this, e);
        protected internal virtual void OnChannelCTCP(ChannelMessageEventArgs e) => this.ChannelCTCP?.Invoke(this, e);
        protected internal virtual void OnChannelDeAdmin(ChannelStatusChangedEventArgs e) => this.ChannelDeAdmin?.Invoke(this, e);
        protected internal virtual void OnChannelDeHalfOp(ChannelStatusChangedEventArgs e) => this.ChannelDeHalfOp?.Invoke(this, e);
        protected internal virtual void OnChannelDeHalfVoice(ChannelStatusChangedEventArgs e) => this.ChannelDeHalfVoice?.Invoke(this, e);
        protected internal virtual void OnChannelDeOp(ChannelStatusChangedEventArgs e) => this.ChannelDeOp?.Invoke(this, e);
        protected internal virtual void OnChannelDeOwner(ChannelStatusChangedEventArgs e) => this.ChannelDeOwner?.Invoke(this, e);
        protected internal virtual void OnChannelDeVoice(ChannelStatusChangedEventArgs e) => this.ChannelDeVoice?.Invoke(this, e);
        protected internal virtual void OnChannelExempt(ChannelListChangedEventArgs e) => this.ChannelExempt?.Invoke(this, e);
        protected internal virtual void OnChannelHalfOp(ChannelStatusChangedEventArgs e) => this.ChannelHalfOp?.Invoke(this, e);
        protected internal virtual void OnChannelHalfVoice(ChannelStatusChangedEventArgs e) => this.ChannelHalfVoice?.Invoke(this, e);
        protected internal virtual void OnChannelInviteExempt(ChannelListChangedEventArgs e) => this.ChannelInviteExempt?.Invoke(this, e);
        protected internal virtual void OnChannelJoin(ChannelJoinEventArgs e) => this.ChannelJoin?.Invoke(this, e);
        protected internal virtual void OnChannelJoinDenied(ChannelJoinDeniedEventArgs e) => this.ChannelJoinDenied?.Invoke(this, e);
        protected internal virtual void OnChannelKick(ChannelKickEventArgs e) => this.ChannelKick?.Invoke(this, e);
        protected internal virtual void OnChannelLeave(ChannelPartEventArgs e) => this.ChannelLeave?.Invoke(this, e);
        protected internal virtual void OnChannelList(ChannelListEventArgs e) => this.ChannelList?.Invoke(this, e);
        protected internal virtual void OnChannelListChanged(ChannelListChangedEventArgs e) => this.ChannelListChanged?.Invoke(this, e);
        protected internal virtual void OnChannelListEnd(ChannelListEndEventArgs e) => this.ChannelListEnd?.Invoke(this, e);
        protected internal virtual void OnChannelMessage(ChannelMessageEventArgs e) => this.ChannelMessage?.Invoke(this, e);
        protected internal virtual void OnChannelMessageDenied(ChannelJoinDeniedEventArgs e) => this.ChannelMessageDenied?.Invoke(this, e);
        protected internal virtual void OnChannelModeChanged(ChannelModeChangedEventArgs e) => this.ChannelModeChanged?.Invoke(this, e);
        protected internal virtual void OnChannelModesSet(ChannelModesSetEventArgs e) => this.ChannelModesSet?.Invoke(this, e);
        protected internal virtual void OnChannelModesGet(ChannelModesSetEventArgs e) => this.ChannelModesGet?.Invoke(this, e);
        protected internal virtual void OnChannelNotice(ChannelMessageEventArgs e) => this.ChannelNotice?.Invoke(this, e);
        protected internal virtual void OnChannelOp(ChannelStatusChangedEventArgs e) => this.ChannelOp?.Invoke(this, e);
        protected internal virtual void OnChannelOwner(ChannelStatusChangedEventArgs e) => this.ChannelOwner?.Invoke(this, e);
        protected internal virtual void OnChannelPart(ChannelPartEventArgs e) => this.ChannelPart?.Invoke(this, e);
        protected internal virtual void OnChannelQuiet(ChannelListChangedEventArgs e) => this.ChannelQuiet?.Invoke(this, e);
        protected internal virtual void OnChannelRemoveExempt(ChannelListChangedEventArgs e) => this.ChannelExemptRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelRemoveInviteExempt(ChannelListChangedEventArgs e) => this.ChannelInviteExemptRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelRemoveKey(ChannelChangeEventArgs e) => this.ChannelKeyRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelRemoveLimit(ChannelChangeEventArgs e) => this.ChannelLimitRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelSetKey(ChannelKeyEventArgs e) => this.ChannelKeySet?.Invoke(this, e);
        protected internal virtual void OnChannelSetLimit(ChannelLimitEventArgs e) => this.ChannelLimitSet?.Invoke(this, e);
        protected internal virtual void OnChannelStatusChanged(ChannelStatusChangedEventArgs e) => this.ChannelStatusChanged?.Invoke(this, e);
        protected internal virtual void OnChannelTopic(ChannelTopicEventArgs e) => this.ChannelTopicReceived?.Invoke(this, e);
        protected internal virtual void OnChannelTopicChange(ChannelTopicChangeEventArgs e) => this.ChannelTopicChanged?.Invoke(this, e);
        protected internal virtual void OnChannelTopicStamp(ChannelTopicStampEventArgs e) => this.ChannelTopicStamp?.Invoke(this, e);
        protected internal virtual void OnChannelUnBan(ChannelListChangedEventArgs e) => this.ChannelBanRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelUnQuiet(ChannelListChangedEventArgs e) => this.ChannelQuietRemoved?.Invoke(this, e);
        protected internal virtual void OnChannelVoice(ChannelStatusChangedEventArgs e) => this.ChannelVoice?.Invoke(this, e);
        protected internal virtual void OnDisconnected(DisconnectEventArgs e) {
            this.Channels.Clear();
            this.Users.Clear();
            this.UserModes.Clear();
            this.Disconnected?.Invoke(this, e);

            // Fail async requests.
            this.asyncRequestTimer?.Stop();
            lock (this.asyncRequests) {
				if (this.readAsyncTaskSource != null) this.readAsyncTaskSource.SetException(new AsyncRequestDisconnectedException(e.Reason, e.Exception));
				foreach (var asyncRequest in this.asyncRequests) {
                    asyncRequest.OnFailure(new AsyncRequestDisconnectedException(e.Reason, e.Exception));
                }
                this.asyncRequests.Clear();
            }
        }
        protected internal virtual void OnException(ExceptionEventArgs e) => this.Exception?.Invoke(this, e);
        protected internal virtual void OnExemptList(ChannelModeListEventArgs e) => this.ExemptList?.Invoke(this, e);
        protected internal virtual void OnExemptListEnd(ChannelModeListEndEventArgs e) => this.ExemptListEnd?.Invoke(this, e);
        protected internal virtual void OnInvite(InviteEventArgs e) => this.Invite?.Invoke(this, e);
        protected internal virtual void OnInviteSent(InviteSentEventArgs e) => this.InviteSent?.Invoke(this, e);
        protected internal virtual void OnInviteExemptList(ChannelModeListEventArgs e) => this.ChannelInviteExemptList?.Invoke(this, e);
        protected internal virtual void OnInviteExemptListEnd(ChannelModeListEndEventArgs e) => this.ChannelInviteExemptListEnd?.Invoke(this, e);
        protected internal virtual void OnKilled(PrivateMessageEventArgs e) => this.Killed?.Invoke(this, e);
        protected internal virtual void OnMotd(MotdEventArgs e) => this.MOTD?.Invoke(this, e);
        protected internal virtual void OnNames(ChannelNamesEventArgs e) => this.Names?.Invoke(this, e);
        protected internal virtual void OnNamesEnd(ChannelModeListEndEventArgs e) => this.NamesEnd?.Invoke(this, e);
        protected internal virtual void OnNicknameChange(NicknameChangeEventArgs e) => this.NicknameChange?.Invoke(this, e);
        protected internal virtual void OnNicknameChangeFailed(NicknameEventArgs e) => this.NicknameChangeFailed?.Invoke(this, e);
        protected internal virtual void OnNicknameInvalid(NicknameEventArgs e) => this.NicknameInvalid?.Invoke(this, e);
        protected internal virtual void OnNicknameTaken(NicknameEventArgs e) => this.NicknameTaken?.Invoke(this, e);
        protected internal virtual void OnPingReceived(PingEventArgs e) => this.PingReceived?.Invoke(this, e);
        protected internal virtual void OnPong(PingEventArgs e) => this.Pong?.Invoke(this, e);
        protected internal virtual void OnPrivateAction(PrivateMessageEventArgs e) => this.PrivateAction?.Invoke(this, e);
        protected internal virtual void OnPrivateCTCP(PrivateMessageEventArgs e) => this.PrivateCTCP?.Invoke(this, e);
        protected internal virtual void OnPrivateMessage(PrivateMessageEventArgs e) => this.PrivateMessage?.Invoke(this, e);
        protected internal virtual void OnPrivateNotice(PrivateMessageEventArgs e) => this.PrivateNotice?.Invoke(this, e);
        protected internal virtual void OnUserDisappeared(IrcUserEventArgs e) => this.UserDisappeared?.Invoke(this, e);
        protected internal virtual void OnUserQuit(QuitEventArgs e) => this.UserQuit?.Invoke(this, e);
        protected internal virtual void OnRawLineReceived(IrcLineEventArgs e) => this.RawLineReceived?.Invoke(this, e);
        protected internal virtual void OnRawLineUnhandled(IrcLineEventArgs e) => this.RawLineUnhandled?.Invoke(this, e);
        protected internal virtual void OnRawLineSent(RawLineEventArgs e) => this.RawLineSent?.Invoke(this, e);
        protected internal virtual void OnRegistered(RegisteredEventArgs e) => this.Registered?.Invoke(this, e);
        protected internal virtual void OnUserModesGet(UserModesEventArgs e) => this.UserModesGet?.Invoke(this, e);
        protected internal virtual void OnUserModesSet(UserModesEventArgs e) => this.UserModesSet?.Invoke(this, e);
        protected internal virtual void OnWallops(PrivateMessageEventArgs e) => this.Wallops?.Invoke(this, e);
        protected internal virtual void OnServerNotice(PrivateMessageEventArgs e) => this.ServerNotice?.Invoke(this, e);
        protected internal virtual void OnServerError(ServerErrorEventArgs e) => this.ServerError?.Invoke(this, e);
        protected internal virtual void OnStateChanged(StateEventArgs e) {
            if (e.NewState >= IrcClientState.ReceivingServerInfo && e.OldState < IrcClientState.ReceivingServerInfo)
                this.Users.Add(this.Me);

            this.StateChanged?.Invoke(this, e);
        }
        protected internal virtual void OnValidateCertificate(ValidateCertificateEventArgs e) => this.ValidateCertificate?.Invoke(this, e);
        protected internal virtual void OnWhoList(WhoListEventArgs e) => this.WhoList?.Invoke(this, e);
        protected internal virtual void OnWhoIsAuthenticationLine(WhoisAuthenticationEventArgs e) => this.WhoIsAuthenticationLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsChannelLine(WhoisChannelsEventArgs e) => this.WhoIsChannelLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsEnd(WhoisEndEventArgs e) => this.WhoIsEnd?.Invoke(this, e);
        protected internal virtual void OnWhoIsIdleLine(WhoisIdleEventArgs e) => this.WhoIsIdleLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsNameLine(WhoisNameEventArgs e) => this.WhoIsNameLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsOperLine(WhoisOperEventArgs e) => this.WhoIsOperLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsHelperLine(WhoisOperEventArgs e) => this.WhoIsHelperLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsRealHostLine(WhoisRealHostEventArgs e) => this.WhoIsRealHostLine?.Invoke(this, e);
        protected internal virtual void OnWhoIsServerLine(WhoisServerEventArgs e) => this.WhoIsServerLine?.Invoke(this, e);
        protected internal virtual void OnWhoWasNameLine(WhoisNameEventArgs e) => this.WhoWasNameLine?.Invoke(this, e);
        protected internal virtual void OnWhoWasEnd(WhoisEndEventArgs e) => this.WhoWasEnd?.Invoke(this, e);
        #endregion

        // Server information
        /// <summary>The common name (address) of the server, to be checked against the the server's TLS certificate if TLS is used.</summary>
        public string Address { get; set; }
        /// <summary>The password to use when logging in, or null if no password is needed.</summary>
        public string Password { get; set; }
        /// <summary>The server's self-proclaimed name or address.</summary>
        public string ServerName { get; protected internal set; }
        /// <summary>The name of the IRC network, if known.</summary>
        public string NetworkName => this.Extensions.NetworkName;

        /// <summary>A list of all user modes the server supports.</summary>
        public ModeSet SupportedUserModes { get; } = new ModeSet();
        /// <summary>A list of all channel modes the server supports.</summary>
        public ChannelModes SupportedChannelModes => this.Extensions.ChanModes;

        /// <summary>A list of all users we can see on the network.</summary>
        public IrcUserCollection Users { get; protected set; }
        /// <summary>A User object representing the local user.</summary>
        public IrcLocalUser Me { get; protected internal set; }

        /// <summary>The username to use with SASL authentication.</summary>
        /// <remarks>Currently only the PLAIN mechanism is supported.</remarks>
        public string SaslUsername { get; set; }
        /// <summary>The password to use with SASL authentication.</summary>
        public string SaslPassword { get; set; }
        /// <summary>Returns or sets a value indicating whether a connection will be abandoned if SASL authentication is unsuccessful.</summary>
        public bool RequireSaslAuthentication { get; set; }

        /// <summary>/Provides access to RPL_ISUPPORT extensions supported by the server.</summary>
        public IrcExtensions Extensions { get; protected internal set; }

        /// <summary>A <see cref="StringComparer"/> that emulates the comparison the server uses, as specified in the RPL_ISUPPORT message.</summary>
        public IrcStringComparer CaseMappingComparer { get; protected internal set; } = IrcStringComparer.RFC1459;

        /// <summary>The time we last sent a PRIVMSG.</summary>
        public DateTime LastSpoke { get; protected internal set; }
        /// <summary>Our current user modes.</summary>
        public ModeSet UserModes { get; protected internal set; } = new ModeSet();
        /// <summary>The list of channels we are on.</summary>
        public IrcChannelCollection Channels => Me.Channels;
        /// <summary>The current state of the IRC client.</summary>
        public IrcClientState State {
            get { return this.state; }
            protected internal set {
                IrcClientState oldState = this.state;
                this.state = value;
                this.OnStateChanged(new StateEventArgs(oldState, value));
            }
        }
        public bool DataAvailable => this.tcpClient?.GetStream()?.DataAvailable ?? false;

        /// <summary>Contains SHA-256 hashes of TLS certificates that should be accepted.</summary>
        public List<string> TrustedCertificates { get; private set; } = new List<string>();
        /// <summary>Returns or sets a value indicating whether the connection will continue by default if the server's TLS certificate is invalid.</summary>
        /// <remarks>This property can be overridden by handling the <see cref="ValidateCertificate"/> event.</remarks>
        public bool AllowInvalidCertificate { get; set; }

        /// <summary>Returns or sets the text encoding used to interpret data.</summary>
        public Encoding Encoding { get; set; }

        private List<AsyncRequest> asyncRequests = new List<AsyncRequest>();
        public ReadOnlyCollection<AsyncRequest> AsyncRequests;
        private Timer asyncRequestTimer;
		private TaskCompletionSource<IrcLine> readAsyncTaskSource;

        private TcpClient tcpClient;
        private bool ssl;
        private SslStream sslStream;
        private StreamReader reader;
        private StreamWriter writer;
        private Thread readThread;
        private int pingTimeout = 60;
        private bool pinged;
        private Timer pingTimer = new Timer(60000);
        private object receiveLock = new object();
        private object Lock = new object();

        private IrcClientState state;
        protected internal DisconnectReason disconnectReason;
        internal bool accountKnown;  // Some servers send both 330 and 307 in WHOIS replies. We need to ignore the 307 in that case.
        internal Dictionary<string, HashSet<string>> pendingNames = new Dictionary<string, HashSet<string>>();

        /// <summary>Contains functions used to handle replies received from the server.</summary>
        protected internal Dictionary<string, IrcMessageHandler> MessageHandlers = new Dictionary<string, IrcMessageHandler>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates a new IRCClient object with no network name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        public IrcClient(IrcLocalUser localUser) : this(localUser, null, new UTF8Encoding(false, false)) { }  // Don't want a byte order mark. That messes things up.
        /// <summary>Creates a new IRCClient object with no network name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="networkName">The name of the IRC network.</param>
        public IrcClient(IrcLocalUser localUser, string networkName) : this(localUser, networkName, new UTF8Encoding(false, false)) { }
        /// <summary>Creates a new IRCClient object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An IRCLocalUser instance to represent the local user.</param>
        /// <param name="encoding">The encoding to use to send and receive data.</param>
        public IrcClient(IrcLocalUser localUser, Encoding encoding) : this(localUser, null, encoding) { }
        /// <summary>Creates a new <see cref="IrcClient"/> object with no name and the default encoding and ping timeout.</summary>
        /// <param name="localUser">An <see cref="IrcLocalUser"/> instance to represent the local user.</param>
        /// <param name="networkName">The name of the IRC network.</param>
        /// <param name="encoding">The encoding to use to send and receive data.</param>
        public IrcClient(IrcLocalUser localUser, string networkName, Encoding encoding) {
            if (localUser == null) throw new ArgumentNullException(nameof(localUser));
            if (localUser.Client != null && localUser.Client != this) throw new ArgumentException("The " + nameof(IrcLocalUser) + " object is already bound to another " + nameof(IrcClient) + ".", nameof(localUser));

            this.Extensions = new IrcExtensions(this, networkName);
            this.Users = new IrcUserCollection(this);
            this.Encoding = encoding ?? new UTF8Encoding(false, false);
            this.AsyncRequests = this.asyncRequests.AsReadOnly();

            this.Me = localUser;
            localUser.client = this;
            localUser.Channels = new IrcChannelCollection(this);

            this.SetDefaultUserModes();

            this.RegisterHandlers(typeof(Handlers));
        }

        /// <summary>Adds handlers marked by <see cref="IrcMessageHandlerAttribute"/>s from the given type to this <see cref="IrcClient"/>.</summary>
        public void RegisterHandlers(Type type) {
            foreach (var method in type.GetMethods(BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static)) {
                foreach (var attribute in method.GetCustomAttributes<IrcMessageHandlerAttribute>()) {
                    this.MessageHandlers.Add(attribute.Reply, (IrcMessageHandler) method.CreateDelegate(typeof(IrcMessageHandler)));
                }
            }
        }

        /// <summary>Returns or sets a value specifying whether the connection is or is to be made via TLS.</summary>
        /// <exception cref="InvalidOperationException">An attempt was made to set this property while the client is connected.</exception>
        public bool SSL {
            get { return this.ssl; }
            set {
                if (this.State >= IrcClientState.SslHandshaking)
                    throw new InvalidOperationException("This property cannot be set while the client is connected.");
                this.ssl = value;
            }
        }

        /// <summary>Returns or sets the ping timeout, in seconds.</summary>
        public int PingTimeout {
            get { return this.pingTimeout; }
            set {
                this.pingTimeout = value;
                bool flag = value == 0;
                if (value == 0)
                    this.pingTimer.Enabled = false;
                else {
                    this.pingTimer.Interval = value * 1000;
                    if (this.State >= IrcClientState.Connecting) this.pingTimer.Enabled = true;
                }
            }
        }

        /// <summary>Returns or sets the quit message that will be sent in the event of a ping timeout.</summary>
        public string PingTimeoutMessage { get; set; } = "Ping timeout";

        private void PingTimeout_Elapsed(object sender, ElapsedEventArgs e) {
            lock (this.pingTimer) {
                if (this.pinged) {
                    this.disconnectReason = DisconnectReason.PingTimeout;
                    this.Send("QUIT :" + this.PingTimeoutMessage);
                    this.writer.Close();
                    this.pingTimer.Stop();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.PingTimeout, null));
                    this.State = IrcClientState.Disconnected;
                } else {
                    this.Send("PING :Keep-alive");
                    this.pinged = true;
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
            this.State = IrcClientState.Connecting;
            this.tcpClient.BeginConnect(host, port, this.onConnected, host);

            if (this.pingTimeout != 0) this.pingTimer.Start();
            this.pinged = false;
        }
        /// <summary>Connects and logs in to an IRC network.</summary>
        public virtual void Connect(IPAddress ip, int port) {
            this.disconnectReason = 0;
            this.accountKnown = false;

            // Connect to the server.
            this.tcpClient = new TcpClient() { ReceiveBufferSize = 1024, SendBufferSize = 1024 };
            this.State = IrcClientState.Connecting;
            this.tcpClient.BeginConnect(ip, port, this.onConnected, ip.ToString());

            if (this.pingTimeout != 0) this.pingTimer.Start();
            this.pinged = false;
        }

        protected virtual void onConnected(IAsyncResult result) {
            try {
                this.tcpClient.EndConnect(result);
            } catch (SocketException ex) {
                this.OnException(new ExceptionEventArgs(ex, true));
                this.State = IrcClientState.Disconnected;
                return;
            }

            if (this.ssl) {
                // Make the SSL handshake.
                this.State = IrcClientState.SslHandshaking;
                this.sslStream = new SslStream(this.tcpClient.GetStream(), false, this.validateCertificate, null);

                try {
                    const SslProtocols protocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;  // SSLv3 has gone to the dogs.
                    this.sslStream.AuthenticateAsClient(this.Address ?? (string) result.AsyncState, null, protocols, true);

                    this.reader = new StreamReader(this.sslStream, Encoding);
                    this.writer = new StreamWriter(this.sslStream, Encoding);

                    this.State = IrcClientState.Registering;
                    this.SetDefaultChannelModes();
                    this.SetDefaultUserModes();

                    this.readThread = new Thread(this.ReadLoop) { Name = "IrcClient read thread: " + (this.NetworkName ?? this.Address) };
                    this.readThread.Start();

                    this.Register();
                } catch (AuthenticationException ex) {
                    this.OnException(new ExceptionEventArgs(ex, true));
                    this.tcpClient.Close();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.SslAuthenticationFailed, ex));
                    this.State = IrcClientState.Disconnected;
                    return;
                } catch (IOException ex) {
                    this.OnException(new ExceptionEventArgs(ex, true));
                    this.tcpClient.Close();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.Exception, ex));
                    this.State = IrcClientState.Disconnected;
                    return;
                }
            } else {
                this.reader = new StreamReader(this.tcpClient.GetStream(), Encoding);
                this.writer = new StreamWriter(this.tcpClient.GetStream(), Encoding);

                this.readThread = new Thread(this.ReadLoop) { Name = "IrcClient read thread: " + (this.NetworkName ?? this.Address) };
                this.readThread.Start();

                this.State = IrcClientState.Registering;
                this.SetDefaultChannelModes();
                this.SetDefaultUserModes();
                this.Register();
            }
        }

        protected virtual bool validateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            bool valid = false;
            if (this.AllowInvalidCertificate)
                valid = true;
            // If the certificate is valid, continue.
            else if (sslPolicyErrors == SslPolicyErrors.None)
                valid = true;
            else {
                // If the certificate is trusted, continue.
                if (this.TrustedCertificates.Count != 0) {
                    var sha256Hash = string.Join(null, new SHA256Managed().ComputeHash(certificate.GetRawCertData()).Select(b => b.ToString("X2")));
                    if (this.TrustedCertificates.Contains(sha256Hash, StringComparer.OrdinalIgnoreCase))
                        valid = true;
                }
            }

            // Raise the event.
            ValidateCertificateEventArgs e = new ValidateCertificateEventArgs(certificate, chain, sslPolicyErrors, valid);
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
            this.pingTimer.Stop();
            this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.ClientDisconnected, null));
            this.State = IrcClientState.Disconnected;
        }

        /// <summary>Starts the specified <see cref="AsyncRequest"/> on this client.</summary>
        public void AddAsyncRequest(AsyncRequest request) {
            lock (this.asyncRequests) {
                this.asyncRequests.Add(request);
                if (request.CanTimeout) {
                    if (this.asyncRequestTimer == null) {
                        this.asyncRequestTimer = new Timer(30e+3) { AutoReset = false };
                        this.asyncRequestTimer.Elapsed += asyncRequestTimer_Elapsed;
                        this.asyncRequestTimer.Start();
                    } else {
                        this.asyncRequestTimer.Stop();
                        this.asyncRequestTimer.Start();
                    }
                }
            }
        }

        private void asyncRequestTimer_Elapsed(object sender, ElapsedEventArgs e) {
            // Time out async requests.
            lock (this.asyncRequests) {
                foreach (var asyncRequest in this.asyncRequests) {
                    if (asyncRequest.CanTimeout) asyncRequest.OnFailure(new TimeoutException());
                }
                this.asyncRequests.Clear();
            }
        }

        protected virtual void ReadLoop() {
            while (this.State >= IrcClientState.Registering) {
                string line;
                try {
                    if (this.pingTimeout != 0) this.pingTimer.Start();
                    line = reader.ReadLine();
                } catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException) {
                    if (this.State == IrcClientState.Disconnected) break;
                    this.writer.Close();
                    this.pingTimer.Stop();
                    this.OnDisconnected(new DisconnectEventArgs(DisconnectReason.Exception, ex));
                    break;
                }

                if (this.State == IrcClientState.Disconnected) break;
                if (line == null) {  // Server disconnected.
                    if (this.disconnectReason == 0) this.disconnectReason = DisconnectReason.ServerDisconnected;
                    this.OnDisconnected(new DisconnectEventArgs(this.disconnectReason, null));
                    this.State = IrcClientState.Disconnected;
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
            lock (this.receiveLock) {
                var line = IrcLine.Parse(data);

				int i; bool found = false;
				lock (this.asyncRequests) {
					for (i = 0; i < this.asyncRequests.Count; ++i) {
						if (asyncRequestCheck(line, this.asyncRequests[i])) {
							found = true;
							break;
						}
					}
				}

				this.OnRawLineReceived(new IrcLineEventArgs(data, line, found));
				if (this.readAsyncTaskSource != null) this.readAsyncTaskSource.SetResult(line);

                IrcMessageHandler handler;
                if (this.MessageHandlers.TryGetValue(line.Message, out handler))
                    handler?.Invoke(this, line);
                else
                    this.OnRawLineUnhandled(new IrcLineEventArgs(data, line, found));

                if (found) {
                    lock (this.asyncRequests) {
						var skipTypes = new HashSet<Type>();
                        for (; i < this.asyncRequests.Count; ++i) {
                            var asyncRequest = this.asyncRequests[i];
							if (!skipTypes.Contains(asyncRequest.GetType()) && asyncRequestCheck(line, asyncRequest, out bool final)) {
								var result = asyncRequest.OnReply(line, ref final);
								if (result) skipTypes.Add(asyncRequest.GetType());

                                if (final) {
                                    this.asyncRequests.RemoveAt(i);
                                    --i;
                                    if (this.asyncRequests.Count == 0)
                                        this.asyncRequestTimer.Stop();
                                }
                            }
                        }
                    }
                }
            }
        }

		private bool asyncRequestCheck(IrcLine line, AsyncRequest asyncRequest) => this.asyncRequestCheck(line, asyncRequest, out bool _);
		private bool asyncRequestCheck(IrcLine line, AsyncRequest asyncRequest, out bool final) {
			if (asyncRequest.Replies.TryGetValue(line.Message, out final)) {
				if (asyncRequest.Parameters == null) return true;
				for (int i = asyncRequest.Parameters.Count - 1; i >= 0; --i) {
					if (asyncRequest.Parameters[i] != null && !this.CaseMappingComparer.Equals(asyncRequest.Parameters[i], line.Parameters[i]))
						return false;
				}
				return true;
			}
			return false;
		}

		/// <summary>Sends a raw message to the IRC server.</summary>
		/// <param name="data">The message to send.</param>
		/// <exception cref="InvalidOperationException">The client is not connected to a server.</exception>
		public virtual void Send(string data) {
            lock (this.Lock) {
                if (!tcpClient.Connected) throw new InvalidOperationException("The client is not connected.");

                var e = new RawLineEventArgs(data);
                this.OnRawLineSent(e);

                this.writer.Write(e.Data);
                this.writer.Write("\r\n");
                this.writer.Flush();

                if (this.disconnectReason == 0 && e.Data.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    this.disconnectReason = DisconnectReason.Quit;
                else if (e.Data.StartsWith("PRIVMSG ", StringComparison.OrdinalIgnoreCase))
                    this.LastSpoke = DateTime.Now;
            }
        }

        /// <summary>Sends a raw message to the IRC server.</summary>
        /// <param name="format">The format of the message, as per <see cref="string.Format(string, object[])"/>.</param>
        /// <param name="parameters">The parameters to include in the message.</param>
        /// <exception cref="InvalidOperationException">The client is not connected to a server.</exception>
        public virtual void Send(string format, params object[] parameters) {
            this.Send(string.Format(format, parameters));
        }

        /// <summary>Removes mIRC formatting codes from a string.</summary>
        /// <param name="message">The string to strip.</param>
        /// <returns>A copy of the string with mIRC formatting codes removed.</returns>
        public static string RemoveCodes(string message) {
            Regex regex = new Regex(@"\x02|\x0F|\x16|\x1C|\x1F|\x03(\d{0,2}(,\d{1,2})?)?");
            message = regex.Replace(message, "");
            return message;
        }

        protected virtual void SetDefaultChannelModes() {
            this.Extensions.ChanModes = ChannelModes.RFC1459;
        }

        protected virtual void SetDefaultUserModes() {
            this.SupportedUserModes.Clear();
            this.SupportedUserModes.Add('i');
            this.SupportedUserModes.Add('o');
            this.SupportedUserModes.Add('s');
            this.SupportedUserModes.Add('w');
        }

        protected internal void HandleChannelModes(IrcUser sender, IrcChannel channel, string modes, IEnumerable<string> parameters, bool modeMessage) {
            var enumerator = parameters.GetEnumerator();
            bool direction = true;
            string parameter;

            var changes = new List<ModeChange>();
            HashSet<char> oldModes = null;
            if (!modeMessage) oldModes = new HashSet<char>(channel.Modes);

            foreach (char c in modes) {
                if (c == '+')
                    direction = true;
                else if (c == '-')
                    direction = false;
                else {
                    string oldParameter = null;

                    switch (this.Extensions.ChanModes.ModeType(c)) {
                        case 'S':
                            parameter = (enumerator.MoveNext() ? enumerator.Current : "");
                            this.HandleChannelModeStatus(sender, channel, direction, c, parameter, modeMessage);
                            break;
                        case 'A':
                            parameter = (enumerator.MoveNext() ? enumerator.Current : "");
                            this.HandleChannelModeList(sender, channel, direction, c, parameter, modeMessage);
                            break;
                        case 'D':
                            parameter = null;
                            this.HandleChannelMode(sender, channel, direction, c, null, modeMessage);
                            break;
                        case 'B':
                            parameter = (enumerator.MoveNext() ? enumerator.Current : "");
                            if (direction && !modeMessage && channel.Modes.Contains(c)) oldParameter = channel.Modes.GetParameter(c);
                            this.HandleChannelMode(sender, channel, direction, c, parameter, modeMessage);
                            break;
                        case 'C':
                            parameter = (direction ? (enumerator.MoveNext() ? enumerator.Current : "") : null);
                            if (direction && !modeMessage && channel.Modes.Contains(c)) oldParameter = channel.Modes.GetParameter(c);
                            this.HandleChannelMode(sender, channel, direction, c, parameter, modeMessage);
                            break;
                        default:
                            parameter = null;
                            this.HandleChannelMode(sender, channel, direction, c, null, modeMessage);
                            break;
                    }

                    if (direction) {
                        if (modeMessage || !oldModes.Remove(c)) {
                            // A mode is set.
                            changes.Add(new ModeChange() { Direction = direction, Mode = c, Parameter = parameter });
                        } else if (oldParameter != null && parameter != oldParameter) {
                            // The parameter has changed.
                            changes.Add(new ModeChange() { Direction = direction, Mode = c, Parameter = parameter });
                            channel.Modes.SetParameter(c, parameter);
                        }
                    } else if (modeMessage)
                        changes.Add(new ModeChange() { Direction = direction, Mode = c, Parameter = parameter });
                }
            }

            // Check for modes missing from RPL_CHANNELMODEIS.
            if (!modeMessage)
                foreach (char c in oldModes)
                    changes.Add(new ModeChange() { Direction = false, Mode = c, Parameter = null });

            if (modeMessage) this.OnChannelModesSet(new ChannelModesSetEventArgs(sender, channel, changes));
            else this.OnChannelModesGet(new ChannelModesSetEventArgs(sender, channel, changes));
        }

        protected internal void HandleChannelModeList(IrcUser sender, IrcChannel channel, bool direction, char mode, string parameter, bool events) {
            // TODO: implement internal mode lists.
        }
        protected internal void HandleChannelModeStatus(IrcUser sender, IrcChannel channel, bool direction, char mode, string parameter, bool events) {
            IrcChannelUser user;
            if (channel != null && channel.Users.TryGetValue(parameter, out user)) {
                if (direction) user.Status.Add(mode);
                else user.Status.Remove(mode);
            } else {
                user = new IrcChannelUser(this, channel, parameter);
            }
        }
        private void HandleChannelMode(IrcUser sender, IrcChannel channel, bool direction, char mode, string parameter, bool events) {
            if (direction) {
                if (parameter != null) channel.Modes.Add(mode, parameter);
                else channel.Modes.Add(mode);
            } else channel.Modes.Remove(mode);
        }

        private void notifyChannelModes(ChannelModesSetEventArgs e) {
            foreach (var change in e.Modes) {
                switch (this.Extensions.ChanModes.ModeType(change.Mode)) {
                    case 'A':
                        var e2 = new ChannelListChangedEventArgs(e.Sender, e.Channel, change.Direction, change.Mode, change.Parameter, e.Channel.Users.Matching(change.Parameter));
                        if (change.Mode == 'b') {
                            if (change.Direction) this.OnChannelBan(e2);
                            else this.OnChannelUnBan(e2);
                        } else if (change.Mode == 'q') {
                            if (change.Direction) this.OnChannelQuiet(e2);
                            else this.OnChannelUnQuiet(e2);
                        } else if (change.Mode == this.Extensions.BanExceptionsMode) {
                            if (change.Direction) this.OnChannelExempt(e2);
                            else this.OnChannelRemoveExempt(e2);
                        } else if (change.Mode == this.Extensions.InviteExceptionsMode) {
                            if (change.Direction) this.OnChannelInviteExempt(e2);
                            else this.OnChannelRemoveInviteExempt(e2);
                        } else
                            this.OnChannelListChanged(e2);
                        break;
                    case 'B':
                        if (change.Mode == 'k') {
                            if (change.Direction) this.OnChannelSetKey(new ChannelKeyEventArgs(e.Sender, e.Channel, change.Parameter));
                            else this.OnChannelRemoveKey(new ChannelChangeEventArgs(e.Sender, e.Channel));
                        } else
                            this.OnChannelModeChanged(new ChannelModeChangedEventArgs(e.Sender, e.Channel, change.Direction, change.Mode, change.Parameter));
                        break;
                    case 'C':
                        if (change.Mode == 'l') {
                            if (change.Direction) this.OnChannelSetLimit(new ChannelLimitEventArgs(e.Sender, e.Channel, int.Parse(change.Parameter)));
                            else this.OnChannelRemoveLimit(new ChannelChangeEventArgs(e.Sender, e.Channel));
                        } else
                            this.OnChannelModeChanged(new ChannelModeChangedEventArgs(e.Sender, e.Channel, change.Direction, change.Mode, change.Parameter));
                        break;
                    case 'S':
                        IrcChannelUser user;
                        if (!e.Channel.Users.TryGetValue(change.Parameter, out user)) user = new IrcChannelUser(this, e.Channel, change.Parameter);
                        var e3 = new ChannelStatusChangedEventArgs(e.Sender, e.Channel, change.Direction, change.Mode, user);

                        if (change.Mode == 'o') {
                            if (change.Direction) this.OnChannelOp(e3);
                            else this.OnChannelDeOp(e3);
                        } else if (change.Mode == 'v') {
                            if (change.Direction) this.OnChannelVoice(e3);
                            else this.OnChannelDeVoice(e3);
                        } else if (change.Mode == 'h') {
                            if (change.Direction) this.OnChannelHalfOp(e3);
                            else this.OnChannelDeHalfOp(e3);
                        } else if (change.Mode == 'a') {
                            if (change.Direction) this.OnChannelAdmin(e3);
                            else this.OnChannelDeAdmin(e3);
                        } else if (change.Mode == 'q') {
                            if (change.Direction) this.OnChannelOwner(e3);
                            else this.OnChannelDeOwner(e3);
                        } else if (change.Mode == 'V') {
                            if (change.Direction) this.OnChannelHalfVoice(e3);
                            else this.OnChannelDeHalfVoice(e3);
                        } else
                            this.OnChannelStatusChanged(e3);

                        break;
                    default:
                        this.OnChannelModeChanged(new ChannelModeChangedEventArgs(e.Sender, e.Channel, change.Direction, change.Mode, change.Parameter));
                        break;
                }
            }
        }

        internal IrcUser[] RemoveUserFromChannel(IrcChannel channel, IrcUser user) {
            channel.Users.Remove(user.Nickname);

            user.Channels.Remove(channel);
            if (user == Me) {
                var disappearedUsers = new List<IrcUser>();
                foreach (var channelUser in channel.Users) {
                    channelUser.User.Channels.Remove(channel);
                    if (!channelUser.User.IsSeen) disappearedUsers.Add(channelUser.User);
                }
                return disappearedUsers.ToArray();
            } else if (!user.IsSeen) {
                return new[] { user };
            } else
                return null;
        }

        internal void SetCaseMappingComparer() {
            switch (this.Extensions.CaseMapping) {
                case "ascii"         : this.CaseMappingComparer = new IrcStringComparer(CaseMappingMode.ASCII); break;
                case "strict-rfc1459": this.CaseMappingComparer = new IrcStringComparer(CaseMappingMode.StrictRFC1459); break;
                default              : this.CaseMappingComparer = new IrcStringComparer(CaseMappingMode.RFC1459); break;
            }

            // We need to rebuild the hash tables after setting this.
            var oldUsers = this.Users; IrcChannelCollection oldChannels;
            this.Users = new IrcUserCollection(this);
            foreach (var user in oldUsers) {
                this.Users.Add(user);

                oldChannels = user.Channels;
                user.Channels = new IrcChannelCollection(this);
                foreach (var channel in oldChannels) user.Channels.Add(channel);
            }

            foreach (var channel in this.Me.Channels) {
                var oldChannelUsers = channel.Users;
                channel.Users = new IrcChannelUserCollection(this);
                foreach (var user in oldChannelUsers) channel.Users.Add(user);
            }
        }

        /// <summary>Searches the users on a channel for those matching a specified hostmask.</summary>
        /// <param name="channel">The channel to search.</param>
        /// <param name="hostmask">The hostmask to search for.</param>
        /// <returns>A list of <see cref="IrcChannelUser"/> objects representing the matching users.</returns>
        public IEnumerable<IrcChannelUser> FindMatchingUsers(string channel, string hostmask)
            => this.Channels[channel].Users.Matching(hostmask);

		private static HashSet<char> breakingCharacters = new HashSet<char>() {
			'\t', ' ', '\u1680', '\u180E', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005',
			'\u2006', '\u2008', '\u2009', '\u200A', '\u200B', '\u200C', '\u200D', '\u205F', '\u3000'
		};

        /// <summary>Splits a message that is too long to fit in one line into multiple lines, using this <see cref="IrcClient"/>'s encoding.</summary>
        /// <param name="message">The message to split.</param>
        /// <param name="maxLength">The maximum size, in bytes, of each part.</param>
        /// <returns>
        /// An enumerable that yields substrings of the message that fit within the specified limit.
        /// If the message is already small enough to fit into one line, only <paramref name="message"/> itself is yielded.
        /// </returns>
        public IEnumerable<string> SplitMessage(string message, int maxLength) => IrcClient.SplitMessage(message, maxLength, this.Encoding);
        /// <summary>Splits a message that is too long to fit in one line into multiple lines using the specified encoding.</summary>
        /// <param name="message">The message to split.</param>
        /// <param name="maxLength">The maximum size, in bytes, of each part.</param>
        /// <param name="encoding">The encoding to use to calculate lengths.</param>
        /// <returns>
        /// An enumerable that yields substrings of the message that fit within the specified limit.
        /// If the message is already small enough to fit into one line, only <paramref name="message"/> itself is yielded.
        /// </returns>
        public static IEnumerable<string> SplitMessage(string message, int maxLength, Encoding encoding) {
            if (message == null) throw new ArgumentException("message");
            if (encoding == null) throw new ArgumentException("encoding");
            if (maxLength <= 0) throw new ArgumentOutOfRangeException("maxLength", "maxLength must be positive.");

            if (encoding.GetByteCount(message) <= maxLength) {
                yield return message;
                yield break;
            }

            int messageStart = 0, pos = 0, pos2 = 0;
            while (messageStart < message.Length) {
                string part = null;
				pos = messageStart + 1;
				do {
					// Find the next breaking character.
					for (; pos < message.Length; ++pos) {
						if (breakingCharacters.Contains(message[pos])) break;
					}

					string part2 = message.Substring(messageStart, pos - messageStart);

					// Skip repeated breaking characters.
					for (++pos; pos < message.Length; ++pos) {
						if (!breakingCharacters.Contains(message[pos])) break;
					}

					// Are we over the limit?
					if (encoding.GetByteCount(part2) > maxLength) {
						if (part == null) {
							// If a single word exceeds the limit, we must break it up.
							for (pos = messageStart + 1; pos < message.Length; ++pos) {
								part2 = message.Substring(messageStart, pos - messageStart);
								if (encoding.GetByteCount(part2) > maxLength) break;
								part = part2;
							}
							if (part == null) throw new InvalidOperationException("Can't even fit a single character in a message?!");
							pos2 = pos - 1;
						}
						break;
					}

					// No.
					part = part2;
					pos2 = pos;
				} while (pos < message.Length);

                yield return part;
                messageStart = pos2;
            }
        }

        /// <summary>Determines whether the specified string is a valid channel name.</summary>
        /// <param name="target">The string to check.</param>
        /// <returns>True if the specified string is a valid channel name; false if it is not.</returns>
        public bool IsChannel(string target) {
            if (target == null || target == "") return false;
            return this.Extensions.ChannelTypes.Contains(target[0]);
        }

		#region Async methods
		/// <summary>Waits for the next line from the server.</summary>
		public Task<IrcLine> ReadAsync() {
			if (this.readAsyncTaskSource == null)
				this.readAsyncTaskSource = new TaskCompletionSource<IrcLine>();
			return this.readAsyncTaskSource.Task;
		}

		/// <summary>Sends a PING message to the server and measures the ping time.</summary>
		public async Task<TimeSpan> PingAsync() {
			var request = new AsyncRequest.VoidAsyncRequest(this, null, "PONG", null);
			this.AddAsyncRequest(request);

			var stopwatch = Stopwatch.StartNew();
			this.Send("PING :" + this.ServerName);
			await request.Task;
			return stopwatch.Elapsed;
		}

		/// <summary>Attempts to oper up the local user. The returned Task object completes only if the command is accepted.</summary>
		public Task OperAsync(string name, string password) {
            if (this.state < IrcClientState.ReceivingServerInfo) throw new InvalidOperationException("The client must be registered to oper up.");

            var request = new AsyncRequest.VoidAsyncRequest(this, this.Me.Nickname, RPL_YOUREOPER, null, ERR_NEEDMOREPARAMS, ERR_NOOPERHOST, ERR_PASSWDMISMATCH);
            this.AddAsyncRequest(request);
            this.Send("OPER " + name + " " + password);
            return request.Task;
        }

		/// <summary>Attempts to join the specified channel. The returned Task object completes only if the join is successful.</summary>
		public Task JoinAsync(string channel) => this.JoinAsync(channel, null);
		/// <summary>Attempts to join the specified channel. The returned Task object completes only if the join is successful.</summary>
		public Task JoinAsync(string channel, string key) {
            if (channel == null) throw new ArgumentNullException(nameof(channel));
            if (this.state < IrcClientState.ReceivingServerInfo) throw new InvalidOperationException("The client must be registered to join channels.");

            var request = new AsyncRequest.VoidAsyncRequest(this, this.Me.Nickname, "JOIN", new[] { channel }, ERR_NEEDMOREPARAMS, ERR_BANNEDFROMCHAN, ERR_INVITEONLYCHAN, ERR_BADCHANNELKEY, ERR_CHANNELISFULL, ERR_BADCHANMASK, ERR_NOSUCHCHANNEL, ERR_TOOMANYCHANNELS, ERR_TOOMANYTARGETS, ERR_UNAVAILRESOURCE);
            this.AddAsyncRequest(request);

            if (key != null) this.Send("JOIN " + channel + " " + key);
            else this.Send("JOIN " + channel);

            return request.Task;
        }

        /// <summary>Performs a WHO request.</summary>
        public Task<ReadOnlyCollection<WhoResponse>> WhoAsync(string query) {
            if (this.state < IrcClientState.ReceivingServerInfo) throw new InvalidOperationException("The client must be registered to perform a WHO request.");

            var request = new AsyncRequest.WhoAsyncRequest(this, query);
            this.AddAsyncRequest(request);
            this.Send("WHO " + query);
            return (Task<ReadOnlyCollection<WhoResponse>>) request.Task;
        }

        /// <summary>Performs a WHOIS request on a nickname.</summary>
        /// <returns>A <see cref="Task"/> representing the status of the request. The <see cref="Task{TResult}.Result"/> represents the response to the request.</returns>
        public Task<WhoisResponse> WhoisAsync(string nickname) {
            if (nickname == null) throw new ArgumentNullException(nameof(nickname));
            if (this.state < IrcClientState.ReceivingServerInfo) throw new InvalidOperationException("The client must be registered to perform a WHOIS request.");

			var request = this.AsyncRequests.FirstOrDefault(r => r is AsyncRequest.WhoisAsyncRequest && this.CaseMappingComparer.Equals(((AsyncRequest.WhoisAsyncRequest) r).Target, nickname));
			if (request == null) {
				request = new AsyncRequest.WhoisAsyncRequest(this, nickname);
				this.AddAsyncRequest(request);
				this.Send("WHOIS " + nickname);
			}
            return (Task<WhoisResponse>) request.Task;
        }
        #endregion
    }
}
