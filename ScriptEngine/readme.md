Scripting
=========

A scripting engine. It's definitely not complete.
There are currently two kinds of scripts: event scripts and immediate (`~`) scripts. Event scripts are like remote scripts in mIRC: they are loaded, and the script runs when the event is triggered.

Commands
--------

* `!loadscript <filename>` — loads a script file.
* `~ <command>` — runs a script command.
* `!eval <expression>` or `~? <expression>` — evaluates an expression.

Control structures
------------------

**Events**

	event <name>:<parameter>:<permission>:<channel>:{
		commands
	}

**If**

	if (condition)
	{
		commands
	}
	else
	{
		commands
	}

**While loop**

	while (condition)
	{
		commands
	}

**Variables**

These actually are control structures, and not commands.

* `set <variable> <value>`
* `unset <variable>`
* `inc <variable> [value]`
* `dec <variable> [value]`

**Halting**

* `halt` — stops all further processing of an event or function call stack.
* `haltdef` — not yet implemented.

Variables
---------

Borrowed from mIRC, variable names consist of a '%' followed by alphanumeric characters. They work pretty much the same as in mIRC. However, the set commands can set members of plugins too. For example:

	set $UNO.TurnTimeLimit 90

Functions
---------

Are called like this: `$function(argument, list)`. Arguments can be expressions containing variables, functions and scalars. The space after the comma is optional.

Script commands
---------------

Are called like this: `command argument list`. Functions in plugins can also be used: `plugin-key.function argument list`.
Each command (or control structure) must go on its own line.

Comments
--------

Any line in a script that starts with a ';' is considered a comment, and ignored.

Permissions
-----------

* `script.execute` — enables $functions and the `~` command.
* `<plugin>.load` — enables the `!loadscript` command.
