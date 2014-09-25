Version 2.7.2 – 25 September 2014
---------------------------------

* Now compatible with Battle Arena 2.6 September 24 beta

Version 2.7.1 – 25 September 2014
---------------------------------

* **Fix:** The plugin no longer crashes on startup if a network name or address is specified in its channel list.
* **Fix:** The Arena level is now tracked correctly.

Version 2.7 – 24 September 2014
-------------------------------

* Ported to C# for The Great Rewrite
* The plugin now works with Battle Arena version 2.5.
* The AI architecture has been redesigned. AI 1 is now known as AI 2, but works the same way. Instead of the AI being hard-coded, the plugin now provides a programming interface for AIs. That means other plugins can create a custom AI.
* The bot now understands `!stats` responses, even if the messages are out of order.