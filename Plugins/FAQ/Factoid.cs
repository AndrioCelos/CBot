using System;
using System.Collections.Generic;

namespace FAQ {
	public class Factoid {
		public string Data;
		public List<string> Expressions;
		public Dictionary<string, Queue<DateTime>> HitTimes;
		public int RateLimitCount;
		public int RateLimitTime;
		public bool HideLabel;
		public bool Hidden;
		public bool NoticeOnJoin;

		public Factoid() {
			this.Expressions = new List<string>();
			this.HitTimes = new Dictionary<string, Queue<DateTime>>(StringComparer.InvariantCultureIgnoreCase);
			this.RateLimitCount = 0;
			this.RateLimitTime = 120;
			this.NoticeOnJoin = true;
		}
	}
}
