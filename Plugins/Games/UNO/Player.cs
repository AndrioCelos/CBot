using System;
using System.Collections.Generic;

namespace UNO {
    public class Player {
        public string Name;
        public PlayerPresence Presence;
        public int BasePoints;
        public int HandPoints;
        public List<Card> Hand;
        public short IdleCount;
        internal string StreakMessage;
        public bool CanMove;
        public DateTime DisconnectedAt;
        public int Rank;

        public Player(string name) {
            this.Name = name;
            this.Hand = new List<Card>(10);
        }

        public void SortHandByColour() {
            Player.SortHandByColour(this.Hand);
        }
        public static void SortHandByColour(List<Card> hand) {
			hand.Sort((v1, v2) => (byte) v1 - (byte) v2);
        }
        public void SortHandByRank() {
            Player.SortHandByRank(this.Hand);
        }
        public static void SortHandByRank(List<Card> hand) {
			hand.Sort((v1, v2) => (
				v2.IsWild ? (byte) v1 - (byte) v2 :
				v1.IsWild ? 1 :
				v1.Colour != v2.Colour ? v1.Colour - v2.Colour :
					(byte) v1 - (byte) v2
			));
        }
    }

    public enum PlayerPresence {
        Playing,
        Left,
        Out,
		OutByDefault
    }
}
