/**** CompuChat - IRC Connection Module ****
 * by Andrio Celos
 * Created  20 August    2012
 * Modified  2 September 2014
 *
 * This module contains the IRCClient and related classes, which manage a connection
 * with an IRC server.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

namespace IRC {
    public struct ChannelModes {
        public char[] TypeAModes;
        public char[] TypeBModes;
        public char[] TypeCModes;
        public char[] TypeDModes;
        public ChannelModes(char[] A, char[] B, char[] C, char[] D) {
            this = default(IRC.ChannelModes);
            this.TypeAModes = A;
            this.TypeBModes = B;
            this.TypeCModes = C;
            this.TypeDModes = D;
        }
    }

    [Flags]
    public enum ChannelAccess : short {
        Normal = 0,
        HalfVoice = 1,
        Voice = 2,
        HalfOp = 4,
        Op = 8,
        Admin = 16,
        Owner = 32
    }

    public class IRCClient {

        #region Event handlers
        public delegate void AwayCancelledEventHandler(IRCClient sender, string Message);
        public delegate void AwaySetEventHandler(IRCClient sender, string Message);
        public delegate void BanListEventHandler(IRCClient sender, string Channel, string BannedUser, string BanningUser, DateTime Time);
        public delegate void BanListEndEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void NicknameChangeEventHandler(IRCClient sender, string Sender, string NewNick);
        public delegate void NicknameChangeSelfEventHandler(IRCClient sender, string Sender, string NewNick);
        public delegate void ChannelActionEventHandler(IRCClient sender, string Sender, string Channel, string Action);
        public delegate void ChannelActionHighlightEventHandler(IRCClient sender, string Sender, string Channel, string Action);
        public delegate void ChannelAdminEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelAdminSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelBanEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelBanSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelTimestampEventHandler(IRCClient sender, string Channel, DateTime Timestamp);
        public delegate void ChannelCTCPEventHandler(IRCClient sender, string Sender, string Channel, string Message);
        public delegate void ChannelDeAdminEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeAdminSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeHalfOpEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeHalfOpSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeHalfVoiceEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeHalfVoiceSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeOpEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeOpSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeOwnerEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeOwnerSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeVoiceEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelDeVoiceSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelExemptEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelExemptSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelHalfOpEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelHalfOpSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelHalfVoiceEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelHalfVoiceSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelInviteExemptEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelInviteExemptSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelJoinEventHandler(IRCClient sender, string Sender, string Channel);
        public delegate void ChannelJoinSelfEventHandler(IRCClient sender, string Sender, string Channel);
        public delegate void ChannelJoinDeniedBannedEventHandler(IRCClient sender, string Channel);
        public delegate void ChannelJoinDeniedFullEventHandler(IRCClient sender, string Channel);
        public delegate void ChannelJoinDeniedInviteEventHandler(IRCClient sender, string Channel);
        public delegate void ChannelJoinDeniedKeyEventHandler(IRCClient sender, string Channel);
        public delegate void ChannelKickEventHandler(IRCClient sender, string Sender, string Channel, string Target, string Reason);
        public delegate void ChannelKickSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string Reason);
        public delegate void ChannelListEventHandler(IRCClient sender, string Channel, int Users, string Topic);
        public delegate void ChannelMessageEventHandler(IRCClient sender, string Sender, string Channel, string Message);
        public delegate void ChannelMessageSendDeniedEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void ChannelMessageHighlightEventHandler(IRCClient sender, string Sender, string Channel, string Message);
        public delegate void ChannelModeEventHandler(IRCClient sender, string Sender, string Channel, bool Direction, string Mode);
        public delegate void ChannelModeSelfEventHandler(IRCClient sender, string Sender, string Channel, bool Direction, string Mode);
        public delegate void ChannelModeUnhandledEventHandler(IRCClient sender, string Sender, string Channel, bool Direction, string Mode);
        public delegate void ChannelModesSetEventHandler(IRCClient sender, string Sender, string Channel, string Modes);
        public delegate void ChannelModesGetEventHandler(IRCClient sender, string Channel, string Modes);
        public delegate void ChannelNoticeEventHandler(IRCClient sender, string Sender, string Channel, string Message);
        public delegate void ChannelOpEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelOpSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelOwnerEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelOwnerSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelPartEventHandler(IRCClient sender, string Sender, string Channel, string Reason);
        public delegate void ChannelPartSelfEventHandler(IRCClient sender, string Sender, string Channel, string Reason);
        public delegate void ChannelQuietEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelQuietSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelRemoveExemptEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelRemoveExemptSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelRemoveInviteExemptEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelRemoveInviteExemptSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelRemoveKeyEventHandler(IRCClient sender, string Sender, string Channel);
        public delegate void ChannelRemoveLimitEventHandler(IRCClient sender, string Sender, string Channel);
        public delegate void ChannelSetKeyEventHandler(IRCClient sender, string Sender, string Channel, string Key);
        public delegate void ChannelSetLimitEventHandler(IRCClient sender, string Sender, string Channel, int Limit);
        public delegate void ChannelTopicEventHandler(IRCClient sender, string Channel, string Topic);
        public delegate void ChannelTopicChangeEventHandler(IRCClient sender, string Sender, string Channel, string NewTopic);
        public delegate void ChannelTopicStampEventHandler(IRCClient sender, string Channel, string Setter, DateTime SetDate);
        public delegate void ChannelUsersEventHandler(IRCClient sender, string Channel, string Names);
        public delegate void ChannelUnBanEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelUnBanSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelUnQuietEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelUnQuietSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target, string[] MatchedUsers);
        public delegate void ChannelVoiceEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ChannelVoiceSelfEventHandler(IRCClient sender, string Sender, string Channel, string Target);
        public delegate void ConnectedEventHandler(IRCClient sender);
        public delegate void ConnectingEventHandler(IRCClient sender, string Host, IPEndPoint Endpoint);
        public delegate void ConnectingFailedEventHandler(IRCClient sender, Exception Exception);
        public delegate void PrivateCTCPEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void SendCTCPEventHandler(IRCClient sender, string Target, string Message);
        public delegate void SendDCCChatOfferEventHandler(IRCClient sender, string Target);
        public delegate void SendDCCOfferEventHandler(IRCClient sender, string Target, string Message);
        public delegate void SendMessageEventHandler(IRCClient sender, string Target, string Message);
        public delegate void DCCChatOfferEventHandler(IRCClient sender, string Sender);
        public delegate void DCCChatAlreadyOfferingEventHandler(IRCClient sender, string Target);
        public delegate void DCCOfferEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void DCCBadOfferEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void DCCFileResumeRequestEventHandler(IRCClient sender, string Sender, string Filename, object Position);
        public delegate void DCCFileSendOfferEventHandler(IRCClient sender, string Sender, string Filename, long Length);
        public delegate void DisconnectedEventHandler(IRCClient sender, string ErrorMessage);
        public delegate void WaitingToReconnectEventHandler(IRCClient sender, decimal Interval, int Attempts, int MaxAttempts);
        public delegate void ExceptionEventHandler(IRCClient sender, Exception Exception);
        public delegate void ExemptListEventHandler(IRCClient sender, string Channel, string ExemptedUser, string ExemptingUser, DateTime Time);
        public delegate void ExemptListEndEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void InviteEventHandler(IRCClient sender, string Sender, string Channel);
        public delegate void InviteSentEventHandler(IRCClient sender, string Target, string Channel);
        public delegate void InviteListEventHandler(IRCClient sender, string Channel, string InvitedUser, string InvitingUser, DateTime Time);
        public delegate void InviteListEndEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void InviteExemptListEventHandler(IRCClient sender, string Channel, string ExemptedUser, string ExemptingUser, DateTime Time);
        public delegate void InviteExemptListEndEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void KilledEventHandler(IRCClient sender, string Sender, string Reason);
        public delegate void LookingUpHostEventHandler(IRCClient sender, string Hostname);
        public delegate void LookingUpHostFailedEventHandler(IRCClient sender, string Hostname, string ErrorMessage);
        public delegate void MOTDEventHandler(IRCClient sender, string Message);
        public delegate void MOTDSkippedEventHandler(IRCClient sender);
        public delegate void NamesEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void NamesEndEventHandler(IRCClient sender, string Channel, string Message);
        public delegate void NicknameInvalidEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void NicknameTakenEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void NicknameChangeFailedEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void PrivateNoticeEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void SendNoticeEventHandler(IRCClient sender, string Target, string Message);
        public delegate void PingEventHandler(IRCClient sender, string ServerName);
        public delegate void PingReplyEventHandler(IRCClient sender, string Sender);
        public delegate void PrivateMessageEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void PrivateActionEventHandler(IRCClient sender, string Sender, string Action);
        public delegate void QuitEventHandler(IRCClient sender, string Sender, string Reason);
        public delegate void QuitSelfEventHandler(IRCClient sender, string Sender, string Reason);
        public delegate void RawLineReceivedEventHandler(IRCClient sender, string Message);
        public delegate void RawLineReceivedUnhandledEventHandler(IRCClient sender, string Message);
        public delegate void RawLineSentEventHandler(IRCClient sender, string Message);
        public delegate void UserModesSetEventHandler(IRCClient sender, string Sender, string Modes);
        public delegate void WallopsEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void ServerNoticeEventHandler(IRCClient sender, string Sender, string Message);
        public delegate void ServerErrorEventHandler(IRCClient sender, string Message);
        public delegate void ServerMessageEventHandler(IRCClient sender, string Sender, string Numeric, string[] Parameters, string Message);
        public delegate void ServerMessageUnhandledEventHandler(IRCClient sender, string Sender, string Numeric, string[] Parameters, string Message);
        public delegate void TimeOutEventHandler(IRCClient sender);
        public delegate void WhoListEventHandler(IRCClient sender, string Channel, string Username, string Address, string Server, string Nickname, string Flags, int Hops, string FullName);
        public delegate void WhoIsAuthenticationLineEventHandler(IRCClient sender, string Nickname, string Message, string Account);
        public delegate void WhoIsAwayLineEventHandler(IRCClient sender, string Nickname, string AwayReason);
        public delegate void WhoIsChannelLineEventHandler(IRCClient sender, string Nickname, string Channels);
        public delegate void WhoIsEndEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void WhoIsIdentifiedEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void WhoIsIdleLineEventHandler(IRCClient sender, string Nickname, TimeSpan IdleTime, DateTime SignOnTime, string Message);
        public delegate void WhoIsNameLineEventHandler(IRCClient sender, string Nickname, string Username, string Host, string FullName);
        public delegate void WhoIsOperLineEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void WhoIsHelperLineEventHandler(IRCClient sender, string Nickname, string Message);
        public delegate void WhoIsRealHostLineEventHandler(IRCClient sender, string Nickname, string Message, string RealUsername, string RealHost, string RealIP);
        public delegate void WhoIsServerLineEventHandler(IRCClient sender, string Nickname, string Server, string Info);
        public delegate void WhoIsSpecialLineEventHandler(IRCClient sender, string Nickname, string Numeric, string Message);
        public delegate void WhoWasNameLineEventHandler(IRCClient sender, string Nickname, string Username, string Host, string FullName);
        public delegate void WhoWasEndEventHandler(IRCClient sender, string Nickname, string Message);
        #endregion

        #region Events
        public event IRCClient.AwayCancelledEventHandler AwayCancelled;
        public event IRCClient.AwaySetEventHandler AwaySet;
        public event IRCClient.BanListEventHandler BanList;
        public event IRCClient.BanListEndEventHandler BanListEnd;
        public event IRCClient.NicknameChangeEventHandler NicknameChange;
        public event IRCClient.NicknameChangeSelfEventHandler NicknameChangeSelf;
        public event IRCClient.ChannelActionEventHandler ChannelAction;
        public event IRCClient.ChannelActionHighlightEventHandler ChannelActionHighlight;
        public event IRCClient.ChannelAdminEventHandler ChannelAdmin;
        public event IRCClient.ChannelAdminSelfEventHandler ChannelAdminSelf;
        public event IRCClient.ChannelBanEventHandler ChannelBan;
        public event IRCClient.ChannelBanSelfEventHandler ChannelBanSelf;
        public event IRCClient.ChannelTimestampEventHandler ChannelTimestamp;
        public event IRCClient.ChannelCTCPEventHandler ChannelCTCP;
        public event IRCClient.ChannelDeAdminEventHandler ChannelDeAdmin;
        public event IRCClient.ChannelDeAdminSelfEventHandler ChannelDeAdminSelf;
        public event IRCClient.ChannelDeHalfOpEventHandler ChannelDeHalfOp;
        public event IRCClient.ChannelDeHalfOpSelfEventHandler ChannelDeHalfOpSelf;
        public event IRCClient.ChannelDeHalfVoiceEventHandler ChannelDeHalfVoice;
        public event IRCClient.ChannelDeHalfVoiceSelfEventHandler ChannelDeHalfVoiceSelf;
        public event IRCClient.ChannelDeOpEventHandler ChannelDeOp;
        public event IRCClient.ChannelDeOpSelfEventHandler ChannelDeOpSelf;
        public event IRCClient.ChannelDeOwnerEventHandler ChannelDeOwner;
        public event IRCClient.ChannelDeOwnerSelfEventHandler ChannelDeOwnerSelf;
        public event IRCClient.ChannelDeVoiceEventHandler ChannelDeVoice;
        public event IRCClient.ChannelDeVoiceSelfEventHandler ChannelDeVoiceSelf;
        public event IRCClient.ChannelExemptEventHandler ChannelExempt;
        public event IRCClient.ChannelExemptSelfEventHandler ChannelExemptSelf;
        public event IRCClient.ChannelHalfOpEventHandler ChannelHalfOp;
        public event IRCClient.ChannelHalfOpSelfEventHandler ChannelHalfOpSelf;
        public event IRCClient.ChannelHalfVoiceEventHandler ChannelHalfVoice;
        public event IRCClient.ChannelHalfVoiceSelfEventHandler ChannelHalfVoiceSelf;
        public event IRCClient.ChannelInviteExemptEventHandler ChannelInviteExempt;
        public event IRCClient.ChannelInviteExemptSelfEventHandler ChannelInviteExemptSelf;
        public event IRCClient.ChannelJoinEventHandler ChannelJoin;
        public event IRCClient.ChannelJoinSelfEventHandler ChannelJoinSelf;
        public event IRCClient.ChannelJoinDeniedBannedEventHandler ChannelJoinDeniedBanned;
        public event IRCClient.ChannelJoinDeniedFullEventHandler ChannelJoinDeniedFull;
        public event IRCClient.ChannelJoinDeniedInviteEventHandler ChannelJoinDeniedInvite;
        public event IRCClient.ChannelJoinDeniedKeyEventHandler ChannelJoinDeniedKey;
        public event IRCClient.ChannelKickEventHandler ChannelKick;
        public event IRCClient.ChannelKickSelfEventHandler ChannelKickSelf;
        public event IRCClient.ChannelListEventHandler ChannelList;
        public event IRCClient.ChannelMessageEventHandler ChannelMessage;
        public event IRCClient.ChannelMessageSendDeniedEventHandler ChannelMessageSendDenied;
        public event IRCClient.ChannelMessageHighlightEventHandler ChannelMessageHighlight;
        public event IRCClient.ChannelModeEventHandler ChannelMode;
        public event IRCClient.ChannelModeSelfEventHandler ChannelModeSelf;
        public event IRCClient.ChannelModeUnhandledEventHandler ChannelModeUnhandled;
        public event IRCClient.ChannelModesSetEventHandler ChannelModesSet;
        public event IRCClient.ChannelModesGetEventHandler ChannelModesGet;
        public event IRCClient.ChannelNoticeEventHandler ChannelNotice;
        public event IRCClient.ChannelOpEventHandler ChannelOp;
        public event IRCClient.ChannelOpSelfEventHandler ChannelOpSelf;
        public event IRCClient.ChannelOwnerEventHandler ChannelOwner;
        public event IRCClient.ChannelOwnerSelfEventHandler ChannelOwnerSelf;
        public event IRCClient.ChannelPartEventHandler ChannelPart;
        public event IRCClient.ChannelPartSelfEventHandler ChannelPartSelf;
        public event IRCClient.ChannelQuietEventHandler ChannelQuiet;
        public event IRCClient.ChannelQuietSelfEventHandler ChannelQuietSelf;
        public event IRCClient.ChannelRemoveExemptEventHandler ChannelRemoveExempt;
        public event IRCClient.ChannelRemoveExemptSelfEventHandler ChannelRemoveExemptSelf;
        public event IRCClient.ChannelRemoveInviteExemptEventHandler ChannelRemoveInviteExempt;
        public event IRCClient.ChannelRemoveInviteExemptSelfEventHandler ChannelRemoveInviteExemptSelf;
        public event IRCClient.ChannelRemoveKeyEventHandler ChannelRemoveKey;
        public event IRCClient.ChannelRemoveLimitEventHandler ChannelRemoveLimit;
        public event IRCClient.ChannelSetKeyEventHandler ChannelSetKey;
        public event IRCClient.ChannelSetLimitEventHandler ChannelSetLimit;
        public event IRCClient.ChannelTopicEventHandler ChannelTopic;
        public event IRCClient.ChannelTopicChangeEventHandler ChannelTopicChange;
        public event IRCClient.ChannelTopicStampEventHandler ChannelTopicStamp;
        public event IRCClient.ChannelUsersEventHandler ChannelUsers;
        public event IRCClient.ChannelUnBanEventHandler ChannelUnBan;
        public event IRCClient.ChannelUnBanSelfEventHandler ChannelUnBanSelf;
        public event IRCClient.ChannelUnQuietEventHandler ChannelUnQuiet;
        public event IRCClient.ChannelUnQuietSelfEventHandler ChannelUnQuietSelf;
        public event IRCClient.ChannelVoiceEventHandler ChannelVoice;
        public event IRCClient.ChannelVoiceSelfEventHandler ChannelVoiceSelf;
        public event IRCClient.ConnectedEventHandler Connected;
        public event IRCClient.ConnectingEventHandler Connecting;
        public event IRCClient.ConnectingFailedEventHandler ConnectingFailed;
        public event IRCClient.PrivateCTCPEventHandler PrivateCTCP;
        public event IRCClient.SendCTCPEventHandler SendCTCP;
        public event IRCClient.SendDCCChatOfferEventHandler SendDCCChatOffer;
        public event IRCClient.SendDCCOfferEventHandler SendDCCOffer;
        public event IRCClient.SendMessageEventHandler SendMessage;
        public event IRCClient.DCCChatOfferEventHandler DCCChatOffer;
        public event IRCClient.DCCChatAlreadyOfferingEventHandler DCCChatAlreadyOffering;
        public event IRCClient.DCCOfferEventHandler DCCOffer;
        public event IRCClient.DCCBadOfferEventHandler DCCBadOffer;
        public event IRCClient.DCCFileResumeRequestEventHandler DCCFileResumeRequest;
        public event IRCClient.DCCFileSendOfferEventHandler DCCFileSendOffer;
        public event IRCClient.DisconnectedEventHandler Disconnected;
        public event IRCClient.WaitingToReconnectEventHandler WaitingToReconnect;
        public event IRCClient.ExceptionEventHandler Exception;
        public event IRCClient.ExemptListEventHandler ExemptList;
        public event IRCClient.ExemptListEndEventHandler ExemptListEnd;
        public event IRCClient.InviteEventHandler Invite;
        public event IRCClient.InviteSentEventHandler InviteSent;
        public event IRCClient.InviteListEventHandler InviteList;
        public event IRCClient.InviteListEndEventHandler InviteListEnd;
        public event IRCClient.InviteExemptListEventHandler InviteExemptList;
        public event IRCClient.InviteExemptListEndEventHandler InviteExemptListEnd;
        public event IRCClient.KilledEventHandler Killed;
        public event IRCClient.LookingUpHostEventHandler LookingUpHost;
        public event IRCClient.LookingUpHostFailedEventHandler LookingUpHostFailed;
        public event IRCClient.MOTDEventHandler MOTD;
        public event IRCClient.MOTDSkippedEventHandler MOTDSkipped;
        public event IRCClient.NamesEventHandler Names;
        public event IRCClient.NamesEndEventHandler NamesEnd;
        public event IRCClient.NicknameInvalidEventHandler NicknameInvalid;
        public event IRCClient.NicknameTakenEventHandler NicknameTaken;
        public event IRCClient.NicknameChangeFailedEventHandler NicknameChangeFailed;
        public event IRCClient.PrivateNoticeEventHandler PrivateNotice;
        public event IRCClient.SendNoticeEventHandler SendNotice;
        public event IRCClient.PingEventHandler Ping;
        public event IRCClient.PingReplyEventHandler PingReply;
        public event IRCClient.PrivateMessageEventHandler PrivateMessage;
        public event IRCClient.PrivateActionEventHandler PrivateAction;
        public event IRCClient.QuitEventHandler Quit;
        public event IRCClient.QuitSelfEventHandler QuitSelf;
        public event IRCClient.RawLineReceivedEventHandler RawLineReceived;
        public event IRCClient.RawLineReceivedUnhandledEventHandler RawLineReceivedUnhandled;
        public event IRCClient.RawLineSentEventHandler RawLineSent;
        public event IRCClient.UserModesSetEventHandler UserModesSet;
        public event IRCClient.WallopsEventHandler Wallops;
        public event IRCClient.ServerNoticeEventHandler ServerNotice;
        public event IRCClient.ServerErrorEventHandler ServerError;
        public event IRCClient.ServerMessageEventHandler ServerMessage;
        public event IRCClient.ServerMessageUnhandledEventHandler ServerMessageUnhandled;
        public event IRCClient.TimeOutEventHandler TimeOut;
        public event IRCClient.WhoListEventHandler WhoList;
        public event IRCClient.WhoIsAuthenticationLineEventHandler WhoIsAuthenticationLine;
        public event IRCClient.WhoIsAwayLineEventHandler WhoIsAwayLine;
        public event IRCClient.WhoIsChannelLineEventHandler WhoIsChannelLine;
        public event IRCClient.WhoIsEndEventHandler WhoIsEnd;
        public event IRCClient.WhoIsIdentifiedEventHandler WhoIsIdentified;
        public event IRCClient.WhoIsIdleLineEventHandler WhoIsIdleLine;
        public event IRCClient.WhoIsNameLineEventHandler WhoIsNameLine;
        public event IRCClient.WhoIsOperLineEventHandler WhoIsOperLine;
        public event IRCClient.WhoIsHelperLineEventHandler WhoIsHelperLine;
        public event IRCClient.WhoIsRealHostLineEventHandler WhoIsRealHostLine;
        public event IRCClient.WhoIsServerLineEventHandler WhoIsServerLine;
        public event IRCClient.WhoIsSpecialLineEventHandler WhoIsSpecialLine;
        public event IRCClient.WhoWasNameLineEventHandler WhoWasNameLine;
        public event IRCClient.WhoWasEndEventHandler WhoWasEnd;
        #endregion

        // Server information
        public IPAddress IP;
        public int Port;
        public string Address { get; set; }
        public string ServerName { get; protected set; }

        public char[] SupportedUserModes;
        public char[] SupportedChannelModes;

        public UserCollection Users { get; private set; }

        // 005 information
        public string CaseMapping { get; private set; }
        public Dictionary<char, int> ChannelLimit { get; private set; }
        public IRC.ChannelModes ChanModes { get; private set; }
        public int ChannelLength { get; private set; }
        public char[] ChannelTypes { get; private set; }
        public bool SupportsBanExceptions { get; private set; }
        public char BanExceptionsMode { get; private set; }
        public bool SupportsInviteExceptions { get; private set; }
        public char InviteExceptionsMode { get; private set; }
        public bool SupportsWatch { get; private set; }
        public int KickMessageLength { get; private set; }
        public Dictionary<char, int> ListModeLength { get; private set; }
        public int Modes { get; private set; }
        public string NetworkName { get; private set; }
        public int NicknameLength { get; private set; }
        public Dictionary<char, char> StatusPrefix { get; private set; }
        public char[] StatusMessage { get; private set; }
        public Dictionary<string, int> MaxTargets { get; private set; }
        public int TopicLength { get; private set; }

        public StringComparer CaseMappingComparer { get; private set; }

        public bool Away;
        public string AwayReason;
        public DateTime AwaySince;
        public DateTime LastSpoke;
        public string Nickname;
        public string[] Nicknames;
        public string Username;
        public string FullName;
        public string UserModes;
        public readonly IRC.ChannelCollection Channels;
        public int ReconnectInterval;
        public int ReconnectMaxAttempts;
        private int ReconnectAttempts;
        private System.Timers.Timer ReconnectTimer;
        public bool IsRegistered;
        public bool IsConnected;
        public bool VoluntarilyQuit;

        private TcpClient mobjClient;
        private bool _IsUsingSSL;
        public bool AllowInvalidCertificate;
        private SslStream SSLStream;
        private byte[] marData;
        private StringBuilder mobjText;
        private BackgroundWorker bgwRead;
        private int _PingTimeout;
        private bool Pinged;
        private System.Timers.Timer PingTimer;


        public bool IsUsingSSL {
            get { return this._IsUsingSSL; }
            set {
                if (this.IsConnected)
                    throw new InvalidOperationException("This property cannot be set while the client is connected.");
                this._IsUsingSSL = value;
            }
        }

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
                    if (this.TimeOut != null) TimeOut(this);
                    this.Send("QUIT :Ping timeout; reconnecting.");
                    this.PingTimer.Stop();
                    this.Disconnect();
                } else {
                    this.Send("PING :Keep-alive");
                    this.Pinged = true;
                }
            }
        }

        public virtual void Connect() {
            if (!IPAddress.TryParse(this.Address, out this.IP)) {
                try {
                    if (this.LookingUpHost != null) this.LookingUpHost(this, this.Address);
                    this.IP = Dns.GetHostEntry(this.Address).AddressList[0];
                } catch (SocketException e) {
                    if (this.LookingUpHostFailed != null) this.LookingUpHostFailed(this, this.Address, e.Message);
                    return;
                }
            }
            if (this.Connecting != null) this.Connecting(this, this.Address, new IPEndPoint(this.IP, this.Port));
            try {
                this.mobjClient = new TcpClient(this.IP.ToString(), this.Port);
            } catch (Exception e) {
                if (this.ConnectingFailed != null) this.ConnectingFailed(this, e);
                this.ReconnectTimer = new System.Timers.Timer((double) this.ReconnectInterval) { AutoReset = false, Enabled = true };
                return;
            }
            if (this.IsUsingSSL) {
                this.SSLStream = new SslStream(this.mobjClient.GetStream(), false, new RemoteCertificateValidationCallback(this.ValidateServerCertificate), null);
                try {
                    this.SSLStream.AuthenticateAsClient(this.Address);
                } catch (AuthenticationException) {
                    /*
                    OutputLine("\cREDAuthentication failed: " & e.Message)
                    If e.InnerException IsNot Nothing Then
                        Console.WriteLine("Inner exception: {0}", e.InnerException.Message)
                    End If
                     */
                    this.mobjClient.Close();
                    return;
                }
            }
            this.marData = new byte[512];
            this.bgwRead = new BackgroundWorker();
            this.bgwRead.DoWork += this.BeginRead;
            this.bgwRead.WorkerReportsProgress = true;
            this.bgwRead.RunWorkerAsync();
            this.ReconnectAttempts = 0;
            this.LastSpoke = DateTime.Now;
            this.VoluntarilyQuit = false;
            this.IsConnected = true;
            if (this._PingTimeout != 0) this.PingTimer.Start();
            this.Pinged = false;
            if (this.Connected != null) this.Connected(this);
            this.Send("NICK {0}", this.Nickname);
            this.Send("USER {0} 0 {2} :{1}", this.Username, this.FullName, this.Address);
        }

        private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
            if (sslPolicyErrors == SslPolicyErrors.None) return true;
            Console.WriteLine("Failed to validate the server's certificate: {0}", sslPolicyErrors);
            return this.AllowInvalidCertificate;
        }

        public virtual void Disconnect() {
            if (this.IsUsingSSL) this.SSLStream.Close();
            this.mobjClient.Close();
            this.PingTimer.Stop();
        }

        private void BeginRead(object sender, DoWorkEventArgs e) {
                while (true) {
                    int intCount;
                    try {
                        if (this._PingTimeout != 0) this.PingTimer.Start();
                        if (this.IsUsingSSL)
                            intCount = this.SSLStream.Read(this.marData, 0, 512);
                        else
                            intCount = this.mobjClient.GetStream().Read(this.marData, 0, 512);
                    } catch (IOException ex) {
                        if (this.Disconnected != null) this.Disconnected(this, ex.Message);
                        e.Result = ex.Message;
                        this.PingTimer.Stop();
                        return;
                    } catch (SocketException ex) {
                        if (this.Disconnected != null) this.Disconnected(this, ex.Message);
                        e.Result = ex.Message;
                        this.PingTimer.Stop();
                        return;
                    }
                    if (((BackgroundWorker) sender).CancellationPending) break;
                    if (intCount < 1) {
                        if (this.Disconnected != null) this.Disconnected(this, "The server closed the connection.");
                        e.Result = "The server closed the connection.";
                        this.PingTimer.Stop();
                        return;
                    }
                    try {
                        for (int intIndex = 0; intIndex < intCount; ++intIndex) {
                            if (this.marData[intIndex] == 10 || this.marData[intIndex] == 13) {
                                if (this.mobjText.Length > 0) {
                                    System.Timers.Timer pingTimer = this.PingTimer;
                                    lock (this.PingTimer) {
                                        this.Pinged = false;
                                        if (this._PingTimeout != 0) this.PingTimer.Stop();
                                        object[] array = new object[] { this.mobjText.ToString() };
                                        try {
                                            this.ReceivedLine(this.mobjText.ToString());
                                        } catch (Exception ex) {
                                            if (this.Exception != null) this.Exception(this, ex);
                                        }
                                    }
                                }
                                this.mobjText = new StringBuilder();
                            } else {
                                this.mobjText.Append((char) this.marData[intIndex]);
                            }
                        }
                    } catch (Exception ex) {
                        if (this.Exception != null) this.Exception(this, ex);
                        this.mobjText = new StringBuilder();
                    }
                }
                e.Result = "";
                e.Cancel = true;
        }

        private void NamesDirty(IRC.Channel sender) {
            this.Send("NAMES :{0}", sender.Name);
        }

        public static void ParseIRCLine(string Data, out string Prefix, out string Command, out string[] Parameters, out string Trail, bool IncludeTrail = true) {
            int p = 0; int ps = 0;

            Trail = null;
            if (Data.Length == 0) {
                Prefix = null;
                Command = null;
                Parameters = null;
            } else {
                if (Data[0] == ':') {
                    p = Data.IndexOf(' ');
                    if (p < 0) {
                        Prefix = Data.Substring(1);
                        Command = null;
                        Parameters = null;
                        return;
                    }
                    Prefix = Data.Substring(1, p - 1);
                    ps = p + 1;
                } else {
                    Prefix = null;
                }

                List<string> tParameters = new List<string>();
                while (ps < Data.Length) {
                    if (Data[ps] == ':') {
                        Trail = Data.Substring(ps + 1);
                        if (IncludeTrail) {
                            tParameters.Add(Trail);
                        }
                        break;
                    }
                    p = Data.IndexOf(' ', ps);
                    if (p < 0) {
                        // Final parameter
                        tParameters.Add(Data.Substring(ps));
                        break;
                    }
                    string tP = Data.Substring(ps, p - ps);
                    if (tP.Length > 0) tParameters.Add(tP);
                    ps = p + 1;
                }
                Command = tParameters[0];
                tParameters.RemoveAt(0);
                Parameters = tParameters.ToArray();
            }
        }

        public void ReceivedLine(string Data) {
            lock (this) {
                if (this.RawLineReceived != null) this.RawLineReceived(this, Data);

                string Prefix;
                string Command;
                string[] Parameters;
                string Trail = null;
                IRCClient.ParseIRCLine(Data, out Prefix, out Command, out Parameters, out Trail, true);

                DateTime time;
                User user;

                switch (Command.ToUpper()) {
                    case "001":
                        this.ServerName = Prefix;
                        if (this.Nickname != Parameters[0]) {
                            if (this.NicknameChangeSelf != null) this.NicknameChangeSelf(this, new User(this, this.Nickname, "*", "*"), Parameters[0]);
                            this.Nickname = Parameters[0];
                        }
                        this.Users.Add(new User(this, this.Nickname, "*", "*"));
                        this.IsRegistered = true;
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, Parameters[1]);
                        break;
                    case "005":
                        for (int i = 1; i < (Trail == null ? Parameters.Length : Parameters.Length - 1); ++i) {
                            string[] fields; string key; string value;
                            fields = Parameters[i].Split(new char[] { '=' }, 2);
                            if (fields.Length == 2) {
                                key = fields[0];
                                value = fields[1];
                            } else {
                                key = fields[0];
                                value = null;
                            }

                            switch (key) {  // Parameter names are case sensitive.
                                case "CASEMAPPING":
                                    this.CaseMapping = value;
                                    switch (value.ToUpper()) {
                                        case "ASCII":
                                            this.CaseMappingComparer = IRCStringComparer.ASCIICaseInsensitiveComparer;
                                            break;
                                        case "STRICT-RFC1459":
                                            this.CaseMappingComparer = IRCStringComparer.StrictRFC1459CaseInsensitiveComparer;
                                            break;
                                        default:
                                            this.CaseMappingComparer = IRCStringComparer.RFC1459CaseInsensitiveComparer;
                                            break;
                                    }
                                    break;
                                case "CHANLIMIT":
                                    this.ChannelLimit = new Dictionary<char, int>();
                                    foreach (string field in value.Split(new char[] { ',' })) {
                                        fields = field.Split(new char[] { ':' });
                                        this.ChannelLimit.Add(fields[0][0], int.Parse(fields[1]));
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
                                    this.BanExceptionsMode = value == null ? 'e' : value[0];
                                    break;
                                case "INVEX":
                                    this.SupportsInviteExceptions = true;
                                    this.InviteExceptionsMode = value == null ? 'I' : value[0];
                                    break;
                                case "KICKLEN": this.KickMessageLength = int.Parse(value); break;
                                case "MAXLIST":
                                    foreach (string field in value.Split(new char[] { ',' })) {
                                        fields = field.Split(new char[] { ':' }, 2);
                                        foreach (char mode in fields[0])
                                            this.ListModeLength.Add(mode, int.Parse(fields[1]));
                                    }
                                    break;
                                case "MODES": this.Modes = int.Parse(value); break;
                                case "NETWORK": this.NetworkName = value; break;
                                case "NICKLEN": this.NicknameLength = int.Parse(value); break;
                                case "PREFIX":
                                    this.StatusPrefix = new Dictionary<char, char>();
                                    if (value != null) {
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
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;

                    case "221":  // User modes
                        if (Parameters[0] == this.Nickname) this.UserModes = Parameters[1];
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "301":  // WHOIS away line
                        if (this.WhoIsAwayLine != null) this.WhoIsAwayLine(this, Parameters[1], Trail);
                        break;
                    case "303":  // ISON reply
                        // TODO: This can be trapped as part of a notify feature.
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "305":  // AWAY cancellation
                        this.Away = false;
                        if (this.AwayCancelled != null) this.AwayCancelled(this, Trail);
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "306":  // AWAY set
                        this.Away = true;
                        if (this.AwaySince == null) this.AwaySince = DateTime.Now;
                        if (this.AwaySet != null) this.AwaySet(this, Trail);
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "310":  // WHOIS helper line
                        if (this.WhoIsHelperLine != null) this.WhoIsHelperLine(this, Parameters[1], Trail);
                        break;
                    case "311":  // WHOIS name line
                        if (this.Users.Contains(Parameters[1])) {
                            User _user = this.Users[Parameters[1]];
                            _user.Username = Parameters[2];
                            _user.Host = Parameters[3];
                            _user.FullName = Parameters[5];

                            MatchCollection matches = Regex.Matches(_user.FullName, @"\G\x03(\d\d?)(?:,(\d\d?))?\x0F");
                            foreach (Match match in matches) {
                                if (!match.Groups[2].Success) _user.Gender = (Gender) (int.Parse(match.Groups[1].Value) & 3);
                            }
                        }
                        if (this.WhoIsNameLine != null) this.WhoIsNameLine(this, Parameters[1], Parameters[2], Parameters[3], Parameters[5]);
                        break;
                    case "312":  // WHOIS server line
                        if (this.WhoIsServerLine != null) this.WhoIsServerLine(this, Parameters[1], Parameters[2], Parameters[3]);
                        break;
                    case "313":  // WHOIS oper line
                        if (this.WhoIsHelperLine != null) this.WhoIsHelperLine(this, Parameters[1], Trail);
                        break;
                    case "314":  // WHOWAS list
                        if (this.WhoWasNameLine != null) this.WhoWasNameLine(this, Parameters[1], Parameters[2], Parameters[3], Parameters[5]);
                        break;
                    case "315":  // End of WHO list
                        // TODO: respond to 315 similarly to 366.
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "317":  // WHOIS idle line
                        if (this.WhoIsIdleLine != null) this.WhoIsIdleLine(this, Parameters[1], TimeSpan.FromSeconds(double.Parse(Parameters[2])), new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[3])), Trail);
                        break;
                    case "318":  // End of WHOIS list
                        if (this.WhoIsEnd != null) this.WhoIsEnd(this, Parameters[1], Trail);
                        break;
                    case "319":  // WHOIS channels line
                        if (this.WhoIsChannelLine != null) this.WhoIsChannelLine(this, Parameters[1], Parameters[2]);
                        break;
                    case "321":  // LIST header
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "322":  // Channel list
                        if (this.ChannelList != null) this.ChannelList(this, Parameters[1], int.Parse(Parameters[2]), Parameters[3]);
                        break;
                    case "323":  // End of channel list
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "324":  // Channel modes
                        string channel = Parameters[1]; string modes = Parameters[2];
                        if (Channels.Contains(channel)) Channels[channel].Modes = modes;
                        if (this.ChannelModesGet != null) this.ChannelModesGet(this, channel, modes);
                        break;
                    case "329":  // Channel timestamp
                        time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[2]));
                        if (Channels.Contains(Parameters[1])) Channels[Parameters[1]].Timestamp = time;
                        if (this.ChannelTimestamp != null) this.ChannelTimestamp(this, Parameters[1], time);
                        break;
                    case "332":  // Channel topic
                        if (Channels.Contains(Parameters[1])) Channels[Parameters[1]].Topic = Parameters[2];
                        break;
                    case "333":  // Channel topic stamp
                        time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[3]));
                        if (Channels.Contains(Parameters[1])) {
                            Channels[Parameters[1]].TopicSetter = Parameters[2];
                            Channels[Parameters[1]].TopicStamp = time;
                        }
                        if (this.ChannelTopicStamp != null) this.ChannelTopicStamp(this, Parameters[1], Parameters[2], time);
                        break;
                    case "341":  // Invite sent
                        if (this.InviteSent != null) this.InviteSent(this, Parameters[1], Parameters[2]);
                        break;
                    case "346":  // Invite list
                        time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[4]));
                        if (this.InviteList != null) this.InviteList(this, Parameters[1], Parameters[2], Parameters[3], time);
                        break;
                    case "347":  // End of invite list
                        if (this.InviteListEnd != null) this.InviteListEnd(this, Parameters[1], Trail);
                        break;
                    case "348":  // Exempt list
                        time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[4]));
                        if (this.ExemptList != null) this.ExemptList(this, Parameters[1], Parameters[2], Parameters[3], time);
                        break;
                    case "349":  // End of exempt list
                        if (this.ExemptListEnd != null) this.ExemptListEnd(this, Parameters[1], Trail);
                        break;
                    case "352":  // WHO list
                        // TODO: populate the user list
                        {
                            string[] fields = Parameters[7].Split(new char[] { ' ' }, 2);
                            this.OnWhoList(Parameters[1], Parameters[2], Parameters[3], Parameters[4], Parameters[5], Parameters[6], int.Parse(fields[0]), fields[1]);
                            break;
                        }
                    case "353":  // NAMES list
                        {
                            string[] names = Parameters[3].Split(new char[] { ' ' });
                            Channel _channel;

                            if (Channels.TryGetValue(Parameters[2], out _channel)) {
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

                            if (this.Names != null) this.Names(this, Parameters[2], Parameters[3]);
                            break;
                        }
                    case "366":  // End of NAMES list
                        if (this.Channels.Contains(Parameters[2])) {
                            if (this.Channels[Parameters[2]].WaitingForNamesList % 2 == 1) {
                                for (int i = this.Channels[Parameters[2]].Users.Count - 1; i >= 0; --i) {
                                    ChannelUser _user = this.Channels[Parameters[2]].Users[i];
                                    if (_user.Access < 0) this.Channels[Parameters[2]].Users.Remove(_user.Nickname);
                                }
                                this.Channels[Parameters[2]].WaitingForNamesList -= 3;
                            }
                        }

                        if (this.NamesEnd != null) this.NamesEnd(this, Parameters[2], Trail);
                        break;
                    case "367":  // Ban list
                        time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(double.Parse(Parameters[4]));
                        if (this.BanList != null) this.BanList(this, Parameters[1], Parameters[2], Parameters[3], time);
                        break;
                    case "368":  // End of ban list
                        if (this.BanListEnd != null) this.BanListEnd(this, Parameters[1], Trail);
                        break;
                    case "369":  // End of WHOWAS list
                        if (this.WhoWasEnd != null) this.WhoWasEnd(this, Parameters[1], Trail);
                        break;
                    case "404":  // Cannot send to channel  (Any similarity with HTTP 404 is (probably) purely coincidential. (^_^))
                        if (this.ChannelMessageSendDenied != null) this.ChannelMessageSendDenied(this, Parameters[1], Trail);
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "432":  // Erroneous nickname
                        if (this.NicknameInvalid != null) this.NicknameInvalid(this, Parameters[1], Parameters[2]);
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                    case "433":  // Nickname already in use
                        if (this.NicknameTaken != null) this.NicknameTaken(this, Parameters[1], Parameters[2]);
                        if (!this.IsRegistered && this.Nicknames.Length > 1) {
                            for (int i = 0; i < this.Nicknames.Length - 1; ++i) {
                                if (this.Nicknames[i] == Parameters[1]) {
                                    this.Nickname = this.Nicknames[i + 1];
                                    this.Send("NICK {0}", this.Nickname);
                                    break;
                                }
                            }
                        }
                        break;
                    case "436":  // Nickname collision KILL
                        if (this.Killed != null) this.Killed(this, Prefix, Parameters[2]);
                        break;
                    case "598":  // Watched user went away
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(Parameters[0])) {
                                this.Users[Parameters[0]].Away = true;
                                this.Users[Parameters[0]].AwayReason = Parameters[4];
                                this.Users[Parameters[0]].AwaySince = DateTime.Now;
                            }
                        }
                        break;
                    case "599":  // Watched user came back
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(Parameters[0])) {
                                this.Users[Parameters[0]].Away = false;
                            }
                        }
                        break;
                    case "602":  // Stopped watching
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(Parameters[1])) {
                                this.Users[Parameters[1]].Watched = false;
                                if (this.Users[Parameters[1]].Channels.Count == 0)
                                    this.Users.Remove(Parameters[1]);
                            }
                        }
                        break;
                    case "604":  // Watched user is online
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(Parameters[1]))
                                this.Users[Parameters[1]].Watched = true;
                            else
                                this.Users.Add(new User(this, Parameters[1], Parameters[2], Parameters[3]) { Watched = true });
                        }
                        break;
                    case "601":  // Watched user went offline
                    case "605":  // Watched user is offline
                        if (this.SupportsWatch)
                            this.Users.Remove(Parameters[1]);
                        break;
                    case "609":  // Watched user is away
                        if (this.SupportsWatch) {
                            if (this.Users.Contains(Parameters[1])) {
                                this.Users[Parameters[1]].Away = true;
                                this.Users[Parameters[1]].AwayReason = null;
                                this.Users[Parameters[1]].AwaySince = DateTime.Now;
                            }
                        }
                        break;
                    case "ERROR":
                        if (this.ServerError != null) this.ServerError(this, Trail);
                        break;
                    case "INVITE":
                        if (this.Invite != null) this.Invite(this, new User(Prefix), Parameters[1]);
                        break;
                    case "JOIN":
                        user = new User(Prefix);
                        if (this.Users.Contains(user.Nickname)) {
                            User user2 = this.Users[user.Nickname];
                            user2.Username = user.Username;
                            user2.Host = user.Host;
                            user = user2;
                        } else {
                            this.Users.Add(user);
                        }

                        if (user.Nickname == this.Nickname) {
                            if (this.ChannelJoinSelf != null) this.ChannelJoinSelf(this, user, Parameters[0]);

                            Channel newChannel = new Channel(Parameters[0], this) {
                                OwnStatus = 0,
                                Users = new ChannelUserCollection()
                            };
                            newChannel.Users.Add(new ChannelUser(user.Nickname, this));
                            this.Channels.Add(newChannel);
                        } else {
                            this.Channels[Parameters[0]].Users.Add(new ChannelUser(user.Nickname, this));
                            if (this.ChannelJoin != null) this.ChannelJoin(this, user, Parameters[0]);
                        }

                        user.Channels.Add(this.Channels[Parameters[0]]);
                        break;
                    case "KICK":
                        user = new User(Prefix);
                        if (Parameters[1].Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.Channels.Remove(Parameters[0]);
                            if (this.ChannelKickSelf != null) this.ChannelKickSelf(this, user, Parameters[0], Parameters[1], Parameters.Length >= 3 ? Parameters[2] : null);
                        } else {
                            if (this.Channels[Parameters[0]].Users.Contains(Parameters[1]))
                                this.Channels[Parameters[0]].Users.Remove(Parameters[1]);
                            if (this.ChannelKick != null) this.ChannelKick(this, user, Parameters[0], Parameters[1], Parameters.Length >= 3 ? Parameters[2] : null);
                        }
                        break;
                    case "KILL":
                        user = new User(Prefix);
                        if (Parameters[0].Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.Killed != null) this.Killed(this, user, Parameters[1]);
                        }
                        break;
                    case "MODE":
                        if (Parameters[0].StartsWith("#")) {
                            int index = 2; bool direction = true;
                            foreach (char c in Parameters[1]) {
                                if (c == '+')
                                    direction = true;
                                else if (c == '-')
                                    direction = false;
                                else if (this.ChanModes.TypeAModes.Contains(c))
                                    this.OnChannelMode(Prefix, Parameters[0], direction, c, Parameters[index++]);
                                else if (this.ChanModes.TypeBModes.Contains(c))
                                    this.OnChannelMode(Prefix, Parameters[0], direction, c, Parameters[index++]);
                                else if (this.ChanModes.TypeCModes.Contains(c)) {
                                    if (direction)
                                        this.OnChannelMode(Prefix, Parameters[0], direction, c, Parameters[index++]);
                                    else
                                        this.OnChannelMode(Prefix, Parameters[0], direction, c, null);
                                } else if (this.ChanModes.TypeDModes.Contains(c))
                                    this.OnChannelMode(Prefix, Parameters[0], direction, c, null);
                                else if (this.StatusPrefix.ContainsKey(c))
                                    this.OnChannelMode(Prefix, Parameters[0], direction, c, Parameters[index++]);
                            }
                        }
                        break;
                    case "NICK":
                        user = new User(Prefix);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.Nickname = Parameters[0];
                            if (this.NicknameChangeSelf != null) this.NicknameChangeSelf(this, user, this.Nickname);
                        } else {
                            if (this.NicknameChange != null) this.NicknameChange(this, user, this.Nickname);
                        }

                        foreach (Channel _channel in this.Channels) {
                            if (_channel.Users.Contains(user.Nickname)) {
                                _channel.Users.Remove(user.Nickname);
                                // TODO: Fix this
                                _channel.Users.Add(new ChannelUser(Parameters[0], this));
                            }
                        }
                        break;
                    case "NOTICE":
                        if (Parameters[0].StartsWith("#")) {
                            if (this.ChannelNotice != null) this.ChannelNotice(this, new User(Prefix ?? this.Address), Parameters[0], Parameters[1]);
                        } else if (Prefix == null || Prefix.Contains(".")) {
                            // TODO: fix this
                            if (this.ServerNotice != null) this.ServerNotice(this, Prefix ?? this.Address, Parameters[1]);
                        } else {
                            if (this.PrivateNotice != null) this.PrivateNotice(this, new User(Prefix ?? this.Address), Parameters[1]);
                        }
                        break;
                    case "PART":
                        user = new User(Prefix);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            this.Channels.Remove(Parameters[0]);
                            if (this.ChannelPartSelf != null) this.ChannelPartSelf(this, user, Parameters[0], Parameters.Length == 1 ? null : Parameters[1]);
                        } else {
                            if (this.Channels[Parameters[0]].Users.Contains(user.Nickname))
                                this.Channels[Parameters[0]].Users.Remove(user.Nickname);
                            if (this.ChannelPart != null) this.ChannelPart(this, user, Parameters[0], Parameters.Length == 1 ? null : Parameters[1]);
                        }
                        break;
                    case "PING":
                        if (this.Ping != null) this.Ping(this, Parameters.ElementAtOrDefault(0));
                        this.Send(Parameters.Length == 0 ? "PONG" : "PONG :" + Parameters[0]);
                        break;
                    case "PONG":
                        if (this.PingReply != null) this.PingReply(this, Prefix);
                        break;
                    case "PRIVMSG":
                        user = new User(Prefix);
                        if (this.Users.Contains(user.Nickname)) {
                            User user2 = this.Users[user.Nickname];
                            user2.Username = user.Username;
                            user2.Host = user.Host;
                            user = user2;
                        } else {
                            this.Users.Add(user);
                        }

                        if (Parameters[0].StartsWith("#")) {
                            // It's a channel message.
                            if (Parameters[1].Length > 1 && Parameters[1].StartsWith("\u0001") && Parameters[1].EndsWith("\u0001")) {
                                string CTCPMessage = Parameters[1].Trim(new char[] { '\u0001' });
                                string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                                if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                                    if (this.ChannelAction != null) this.ChannelAction(this, user, Parameters[0], fields.ElementAtOrDefault(1) ?? "");
                                } else {
                                    if (this.ChannelCTCP != null) this.ChannelCTCP(this, user, Parameters[0], CTCPMessage);
                                }
                            } else {
                                if (this.ChannelMessage != null) this.ChannelMessage(this, user, Parameters[0], Parameters[1]);
                            }
                        } else {
                            // It's a private message.
                            if (Parameters[1].Length > 1 && Parameters[1].StartsWith("\u0001") && Parameters[1].EndsWith("\u0001")) {
                                string CTCPMessage = Parameters[1].Trim(new char[] { '\u0001' });
                                string[] fields = CTCPMessage.Split(new char[] { ' ' }, 2);
                                if (fields[0].Equals("ACTION", StringComparison.OrdinalIgnoreCase)) {
                                    if (this.PrivateAction != null) this.PrivateAction(this, user, fields.ElementAtOrDefault(1) ?? "");
                                } else {
                                    if (this.PrivateCTCP != null) this.PrivateCTCP(this, user, CTCPMessage);
                                }
                            } else {
                                if (this.PrivateMessage != null) this.PrivateMessage(this, user, Parameters[1]);
                            }
                        }
                        break;
                    case "QUIT":
                        user = new User(Prefix);
                        if (user.Nickname.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.QuitSelf != null) this.QuitSelf(this, user, Parameters[0]);
                            this.Channels.Clear();
                        } else {
                            if (this.Quit != null) this.Quit(this, user, Parameters[0]);
                            foreach (Channel _channel in this.Channels) {
                                if (_channel.Users.Contains(user.Nickname))
                                    _channel.Users.Remove(user.Nickname);
                            }
                        }
                        break;
                    case "TOPIC":
                        user = new User(Prefix);
                        if (this.ChannelTopicChange != null) this.ChannelTopicChange(this, user, Parameters[0], Parameters[1]);
                        break;
                    default:
                        if (this.ServerMessage != null) this.ServerMessage(this, Prefix, Command, Parameters, String.Join(" ", Parameters));
                        break;
                }
            }
        }

        public virtual void Send(string t) {
            if (!mobjClient.Connected) throw new InvalidOperationException("The client is not connected.");
            if (this.RawLineSent != null) this.RawLineSent(this, t);

            StreamWriter w;
            if (SSLStream != null)
                w = new StreamWriter(SSLStream);
            else
                w = new StreamWriter(mobjClient.GetStream());
            w.Write(t + "\r\n");
            w.Flush();

            string[] fields = t.Split(new char[] { ' ' });
            if (fields[0].Equals("QUIT", StringComparison.OrdinalIgnoreCase) && t != "QUIT :Ping timeout; reconnecting.")
                this.VoluntarilyQuit = true;
            else if (fields[0].Equals("PRIVMSG", StringComparison.OrdinalIgnoreCase))
                this.LastSpoke = DateTime.Now;
        }

        public virtual void Send(string Format, params object[] Parameters) {
            this.Send(string.Format(Format, Parameters));
        }

        public static string RemoveCodes(string Message) {
            Regex regex = new Regex(@"\x03(\d{0,2}(,\d{1,2})?)?");
            Message = regex.Replace(Message.Trim(), "");
            Message = Message.Replace("\u0002", "");
            Message = Message.Replace("\u000F", "");
            Message = Message.Replace("\u0016", "");
            Message = Message.Replace("\u001C", "");
            Message = Message.Replace("\u001F", "");
            return Message;
        }
        public static string RemoveColon(string Data) {
            if (Data.StartsWith(":"))
                return Data.Substring(1);
            else
                return Data;
        }
        private void bgwRead_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            this.IsRegistered = false;
            this.IsConnected = false;
            this.Channels.Clear();
            this.mobjClient.Close();
            bool flag = !e.Cancelled & this.ReconnectAttempts != this.ReconnectMaxAttempts & !this.VoluntarilyQuit;
            if (flag) {
                this.ReconnectTimer = new System.Timers.Timer((double) this.ReconnectInterval) {
                    AutoReset = false,
                    Enabled = true
                };
                if (this.WaitingToReconnect != null) this.WaitingToReconnect(this, new decimal(this.ReconnectTimer.Interval / 1000.0), this.ReconnectAttempts, this.ReconnectMaxAttempts);
            }
        }
        private void Reconnect(object sender, ElapsedEventArgs e) {
            checked {
                ++this.ReconnectAttempts;
                this.Connect();
            }
        }
        private void OnChannelMode(string Sender, string Target, bool Direction, char Mode, string Parameter) {
            string[] matchedUsers;
            switch (Mode) {
                case 'I':
                    if (!this.ChanModes.TypeAModes.Contains(Mode)) return;
                    matchedUsers = FindMatchingUsers(Target, Parameter);
                    if (Direction) {
                        if (matchedUsers.Contains(this.Nickname)) {
                            if (this.ChannelInviteExemptSelf != null) this.ChannelInviteExemptSelf(this, new User(Sender), Target, Parameter, matchedUsers);
                        } else {
                            if (this.ChannelInviteExempt != null) this.ChannelInviteExempt(this, new User(Sender), Target, Parameter, matchedUsers);
                        }
                    }
                    break;
                case 'V':
                    if (!this.StatusPrefix.ContainsKey(Mode)) return;
                    if (Direction) {
                        this.Channels[Target].Users[Parameter].Access |= ChannelAccess.HalfVoice;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelHalfVoiceSelf != null) this.ChannelHalfVoiceSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelHalfVoice != null) this.ChannelHalfVoice(this, new User(Sender), Target, Parameter);
                        }
                    } else {
                        this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.HalfVoice;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelDeHalfVoiceSelf != null) this.ChannelDeHalfVoiceSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelDeHalfVoice != null) this.ChannelDeHalfVoice(this, new User(Sender), Target, Parameter);
                        }
                    }
                    break;
                case 'a':
                    if (!this.StatusPrefix.ContainsKey(Mode)) return;
                    if (Direction) {
                        this.Channels[Target].Users[Parameter].Access |= ChannelAccess.Admin;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelAdminSelf != null) this.ChannelAdminSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelAdmin != null) this.ChannelAdmin(this, new User(Sender), Target, Parameter);
                        }
                    } else {
                        this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.Admin;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelDeAdminSelf != null) this.ChannelDeAdminSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelDeAdmin != null) this.ChannelDeAdmin(this, new User(Sender), Target, Parameter);
                        }
                    }
                    break;
                case 'b':
                    if (!this.ChanModes.TypeAModes.Contains(Mode)) return;
                    matchedUsers = FindMatchingUsers(Target, Parameter);
                    if (Direction) {
                        if (matchedUsers.Contains(this.Nickname)) {
                            if (this.ChannelBanSelf != null) this.ChannelBanSelf(this, new User(Sender), Target, Parameter, matchedUsers);
                        } else {
                            if (this.ChannelBan != null) this.ChannelBan(this, new User(Sender), Target, Parameter, matchedUsers);
                        }
                    }
                    break;
                case 'e':
                    if (!this.ChanModes.TypeAModes.Contains(Mode)) return;
                    matchedUsers = FindMatchingUsers(Target, Parameter);
                    if (Direction) {
                        if (matchedUsers.Contains(this.Nickname)) {
                            if (this.ChannelExemptSelf != null) this.ChannelExemptSelf(this, new User(Sender), Target, Parameter, matchedUsers);
                        } else {
                            if (this.ChannelExempt != null) this.ChannelExempt(this, new User(Sender), Target, Parameter, matchedUsers);
                        }
                    }
                    break;
                case 'h':
                    if (!this.StatusPrefix.ContainsKey(Mode)) return;
                    if (Direction) {
                        this.Channels[Target].Users[Parameter].Access |= ChannelAccess.HalfOp;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelHalfOpSelf != null) this.ChannelHalfOpSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelHalfOp != null) this.ChannelHalfOp(this, new User(Sender), Target, Parameter);
                        }
                    } else {
                        this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.HalfOp;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelDeHalfOpSelf != null) this.ChannelDeHalfOpSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelDeHalfOp != null) this.ChannelDeHalfOp(this, new User(Sender), Target, Parameter);
                        }
                    }
                    break;
                case 'o':
                    if (!this.StatusPrefix.ContainsKey(Mode)) return;
                    if (Direction) {
                        this.Channels[Target].Users[Parameter].Access |= ChannelAccess.Op;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelOpSelf != null) this.ChannelOpSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelOp != null) this.ChannelOp(this, new User(Sender), Target, Parameter);
                        }
                    } else {
                        this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.Op;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelDeOpSelf != null) this.ChannelDeOpSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelDeOp != null) this.ChannelDeOp(this, new User(Sender), Target, Parameter);
                        }
                    }
                    break;
                case 'q':
                    if (this.StatusPrefix.ContainsKey(Mode)) {
                        if (Direction) {
                            this.Channels[Target].Users[Parameter].Access |= ChannelAccess.Owner;
                            if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                                if (this.ChannelOwnerSelf != null) this.ChannelOwnerSelf(this, new User(Sender), Target, Parameter);
                            } else {
                                if (this.ChannelOwner != null) this.ChannelOwner(this, new User(Sender), Target, Parameter);
                            }
                        } else {
                            this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.Owner;
                            if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                                if (this.ChannelDeOwnerSelf != null) this.ChannelDeOwnerSelf(this, new User(Sender), Target, Parameter);
                            } else {
                                if (this.ChannelDeOwner != null) this.ChannelDeOwner(this, new User(Sender), Target, Parameter);
                            }
                        }
                    } else if (this.ChanModes.TypeAModes.Contains(Mode)) {
                        matchedUsers = FindMatchingUsers(Target, Parameter);
                        if (Direction) {
                            if (matchedUsers.Contains(this.Nickname)) {
                                if (this.ChannelQuietSelf != null) this.ChannelQuietSelf(this, new User(Sender), Target, Parameter, matchedUsers);
                            } else {
                                if (this.ChannelQuiet != null) this.ChannelQuiet(this, new User(Sender), Target, Parameter, matchedUsers);
                            }
                        }
                    }
                    break;
                case 'v':
                    if (!this.StatusPrefix.ContainsKey(Mode)) return;
                    if (Direction) {
                        this.Channels[Target].Users[Parameter].Access |= ChannelAccess.Voice;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelVoiceSelf != null) this.ChannelVoiceSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelVoice != null) this.ChannelVoice(this, new User(Sender), Target, Parameter);
                        }
                    } else {
                        this.Channels[Target].Users[Parameter].Access &= ~ChannelAccess.Voice;
                        if (Parameter.Equals(this.Nickname, StringComparison.OrdinalIgnoreCase)) {
                            if (this.ChannelDeVoiceSelf != null) this.ChannelDeVoiceSelf(this, new User(Sender), Target, Parameter);
                        } else {
                            if (this.ChannelDeVoice != null) this.ChannelDeVoice(this, new User(Sender), Target, Parameter);
                        }
                    }
                    break;
            }
        }
        public string[] FindMatchingUsers(string Channel, string Mask) {
            List<string> MatchedUsers = new List<string>();
            StringBuilder exBuilder = new StringBuilder();

            foreach (char c in Mask) {
                if (c == '*') exBuilder.Append(".*");
                else if (c == '?') exBuilder.Append(".");
                else exBuilder.Append(Regex.Escape(c.ToString()));
            }
            Mask = exBuilder.ToString();

            foreach (ChannelUser user in this.Channels[Channel].Users) {
                if (Regex.IsMatch(user.User.ToString(), Mask)) MatchedUsers.Add(user.Nickname);
            }

            return MatchedUsers.ToArray();
        }

        internal void OnWhoList(string channelName, string username, string address, string server, string nickname, string flags, int hops, string fullName) {
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
                        if (this.StatusPrefix.TryGetValue(flag, out mode)) {
                            if (mode == 'V')
                                channelUser.Access |= ChannelAccess.HalfVoice;
                            else if (mode == 'v')
                                channelUser.Access |= ChannelAccess.Voice;
                            else if (mode == 'h')
                                channelUser.Access |= ChannelAccess.HalfOp;
                            else if (mode == 'o')
                                channelUser.Access |= ChannelAccess.Op;
                            else if (mode == 'a')
                                channelUser.Access |= ChannelAccess.Admin;
                            else if (mode == 'q')
                                channelUser.Access |= ChannelAccess.Owner;
                        }
                    }
                }
            }
            if (this.WhoList != null) this.WhoList(this, channelName, username, address, server, nickname, flags, hops, fullName);
        }

        public bool IsChannel(string Target) {
            if (Target == null || Target == "") return false;
            foreach (char c in this.ChannelTypes)
                if (Target[0] == c) return true;
            return false;
        }

        public IRCClient() : this(60) { }
        public IRCClient(int PingTimeout) {
            this.CaseMapping = "rfc1459";
            this.CaseMappingComparer = IRCStringComparer.RFC1459CaseInsensitiveComparer;
            this.ChannelLimit = new Dictionary<char, int> { { '#', int.MaxValue } };
            this.ChannelLength = 200;
            this.ChannelTypes = new char[] { '#', '&' };
            this.SupportsBanExceptions = false;
            this.SupportsInviteExceptions = false;
            this.KickMessageLength = int.MaxValue;
            this.ListModeLength = new Dictionary<char, int>();
            this.Modes = 3;
            this.NicknameLength = 9;
            this.StatusPrefix = new Dictionary<char, char> { { 'o', '@' }, { 'v', '+' } };
            this.StatusMessage = new char[0];
            this.MaxTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            this.TopicLength = int.MaxValue;
            this.LastSpoke = default(DateTime);
            this.Channels = new ChannelCollection();
            this.ReconnectInterval = 30000;
            this.ReconnectMaxAttempts = 10;
            this.ReconnectAttempts = 0;
            this.marData = new byte[512];
            this.mobjText = new StringBuilder();
            this.Users = new UserCollection();

            this._PingTimeout = PingTimeout;
            if (PingTimeout <= 0)
                this.PingTimer = new System.Timers.Timer();
            else
                this.PingTimer = new System.Timers.Timer((double) PingTimeout * 1000d);
        }
    }
}
