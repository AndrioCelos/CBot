FAQ plugin
==========

This is a fairly simple concept, used all over IRC.
A factoid can be looked up by name, using the command:
	`? info`
where `info` is the name.

Actually, different 'contexts' can be set up, which can correspond to channels. Factoids must be assigned a context. For example, the above example may be known internally as `me/info`, where 'me' is the context name.
The idea is that the context name can be omitted in one of its channels. But not in wildcard channels. In fact, users and moderators alike may not even know it exists.

Regular expressions
-------------------

Factoids can also have regular expressions associated with them. The factoid will be shown if any message in the channel matches the regular expression. It's rate limited, by default, to 1 hit per 2 minutes.

The regular expression can be just a normal regular expression, which will match channel messages, but these can also be used:

* `MSG:[hostmask]:[channel]:[message]`
* `ACTION:[hostmask]:[channel]:[message]`
* `JOIN:[hostmask]:[channel]:[message]`
* `PART:[hostmask]:[channel]:[message]`
* `QUIT:[hostmask]:[channel]:[message]`
* `KICK:[hostmask]:[channel]:[message]` — `message` is in the form `target:message`
* `EXIT:[hostmask]:[channel]:[message]` — matches users leaving the channel by any means.
* `NICK:[hostmask]:[channel]:[message]` — `message` is the new nickname

All parameters are optional, and can be in any order, except `[message]`, which always comes last.

Configuration
-------------

You'll need to add the contexts. To do so, use `!contextadd <key> <channels>`. `<channels>` is in the same format as in `CBotPlugins.ini`.

To avoid conflicting with other bots, the ? shorthand forms can be disabled in certain channels. To do this, enter `!globalset noshortcutchans <channels>`.

Managing factoids
-----------------

The following commands exist:
Note that all of them accept '.' in place of a key parameter to refer to the most recently touched factoid.

* `!faqadd` or `?+ <key> <data>` — you can also append lines this way.
* `!faqdel` or `?- <key>`
* `!faqlist` or `?: [context]`
* `!faqedit` or `?= <key> [[+]line #] [text]` — prefix the number with '+' to insert a line; omit the text parameter to delete one.

* `!faqaliasadd` or `?@+ <key> <target>`
* `!faqaliasdel` or `?@- <key>`
* `!faqalaslist` or `?@: <target|context>`

* `!faqregexadd` or `?*+ <key> <regex>`
* `!faqregexlist` or `?*: <key>`
* `!faqregexedit` or `?*= <key> <number> <regex>`
* `!faqregexdel` or `?*- <key> <number>`

* `!faqset <key> RateLimitCount <number>`
* `!faqset <key> RateLimitTime <seconds>`
* `!faqset <key> Hidden true|false` — hidden factoids don't show up in a `?:` list unless the user has permission to see them.
* `!faqset <key> HideLabel true|false` — if true, the label won't be shown if the factoid's regular expression is hit.
* `!faqset <key> NoticeOnJoin true|false` — if true (default), a factoid that is shown to a user joining a channel will be NOTICEd to them instead of announced to the channel.

Permissions
-----------

The permission names are pretty self-explanatory:

* `<plugin>.add.<context>`
* `<plugin>.list.<context>`
* `<plugin>.listhidden.<context>` — shows hidden factoids.
* `<plugin>.delete.<context>`
* `<plugin>.edit.<context>`
* `<plugin>.faqset.<context>` — enables the `!faqset` command.
* `<plugin>.regex.<context>` — enables the `?*+`, `?*=` and `?*-` commands.
* `<plugin>.regexlist.<context>`

where `<plugin>` is the plugin key.

* `<plugin>.set` — enables the `!globalset` command.
