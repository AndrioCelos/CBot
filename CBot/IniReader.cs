using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBot {
    public class IniFile : Dictionary<string, Dictionary<string, string>> {
        public IniFile() : base() { }
        public IniFile(IEqualityComparer<string> comparer) : base(comparer) { }
        public IniFile(IDictionary<string, Dictionary<string, string>> source) : base(source) { }
        public IniFile(IDictionary<string, Dictionary<string, string>> source, IEqualityComparer<string> comparer) : base(source, comparer) { }

        public string this[string section, string key] {
            get { return this[section][key]; }
            set {
                Dictionary<string, string> dictionary;
                if (!this.TryGetValue(section, out dictionary)) {
                    dictionary = new Dictionary<string, string>(this.Comparer);
                    this[section] = dictionary;
                }
                dictionary[key] = value;
            }
        }

        public bool TryGetValue(string section, string key, out string value) {
            Dictionary<string, string> dictionary;
            if (!this.TryGetValue(section, out dictionary)) { value = null; return false; }
            return (!dictionary.TryGetValue(key, out value));
        }

        public static IniFile FromFile(string file) => FromFile(file, EqualityComparer<string>.Default);
        public static IniFile FromFile(string file, IEqualityComparer<string> comparer) {
            using (var reader = new StreamReader(file)) {
                IniFile result = new IniFile(comparer);
                Dictionary<string, string> section = null;

                while (!reader.EndOfStream) {
                    string line = reader.ReadLine().TrimStart();
                    if (line.StartsWith(";")) continue;  // Comment.
                    if (line.StartsWith("[")) {
                        // Section header.
                        int pos = line.IndexOf(']', 1);
                        if (pos == -1) pos = line.Length;
                        string sectionName = line.Substring(1, pos - 1).Trim();

                        if (result.TryGetValue(sectionName, out section))
                            // To emulate the Windows API, duplicate sections are ignored.
                            section = null;
                        else {
                            section = new Dictionary<string, string>(comparer);
                            result[sectionName] = section;
                        }
                    } else if (section != null) {
                        int pos = line.IndexOf('=');
                        if (pos == -1) continue;

                        string key = line.Substring(0, pos).TrimEnd();
                        string value = line.Substring(pos + 1).Trim();

                        // Duplicate keys are also ignored.
                        if (!section.ContainsKey(key))
                            section[key] = value;
                    }
                }

                return result;
            }
        }
    }
}
