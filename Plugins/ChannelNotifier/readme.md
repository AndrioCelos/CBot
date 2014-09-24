Channel Notifier plugin
=======================

This plugin listens for messages in its channel list and private messages, and relays them to another target list in its config file.
The target can be an individual user. If so, they'll only hear the messages if they have the `<key>.receive` permssion.
It will not relay private messages that start with a `!`.
