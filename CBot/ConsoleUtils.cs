using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace CBot
{
	public static class ConsoleUtils
	{
        private static object writeLock = new object();
        private static string[] colours = new string[] { "BLACK"    , "DKBLUE"   , "DKGREEN"  , "DKCYAN"   , "DKRED"    , "DKMAGENTA", "DKYELLOW" , "GRAY"       ,
                                                         "DKGRAY"   , "BLUE"     , "GREEN"    , "CYAN"     , "RED"      , "MAGENTA"  , "YELLOW"   , "WHITE"       };

		public static void Write(string Text)
		{
            ConsoleColor originalBackground; ConsoleColor originalForeground;

            lock (writeLock) {
                if (Text == null) return;
                originalBackground = Console.BackgroundColor;
                originalForeground = Console.ForegroundColor;
                int pos = 0;

                while (pos < Text.Length) {
                    int pos2 = Text.IndexOf('%', pos);
                    if (pos2 == -1) {
                        Console.Write(Text.Substring(pos));
                        return;
                    }
                    if (pos2 != pos) Console.Write(Text.Substring(pos, pos2 - pos));
                    if (pos < Text.Length - 1) {
                        bool flag = false;
                        switch (Text[pos2 + 1]) {
                            case 'B':
                            case 'b':
                                pos2 += 2;
                                if (pos2 < Text.Length) {
                                    if (pos2 < Text.Length && char.ToUpper(Text[pos2 + 1]) == 'O') {
                                        Console.BackgroundColor = originalBackground;
                                        pos = pos2 + 1;
                                        continue;
                                    }
                                    for (int i = 0; i < 16; ++i) {
                                        if (Text.Length - pos2 > 0 && Text.Substring(pos2, colours[i].Length).Equals(colours[i], StringComparison.OrdinalIgnoreCase)) {
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
                                if (pos2 < Text.Length) {
                                    if (pos2 < Text.Length && char.ToUpper(Text[pos2 + 1]) == 'O') {
                                        Console.ForegroundColor = originalForeground;
                                        pos = pos2 + 1;
                                        continue;
                                    }
                                    for (int i = 0; i < 16; ++i) {
                                        if (Text.Length - pos2 >= colours[i].Length && Text.Substring(pos2, colours[i].Length).Equals(colours[i], StringComparison.OrdinalIgnoreCase)) {
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
                                pos2 += 2;
                                continue;
                            case 'R':
                            case 'r':
                                Console.ForegroundColor = originalForeground;
                                Console.BackgroundColor = originalBackground;
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
		}
		public static void Write(string Format, params object[] args)
		{
			ConsoleUtils.Write(string.Format(Format, args));
		}
        public static void WriteLine() {
            Console.WriteLine();
        }
        public static void WriteLine(string Text)
		{
			ConsoleUtils.Write(Text);
    		Console.WriteLine();
		}
        public static void WriteLine(string Format, params object[] args) {
            ConsoleUtils.Write(Format, args);
            Console.WriteLine();
        }
	}
}
