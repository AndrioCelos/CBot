using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    /// <summary>
    /// Represents a case-mapping mode.
    /// </summary>
    public enum CaseMappingMode {
        /// <summary>ASCII: the characters 'a' through 'z' are considered the lowercase equivalents to 'A' through 'Z'.</summary>
        ASCII,
        /// <summary>RFC 1459: the characters 'a' through '~' are considered the lowercase equivalents to 'A' through '^'.</summary>
        RFC1459,
        /// <summary>Strict RFC 1459: the characters 'a' through '}' are considered the lowercase equivalents to 'A' through ']'.</summary>
        StrictRFC1459
    }

    /// <summary>
    /// Provides StringComparer subclasses that make case-insensitive comparisons as defined by the IRC protocol.
    /// </summary>
    public class IRCStringComparer : StringComparer {
        private char maxUppercase;
        private char maxLowercase;

        /// <summary>Returns a StringComparer object that makes case-insensitive comparisons using the ASCII case mapping.</summary>
        public static IRCStringComparer ASCII {
            get { return new IRCStringComparer(CaseMappingMode.ASCII); }
        }

        /// <summary>Returns a StringComparer object that makes case-insensitive comparisons using the RFC 1459 case mapping.</summary>
        public static IRCStringComparer RFC1459 {
            get { return new IRCStringComparer(CaseMappingMode.RFC1459); }
        }

        /// <summary>Returns a StringComparer object that makes case-insensitive comparisons using the strict RFC 1459 case mapping.</summary>
        public static IRCStringComparer StrictRFC1459 {
            get { return new IRCStringComparer(CaseMappingMode.StrictRFC1459); }
        }

        internal IRCStringComparer(CaseMappingMode mode) {
            switch (mode) {
                case CaseMappingMode.ASCII:
                    maxUppercase = 'Z';
                    maxLowercase = 'z';
                    break;
                case CaseMappingMode.RFC1459:
                    maxUppercase = '^';
                    maxLowercase = '~';
                    break;
                case CaseMappingMode.StrictRFC1459:
                    maxUppercase = ']';
                    maxLowercase = '}';
                    break;
                default:
                    throw new ArgumentException("mode is not a valid case mapping mode.", "mode");
            }
        }

        /// <summary>Determines whether two strings are equivalent.</summary>
        /// <param name="p1">The first string to compare.</param>
        /// <param name="p2">The second string to compare.</param>
        /// <returns>True if the strings are equivalent, or both null; false otherwise.</returns>
        public override bool Equals(string p1, string p2) {
            if (object.ReferenceEquals(p1, p2)) return true;
            if (p1 == null || p2 == null) return false;
            if (p1.Length != p2.Length) return false;

            for (int i = 0; i < p1.Length; ++i) {
                char c1 = p1[i]; char c2 = p2[i];
                if (c1 >= 'a' && c1 <= maxLowercase) c1 -= (char) 32;
                if (c2 >= 'a' && c2 <= maxLowercase) c2 -= (char) 32;
                if (c1 != c2) return false;
            }
            return true;
        }

        /// <summary>Calculates a case-insensitive hash code for a string.</summary>
        /// <param name="s">The string to use.</param>
        /// <returns>The hash code of the uppercase version of the specified string.</returns>
        public override int GetHashCode(string s) {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < s.Length; ++i) {
                char c1 = s[i];
                if (c1 >= 'a' && c1 <= maxLowercase) c1 -= (char) 32;
                builder.Append(c1);
            }
            return builder.ToString().GetHashCode();
        }

        /// <summary>Compares two strings.</summary>
        /// <param name="p1">The first string to compare.</param>
        /// <param name="p2">The second string to compare.</param>
        /// <returns>
        ///     Zero if the two strings are equal, or both are null;
        ///     a positive number if p1 comes after p2, or p2 is null;
        ///     a negative number if p1 comes before p2, or p1 is null.
        /// </returns>
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