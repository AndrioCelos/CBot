using System.Diagnostics.CodeAnalysis;

namespace CBot;
public class IniFile : Dictionary<string, Dictionary<string, string>> {
	public IniFile() : base() { }
	public IniFile(IEqualityComparer<string> comparer) : base(comparer) { }
	public IniFile(IDictionary<string, Dictionary<string, string>> source) : base(source) { }
	public IniFile(IDictionary<string, Dictionary<string, string>> source, IEqualityComparer<string> comparer) : base(source, comparer) { }

	public string this[string section, string key] {
		get => this[section][key];
		set {
			if (!this.TryGetValue(section, out var dictionary)) {
				dictionary = new Dictionary<string, string>(this.Comparer);
				this[section] = dictionary;
			}
			dictionary[key] = value;
		}
	}

	public bool TryGetValue(string section, string key, [MaybeNullWhen(false)] out string value) {
		if (!this.TryGetValue(section, out var dictionary)) { value = null; return false; }
		return dictionary.TryGetValue(key, out value);
	}

	public static IniFile FromFile(string file) => FromFile(file, EqualityComparer<string>.Default);
	public static IniFile FromFile(string file, IEqualityComparer<string> comparer) {
		using var reader = new StreamReader(file); var result = new IniFile(comparer);
		Dictionary<string, string>? section = null;

		while (!reader.EndOfStream) {
			var line = reader.ReadLine()?.TrimStart();
			if (line == null) break;
			if (line.StartsWith(";")) continue;  // Comment.
			if (line.StartsWith("[")) {
				// Section header.
				int pos = line.IndexOf(']', 1);
				if (pos == -1) pos = line.Length;
				string sectionName = line[1..pos].Trim();

				if (result.ContainsKey(sectionName))
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
				string value = line[(pos + 1)..].Trim();

				// Duplicate keys are also ignored.
				if (!section.ContainsKey(key))
					section[key] = value;
			}
		}

		return result;
	}
}
