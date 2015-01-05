namespace UNO {
    public enum Colour : byte {
        Red = 0,
        Yellow = 16,
        Green = 32,
        Blue = 48,
        Pending = 64,
        None = 128
    }

    public enum Rank : byte {
        Zero,
        One,
        Two,
        Three,
        Four,
        Five,
        Six,
        Seven,
        Eight,
        Nine,
        Reverse,
        Skip,
        DrawTwo,
        Wild = 64,
        WildDrawFour
    }

    public enum WildDrawFourRule : short {
        DisallowBluffing,
        AllowBluffing,
        Free
    }

    public enum LeaderboardMode {
        None,
        Unsorted,
        SortedByName,
        SortedByScore,
        SortedByPlays,
        SortedByWins,
        SortedByChallenge,
        SortedByRecord,
        SortedByStreak,
        SortedByBestPeriod,
        SortedByBestPeriodChallenge
    }
}
