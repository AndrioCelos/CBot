using System;
using Newtonsoft.Json;

namespace CBot {
	/// <summary>
	/// Contains the data used to deal with nickname services.
	/// </summary>
	public class NickServSettings {
		/// <summary>The list of grouped nicknames.</summary>
		public string[] RegisteredNicknames;
		/// <summary>Indicates whether NickServ allows users to identify to a nickname other than their current one.</summary>
		public bool AnyNickname;
		/// <summary>Specifies whether the ghost command should be used to kill dead sessions.</summary>
		public bool UseGhostCommand;
		/// <summary>The format of the ghost command. The following tokens can be used: $target $nickname $password</summary>
		public string GhostCommand = "PRIVMSG $target :GHOST $nickname $password";
		/// <summary>The bot's NickServ password.</summary>
		public string Password;
		/// <summary>The format of the identify command. The following tokens can be used: $target $nickname $password</summary>
		public string IdentifyCommand = "PRIVMSG $target :IDENTIFY $password";
		/// <summary>A hostmask matched by NickServ.</summary>
		public string Hostmask = "NickServ!*@*";
		/// <summary>A mask that matches NickServ's request to identify.</summary>
		public string RequestMask = "*IDENTIFY*";
		/// <summary>The time when CBot last identified. Used for rate limiting.</summary>
		[JsonIgnore]
		public DateTime IdentifyTime;

		public NickServSettings(string[] registeredNicknames, string password, bool anyNickname, bool useGhostCommand) {
			this.RegisteredNicknames = registeredNicknames ?? throw new ArgumentNullException(nameof(registeredNicknames));
			this.Password = password;
			this.AnyNickname = anyNickname;
			this.UseGhostCommand = useGhostCommand;
		}
	}
}
