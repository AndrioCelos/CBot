using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace IRC {
    public class AwayEventArgs : EventArgs {
		/// <summary>Returns the status message received from the server.</summary>
        public string Message { get; set; }

        public AwayEventArgs(string message) {
            this.Message = message;
        }
    }

    public class AwayMessageEventArgs : EventArgs {
		/// <summary>Returns the nickname in the message.</summary>
        public string Nickname { get; set; }
		/// <summary>Returns the user's away message.</summary>
        public string Reason { get; set; }

        public AwayMessageEventArgs(string nickname, string reason) {
            this.Nickname = nickname;
            this.Reason = reason;
        }
    }

    public class ChannelChangeEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
        public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
        public IrcChannel Channel { get; set; }

        public ChannelChangeEventArgs(IrcUser sender, IrcChannel channel) {
            this.Sender = sender;
            this.Channel = channel;
        }
    }

    public class ChannelJoinEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who is joining.</summary>
		public IrcUser Sender { get; set; }
		/// Returns an <see cref="IrcChannel"/> object representing the channel that is affected.
		public IrcChannel Channel { get; set; }
        /// <summary>If the local user joined a channel, returns a <see cref="Task"/> that will complete when the NAMES list is received.</summary>
        public Task NamesTask { get; }

        public ChannelJoinEventArgs(IrcUser sender, IrcChannel channel) {
            this.Sender = sender;
            this.Channel = channel;
        }
        public ChannelJoinEventArgs(IrcUser sender, IrcChannel channel, Task namesTask) {
            this.Sender = sender;
            this.Channel = channel;
            this.NamesTask = namesTask;
        }
    }

    public class ChannelJoinDeniedEventArgs : EventArgs {
		/// <summary>Returns the name of the channel in the message.</summary>
        public string Channel { get; set; }
		/// <summary>Returns a <see cref="ChannelJoinDeniedReason"/> value representing the reason a join failed.</summary>
        public ChannelJoinDeniedReason Reason { get; private set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public ChannelJoinDeniedEventArgs(string channel, ChannelJoinDeniedReason reason, string message) {
            this.Channel = channel;
            this.Reason = reason;
            this.Message = message;
        }
    }

    public class ChannelKeyEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
        public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the new channel key, or null if a key was removed.</summary>
		public string Key { get; set; }

        public ChannelKeyEventArgs(IrcUser sender, IrcChannel channel, string key) {
            this.Sender = sender;
            this.Channel = channel;
            this.Key = key;
        }
    }

    public class ChannelKickEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns an <see cref="IrcChannelUser"/> object representing the user who was kicked out.</summary>
		public IrcChannelUser Target { get; set; }
		/// <summary>Returns the reason provided by the kicker.</summary>
        public string Reason { get; set; }

        public ChannelKickEventArgs(IrcUser sender, IrcChannel channel, IrcChannelUser target, string reason) {
            this.Sender = sender;
            this.Channel = channel;
            this.Target = target;
            this.Reason = reason;
        }
    }

    public class ChannelLimitEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the new limit.</summary>
		public int Limit { get; set; }

        public ChannelLimitEventArgs(IrcUser sender, IrcChannel channel, int limit) {
            this.Sender = sender;
            this.Channel = channel;
            this.Limit = limit;
        }
    }

    public class ChannelListChangedEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns true if an entry was added, or false if one was removed.</summary>
        public bool Direction { get; set; }
		/// <summary>Returns the mode character of the changed list.</summary>
        public char Mode { get; set; }
		/// <summary>Returns the entry that was added or removed.</summary>
		public string Parameter { get; set; }
		/// <summary>Returns an <see cref="IEnumerable{T}"/> of <see cref="IrcChannelUser"/> that enumerates users on the channel who match the parameter.</summary>
		/// <remarks>This property uses deferred execution. This means that the user list is not actually searched until the enumerable is enumerated.</remarks>
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
		/// <summary>Returns the name of a channel. Some servers may mask the name for private channels.</summary>
        public string Channel { get; set; }
		/// <summary>Returns the number of users on the channel, as received from the server.</summary>
		public int Users { get; set; }
		/// <summary>Returns the topic of the channel, as received from the server.</summary>
        public string Topic { get; set; }

        public ChannelListEventArgs(string channel, int users, string topic) {
            this.Channel = channel;
            this.Users = users;
            this.Topic = topic;
        }
    }

    public class ChannelListEndEventArgs : EventArgs {
		/// <summary>Returns the status message received from the server.</summary>
        public string Message { get; set; }

        public ChannelListEndEventArgs(string message) {
            this.Message = message;
        }
    }

    public class ChannelMessageEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who sent the message.</summary>
        public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel receiving the message.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the message text.</summary>
        public string Message { get; set; }

        public ChannelMessageEventArgs(IrcUser sender, IrcChannel channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelModeChangedEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns true if a mode was set, or false if one was removed.</summary>
		public bool Direction { get; set; }
		/// <summary>Returns the mode character of the mode that was changed.</summary>
		public char Mode { get; set; }
		/// <summary>Returns the parameter to the mode change, or null if there was no parameter.</summary>
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
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the list entry in the message.</summary>
        public string Mask { get; set; }
		/// <summary>Returns the nickname or hostmask of the entity who added the entry. This may be reset during netsplits.</summary>
		public string AddedBy { get; set; }
		/// <summary>Returns the time when the entry was added. This may be reset during netsplits.</summary>
        public DateTime AddedOn { get; set; }

        public ChannelModeListEventArgs(IrcChannel channel, string mask, string addedBy, DateTime addedOn) {
            this.Channel = channel;
            this.Mask = mask;
            this.AddedBy = addedBy;
            this.AddedOn = addedOn;
        }
    }

    public class ChannelModeListEndEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public ChannelModeListEndEventArgs(IrcChannel channel, string message) {
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelModesGetEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns a <see cref="ModeSet"/> object representing the modes on the channel.</summary>
        public ModeSet Modes { get; set; }

        public ChannelModesGetEventArgs(IrcChannel channel, ModeSet modes) {
            this.Channel = channel;
            this.Modes = modes;
        }
    }

    public class ChannelModesSetEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that was affected.</summary>
        public IrcChannel Channel { get; set; }
		/// <summary>Returns a list of <see cref="ModeChange"/> values representing the changes that were made.</summary>
        public ReadOnlyCollection<ModeChange> Modes { get; set; }

        public ChannelModesSetEventArgs(IrcUser sender, IrcChannel channel, IList<ModeChange> modes) {
            this.Sender = sender;
            this.Channel = channel;
            this.Modes = new ReadOnlyCollection<ModeChange>(modes);
        }
    }

    public class ChannelNamesEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the raw list fragment.</summary>
        public string Names { get; set; }

        public ChannelNamesEventArgs(IrcChannel channel, string names) {
            this.Channel = channel;
            this.Names = names;
        }
    }

    public class ChannelPartEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who is leaving.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that was affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the part message, or null if there was no part message.</summary>
        public string Message { get; set; }

        public ChannelPartEventArgs(IrcUser sender, IrcChannel channel, string message) {
            this.Sender = sender;
            this.Channel = channel;
            this.Message = message;
        }
    }

    public class ChannelStatusChangedEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that was affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns true if a mode was set, or false if one was removed.</summary>
		public bool Direction { get; set; }
		/// <summary>Returns the mode character of the mode that was changed.</summary>
		public char Mode { get; set; }
		/// <summary>Returns a <see cref="IrcChannelUser"/> object representing the user whose status changed.</summary>
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
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the time when the channel was created.</summary>
        public DateTime Timestamp { get; set; }

        public ChannelTimestampEventArgs(IrcChannel channel, DateTime timestamp) {
            this.Channel = channel;
            this.Timestamp = timestamp;
        }
    }

    public class ChannelTopicEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the channel topic, or null if there is no topic.</summary>
        public string Topic { get; set; }

        public ChannelTopicEventArgs(IrcChannel channel, string topic) {
            this.Channel = channel;
            this.Topic = topic;
        }
    }

    public class ChannelTopicChangeEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who made the change.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that is affected.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the old channel topic.</summary>
        public string OldTopic { get; set; }
		/// <summary>Returns the nickname or hostmask of the entity who set the old channel topic.</summary>
        public string OldTopicSetter { get; set; }
		/// <summary>Returns the time when the old topic was set.</summary>
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
		/// <summary>Returns an <see cref="IrcChannel"/> object representing the channel that this message refers to.</summary>
		public IrcChannel Channel { get; set; }
		/// <summary>Returns the nickname or hostmask of the entity who set the channel topic. This may be reset during a netsplit.</summary>
		public string Setter { get; set; }
		/// <summary>Returns the time when the topic was set.</summary>
        public DateTime Timestamp { get; set; }

        public ChannelTopicStampEventArgs(IrcChannel channel, string setter, DateTime timestamp) {
            this.Channel = channel;
            this.Setter = setter;
            this.Timestamp = timestamp;
        }
    }

    public class DisconnectEventArgs : EventArgs {
		/// <summary>Returns a <see cref="DisconnectReason"/> value specifying the cause of the disconnection.</summary>
        public DisconnectReason Reason { get; }
		/// <summary>If the disconnection caused an exception to be thrown, returns the exception.</summary>
        public Exception Exception { get; }

        public DisconnectEventArgs(DisconnectReason reason, Exception exception) {
            this.Reason = reason;
            this.Exception = exception;
        }
    }

    public class ExceptionEventArgs : EventArgs {
		/// <summary>Returns the exception that occurred.</summary>
        public Exception Exception { get; }
		/// <summary>Returns a value indicating whether the connection cannot continue.</summary>
        public bool Fatal { get; }

        public ExceptionEventArgs(Exception exception, bool fatal) {
            this.Exception = exception;
            this.Fatal = fatal;
        }
    }

    public class IrcLineEventArgs : RawLineEventArgs {
		/// <summary>Returns an <see cref="IrcLine"/> object representing the received line.</summary>
        public IrcLine Line { get; }
		/// <summary>Returns a value indicating whether the line matched any async requests.</summary>
		/// <seealso cref="AsyncRequest"/>
        public bool MatchesAsyncRequests { get; }

        public IrcLineEventArgs(string data, IrcLine line, bool matchesAsyncRequests) : base(data) {
            this.Line = line;
            this.MatchesAsyncRequests = matchesAsyncRequests;
        }
    }

    public class IrcUserEventArgs : EventArgs {
        public IrcUser User { get; }

        public IrcUserEventArgs(IrcUser user) {
            this.User = user;
        }
    }

    public class InviteEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who sent the message.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns the nickname that the message is addressed to.</summary>
        public string Target { get; set; }
		/// <summary>Returns the name of the channel that this message refers to.</summary>
		public string Channel { get; set; }

        public InviteEventArgs(IrcUser sender, string target, string channel) {
            this.Sender = sender;
            this.Target = target;
            this.Channel = channel;
        }
    }

    public class InviteSentEventArgs : EventArgs {
		/// <summary>Returns the name of the channel that you are inviting to.</summary>
		public string Channel { get; set; }
		/// <summary>Returns the nickname that the message is addressed to.</summary>
		public string Target { get; set; }

        public InviteSentEventArgs(string channel, string target) {
            this.Channel = channel;
            this.Target = target;
        }
    }

    public class MotdEventArgs : EventArgs {
		/// <summary>Returns a line of the MotD.</summary>
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
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user whose nickname is changing.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns the user's new nickname.</summary>
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
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who sent the message.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns the target that the message is addressed to: usually the local user's nickname, but may be something else, such as global messages.</summary>
        public string Target { get; set; }
		/// <summary>Returns the message text.</summary>
		public string Message { get; set; }

        public PrivateMessageEventArgs(IrcUser sender, string target, string message) {
            this.Sender = sender;
            this.Target = target;
            this.Message = message;
        }
    }

    public class QuitEventArgs : EventArgs {
		/// <summary>Returns an <see cref="IrcUser"/> object representing the user who is quitting.</summary>
		public IrcUser Sender { get; set; }
		/// <summary>Returns the quit message.</summary>
        public string Message { get; set; }

        public QuitEventArgs(IrcUser sender,  string message) {
            this.Sender = sender;
            this.Message = message;
        }
    }

    public class RawLineEventArgs : EventArgs {
		/// <summary>Returns or sets the line that is to be sent as a string.</summary>
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
		/// <summary>Returns the error message text.</summary>
        public string Message { get; set; }

        public ServerErrorEventArgs(string message) {
            this.Message = message;
        }
    }

    public class StateEventArgs : EventArgs {
		/// <summary>Returns the previous state of the <see cref="IrcClient"/>.</summary>
        public IrcClientState OldState { get; }
		/// <summary>Returns the new state of the <see cref="IrcClient"/>.</summary>
		public IrcClientState NewState { get; }

        public StateEventArgs(IrcClientState oldState, IrcClientState newState) {
            this.OldState = oldState;
            this.NewState = newState;
        }
    }

    public class UserModeEventArgs : EventArgs {
		/// <summary>Returns true if a mode was set, or false if one was removed.</summary>
		public bool Direction { get; set; }
		/// <summary>Returns the mode character of the mode that was changed.</summary>
		public char Mode { get; set; }

        public UserModeEventArgs(bool direction, char mode) {
            this.Direction = direction;
            this.Mode = mode;
        }
    }

    public class UserModesEventArgs : EventArgs {
		/// <summary>Returns a string representing the local user's current user modes.</summary>
        public string Modes { get; set; }

        public UserModesEventArgs(string modes) {
            this.Modes = modes;
        }
    }

    public class ValidateCertificateEventArgs : EventArgs {
		/// <summary>Returns the certificate presented by the server.</summary>
        public X509Certificate Certificate { get; }
		/// <summary>Returns the chain of certificate authorities associated with the server's certificate.</summary>
		public X509Chain Chain { get; }
		/// <summary>Returns a value indicating why the certificate is invalid.</summary>
        public SslPolicyErrors SslPolicyErrors { get; }
		/// <summary>Returns or sets a value specifying whether the connection will continue.</summary>
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
		/// <summary>Returns one of the channels the local user shares with this user, or "*".</summary>
		public string Channel { get; set; }
		/// <summary>Returns the user's ident username.</summary>
        public string Ident { get; set; }
		/// <summary>Returns the user's hostname.</summary>
        public string Host { get; set; }
		/// <summary>Returns the name of the server that the user is connected to.</summary>
		public string Server { get; set; }
		/// <summary>Returns the user's nickname.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns a list of flags that apply to this user. See RFC 2812 for more details.</summary>
		public char[] Flags { get; set; }
		/// <summary>Returns the number of 'hops' between this server and the user's server.</summary>
		public int Hops { get; set; }
		/// <summary>Returns the user's full name.</summary>
		public string FullName { get; set; }

        public WhoListEventArgs(string channel, string username, string host, string server, string nickname, char[] flags, int hops, string fullName) {
            this.Channel = channel;
            this.Ident = username;
            this.Host = host;
            this.Server = server;
            this.Nickname = nickname;
            this.Flags = flags;
            this.Hops = hops;
            this.FullName = fullName;
        }
    }

    public class WhoisAuthenticationEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
        public string Nickname { get; set; }
		/// <summary>Returns the user's services account name, or null if the user is not identified with services.</summary>
        public string Account { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public WhoisAuthenticationEventArgs(string nickname, string account, string message) {
            this.Nickname = nickname;
            this.Account = account;
            this.Message = message;
        }
    }

    public class WhoisChannelsEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the raw list of channels that the user is on.</summary>
		public string Channels { get; set; }

        public WhoisChannelsEventArgs(string nickname, string channels) {
            this.Nickname = nickname;
            this.Channels = channels;
        }
    }

    public class WhoisEndEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public WhoisEndEventArgs(string nickname, string message) {
            this.Nickname = nickname;
            this.Message = message;
        }
    }

    public class WhoisIdleEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the user's idle time.</summary>
        public TimeSpan IdleTime { get; set; }
		/// <summary>Returns the time when the user registered.</summary>
        public DateTime LoginTime { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public WhoisIdleEventArgs(string nickname, TimeSpan idleTime, DateTime loginTime, string message) {
            this.Nickname = nickname;
            this.IdleTime = idleTime;
            this.LoginTime = loginTime;
            this.Message = message;
        }
    }

    public class WhoisNameEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the user's ident username.</summary>
        public string Username { get; set; }
		/// <summary>Returns the user's hostname.</summary>
        public string Host { get; set; }
		/// <summary>Returns the user's full name.</summary>
        public string FullName { get; set; }

        public WhoisNameEventArgs(string nickname, string username, string host, string fullName) {
            this.Nickname = nickname;
            this.Username = username;
            this.Host = host;
            this.FullName = fullName;
        }
    }

    public class WhoisOperEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public WhoisOperEventArgs(string nickname, string message) {
            this.Nickname = nickname;
            this.Message = message;
        }
    }

    public class WhoisRealHostEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the user's real hostname.</summary>
        public string RealHost { get; set; }
		/// <summary>Returns the user's real IP address.</summary>
        public IPAddress RealIP { get; set; }
		/// <summary>Returns the status message received from the server.</summary>
		public string Message { get; set; }

        public WhoisRealHostEventArgs(string nickname, string realHost, IPAddress realIP, string message) {
            this.Nickname = nickname;
            this.RealHost = realHost;
            this.RealIP = realIP;
            this.Message = message;
        }
    }

    public class WhoisServerEventArgs : EventArgs {
		/// <summary>Returns the nickname of the user that this message refers to.</summary>
		public string Nickname { get; set; }
		/// <summary>Returns the name of the server that this user is connected to.</summary>
        public string Server { get; set; }
		/// <summary>Returns the information line of the server that this user is connected to.</summary>
		public string Info { get; set; }

        public WhoisServerEventArgs(string nickname, string server, string info) {
            this.Nickname = nickname;
            this.Server = server;
            this.Info = info;
        }
    }
}
