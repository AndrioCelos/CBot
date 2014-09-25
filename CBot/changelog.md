Version 3.0.1 – 25 September 2014
---------------------------------

* Auto-joining channels is now done by a separate thread from the one receiving messages.

Version 3.0 – 24 September 2014 (The Great Rewrite)
---------------------------------------------------

* The entire project is being ported to C# and renamed accordingly to CBot.
* The IRC library has been redesigned. There is now a global list of users for each client, as well as a list of channels the bot is on, which each contain a list of users on the channel. Each list of users contains different information.
* The IRC library now supports gender codes.
* Plugins are now version locked. Old and incompatible plugins won't load.
* The minor channel concept has been retired. Plugins wishing to retain this functionality should maintain a list of channels on their own.
* Output attributes have been retired. The console now uses an IRCClient subclass to emulate an IRC channel. Other plugins can also do this.
* Command and Regex attributes now use an EventArgs subclass rather than a difficult-to-remember-and-change parameter list.
* Advanced Console Output is now known as CBot.ConsoleUtils, and its procedures have been rearranged. The escape character is now `%` instead of `\`.