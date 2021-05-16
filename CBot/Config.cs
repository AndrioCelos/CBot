﻿using System;
using System.Collections.Generic;

namespace CBot {
	public sealed class Config {
		public string[] Nicknames = new[] { "CBot" };
		public string Ident = "CBot";
		public string FullName = "CBot by Andrio Celos";
		public string UserInfo = "CBot by Andrio Celos";
		public string? Avatar;

		public string[] CommandPrefixes = new[] { "!" };
		public Dictionary<string, string[]> ChannelCommandPrefixes = new(StringComparer.OrdinalIgnoreCase);

		public List<ClientEntry> Networks = new();
	}
}
