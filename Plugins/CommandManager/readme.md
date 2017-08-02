Command Manager plugin
======================

This plugin allows labels and triggers for other commands to be modified.

Commands
--------

* `!addalias <alias> <command>` — adds a new alias to the specified command.
* `!delalias <alias>` — removes an alias from a command. The only alias cannot be deleted.
* `!addtrigger /<regex>/ <command>` — adds a trigger to the specified command. Capturing groups in the regular expression are passed as parameters to the command.

Permissions
-----------

These give access to the corresponding commands.

* `<plugin>.addalias`
* `<plugin>.delalias`
* `<plugin>.addtrigger`

where `<plugin>` is the plugin key.
