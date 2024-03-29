﻿namespace CBot;
/// <summary>
/// Specifies the scope in which a command can be used.
/// </summary>
[Flags]
public enum CommandScope {
	/// <summary>The command can be used in channels.</summary>
	Channel = 1,
	/// <summary>The command can be used via PM.</summary>
	PM = 2,
	/// <summary>The command can be used anywhere using global command syntax (as !plugin.command).</summary>
	Global = 4
}
