Battle Arena Manager
====================

This plugin performs operations that help run the _Battle Arena_ bot on EsperNet. These operations are possible because CBot and the _Battle Arena_ bot are running under the same user account on the same computer.
See _Battle Arena_ with an IRC client (irc://irc.esper.net/#BattleArena) or [webchat](http://webchat.esper.net/?channels=BattleArena).

Automatic updates
-----------------

We regularly check the [GitHub repository](https://github.com/Iyouboushi/mIRC-BattleArena) for updates, and when one is found, it will be automatically applied to the bot. Applying the update is a matter of downloading the new scripts, putting them in the Arena working directory, then telling the Arena bot to reload the scripts. We perform a backup too, of course.

Wine crash recovery
-------------------

Wine is the software used to run the Arena bot, which is mIRC, on Linux. Wine isn't perfect, and occasionally it crashed. This caused problems because someone (me) needs to go in and restart it, which means the Arena is down for a few hours until I get there. So I trained my CBot instance Angelina to do this for me, so that the Arena bot goes down for a few minutes instead of hours. Both bots run in a GNU screen session, so this is a simple matter.

Error reporting
---------------

Unfortunately, mIRC has no way to catch unhandled errors. The only way to handle them with a script is using an `:error` label. There is a way to automtically respond to mIRC script errors: they are written to mIRC's log, so we open the log file and watch for messages being written to it. Script errors are automatically reported to the channel and the bot admins.
