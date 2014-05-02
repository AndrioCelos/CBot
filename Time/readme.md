Time
====

This plugin allows for specification of times in different time zones.

Commands
--------

* `!time <time> [in <zone>] [to <zone>]`

Up to one of the zones need not be specfied; if they're not, the bot will try to use a CTCP TIME request and use your local time that way.

`<time>` can consist of a date and/or day of the week, and time of day.

`<zone>` can be specified by name, abbreviation or offset (e.g. +10:00).

* `!reloadzones` — reloads the time zone database from `timezones.csv`.

Permissions
-----------

* `<plugin>.reload` — enabled the `!reloadzones` command.