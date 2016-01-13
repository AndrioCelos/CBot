using System;
using System.Collections.Generic;
using System.Text;

namespace IRC {
    public class IRCLine {
        public Dictionary<string, string> Tags { get; }
        public string Prefix;
        public string Command;
        public string[] Parameters;
        public bool HasTrail;

        public IRCLine(string command, string[] parameters) {
            this.Command = command;
            this.Parameters = parameters;
        }
        public IRCLine(Dictionary<string, string> tags, string prefix, string command, List<string> parameters, bool hasTrail) {
            this.Tags = tags;
            this.Prefix = prefix;
            this.Command = command;
            if (parameters == null) this.Parameters = new string[0];
            else this.Parameters = parameters.ToArray();
            this.HasTrail = hasTrail;
        }

        public static IRCLine Parse(string line) {
            return IRCLine.Parse(line, true);
        }
        public static IRCLine Parse(string line, bool allowTags) {
            if (line == null) throw new ArgumentNullException("line");
            if (line.Length == 0) return new IRCLine(null, null, null, null, false);

            Dictionary<string, string> tags = null; string prefix = null, command = null;
            var parameters = new List<string>();
            bool hasTrail = false;

            StringBuilder builder = new StringBuilder(32);
            int i = 0; char c = line[0];

            if (allowTags && c == '@') {
                // The line has IRCv3.2 tags.
                tags = new Dictionary<string, string>();
                for ( ; i < line.Length; ++i) {
                    string tag; string value;

                    for ( ; i < line.Length; ++i) {
                        c = line[i];
                        if (c == '=' || c == ';' || c == ' ') break;
                        builder.Append(c);
                    }
                    tag = builder.ToString();

                    if (i < line.Length && c == '=') {
                        builder.Clear();
                        for (; i < line.Length; ++i) {
                            c = line[i];
                            if (c == ';' || c == ' ') break;
                            if (c == '\\') {  // Escape sequence
                                if (++i == line.Length) {
                                    builder.Append('\\');
                                    break;
                                }
                                c = line[i];
                                switch (c) {
                                    case ':':
                                        builder.Append(';'); break;
                                    case 's':
                                        builder.Append(' '); break;
                                    case '\\':
                                        builder.Append('\\'); break;
                                    case 'r':
                                        builder.Append('\r'); break;
                                    case 'n':
                                        builder.Append('\n'); break;
                                    default:
                                        builder.Append(c); break;
                                }
                            } else
                                builder.Append(c);
                        }
                        value = builder.ToString();
                    } else
                        value = null;

                    tags.Add(tag, value);
                    if (i >= line.Length || c == ' ') break;
                }
                ++i;
            }

            if (i < line.Length) {
                if (line[i] == ':') {  // Prefix
                    builder.Clear();
                    ++i;
                    for (; i < line.Length; ++i) {
                        c = line[i];
                        if (c == ' ') break;
                        builder.Append(c);
                    }
                    prefix = builder.ToString();
                    ++i;
                }

                // Numeric
                if (i < line.Length) {
                    builder.Clear();
                    for (; i < line.Length; ++i) {
                        c = line[i];
                        if (c == ' ') break;
                        builder.Append(c);
                    }
                    command = builder.ToString();
                    ++i;

                    // Parameters
                    while (i < line.Length) {
                        if (line[i] == ':') {  // Trail
                            hasTrail = true;
                            parameters.Add(line.Substring(i + 1));
                            break;
                        }

                        builder.Clear();
                        for (; i < line.Length; ++i) {
                            c = line[i];
                            if (c == ' ') break;
                            builder.Append(c);
                        }
                        parameters.Add(builder.ToString());
                        ++i;
                    }
                }
            }

            return new IRCLine(tags, prefix, command, parameters, hasTrail);
        }

        public static string EscapeTag(string value) {
            StringBuilder builder = new StringBuilder(value.Length + value.Length / 4);
            foreach (char c in value) {
                switch (c) {
                    case ';':
                        builder.Append(@"\:"); break;
                    case ' ':
                        builder.Append(@"\s"); break;
                    case '\\':
                        builder.Append(@"\\"); break;
                    case '\r':
                        builder.Append(@"\r"); break;
                    case '\n':
                        builder.Append(@"\n"); break;
                    default:
                        builder.Append(c); break;
                }
            }
            return builder.ToString();
        }

        public static string UnescapeTag(string value) {
            return UnescapeTag(value, false);
        }
        public static string UnescapeTag(string value, bool strict) {
            StringBuilder builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; ++i) {
                char c = value[i];
                if (c == '\\') {  // Escape sequence
                    if (++i == value.Length) {
                        if (strict) throw new FormatException("Incomplete escape sequence.");
                        builder.Append('\\');
                        break;
                    }
                    c = value[i];
                    switch (c) {
                        case ':':
                            builder.Append(';'); break;
                        case 's':
                            builder.Append(' '); break;
                        case '\\':
                            builder.Append('\\'); break;
                        case 'r':
                            builder.Append('\r'); break;
                        case 'n':
                            builder.Append('\n'); break;
                        default:
                            if (strict) throw new FormatException("Invalid escape sequence '\\" + c + "'.");
                            builder.Append(c); break;
                    }
                } else
                    builder.Append(c);
            }

            return builder.ToString();
        }

        public override string ToString() {
            StringBuilder builder = new StringBuilder(128);
            if (this.Tags != null) {
                builder.Append("@");
                foreach (var tag in this.Tags) {
                    if (builder.Length != 1) builder.Append(";");
                    builder.Append(tag.Key);
                    if (tag.Value != null) {
                        builder.Append("=");
                        builder.Append(EscapeTag(tag.Value));
                    }
                }
                builder.Append(" ");
            }
            if (this.Prefix != null) {
                builder.Append(":");
                builder.Append(this.Prefix);
                builder.Append(" ");
            }
            builder.Append(this.Command);

            for (int i = 0; i < this.Parameters.Length; ++i) {
                builder.Append(" ");
                if (this.HasTrail && i == this.Parameters.Length - 1) builder.Append(":");
                builder.Append(this.Parameters[i]);
            }

            return builder.ToString();
        }
    }
}
