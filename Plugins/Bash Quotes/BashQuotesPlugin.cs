using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CBot;
using AnIRC;

namespace BashQuotes {
    [ApiVersion(3, 6)]
    public class BashQuotesPlugin : Plugin {
        private System.Timers.Timer QuoteTimer;
        private Task getQuotesTask;

        QuoteSource source1;
        public QuoteSource LastSource {
            get { return this.source1; }
        }

        private QuoteSource source;
        public QuoteSource Source {
            get {
                return this.source;
            }
            set {
                if (value == null) throw new ArgumentNullException();
                this.source = value;
            }
        }

        private SortedDictionary<int, Quote> Quotes1;
        private SortedDictionary<int, Quote> Quotes2;
        private int Index;
        private int FailureCount;
        private string FailureMessage;

        public readonly string UserAgent = "CBot-Quotes/" + typeof(BashQuotesPlugin).Assembly.GetName().Version.ToString(2) + " (annihilator127@gmail.com)";
        public static Regex Regex = new Regex(@"<p class=""quote""><a (?>[^>]*)><b>#(\d+)</b>.*?\((?:<font (?>[^>]*)>)?(-?\d+)(?:</font>)?\).*?<p class=""qt"">((?>[^<]*)(?:<br />(?>[^<]*))*)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        public override string Name => "Quotes";

        public override string Help(string topic, IrcMessageTarget target) {
            if (topic == null) return "Quotes are being provided in this channel.";
            return null;
        }

        public BashQuotesPlugin() {
            this.Initialise();
        }

        public override void OnUnload() {
            base.OnUnload();
            QuoteTimer.Dispose();
            Quotes1 = null;
            Quotes2 = null;
        }

        private void Initialise() {
            if (this.QuoteTimer == null) {
                this.source1 = new BashQuoteSource(UserAgent);
                this.source = this.source1;

                this.getQuotesTask = Task.Run(async () => await this.GetQuotes());

                this.QuoteTimer = new System.Timers.Timer(60e+3);
                QuoteTimer.Elapsed += QuoteTimer_Elapsed;
                this.QuoteTimer.Start();
            }
        }

        public static string Colourize(string line) {
            Match match = Regex.Match(line, @"^(\s*[<\[(\-]?[ +%@&!~]?)([A-}0-9-]+)([>\])\-]:?\s+.*)");
            if (match.Success)
                line = match.Groups[1].Value + Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
            else {
                match = Regex.Match(line, @"^([ +%@&!~]?)([A-}0-9-]+)(:\s+.*)");
                if (match.Success)
                    line = match.Groups[1].Value + Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
                else {
                    match = Regex.Match(line, @"^(\*+ )([A-}0-9-]+)(\s+(?!Now talking)(?!Topic)(?:Joins: |Parts: )?.*)");
                    if (match.Success)
                        line = match.Groups[1].Value + Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
                }
            }
            return line;
        }

        void QuoteTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (Quotes1 == null && Quotes2 != null) {
                Quotes1 = Quotes2;
                Quotes2 = null;
                this.QuoteTimer.Interval = 60e+3;
            }
            if (this.Channels.Length == 0) return;
            if (Index < Quotes1?.Count) {
                // Show a quote.
                this.SayToAllChannels(string.Format("\u0002-------- {2} #{0} - \u0002Rating:\u0002 {1} --------", Quotes1.Keys.ElementAt(Index), Quotes1.Values.ElementAt(Index).Rating, this.source1.Name));
                foreach (string line in Quotes1.Values.ElementAt(Index).Text.Split(new string[] { "<br />", '\r'.ToString(), '\n'.ToString() }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (line.Length <= 4) continue;

                    string newLine = BashQuotesPlugin.Colourize(line);

                    Thread.Sleep(1000);
                    if (newLine.Length > 350) {
                        int i;
                        for (i = 350; i >= 300; --i)
                            if (newLine[i] == ' ' || newLine[i] == (char) 9)
                                break;
                        this.SayToAllChannels(newLine.Substring(0, i));
                        Thread.Sleep(1000);
                        this.SayToAllChannels("    " + newLine.Substring(i + 1));
                    } else
                        this.SayToAllChannels(newLine);
                }
                ++Index;
            } else if (this.FailureMessage != null) {
                this.SayToAllChannels(this.FailureMessage);
                this.QuoteTimer.Interval = 20 * 60e+3;
                this.FailureMessage = null;
                return;
            }

            // Check whether we need to download new quotes.
            if (Quotes2 == null) {
                if (this.getQuotesTask.IsCompleted && (Quotes1 == null || Index >= Quotes1.Count - 5)) {
                    this.getQuotesTask = Task.Run(async () => await this.GetQuotes());
                }
            } else {
                // Swap the second list in when we reach the end of the first list.
                if (Quotes1 == null || Index >= Quotes1.Count) {
                    Quotes1 = Quotes2;
                    Quotes2 = null;
                    Index = 0;
                }
            }
        }

        private async Task GetQuotes() {
            try {
                SortedDictionary<int, Quote> result = await source.GetQuotes();
                if (result == null || result.Count == 0) {
                    ++this.FailureCount;
                    if (this.FailureCount == 3) {
                        this.FailureMessage = "\u00034No quotes were found three times in a row. Retrying in 20 minutes.";
                    }
                } else {
                    this.FailureCount = 0;
                    this.Quotes2 = result;
                    this.QuoteTimer.Interval = 60e+3;
                }
            } catch (Exception ex) {
                this.FailureMessage = "\u00034Failed to download the quotes page: " + ex.Message;
                this.LogError("GetQuotes", ex);
            }
        }
    }
}
