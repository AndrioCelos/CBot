using Newtonsoft.Json;
using WebSocketSharp.Server;

namespace Queue;

public class QueueWebSocketBehavior : WebSocketBehavior {
	public string? Channel { get; internal set; }
	internal QueuePlugin? Module { get; set; }

	protected override void OnOpen() {
		this.Channel = this.Context.QueryString["channel"];

		if (this.Module is not null && this.Channel is not null) {
			var queue = this.Module.GetQueue(this.Channel);
			this.Send(JsonConvert.SerializeObject(new { IsClosed = queue.IsClosed, Queue = queue }));
		}
	}

	internal void SendInternal(string text) => this.Send(text);
}
