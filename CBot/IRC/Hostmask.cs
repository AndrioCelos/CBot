using System.Text;
using System.Text.RegularExpressions;

namespace IRC {
	/// <summary>
	/// Provides methods that help deal with hostmasks.
	/// </summary>
    public static class Hostmask {
		/// <summary>Determines whether the specified string matches the specified glob pattern case-insensitively.</summary>
		/// <param name="input">The string to check.</param>
		/// <param name="pattern">The pattern to match against. '*' matches any sequence of zero or more characters, and '?' matches any single character.</param>
        // TODO: Use the actual case mapping comparer from the IRC client.
        public static bool Matches(string input, string pattern) {
            StringBuilder regexBuilder = new StringBuilder();
            regexBuilder.Append('^');
            foreach (char c in pattern) {
                switch (c) {
                    case '*': regexBuilder.Append(".*"); break;
                    case '?': regexBuilder.Append("."); break;
                    case '\\': case '+': case '|': case '{': case '[': case '(': case ')': case '^': case '$': case '.':
                        regexBuilder.Append('\\'); regexBuilder.Append(c); break;
                    default: regexBuilder.Append(c); break;
                }
            }
            regexBuilder.Append('$');

            return Regex.IsMatch(input, regexBuilder.ToString(), RegexOptions.IgnoreCase);
        }

		/// <summary>Returns the nickname part of the specified hostmask.</summary>
        public static string GetNickname(string mask) {
            var pos = mask.IndexOf('!');
            if (pos == -1) return mask;
            return mask.Substring(0, pos);
        }

		/// <summary>Returns the ident part of the specified hostmask.</summary>
		public static string GetIdent(string mask) {
            var pos = mask.IndexOf('!');
            if (pos == -1 || pos == mask.Length - 1) return "*";
            ++pos;

            var pos2 = mask.IndexOf('@', pos);
            if (pos2 == -1) return mask.Substring(pos);
            return mask.Substring(pos, pos2 - pos);
        }

		/// <summary>Returns the host part of the specified hostmask.</summary>
		public static string GetHost(string mask) {
            var pos = mask.IndexOf('!');
            if (pos == -1 || pos == mask.Length - 1) return "*";
            ++pos;

            pos = mask.IndexOf('@', pos);
            if (pos == -1) return "*";
            return mask.Substring(pos + 1);
        }
    }
}
