#pragma warning disable 1591  // Missing XML documentation

using System;

namespace IRC {
    /// <summary>
    /// Provides methods for dealing with mIRC formatting codes.
    /// </summary>
	public static class Colours	{
        /// <summary>The colour code.</summary>
		public const char ColourCode      = '\u0003';
		public const string White         = "\u000300";
        public const string Black         = "\u000301";
        public const string DarkBlue      = "\u000302";
        public const string DarkGreen     = "\u000303";
        public const string Red           = "\u000304";
        public const string DarkRed       = "\u000305";
        public const string Purple        = "\u000306";
        public const string Orange        = "\u000307";
        public const string Yellow        = "\u000308";
        public const string Green         = "\u000309";
        public const string Teal          = "\u000310";
        public const string Cyan          = "\u000311";
        public const string Blue          = "\u000312";
        public const string Magenta       = "\u000313";
        public const string DarkGray      = "\u000314";
        public const string Gray          = "\u000315";
        public const string Bold          = "\u0002";
        public const string Italic        = "\u001C";
        public const string Underline     = "\u001F";
        public const string Strikethrough = "\u0013";
        /// <summary>The CTCP code.</summary>
        public const string CTCP          = "\u0001";
        /// <summary>The clear code.</summary>
        public const string ClearFormat   = "\u000F";
        /// <summary>The clear code.</summary>
        public const string Reset         = "\u000F";
        /// <summary>The reverse video code.</summary>
        public const string Reverse       = "\u0016";

        private static readonly int[,] defaultColours = new[,] { {3,99}, {4,99}, {6,99}, {8,99}, {9,99}, {10,99}, {11,99}, {12,99}, {13,99} };

        /// <summary>Returns a colour code that can be used to colour the given string using HexChat's default list of colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <returns>The full colour code for the given string.</returns>
        public static string NicknameColour(string nickname) => NicknameColour(nickname, defaultColours);
        /// <summary>Returns a colour code that can be used to colour the given string using the specified list of text colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <param name="colours">An array containing colour indices to use on the text.</param>
        /// <returns>The full colour code for the given string.</returns>
        public static string NicknameColour(string nickname, int[] colours) => NicknameColour(nickname, transformColourArray(colours));
        /// <summary>Returns a colour code that can be used to colour the given string using the specified list of text and background colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <param name="colours">
        ///     A two-dimensional array containing colour indices to use on the text.
        ///     The second dimension must contain two elements. The first element (0) specifies the foreground colours, and the second element (1) the background colours.
        /// </param>
        /// <returns>The full colour code for the given string.</returns>
        public static string NicknameColour(string nickname, int[,] colours) {
            var result = NicknameColourIndex(nickname, colours);
            if (result[1] >= 0 && result[1] < 99) return "\u0003" + result[0] + "," + result[1].ToString("00");
            else return "\u0003" + result[0].ToString("00");
		}
        /// <summary>Returns a colour index that can be used to colour the given string using HexChat's default list of colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <returns>An array with two elements, consisting of the foreground and background colour for the given string, in that order.</returns>
        public static int[] NicknameColourIndex(string nickname) => NicknameColourIndex(nickname, defaultColours);
        /// <summary>Returns a colour index that can be used to colour the given string using the specified list of text colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <param name="colours">An array containing colour indices to use on the text.</param>
        /// <returns>An array with two elements, consisting of the foreground and background colour for the given string, in that order.</returns>
        public static int[] NicknameColourIndex(string nickname, int[] colours) => NicknameColourIndex(nickname, transformColourArray(colours));
        /// <summary>Returns a colour index that can be used to colour the given string using the specified list of text and background colours.</summary>
        /// <param name="nickname">The string to colour.</param>
        /// <param name="colours">
        ///     A two-dimensional array containing colour indices to use on the text.
        ///     The second dimension must contain two elements. The first element (0) specifies the foreground colours, and the second element (1) the background colours.
        /// </param>
        /// <returns>An array with two elements, consisting of the foreground and background colour for the given string, in that order.</returns>
        public static int[] NicknameColourIndex(string nickname, int[,] colours) {
            if (colours == null) throw new ArgumentNullException("colours");
            if (colours.Length == 0) throw new ArgumentException("colours must contain at least one element.", "colours");
            if (colours.GetUpperBound(1) != 1) throw new ArgumentException("The second dimension of the array must have an upper bound of 1.", "colours");

            int digest = 0;
            for (int i = 0; i < nickname.Length; ++i)
				digest += nickname[i];
            digest %= colours.GetUpperBound(0) + 1;

			return new[] { colours[digest, 0], colours[digest, 1] };
		}

        private static int[,] transformColourArray(int[] array) {
            var result = new int[array.Length, 2];
            for (int i = 0; i < array.Length; ++i) {
                result[i, 0] = array[i];
                result[i, 1] = 99;
            }
            return result;
        }
    }

    /// <summary>
    /// Gives the mIRC colour codes.
    /// </summary>
    public enum ColourIndex {
        White,
        Black,
        DarkBlue,
        DarkGreen,
        Red,
        DarkRed,
        Purple,
        Orange,
        Yellow,
        Green,
        Teal,
        Cyan,
        Blue,
        Magenta,
        DarkGray,
        Gray,
        /// <summary>A colour code that resets the background colour. In some clients, it can also reset the foreground colour.</summary>
        Default = 99
    }
}
