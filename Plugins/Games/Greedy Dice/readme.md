Greedy Dice
===========

Greedy Dice is a simple game. There are many forms. This one uses two six-sided dice, each with two X faces and four scoring faces. Players take turns to roll the dice as many times as they dare to score the most points. But if a double X is rolled, they lose all points for the turn. The game lasts four turns; this will be configurable later.

The bot will idle in the channel until someone starts a game by entering `!djoin`. Then, other players may enter with the same command before the game starts.

If no one else joins, the bot will.

Commands
--------

* `!djoin`
* `!dquit` — if the game has already started, you can forfeit and leave.
* `!droll` — on your turn, you may use this command to roll the dice.
* `!dpass` — use this to end your turn and keep your points.

Admin commands
--------------

* `!dset <setting> [value]` — changes settings.

Configuration
-------------

The following settings can be configured:

* `ai` — if 'true', the bot can join games.

* `entrytime` — the number of seconds players have to join the game. Default is 30.
* `turntime` — the number of seconds players have to take their turn. Default is 90. If 0, the time limit is disabled.

Scoring
-------

Each time you roll the dice, you get points for the faces showing:

* <span style="color: magenta; background-color: purple; width: 3ch; text-align: center;">♥</span>   50 points
* <span style="color: lime; background-color: green; width: 3ch; text-align: center;">*</span> 50 points
* <span style="color: red; background-color: maroon; width: 3ch; text-align: center;">**</span> 100 points
* <span style="color: skyblue; background-color: navy; width: 3ch; text-align: center;">***</span> 150 points

Doubles (the same face other than X showing on both dice) give you double points.

Time limit
----------

There is a time limit for players' turns (which can be disabled; see above). If a player doesn't do something on their turn within the time limit, the next player *has the option* to 'jump in'. If they do so, the idler loses their turn. The exception to this is the case where the last player times out on the last turn; in this case, the game just ends.

Anyone who times out twice in a row automatically forfeits the game.

Anyone who leaves the channel before the game starts is automatically removed without penalty. Anyone playing who leaves the channel *during* the game has a minimum of 60 seconds to get back in. If they don't, they will immediately be removed when their time limit runs out.

If everyone times out, the game ends there.

Permissions
-----------

* `<plugin>.set` — enables the `!dset` command to set global settings.
