VBot
====

This is my pet IRC bot framework. It runs on the .NET framework. This is the framework that my helper bot, Angelina, runs on.
It is *by no means* complete.

Built in is:

* support for multiple IRC networks
* support for SSL
* support for user identification using a password (`/msg bot !id password`) (inspired by Battle Arena), hostmask or channel status flag
* a permission system (inspired by PermissionsEx)
* automatic NickServ identification
* an interactive setup wizard

Of course, it can load different plugins to do much more.

**Important:** You almost certainly need to load the **MainCommands** plugin, as it contains the `!id` command. (That'll be isolated at some point in the future, and maybe built in.)

Confguration files
------------------

There are three:

* `/VBotConfig.ini` for IRC server data
* `/VBotUsers.ini` for user accounts
* `/VBotPlugins.ini` for plugins

Plus those used by the plugins themselves, of course.
**To load a plugin**, add the following to **/VBotPlugins.ini**:

	[Key]
	Filename=VBotPlugins/plugin.dll
	Channels=irc.example.net/#lobby,irc.example.com/#bots
	MinorChannels=*
	Label=[New Plugin]

The `[Key]` (without brackets) is what you type in commands to refer to the plugin instance. Note that you can have multiple instances of the same plugin.
The channels are a list of channels for which this plugin receives events, and is also the list of channels that SayToAllChannels() methods use (excluding wildcards). It can include wildcards like */#bots, *, irc.home.net/*
The minor channels and label (prefix) are for basic status messages. Many of my plugins don't use it, and it may get removed fairly soon.

Note that, with MainCommands loaded, you can run the following command to load a plugin:
`!loadplugin <key> [filename]`
If the filename isn't specified, it defaults to `/VBotPlugins/<key>.dll`.
To set its channels: `!pluginchans <key> <channels>`, where `<channels>` is in the same format as in the INI file.

Important MainCommands
----------------------

* `!id [name] <password>` — identifies you to the bot.
* `!loadplugin <key> [filename]` — loads a plugin.
* `!pluginchans <key> [major|minor] <channels>` — sets a plugin's channel list.

* `!help [topic]` — shows help text defined by plugins.
* `!cmdlist` — shows a list of commands the user has access to, grouped by plugin key (needs reworking!).
* `!cmdinfo [command name without the '!']` — shows the syntax and a description of a command.

* `!vbotconfigload` — reloads /VBotConfig.ini and restarts all IRC connections.
* `!vbotconfigsave` — saves /VBotConfig.ini.
* `!vbotpluginsload` — reloads /VBotPlugins.ini and all plugins.
* `!vbotpluginssave` — saves /VBotPlugins.ini and causes all plugins to save data.
* `!vbotpluginsave <plugin key>` — calls OnSave() on that plugin.
* `!vbotusersload` — reloads /VBotUsers.ini and invalidates all identifications.
* `!vbotuserssave` — saves /VBotUsers.ini.
 
* `!ircsend <server address> <command>` — sends a raw IRC command.
* `!ircjoin [server address] <channel>` — causes the bot to join a channel.
* `!ircpart [server address] <channel> [message]` — causes the bot to flee a channel.
* `!ircquit <server address> [message]` — causes the bot to quit a server.
* `!ircquitall [message]` — causes the bot to quit all servers.
* `!die [message]` — quits all servers and shuts the bot down. *Remember to save before doing this!*

* `!autojoin [server address] <+|-><channel>` or `!autojoin [server address] [channels]` — modifies auto-join lists.
* `!nickserv [server address] <property> [value]` or `!nickserv [server address] remove` — changes NickServ identification settions
* `!nickserv [server address] add <nicknames> <password> [anynickname] [useghostcommand] [hostmask] [requestmask][ > <identifycommand>]` — sets up NickServ identification.

IRC channel
-----------

\#angelina on irc.esper.net
Use an [IRC client](irc://irc.esper.net/#angelina) or [webchat](http://webchat.esper.net/?channels=angelina)