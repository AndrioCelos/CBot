namespace UNO {
    public class PlayerSettings {
        public AutoSortOptions AutoSort { get; set; }
        public HighlightOptions Highlight { get; set; }
        public bool AllowDuelWithBot { get; set; }
        public bool Hints { get; set; }
        public bool[] HintsSeen { get; }

        public PlayerSettings() {
            this.AutoSort = AutoSortOptions.ByColour;
            this.Highlight = HighlightOptions.Off;
            this.AllowDuelWithBot = true;
            this.Hints = true;
            this.HintsSeen = new bool[UNOPlugin.Hints.Length];
        }

        public bool IsDefault() {
            return (this.AutoSort == AutoSortOptions.ByColour &&
                    this.Highlight == HighlightOptions.Off &&
                    this.AllowDuelWithBot &&
                    this.Hints);
        }
    }

    public enum AutoSortOptions : short {
        Off,
        ByColour,
        ByRank
    }

    public enum HighlightOptions : short {
        Off,
        OffBecauseIdle,
        OffBecauseLeftChannel,
        OnTemporary = 4,
        OnTemporaryOneGame,
        On,
    }
}
