using System;
using System.IO;

using CBot;

namespace Updater {
	[ApiVersion(4, 0)]
	public class UpdaterPlugin : Plugin {
		public override string Name => "Updater";

		public override void Initialize() {
			File.WriteAllText("CBot.pid", Environment.ProcessId.ToString());
			Console.CancelKeyPress += this.Console_CancelKeyPress;
			//AppDomain.CurrentDomain.ProcessExit += this.Interrupt;
		}

		public override void OnUnload() {
			File.Delete("CBot.pid");
			Console.CancelKeyPress -= this.Console_CancelKeyPress;
			//AppDomain.CurrentDomain.ProcessExit -= this.Interrupt;
		}

		private void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e) {
			// SIGINT
			this.Interrupt(sender, e);
		}

		private void Interrupt(object sender, EventArgs e) {
			Console.WriteLine("Received SIGINT. Saving plugins...");
			foreach (var plugin in this.Bot.Plugins)
				plugin.Obj.OnSave();
			Console.WriteLine("Disconnecting...");
			foreach (var client in this.Bot.Clients)
				if (client.Client.State > AnIRC.IrcClientState.Disconnected) {
					client.Client.Send("QUIT :Shutting down.");
					client.Client.Disconnect();
				}
			File.Delete("CBot.pid");
		}
	}
}
