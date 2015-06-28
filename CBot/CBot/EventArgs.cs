using System;
using System.Text.RegularExpressions;

using IRC;

namespace CBot {
    /// <summary>
    /// Gives information about how a command has been invoked.
    /// </summary>
    public class CommandEventArgs : EventArgs {
        /// <summary>The IRCClient object on which the command was heard.</summary>
        public IRCClient Connection { get; private set; }
        /// <summary>The channel in which the command was used, or the sender's nickname if it was a PM.</summary>
        public string Channel { get; private set; }
        /// <summary>The user invoking the command.</summary>
        public User Sender { get; private set; }

        /// <summary>The list of parameters to the command.</summary>
        public string[] Parameters { get; private set; }

        /// <summary>If this is set to true, no more commands will be processed for this message. Defaults to true.</summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Creates a CommandEventArgs object with the specified data.
        /// </summary>
        /// <param name="connection">The IRCClient object on which the command was heard.</param>
        /// <param name="channel">The channel in which the command was used, or the sender's nickname if it was a PM.</param>
        /// <param name="sender">The user invoking the command.</param>
        /// <param name="parameters">The list of parameters to the command.</param>
        public CommandEventArgs(IRCClient connection, string channel, User sender, string[] parameters) {
            this.Connection = connection;
            this.Channel = channel;
            this.Sender = sender;
            this.Parameters = parameters;
            this.Cancel = true;
        }
    }

    /// <summary>
    /// Gives information about how a regex-bound procedure has been triggered.
    /// </summary>
    public class RegexEventArgs : EventArgs {
        /// <summary>The IRCClient object on which the trigger occurred.</summary>
        public IRCClient Connection { get; private set; }
        /// <summary>The channel in which the trigger occurred, or the sender's nickname if it was a PM.</summary>
        public string Channel { get; private set; }
        /// <summary>The user triggering the procedure.</summary>
        public User Sender { get; private set; }

        /// <summary>The RegularExpressions.Match object containing details of the match.</summary>
        public Match Match { get; private set; }

        /// <summary>If this is set to true, no more triggers will be processed for this message. Defaults to false.</summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// Creates a RegexEventArgs object with the specified data.
        /// </summary>
        /// <param name="connection">The IRCClient object on which the trigger occurred.</param>
        /// <param name="channel">The channel in which the trigger occurred, or the sender's nickname if it was a PM.</param>
        /// <param name="sender">The user triggering the procedure.</param>
        /// <param name="match">The RegularExpressions.Match object containing details of the match.</param>
        public RegexEventArgs(IRCClient connection, string channel, User sender, Match match) {
            this.Connection = connection;
            this.Channel = channel;
            this.Sender = sender;
            this.Match = match;
            this.Cancel = false;
        }
    }
}