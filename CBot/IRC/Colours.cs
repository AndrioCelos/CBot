namespace IRC {
    /// <summary>
    /// Provides methods for dealing with mIRC formatting codes.
    /// </summary>
	public static class Colours	{
        /// <summary>The colour code.</summary>
		public const char ColourCode      = '\u0003';
        /// <summary>The full 'white' colour code.</summary>
		public const string White         = "\u000300";
        /// <summary>The full 'black' colour code.</summary>
        public const string Black         = "\u000301";
        /// <summary>The full 'dark blue' colour code.</summary>
        public const string DarkBlue      = "\u000302";
        /// <summary>The full 'dark green' colour code.</summary>
        public const string DarkGreen     = "\u000303";
        /// <summary>The full 'red' colour code.</summary>
        public const string Red           = "\u000304";
        /// <summary>The full 'brown' colour code.</summary>
        public const string DarkRed       = "\u000305";
        /// <summary>The full 'purple' colour code.</summary>
        public const string Purple        = "\u000306";
        /// <summary>The full 'orange' colour code.</summary>
        public const string Orange        = "\u000307";
        /// <summary>The full 'yellow' colour code.</summary>
        public const string Yellow        = "\u000308";
        /// <summary>The full 'green' colour code.</summary>
        public const string Green         = "\u000309";
        /// <summary>The full 'teal' colour code.</summary>
        public const string Teal          = "\u000310";
        /// <summary>The full 'cyan' colour code.</summary>
        public const string Cyan          = "\u000311";
        /// <summary>The full 'blue' colour code.</summary>
        public const string Blue          = "\u000312";
        /// <summary>The full 'magenta' colour code.</summary>
        public const string Magenta       = "\u000313";
        /// <summary>The full 'dark gray' colour code.</summary>
        public const string DarkGray      = "\u000314";
        /// <summary>The full 'gray' colour code.</summary>
        public const string Gray          = "\u000315";
        /// <summary>The bold code.</summary>
        public const string Bold          = "\u0002";
        /// <summary>The italic code. Not recognised by some clients.</summary>
        public const string Italic        = "\u001C";
        /// <summary>The underline.</summary>
        public const string Underline     = "\u001F";
        /// <summary>The strikethrough code. Recognised by few clients.</summary>
        public const string Strikethrough = "\u0013";
        /// <summary>The CTCP code.</summary>
        public const string CTCP          = "\u0001";
        /// <summary>The clear code.</summary>
        public const string ClearFormat   = "\u000F";
        /// <summary>The clear code.</summary>
        public const string Reset         = "\u000F";
        /// <summary>The reverse video code.</summary>
        public const string Reverse       = "\u0016";

        private static readonly int[] nicknameColours = new int[] {3, 4, 6, 8, 9, 10, 11, 12, 13 };

        /// <summary>Returns the given nickname preceded by a colour code.</summary>
        /// <param name="nickname">The nickname to colour.</param>
        /// <returns>The nickname preceded by a colour code, calculated as per HexChat.</returns>
        public static string NicknameColour(string nickname) {
            return "\u0003" + NicknameColourIndex(nickname);
		}
        /// <summary>Returns the HexChat colour index for a given nickname.</summary>
        /// <param name="nickname">The nickname to colour.</param>
        /// <returns>An integer specifying which colour should be used for this nickname.</returns>
        public static int NicknameColourIndex(string nickname) {
			int digest = 0;
            for (int i = 0; i < nickname.Length; ++i)
				digest += nickname[i];
			return nicknameColours[digest % nicknameColours.Length];
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
        Default = 99
    }
}
