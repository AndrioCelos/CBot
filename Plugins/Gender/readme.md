Gender manager plugin
=====================

This plugin allows admins to inform the bot of a user's gender, when the user doesn't publicise it in their full name.
It also periodically sends WHO requests to channels to update this information.

Commands
--------

* `!setgender male|female|none|clear` — assigns the given gender to yourself.
* `!setgender <hostmask> male|female|none|clear` — assigns the given gender to the given hostmask.
* `!getgender <hostmask>|[server]/<nickname>` — returns the gender assigned to the given hostmask, or of the given user.
* `!set whointerval 0` — causes the plugin to send no WHO requests.
* `!set whointerval <time>` — causes the bot to send a WHO request per the given number of seconds.

Permissions
-----------

* `<plugin>.setgender` — enables access to the `!setgender` command.
* `<plugin>.setgender` — allows you to specify a gender for other users.
* `<plugin>.getgender` — enables access to the `!getgender` command.
* `<plugin>.set` — enables access to the `!set` command.
