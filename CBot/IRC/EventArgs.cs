using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public class AwayMessageEventArgs : EventArgs {
        public string Nickname { get; set; }
        public string Reason { get; set; }

        public AwayMessageEventArgs(string nickname, string reason) {
            this.Nickname = nickname;
            this.Reason = reason;
        }
    }

    public class ChannelChangeEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }

        public ChannelChangeEventArgs(IrcUser sender, IrcChannel channel) {
            this.Sender = sender;
            this.Channel = channel;
        }
    }

    public class ChannelJoinEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }

        public ChannelJoinEventArgs(IrcUser sender, IrcChannel channel) {
            this.Sender = sender;
            this.Channel = channel;
        }
    }

    public class ChannelJoinDeniedEventArgs : EventArgs {
        public string Channel { get; set; }
        public ChannelJoinDeniedReason Reason { get; private set; }
        public string Message { get; set; }

        public ChannelJoinDeniedEventArgs(string channel, ChannelJoinDeniedReason reason, string message) {
            this.Channel = channel;
            this.Reason = reason;
            this.Message = message;
        }
    }

    public class ChannelKeyEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public string Key { get; set; }

        public ChannelKeyEventArgs(IrcUser sender, IrcChannel channel, string key) {
            this.Sender = sender;
            this.Channel = channel;
            this.Key = key;
        }
    }

    public class ChannelKickEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public IrcChannelUser Target { get; set; }
        public string Reason { get; set; }

        public ChannelKickEventArgs(IrcUser sender, IrcChannel channel, IrcChannelUser target, string reason) {
            this.Sender = sender;
            this.Channel = channel;
            this.Target = target;
            this.Reason = reason;
        }
    }

    public class ChannelLimitEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public int Limit { get; set; }

        public ChannelLimitEventArgs(IrcUser sender, IrcChannel channel, int limit) {
            this.Sender = sender;
            this.Channel = channel;
            this.Limit = limit;
        }
    }

    public class ChannelListChangedEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public bool Direction { get; set; }
        public char Mode { get; set; }
        public string Parameter { get; set; }
        public IEnumerable<IrcChannelUser> MatchedUsers { get; set; }

        public ChannelListChangedEventArgs(IrcUser sender, IrcChannel channel, bool direction, char mode, string parameter, IEnumerable<IrcChannelUser> matchedUsers) {
            this.Sender = sender;
            this.Channel = channel;
            this.Direction = direction;
            this.Mode = mode;
            this.Parameter = parameter;
            this.MatchedUsers = Channel.Users.Matching(parameter);
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

    public class ChannelMessageEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public string Message { get; set; }

        public ChannelMessageEventArgs(IrcUser sender, IrcChannel channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelModeChangedEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public bool Direction { get; set; }
        public char Mode { get; set; }
        public string Parameter { get; set; }

        public ChannelModeChangedEventArgs(IrcUser sender, IrcChannel channel, bool direction, char mode, string parameter) {
            this.Sender = sender;
            this.Channel = channel;
            this.Direction = direction;
            this.Mode = mode;
            this.Parameter = parameter;
        }
    }

    public class ChannelModeListEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public string Mask { get; set; }
        public string AddedBy { get; set; }
        public DateTime AddedOn { get; set; }

        public ChannelModeListEventArgs(IrcChannel channel, string mask, string addedBy, DateTime addedOn) {
            this.Channel = channel;
            this.Mask = mask;
            this.AddedBy = addedBy;
            this.AddedOn = addedOn;
        }
    }

    public class ChannelModeListEndEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public string Message { get; set; }

        public ChannelModeListEndEventArgs(IrcChannel channel, string message) {
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelModesGetEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public ModeSet Modes { get; set; }

        public ChannelModesGetEventArgs(IrcChannel channel, ModeSet modes) {
            this.Channel = channel;
            this.Modes = modes;
        }
    }

    public class ChannelModesSetEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public ReadOnlyCollection<ModeChange> Modes { get; set; }

        public ChannelModesSetEventArgs(IrcUser sender, IrcChannel channel, IList<ModeChange> modes) {
            this.Sender = sender;
            this.Channel = channel;
            this.Modes = new ReadOnlyCollection<ModeChange>(modes);
        }
    }

    public class ChannelNamesEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public string Names { get; set; }

        public ChannelNamesEventArgs(IrcChannel channel, string names) {
            this.Channel = channel;
            this.Names = names;
        }
    }

    public class ChannelPartEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public string Message { get; set; }

        public ChannelPartEventArgs(IrcUser sender, IrcChannel channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelStatusChangedEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public bool Direction { get; set; }
        public char Mode { get; set; }
        public IrcChannelUser Target { get; set; }

        public ChannelStatusChangedEventArgs(IrcUser sender, IrcChannel channel, bool direction, char mode, IrcChannelUser target) {
            this.Sender = sender;
            this.Channel = channel;
            this.Direction = direction;
            this.Mode = mode;
            this.Target = target;
        }
    }

    public class ChannelTimestampEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public DateTime Timestamp { get; set; }

        public ChannelTimestampEventArgs(IrcChannel channel, DateTime timestamp) {
            this.Channel = channel;
            this.Timestamp = timestamp;
        }
    }

    public class ChannelTopicEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public string Topic { get; set; }

        public ChannelTopicEventArgs(IrcChannel channel, string topic) {
            this.Channel = channel;
            this.Topic = topic;
        }
    }

    public class ChannelTopicChangeEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public IrcChannel Channel { get; set; }
        public string OldTopic { get; set; }
        public string OldTopicSetter { get; set; }
        public DateTime OldTopicStamp { get; set; }

        public ChannelTopicChangeEventArgs(IrcUser sender, IrcChannel channel, string oldTopic, string oldTopicSetter, DateTime oldTopicStamp) {
            this.Sender = sender;
            this.Channel = channel;
            this.OldTopic = oldTopic;
            this.OldTopicSetter = oldTopicSetter;
            this.OldTopicStamp = oldTopicStamp;
        }
    }

    public class ChannelTopicStampEventArgs : EventArgs {
        public IrcChannel Channel { get; set; }
        public string Setter { get; set; }
        public DateTime Timestamp { get; set; }

        public ChannelTopicStampEventArgs(IrcChannel channel, string setter, DateTime timestamp) {
            this.Channel = channel;
            this.Setter = setter;
            this.Timestamp = timestamp;
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

    public class ExceptionEventArgs : EventArgs {
        public Exception Exception { get; }
        public bool Fatal { get; }

        public ExceptionEventArgs(Exception exception, bool fatal) {
            this.Exception = exception;
            this.Fatal = fatal;
        }
    }

    public class IrcLineEventArgs : RawLineEventArgs {
        public IrcLine Line { get; }

        public IrcLineEventArgs(string data, IrcLine line) : base(data) {
            this.Line = line;
        }
    }

    public class IrcUserEventArgs : EventArgs {
        public IrcUser User { get; }

        public IrcUserEventArgs(IrcUser user) {
            this.User = user;
        }
    }

    public class InviteEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public string Target { get; set; }
        public string Channel { get; set; }

        public InviteEventArgs(IrcUser sender, string target, string channel) {
            this.Sender = sender;
            this.Target = target;
            this.Channel = channel;
        }
    }

    public class InviteSentEventArgs : EventArgs {
        public string Channel { get; set; }
        public string Target { get; set; }

        public InviteSentEventArgs(string channel, string target) {
            this.Channel = channel;
            this.Target = target;
        }
    }

    public class MotdEventArgs : EventArgs {
        public string Message { get; set; }

        public MotdEventArgs(string message) {
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
        public IrcUser Sender { get; set; }
        public string NewNickname { get; set; }

        public NicknameChangeEventArgs(IrcUser sender, string newNickname) {
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

    public class PrivateMessageEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public string Target { get; set; }
        public string Message { get; set; }

        public PrivateMessageEventArgs(IrcUser sender, string target, string message) {
            this.Sender = sender;
            this.Target = target;
            this.Message = message;
        }
    }

    public class QuitEventArgs : EventArgs {
        public IrcUser Sender { get; set; }
        public string Message { get; set; }

        public QuitEventArgs(IrcUser sender,  string message) {
            this.Sender = sender;
            this.Message = message;
        }
    }

    public class RawLineEventArgs : EventArgs {
        public string Data { get; set; }

        public RawLineEventArgs(string data) {
            this.Data = data;
        }
    }

    public class RegisteredEventArgs : EventArgs {
        /// <summary>Returns a value indicating whether the connection is going to be continued or terminated.</summary>
        /// <remarks>The connection will be terminated if SASL authentication was required and the server doesn't support it.</remarks>
        public bool Continuing { get; }

        public RegisteredEventArgs(bool continuing) {
            this.Continuing = continuing;
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

    public class StateEventArgs : EventArgs {
        public IrcClientState OldState { get; }
        public IrcClientState NewState { get; }

        public StateEventArgs(IrcClientState oldState, IrcClientState newState) {
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
