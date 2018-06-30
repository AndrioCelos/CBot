namespace UNO {
	public enum WildDrawFourRule {
		/// <summary>A Wild Draw Four may not be played if the player has a matching colour card.</summary>
		DisallowBluffing,
		/// <summary>A Wild Draw Four may be played if the player has a matching colour card, but will be penalised if challenged.</summary>
		AllowBluffing,
		/// <summary>A Wild Draw Four may be played without penalty if the player has a matching colour card.</summary>
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
