Minecraft server bridge
=======================

This plugin connects a Minecraft server with an IRC channel, and does some stuff with it.
It allows players on the Minecraft server to use VBot commands in its chat, as well as relaying messages to an IRC channel. It also supports some form of administration through an IRC channel.

By default, the bot will also restart the server each midnight, if left running at that time.

Commands
--------

* `!start` — starts the server.
* `!stop` — tells the server to stop.
* `!restart`
* `!players` — shows who's online on the server.
* `!send <command>` — runs a command on the server console.
* `!updatecheck [RB|beta|dev]` — checks for CraftBukkit updates.
* `!set <setting> [value]`

You can also enter `> command` to send commands to the server.

Configuration
-------------

The following settings can be changed:

* `workingdir` — the server's working directory
* `java` — the Java VM executable location
* `jar` — the Minecraft server .jar
* `Xms` — the initial heap size (e.g. 1024M)
* `Xmx` — the maximum heap size (e.g. 2048M)
* `Xincgc` — if 'on', incremental garbage collecton will be enabled on the server.
* `autostart` — if 'on', the server will be started when the plugin is loaded.

* `relayserverchat` — if 'on', chat messages from Minecraft will be relayed to IRC.
* `relayircchat` — if 'on', chat messages from IRC will be relayed to Minecraft.

Not to mention all the `server.properties` settings.

Permissions
-----------

* `<plugin>.startstop` — enables the `!start`, `!stop` and `!restart` commands.
* `<plugin>.console` — enables the !send command.