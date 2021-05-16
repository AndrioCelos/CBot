using System;
using System.Collections.Generic;

using AnIRC;

namespace CBot {
	/// <summary>
	/// Records a user's identification to an account.
	/// </summary>
	public class Identification {
		/// <summary>The connection to the IRC network the user is on.</summary>
		public IrcClient Connection;
		/// <summary>The user's nickname.</summary>
		public string Nickname;
		/// <summary>The account to which the user has identified.</summary>
		public string AccountName;
		/// <summary>Indicates whether CBot is watching this user using the MONITOR or WATCH command.</summary>
		public bool Monitoring;
		/// <summary>The list of channels this user shares with the bot.</summary>
		public HashSet<string> Channels;

		public Identification(IrcClient connection, string nickname, string accountName, bool monitoring, HashSet<string> channels) {
			this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
			this.Nickname = nickname ?? throw new ArgumentNullException(nameof(nickname));
			this.AccountName = accountName ?? throw new ArgumentNullException(nameof(accountName));
			this.Monitoring = monitoring;
			this.Channels = channels ?? throw new ArgumentNullException(nameof(channels));
		}
	}
}
