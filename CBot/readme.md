VBot
====

The framework itself. This, CBot.exe, is what you start to run the bot. It also provides the interactive setup wizard.

Of course, it can load different plugins to do much more.

Confguration files
------------------

There are three:

* `/CBotConfig.ini` for IRC server data
* `/CBotUsers.ini` for user accounts
* `/CBotPlugins.ini` for plugins

Plus those used by the plugins themselves, of course.
**To load a plugin**, add the following to **/CBotPlugins.ini**:

	[Key]
	Filename=Plugins/plugin.dll
	Channels=irc.example.net/#lobby,#bots

The `[Key]` (without brackets) is what you type in commands to refer to the plugin instance. Note that you can have multiple instances of the same plugin.
The channels are a list of channels for which this plugin receives events, and is also the list of channels that SayToAllChannels() methods use (excluding wildcards). It can include wildcards like */#bots, *, irc.home.net/*
The network can be an address (which must match that in the main configuration file), a network name, or omitted to refer to the named channel on any network.

Note that, with Plugin Manager loaded, you can run the following command to load a plugin:
`!loadplugin <key> [filename]`
To set its channels: `!pluginchans <key> <channels>`, where `<channels>` is in the same format as in the INI file.
