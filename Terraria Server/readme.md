Terraria server bridge
======================

This plugin connects a Terraria server with an IRC channel, and does some stuff with it.
It allows players on the Terraria server to use VBot commands in its chat, as well as relaying messages to an IRC channel. It also supports some form of administration through an IRC channel.

Commands
--------

* `!start` — starts the server.
* `!stop` — tells the server to stop.
* `!restart`
* `!players` — shows who's online on the server.
* `!send <command>` — runs a command on the server console.
* `!set <setting> [value]`

You can also enter `> command` to send commands to the server.

* `!twhisper` — if used in-game on a TShock server, this will allow you to send private messags to the console via `/r`.

Configuration
-------------

The following settings can be changed:

* `workingdir` — the server's working directory
* `configfile` — the server configuration file
* `exe` — the Terraria server executable.
* `autostart` — if 'on', the server will be started when the plugin is loaded.

Not to mention all the `serverconfig.txt` settings.

Permissions
-----------

* `<plugin>.startstop` — enables the `!start`, `!stop` and `!restart` commands.
* `<plugin>.console` — enabled the !send command.