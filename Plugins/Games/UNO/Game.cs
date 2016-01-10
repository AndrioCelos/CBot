using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

using IRC;

using Newtonsoft.Json;
using System.IO;

namespace UNO {
    public class Game {
        public UNOPlugin Plugin { get; }
        public IRCClient Connection { get; internal set; }
        public string Channel { get; internal set; }

        internal int index;

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
        public object[] HintParameters;

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
        public int DrawCount;

        public short CardsPlayed;
        public short TurnNumber;

        internal List<byte> Deck;
        internal List<byte> Discards;
        private int cardsDrawn;
        internal byte WildColour;

        internal GameRecord record = new GameRecord();

        internal Random RNG = new Random();
        internal object Lock = new object();
        internal static object LockShuffle = new object();  // Avoid multiple concurrent requests.

        public Game(UNOPlugin plugin, IRCClient connection, string channel, int entryTime) {
            this.index = plugin.GameCount;
            ++plugin.GameCount;

            this.Plugin = plugin;
            this.Players = new List<Player>(10);
            this.DrawnCard = 255;
            this.DrawFourBadColour = (byte) Colour.None;
            this.Deck = new List<byte>(108);
            this.Discards = new List<byte>(108);
            this.WildColour = (byte) Colour.None;
            this.RecordBreakers = new List<string>(4);
            this.DrawFourChallenger = -1;
            this.DrawFourUser = -1;
            this.DrawFourBadColour = 128;

            this.record = new GameRecord() { shuffles = new List<object>(), duration = TimeSpan.Zero, time = DateTime.UtcNow };

            this.Connection = connection;
            this.Channel = channel;
            this.GameTimer = new Timer(entryTime == 0 ? 60e+3 : entryTime * 1e+3) { AutoReset = false };
            this.HintTimer = new Timer() { AutoReset = false };

            // Populate the cards.
            this.Discards.AddRange(new byte[] {
                 0,  1,  1,  2,  2,  3,  3,  4,  4,  5,  5,  6,  6,  7,  7,  8,  8,  9,  9, 10, 10, 11, 11, 12, 12,
                16, 17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 23, 24, 24, 25, 25, 26, 26, 27, 27, 28, 28,
                32, 33, 33, 34, 34, 35, 35, 36, 36, 37, 37, 38, 38, 39, 39, 40, 40, 41, 41, 42, 42, 43, 43, 44, 44,
                48, 49, 49, 50, 50, 51, 51, 52, 52, 53, 53, 54, 54, 55, 55, 56, 56, 57, 57, 58, 58, 59, 59, 60, 60,
                64, 64, 64, 64, 65, 65, 65, 65
            });

            // Shuffle the cards.
            var thread = new System.Threading.Thread(this.shuffle);
            thread.Start(true);
        }

        public void Shuffle() {
            this.shuffle(false);
        }
        private void shuffle(object initial) {
            lock (LockShuffle) {
                object deal = null; bool localShuffle = (this.Plugin.RandomOrgAPIKey == null);

                // Remove the up-card; this shouldn't be shuffled in.
                byte upcard = 128;
                if (!(bool) initial) {
                    upcard = this.Discards[this.Discards.Count - 1];
                    this.Discards.RemoveAt(this.Discards.Count - 1);
                }

                var cards = this.Discards.Select(this.getCardString).ToArray();

                if (localShuffle) {
                    deal = new GameRecord.FailedShuffle() { cards = cards, error = "Use of random.org was disabled." };
                } else {
                    try {
                        // Send the request to random.org
                        var response = Plugin.randomClient.GenerateIntegers(this.Discards.Count, 0, this.Discards.Count - 1, true, false);
                        deal = new GameRecord.Shuffle() { cards = cards, random = response.RandomObject["random"], signature = response.Signature };

                        this.Deck.Clear();
                        foreach (var i in response.Integers) {
                            this.Deck.Add(this.Discards[i]);
                        }

                    } catch (Exception ex) when (ex is TimeoutException || ex is System.Net.WebException) {
                        deal = new GameRecord.FailedShuffle() { cards = cards, error = ex.Message };
                        // The request failed; use the PRNG to shuffle cards.
                        localShuffle = true;
                    }
                }

                if (localShuffle) {
                    while (this.Discards.Count != 0) {
                        var i = this.RNG.Next(this.Discards.Count);
                        this.Deck.Add(this.Discards[i]);
                        this.Discards.RemoveAt(i);
                    }
                }

                this.record.shuffles.Add(deal);

                this.cardsDrawn = 0;
                this.Discards.Clear();
                if (!(bool) initial) this.Discards.Add(upcard);
            }
        }
        private string getCardString(byte card) {
            var s = new char[2];

            if ((card & 64) != 0) {
                // Wild card
                s[0] = 'W';
                if (card == 64)
                    s[1] = ' ';
                else if (card == 65)
                    s[1] = 'D';
                else
                    s[1] = '?';
            } else {
                switch (card & 48) {
                    case  0: s[0] = 'R'; break;
                    case 16: s[0] = 'Y'; break;
                    case 32: s[0] = 'G'; break;
                    case 48: s[0] = 'B'; break;
                    default: s[0] = '?'; break;
                }
                switch (card & 15) {
                    case  0: s[1] = '0'; break;
                    case  1: s[1] = '1'; break;
                    case  2: s[1] = '2'; break;
                    case  3: s[1] = '3'; break;
                    case  4: s[1] = '4'; break;
                    case  5: s[1] = '5'; break;
                    case  6: s[1] = '6'; break;
                    case  7: s[1] = '7'; break;
                    case  8: s[1] = '8'; break;
                    case  9: s[1] = '9'; break;
                    case 10: s[1] = 'R'; break;
                    case 11: s[1] = 'S'; break;
                    case 12: s[1] = 'D'; break;
                    default: s[1] = '?'; break;
                }
            }
            return new string(s);
        }

        public byte DrawCard() {
            var b = this.Deck[this.cardsDrawn];
            ++this.cardsDrawn;
            return b;
        }
        public bool EndOfDeck => this.cardsDrawn == this.Deck.Count;

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
                int playerIndex = this.IndexOf(this.Connection.Me.Nickname);
                return (playerIndex != -1 && this.Players[playerIndex].CanMove);
            }
        }

        public void WriteRecord() {
            Directory.CreateDirectory(Path.Combine("data", this.Plugin.Key, "games"));
            using (var writer = new StreamWriter(Path.Combine("data", this.Plugin.Key, "games", this.index.ToString() + ".json"))) {
                writer.Write(JsonConvert.SerializeObject(this.record));
            }
        }
    }
}
