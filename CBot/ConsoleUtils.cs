namespace CBot;
public static class ConsoleUtils {
	public static bool UseDarkGray { get; set; }

	private static readonly object writeLock = new();
	private static readonly string[] colours = new string[] { "BLACK"    , "DKBLUE"   , "DKGREEN"  , "DKCYAN"   , "DKRED"    , "DKMAGENTA", "DKYELLOW" , "GRAY"       ,
	                                                          "DKGRAY"   , "BLUE"     , "GREEN"    , "CYAN"     , "RED"      , "MAGENTA"  , "YELLOW"   , "WHITE"       };

	private static void WriteSub(string text, ConsoleColor originalForeground, ConsoleColor originalBackground) {
		if (text == null) return;
		int pos = 0;

		while (pos < text.Length) {
			int pos2 = text.IndexOf('%', pos);
			if (pos2 == -1) {
				Console.Write(text[pos..]);
				return;
			}
			if (pos2 != pos) Console.Write(text[pos..pos2]);
			if (pos < text.Length - 1) {
				bool flag = false;
				switch (text[pos2 + 1]) {
					case 'B':
					case 'b':
						pos2 += 2;
						if (pos2 < text.Length) {
							if (pos2 < text.Length && char.ToUpper(text[pos2 + 1]) == 'O') {
								Console.BackgroundColor = originalBackground;
								pos = pos2 + 1;
								continue;
							}
							for (int i = 0; i < 16; ++i) {
								if (text.Length - pos2 > 0 && text.Substring(pos2, colours[i].Length).Equals(colours[i], StringComparison.OrdinalIgnoreCase)) {
									Console.BackgroundColor = (ConsoleColor) i;
									pos = pos2 + colours[i].Length;
									flag = true;
									break;
								}
							}
							if (flag) continue;
						}
						break;
					case 'C':
					case 'c':
						pos2 += 2;
						if (pos2 < text.Length) {
							if (pos2 < text.Length && char.ToUpper(text[pos2 + 1]) == 'O') {
								Console.ForegroundColor = originalForeground;
								pos = pos2 + 1;
								continue;
							}
							for (int i = 0; i < 16; ++i) {
								if (text.Length - pos2 >= colours[i].Length && text.Substring(pos2, colours[i].Length).Equals(colours[i], StringComparison.OrdinalIgnoreCase)) {
									Console.ForegroundColor = (ConsoleColor) i;
									pos = pos2 + colours[i].Length;
									flag = true;
									break;
								}
							}
							if (flag) continue;
						}
						break;
					case 'N':
					case 'n':
						Console.WriteLine();
						continue;
					case 'O':
					case 'o':
						Console.ForegroundColor = originalForeground;
						Console.BackgroundColor = originalBackground;
						pos = pos2 + 2;
						continue;
					case 'R':
					case 'r':
						Console.ResetColor();
						pos = pos2 + 2;
						continue;
					case '%':
						Console.Write("%");
						pos = pos2 + 2;
						continue;
				}
			}
			Console.Write("%");
			pos = pos2 + 1;
		}
	}
	private static void WriteSub(string format, params object?[] args) {
		int i = 0; int open = -1; int index = -1; int start = 0;
		var originalForeground = Console.ForegroundColor;
		var originalBackground = Console.BackgroundColor;

		while (i < format.Length) {
			char c = format[i];

			if (c == '}') {
				if (i != format.Length - 1 && format[i + 1] == '}') {
					if (index == -1) {
						if (i != start)
							WriteSub(format[start..i], originalForeground, originalBackground);
						Console.Write("}");
						start = i + 2;
					}
					++i;
				} else if (index < 0)
					throw new FormatException();
				else {
					if (open == -1)
						Console.Write((args[index] ?? "").ToString());
					else
						Console.Write("{0" + format[open..i] + "}", args[index]);
					open = -1;
					index = -1;
					start = i + 1;
				}
			} else if (open == -1) {
				if (index == -2) {
					if (c is < '0' or > '9')
						throw new FormatException();
					index = c - '0';
				} else if (index != -1) {
					if (c != ' ') {
						if (c is ',' or ':') {
							open = i;
						} else {
							if (c is < '0' or > '9')
								throw new FormatException();
							index = index * 10 + (int) (c - '0');
						}
					}
				} else if (c == '{') {
					if (i != format.Length - 1 && format[i + 1] == '{') {
						if (index == -1) {
							if (i != start)
								WriteSub(format[start..i], originalForeground, originalBackground);
							Console.Write("{");
							start = i + 2;
						}
						++i;
					} else if (open == -1) {
						if (i != start)
							WriteSub(format[start..i], originalForeground, originalBackground);
						index = -2;
					} else
						throw new FormatException();
				}
			}
			++i;
		}
		if (index != -1)
			throw new FormatException();
		if (i != start)
			WriteSub(format[start..i], originalForeground, originalBackground);
	}

	public static void Write(string text)
	{
		lock (writeLock)
			WriteSub(text, Console.ForegroundColor, Console.BackgroundColor);
	}
	public static void Write(string format, params object?[] args)
	{
		lock (writeLock)
			WriteSub(format, args);
	}
	public static void WriteLine() => Console.WriteLine();
	public static void WriteLine(string text)
	{
		lock (writeLock) {
			Write(text);
			Console.WriteLine();
		}
	}
	public static void WriteLine(string format, params object?[] args) {
		lock (writeLock) {
			WriteSub(format, args);
			Console.WriteLine();
		}
	}
}
