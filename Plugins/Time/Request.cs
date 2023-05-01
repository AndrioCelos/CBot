using System.Timers;
using AnIRC;
using Timer = System.Timers.Timer;

namespace Time;
public class Request {
	public DateTime RequestTime { get; }
	public Timer Timer { get; private set; }

	public IrcClient Connection { get; }
	public string Channel { get; }
	public IrcUser Sender { get; }

	public DateTime? Time { get; }
	public TimeSpan? Zone { get; }
	public TimeSpan? TargetZone { get; }
	public string ZoneName { get; }

	public event ElapsedEventHandler Timeout;

	public Request(IrcClient connection, string channel, IrcUser sender, DateTime? time, TimeSpan? zone, TimeSpan? targetZone, string zoneName) {
		this.Connection = connection;
		this.Channel = channel;
		this.Sender = sender;
		this.Time = time;
		this.Zone = zone;
		this.TargetZone = targetZone;
		this.ZoneName = zoneName;

		this.RequestTime = DateTime.Now;
	}

	~Request() {
		this.Timer?.Dispose();
	}

	public void Start() {
		this.Timer?.Dispose();
		this.Timer = new Timer(15000) { AutoReset = false };
		this.Timer.Elapsed += Timer_Elapsed;
		this.Timer.Start();
	}

	public void Stop() {

	}

	private void Timer_Elapsed(object? sender, ElapsedEventArgs e) {
		this.Timeout?.Invoke(sender, e);
	}
}
