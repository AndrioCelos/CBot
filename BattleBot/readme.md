BattleBot plugin
================

This plugin allows the bot to oversee and participate in Battle Arena.
See Battle Arena with an [IRC client](irc://irc.esper.net/#BattleArena) or [webchat](http://webchat.esper.net/?channels=BattleArena).

The bot **is** able to connect to a DCC session and participate that way. It is also able to partake in PvP if participation is enabled.

Setting up
----------

The plugin's database flie is `/BattleArena-<key>.ini`. For the bot to participate, the following data must be added to it:

	[Me:<name>]
	Password=<password>

The bot will attempt to log in to Battle Arena using that password. At the moment, modified player character full names are unsupported, but not for long.
If participation is enabled, the bot will look up a lot of information about its character. In the following order: attributes, weapons, techniques, skills and styles will be examined. Players the bot will control will also be subject to a similar examination. Be aware that this might flood the Arena bot a little bit.

The database is saved when the plugin's `OnSave()` method is called. Be sure to save after this finishes.

If the bot has Arena admin status, it will automatically discover this and enable certain commands accordingly. It currently does so by cycling the `!toggle ai system` command.

Configuration
-------------

The configuration file is `/Config/<key>.ini`. The following settings are there, and can also be set using the `set <property> <value>` command:

* `Analysis`: if 'on', the bot will learn about the Arena combatants.
* `Participation`: if 'on', the bot will enter and fight in battles.
* `MinPlayers`: the bot won't enter battles without this many other players.
* `Gambling`: if 'on', the bot will bet on AI battles. At the moment, it always bets $$10 based on the favourite and the character's past performance in AI battles.
* `AI`: set to '0' or '1' to select which version of the AI the bot will use. Version 0 is an AI roulette; version 1 is more analytical.
* `ArenaDirectory`: if the bot can access the Arena bot's root folder, set this to a path to it to enable certain commands.

User commands
-------------

* `!controlme` — instructs the bot to control your character.
* `!stopcontrol` — instructs the bot to stop controlling your character.
* `!time` — asks how much time you have left before darkness arises.

Admin commands
--------------

* `!control <player>` — instructs the bot to control someone.
* `!stopcontrol <player>` — instructs the bot to stop controlling someone.
* `!arena-id` — causes the bot to re-identify.

If the bot has access to the Arena files, it can do these:

* `!lateentry <player>` — enters a player after the battle starts.
* `!rename <player> <new name>` — renames a character file.
* `!restore <player> [new name]` — restores a zapped character. This includes characters who were retired for inactivity. If there are multple zapped character files with the given name, it'll use the most recent one.