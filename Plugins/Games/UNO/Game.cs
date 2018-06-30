using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

using AnIRC;

using Newtonsoft.Json;
using System.IO;

namespace UNO {
	public class Game {
		public UnoPlugin Plugin { get; }
		public IrcClient Connection { get; internal set; }
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
		public Card DrawnCard;
		public Colour DrawFourBadColour;
		public int DrawFourUser;
		public int DrawFourChallenger;
		public int DrawCount;

		public short CardsPlayed;
		public short TurnNumber;

		internal List<Card> Deck;
		internal List<Card> Discards;
		private int cardsDrawn;
		internal Colour WildColour;

		internal GameRecord record = new GameRecord();

		internal Random RNG = new Random();
		internal object Lock = new object();
		internal static object LockShuffle = new object();  // Avoid multiple concurrent requests.

		public Card UpCard => this.Discards[this.Discards.Count - 1];

		public Game(UnoPlugin plugin, IrcClient connection, string channel, int entryTime) {
			this.index = plugin.GameCount;
			++plugin.GameCount;

			this.Plugin = plugin;
			this.Players = new List<Player>(10);
			this.DrawnCard = Card.None;
			this.DrawFourBadColour = Colour.None;
			this.Deck = new List<Card>(108);
			this.Discards = new List<Card>(108);
			this.WildColour = Colour.None;
			this.RecordBreakers = new List<string>(4);
			this.DrawFourChallenger = -1;
			this.DrawFourUser = -1;
			this.DrawFourBadColour = Colour.None;

			this.record = new GameRecord() { shuffles = new List<object>(), duration = TimeSpan.Zero, time = DateTime.UtcNow };

			this.Connection = connection;
			this.Channel = channel;
			this.GameTimer = new Timer(entryTime == 0 ? 60e+3 : entryTime * 1e+3) { AutoReset = false };
			this.HintTimer = new Timer() { AutoReset = false };

			// Populate the cards.
			this.Discards.AddRange(new Card[] {
				(Card)  0, (Card)  1, (Card)  1, (Card)  2, (Card)  2, (Card)  3, (Card)  3, (Card)  4, (Card)  4, (Card)  5, (Card)  5, (Card)  6, (Card)  6, (Card)  7, (Card)  7, (Card)  8, (Card)  8, (Card)  9, (Card)  9, (Card) 10, (Card) 10, (Card) 11, (Card) 11, (Card) 12, (Card) 12,
				(Card) 16, (Card) 17, (Card) 17, (Card) 18, (Card) 18, (Card) 19, (Card) 19, (Card) 20, (Card) 20, (Card) 21, (Card) 21, (Card) 22, (Card) 22, (Card) 23, (Card) 23, (Card) 24, (Card) 24, (Card) 25, (Card) 25, (Card) 26, (Card) 26, (Card) 27, (Card) 27, (Card) 28, (Card) 28,
				(Card) 32, (Card) 33, (Card) 33, (Card) 34, (Card) 34, (Card) 35, (Card) 35, (Card) 36, (Card) 36, (Card) 37, (Card) 37, (Card) 38, (Card) 38, (Card) 39, (Card) 39, (Card) 40, (Card) 40, (Card) 41, (Card) 41, (Card) 42, (Card) 42, (Card) 43, (Card) 43, (Card) 44, (Card) 44,
				(Card) 48, (Card) 49, (Card) 49, (Card) 50, (Card) 50, (Card) 51, (Card) 51, (Card) 52, (Card) 52, (Card) 53, (Card) 53, (Card) 54, (Card) 54, (Card) 55, (Card) 55, (Card) 56, (Card) 56, (Card) 57, (Card) 57, (Card) 58, (Card) 58, (Card) 59, (Card) 59, (Card) 60, (Card) 60,
				(Card) 64, (Card) 64, (Card) 64, (Card) 64, (Card) 65, (Card) 65, (Card) 65, (Card) 65
			});

			// Shuffle the cards.
			var thread = new System.Threading.Thread(p => this.shuffle((bool) p));
			thread.Start(true);
		}

		public void Shuffle() {
			this.shuffle(false);
		}
		private void shuffle(bool initial) {
			lock (LockShuffle) {
				object deal = null; bool localShuffle = (this.Plugin.RandomOrgAPIKey == null);

				// Remove the up-card; this shouldn't be shuffled in.
				var upcard = default(Card);
				if (!initial) {
					upcard = this.UpCard;
					this.Discards.RemoveAt(this.Discards.Count - 1);
				}

				var cards = this.Discards.Select(c => c.ToString()).ToArray();

				if (localShuffle) {
					deal = new GameRecord.FailedShuffle() { cards = cards, error = "Use of random.org was disabled." };
				} else {
					try {
						// Send the request to random.org.
						var response = Plugin.randomClient.GenerateIntegers(this.Discards.Count, 0, this.Discards.Count - 1, true, false);
						deal = new GameRecord.Shuffle() { cards = cards, random = response.RPCObject, signature = response.Signature };

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
				if (!initial) this.Discards.Add(upcard);
			}
		}

		public Card DrawCard() {
			var card = this.Deck[this.cardsDrawn];
			++this.cardsDrawn;
			return card;
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
			this.DrawnCard = Card.None;
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
