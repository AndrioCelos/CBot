using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC {
    /// <summary>
    /// Represents the response to a WHOIS command.
    /// </summary>
    public class WhoisResponse {
        /// <summary>The user's nickname.</summary>
        public string Nickname { get; internal set; }
        /// <summary>The user's ident name.</summary>
        public string Ident { get; internal set; }
        /// <summary>The user's displayed host.</summary>
        public string Host { get; internal set; }
        /// <summary>The user's full name.</summary>
        public string FullName { get; internal set; }
        /// <summary>The name of the server the user is on.</summary>
        public string ServerName { get; internal set; }
        /// <summary>The description or tag line of the server the user is on.</summary>
        public string ServerInfo { get; internal set; }
        /// <summary>Indicates whether the user is an oper.</summary>
        public bool Oper { get; internal set; }
        /// <summary>The time the user has been idle, or null if the server didn't say.</summary>
        public TimeSpan? IdleTime { get; internal set; }
        /// <summary>The time the user logged in, or null if the server didn't say.</summary>
        public DateTime? SignonTime { get; internal set; }
        /// <summary>A dictionary listing the publicly visible channels the user is on, along with the user's status on them.</summary>
        public ReadOnlyDictionary<string, ChannelStatus> Channels { get; internal set; }
        /// <summary>The name of the server from which the response originated.</summary>
        public string ProvidingServerName { get; internal set; }
        /// <summary>Indicates whether the user is away.</summary>
        public bool Away => this.AwayMessage != null;
        /// <summary>If the user is away, returns their away message; otherwise returns null.</summary>
        public string AwayMessage { get; internal set; }
        /// <summary>The user's account name.</summary>
        public string Account { get; internal set; }
        /// <summary>The list of raw IRC lines that made up the response.</summary>
        public ReadOnlyCollection<IrcLine> Lines { get; internal set; }

        internal List<IrcLine> lines;
        internal Dictionary<string, ChannelStatus> channels;

        internal WhoisResponse(IrcClient client) {
            this.lines = new List<IrcLine>();
            this.Lines = lines.AsReadOnly();
            this.channels = new Dictionary<string, ChannelStatus>(client.CaseMappingComparer);
            this.Channels = new ReadOnlyDictionary<string, ChannelStatus>(this.channels);
        }
    }

    public class WhoResponse {
        public string Channel { get; internal set; }
        public string Ident { get; internal set; }
        public string Host { get; internal set; }
        public string Server { get; internal set; }
        public string Nickname { get; internal set; }
        public bool Away { get; internal set; }
        public bool Oper { get; internal set; }
        public ChannelStatus ChannelStatus { get; internal set; }
        public int HopCount { get; internal set; }
        public string FullName { get; internal set; }
    }
}
