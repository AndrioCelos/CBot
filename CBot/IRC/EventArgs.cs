using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace IRC {
    public class AwayEventArgs : EventArgs {
        public string Message { get; set; }

        public AwayEventArgs(string message) {
            this.Message = message;
        }
    }

    public class ChannelEventArgs : EventArgs {
        public string Channel { get; set; }

        public ChannelEventArgs(string channel) {
            this.Channel = channel;
        }
    }

    public class ChannelModeListEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Mask { get; set; }
        public string AddedBy { get; set; }
        public DateTime AddedOn { get; set; }

        public ChannelModeListEventArgs(string channel, string mask, string addedBy, DateTime addedOn) {
            this.Channel = channel;
            this.Mask = mask;
            this.AddedBy = addedBy;
            this.AddedOn = addedOn;
        }
    }

    public class ChannelModeListEndEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Message { get; set; }

        public ChannelModeListEndEventArgs(string channel, string message) {
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelMessageEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public string Message { get; set; }

        public ChannelMessageEventArgs(IRCUser sender, string channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelNicknameModeEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public IRCChannelUser Target { get; set; }

        public ChannelNicknameModeEventArgs(IRCUser sender, string channel, IRCChannelUser target) {
            this.Sender = sender;
            this.Channel = channel;
            this.Target = target;
        }
    }

    public class ChannelListModeEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public string Mask { get; set; }
        public IRCChannelUser[] MatchedUsers { get; set; }

        public ChannelListModeEventArgs(IRCUser sender, string channel, string mask, IRCChannelUser[] matchedUsers) {
            this.Sender = sender;
            this.Channel = channel;
            this.Mask = mask;
            this.MatchedUsers = matchedUsers;
        }
    }

    public class ChannelTimestampEventArgs : EventArgs {
        public string Channel { get; set; }
        public DateTime Timestamp { get; set; }

        public ChannelTimestampEventArgs(string channel, DateTime timestamp) {
            this.Channel = channel;
            this.Timestamp = timestamp;
        }
    }

    public class ChannelInviteEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Target { get; set; }
        public string Channel { get; set; }

        public ChannelInviteEventArgs(IRCUser sender, string target, string channel) {
            this.Sender = sender;
            this.Target = target;
            this.Channel = channel;
        }
    }

    public class ChannelInviteSentEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Target { get; set; }

        public ChannelInviteSentEventArgs(string channel, string target) {
            this.Channel = channel;
            this.Target = target;
        }
    }

    public class ChannelJoinEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }

        public ChannelJoinEventArgs(IRCUser sender, string channel) {
            this.Sender = sender;
            this.Channel = channel;
        }
    }

    public class ChannelKickEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public IRCChannelUser Target { get; set; }
        public string Reason { get; set; }

        public ChannelKickEventArgs(IRCUser sender, string channel, IRCChannelUser target, string reason) {
            this.Sender = sender;
            this.Channel = channel;
            this.Target = target;
            this.Reason = reason;
        }
    }

    public class ChannelDeniedEventArgs : EventArgs {
        public string Channel { get; set; }
        public ChannelJoinDeniedReason Reason { get; private set; }
        public string Message { get; set; }

        public ChannelDeniedEventArgs(string channel, ChannelJoinDeniedReason reason, string message) {
            this.Channel = channel;
            this.Reason = reason;
            this.Message = message;
        }
    }

    public class ChannelModeEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public bool Direction { get; set; }
        public char Mode { get; set; }

        public ChannelModeEventArgs(IRCUser sender, string channel, bool direction, char mode) {
            this.Sender = sender;
            this.Channel = channel;
            this.Direction = direction;
            this.Mode = mode;
        }
    }

    public class ChannelModesSetEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public string Modes { get; set; }

        public ChannelModesSetEventArgs(IRCUser sender, string channel, string modes) {
            this.Sender = sender;
            this.Channel = channel;
            this.Modes = modes;
        }
    }

    public class ChannelModesGetEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Modes { get; set; }

        public ChannelModesGetEventArgs(string channel, string modes) {
            this.Channel = channel;
            this.Modes = modes;
        }
    }

    public class ChannelKeyEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public string Key { get; set; }

        public ChannelKeyEventArgs(IRCUser sender, string channel, string key) {
            this.Sender = sender;
            this.Channel = channel;
            this.Key = key;
        }
    }

    public class ChannelLimitEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public int Limit { get; set; }

        public ChannelLimitEventArgs(IRCUser sender, string channel, int limit) {
            this.Sender = sender;
            this.Channel = channel;
            this.Limit = limit;
        }
    }

    public class ChannelPartEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Channel { get; set; }
        public string Message { get; set; }

        public ChannelPartEventArgs(IRCUser sender, string channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelTopicEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Topic { get; set; }

        public ChannelTopicEventArgs(string channel, string topic) {
            this.Channel = channel;
            this.Topic = topic;
        }
    }

    public class ChannelTopicChangeEventArgs : EventArgs {
        public IRCChannelUser Sender { get; set; }
        public string Channel { get; set; }
        public string Topic { get; set; }

        public ChannelTopicChangeEventArgs(IRCChannelUser sender, string channel, string topic) {
            this.Sender = sender;
            this.Channel = channel;
            this.Topic = topic;
        }
    }

    public class ChannelTopicStampEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Setter { get; set; }
        public DateTime Timestamp { get; set; }

        public ChannelTopicStampEventArgs(string channel, string setter, DateTime timestamp) {
            this.Channel = channel;
            this.Setter = setter;
            this.Timestamp = timestamp;
        }
    }

    public class ChannelNamesEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Names { get; set; }

        public ChannelNamesEventArgs(string channel, string names) {
            this.Channel = channel;
            this.Names = names;
        }
    }

    public class ExceptionEventArgs : EventArgs {
        public Exception Exception { get; }
        public bool Fatal { get; }

        public ExceptionEventArgs(Exception exception, bool fatal) {
            this.Exception = exception;
            this.Fatal = fatal;
        }
    }

    public class DisconnectEventArgs : EventArgs {
        public DisconnectReason Reason { get; }
        public Exception Exception { get; }

        public DisconnectEventArgs(DisconnectReason reason, Exception exception) {
            this.Reason = reason;
            this.Exception = exception;
        }
    }

    public class ChannelListEventArgs : EventArgs {
        public string Channel { get; set; }
        public int Users { get; set; }
        public string Topic { get; set; }

        public ChannelListEventArgs(string channel, int users, string topic) {
            this.Channel = channel;
            this.Users = users;
            this.Topic = topic;
        }
    }

    public class ChannelListEndEventArgs : EventArgs {
        public string Message { get; set; }

        public ChannelListEndEventArgs(string message) {
            this.Message = message;
        }
    }

    public class PrivateMessageEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Target { get; set; }
        public string Message { get; set; }

        public PrivateMessageEventArgs(IRCUser sender, string target, string message) {
            this.Sender = sender;
            this.Target = target;
            this.Message = message;
        }
    }

    public class MOTDEventArgs : EventArgs {
        public string Message { get; set; }

        public MOTDEventArgs(string message) {
            this.Message = message;
        }
    }

    public class NicknameEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Message { get; set; }

        public NicknameEventArgs(string nickname, string message) {
            this.Nickname = nickname;
            this.Message = message;
        }
    }

    public class NicknameChangeEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string NewNickname { get; set; }

        public NicknameChangeEventArgs(IRCUser sender, string newNickname) {
            this.Sender = sender;
            this.NewNickname = newNickname;
        }
    }

    public class PingEventArgs : EventArgs {
        public string Server { get; set; }

        public PingEventArgs(string server) {
            this.Server = server;
        }
    }

    public class QuitEventArgs : EventArgs {
        public IRCUser Sender { get; set; }
        public string Message { get; set; }

        public QuitEventArgs(IRCUser sender,  string message) {
            this.Sender = sender;
            this.Message = message;
        }
    }

    public class IRCLineEventArgs : RawEventArgs {
        public IRCLine Line { get; }

        public IRCLineEventArgs(string data, IRCLine line) : base(data) {
            this.Line = line;
        }
    }

    public class RawEventArgs : EventArgs {
        public string Data { get; set; }

        public RawEventArgs(string data) {
            this.Data = data;
        }
    }

    public class StateEventArgs : EventArgs {
        public IRCClientState OldState { get; }
        public IRCClientState NewState { get; }

        public StateEventArgs(IRCClientState oldState, IRCClientState newState) {
            this.OldState = oldState;
            this.NewState = newState;
        }
    }

    public class UserModeEventArgs : EventArgs {
        public bool Direction { get; set; }
        public char Mode { get; set; }

        public UserModeEventArgs(bool direction, char mode) {
            this.Direction = direction;
            this.Mode = mode;
        }
    }

    public class UserModesEventArgs : EventArgs {
        public string Modes { get; set; }

        public UserModesEventArgs(string modes) {
            this.Modes = modes;
        }
    }

    public class ServerErrorEventArgs : EventArgs {
        public string Message { get; set; }

        public ServerErrorEventArgs(string message) {
            this.Message = message;
        }
    }

    public class ServerMessageEventArgs : EventArgs {
        public string Sender { get; set; }
        public string Numeric { get; set; }
        public string[] Parameters { get; set; }
        public string Message { get; set; }

        public ServerMessageEventArgs(string sender, string numeric, string[] parameters, string message) {
            this.Sender = sender;
            this.Numeric = numeric;
            this.Parameters = parameters;
            this.Message = message;
        }
    }

    public class ValidateCertificateEventArgs : EventArgs {
        public X509Certificate Certificate { get; }
        public X509Chain Chain { get; }
        public SslPolicyErrors SslPolicyErrors { get; }
        public bool Valid { get; set; }

        public ValidateCertificateEventArgs(X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
            : this(certificate, chain, errors, false) { }
        public ValidateCertificateEventArgs(X509Certificate certificate, X509Chain chain, SslPolicyErrors errors, bool valid) {
            this.Certificate = certificate;
            this.Chain = chain;
            this.SslPolicyErrors = errors;
            this.Valid = valid;
        }
    }

    public class WhoListEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Username { get; set; }
        public string Host { get; set; }
        public string Server { get; set; }
        public string Nickname { get; set; }
        public char[] Flags { get; set; }
        public int Hops { get; set; }
        public string FullName { get; set; }

        public WhoListEventArgs(string channel, string username, string host, string server, string nickname, char[] flags, int hops, string fullName) {
            this.Channel = channel;
            this.Username = username;
            this.Host = host;
            this.Server = server;
            this.Nickname = nickname;
            this.Flags = flags;
            this.Hops = hops;
            this.FullName = fullName;
        }
    }

    public class WhoisAuthenticationEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Account { get; set; }
        public string Message { get; set; }

        public WhoisAuthenticationEventArgs(string nickname, string account, string message) {
            this.Nickname = nickname;
            this.Account = account;
            this.Message = message;
        }
    }

    public class WhoisAwayEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Reason { get; set; }

        public WhoisAwayEventArgs(string nickname, string reason) {
            this.Nickname = nickname;
            this.Reason = reason;
        }
    }

    public class WhoisChannelsEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Channels { get; set; }

        public WhoisChannelsEventArgs(string nickname, string channels) {
            this.Nickname = nickname;
            this.Channels = channels;
        }
    }

    public class WhoisEndEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Message { get; set; }

        public WhoisEndEventArgs(string nickname, string message) {
            this.Nickname = nickname;
            this.Message = message;
        }
    }

    public class WhoisIdleEventArgs : EventArgs {
        public string Nickname { get; set; }
        public TimeSpan IdleTime { get; set; }
        public DateTime LoginTime { get; set; }
        public string Message { get; set; }

        public WhoisIdleEventArgs(string nickname, TimeSpan idleTime, DateTime loginTime, string message) {
            this.Nickname = nickname;
            this.IdleTime = idleTime;
            this.LoginTime = loginTime;
            this.Message = message;
        }
    }

    public class WhoisNameEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Username { get; set; }
        public string Host { get; set; }
        public string FullName { get; set; }

        public WhoisNameEventArgs(string nickname, string username, string host, string fullName) {
            this.Nickname = nickname;
            this.Username = username;
            this.Host = host;
            this.FullName = fullName;
        }
    }

    public class WhoisOperEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Message { get; set; }

        public WhoisOperEventArgs(string nickname, string message) {
            this.Nickname = nickname;
            this.Message = message;
        }
    }

    public class WhoisRealHostEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string RealHost { get; set; }
        public IPAddress RealIP { get; set; }
        public string Message { get; set; }

        public WhoisRealHostEventArgs(string nickname, string realHost, IPAddress realIP, string message) {
            this.Nickname = nickname;
            this.RealHost = realHost;
            this.RealIP = realIP;
            this.Message = message;
        }
    }

    public class WhoisServerEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Server { get; set; }
        public string Info { get; set; }

        public WhoisServerEventArgs(string nickname, string server, string info) {
            this.Nickname = nickname;
            this.Server = server;
            this.Info = info;
        }
    }
}
