Greedy Dice
===========

Greedy Dice is a simple game played with two dice. Each die has four scoring faces and two 'X' faces. The object is to score as much as you dare and then pass, without getting a double 'X'.

The bot will idle in the channel until someone starts a game by entering `!djoin`. Then, other players may enter with the same command before the game starts.

If no one else joins, the bot will.

The game is played in turns. In a player's turn, they roll the dice as many times as they dare, then pass. A double 'X' loses all points for that turn and ends that turn, unless it's the first roll, in which case it doesn't count.

Whoever has the most points after four turns is the winner. Stats aren't in yet.

Commands
========

* `!djoin`
* `!droll` — rolls the dice.
* `!dpass` — ends your turn.

That's it.

Admin commands
==============

* `!dset ai <on|off>` — turns computer players on or off.

Scoring
=======

You earn points for each roll depending on what faces come up:

* **O** — 50 points
* **I** — 50 points
* **Flower** — 100 points
* **Eye** — 150 points
* **X** — 0 points

A double (other than 'X') earns double points.
