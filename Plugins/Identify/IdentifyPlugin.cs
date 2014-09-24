using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CBot;
using IRC;

namespace IdentifyPlugin {
    [APIVersion(3, 0)]
    public class IdentifyPlugin : Plugin {
        public override string Name {
            get {
                return "Identify";
            }
        }

        [Command(new string[] { "id", "identify", "login" }, 1, 2, "id [username] <password>", "Identifies you to me using a password")]
        public void CommandIdentify(object sender, CommandEventArgs e) {
            if (e.Channel.StartsWith("#")) {  // A channel message. Identification should (obviously) be done privately.
                this.Say(e.Connection, e.Sender.Nickname, Bot.Choose(Bot.Choose("Hey ", "") + e.Sender.Nickname + ", ", "") + Bot.Choose("I think " + Bot.Choose("that ", "")) + Bot.Choose("you should probably ", "you'll want to ") + Bot.Choose("run ", "use ", "invoke ") + "that command in a PM to me, " + Bot.Choose("not in a channel.", "rather than in a channel."), SayOptions.Capitalise);
                // TODO: Prompt the user to change their password.
            }

            if (!e.Connection.SupportsWatch) {
                // Ensure that the user is on at least one channel with the bot. Otherwise it's a security hole.
                bool found = false;
                foreach (Channel _channel in e.Connection.Channels) {
                    if (_channel.Users.Contains(e.Sender.Nickname)) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    this.Say(e.Connection, e.Sender.Nickname, Bot.Choose("You need to ", "You must ") + "be in " + Bot.Choose("at least one ", "a ") + "channel with me to identify yourself" + Bot.Choose(", " + e.Sender.Nickname, "") + ".");
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

            if (Bot.Identify(e.Connection.Address + "/" + e.Sender.Nickname, username, password, out id, out message)) {
                if (e.Connection.SupportsWatch) e.Connection.Send("WATCH +{0}", e.Sender.Nickname);
            }

            this.Say(e.Connection, e.Sender.Nickname, message);
        }
    }
}
