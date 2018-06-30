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
		public string GhostCommand;
		/// <summary>The bot's NickServ password.</summary>
		public string Password;
		/// <summary>The format of the identify command. The following tokens can be used: $target $nickname $password</summary>
		public string IdentifyCommand;
		/// <summary>A hostmask matched by NickServ.</summary>
		public string Hostmask;
		/// <summary>A mask that matches NickServ's request to identify.</summary>
		public string RequestMask;
		/// <summary>The time when CBot last identified. Used for rate limiting.</summary>
		[JsonIgnore]
		public DateTime IdentifyTime;

		/// <summary>Creates a NickServSettings object with standard default data.</summary>
		/// <remarks>
		///     The default data is the following:
		///         RegisteredNicknames = [empty array]
		///         AnyNickname         = false
		///         UseGhostCommand     = true
		///         GhostCommand        = PRIVMSG $target :GHOST $nickname $password
		///         IdentifyCommand     = PRIVMSG $target :IDENTIFY $password
		///         Hostmask            = NickServ!*@*
		///         RequestMask         = *IDENTIFY*
		/// </remarks>
		public NickServSettings() {
			this.RegisteredNicknames = new string[0];
			this.AnyNickname = false;
			this.UseGhostCommand = true;
			this.GhostCommand = "PRIVMSG $target :GHOST $nickname $password";
			this.IdentifyCommand = "PRIVMSG $target :IDENTIFY $password";
			this.Hostmask = "NickServ!*@*";
			this.RequestMask = "*IDENTIFY*";
			this.IdentifyTime = default(DateTime);
		}
	}
}
