using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    public enum CaseMappingMode {
        ASCII,
        RFC1459,
        StrictRFC1459
    }

    public class IRCStringComparer : StringComparer {
        char maxUppercase;
        char maxLowercase;

        public static IRCStringComparer ASCII {
            get { return new IRCStringComparer(CaseMappingMode.ASCII); }
        }

        public static IRCStringComparer RFC1459 {
            get { return new IRCStringComparer(CaseMappingMode.RFC1459); }
        }

        public static IRCStringComparer StrictRFC1459 {
            get { return new IRCStringComparer(CaseMappingMode.StrictRFC1459); }
        }

        internal IRCStringComparer(CaseMappingMode mode) {
            switch (mode) {
                case CaseMappingMode.ASCII:
                    maxUppercase = (char) 90;
                    maxLowercase = (char) 122;
                    break;
                case CaseMappingMode.RFC1459:
                    maxUppercase = (char) 94;
                    maxLowercase = (char) 126;
                    break;
                case CaseMappingMode.StrictRFC1459:
                    maxUppercase = (char) 93;
                    maxLowercase = (char) 125;
                    break;
                default:
                    throw new ArgumentException("mode is not a valid case mapping mode.", "mode");
            }
        }

        public override bool Equals(string p1, string p2) {
            if (object.ReferenceEquals(p1, p2)) return true;
            if (p1 == null || p2 == null) return false;
            if (p1.Length != p2.Length) return false;

            for (int i = 0; i < p1.Length; ++i) {
                char c1 = p1[i]; char c2 = p2[i];
                if (c1 >= (char) 97 && c1 <= maxLowercase) c1 -= (char) 32;
                if (c2 >= (char) 97 && c2 <= maxLowercase) c2 -= (char) 32;
                if (c1 != c2) return false;
            }
            return true;
        }

        public override int GetHashCode(string s) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < s.Length; ++i) {
                char c1 = s[i];
                if (c1 >= (char) 97 && c1 <= maxLowercase) c1 -= (char) 32;
                builder.Append(c1);
            }
            return builder.ToString().GetHashCode();
        }

        public override int Compare(string p1, string p2) {
            if (object.ReferenceEquals(p1, p2)) return 0;
            if (p1 == null) return -1;
            if (p2 == null) return 1;

            for (int i = 0; i < p1.Length; ++i) {
                if (i >= p2.Length) return 1;
                int c1 = p1[i]; int c2 = p2[i];
                if (c1 >= (char) 65 && c1 <= maxUppercase) c1 -= 97;
                if (c2 >= (char) 97 && c2 <= maxLowercase) c2 -= 129;
                c1 -= c2;
                if (c1 != 0) return c1;
            }
            if (p2.Length > p1.Length) return -1;
            return 0;
        }
    }
}