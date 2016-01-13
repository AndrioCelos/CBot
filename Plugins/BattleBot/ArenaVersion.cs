using System;
using System.Text.RegularExpressions;

namespace BattleBot {
    public struct ArenaVersion {
        public int Major;
        public int Minor;
        public int Revision;
        public DateTime BetaDate;

        public ArenaVersion(string version) {
            Match m = Regex.Match(version, @"(\d+)\.(\d+)(\.(\d+))?(beta_(\d\d)(\d\d)(\d\d))?");
            if (!m.Success) throw new FormatException();
            this.Major = int.Parse(m.Groups[1].Value);
            this.Minor = int.Parse(m.Groups[2].Value);
            this.Revision = m.Groups[3].Success ? int.Parse(m.Groups[4].Value) : 0;
            if (m.Groups[5].Success)
                this.BetaDate = new DateTime(int.Parse(m.Groups[8].Value), int.Parse(m.Groups[6].Value), int.Parse(m.Groups[7].Value));
            else
                this.BetaDate = default(DateTime);
        }
        public ArenaVersion(int major, int minor) : this(major, minor, 0, default(DateTime)) { }
        public ArenaVersion(int major, int minor, DateTime betaDate) : this(major, minor, 0, betaDate) { }
        public ArenaVersion(int major, int minor, int revision) : this(major, minor, revision, default(DateTime)) { }
        public ArenaVersion(int major, int minor, int revision, DateTime betaDate) {
            this.Major = major;
            this.Minor = minor;
            this.Revision = revision;
            this.BetaDate = betaDate;
        }

        public static ArenaVersion Empty() {
            return new ArenaVersion(0, 0, 0, default(DateTime));
        }
        public bool IsEmpty() {
            return this.Major == 0 && this.Minor == 0 && this.Revision == 0 && this.BetaDate == default(DateTime);
        }

        public override bool Equals(object other) {
            if (other == null || !(other is ArenaVersion)) return false;
            return (this == (ArenaVersion) other);
        }

        public override int GetHashCode() {
            return this.Major << 10 ^ this.Minor << 5 ^ this.Revision ^ this.BetaDate.GetHashCode();
        }

        public static bool operator ==(ArenaVersion v1, ArenaVersion v2) {
            return v1.Major == v2.Major && v1.Minor == v2.Minor && v1.Revision == v2.Revision && v1.BetaDate == v2.BetaDate;
        }
        public static bool operator !=(ArenaVersion v1, ArenaVersion v2) {
            return v1.Major != v2.Major || v1.Minor != v2.Minor || v1.Revision != v2.Revision || v1.BetaDate != v2.BetaDate;
        }

        public static bool operator <(ArenaVersion v1, ArenaVersion v2) {
            if (v1.Major < v2.Major) return true;
            if (v1.Major > v2.Major) return false;
            if (v1.Minor < v2.Minor) return true;
            if (v1.Minor > v2.Minor) return false;
            if (v1.Revision < v2.Revision) return true;
            if (v1.Revision > v2.Revision) return false;
            if (v1.BetaDate == default(DateTime)) return false;
            if (v2.BetaDate == default(DateTime) && v1.BetaDate != default(DateTime)) return true;
            return v1.BetaDate < v2.BetaDate;
        }
        public static bool operator >(ArenaVersion v1, ArenaVersion v2) {
            if (v1.Major > v2.Major) return true;
            if (v1.Major < v2.Major) return false;
            if (v1.Minor > v2.Minor) return true;
            if (v1.Minor < v2.Minor) return false;
            if (v1.Revision > v2.Revision) return true;
            if (v1.Revision < v2.Revision) return false;
            if (v2.BetaDate == default(DateTime)) return false;
            if (v1.BetaDate == default(DateTime) && v2.BetaDate != default(DateTime)) return true;
            return v1.BetaDate > v2.BetaDate;
        }

        public static bool operator <=(ArenaVersion v1, ArenaVersion v2) {
            if (v1.Major < v2.Major) return true;
            if (v1.Major > v2.Major) return false;
            if (v1.Minor < v2.Minor) return true;
            if (v1.Minor > v2.Minor) return false;
            if (v1.Revision < v2.Revision) return true;
            if (v1.Revision > v2.Revision) return false;
            if (v2.BetaDate == default(DateTime)) return true;
            if (v1.BetaDate == default(DateTime) && v2.BetaDate != default(DateTime)) return false;
            return v1.BetaDate >= v2.BetaDate;
        }
        public static bool operator >=(ArenaVersion v1, ArenaVersion v2) {
            if (v1.Major > v2.Major) return true;
            if (v1.Major < v2.Major) return false;
            if (v1.Minor > v2.Minor) return true;
            if (v1.Minor < v2.Minor) return false;
            if (v1.Revision > v2.Revision) return true;
            if (v1.Revision < v2.Revision) return false;
            if (v1.BetaDate == default(DateTime)) return true;
            if (v2.BetaDate == default(DateTime) && v1.BetaDate != default(DateTime)) return false;
            return v1.BetaDate <= v2.BetaDate;
        }

        public override string ToString() {
            return this.Major.ToString() +
                "." + this.Minor.ToString() +
                (this.Revision != 0 ? "." + this.Revision.ToString() : "") +
                (this.BetaDate != default(DateTime) ? "beta_" + this.BetaDate.ToString("MMddyy") : "");
        }
    }
}
