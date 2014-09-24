UNO
===

It's the classic card game on IRC. The object of the game is to be the first to get rid of all your cards.
You may discard a card of the same colour, number or action as the up-card. (It then becomes the next up-card.)

The bot will idle in the channel until someone starts a game by entering `!ujoin`. Then, other players may enter with the same command before the game starts.

If no one else joins, the bot will.

In a player's turn, they may either play a card, or draw a card. If they draw, they have the option to immediately play that card (if they can), or pass.

Commands
--------

Most commands from Marky's Color UNO work here too, for veterans of that bot (and they're easier to type!)

* `!ujoin`
* `!uquit` — if the game has already started, you can forfeit and leave.
* `!aijoin` — makes the bot enter the game.
* `!play <card>` or `pl <card>` — <card> can be in full ('Red 7') or abbreviated ('r7'). Action cards are '\*r'; '\*s'; '\*d' or '\*dt'; 'w'; 'wd'. If you play a Wild card, you can choose a colour in the one command with `!play Wild red`.
* `!draw` or `dr`
* `!pass` or `pa` — if you can't play the card you drew.
* `!colour <colour>` or `co <r|y|g|b>` — to choose a colour for a Wild card. Simply announcing the colour to the channel works too.
* `!challenge` — challenges the legality of a Wild Draw Four played on you.

* `!cards` or `ca` — shows you your hand.
* `!upcard` or `cd` — shows you the up-card.
* `!count` or `ct` — shows you the number of cards each player holds.
* `!turn` or `tu` — shows whose turn it is.
* `!time` or `ti` — shows how long the game has lasted.

* `!ustats [player] [current|last|alltime]` — shows a player's stats.
* `!uscore [player]` — shows a player's score on the leaderboard.
* `!utop [top|nearme] [current|last|alltime] [total|challenge|wins|plays]` — shows the leaderboard.

Admin commands
--------------

* `!ustop` — ends the game without a resolution.
* `!uset <setting> [value]` — changes settings.

Configuration
-------------

The following settings can be configured:

* `ai` — if 'true', the bot can join games.
* `allout` — if 'true', the game will continue until one player remains. If 'false', the game will end as soon as someone goes out, as per the official rules.
* `wilddrawfour` — if 'bluffoff', Wild Draw Four cards cannot be played if you hold a matching colour. If 'bluffon', Wild Draw Four cards can be played and challenged. If 'free', the Wild Draw Four rule is void entirely.
* `showhandonchallenge` — if 'true' (default), you'll see your attacker's hand when you challenge their Wild Draw Four, as per the official rules.

* `entryperiod` — the number of seconds players have to join the game. Default is 30.
* `turntime` — the number of seconds players have to take their turn. Default is 90. If 0, the time limit is disabled.

* `victorybonus' — if 'on' (default), points are awarded to the winner/s of each game.
* `victorybonuslastplace` — if 'off' (default), players who don't go out don't get this bonus.
* `victorybonusrepeat` — if 'on', the lowest victory bonus is awarded to everyone who goes out. If 'off', players can go out but still not get a victory bonus.
* `handbonus` — if 'on' (default), points are awarded to players who go out for cards their opponents still hold.
* `participationbonus` — the number of points are awarded to everyone who sees the game through to completion. Default is 0.
* `quitpenalty` — players who leave the game lose this many points. Default is 0.
* `victorybonusvalue` — a comma-separated list of numbers of points to award to players who go out. Default is 30, 10, 5: i.e. 30 points for first place, 10 for second and 5 for third. Under the default rules, only the first bonus will actually be awarded.

The following settings are per-player, and thus anyone can use them:

* `highlight` — if 'on', you will be alerted when a game starts.
* `autosort` — if 'colour', your cards will be sorted by colour, then face value. If 'rank', your cards will be sorted by face value, then colour. If 'off', your cards will not be sorted.
* `allowduelbot` — if this is set to 'no', the bot won't enter a two-player game with you.

Action cards
------------

These have special powers when played.

* **Reverse**: reverses the turn order. The player before you goes next. With two players, it acts like a Skip instead. If drawn as the initial up-card, Eldest Hand goes last instead of first.
* **Skip**: skips the next player. If drawn as the initial up-card, Eldest Hand is skipped.
* **Draw Two**: the next player must draw two cards in addition to being skipped.
* **Wild**: can be played at any time, and allows you to choose a colour for it. If drawn as the initial up-card, Eldest Hand picks the colour.
* **Wild Draw Four**: same as a Wild, but the next player draws four cards and is skipped. Special rules apply to this card: you can't play it if you hold a card whose *colour* matches the up-card. If drawn as the initial up-card, it is returned to the deck and another card is picked.

Where 'Eldest Hand' is the first player to enter, who normally plays first.

If a Wild Draw Four is played on you (and this rule is enabled), you can challenge it by entering `!challenge`. If you choose not to challenge, `!draw` instead. If you do challenge, your attacker shows you their hand. If the Wild Draw Four was illegal (they have a matching colour), the attacker draws four cards and you avoid being skipped. If the Wild Draw Four was legal, you cop two penalty cards plus the normal four.

Scoring
-------

The hand bonus is the face values of all cards your opponents still hold, when you go out:

* **Number cards**: face value
* **Reverse, Skip, Draw Two**: 20 points
* **Wild, Wild Draw Four**: 50 points

If you went out by playing a Draw card, the penalty cards you inflict are counted, too.

The **challenge score** is the points a player has taken minus the points taken from the player's remaining cards by rivals who beat them.

The leaderboard resets every 14 days, at 8 AM UTC. The most recent previous leaderboard is archived, and the all-time leaderboard is untouched. When the reset happens, the top 3 players (by total points) are announced to the channel.

Note that it currently works on nicknames, so if you change nickname, you'll get a separate stats entry.

Time limit
----------

There is a time limit for players' turns (which can be disabled; see above). If a player doesn't do something on their turn within the time limit, the next player *has the option* to 'jump in'. If they do so, the idler loses their turn and cops one card (to prevent cheating by idling) if they haven't already drawn one. If they idled on a Wild Draw Four, they cop the usual four cards instead. If they idled after playing a Wild card, it becomes colourless, and the next player can play anything.

Anyone who times out twice in a row automatically forfeits the game.

Anyone who leaves the channel before the game starts is automatically removed without penalty. Anyone playing who leaves the channel *during* the game has a minimum of 60 seconds to get back in. If they don't, they will immediately be removed when their time limit runs out.

If everyone times out, the game ends without scoring.

Permissions
-----------

* `<plugin>.stop` — enables the `!ustop` command.
* `<plugin>.set` — enables the `!uset` command to set global settings.
