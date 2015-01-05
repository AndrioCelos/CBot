using System;

namespace GreedyDice {
    public class Player {
        public string Name { get; set; }
        public bool Quit { get; set; }
        public int Score { get; set; }

        public DateTime DisconnectedAt { get; set; }
        public bool CanMove { get; set; }
        public int IdleCount { get; set; }

        public Player(string name) {
            this.Name = name;
        }
    }
}
