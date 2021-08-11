using System;
using System.Text.RegularExpressions;

namespace BattleBot {
	public struct ArenaVersion : IEquatable<ArenaVersion>, IComparable<ArenaVersion> {
		public int Major;
		public int Minor;
		public int Revision;
		public DateTime BetaDate;

		public ArenaVersion(int major, int minor) : this(major, minor, 0, default) { }
		public ArenaVersion(int major, int minor, DateTime betaDate) : this(major, minor, 0, betaDate) { }
		public ArenaVersion(int major, int minor, int revision) : this(major, minor, revision, default) { }
		public ArenaVersion(int major, int minor, int revision, DateTime betaDate) {
			this.Major = major;
			this.Minor = minor;
			this.Revision = revision;
			this.BetaDate = betaDate;
		}

		public static ArenaVersion Empty => new();
		public bool IsEmpty => this.Major == 0 && this.Minor == 0 && this.Revision == 0 && this.BetaDate == default;
		public bool IsBeta => this.BetaDate != default;

		public static ArenaVersion Parse(string s) {
			var m = Regex.Match(s, @"(\d+)\.(\d+)(?:\.(\d+))?(?:beta_(\d\d)(\d\d)(\d\d))?");
			return m.Success
				? (new(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0,
					m.Groups[4].Success ? new(int.Parse(m.Groups[6].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value)) : default))
				: throw new FormatException();
		}

		public override bool Equals(object? other) => other is ArenaVersion version && this == version;
		public bool Equals(ArenaVersion other) => this == other;
		public int CompareTo(ArenaVersion other) {
			int result;
			return (result = this.Major.CompareTo(other.Major)) != 0 ? result :
				(result = this.Minor.CompareTo(other.Minor)) != 0 ? result :
				(result = this.Revision.CompareTo(other.Revision)) != 0 ? result :
				this.IsBeta ? (other.IsBeta ? this.BetaDate.CompareTo(other.BetaDate) : -1) :
				other.IsBeta ? 0 : 1;
		}

		public override int GetHashCode() => this.Major << 10 ^ this.Minor << 5 ^ this.Revision ^ this.BetaDate.GetHashCode();

		public static bool operator ==(ArenaVersion v1, ArenaVersion v2) => v1.Major == v2.Major && v1.Minor == v2.Minor && v1.Revision == v2.Revision && v1.BetaDate == v2.BetaDate;
		public static bool operator !=(ArenaVersion v1, ArenaVersion v2) => v1.Major != v2.Major || v1.Minor != v2.Minor || v1.Revision != v2.Revision || v1.BetaDate != v2.BetaDate;

		public static bool operator <(ArenaVersion v1, ArenaVersion v2) =>
			v1.Major < v2.Major ||
				(v1.Major <= v2.Major && (v1.Minor < v2.Minor ||
					(v1.Minor <= v2.Minor && (v1.Revision < v2.Revision ||
						(v1.Revision <= v2.Revision && v1.IsBeta && (!v2.IsBeta || v1.BetaDate < v2.BetaDate))))));
		public static bool operator >(ArenaVersion v1, ArenaVersion v2) =>
			v1.Major > v2.Major ||
				(v1.Major >= v2.Major && (v1.Minor > v2.Minor ||
					(v1.Minor >= v2.Minor && (v1.Revision > v2.Revision ||
						(v1.Revision >= v2.Revision && v2.IsBeta && (!v1.IsBeta || v1.BetaDate > v2.BetaDate))))));
		public static bool operator <=(ArenaVersion v1, ArenaVersion v2) =>
			v1.Major < v2.Major ||
				(v1.Major <= v2.Major && (v1.Minor < v2.Minor ||
					(v1.Minor <= v2.Minor && (v1.Revision < v2.Revision ||
						(v1.Revision <= v2.Revision && (!v2.IsBeta || (v1.IsBeta && v1.BetaDate < v2.BetaDate)))))));

		public static bool operator >=(ArenaVersion v1, ArenaVersion v2) =>
			v1.Major > v2.Major ||
				(v1.Major >= v2.Major && (v1.Minor > v2.Minor ||
					(v1.Minor >= v2.Minor && (v1.Revision > v2.Revision ||
						(v1.Revision >= v2.Revision && (!v1.IsBeta || (v2.IsBeta && v1.BetaDate > v2.BetaDate)))))));

		public override string ToString() {
			return this.Major.ToString() +
				"." + this.Minor.ToString() +
				(this.Revision != 0 ? "." + this.Revision.ToString() : "") +
				(this.BetaDate != default ? "beta_" + this.BetaDate.ToString("MMddyy") : "");
		}
	}
}
