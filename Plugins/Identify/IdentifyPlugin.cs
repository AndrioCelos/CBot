﻿
using CBot;
using IRC;

namespace IdentifyPlugin {
    [APIVersion(3, 2)]
    public class IdentifyPlugin : Plugin {
        public override string Name {
            get {
                return "Identify";
            }
        }

        [Command(new string[] { "id", "identify", "login" }, 1, 2, "id [username] <password>", "Identifies you to me using a password")]
        public void CommandIdentify(object sender, CommandEventArgs e) {
            if (e.Client.IsChannel(e.Channel)) {  // A channel message. Identification should (obviously) be done privately.
                Bot.Say(e.Client, e.Sender.Nickname, Bot.Choose(Bot.Choose("Hey ", "") + e.Sender.Nickname + ", ", "") + Bot.Choose("I think " + Bot.Choose("that ", "")) + Bot.Choose("you should probably ", "you'll want to ") + Bot.Choose("run ", "use ", "invoke ") + "that command in a PM to me, " + Bot.Choose("not in a channel.", "rather than in a channel."), SayOptions.Capitalise);
                // TODO: Prompt the user to change their password.
            }

            if (!e.Client.Extensions.SupportsWatch) {
                // Ensure that the user is on at least one channel with the bot. Otherwise it's a security hole.
                bool found = false;
                foreach (IRCChannel _channel in e.Client.Channels) {
                    if (_channel.Users.Contains(e.Sender.Nickname)) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    Bot.Say(e.Client, e.Sender.Nickname, Bot.Choose("You need to ", "You must ") + "be in " + Bot.Choose("at least one ", "a ") + "channel with me to identify yourself" + Bot.Choose(", " + e.Sender.Nickname, "") + ".");
                    return;
                }
            }

            // Identify.
            string username; string password; Identification id; string message;

            if (e.Parameters.Length == 1) {
                username = e.Sender.Nickname;
                password = e.Parameters[0];
            } else {
                username = e.Parameters[0];
                password = e.Parameters[1];
            }

            if (Bot.Identify(e.Client.NetworkName + "/" + e.Sender.Nickname, username, password, out id, out message)) {
                if (e.Client.Extensions.SupportsWatch) e.Client.Send("WATCH +{0}", e.Sender.Nickname);
            }

            Bot.Say(e.Client, e.Sender.Nickname, message);
        }
    }
}
