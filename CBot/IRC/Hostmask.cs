using System.Text;
using System.Text.RegularExpressions;

namespace IRC {
    public static class Hostmask {
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

        public static string GetNickname(string mask) {
            var pos = mask.IndexOf('!');
            if (pos == -1) return mask;
            return mask.Substring(0, pos);
        }

        public static string GetIdent(string mask) {
            var pos = mask.IndexOf('!');
            if (pos == -1 || pos == mask.Length - 1) return "*";
            ++pos;

            var pos2 = mask.IndexOf('@', pos);
            if (pos2 == -1) return mask.Substring(pos);
            return mask.Substring(pos, pos2 - pos);
        }

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
