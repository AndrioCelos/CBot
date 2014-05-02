Core commands
=============

This plugin provides some important commands for VBot, as well as some unimportant ones that will be moved to another plugin at some point.
You almost certainly need to load this plugin, as it contains the `!id` command. (That'll be isolated at some point in the future, and maybe built in to VBot.)

Commands
--------

* `!id [name] <password>` — identifies you to the bot.
* `!help [topic]` — shows help text defined by plugins.
* `!cmdlist` — shows a list of commands the user has access to, grouped by plugin key (needs reworking!).
* `!cmdinfo [command name without the '!']` — shows the syntax and a description of a command.

* `!dns <address>` — looks up a hostname or IP address.
* `!unixtime <time>` — translates to and from UNIX time.

Admin commands
--------------

* `!loadplugin <key> [filename]` — loads a plugin.
* `!pluginchans <key> [major|minor] <channels>` — sets a plugin's channel list.

* `!vbotconfigload` — rel/oads /VBotConfig.ini and restarts all IRC connections.
* `!vbotconfigsave` — saves /VBotConfig.ini.
* `!vbotpluginsload` — reloads /VBotPlugins.ini and all plugins.
* `!vbotpluginssave` — saves /VBotPlugins.ini and causes all plugins to save data.
* `!vbotpluginsave <plugin key>` — calls OnSave() on that plugin.
* `!vbotusersload` — reloads /VBotUsers.ini and invalidates all identifications.
* `!vbotuserssave` — saves /VBotUsers.ini.
 
* `!connect <address>` — connects to an IRC server.
* `!disconnect <address>` — disconnects from an IRC server.

* `!names <channel>` — returns all users on the given channel. **Use this privately.**
* `!ircsend <server address> <command>` — sends a raw IRC command.
* `!ircjoin [server address] <channel>` — causes the bot to join a channel.
* `!ircpart [server address] <channel> [message]` — causes the bot to flee a channel.
* `!ircquit <server address> [message]` — causes the bot to quit a server.
* `!ircquitall [message]` — causes the bot to quit all servers.
* `!die [message]` — quits all servers and shuts the bot down. *Remember to save before doing this!*

* `!autojoin [server address] <+|-><channel>` or `!autojoin [server address] [channels]` — modifies auto-join lists.
* `!nickserv [server address] <property> [value]` or `!nickserv [server address] remove` — changes NickServ identification settions
* `!nickserv [server address] add <nicknames> <password> [anynickname] [useghostcommand] [hostmask] [requestmask][ > <identifycommand>]` — sets up NickServ identification.

It is possible to define custom command prefixes for channels. If you do, the default prefix, '!', is no longer used in those channels.

* `!prefixadd <server>/<channel> <prefix>`
* `!prefixremove <server>/<channel> <prefix>`
* `!prefixlist <server>/<channel>`

Permissions
-----------

* `me.ircsend` — enables the `!ircsend`, `!ircjoin`, `!ircpart`, `!ircquit` and `!ircquitall` commands.
* `me.manageplugins` — enables the `!loadplugin`, `!pluginchans`, `!pluginlabel`, `!unloadplugin` and `!reloadplugin` commands.
* `me.nickserv` — enables the `!nickserv` command.
* `me.prefix` — enables the `!prefixadd`, `!prefixremove` and `!prefixlist` commands.
* `me.reload` — enables the '`load`' commands.
* `me.save` — enables the '`save`' commands.
* `me.die` — enabled the `!die` command.
