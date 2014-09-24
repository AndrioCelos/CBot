namespace UNO {
    public class PlayerSettings {
        public AutoSortOptions AutoSort = AutoSortOptions.ByColour;
        public HighlightOptions Highlight = HighlightOptions.Off;
        public bool AllowDuelWithBot = true;

        public bool IsDefault() {
            return (this.AutoSort == AutoSortOptions.ByColour &&
                    this.Highlight == HighlightOptions.Off &&
                    this.AllowDuelWithBot);
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
