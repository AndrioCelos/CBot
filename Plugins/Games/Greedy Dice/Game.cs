using System;
using System.Collections.Generic;
using System.Timers;

using IRC;

namespace GreedyDice {
    public class Game {
        public IrcClient Connection { get; }
        public string Channel { get; }

        public bool IsOpen { get; set; }
        public DateTime StartTime { get; set; }

        public Timer GameTimer;
        public DateTime TurnStartTime;
        public int WaitTime;
        public bool NoTimerReset;

        public List<Player> Players;
        public int Turn { get; set; }
        public int IdleTurn { get; set; }
        public int TurnNumber { get; set; }

        public int TurnScore { get; set; }

        internal Random RNG;
        internal object Lock;

        public Game(IrcClient connection, string channel, int entryTime) {
            this.Connection = connection;
            this.Channel = channel;
            this.GameTimer = new Timer(entryTime == 0 ? 60e+3 : entryTime * 1e+3) { AutoReset = false };
            this.Players = new List<Player>(4);
            this.Lock = new object();

            this.IsOpen = true;
        }

        public int IndexOf(string nickname) {
            for (int i = 0; i < this.Players.Count; i++) {
                if (this.Players[i].Name == nickname)
                    return i;
            }
            return -1;
        }

        public bool IsAIUp {
            get {
                return this.Players[this.Turn].Name == this.Connection.Me.Nickname;
            }
        }


    }
}
