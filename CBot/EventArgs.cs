using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

using AnIRC;

namespace CBot {
	public class IrcClientEventArgs : EventArgs {
		public ClientEntry Entry { get; }

		public IrcClientEventArgs(ClientEntry entry) => this.Entry = entry;
	}

	/// <summary>
	/// Gives information about how a command has been invoked.
	/// </summary>
	public class CommandEventArgs : CancelEventArgs {
		/// <summary>The IRCClient object on which the command was heard.</summary>
		public IrcClient Client { get; }
		/// <summary>The channel in which the command was used, or null if it was not on a channel.</summary>
		public IrcChannel? Channel => this.Target as IrcChannel;
		/// <summary>The target of the event.</summary>
		public IrcMessageTarget Target { get; }
		/// <summary>The user invoking the command.</summary>
		public IrcUser Sender { get; }

		/// <summary>The list of parameters to the command.</summary>
		public string[] Parameters { get; }
		
		/// <summary>Initializes a new <see cref="CommandEventArgs"/> object with the specified data.</summary>
		/// <param name="client">The <see cref="IrcClient"/> on which the command was heard.</param>
		/// <param name="target">The target of the event.</param>
		/// <param name="sender">The user invoking the command.</param>
		/// <param name="parameters">The list of parameters to the command.</param>
		public CommandEventArgs(IrcClient client, IrcMessageTarget target, IrcUser sender, string[] parameters) {
			this.Client = client;
			this.Target = target;
			this.Sender = sender;
			this.Parameters = parameters;
			this.Cancel = true;
		}

		public void Reply(string message) => Bot.Say(this.Client, this.Target.Target, message);
		public void Reply(string format, params object[] args) => this.Reply(string.Format(format, args));
		public void Whisper(string message) => Bot.Say(this.Client, this.Sender.Nickname, message);
		public void Whisper(string format, params object[] args) => this.Whisper(string.Format(format, args));
		public void Fail(string message) => Bot.Say(this.Client, this.Target.Client.NetworkName == "Twitch" ? this.Target.Target : this.Sender.Nickname, message);
		public void Fail(string format, params object[] args) => this.Fail(string.Format(format, args));
	}

	/// <summary>
	/// Gives information about how a trigger has been triggered.
	/// </summary>
	public class TriggerEventArgs : CancelEventArgs {
		/// <summary>The <see cref="IrcClient"/> on which the trigger occurred.</summary>
		public IrcClient Client { get; }
		/// <summary>The channel in which the trigger occurred, or null if it was not on a channel.</summary>
		public IrcChannel? Channel => this.Target as IrcChannel;
		/// <summary>The target of the event.</summary>
		public IrcMessageTarget Target { get; }
		/// <summary>The user triggering the procedure.</summary>
		public IrcUser Sender { get; }

		/// <summary>The <see cref="System.Text.RegularExpressions.Match"/> object containing details of the match.</summary>
		public Match Match { get; }

		/// <summary>
		/// Initializes a new a <see cref="TriggerEventArgs"/> object with the specified data.
		/// </summary>
		/// <param name="client">The <see cref="IrcClient"/> on which the trigger occurred.</param>
		/// <param name="target">The channel in which the trigger occurred, or the sender if it was a PM.</param>
		/// <param name="sender">The user triggering the procedure.</param>
		/// <param name="match">The <see cref="System.Text.RegularExpressions.Match"/> object containing details of the match.</param>
		public TriggerEventArgs(IrcClient client, IrcMessageTarget target, IrcUser sender, Match match) {
			this.Client = client;
			this.Target = target;
			this.Sender = sender;
			this.Match = match;
		}

		public void Reply(string message) => Bot.Say(this.Client, this.Target.Target, message);
		public void Reply(string format, params object[] args) => this.Reply(string.Format(format, args));
		public void Whisper(string message) => Bot.Say(this.Client, this.Sender.Nickname, message);
		public void Whisper(string format, params object[] args) => this.Whisper(string.Format(format, args));
		public void Fail(string message) => Bot.Say(this.Client, this.Sender.Nickname, message);
		public void Fail(string format, params object[] args) => this.Fail(string.Format(format, args));
	}
}