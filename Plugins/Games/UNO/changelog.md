Version 1.14 – 8 August 2018
----------------------------

* Implemented the penalty for not calling UNO.

Version 1.12.1 – 8 February 2017
------------------------------

* **Fix**: Going out with Draw cards with progressive rules enabled no longer corrupts the game state.

Version 1.12 – 25 January 2017
------------------------------

* Internal reorganisations.
* **Fix**: You no longer need to choose a colour if you go out with a Wild card.

Version 1.11 – 10 January 2016
------------------------------

* Added progressive rules, which allow Draw cards to be stacked on other cards of the same rank.

Version 1.10 – 20 January 2015
------------------------------

* Added hints.
* **Fix:** The AI will no longer continue playing a game that ended.

Version 1.9.1 – 5 January 2015
------------------------------

* **Fix:** When a player going out doesn't end the game, the AI and game timer are now set up correctly.

Version 1.9 – 11 December 2014
-----------------------------

* Added the `OutLimit` setting, to replace `AllOut`. The game will end when this many players go out. It can also be set to `none`.
* Added the `MidGameJoin` setting. If enabled, players will be allowed to join after the game starts.
* `d` no longer parses in the `!play` command as 'Wild Draw Four'.

Version 1.8 – 5 November 2014
-----------------------------

* Added the `!uwait` command. The `<plugin>.wait` permission is required to use it.
* The bot now PMs players who go out with the number of points they won, if the game hasn't ended.
* **Fix:** Reverse cards now work correctly when there are three players remaining, one of whom uses one to go out.

Version 1.7.3 – 21 October 2014
-------------------------------

* The bot now writes stats to another file in JSON format.

Version 1.7.2 – 19 October 2014
-------------------------------

* Added the `!ustart` command. The `<plugin>.start` permission is required to use it.
* **Fix:** The possible typo `pl wdg` will no longer set the colour to green. There must now be a space separating the colour parameter.

Version 1.7.1 – 13 October 2014
-------------------------------

* **Fix:** The bot no longer crashes when the deck runs out of cards.
* **Fix:** Winning by default no longer ends your streak.

Version 1.7 – 24 September 2014
-------------------------------

* Ported to C# for The Great Rewrite
* You can now choose a colour for a wild card with a single command. To do this, enter the colour on the same line, after the command, as: `!play Wild red`. The AI will do this, too.
* You must now choose a colour for a colourless Wild card, even if you decline to discard on it.
* Added the `!time` command
* Added more extended statistics
* The 'end of losing streak' message now refers correctly to the player's gender, if it is known.
* The bot no longer locks up on the AI's turn.
* **Fix:** If a Wild card appears as the initial up-card, Eldest Hand now chooses the colour instead of Youngest Hand.
* **Fix:** If a player goes out with a Draw card, the cards inflicted are now counted correctly for their score.
* **Fix:** All game-related commands and events are now synclocked, to prevent race conditions. This means no two can run simultaneously for the same game.