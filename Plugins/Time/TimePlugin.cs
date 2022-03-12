using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using CBot;
using AnIRC;

using static System.StringComparison;

namespace Time {
	[ApiVersion(4, 0)]
	public class TimePlugin : Plugin {
		public override string Name => "Time zone calculator";

		private Dictionary<string, Request> requests;
		private Dictionary<string, TimeSpan> timeZones;
		private Dictionary<string, Tuple<string, TimeSpan>> timeZoneAbbrevations;

		public override void Initialize() {
			requests = new Dictionary<string, Request>(IrcStringComparer.RFC1459);
			timeZones = new Dictionary<string, TimeSpan>(StringComparer.InvariantCultureIgnoreCase);
			timeZoneAbbrevations = new Dictionary<string, Tuple<string, TimeSpan>>(StringComparer.InvariantCultureIgnoreCase);

			this.LoadTimeZones();
		}

		public void LoadTimeZones() {
			int lineNumber = 0;
			if (!File.Exists("timezones.csv")) throw new FileNotFoundException("timezones.csv is missing.");

			using (StreamReader reader = new StreamReader("timezones.csv")) {
				while (!reader.EndOfStream) {
					++lineNumber;
					var s = reader.ReadLine();
					if (string.IsNullOrWhiteSpace(s)) continue;

					var fields = s.Split(',');
					if (fields.Length != 3) throw new FormatException("timezones.csv is invalid: wrong number of columns on line " + lineNumber);

					try {
						var offset = TimeSpan.Parse(fields[2].TrimStart('+'));
						if (string.IsNullOrWhiteSpace(fields[0]))  // which indicates no abbreviation
							fields[0] = fields[1];
						else
							timeZones.Add(fields[0], offset);
						timeZoneAbbrevations.Add(fields[1], new Tuple<string, TimeSpan>(fields[0], offset));
					} catch (FormatException ex) {
						throw new FormatException("timezones.csv is invalid: can't parse line " + lineNumber + ": " + ex.Message, ex);
					}
				}
			}
		}

		public override bool OnPrivateNotice(object? sender, PrivateMessageEventArgs e) {
			if (e.Message.StartsWith("\u0001") && e.Message.EndsWith("\u0001")) {
				if (e.Message.StartsWith("\u0001TIME ")) {
					lock (requests) {
						string key = ((IrcClient) sender).NetworkName + "/" + e.Sender.Nickname;
						Request request;
						if (requests.TryGetValue(key, out request)) {
							var timeString = e.Message.Substring(6, e.Message.Length - 7);
							DateTime time;
							if (TryParseCTCPTime(timeString, out time)) {
								int minutesDifference = (int) Math.Round((time - DateTime.Now).TotalMinutes / 15) * 15;
								var UTCOffset = DateTimeOffset.Now.Offset + TimeSpan.FromMinutes(minutesDifference);

								if (request.Zone == null)
									DoConversion((IrcClient) sender, e.Sender, request.Channel, request.Time, "your time", UTCOffset, request.ZoneName, request.TargetZone.Value);
								else
									DoConversion((IrcClient) sender, e.Sender, request.Channel, request.Time, request.ZoneName, request.Zone.Value, "your time", UTCOffset);
							} else {
								Bot.Say((IrcClient) sender, e.Sender.Nickname, "Could not parse your CTCP TIME reply.");
							}
							request.Timer.Dispose();
							requests.Remove(key);
						}
					}
				}
		   }

			return base.OnPrivateNotice(sender, e);
		}

		public static bool TryParseCTCPTime(string s, out DateTime result) {
			return TryParseCTCPTime(s, 0, out result);
		}
		public static bool TryParseCTCPTime(string s, DateTimeStyles style, out DateTime result) {
			// Try to move the year before the time component of the string.
			// Most CTCP time replies are of the form 'date, time, year' which .NET doesn't like.
			Match m = Regex.Match(s, @"(?<=\s)\d{1,2}:\d{1,2}\b");
			if (m.Success) {
				Match m2 = Regex.Match(s, @"\b\d{4}\b");
				if (m2.Success && m2.Index > m.Index) {
					s = s.Substring(0, m.Index) + m2.Value + " " + s.Substring(m.Index, m2.Index - m.Index) + s.Substring(m2.Index + m2.Length);
				}
			}
			return DateTime.TryParse(s, null, style, out result);
		}

		public static bool TryParseUserTime(string s, out DateTime result) {
			DateTime time;
			if (TryParseCTCPTime(s, DateTimeStyles.NoCurrentDateDefault, out time)) {
				if (time.Year == 1)
					// If they didn't specify a date, we add our own code.
					result = new DateTime(999, 1, 3, time.Hour, time.Minute, time.Second, time.Millisecond);
				else
					result = time;
				return true;
			}

			// If they specified a day of the week but no date, handle it specially.
			DateTimeFormatInfo format = DateTimeFormatInfo.CurrentInfo;
			s = s.TrimStart();
			DayOfWeek day;
			for (day = DayOfWeek.Sunday; day <= DayOfWeek.Saturday; ++day) {
				if (s.StartsWith(format.GetDayName(day))) {
					s = s.Substring(format.GetDayName(day).Length);
					break;
				}
				if (s.StartsWith(format.GetAbbreviatedDayName(day))) {
					s = s.Substring(format.GetAbbreviatedDayName(day).Length);
					break;
				}
			}
			if ((int) day == 7) {
				result = default(DateTime);
				return false;
			}

			if (!TryParseCTCPTime(s, DateTimeStyles.NoCurrentDateDefault, out time) || time.Year != 1) {
				result = default(DateTime);
				return false;
			}
			result = new DateTime(999, 1, 6 + (int) day, time.Hour, time.Minute, time.Second, time.Millisecond);
			return true;
		}

		[Command(new string[] { "time", "timezone" }, 1, 1, "time [\u001Fzone\u001F] \u0002or\u0002 time \u001Ftime\u001F [in \u001Fzone\u001F] [to \u001Fzone\u001F]", "Shows the time, or converts a time between zones.")]
		public void CommandTime(object? sender, CommandEventArgs e) {
			string key = e.Client.NetworkName + "/" + e.Sender.Nickname;
			Request request;
			if (requests.TryGetValue(key, out request)) {
				e.Whisper("I already have a pending request from you.");
				return;
			}

			// Parse the message.
			string timeString, zoneString, targetZoneString;
			var fields = e.Parameters[0].Split(new string[] { " in " }, 2, StringSplitOptions.None);
			if (fields.Length == 2) {
				timeString = fields[0];
				zoneString = fields[1];
			} else {
				timeString = fields[0];
				zoneString = null;
			}

			fields = (zoneString ?? timeString).Split(new string[] { " to " }, 2, StringSplitOptions.None);
			DateTime? time; TimeSpan? zone; TimeSpan? targetZone;

			if (fields.Length == 2) {
				if (zoneString == null) timeString = fields[0];
				else zoneString = fields[0];
				targetZoneString = fields[1];
			} else {
				targetZoneString = null;
			}

			if (targetZoneString == null && zoneString == null) {
				time = null;
				zone = null;
				targetZone = GetOffset(timeString, out targetZoneString);
				if (targetZone == null) {
					e.Whisper($"I do not recognize the time zone '{targetZoneString}'.");
					return;
				}

				DoConversion(e.Client, e.Sender, e.Target.Target, null, null, TimeSpan.Zero, targetZoneString, targetZone.Value);
			} else {
				DateTime time2;
				if (!TryParseUserTime(timeString, out time2)) {
					e.Whisper("I could not parse that time.");
					return;
				}
				time = time2;
				if (zoneString == null || zoneString.StartsWith("local", InvariantCultureIgnoreCase)) zone = null;
				else {
					zone = GetOffset(zoneString, out zoneString);
					if (zone == null) {
						e.Whisper($"I do not recognize the time zone '{zoneString}'.");
						return;
					}
				}
				if (targetZoneString == null || targetZoneString.StartsWith("local", InvariantCultureIgnoreCase)) targetZone = null;
				else {
					targetZone = GetOffset(targetZoneString, out targetZoneString);
					if (targetZone == null) {
						e.Whisper($"I do not recognize the time zone '{targetZoneString}'.");
						return;
					}
				}

				// Unless both zones are specified, we need to do a CTCP TIME.
				if (zone == null || targetZone == null) {
					request = new Request(e.Client, e.Target.Target, e.Sender, time, zone, targetZone, zoneString ?? targetZoneString);
					request.Timeout += Request_Timeout;
					e.Sender.Ctcp("TIME");
					requests.Add(key, request);
					request.Start();
				} else {
					DoConversion(e.Client, e.Sender, e.Target.Target, time, zoneString, zone.Value, targetZoneString, targetZone.Value);
				}
			}

		}

		private TimeSpan? GetOffset(string zoneString, out string name) {
			Match m = Regex.Match(zoneString, @"^\s*(?:((?:GMT|UTC)\s*$)|(?:(?:GMT|UTC)\s*)?([+-])\s*(?:(\d\d?)(?::(\d\d?)(?::(\d\d?))?)?|(\d\d)(?:(\d\d)(\d\d)?)?)\s*)$", RegexOptions.IgnoreCase);
			// Tame the beast: https://www.debuggex.com/r/PdXgjokCw9M2l3xe/1
			if (m.Success) {
				if (m.Groups[1].Success) {
					name = "UTC";
					return TimeSpan.Zero;
				}

				TimeSpan timeSpan;
				if (m.Groups[3].Success) {
					timeSpan = new TimeSpan(
						int.Parse(m.Groups[3].Value),
						m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0,
						m.Groups[5].Success ? int.Parse(m.Groups[5].Value) : 0
					);
				} else {
					timeSpan = new TimeSpan(
						int.Parse(m.Groups[6].Value),
						m.Groups[7].Success ? int.Parse(m.Groups[7].Value) : 0,
						m.Groups[8].Success ? int.Parse(m.Groups[8].Value) : 0
					);
				}
				if (m.Groups[2].Value == "-") timeSpan = -timeSpan;

				name = timeSpan.ToString(timeSpan >= TimeSpan.Zero ? @"\+hhmm" : @"\-hhmm");
				return timeSpan;
			}

			// Find the time zone in the database.
			TimeSpan offset;
			zoneString = zoneString.Trim();
			if (timeZones.TryGetValue(zoneString, out offset)) {
				name = zoneString.ToUpperInvariant();
				return offset;
			}
			Tuple<string, TimeSpan> zone;
			if (timeZoneAbbrevations.TryGetValue(zoneString, out zone)) {
				name = zone.Item1;
				return offset;
			}
			name = zoneString;
			return null;
		}

		private void Request_Timeout(object? sender, System.Timers.ElapsedEventArgs e) {
			var request = requests.Values.FirstOrDefault(request2 => request2.Timer == sender);
			if (request == null) return;
			Bot.Say(request.Connection, request.Sender.Nickname, "Didn't receive a CTCP TIME reply from you.");
		}

		private void DoConversion(IrcClient client, IrcUser sender, string target, DateTime? time, string zoneName, TimeSpan zoneOffset, string targetZoneName, TimeSpan targetZoneOffset) {
			var newTime = new DateTimeOffset(time ?? DateTime.UtcNow, zoneOffset).ToOffset(targetZoneOffset);

			if (time == null)
				Bot.Say(client, target, $"The time now is \u0002{GetTimeString(newTime.DateTime)} {targetZoneName}\u0002.");
			else
				Bot.Say(client, target, $"\u0002{GetTimeString(time.Value)} {zoneName}\u0002 equals \u0002{GetTimeString(newTime.DateTime)} {targetZoneName}\u0002.");
		}

		public static string GetTimeString(DateTime time) {
			if (time.Year == 999 && time.Month == 1) {
				// A date in January 999 is used for when the user didn't specify a date, and only a time, or only a day of the week.
				switch (time.Day) {
					case  2:
						return time.TimeOfDay.ToString() + " yesterday";
					case  3:
						return time.TimeOfDay.ToString();
					case  4:
						return time.TimeOfDay.ToString() + " tomorrow";
					case  5:
					case 12:
						return time.TimeOfDay.ToString() + " on Saturday";
					case  6:
					case 13:
						return time.TimeOfDay.ToString() + " on Sunday";
					case  7:
						return time.TimeOfDay.ToString() + " on Monday";
					case  8:
						return time.TimeOfDay.ToString() + " on Tuesday";
					case  9:
						return time.TimeOfDay.ToString() + " on Wednesday";
					case 10:
						return time.TimeOfDay.ToString() + " on Thursday";
					case 11:
						return time.TimeOfDay.ToString() + " on Friday";
				}
			}
			return time.ToString();
		}

	}
}
