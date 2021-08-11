
using CBot;
using AnIRC;

namespace IdentifyPlugin {
	[ApiVersion(4, 0)]
	public class IdentifyPlugin : Plugin {
		public override string Name => "Identify";

		[Command(new string[] { "id", "identify", "login" }, 1, 2, "id [username] <password>", "Identifies you to me using a password")]
		public void CommandIdentify(object sender, CommandEventArgs e) {
			if (e.Target is IrcChannel) {  // A channel message. Identification should (obviously) be done privately.
				e.Whisper(Bot.Choose(Bot.Choose("Hey ", "") + e.Sender.Nickname + ", ", "") + Bot.Choose("I think " + Bot.Choose("that ", "")) + Bot.Choose("you should probably ", "you'll want to ") + Bot.Choose("run ", "use ", "invoke ") + "that command in a PM to me, " + Bot.Choose("not in a channel.", "rather than in a channel."), SayOptions.Capitalise);
				// TODO: Prompt the user to change their password.
			}

			if (!e.Client.Extensions.SupportsMonitor) {
				// Ensure that the user is on at least one channel with the bot. Otherwise it's a security hole.
				bool found = false;
				foreach (IrcChannel _channel in e.Client.Channels) {
					if (_channel.Users.Contains(e.Sender.Nickname)) {
						found = true;
						break;
					}
				}
				if (!found) {
					e.Whisper(Bot.Choose("You need to ", "You must ") + "be in " + Bot.Choose("at least one ", "a ") + "channel with me to identify yourself" + Bot.Choose(", " + e.Sender.Nickname, "") + ".");
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

			if (Bot.Identify(e.Sender, username, password, out id, out message)) {
				if (e.Client.Extensions.SupportsMonitor)
					e.Client.MonitorList.Add(e.Sender.Nickname);
			}

			e.Whisper(message);
		}
	}
}
