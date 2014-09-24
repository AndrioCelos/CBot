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