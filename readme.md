CBot
====

Known as VBot before The Great Rewrite, this is my pet IRC bot framework. It runs on the .NET framework. This is the framework that my helper bot, Angelina, runs on.
It is *by no means* complete.

Built in is:

* support for multiple IRC networks
* support for SSL
* a permission system (inspired by PermissionsEx)
* support for assigning permissions by a password (inspired by Battle Arena), hostmask or channel status flag
* automatic NickServ identification
* an interactive setup wizard

Support for user identification has been demoted to a plugin (Identify).

Of course, it can load different plugins to do much more.

Changelogs can be found in the individual project folders.

Confguration files
------------------

There are three:

* `/config.json` for IRC server data
* `/users.json` for user accounts
* `/plugins.json` for plugins

Plus those used by the plugins themselves, of course.
**To load a plugin** after the setup procedure, add the following to the dictionary in **plugins.json**:

```JSON
	"Key": {
		"Filename": "Plugins/plugin.dll",
		"Channels": [ "irc.example.net/#lobby", "#bots" ]
	}
```

The `Key` is what you type in commands to refer to the plugin instance. Note that you can have multiple instances of the same plugin.
The channels are a list of channels for which this plugin receives events, and is also the list of channels that SayToAllChannels() methods use (excluding wildcards). It can include wildcards like `*/#bots`, `*`, `irc.home.net/*`
The network can be an address (which must match that in the main configuration file), a network name, or omitted to refer to the named channel on any network.

Note that, with Plugin Manager loaded, you can run the following command to load a plugin:
`!loadplugin <key> [filename]`
To set its channels: `!pluginchans <key> <channels>`, where `<channels>` is in the same format as in the INI file.

Important commands from plugins
-------------------------------

* `!id [name] <password>` — identifies you to the bot.
* `!loadplugin <key> [filename]` — loads a plugin.
* `!pluginchans <key> [major|minor] <channels>` — sets a plugin's channel list.

* `!saveplugins` — saves plugins.json and causes all plugins to save data.
* `!saveplugin <plugin key>` — calls `OnSave` on that plugin.

* `!raw <server address> <command>` — sends a raw IRC command.
* `!ircjoin [server address] <channel>` — causes the bot to join a channel.
* `!ircpart [server address] <channel> [message]` — causes the bot to flee a channel.
* `!ircquit <server address> [message]` — causes the bot to quit a server.

IRC channel
-----------

\#andrio on irc.esper.net

Use an IRC client (ircs://irc.esper.net/#andrio) or [webchat](http://webchat.esper.net/?channels=andrio)
