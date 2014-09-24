Plugin manager plugin
=====================

This plugin provides some control over plugins.
It includes the save command, so it's recommended to load this.

Commands
--------

* `!loadplugin [key] <file>` — loads a new plugin. The key must be unique, as it's used to refer to the plugin in commands and config files.
* `!unloadplugin <key>` — calls the OnUnload method on the given plugin and removes it from the list of loaded plugins. It's not possible to actually unload it.
* `!saveplugin <key>` — calls the OnSave method on the given plugin.
* `!saveall` — calls the OnSave method on all plugins.
* `!pluginchans <plugin> [+|-]<channels>` — modifies the channel list of the given plugin. The + or - prefix will add or remove the following channels to the list.

Permissions
-----------

* `me.manageplugins` — gives access to all commands listed above.
