using System;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    /// <summary>
    /// Represents an entity on IRC that can receive messages.
    /// </summary>
    public class IrcMessageTarget {
        /// <summary>Returns the <see cref="IrcClient"/> that this entity belongs to.</summary>
        public virtual IrcClient Client { get; }

        /// <summary>When overridden, returns the value of the target parameter used to send messages to this entity.</summary>
        public virtual string Target { get; }

        /// <summary>When overridden, returns a value indicating whether this target is a private query or a group.</summary>
        public virtual bool IsPrivate { get; }

        /// <summary>Initializes a new <see cref="IrcMessageTarget"/> with no target string.</summary>
        protected IrcMessageTarget() { }
        /// <summary>Initializes a new <see cref="IrcMessageTarget"/> with the specified target string.</summary>
        public IrcMessageTarget(IrcClient client, string target) {
            this.Client = client;
            this.Target = target;
        }

        /// <summary>Returns the maximum length, in bytes, of a PRIVMSG that can be sent to this entity.</summary>
        public virtual int MaxPrivmsgLength => 498 - this.Client.Encoding.GetByteCount(this.Client.Me.ToString()) - this.Client.Encoding.GetByteCount(this.Target);
        /// <summary>Returns the maximum length, in bytes, of a NOTICE that can be sent to this entity.</summary>
        public virtual int MaxNoticeLength => 499 - this.Client.Encoding.GetByteCount(this.Client.Me.ToString()) - this.Client.Encoding.GetByteCount(this.Target);
        /// <summary>Returns the maximum length, in bytes, of a CTCP action that can be sent to this entity.</summary>
        public virtual int MaxActionLength => this.MaxPrivmsgLength - 9;

        /// <summary>Sends a PRIVMSG to this entity, splitting it over multiple lines if needed.</summary>
        /// <seealso cref="MaxPrivmsgLength"/>
        public virtual void Say(string message) {
            foreach (var part in this.Client.SplitMessage(message, this.MaxPrivmsgLength))
                this.Client.Send("PRIVMSG " + this.Target + " :" + part);
        }
        /// <summary>Sends a NOTICE to this entity, splitting it over multiple lines if needed.</summary>
        /// <seealso cref="MaxNoticeLength"/>
        public virtual void Notice(string message) {
            foreach (var part in this.Client.SplitMessage(message, this.MaxNoticeLength))
                this.Client.Send("NOTICE " + this.Target + " :" + part);
        }

        /// <summary>Sends a client-to-client protocol request to this entity.</summary>
        /// <param name="message">The message to send, which may include spaces.</param>
        public void Ctcp(string message) => this.Say(Colours.CTCP + message + Colours.CTCP);
        /// <summary>Sends a client-to-client protocol request to this entity.</summary>
        /// <param name="command">The command to send.</param>
        /// <param name="arg0">The parameter to the command.</param>
        public void Ctcp(string command, string arg0) => this.Ctcp(command + " " + arg0);
        /// <summary>Sends a client-to-client protocol request to this entity.</summary>
        /// <param name="command">The command to send.</param>
        /// <param name="args">The parameters to the command.</param>
        public void Ctcp(string command, params string[] args) {
            var builder = new StringBuilder();
            builder.Append(Colours.CTCP);
            builder.Append(command);
            foreach (var arg in args) {
                builder.Append(' ');
                builder.Append(arg);
            }
            builder.Append(Colours.CTCP);
            this.Say(builder.ToString());
        }

        /// <summary>Sends a client-to-client protocol reply to this entity.</summary>
        /// <param name="message">The message to send, which may include spaces.</param>
        public void CtcpReply(string message) => this.Notice(Colours.CTCP + message + Colours.CTCP);
        /// <summary>Sends a client-to-client protocol reply to this entity.</summary>
        /// <param name="command">The command to acknowledge.</param>
        /// <param name="arg0">The parameter to the reply.</param>
        public void CtcpReply(string command, string arg0) => this.CtcpReply(command + " " + arg0);
        /// <summary>Sends a client-to-client protocol reply to this entity.</summary>
        /// <param name="command">The command to acknowledge.</param>
        /// <param name="args">The parameters to the reply.</param>
        public void CtcpReply(string command, params string[] args) {
            var builder = new StringBuilder();
            builder.Append(Colours.CTCP);
            builder.Append(command);
            foreach (var arg in args) {
                builder.Append(' ');
                builder.Append(arg);
            }
            builder.Append(Colours.CTCP);
            this.Notice(builder.ToString());
        }

        /// <summary>Sends a CTCP action to this entity, splitting it over multiple lines if needed.</summary>
        /// <seealso cref="MaxActionLength"/>
        public void Act(string action) {
            foreach (var part in this.Client.SplitMessage(action, this.MaxActionLength))
                this.Ctcp("ACTION", part);
        }

        /// <summary>Returns a string representing this entity.</summary>
        public override string ToString() => this.Target;
    }
}
