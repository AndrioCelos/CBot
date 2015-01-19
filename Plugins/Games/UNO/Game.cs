using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

using IRC;

namespace UNO {
    public class Game {
        public IRCClient Connection { get; internal set; }
        public string Channel { get; internal set; }

        public bool IsOpen;
        public bool Ended;
        public DateTime StartTime;
        
        public Timer GameTimer;
        public DateTime TurnStartTime;
        public int WaitTime;
        public bool NoTimerReset;

        public Timer HintTimer;
        public int Hint;
        public int HintRecipient;

        public List<Player> Players;
        internal List<int> PlayersOut;
        public int Turn;
        public int IdleTurn;

        internal List<string> RecordBreakers;

        public bool IsReversed;
        public byte DrawnCard;
        public byte DrawFourBadColour;
        public int DrawFourUser;
        public int DrawFourChallenger;
        public bool GameEnded;

        public short CardsPlayed;
        public short TurnNumber;

        internal List<byte> Deck;
        internal List<byte> Discards;
        internal byte WildColour;

        internal Random RNG;
        internal object Lock;

        public Game(IRCClient connection, string channel, int entryTime) {
            this.Players = new List<Player>(10);
            this.DrawnCard = 255;
            this.DrawFourBadColour = (byte) Colour.None;
            this.Deck = new List<byte>(108);
            this.Discards = new List<byte>(108);
            this.WildColour = (byte) Colour.None;
            this.RecordBreakers = new List<string>(4);
            this.Lock = new object();
            this.DrawFourChallenger = -1;
            this.DrawFourUser = -1;
            this.DrawFourBadColour = 128;

            this.Connection = connection;
            this.Channel = channel;
            this.GameTimer = new Timer(entryTime == 0 ? 60e+3 : entryTime * 1e+3) { AutoReset = false };

            // Populate the deck.
            this.Deck.AddRange(new byte[] {
                 0,  1,  1,  2,  2,  3,  3,  4,  4,  5,  5,  6,  6,  7,  7,  8,  8,  9,  9, 10, 10, 11, 11, 12, 12,
                16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 23, 24, 24, 25, 25, 26, 26, 27, 27, 28, 28,
                32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 38, 39, 39, 40, 40, 41, 41, 42, 42, 43, 43, 44, 44,
                48, 49, 49, 50, 50, 51, 51, 52, 52, 53, 53, 54, 54, 55, 55, 56, 56, 57, 57, 58, 58, 59, 59, 60, 60,
                64, 64, 64, 64, 65, 65, 65, 65
            });
        }

        public int NextPlayer() {
            return this.NextPlayer(this.Turn);
        }
        public int NextPlayer(int player) {
            do {
                if (this.IsReversed) {
                    if (player == 0) player = this.Players.Count - 1;
                    else player--;
                } else {
                    if (player == this.Players.Count - 1) player = 0;
                    else player++;
                }
            } while (this.Players[player].Presence != PlayerPresence.Playing);
            return player;
        }

        public void Advance() {
            if (this.IdleTurn != this.Turn) {
                int increment = this.IsReversed ? 1 : -1;
                for (int player = this.IdleTurn; player != this.Turn; ) {
                    this.Players[player].CanMove = false;

                    player += increment;
                    if (player == this.Players.Count) player = 0;
                    else if (player == -1) player = this.Players.Count - 1;
                }
            }
            this.Players[this.Turn].CanMove = false;
            this.Turn = this.NextPlayer();
            this.IdleTurn = this.Turn;
            this.Players[this.Turn].CanMove = true;
            this.DrawnCard = 255;
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
                return this.Players[this.Turn].Name == this.Connection.Nickname;
            }
        }
    }
}
