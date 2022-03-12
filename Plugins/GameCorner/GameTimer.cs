using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace GameCorner {
	/// <summary>Provides an extension of the <see cref="Timer"/> class.</summary>
	public class GameTimer<TGame> {
		public event EventHandler<GameTimerEventArgs<TGame>> Tick;

		public TGame Game { get; }

		protected Timer Timer { get; }
		protected Stopwatch Stopwatch { get; } = new Stopwatch();

		public GameTimer(TGame game) : this(game, TimeSpan.FromSeconds(2)) { }
		public GameTimer(TGame game, TimeSpan interval) {
			this.Game = game;
			this.interval = interval;
			this.Timer = new Timer(interval.TotalMilliseconds);
			this.Timer.Elapsed += this.Timer_Elapsed;
		}

		private void Timer_Elapsed(object sender, ElapsedEventArgs e) {
			this.Tick(this, new GameTimerEventArgs<TGame>(this.Game, e.SignalTime));
		}

		private TimeSpan interval;
		/// <summary>Returns or sets the interval of the timer. The timer is reset if this is set while the timer is running.</summary>
		public TimeSpan Interval {
			get => this.interval;
			set {
				if (value <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive.");
				this.interval = value;
				this.Timer.Interval = value.TotalMilliseconds;
				if (this.Timer.Enabled) this.Stopwatch.Restart();
				else this.Stopwatch.Reset();
			}
		}
		/// <summary>Returns or sets a value indicating whether the timer is running.</summary>
		public bool Enabled {
			get => this.Timer.Enabled;
			set { if (value) this.Start(); else this.Stop(); }
		}
		public bool AutoReset {
			get => this.Timer.AutoReset;
			set => this.Timer.AutoReset = value;
		}

		/// <summary>Starts the game timer.</summary>
		public void Start() {
			if (this.Timer.Enabled) this.Timer.Stop();
			this.Timer.Interval = this.interval.TotalMilliseconds;
			this.Timer.Start();
			this.Stopwatch.Restart();
		}

		public void Start(TimeSpan time) {
			if (time <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive.");
			this.interval = time;
			this.Timer.Interval = time.TotalMilliseconds;
			this.Timer.Start();
			this.Stopwatch.Restart();
		}

		public void Stop() {
			this.Timer.Stop();
			this.Stopwatch.Stop();
		}

		/// <summary>Delays the next timer tick by the specified interval.</summary>
		public void ExtendTimer(TimeSpan time) {
			try {
				this.Timer.Interval = this.Timer.Interval - Stopwatch.Elapsed.TotalMilliseconds + time.TotalMilliseconds;
			} catch (ArgumentException ex) {
				throw new ArgumentException("Interval to shorten by must be shorter than the remaining time.", ex);
			}
			if (this.Timer.Enabled) this.Stopwatch.Restart();
			else this.Stopwatch.Reset();
		}

		public TimeSpan TimeRemaining => TimeSpan.FromMilliseconds(this.Timer.Interval) - Stopwatch.Elapsed;
	}
}
