Bash Quotes
===========

This plugin spits a random quote from bash.org in all channels assigned to it every minute. It's designed to replace another bot that went missing, and did most of the same thing.
The quotes are retrieved by downloading the page from http://bash.org/?random approximately every 50 minutes, and parsing it using a regular expression. When the end of the file nears, the plugin will download a new page in advance. It will spit the error message in the channels if this fails.
