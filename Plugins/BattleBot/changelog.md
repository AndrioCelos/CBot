Version 2.7 – 24 September 2014
-------------------------------

* Ported to C# for The Great Rewrite
* The plugin now works with Battle Arena version 2.5.
* The AI architecture has been redesigned. AI 1 is now known as AI 2, but works the same way. Instead of the AI being hard-coded, the plugin now provides a programming interface for AIs. That means other plugins can create a custom AI.
* The bot now understands `!stats` responses, even if the messages are out of order.
