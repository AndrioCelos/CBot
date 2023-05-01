using System.Text;
using AnIRC;
using CBot;
using Newtonsoft.Json;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Queue;
[ApiVersion(4, 0)]
public class QueuePlugin : Plugin {
	public override string Name => "Queue";

	private readonly Dictionary<string, Queue> queues = new(StringComparer.InvariantCultureIgnoreCase);

	public override bool OnCapabilitiesAdded(object? sender, CapabilitiesAddedEventArgs e) {
		e.EnableIfSupported("twitch.tv/tags");
		return false;
	}

	public override void Initialize() {
		if (this.Bot.HttpServer is not null) {
			this.Bot.HttpServer.AddWebSocketService<QueueWebSocketBehavior>($"/{this.Key}/websocket", s => s.Module = this);
		}
	}

	public override void OnHttpRequest(HttpRequestEventArgs e) {
		if (e.Request.Url.AbsolutePath == $"/{this.Key}/queue") {
			if (e.Request.HttpMethod == "GET") {
				if (e.Request.QueryString["channel"] is string key) {
					if (!this.queues.TryGetValue(key, out var queue)) queue = new();

					e.Response.StatusCode = (int) HttpStatusCode.OK;
					using var writer = new JsonTextWriter(new StreamWriter(e.Response.OutputStream));
					new JsonSerializer().Serialize(writer, new { IsClosed = queue.IsClosed, Queue = queue });
				}
				// Otherwise return Not Found.
			} else {
				e.Response.StatusCode = (int) HttpStatusCode.MethodNotAllowed;
			}
		}
	}

	private void SendWebSocketUpdates(IrcMessageTarget channel, Queue queue, List<string>? mentionUsers = null) {
		var key = channel is IrcChannel ircChannel ? ircChannel.Name[1..] : channel.Target;
		this.SendWebSocketUpdates(key, queue, mentionUsers);
	}
	private void SendWebSocketUpdates(string channel, Queue queue, List<string>? mentionUsers = null) {
		if (this.Bot.HttpServer is not null) {
			var text = JsonConvert.SerializeObject(new { IsClosed = queue.IsClosed, Queue = queue, MentionUsers = mentionUsers });
			foreach (var session in this.Bot.HttpServer.WebSocketServices[$"/{this.Key}/websocket"].Sessions.Sessions) {
				if (session is QueueWebSocketBehavior behavior) {
					if (behavior.Channel is not null && behavior.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase))
						behavior.SendInternal(text);
				}
			}
		}
	}

	[Command(new[] { "q", "queue" }, 0, 2, "q <command>", "Alternate form for all queue commands.")]
	public void CommandQ(object? sender, CommandEventArgs e) {
		if (e.Parameters.Length == 0) {
			this.CommandHelp(sender, new(e.Client, e.Target, e.Sender, Array.Empty<string>()));
			return;
		}
		var parameters = new string[e.Parameters.Length - 1];
		Array.Copy(e.Parameters, 1, parameters, 0, parameters.Length);
		var e2 = new CommandEventArgs(e.Client, e.Target, e.Sender, parameters);
		switch (e.Parameters[0].ToLowerInvariant()) {
			case "help": this.CommandHelp(sender, e2); break;
			case "join": this.CommandJoin(sender, e2); break;
			case "leave": this.CommandLeave(sender, e2); break;
			case "pos": case "position": this.CommandPosition(sender, e2); break;
			case "remove": this.CommandRemove(sender, e2); break;
			case "next": this.CommandNext(sender, e2); break;
			case "peek": this.CommandPeek(sender, e2); break;
			case "clear": this.CommandClear(sender, e2); break;
			case "close": case "lock": this.CommandClose(sender, e2); break;
			case "open": case "unlock": this.CommandOpen(sender, e2); break;
		}
	}

	[Command(new[] { "qhelp" }, 0, 0, "qhelp", "Asks for help on queue commands.")]
	public void CommandHelp(object? sender, CommandEventArgs e) {
		var queue = this.GetOrCreateQueue(e.Target);
		if (queue.IsClosed)
			e.Reply("The queue is currently closed.");
		else
			e.Reply("Queue commands: !q join – join the queue. !q leave – leave the queue. !q peek [n] – see the next [n] people. !q pos – see your position in the queue.");
	}

	[Command(new[] { "qjoin" }, 0, 0, "qjoin", "Adds you to the queue.")]
	public void CommandJoin(object? sender, CommandEventArgs e) {
		var queue = this.GetOrCreateQueue(e.Target);
		int i;
		for (i = queue.Count - 1; i >= 0; --i) {
			if (e.Client.CaseMappingComparer.Equals(queue[i], GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender))) break;
		}
		if (i < 0) {
			if (queue.IsClosed && !UserIsModerator(e))
				e.Reply("The queue is currently closed.");
			else {
				queue.Add(GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender));
				e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, you've joined the queue at position {queue.Count}.");
				this.SendWebSocketUpdates(e.Target, queue);
			}
		} else
			e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, you're already in the queue at position {i + 1}.");
	}

	[Command(new[] { "qleave" }, 0, 0, "qleave", "Removes you from the queue.")]
	public void CommandLeave(object? sender, CommandEventArgs e) {
		var queue = this.GetOrCreateQueue(e.Target);
		int i;
		for (i = queue.Count - 1; i >= 0; --i) {
			if (e.Client.CaseMappingComparer.Equals(queue[i], GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender))) {
				queue.RemoveAt(i);
				e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, you've left the queue.");
				this.SendWebSocketUpdates(e.Target, queue);
				return;
			}
		}
		e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, you were not in the queue.");
	}

	[Command(new[] { "qposition" }, 0, 1, "qposition [user]", "Shows your position in the queue.")]
	public void CommandPosition(object? sender, CommandEventArgs e) {
		var queue = this.GetOrCreateQueue(e.Target);
		int i;
		for (i = queue.Count - 1; i >= 0; --i) {
			if (e.Client.CaseMappingComparer.Equals(queue[i], GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender))) break;
		}
		if (i < 0) {
			e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, you're not in the queue."
				+ (queue.IsClosed ? "" : " Use `!q join` to join the queue."));
		} else
			e.Reply($"@{GetDisplayName(e.Client.CurrentLine?.Tags, e.Sender)}, your position in the queue is {i + 1}.");
	}

	[Command(new[] { "qremove" }, 1, short.MaxValue, "qremove [user ...]", "Removes one or more specified users from the queue.")]
	public void CommandRemove(object? sender, CommandEventArgs e) {
		if (!CheckPermission(e.Target is IrcChannel channel && channel.Users.TryGetValue(e.Sender.Nickname, out var user) ? user : null, e.Client.CurrentLine?.Tags)) {
			e.Fail("Only moderators can do that.");
			return;
		}
		var usersToRemove = new HashSet<string>(e.Parameters.Select(s => s.TrimStart(new[] { '@', ',', ' ' })), e.Client.CaseMappingComparer);
		int removed = 0;
		var queue = this.GetOrCreateQueue(e.Target);
		int i;
		for (i = queue.Count - 1; i >= 0; --i) {
			if (usersToRemove.Contains(queue[i])) {
				queue.RemoveAt(i);
				removed++;
			}
		}
		if (removed == 0) {
			e.Reply("None of them were in the queue.");
		} else {
			if (removed == 1)
				e.Reply("Removed 1 user from the queue.");
			else
				e.Reply($"Removed {removed} users from the queue.");
			this.SendWebSocketUpdates(e.Target, queue);
		}
	}

	[Command(new[] { "qnext" }, 0, 1, "qnext [n]", "Removes and mentions the next n nicknames in the queue.")]
	public void CommandNext(object? sender, CommandEventArgs e) {
		if (!CheckPermission(e.Target is IrcChannel channel && channel.Users.TryGetValue(e.Sender.Nickname, out var user) ? user : null, e.Client.CurrentLine?.Tags)) {
			e.Fail("Only moderators can do that.");
			return;
		}
		var queue = this.GetOrCreateQueue(e.Target);
		if (queue.Count == 0) {
			e.Reply("The queue is currently empty.");
			return;
		}

		int n;
		if (e.Parameters.Length > 0) {
			if (!(int.TryParse(e.Parameters[0], out n) && n > 0)) {
				e.Fail("That is not a valid number.");
				return;
			}
			if (n > queue.Count) n = queue.Count;
		} else
			n = 1;

		var mentionUsers = new List<string>();
		var builder = new StringBuilder();
		builder.Append("You are up: ");
		bool any = false;
		while (n > 0) {
			if (!any) any = true;
			else builder.Append(", ");
			if (e.Client.NetworkName == "Twitch") builder.Append('@');
			mentionUsers.Add(queue[0]);
			builder.Append(queue[0]);
			queue.RemoveAt(0);
			--n;
		}
		e.Reply(builder.ToString());
		this.SendWebSocketUpdates(e.Target, queue, mentionUsers);
	}

	[Command(new[] { "qpeek" }, 0, 1, "qpeek [n]", "Mentions the next n nicknames in the queue without changing it.")]
	public void CommandPeek(object? sender, CommandEventArgs e) {
		var queue = this.GetOrCreateQueue(e.Target);
		if (queue.Count == 0) {
			e.Reply("The queue is currently empty.");
			return;
		}

		int n;
		if (e.Parameters.Length > 0) {
			if (!(int.TryParse(e.Parameters[0], out n) && n > 0)) {
				e.Fail("That is not a valid number.");
				return;
			}
			if (n > queue.Count) n = queue.Count;
		} else
			n = 1;

		var builder = new StringBuilder();
		builder.Append($"Next {n} in the queue: ");
		for (int i = 0; i < n; ++i) {
			if (i > 0) builder.Append(", ");
			//if (e.Client.NetworkName == "Twitch") builder.Append('@');
			builder.Append(queue[i]);
		}
		e.Reply(builder.ToString());
	}

	[Command(new[] { "qclear" }, 0, 0, "qclear", "Clears the queue.")]
	public void CommandClear(object? sender, CommandEventArgs e) {
		if (!CheckPermission(e.Target is IrcChannel channel && channel.Users.TryGetValue(e.Sender.Nickname, out var user) ? user : null, e.Client.CurrentLine?.Tags)) {
			e.Fail("Only moderators can do that.");
			return;
		}
		var queue = this.GetOrCreateQueue(e.Target);
		if (queue.Count == 0) {
			e.Reply("The queue is currently empty.");
			return;
		}

		queue.Clear();
		e.Reply("The queue has been cleared.");
		this.SendWebSocketUpdates(e.Target, queue);
	}

	[Command(new[] { "qclose", "qlock" }, 0, 0, "qclose", "Closes the queue, preventing new users from joining.")]
	public void CommandClose(object? sender, CommandEventArgs e) {
		if (!UserIsModerator(e)) {
			e.Fail("Only moderators can do that.");
			return;
		}
		var queue = this.GetOrCreateQueue(e.Target);
		if (queue.IsClosed)
			e.Reply("The queue was already closed.");
		else {
			queue.IsClosed = true;
			e.Reply("The queue is now closed.");
			this.SendWebSocketUpdates(e.Target, queue);
		}
	}

	[Command(new[] { "qopen", "qunlock" }, 0, 0, "qopen", "Opens the queue, allowing new users to join.")]
	public void CommandOpen(object? sender, CommandEventArgs e) {
		if (!CheckPermission(e.Target is IrcChannel channel && channel.Users.TryGetValue(e.Sender.Nickname, out var user) ? user : null, e.Client.CurrentLine?.Tags)) {
			e.Fail("Only moderators can do that.");
			return;
		}
		var queue = this.GetOrCreateQueue(e.Target);
		if (!queue.IsClosed)
			e.Reply("The queue was already open.");
		else {
			queue.IsClosed = false;
			e.Reply("The queue is now open.");
			this.SendWebSocketUpdates(e.Target, queue);
		}
	}

	private static bool UserIsModerator(CommandEventArgs e) => CheckPermission(e.Target is IrcChannel channel && channel.Users.TryGetValue(e.Sender.Nickname, out var user) ? user : null, e.Client.CurrentLine?.Tags);

	private static bool CheckPermission(IrcChannelUser? user, IReadOnlyDictionary<string, string>? tags) {
		if (user != null && user.Status >= ChannelStatus.Halfop) return true;
		if (tags == null || !tags.TryGetValue("badges", out var badges)) return false;
		return badges.Split(',').Any(s => s.Split('/')[0] is "moderator" or "broadcaster");
	}

	private static string GetDisplayName(IReadOnlyDictionary<string, string>? tags, IrcUser user)
		=> tags != null && tags.TryGetValue("display-name", out var displayName) && displayName != ""
			? displayName
			: user.Nickname;

	public Queue GetOrCreateQueue(IrcMessageTarget domain) {
		//var key = $"{domain.Client.NetworkName}/{domain.Target}";
		var key = domain is IrcChannel ircChannel ? ircChannel.Name[1..] : domain.Target;
		if (!this.queues.TryGetValue(key, out var queue)) {
			queue = new Queue();
			this.queues[key] = queue;
		}
		return queue;
	}

	public Queue GetQueue(string domain) {
		if (!this.queues.TryGetValue(domain, out var queue)) {
			queue = new Queue();
		}
		return queue;
	}
}
