using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace IRC {
    /// <summary>Represents the status a user can have on a channel. A user can have zero, one or more of these.</summary>
    /// <remarks>The flags are given values such that the higher bits represent higher status. Therefore, any user that can set modes on a channel will have access >= HalfOp, discounting oper powers.</remarks>
    [Flags]
    public enum ChannelAccess {
        /// <summary>The user has no known status.</summary>
        Normal = 0,
        /// <summary>The user has half-voice (mode +V).</summary>
        /// <remarks>I've never heard of an IRC server supporting this, and support in CIRC may be removed in future.</remarks>
        HalfVoice = 1,
        /// <summary>The user has voice (mode +v).</summary>
        Voice = 2,
        /// <summary>The user has half-operator status (mode +h).</summary>
        /// <remarks>Many IRC servers don't support this.</remarks>
        HalfOp = 4,
        /// <summary>The user has operator status (mode +o).</summary>
        Op = 8,
        /// <summary>The user has administrator (or super-op) status (mode +a).</summary>
        /// <remarks>Many IRC servers don't support this.</remarks>
        Admin = 16,
        /// <summary>The user has owner status (mode +q).</summary>
        /// <remarks>Many IRC servers don't support this, and channel mode q is often used for other purposes.</remarks>
        Owner = 32
    }

    /// <summary>
    /// Specifies the reason a join command failed.
    /// </summary>
    public enum ChannelJoinDeniedReason {
        /// <summary>The join failed because the channel has reached its population limit.</summary>
        Limit = 1,
        /// <summary>The join failed because the channel is invite only.</summary>
        InviteOnly,
        /// <summary>The join failed because we are banned.</summary>
        Banned,
        /// <summary>The join failed because we did not give the correct key.</summary>
        KeyFailure,
        /// <summary>The join failed for some other reason.</summary>
        Other = -1
    }

    /// <summary>
    /// Manages a connectionto an IRC network.
    /// </summary>
    public class IRCClient {
        #region Events
        public event EventHandler<AwayEventArgs> AwayCancelled;
        public event EventHandler<AwayEventArgs> AwaySet;
        public event EventHandler<ChannelModeListEventArgs> BanList;
        public event EventHandler<ChannelModeListEndEventArgs> BanListEnd;
        public event EventHandler<ChannelMessageEventArgs> ChannelAction;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelAdmin;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelAdminSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelBan;
        public event EventHandler<ChannelListModeEventArgs> ChannelBanSelf;
        public event EventHandler<ChannelTimestampEventArgs> ChannelTimestamp;
        public event EventHandler<ChannelMessageEventArgs> ChannelCTCP;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeAdmin;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeAdminSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfOp;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfOpSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfVoice;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeHalfVoiceSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOp;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOpSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOwner;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeOwnerSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeVoice;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelDeVoiceSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelExempt;
        public event EventHandler<ChannelListModeEventArgs> ChannelExemptSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfOp;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfOpSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfVoice;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelHalfVoiceSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelInviteExempt;
        public event EventHandler<ChannelListModeEventArgs> ChannelInviteExemptSelf;
        public event EventHandler<ChannelJoinEventArgs> ChannelJoin;
        public event EventHandler<ChannelJoinEventArgs> ChannelJoinSelf;
        public event EventHandler<ChannelDeniedEventArgs> ChannelJoinDenied;
        public event EventHandler<ChannelKickEventArgs> ChannelKick;
        public event EventHandler<ChannelKickEventArgs> ChannelKickSelf;
        public event EventHandler<ChannelMessageEventArgs> ChannelMessage;
        public event EventHandler<ChannelDeniedEventArgs> ChannelMessageDenied;
        public event EventHandler<ChannelModeEventArgs> ChannelModeSet;
        public event EventHandler<ChannelModeEventArgs> ChannelModeSetSelf;
        public event EventHandler<ChannelModeEventArgs> ChannelModeUnhandled;
        public event EventHandler<ChannelModesSetEventArgs> ChannelModesSet;
        public event EventHandler<ChannelModesGetEventArgs> ChannelModesGet;
        public event EventHandler<ChannelMessageEventArgs> ChannelNotice;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOp;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOpSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOwner;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelOwnerSelf;
        public event EventHandler<ChannelPartEventArgs> ChannelPart;
        public event EventHandler<ChannelPartEventArgs> ChannelPartSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelQuiet;
        public event EventHandler<ChannelListModeEventArgs> ChannelQuietSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveExempt;
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveExemptSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveInviteExempt;
        public event EventHandler<ChannelListModeEventArgs> ChannelRemoveInviteExemptSelf;
        public event EventHandler<ChannelEventArgs> ChannelRemoveKey;
        public event EventHandler<ChannelEventArgs> ChannelRemoveLimit;
        public event EventHandler<ChannelKeyEventArgs> ChannelSetKey;
        public event EventHandler<ChannelLimitEventArgs> ChannelSetLimit;
        public event EventHandler<ChannelTopicEventArgs> ChannelTopic;
        public event EventHandler<ChannelTopicChangeEventArgs> ChannelTopicChange;
        public event EventHandler<ChannelTopicStampEventArgs> ChannelTopicStamp;
        public event EventHandler<ChannelNamesEventArgs> ChannelUsers;
        public event EventHandler<ChannelListModeEventArgs> ChannelUnBan;
        public event EventHandler<ChannelListModeEventArgs> ChannelUnBanSelf;
        public event EventHandler<ChannelListModeEventArgs> ChannelUnQuiet;
        public event EventHandler<ChannelListModeEventArgs> ChannelUnQuietSelf;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelVoice;
        public event EventHandler<ChannelNicknameModeEventArgs> ChannelVoiceSelf;
        public event EventHandler<ExceptionEventArgs> Disconnected;
        public event EventHandler<ExceptionEventArgs> Exception;
        public event EventHandler<ChannelModeListEventArgs> ExemptList;
        public event EventHandler<ChannelModeListEndEventArgs> ExemptListEnd;
        public event EventHandler<ChannelInviteEventArgs> Invite;
        public event EventHandler<ChannelInviteSentEventArgs> InviteSent;
        public event EventHandler<ChannelModeListEventArgs> InviteList;
        public event EventHandler<ChannelModeListEndEventArgs> InviteListEnd;
        public event EventHandler<ChannelModeListEventArgs> InviteExemptList;
        public event EventHandler<ChannelModeListEndEventArgs> InviteExemptListEnd;
        public event EventHandler<PrivateMessageEventArgs> Killed;
        public event EventHandler<ChannelListEventArgs> ChannelList;
        public event EventHandler<ChannelListEndEventArgs> ChannelListEnd;
        public event EventHandler<MOTDEventArgs> MOTD;
        public event EventHandler<ChannelNamesEventArgs> Names;
        public event EventHandler<ChannelModeListEndEventArgs> NamesEnd;
        public event EventHandler<NicknameChangeEventArgs> NicknameChange;
        public event EventHandler<NicknameChangeEventArgs> NicknameChangeSelf;
        public event EventHandler<NicknameEventArgs> NicknameChangeFailed;
        public event EventHandler<NicknameEventArgs> NicknameInvalid;
        public event EventHandler<NicknameEventArgs> NicknameTaken;
        public event EventHandler<PingEventArgs> Ping;
        public event EventHandler<PingEventArgs> PingReply;
        public event EventHandler<PrivateMessageEventArgs> PrivateAction;
        public event EventHandler<PrivateMessageEventArgs> PrivateCTCP;
        public event EventHandler<PrivateMessageEventArgs> PrivateMessage;
        public event EventHandler<PrivateMessageEventArgs> PrivateNotice;
        public event EventHandler<QuitEventArgs> Quit;
        public event EventHandler<QuitEventArgs> QuitSelf;
        public event EventHandler<RawParsedEventArgs> RawLineReceived;
        public event EventHandler<RawParsedEventArgs> RawLineUnhandled;
        public event EventHandler<RawEventArgs> RawLineSent;
        public event EventHandler<UserModesEventArgs> UserModesGet;
        public event EventHandler<UserModesEventArgs> UserModesSet;
        public event EventHandler<PrivateMessageEventArgs> Wallops;
        public event EventHandler<PrivateMessageEventArgs> ServerNotice;
        public event EventHandler<ServerErrorEventArgs> ServerError;
        [Obsolete("This event is being discontinued in favour of OnRawLineReceived.")]
        public event EventHandler<ServerMessageEventArgs> ServerMessage;
        [Obsolete("This event is being discontinued.")]
        public event EventHandler<ServerMessageEventArgs> ServerMessageUnhandled;
        public event EventHandler SSLHandshakeComplete;
        public event EventHandler TimeOut;
        public event EventHandler<WhoListEventArgs> WhoList;
        public event EventHandler<WhoisAuthenticationEventArgs> WhoIsAuthenticationLine;
        public event EventHandler<WhoisAwayEventArgs> WhoIsAwayLine;
        public event EventHandler<WhoisChannelsEventArgs> WhoIsChannelLine;
        public event EventHandler<WhoisEndEventArgs> WhoIsEnd;
        public event EventHandler<WhoisIdleEventArgs> WhoIsIdleLine;
        public event EventHandler<WhoisNameEventArgs> WhoIsNameLine;
        public event EventHandler<WhoisOperEventArgs> WhoIsOperLine;
        public event EventHandler<WhoisOperEventArgs> WhoIsHelperLine;
        public event EventHandler<WhoisRealHostEventArgs> WhoIsRealHostLine;
        public event EventHandler<WhoisServerEventArgs> WhoIsServerLine;
        public event EventHandler<WhoisNameEventArgs> WhoWasNameLine;
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
        protected internal void OnChannelAdminSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelAdminSelf?.Invoke(this, e);
        }
        protected internal void OnChannelBan(ChannelListModeEventArgs e) {
            this.ChannelBan?.Invoke(this, e);
        }
        protected internal void OnChannelBanSelf(ChannelListModeEventArgs e) {
            this.ChannelBanSelf?.Invoke(this, e);
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
        protected internal void OnChannelDeAdminSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeAdminSelf?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfOp(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfOp?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfOpSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfOpSelf?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfVoice?.Invoke(this, e);
        }
        protected internal void OnChannelDeHalfVoiceSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeHalfVoiceSelf?.Invoke(this, e);
        }
        protected internal void OnChannelDeOp(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOp?.Invoke(this, e);
        }
        protected internal void OnChannelDeOpSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOpSelf?.Invoke(this, e);
        }
        protected internal void OnChannelDeOwner(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOwner?.Invoke(this, e);
        }
        protected internal void OnChannelDeOwnerSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeOwnerSelf?.Invoke(this, e);
        }
        protected internal void OnChannelDeVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelDeVoice?.Invoke(this, e);
        }
        protected internal void OnChannelDeVoiceSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelDeVoiceSelf?.Invoke(this, e);
        }
        protected internal void OnChannelExempt(ChannelListModeEventArgs e) {
            this.ChannelExempt?.Invoke(this, e);
        }
        protected internal void OnChannelExemptSelf(ChannelListModeEventArgs e) {
            this.ChannelExemptSelf?.Invoke(this, e);
        }
        protected internal void OnChannelHalfOp(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfOp?.Invoke(this, e);
        }
        protected internal void OnChannelHalfOpSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfOpSelf?.Invoke(this, e);
        }
        protected internal void OnChannelHalfVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfVoice?.Invoke(this, e);
        }
        protected internal void OnChannelHalfVoiceSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelHalfVoiceSelf?.Invoke(this, e);
        }
        protected internal void OnChannelInviteExempt(ChannelListModeEventArgs e) {
            this.ChannelInviteExempt?.Invoke(this, e);
        }
        protected internal void OnChannelInviteExemptSelf(ChannelListModeEventArgs e) {
            this.ChannelInviteExemptSelf?.Invoke(this, e);
        }
        protected internal void OnChannelJoin(ChannelJoinEventArgs e) {
            this.ChannelJoin?.Invoke(this, e);
        }
        protected internal void OnChannelJoinSelf(ChannelJoinEventArgs e) {
            this.ChannelJoinSelf?.Invoke(this, e);
        }
        protected internal void OnChannelJoinDenied(ChannelDeniedEventArgs e) {
            this.ChannelJoinDenied?.Invoke(this, e);
        }
        protected internal void OnChannelKick(ChannelKickEventArgs e) {
            this.ChannelKick?.Invoke(this, e);
        }
        protected internal void OnChannelKickSelf(ChannelKickEventArgs e) {
            this.ChannelKickSelf?.Invoke(this, e);
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
        protected internal void OnChannelModeSetSelf(ChannelModeEventArgs e) {
            this.ChannelModeSetSelf?.Invoke(this, e);
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
        protected internal void OnChannelOpSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelOpSelf?.Invoke(this, e);
        }
        protected internal void OnChannelOwner(ChannelNicknameModeEventArgs e) {
            this.ChannelOwner?.Invoke(this, e);
        }
        protected internal void OnChannelOwnerSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelOwnerSelf?.Invoke(this, e);
        }
        protected internal void OnChannelPart(ChannelPartEventArgs e) {
            this.ChannelPart?.Invoke(this, e);
        }
        protected internal void OnChannelPartSelf(ChannelPartEventArgs e) {
            this.ChannelPartSelf?.Invoke(this, e);
        }
        protected internal void OnChannelQuiet(ChannelListModeEventArgs e) {
            this.ChannelQuiet?.Invoke(this, e);
        }
        protected internal void OnChannelQuietSelf(ChannelListModeEventArgs e) {
            this.ChannelQuietSelf?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveExempt(ChannelListModeEventArgs e) {
            this.ChannelRemoveExempt?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveExemptSelf(ChannelListModeEventArgs e) {
            this.ChannelRemoveExemptSelf?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveInviteExempt(ChannelListModeEventArgs e) {
            this.ChannelRemoveInviteExempt?.Invoke(this, e);
        }
        protected internal void OnChannelRemoveInviteExemptSelf(ChannelListModeEventArgs e) {
            this.ChannelRemoveInviteExemptSelf?.Invoke(this, e);
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
        protected internal void OnChannelUsers(ChannelNamesEventArgs e) {
            this.ChannelUsers?.Invoke(this, e);
        }
        protected internal void OnChannelUnBan(ChannelListModeEventArgs e) {
            this.ChannelUnBan?.Invoke(this, e);
        }
        protected internal void OnChannelUnBanSelf(ChannelListModeEventArgs e) {
            this.ChannelUnBanSelf?.Invoke(this, e);
        }
        protected internal void OnChannelUnQuiet(ChannelListModeEventArgs e) {
            this.ChannelUnQuiet?.Invoke(this, e);
        }
        protected internal void OnChannelUnQuietSelf(ChannelListModeEventArgs e) {
            this.ChannelUnQuietSelf?.Invoke(this, e);
        }
        protected internal void OnChannelVoice(ChannelNicknameModeEventArgs e) {
            this.ChannelVoice?.Invoke(this, e);
        }
        protected internal void OnChannelVoiceSelf(ChannelNicknameModeEventArgs e) {
            this.ChannelVoiceSelf?.Invoke(this, e);
        }
        protected internal void OnDisconnected(ExceptionEventArgs e) {
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
        protected internal void OnInviteList(ChannelModeListEventArgs e) {
            this.InviteList?.Invoke(this, e);
        }
        protected internal void OnInviteListEnd(ChannelModeListEndEventArgs e) {
            this.InviteListEnd?.Invoke(this, e);
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
        protected internal void OnNicknameChangeSelf(NicknameChangeEventArgs e) {
            this.NicknameChangeSelf?.Invoke(this, e);
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
        protected internal void OnPing(PingEventArgs e) {
            this.Ping?.Invoke(this, e);
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
        protected internal void OnQuit(QuitEventArgs e) {
            this.Quit?.Invoke(this, e);
        }
        protected internal void OnQuitSelf(QuitEventArgs e) {
            this.QuitSelf?.Invoke(this, e);
        }
        protected internal void OnRawLineReceived(RawParsedEventArgs e) {
            this.RawLineReceived?.Invoke(this, e);
        }
        protected internal void OnRawLineUnhandled(RawParsedEventArgs e) {
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
        protected internal void OnServerMessage(ServerMessageEventArgs e) {
            this.ServerMessage?.Invoke(this, e);
        }
        protected internal void OnServerMessageUnhandled(ServerMessageEventArgs e) {
            this.ServerMessageUnhandled?.Invoke(this, e);
        }
        protected internal void OnSSLHandshakeComplete(EventArgs e) {
            this.SSLHandshakeComplete?.Invoke(this, e);
        }
        protected internal void OnTimeOut(EventArgs e) {
            this.TimeOut?.Invoke(this, e);
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
        public int Port;
        /// <summary>The address to connect to.</summary>
        public string Address { get; set; }
        /// <summary>The password to use when logging in, or null if no password is needed.</summary>
        public string Password { get; set; }
        /// <summary>The server's self-proclaimed name or address.</summary>
        public string ServerName { get; protected set; }

        /// <summary>A list of all user modes the server supports.</summary>
        public char[] SupportedUserModes;
        /// <summary>A list of all channel modes the server supports.</summary>
        public char[] SupportedChannelModes;

        /// <summary>A list of all users we can see on the network.</summary>
        public UserCollection Users { get; protected set; }
        /// <summary>A User object representing the local user.</summary>
        public User Me { get; protected internal set; }

        /// <summary>The username to use with SASL authentication.</summary>
        public string SASLUsername;
        /// <summary>The password to use with SASL authentication.</summary>
        public string SASLPassword;

        // 005 information
        /// <summary>A list of all RPL_ISUPPORT extensions given.</summary>
        public Dictionary<string, string> Extensions { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the case mapping this server uses to compare nicknames and channel names.</summary>
        /// <remarks>The value is case sensitive. There are three known values: ascii, rfc1459 (default) and strict-rfc1459.</remarks>
        public string CaseMapping { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of each type of channel we may be on.</summary>
        /// <remarks>Each key contains one of more channel prefixes, and the corresponding value is the limit for all of those channel types combined.</remarks>
        public Dictionary<string, int> ChannelLimit { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the channel modes this server supports.</summary>
        /// <remarks>The value consists of four or more comma-separated categories, each containing zero or more mode characters. They are described in detail in http://www.irc.org/tech_docs/draft-brocklesby-irc-isupport-03.txt</remarks>
        public IRC.ChannelModes ChanModes { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a channel name.</summary>
        public int ChannelLength { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the channel types supported by this server.</summary>
        public char[] ChannelTypes { get; protected set; }
        /// <summary>True if the server supports channel ban exceptions.</summary>
        public bool SupportsBanExceptions { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the mode character used for channel ban exceptions.</summary>
        public char BanExceptionsMode { get; protected set; }
        /// <summary>True if the server supports channel invite exceptions.</summary>
        public bool SupportsInviteExceptions { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the mode character used for channel invite exceptions.</summary>
        public char InviteExceptionsMode { get; protected set; }
        /// <summary>True if the server supports the WATCH command.</summary>
        /// <remarks>If true, we will use the WATCH list to monitor users in the Users list.</remarks>
        public bool SupportsWatch { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a kick message.</summary>
        public int KickMessageLength { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of entries that may be added to a channel list mode.</summary>
        /// <remarks>Each key contains one of more mode characters, and the corresponding value is the limit for all of those modes combined.</remarks>
        public Dictionary<string, int> ListModeLength { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of modes that can be set with a single command.</summary>
        public int Modes { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the name of the IRC network.</summary>
        /// <remarks>Note that this is not known until, and unless, the RPL_ISUPPORT message is received.</remarks>
        public string NetworkName { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a nickname we may use.</summary>
        public int NicknameLength { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the channel status modes this server supports.</summary>
        /// <remarks>Each entry contains a mode character as the key, and the corresponding prefix as the value. They are given in order from highest to lowest status.</remarks>
        public Dictionary<char, char> StatusPrefix { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the status prefixes we may use to only talk to users on a channel with that status.</summary>
        /// <remarks>Note that many servers require we also have that status to do this.</remarks>
        public char[] StatusMessage { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum number of targets we may give for certain commands.</summary>
        /// <remarks>Each entry consists of the command and the corresponding limit. Any command that's not listed does not support multiple targets.</remarks>
        public Dictionary<string, int> MaxTargets { get; protected set; }
        /// <summary>The RPL_ISUPPORT specification of the maximum length of a channel topic.</summary>
        public int TopicLength { get; protected set; }

        /// <summary>A StringComparer that emulates the comparison the server uses, as specified in the RPL_ISUPPORT message.</summary>
        public StringComparer CaseMappingComparer { get; protected set; }

        /// <summary>True if we are marked as away, false otherwise.</summary>
        public bool Away { get; protected set; }
        /// <summary>Our away message, if it is known; null otherwise..</summary>
        public string AwayReason { get; protected set; }
        /// <summary>The time we were marked as away.</summary>
        public DateTime AwaySince { get; protected set; }
        /// <summary>The time we last sent a PRIVMSG.</summary>
        public DateTime LastSpoke { get; protected set; }
        /// <summary>The list of nicknames to use, in order of preference.</summary>
        public string[] Nicknames;
        /// <summary>The identd username to use.</summary>
        public string Username;
        /// <summary>The full name to use.</summary>
        public string FullName;
        /// <summary>Our current user modes.</summary>
        public string UserModes;
        /// <summary>The list of channels we are on.</summary>
        public readonly ChannelCollection Channels;
        /// <summary>True if we have successfully logged in; false otherwise.</summary>
        public bool IsRegistered;
        /// <summary>True if we are connected; false otherwise.</summary>
        public bool IsConnected;
        /// <summary>True if we quit the current session by sending a QUIT command; false otherwise.</summary>
        public bool VoluntarilyQuit;

        private TcpClient tcpClient;
        private string _Nickname;
        private bool _SSL;
        public bool AllowInvalidCertificate;
        private SslStream SSLStream;
        private byte[] buffer;
        private StringBuilder outputBuilder;
        private Thread ReadThread;
        private int _PingTimeout;
        private bool Pinged;
        private System.Timers.Timer PingTimer;
        private object Lock;

        /// <summary>Creates a new IRC client with the default ping timeout of 60 seconds.</summary>
        public IRCClient() : this(60) { }
        /// <summary>Creates a new IRC client with the specified ping timeout.</summary>
        /// <param name="PingTimeout">The time to wait before disconnecting if we hear nothing from the server after a ping, in seconds.</param>
        public IRCClient(int PingTimeout) {
            this.Extensions = new Dictionary<string, string>();
            this.CaseMapping = "rfc1459";
            this.CaseMappingComparer = IRCStringComparer.RFC1459;
            this.ChannelLimit = new Dictionary<string, int> { { "#&", int.MaxValue } };
            this.ChannelLength = 200;
            this.ChannelTypes = new char[] { '#' };
            this.SupportsBanExceptions = false;
            this.SupportsInviteExceptions = false;
            this.KickMessageLength = int.MaxValue;
            this.ListModeLength = new Dictionary<string, int>();
            this.Modes = 3;
            this.NicknameLength = 9;
            this.StatusPrefix = new Dictionary<char, char> { { 'o', '@' }, { 'v', '+' } };
            this.StatusMessage = new char[0];
            this.MaxTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this.TopicLength = int.MaxValue;
            this.LastSpoke = default(DateTime);
            this.Channels = new ChannelCollection(this);
            this.buffer = new byte[512];
            this.outputBuilder = new StringBuilder();
            this.Users = new UserCollection(this);
            this.Lock = new object();

            this._PingTimeout = PingTimeout;
            if (PingTimeout <= 0)
                this.PingTimer = new System.Timers.Timer();
            else
                this.PingTimer = new System.Timers.Timer((double) PingTimeout * 1000.0);
        }

        /// <summary>Returns our nickname, or sets the nickname to use in the next session.</summary>
        /// <exception cref="System.InvalidOperationException">An attempt was made to set this property, and the client is connected.</exception>
        public string Nickname {
            get { return this._Nickname; }
            set {
                if (this.IsConnected)
                    throw new InvalidOperationException("This property cannot be set while the client is connected.");
                this._Nickname = value;                    
            }
        }

        /// <summary>Returns or sets a value specifying whether the connection is or is to be made via SSL.</summary>
        /// <exception cref="System.InvalidOperationException">An attempt was made to set this property, and the client is connected.</exception>
        public bool SSL {
            get { return this._SSL; }
            set {
                if (this.IsConnected)
                    throw new InvalidOperationException("This property cannot be set while the client is connected.");
                this._SSL = value;
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
                    this.PingTimer.Interval = (double) (value * 1000);
                    if (this.IsConnected) this.PingTimer.Enabled = true;
                }
            }
        }

        private void PingTimeout_Elapsed(object sender, ElapsedEventArgs e) {
            lock (this.PingTimer) {
                if (this.Pinged) {
                    this.OnTimeOut(EventArgs.Empty);
                    this.Send("QUIT :Ping timeout; reconnecting.");
                    this.PingTimer.Stop();
                    this.Disconnect();
                } else {
                    this.Send("PING :Keep-alive");
                    this.Pinged = true;
                }
            }
        }
        
        /// <summary>Connects and logs in to an IRC network.</summary>
        public virtual void Connect() {
            this.Me = new User(this, this.Nickname, "*", "*");

            // Connect to the server.
            if (this.IP == null) {
                this.tcpClient = new TcpClient();
                this.tcpClient.Connect(this.Address, this.Port);
                this.IP = ((IPEndPoint) this.tcpClient.Client.RemoteEndPoint).Address;
            } else {
                this.tcpClient = new TcpClient();
                this.tcpClient.Connect(new IPEndPoint(this.IP, this.Port));
            }

            this.LastSpoke = DateTime.Now;
            this.VoluntarilyQuit = false;
            this.IsConnected = true;
            if (this._PingTimeout != 0) this.PingTimer.Start();
            this.Pinged = false;
            this.ReadThread = new Thread(this.Read);
            this.ReadThread.Start();

            if (!this.SSL) {
                if (this.Password != null)
                    this.Send("PASS :{0}", this.Password);
                this.Send("CAP LS");
                this.Send("NICK {0}", this.Nickname);
                this.Send("USER {0} 4 {2} :{1}", this.Username, this.FullName, this.Address);
            }
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            Console.WriteLine("Failed to validate the server's certificate: {0}", sslPolicyErrors);
            Console.WriteLine("Certificate fingerprint: {0}", certificate.GetCertHashString());
            return this.AllowInvalidCertificate;
        }

        /// <summary>Ungracefully closes the connection to the IRC network.</summary>
        public virtual void Disconnect() {
            if (this.SSL) this.SSLStream.Close();
            this.tcpClient.Close();
            this.PingTimer.Stop();
        }

        private void Read() {
            // Make the SSL handshake.
            if (this.SSL) {
                this.SSLStream = new SslStream(this.tcpClient.GetStream(), false, this.ValidateServerCertificate, null);
                try {
                    this.SSLStream.AuthenticateAsClient(this.Address);
                    if (this.Password != null)
                        this.Send("PASS :{0}", this.Password);
                    this.Send("CAP LS");
                    this.Send("NICK {0}", this.Nickname);
                    this.Send("USER {0} 4 {2} :{1}", this.Username, this.FullName, this.Address);
                } catch (AuthenticationException ex) {
                    this.OnException(new ExceptionEventArgs(ex));
                    this.tcpClient.Close();
                    this.OnDisconnected(new ExceptionEventArgs(ex));
                    return;
                } catch (IOException ex) {
                    this.OnException(new ExceptionEventArgs(ex));
                    this.tcpClient.Close();
                    this.OnDisconnected(new ExceptionEventArgs(ex));
                    return;
                }
            }

            // Read data.
            this.outputBuilder = new StringBuilder();
            while (this.IsConnected) {
                int n;
                try {
                    if (this._PingTimeout != 0) this.PingTimer.Start();
                    if (this.SSL)
                        n = this.SSLStream.Read(this.buffer, 0, 512);
                    else
                        n = this.tcpClient.GetStream().Read(this.buffer, 0, 512);
                } catch (IOException ex) {
                    this.PingTimer.Stop();
                    this.OnDisconnected(new ExceptionEventArgs(ex));
                    return;
                } catch (SocketException ex) {
                    this.PingTimer.Stop();
                    this.OnDisconnected(new ExceptionEventArgs(ex));
                    return;
                } catch (ObjectDisposedException ex) {
                    this.PingTimer.Stop();
                    this.OnDisconnected(new ExceptionEventArgs(ex));
                    return;
                }

                if (!this.IsConnected) break;
                if (n < 1) {
                    this.PingTimer.Stop();
                    this.OnDisconnected(new ExceptionEventArgs(null));
                    return;
                }
                for (int i = 0; i < n; ++i) {
                    if (this.buffer[i] == 13 || this.buffer[i] == 10) {
                        if (this.outputBuilder.Length > 0) {
                            this.Pinged = false;
                            this.PingTimer.Stop();
                            try {
                                this.ReceivedLine(this.outputBuilder.ToString());
                            } catch (Exception ex) {
                                this.OnException(new ExceptionEventArgs(ex));
                            }
                            this.outputBuilder.Clear();
                        }
                    } else
                        this.outputBuilder.Append((char) this.buffer[i]);
                }
            }
        }

        /// <summary>Parses an IRC message and retuns the results in the out parameters.</summary>
        /// <param name="data">The message to parse.</param>
        /// <param name="prefix">Returns the prefix, or null if there is none.</param>
        /// <param name="command">Returns the command or reply.</param>
        /// <param name="parameters">Returns the parameters, without a leading ':' for the final one.</param>
        /// <param name="trail">Returns the paramter with a leading ':', or null if there is one.</param>
        /// <param name="includeTrail">If set to false, the trail will not be included in the parameters list.</param>
        public static void ParseIRCLine(string data, out string prefix, out string command, out string[] parameters, out string trail, bool includeTrail = true) {
            int p = 0; int ps = 0;

            trail = null;
            if (data.Length == 0) {
                prefix = null;
                command = null;
                parameters = null;
            } else {
                if (data[0] == ':') {
                    p = data.IndexOf(' ');
                    if (p < 0) {
                        prefix = data.Substring(1);
                        command = null;
                        parameters = null;
                        return;
                    }
                    prefix = data.Substring(1, p - 1);
                    ps = p + 1;
                } else {
                    prefix = null;
                }

                List<string> tParameters = new List<string>();
                while (ps < data.Length) {
                    if (data[ps] == ':') {
                        trail = data.Substring(ps + 1);
                        if (includeTrail) {
                            tParameters.Add(trail);
                        }
                        break;
                    }
                    p = data.IndexOf(' ', ps);
                    if (p < 0) {
                        // Final parameter
                        tParameters.Add(data.Substring(ps));
                        break;
                    }
                    string tP = data.Substring(ps, p - ps);
                    if (tP.Length > 0) tParameters.Add(tP);
                    ps = p + 1;
                }
                command = tParameters[0];
                tParameters.RemoveAt(0);
                parameters = tParameters.ToArray();
            }
        }

        /// <summary>The UNIX epoch, used for timestamps on IRC, which is midnight UTC of 1 January 1970.</summary>
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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
        public void ReceivedLine(string data) {
            lock (this.Lock) {
                string prefix;
                string command;
                string[] parameters;
                string trail = null;
                IRCClient.ParseIRCLine(data, out prefix, out command, out parameters, out trail, true);
                this.OnRawLineReceived(new RawParsedEventArgs(data, prefix, command, parameters));

                DateTime time;
                User user;

                // TODO: use a hashtable of delegates instead of a switch block.
                switch (command.ToUpper()) {
                    case "001":
                        this.ServerName = prefix;
                        if (this.Nickname != parameters[0]) {
                            this.OnNicknameChangeSelf(new NicknameChangeEventArgs(this.Me, parameters[0]));
                            this.Nickname = parameters[0];
                        }
                        this.Users.Add(this.Me);
                        this.IsRegistered = true;
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "005":
                        if (!(parameters.Length != 0 && parameters[0].StartsWith("Try server"))) {
                            // RPL_ISUPPORT
                            for (int i = 1; i < (trail == null ? parameters.Length : parameters.Length - 1); ++i) {
                                string[] fields; string key; string value;
                                fields = parameters[i].Split(new char[] { '=' }, 2);
                                if (fields.Length == 2) {
                                    key = fields[0];
                                    value = fields[1];
                                } else {
                                    key = fields[0];
                                    value = "";
                                }

                                if (key.StartsWith("-")) {
                                    this.Extensions.Remove(key.Substring(1));
                                } else {
                                    this.Extensions[key] = value;

                                    switch (key) {  // Parameter names are case sensitive.
                                        case "CASEMAPPING":
                                            this.CaseMapping = value;
                                            switch (value.ToUpper()) {
                                                case "ASCII":
                                                    this.CaseMappingComparer = IRCStringComparer.ASCII;
                                                    break;
                                                case "STRICT-RFC1459":
                                                    this.CaseMappingComparer = IRCStringComparer.StrictRFC1459;
                                                    break;
                                                default:
                                                    this.CaseMappingComparer = IRCStringComparer.RFC1459;
                                                    break;
                                            }
                                            break;
                                        case "CHANLIMIT":
                                            this.ChannelLimit = new Dictionary<string, int>();
                                            foreach (string field in value.Split(new char[] { ',' })) {
                                                fields = field.Split(new char[] { ':' });
                                                this.ChannelLimit.Add(fields[0], int.Parse(fields[1]));
                                            }
                                            break;
                                        case "CHANMODES":
                                            fields = value.Split(new char[] { ',' });
                                            this.ChanModes = new ChannelModes(fields[0].ToCharArray(), fields[1].ToCharArray(), fields[2].ToCharArray(), fields[3].ToCharArray());
                                            break;
                                        case "CHANNELLEN": this.ChannelLength = int.Parse(value); break;
                                        case "CHANTYPES": this.ChannelTypes = value.ToCharArray(); break;
                                        case "EXCEPTS":
                                            this.SupportsBanExceptions = true;
                                            this.BanExceptionsMode = value == "" ? 'e' : value[0];
                                            break;
                                        case "INVEX":
                                            this.SupportsInviteExceptions = true;
                                            this.InviteExceptionsMode = value == "" ? 'I' : value[0];
                                            break;
                                        case "KICKLEN": this.KickMessageLength = int.Parse(value); break;
                                        case "MAXLIST":
                                            foreach (string entry in value.Split(new char[] { ',' })) {
                                                fields = entry.Split(new char[] { ':' }, 2);
                                                this.ListModeLength.Add(fields[0], int.Parse(fields[1]));
                                            }
                                            break;
                                        case "MODES": this.Modes = int.Parse(value); break;
                                        case "NETWORK": this.NetworkName = value; break;
                                        case "NICKLEN": this.NicknameLength = int.Parse(value); break;
                                        case "PREFIX":
                                            this.StatusPrefix = new Dictionary<char, char>();
                                            if (value != "") {
                                                Match m = Regex.Match(value, @"^\(([a-zA-Z]*)\)(.*)$");
                                                for (int j = 0; j < m.Groups[1].Value.Length; ++j)
                                                    this.StatusPrefix.Add(m.Groups[1].Value[j], m.Groups[2].Value[j]);
                                            }
                                            break;
                                        case "STATUSMSG": this.StatusMessage = value.ToCharArray(); break;
                                        case "TARGMAX":
                                            foreach (string field in value.Split(new char[] { ',' })) {
                                                fields = field.Split(new char[] { ':' }, 2);
                                                if (fields[1] == "")
                                                    this.MaxTargets.Remove(fields[0]);
                                                else
                                                    this.MaxTargets.Add(fields[0], int.Parse(fields[1]));
                                            }
                                            break;
                                        case "TOPICLEN": this.TopicLength = int.Parse(value); break;
                                        case "WATCH": this.SupportsWatch = true; break;
                                    }
                                }
                            }
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        break;
                    case "221":  // User modes
                        if (parameters[0] == this.Nickname) this.UserModes = parameters[1];
                        this.OnUserModesGet(new UserModesEventArgs(parameters[1]));
                        break;
                    case "301":  // WHOIS away line
                        this.OnWhoIsAwayLine(new WhoisAwayEventArgs(parameters[1], parameters[2]));
                        break;
                    case "303":  // ISON reply
                        // TODO: This can be trapped as part of a notify feature.
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        break;
                    case "305":  // AWAY cancellation
                        this.Away = false;
                        this.OnAwayCancelled(new AwayEventArgs(parameters[1]));
                        break;
                    case "306":  // AWAY set
                        this.Away = true;
                        if (this.AwaySince == null) this.AwaySince = DateTime.Now;
                        this.OnAwaySet(new AwayEventArgs(parameters[1]));
                        break;
                    case "310":  // WHOIS helper line
                        this.OnWhoIsHelperLine(new WhoisOperEventArgs(parameters[1], parameters[2]));
                        break;
                    case "311":  // WHOIS name line
                        if (this.Users.Contains(parameters[1])) {
                            User _user = this.Users[parameters[1]];
                            _user.Username = parameters[2];
                            _user.Host = parameters[3];
                            _user.FullName = parameters[5];

                            // Parse gender codes.
                            MatchCollection matches = Regex.Matches(_user.FullName, @"\G\x03(\d\d?)(?:,(\d\d?))?\x0F");
                            foreach (Match match in matches) {
                                if (!match.Groups[2].Success) _user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);
                            }
                        }
                        this.OnWhoIsNameLine(new WhoisNameEventArgs(parameters[1], parameters[2], parameters[3], parameters[5]));
                        break;
                    case "312":  // WHOIS server line
                        this.OnWhoIsServerLine(new WhoisServerEventArgs(parameters[1], parameters[2], parameters[3]));
                        break;
                    case "313":  // WHOIS oper line
                        this.OnWhoIsOperLine(new WhoisOperEventArgs(parameters[1], parameters[2]));
                        break;
                    case "314":  // WHOWAS list
                        this.OnWhoWasNameLine(new WhoisNameEventArgs(parameters[1], parameters[2], parameters[3], parameters[5]));
                        break;
                    case "315":  // End of WHO list
                        // TODO: respond to 315 similarly to 366.
                        break;
                    case "317":  // WHOIS idle line
                        this.OnWhoIsIdleLine(new WhoisIdleEventArgs(parameters[1], TimeSpan.FromSeconds(double.Parse(parameters[2])), IRCClient.DecodeUnixTime(double.Parse(parameters[3])), parameters[4]));
                        break;
                    case "318":  // End of WHOIS list
                        this.OnWhoIsEnd(new WhoisEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "319":  // WHOIS channels line
                        this.OnWhoIsChannelLine(new WhoisChannelsEventArgs(parameters[1], parameters[2]));
                        break;
                    case "321":  // LIST header
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        break;
                    case "322":  // Channel list
                        this.OnChannelList(new ChannelListEventArgs(parameters[1], int.Parse(parameters[2]), parameters[3]));
                        break;
                    case "323":  // End of channel list
                        this.OnChannelListEnd(new ChannelListEndEventArgs(parameters[1]));
                        break;
                    case "324":  // Channel modes
                        string channel = parameters[1]; string modes = parameters[2];
                        if (Channels.Contains(channel)) Channels[channel].Modes = modes;
                        this.OnChannelModesGet(new ChannelModesGetEventArgs(channel, modes));
                        break;
                    case "329":  // Channel timestamp
                        time = IRCClient.DecodeUnixTime(double.Parse(parameters[2]));
                        if (Channels.Contains(parameters[1])) Channels[parameters[1]].Timestamp = time;
                        this.OnChannelTimestamp(new ChannelTimestampEventArgs(parameters[1], time));
                        break;
                    case "332":  // Channel topic
                        if (Channels.Contains(parameters[1])) Channels[parameters[1]].Topic = parameters[2];
                        this.OnChannelTopic(new ChannelTopicEventArgs(parameters[1], parameters[2]));
                        break;
                    case "333":  // Channel topic stamp
                        time = IRCClient.DecodeUnixTime(double.Parse(parameters[3]));
                        if (Channels.Contains(parameters[1])) {
                            Channels[parameters[1]].TopicSetter = parameters[2];
                            Channels[parameters[1]].TopicStamp = time;
                        }
                        this.OnChannelTopicStamp(new ChannelTopicStampEventArgs(parameters[1], parameters[2], time));
                        break;
                    case "341":  // Invite sent
                        this.OnInviteSent(new ChannelInviteSentEventArgs(parameters[1], parameters[2]));
                        break;
                    case "346":  // Invite list
                        time = IRCClient.DecodeUnixTime(double.Parse(parameters[4]));
                        this.OnInviteList(new ChannelModeListEventArgs(parameters[1], parameters[2], parameters[3], time));
                        break;
                    case "347":  // End of invite list
                        this.OnInviteListEnd(new ChannelModeListEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "348":  // Exempt list
                        time = IRCClient.DecodeUnixTime(double.Parse(parameters[4]));
                        this.OnExemptList(new ChannelModeListEventArgs(parameters[1], parameters[2], parameters[3], time));
                        break;
                    case "349":  // End of exempt list
                        this.OnExemptListEnd(new ChannelModeListEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "352":  // WHO list
                        // TODO: populate the user list
                        {
                            string[] fields = parameters[7].Split(new char[] { ' ' }, 2);
                            this.HandleWhoList(parameters[1], parameters[2], parameters[3], parameters[4], parameters[5], parameters[6].ToCharArray(), int.Parse(fields[0]), fields[1]);
                            break;
                        }
                    case "353":  // NAMES list
                        {
                            string[] names = parameters[3].Split(new char[] { ' ' });
                            Channel _channel;

                            if (Channels.TryGetValue(parameters[2], out _channel)) {
                                // We are online in the channel. Mark all remembered users.
                                if (_channel.WaitingForNamesList % 2 == 0) {
                                    foreach (ChannelUser channelUser2 in _channel.Users)
                                        channelUser2.Access = (ChannelAccess) (-1);
                                    ++_channel.WaitingForNamesList;
                                }
                            }

                            foreach (string name in names) {
                                ChannelAccess access = ChannelAccess.Normal;
                                for (int i = 0; i < name.Length; ++i) {
                                    char c;
                                    if (this.StatusPrefix.TryGetValue('q', out c) && name[i] == c)
                                        access |= ChannelAccess.Owner;
                                    else if (this.StatusPrefix.TryGetValue('a', out c) && name[i] == c)
                                        access |= ChannelAccess.Admin;
                                    else if (this.StatusPrefix.TryGetValue('o', out c) && name[i] == c)
                                        access |= ChannelAccess.Op;
                                    else if (this.StatusPrefix.TryGetValue('h', out c) && name[i] == c)
                                        access |= ChannelAccess.HalfOp;
                                    else if (this.StatusPrefix.TryGetValue('v', out c) && name[i] == c)
                                        access |= ChannelAccess.Voice;
                                    else if (this.StatusPrefix.TryGetValue('V', out c) && name[i] == c)
                                        access |= ChannelAccess.HalfVoice;
                                    else {
                                        string nickname = name.Substring(i);
                                        if (!this.Users.Contains(nickname)) {
                                            User _user = new User(this, nickname, "*", "*");
                                            _user.Channels.Add(_channel);
                                            this.Users.Add(_user);
                                        }
                                        if (_channel.Users.Contains(nickname))
                                            _channel.Users[nickname].Access = access;
                                        else
                                            _channel.Users.Add(new ChannelUser(nickname, this) { Access = access });
                                        break;
                                    }
                                }
                            }

                            this.OnNames(new ChannelNamesEventArgs(parameters[2], parameters[3]));
                            break;
                        }
                    case "366":  // End of NAMES list
                        if (this.Channels.Contains(parameters[1])) {
                            if (this.Channels[parameters[1]].WaitingForNamesList % 2 != 0) {
                                for (int i = this.Channels[parameters[1]].Users.Count - 1; i >= 0; --i) {
                                    ChannelUser _user = this.Channels[parameters[1]].Users[i];
                                    if (_user.Access < 0) this.Channels[parameters[1]].Users.Remove(_user.Nickname);
                                }
                                --this.Channels[parameters[1]].WaitingForNamesList;
                            }
                        }

                        this.OnNamesEnd(new ChannelModeListEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "367":  // Ban list
                        time = IRCClient.DecodeUnixTime(double.Parse(parameters[4]));
                        this.OnBanList(new ChannelModeListEventArgs(parameters[1], parameters[2], parameters[3], time));
                        break;
                    case "368":  // End of ban list
                        this.OnBanListEnd(new ChannelModeListEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "369":  // End of WHOWAS list
                        this.OnWhoWasEnd(new WhoisEndEventArgs(parameters[1], parameters[2]));
                        break;
                    case "404":  // Cannot send to channel  (Any similarity with HTTP 404 is (probably) purely coincidential. (^_^))
                        this.OnChannelMessageDenied(new ChannelDeniedEventArgs(parameters[1], 0, parameters[2]));
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        break;
                    case "432":  // Erroneous nickname
                        this.OnNicknameInvalid(new NicknameEventArgs(parameters[1], parameters[2]));
                        break;
                    case "433":  // Nickname already in use
                        this.OnNicknameTaken(new NicknameEventArgs(parameters[1], parameters[2]));
                        if (!this.IsRegistered && this.Nicknames.Length > 1) {
                            for (int i = 0; i < this.Nicknames.Length - 1; ++i) {
                                if (this.Nicknames[i] == parameters[1]) {
                                    this._Nickname = this.Nicknames[i + 1];
                                    this.Send("NICK {0}", this.Nickname);
                                    break;
                                }
                            }
                        }
                        break;
                    case "436":  // Nickname collision KILL
                        this.OnKilled(new PrivateMessageEventArgs(this.Users.Get(prefix, false), this.Nickname, parameters[2]));
                        break;
                    case "471":  // Cannot join a channel because it has reached its limit
                        OnChannelJoinDenied(new ChannelDeniedEventArgs(parameters[0], ChannelJoinDeniedReason.Limit, parameters[1]));
                        break;
                    case "473":  // Cannot join a channel because it's invite-only
                        OnChannelJoinDenied(new ChannelDeniedEventArgs(parameters[0], ChannelJoinDeniedReason.InviteOnly, parameters[1]));
                        break;
                    case "474":  // Cannot join a channel because we're banned
                        OnChannelJoinDenied(new ChannelDeniedEventArgs(parameters[0], ChannelJoinDeniedReason.Banned, parameters[1]));
                        break;
                    case "475":  // Cannot join a channel because of a key failure
                        OnChannelJoinDenied(new ChannelDeniedEventArgs(parameters[0], ChannelJoinDeniedReason.KeyFailure, parameters[1]));
                        break;
                    case "598":  // Watched user went away
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(parameters[0])) {
                                this.Users[parameters[0]].Away = true;
                                this.Users[parameters[0]].AwayReason = parameters[4];
                                this.Users[parameters[0]].AwaySince = DateTime.Now;
                            }
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "599":  // Watched user came back
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(parameters[0])) {
                                this.Users[parameters[0]].Away = false;
                            }
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "602":  // Stopped watching
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(parameters[1])) {
                                this.Users[parameters[1]].Watched = false;
                                if (this.Users[parameters[1]].Channels.Count == 0)
                                    this.Users.Remove(parameters[1]);
                            }
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "604":  // Watched user is online
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(parameters[1]))
                                this.Users[parameters[1]].Watched = true;
                            else
                                this.Users.Add(new User(this, parameters[1], parameters[2], parameters[3]) { Watched = true });
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "601":  // Watched user went offline
                    case "605":  // Watched user is offline
                        if (this.SupportsWatch)
                            this.Users.Remove(parameters[1]);
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "609":  // Watched user is away
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(parameters[1])) {
                                this.Users[parameters[1]].Away = true;
                                this.Users[parameters[1]].AwayReason = null;
                                this.Users[parameters[1]].AwaySince = DateTime.Now;
                            }
                        }
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, parameters[1]));
                        break;
                    case "900":  // Logged in
                        this.Me.Account = parameters[2];
                        break;
                    case "901":  // Logged out
                        this.Me.Account = null;
                        break;
                    case "903":  // SASL authentication successful
                        this.Send("CAP END");
                        break;
                    case "902":  // SASL username rejected
                    case "904":  // SASL authentication failed
                    case "905":  // SASL response too long
                    case "906":  // SASL authentication aborted
                        this.Send("CAP END");
                        break;
                    case "ACCOUNT":
                        user = this.Users.Get(prefix, false);
                        if (parameters[0] == "*")
                            user.Account = null;
                        else
                            user.Account = parameters[0];
                        break;
                    case "AUTHENTICATE":
                        if (parameters[0] == "+" && this.SASLUsername != null && this.SASLPassword != null) {
                            // Authenticate using SASL.
                            byte[] responseBytes; string response;
                            byte[] usernameBytes; byte[] passwordBytes;

                            usernameBytes = Encoding.UTF8.GetBytes(this.SASLUsername);
                            passwordBytes = Encoding.UTF8.GetBytes(this.SASLPassword);
                            responseBytes = new byte[usernameBytes.Length * 2 + passwordBytes.Length + 2];
                            usernameBytes.CopyTo(responseBytes, 0);
                            usernameBytes.CopyTo(responseBytes, usernameBytes.Length + 1);
                            passwordBytes.CopyTo(responseBytes, (usernameBytes.Length + 1) * 2);

                            response = Convert.ToBase64String(responseBytes);
                            this.Send("AUTHENTICATE :" + response);
                        } else {
                            // Unrecognised challenge or no credentials given; abort.
                            this.Send("AUTHENTICATE *");
                            this.Send("CAP END");
                        }
                        break;
                    case "CAP":
                        string subcommand = parameters[1];
                        switch (subcommand.ToUpperInvariant()) {
                            case "LS":
                                List<string> supportedCapabilities = new List<string>();
                                MatchCollection matches = Regex.Matches(parameters[2], @"\G *(-)?(~)?(=)?([^ ]+)");
                                foreach (Match match in matches) {
                                    if (match.Groups[4].Value == "multi-prefix" || 
                                        match.Groups[4].Value == "extended-join" || 
                                        match.Groups[4].Value == "account-notify" ||
                                        (this.SASLUsername != null && match.Groups[4].Value == "sasl")) {
                                        if (!supportedCapabilities.Contains(match.Groups[4].Value))
                                            supportedCapabilities.Add(match.Groups[4].Value);
                                    }
                                }
                                if (supportedCapabilities.Count > 0)
                                    this.Send("CAP REQ :" + string.Join(" ", supportedCapabilities));
                                else
                                    this.Send("CAP END");
                                break;
                            case "ACK":
                                if (Regex.IsMatch(parameters[2], @"(?<![^ ])[-~=]*sasl(?![^ ])") && this.SASLUsername != null) {
                                    // TODO: SASL authentication
                                    this.Send("AUTHENTICATE PLAIN");
                                } else
                                    this.Send("CAP END");
                                break;
                            case "NAK":
                                this.Send("CAP END");
                                break;
                        }
                        break;
                    case "ERROR":
                        this.OnServerError(new ServerErrorEventArgs(parameters[0]));
                        break;
                    case "INVITE":
                        this.OnInvite(new ChannelInviteEventArgs(this.Users.Get(prefix, false), parameters[0], parameters[1]));
                        break;
                    case "JOIN":
                        user = this.Users.Get(prefix, this.Channels.Contains(parameters[0]));

                        if (user.Nickname == this.Nickname) {
                            Channel newChannel = new Channel(parameters[0], this) {
                                OwnStatus = 0,
                                Users = new ChannelUserCollection(this)
                            };
                            newChannel.Users.Add(new ChannelUser(user.Nickname, this));
                            this.Channels.Add(newChannel);
                            user.Channels.Add(this.Channels[parameters[0]]);
                            this.OnChannelJoinSelf(new ChannelJoinEventArgs(user, parameters[0]));
                        } else {
                            this.Channels[parameters[0]].Users.Add(new ChannelUser(user.Nickname, this));
                            user.Channels.Add(this.Channels[parameters[0]]);
                            this.OnChannelJoin(new ChannelJoinEventArgs(user, parameters[0]));
                        }

                        if (parameters.Length == 3) {
                            // Extended join
                            if (parameters[1] == "*")
                                user.Account = null;
                            else
                                user.Account = parameters[1];
                            user.FullName = parameters[2];
                        }

                        break;
                    case "KICK":
                        user = this.Users.Get(prefix, false);
                        if (parameters[1].Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelKickSelf(new ChannelKickEventArgs(user, parameters[0], this.Channels[parameters[0]].Users[parameters[1]], parameters.Length >= 3 ? parameters[2] : null));
                            this.Channels.Remove(parameters[0]);
                        } else {
                            this.OnChannelKick(new ChannelKickEventArgs(user, parameters[0], this.Channels[parameters[0]].Users[parameters[1]], parameters.Length >= 3 ? parameters[2] : null));
                            if (this.Channels[parameters[0]].Users.Contains(parameters[1]))
                                this.Channels[parameters[0]].Users.Remove(parameters[1]);
                        }
                        break;
                    case "KILL":
                        user = this.Users.Get(prefix, false);
                        if (parameters[0].Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnKilled(new PrivateMessageEventArgs(user, this.Nickname, parameters[1]));
                        }
                        break;
                    case "MODE":
                        if (this.IsChannel(parameters[0])) {
                            int index = 2; bool direction = true;
                            foreach (char c in parameters[1]) {
                                if (c == '+')
                                    direction = true;
                                else if (c == '-')
                                    direction = false;
                                else if (this.ChanModes.TypeA.Contains(c))
                                    this.OnChannelMode(prefix, parameters[0], direction, c, parameters[index++]);
                                else if (this.ChanModes.TypeB.Contains(c))
                                    this.OnChannelMode(prefix, parameters[0], direction, c, parameters[index++]);
                                else if (this.ChanModes.TypeC.Contains(c)) {
                                    if (direction)
                                        this.OnChannelMode(prefix, parameters[0], direction, c, parameters[index++]);
                                    else
                                        this.OnChannelMode(prefix, parameters[0], direction, c, null);
                                } else if (this.ChanModes.TypeD.Contains(c))
                                    this.OnChannelMode(prefix, parameters[0], direction, c, null);
                                else if (this.StatusPrefix.ContainsKey(c))
                                    this.OnChannelMode(prefix, parameters[0], direction, c, parameters[index++]);
                                else
                                    this.OnChannelMode(prefix, parameters[0], direction, c, null);
                            }
                        }
                        break;
                    case "NICK":
                        user = this.Users.Get(prefix, false);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this._Nickname = parameters[0];
                            this.OnNicknameChangeSelf(new NicknameChangeEventArgs(user, this.Nickname));
                        } else {
                            this.OnNicknameChange(new NicknameChangeEventArgs(user, parameters[0]));
                        }

                        foreach (Channel _channel in this.Channels) {
                            if (_channel.Users.Contains(user.Nickname)) {
                                _channel.Users.Remove(user.Nickname);
                                // TODO: Fix this
                                _channel.Users.Add(new ChannelUser(parameters[0], this));
                            }
                        }

                        if (this.Users.TryGetValue(user.Nickname, out user)) {
                            this.Users.Remove(user.Nickname);
                            user.Nickname = parameters[0];
                            this.Users.Add(user);
                        }

                        break;
                    case "NOTICE":
                        if (this.IsChannel(parameters[0])) {
                            this.OnChannelNotice(new ChannelMessageEventArgs(this.Users.Get(prefix ?? this.Address, false), parameters[0], parameters[1]));
                        } else if (prefix == null || prefix.Split(new char[] { '!' }, 2)[0].Contains(".")) {
                            // TODO: fix this
                            this.OnServerNotice(new PrivateMessageEventArgs(this.Users.Get(prefix ?? this.Address, false), parameters[0], parameters[1]));
                        } else {
                            this.OnPrivateNotice(new PrivateMessageEventArgs(this.Users.Get(prefix ?? this.Address, false), parameters[0], parameters[1]));
                        }
                        break;
                    case "PART":
                        user = this.Users.Get(prefix, false);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelPartSelf(new ChannelPartEventArgs(user, parameters[0], parameters.Length == 1 ? null : parameters[1]));
                            this.Channels.Remove(parameters[0]);
                        } else {
                            this.OnChannelPart(new ChannelPartEventArgs(user, parameters[0], parameters.Length == 1 ? null : parameters[1]));
                            if (this.Channels[parameters[0]].Users.Contains(user.Nickname))
                                this.Channels[parameters[0]].Users.Remove(user.Nickname);
                        }
                        break;
                    case "PING":
                        this.OnPing(new PingEventArgs(parameters.Length == 0 ? null : parameters[0]));
                        this.Send(parameters.Length == 0 ? "PONG" : "PONG :" + parameters[0]);
                        break;
                    case "PONG":
                        this.OnPingReply(new PingEventArgs(prefix));
                        break;
                    case "PRIVMSG":
                        user = this.Users.Get(prefix, false);

                        if (this.IsChannel(parameters[0])) {
                            // It's a channel message.
                            if (parameters[1].Length > 1 && parameters[1].StartsWith("\u0001") && parameters[1].EndsWith("\u0001")) {
                                string CTCPMessage = parameters[1].Trim(new char[] { '\u0001' });
                                string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                                if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                                    this.OnChannelAction(new ChannelMessageEventArgs(user, parameters[0], fields.ElementAtOrDefault(1) ?? ""));
                                } else {
                                    this.OnChannelCTCP(new ChannelMessageEventArgs(user, parameters[0], CTCPMessage));
                                }
                            } else {
                                this.OnChannelMessage(new ChannelMessageEventArgs(user, parameters[0], parameters[1]));
                            }
                        } else {
                            // It's a private message.
                            if (parameters[1].Length > 1 && parameters[1].StartsWith("\u0001") && parameters[1].EndsWith("\u0001")) {
                                string CTCPMessage = parameters[1].Trim(new char[] { '\u0001' });
                                string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                                if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                                    this.OnPrivateAction(new PrivateMessageEventArgs(user, parameters[0], fields.ElementAtOrDefault(1) ?? ""));
                                } else {
                                    this.OnPrivateCTCP(new PrivateMessageEventArgs(user, parameters[0], CTCPMessage));
                                }
                            } else {
                                this.OnPrivateMessage(new PrivateMessageEventArgs(user, parameters[0], parameters[1]));
                            }
                        }
                        break;
                    case "QUIT":
                        user = this.Users.Get(prefix, false);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnQuitSelf(new QuitEventArgs(user, parameters[0]));
                            this.Channels.Clear();
                        } else {
                            this.OnQuit(new QuitEventArgs(user, parameters[0]));
                            foreach (Channel _channel in this.Channels) {
                                if (_channel.Users.Contains(user.Nickname))
                                    _channel.Users.Remove(user.Nickname);
                            }
                        }
                        break;
                    case "TOPIC":
                        user = this.Users.Get(prefix, false);
                        this.OnChannelTopicChange(new ChannelTopicChangeEventArgs(new ChannelUser(user.Nickname, this), parameters[0], parameters[1]));
                        break;
                    default:
                        this.OnServerMessage(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        this.OnServerMessageUnhandled(new ServerMessageEventArgs(prefix, command, parameters, string.Join(" ", parameters)));
                        this.OnRawLineUnhandled(new RawParsedEventArgs(data, prefix, command, parameters));
                        break;
                }
            }
        }

        /// <summary>Sends a raw message to the IRC server.</summary>
        /// <param name="data">The message to send.</param>
        /// <exception cref="System.InvalidOperationException">This IRCClient is not connected to a server.</exception>
        public virtual void Send(string data) {
            if (!tcpClient.Connected) throw new InvalidOperationException("The client is not connected.");

            this.OnRawLineSent(new RawEventArgs(data));

            StreamWriter w;
            if (SSLStream != null)
                w = new StreamWriter(SSLStream);
            else
                w = new StreamWriter(tcpClient.GetStream());
            w.Write(data + "\r\n");
            w.Flush();

            string[] fields = data.Split(new char[] { ' ' });
            if (fields[0].Equals("QUIT", StringComparison.OrdinalIgnoreCase) && data != "QUIT :Ping timeout; reconnecting.")
                this.VoluntarilyQuit = true;
            else if (fields[0].Equals("PRIVMSG", StringComparison.OrdinalIgnoreCase))
                this.LastSpoke = DateTime.Now;
        }

        /// <summary>Sends a raw message to the IRC server.</summary>
        /// <param name="format">The format of the message, as per string.Format.</param>
        /// <param name="parameters">The parameters to include in the message.</param>
        /// <exception cref="System.InvalidOperationException">This IRCClient is not connected to a server.</exception>
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

        private void OnChannelMode(string sender, string target, bool direction, char mode, string parameter) {
            ChannelUser[] matchedUsers;
            switch (mode) {
                case 'I':
                    if (!this.ChanModes.TypeA.Contains(mode)) return;
                    matchedUsers = FindMatchingUsers(target, parameter);
                    if (direction) {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelInviteExemptSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelInviteExempt(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    } else {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelRemoveInviteExemptSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelRemoveInviteExempt(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    }
                    break;
                case 'V':
                    if (!this.StatusPrefix.ContainsKey(mode)) return;
                    if (direction) {
                        this.Channels[target].Users[parameter].Access |= ChannelAccess.HalfVoice;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelHalfVoiceSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelHalfVoice(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    } else {
                        this.Channels[target].Users[parameter].Access &= ~ChannelAccess.HalfVoice;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelDeHalfVoiceSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelDeHalfVoice(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    }
                    break;
                case 'a':
                    if (!this.StatusPrefix.ContainsKey(mode)) return;
                    if (direction) {
                        this.Channels[target].Users[parameter].Access |= ChannelAccess.Admin;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelAdminSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelAdmin(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    } else {
                        this.Channels[target].Users[parameter].Access &= ~ChannelAccess.Admin;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelDeAdminSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelDeAdmin(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    }
                    break;
                case 'b':
                    if (!this.ChanModes.TypeA.Contains(mode)) return;
                    matchedUsers = FindMatchingUsers(target, parameter);
                    if (direction) {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelBanSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelBan(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    } else {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelUnBanSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelUnBan(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    }
                    break;
                case 'e':
                    if (!this.ChanModes.TypeA.Contains(mode)) return;
                    matchedUsers = FindMatchingUsers(target, parameter);
                    if (direction) {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelExemptSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelExempt(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    } else {
                        if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                            this.OnChannelRemoveExemptSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        } else {
                            this.OnChannelRemoveExempt(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                        }
                    }
                    break;
                case 'h':
                    if (!this.StatusPrefix.ContainsKey(mode)) return;
                    if (direction) {
                        this.Channels[target].Users[parameter].Access |= ChannelAccess.HalfOp;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelHalfOpSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelHalfOp(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    } else {
                        this.Channels[target].Users[parameter].Access &= ~ChannelAccess.HalfOp;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelDeHalfOpSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelDeHalfOp(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    }
                    break;
                case 'o':
                    if (!this.StatusPrefix.ContainsKey(mode)) return;
                    if (direction) {
                        this.Channels[target].Users[parameter].Access |= ChannelAccess.Op;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelOpSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelOp(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    } else {
                        this.Channels[target].Users[parameter].Access &= ~ChannelAccess.Op;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelDeOpSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelDeOp(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    }
                    break;
                case 'q':
                    if (this.StatusPrefix.ContainsKey(mode)) {
                        if (direction) {
                            this.Channels[target].Users[parameter].Access |= ChannelAccess.Owner;
                            if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                                this.OnChannelOwnerSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                            } else {
                                this.OnChannelOwner(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                            }
                        } else {
                            this.Channels[target].Users[parameter].Access &= ~ChannelAccess.Owner;
                            if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                                this.OnChannelDeOwnerSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                            } else {
                                this.OnChannelDeOwner(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                            }
                        }
                    } else if (this.ChanModes.TypeA.Contains(mode)) {
                        matchedUsers = FindMatchingUsers(target, parameter);
                        if (direction) {
                            if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                                this.OnChannelQuietSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                            } else {
                                this.OnChannelQuiet(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                            }
                        } else {
                            if (matchedUsers.Any(user => this.CaseMappingComparer.Equals(user.Nickname, this.Nickname))) {
                                this.OnChannelUnQuietSelf(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                            } else {
                                this.OnChannelUnQuiet(new ChannelListModeEventArgs(this.Users.Get(sender, false), target, parameter, matchedUsers));
                            }
                        }
                    }
                    break;
                case 'v':
                    if (!this.StatusPrefix.ContainsKey(mode)) return;
                    if (direction) {
                        this.Channels[target].Users[parameter].Access |= ChannelAccess.Voice;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelVoiceSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelVoice(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    } else {
                        this.Channels[target].Users[parameter].Access &= ~ChannelAccess.Voice;
                        if (parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.OnChannelDeVoiceSelf(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        } else {
                            this.OnChannelDeVoice(new ChannelNicknameModeEventArgs(this.Users.Get(sender, false), target, new ChannelUser(parameter, this)));
                        }
                    }
                    break;
            }
        }

        /// <summary>Searches the users on a channel for those matching a specified hostmask.</summary>
        /// <param name="Channel">The channel to search.</param>
        /// <param name="Mask">The hostmask to search for.</param>
        /// <returns>A list of ChannelUser objects representing the matching users.</returns>
        public ChannelUser[] FindMatchingUsers(string Channel, string Mask) {
            List<ChannelUser> MatchedUsers = new List<ChannelUser>();
            StringBuilder exBuilder = new StringBuilder();

            foreach (char c in Mask) {
                if (c == '*') exBuilder.Append(".*");
                else if (c == '?') exBuilder.Append(".");
                else exBuilder.Append(Regex.Escape(c.ToString()));
            }
            Mask = exBuilder.ToString();

            foreach (ChannelUser user in this.Channels[Channel].Users) {
                if (Regex.IsMatch(user.User.ToString(), Mask)) MatchedUsers.Add(user);
            }

            return MatchedUsers.ToArray();
        }

        internal void HandleWhoList(string channelName, string username, string address, string server, string nickname, char[] flags, int hops, string fullName) {
            User user = null; Channel channel = null; ChannelUser channelUser = null;

            if (this.IsChannel(channelName) && this.Channels.TryGetValue(channelName, out channel)) {
                // We are in a common channel with this person.
                if (!channel.Users.TryGetValue(nickname, out channelUser)) {
                    channelUser = new ChannelUser(nickname, this);
                    channel.Users.Add(channelUser);
                }
                channelUser.Access = 0;
            }
            if (!this.Users.TryGetValue(nickname, out user)) {
                if (channelUser != null) {
                    user = new User(this, nickname, username, address) { Client = this };
                    user.Channels.Add(channel);
                }
            } else {
                if (channel != null && !user.Channels.Contains(channelName))
                    user.Channels.Add(channel);
            }

            if (user != null) {
                user.Username = username;
                user.Host = address;
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
                        user.AwayReason = null;
                    } else if (flag == 'G') {
                        if (!user.Away) {
                            user.Away = true;
                            user.AwayReason = null;
                            user.AwaySince = DateTime.Now;
                        }
                    } else if (flag == '*') {
                        user.Oper = true;
                    } else if (channelUser != null) {
                        if (this.StatusPrefix.TryGetValue('q', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.Owner;
                        else if (this.StatusPrefix.TryGetValue('a', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.Admin;
                        else if (this.StatusPrefix.TryGetValue('o', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.Op;
                        else if (this.StatusPrefix.TryGetValue('h', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.HalfOp;
                        else if (this.StatusPrefix.TryGetValue('v', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.Voice;
                        else if (this.StatusPrefix.TryGetValue('V', out mode) && mode == flag)
                            channelUser.Access |= ChannelAccess.HalfVoice;
                    }
                }
            }
            this.OnWhoList(new WhoListEventArgs(channelName, username, address, server, nickname, flags, hops, fullName));
        }

        /// <summary>Determines whether the speciied string is a valid channel name.</summary>
        /// <param name="target">The string to check.</param>
        /// <returns>True if the specified string is a valid channel name; false if it is not.</returns>
        public bool IsChannel(string target) {
            if (target == null || target == "") return false;
            foreach (char c in this.ChannelTypes)
                if (target[0] == c) return true;
            return false;
        }
    }
}
