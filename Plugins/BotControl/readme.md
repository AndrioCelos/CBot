Bot Control plugin
==================

This plugin allows administrators to control the bot directly.

Commands
--------

The [server] parameter defaults to the one on which you run the command, if omitted.

* `!connect` — lists all servers the bot is on (including the console). I recommend using this in private, or the console.
* `!connect <server>` — connects (or reconnects) to the given server. It can be a server address, or a network name to reconnect to that network.
* `!join [server] <channel>` — causes the bot to join the given channel.
* `!part [server] <channel> [message]` — causes the bot to part the given channel.
* `!quit [server] [message]` — causes the bot to quit the given network.
* `!disconnect [server] [message]` — causes the bot to disconnect from the given server immediately. It still sends a quit message.
* `!raw [server] <message>` — causes the bot to send the given raw IRC command.

Permissions
-----------

* `me.connect` — enables access to the `!connect` command.
* `me.ircsend` — enables access to the `!join`, `!part`, `!quit`, `!disconnect` and `!raw` commands.
