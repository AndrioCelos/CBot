using System;
using System.Collections.Generic;

namespace UNO {
    public class Player {
        public string Name;
        public PlayerPresence Presence;
        public int BasePoints;
        public int HandPoints;
        public List<byte> Hand;
        public short IdleCount;
        public bool MultipleCards;
        public string StreakMessage;
        public bool CanMove;
        public DateTime DisconnectedAt;
        public int Position;

        public Player(string name) {
            this.Name = name;
            this.Hand = new List<byte>(10);
        }

        public void SortHandByColour() {
            Player.SortHandByColour(this.Hand);
        }
        public static void SortHandByColour(List<byte> hand) {
            Player.SortHand(hand, (x, p) => x > p);
        }
        public void SortHandByRank() {
            Player.SortHandByRank(this.Hand);
        }
        public static void SortHandByRank(List<byte> hand) {
            Player.SortHand(hand, delegate(byte x, byte p) {
                if ((p & 64) == 64)  // Wild pivot
                    return x > p;
                if ((x & 64) == 64)  // Wild parameter
                    return true;
                if ((x & 15) > (p & 15))
                    return true;
                if ((x & 15) < (p & 15))
                    return false;
                return x > p;
            });
        }
        public static void SortHand(List<byte> hand, Func<byte, byte, bool> predicate) {
            if (hand == null) throw new ArgumentNullException("hand");
            if (hand.Count < 2) return;

            List<byte> l1 = new List<byte>(hand.Count);
            List<byte> l2 = new List<byte>(hand.Count);
            byte pivot = hand[0];

            for (int i = 1; i < hand.Count; i++)
                if (predicate(hand[i], pivot))
                    l2.Add(hand[i]);
                else
                    l1.Add(hand[i]);

            Player.SortHand(l1, predicate);
            Player.SortHand(l2, predicate);

            hand.Clear();
            hand.AddRange(l1);
            hand.Add(pivot);
            hand.AddRange(l2);
        }
    }

    public enum PlayerPresence {
        Playing,
        Left,
        Out
    }
}
