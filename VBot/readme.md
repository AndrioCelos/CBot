VBot
====

The framework itself. This, VBot.exe, is what you start to run the bot. It also provides the interactive setup wizard.

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
