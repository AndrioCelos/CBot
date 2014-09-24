using System;

namespace UNO {

    public class PlayerStats {
        public string Name;
        public long Points;
        public short Rank;
        public int Plays;
        public int Wins;
        public int Losses;
        public int RecordPoints;
        public DateTime RecordTime;
        public long ChallengePoints;
        public DateTime StartedPlaying;

        public int CurrentStreak;
        public int BestStreak;
        public DateTime BestStreakTime;
        public int[] Placed;
        public long BestPeriodScore;
        public DateTime BestPeriodScoreTime;
        public long BestPeriodChallengeScore;
        public DateTime BestPeriodChallengeScoreTime;

        public PlayerStats() {
            this.Placed = new int[] { 0, 0, 0, 0, 0 };
        }

        public float WinRate {
            get {
                if (this.Plays == 0) return 0;
                return (float) this.Wins / (float) this.Plays;
            }
        }

        public float LossRate {
            get {
                if (this.Plays == 0) return 0;
                return (float) this.Losses / (float) this.Plays;
            }
        }
    }
}