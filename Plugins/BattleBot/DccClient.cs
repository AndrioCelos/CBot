using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using AnIRC;
using CBot;

namespace BattleBot;
internal class DccClient : IrcClient {
	protected BattleBotPlugin plugin;
	protected internal readonly IrcUser Target;

	private TcpClient? client;
	private StreamWriter? writer;
	private byte[]? buffer;
	private readonly StringBuilder messageBuilder = new();

	internal DccClient(BattleBotPlugin plugin, IrcUser target) : base((plugin.ArenaConnection ?? throw new ArgumentException($"{nameof(plugin)}.{nameof(plugin.ArenaConnection)} may not be null")).Me) {
		this.plugin = plugin;
		this.Target = target;
		this.Address = "!" + plugin.Key + ".DCC";
	}

	public override void Connect(string host, int port) {
		// Connect to the DCC session.
		this.client = new TcpClient();
		this.client.Connect(host, port);
		this.writer = new StreamWriter(this.client.GetStream());
		this.buffer = new byte[512];
		this.messageBuilder.Clear();

		this.buffer = new byte[512];

		var readThread = new Thread(this.Read);
		readThread.Start();

		this.LastSpoke = DateTime.Now;
		this.State = IrcClientState.Online;
	}

	public override void DisconnectTcpClient() => this.client?.Close();

	public override void Send(IrcLine line) {
		if (this.State != IrcClientState.Online) return;
		ConsoleUtils.WriteLine("%cDKGRAY{0} %cDKRED<<%cDKGRAY {1}%r", this.Address, line.ToString().Replace("%", "%%"));

		if ((line.Message.Equals("PRIVMSG", StringComparison.OrdinalIgnoreCase) || line.Message.Equals("NOTICE", StringComparison.OrdinalIgnoreCase)) && (line.Parameters[0] == "#" ||
			IrcStringComparer.RFC1459.Equals("#Lobby", line.Parameters[0]) ||
			IrcStringComparer.RFC1459.Equals("#BattleRoom", line.Parameters[0]) ||
			IrcStringComparer.RFC1459.Equals(this.Target.Nickname, line.Parameters[0]))) {
			// Emulate a channel message or PM to the target by sending it over DCC.
			this.SendSub(line.Parameters[1].Replace("\u000F", "\u000F\u000312,99"));
		}
	}

	public void SendSub(string t) {
		if (this.writer == null) throw new InvalidOperationException("The client is not connected.");
		this.writer.Write(t);
		this.writer.Write("\r\n");
		this.writer.Flush();
	}

	private void Read() {
		if (this.client == null || this.buffer == null) throw new InvalidOperationException("The client is not connected.");
		int n;
		while (true) {
			try {
				n = this.client.GetStream().Read(this.buffer, 0, 512);
			} catch (IOException ex) {
				this.OnDisconnect(ex.Message);
				return;
			}
			if (n < 1) {
				this.OnDisconnect("The server closed the connection.");
				return;
			}
			for (int i = 0; i < n; ++i) {
				if (this.buffer[i] is (byte) '\r' or (byte) '\n') {
					if (this.messageBuilder.Length > 0) {
						try {
							this.DCCReceivedLine(this.messageBuilder.ToString());
						} catch (Exception ex) {
							this.plugin.LogError("DCCClient.ReceivedLine", ex);
						}
						this.messageBuilder.Clear();
					}
				} else {
					this.messageBuilder.Append((char) this.buffer[i]);
				}
			}
		}
	}

	protected void DCCReceivedLine(string message) {
		Match match;
		match = Regex.Match(message, @"^\x034\[([^\]]*)\] <([^>]*)> \x0312(.*)");
		if (match.Success) {
			// A chat message
			this.ReceivedLine(":" + match.Groups[2].Value + "!*@* PRIVMSG #" + match.Groups[1].Value + " :" + match.Groups[3].Value);
		} else {
			match = Regex.Match(message, @"^\x0313\*\x034\[([^\]]*)\] [^ ]* \x0312 (.*)\x0313\*$");
			if (match.Success) {
				// A chat action
				this.ReceivedLine(":" + match.Groups[2].Value + "!*@* PRIVMSG #" + match.Groups[1].Value + " :\u0001ACTION " + match.Groups[3].Value + "\u0001");
			} else {
				this.ReceivedLine(":" + this.Target.ToString() + " PRIVMSG " + this.Me.Nickname + " :" + message);
			}
		}
	}

	private void OnDisconnect(string reason) {
		this.plugin.LoggedIn = null;
		this.client = null;
		this.messageBuilder.Clear();
		this.plugin.WriteLine(1, 4, "DCC connection closed: {0}", reason);
		for (int i = 1; i < this.plugin.Bot.Clients.Count; ++i) {
			if (this.plugin.Bot.Clients[i].Client == this) {
				this.plugin.Bot.Clients.RemoveAt(i);
				break;
			}
		}
	}
}
