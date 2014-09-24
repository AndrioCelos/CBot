using System;

namespace IRC
{
	public static class Colours
	{
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
        public const string CTCP          = "\u0001";
		public const string ClearFormat   = "\u000F";
        public const string Reset         = "\u000F";
        public const string Reverse       = "\u0016";

        private static readonly int[] nicknameColours = new int[] {3, 4, 6, 8, 9, 10, 11, 12, 13 };

        public static string NicknameColour(string nickname)
		{
			int digest = 0;
            for (int i = 0; i < nickname.Length; ++i)
				digest += (int) nickname[i];
			return "\u0003" + Colours.nicknameColours[digest % Colours.nicknameColours.Length].ToString("00");
		}
	}
}
