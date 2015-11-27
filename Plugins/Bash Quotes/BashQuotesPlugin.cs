using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CBot;

namespace BashQuotes
{
    public struct Quote {
        public string Text;
        public int Rating;

        public Quote(string text) {
            this.Text = text;
            this.Rating = 0;
        }
        public Quote(string text, int rating) {
            this.Text = text;
            this.Rating = rating;
        }
    }

    [APIVersion(3, 2)]
    public class BashQuotesPlugin : Plugin
    {
        private System.Timers.Timer QuoteTimer;
        private Task getQuotesTask;

        QuoteSource source1;
        public QuoteSource LastSource {
            get { return this.source1; }
        }

        private QuoteSource source2;
        public QuoteSource Source {
            get {
                return this.source2;
            }
            set {
                if (value == null) throw new ArgumentNullException();
                this.source2 = value;
            }
        }

        private SortedDictionary<int, Quote> Quotes1;
        private SortedDictionary<int, Quote> Quotes2;
        private int Index;
        private int FailureCount;
        private string FailureMessage;

        public const string UserAgent = "CBot-Quotes/1.3 (annihilator127@gmail.com)";
        public static Regex Regex = new Regex(@"<p class=""quote""><a (?>[^>]*)><b>#(\d+)</b>.*?\((?:<font (?>[^>]*)>)?(-?\d+)(?:</font>)?\).*?<p class=""qt"">((?>[^<]*)(?:<br />(?>[^<]*))*)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public override string Name {
            get {
                return "Bash Quotes";
            }
        }

        public override string Help(string Topic) {
            if (Topic == null) return "Quotes are being provided in this channel.";
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
                this.source2 = this.source1;

                this.getQuotesTask = Task.Run(async () => await this.GetQuotes());

                this.QuoteTimer = new System.Timers.Timer(60e+3);
                QuoteTimer.Elapsed += QuoteTimer_Elapsed;
                this.QuoteTimer.Start();
            }
        }

        public static string Colourize(string line) {
            Match match = Regex.Match(line, @"^(\s*[<\[(\-]?[ +%@&!~]?)([A-}0-9-]+)([>\])\-]:?\s+.*)");
            if (match.Success)
                line = match.Groups[1].Value + IRC.Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
            else {
                match = Regex.Match(line, @"^([ +%@&!~]?)([A-}0-9-]+)(:\s+.*)");
                if (match.Success)
                    line = match.Groups[1].Value + IRC.Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
                else {
                    match = Regex.Match(line, @"^(\*+ )([A-}0-9-]+)(\s+(?!Now talking)(?!Topic)(?:Joins: |Parts: )?.*)");
                    if (match.Success)
                        line = match.Groups[1].Value + IRC.Colours.NicknameColour(match.Groups[2].Value) + match.Groups[2].Value + "\u000F" + match.Groups[3].Value;
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
        
        /*
        public void GetQuotes() {
            try {
                TcpClient client = new TcpClient();

                DateTime waitStart = DateTime.Now;
                string response; StringBuilder data = new StringBuilder(); int state = 0;
                string contentType = null;
                byte[] buffer = new byte[1024]; int n; bool cr = false;

                // Connect and send the request.
                Console.WriteLine("[Bash Quotes] Connecting to bash.org...");
                client.Connect("bash.org", 80);

                Console.WriteLine("[Bash Quotes] Connected in {0} ms.", (DateTime.Now - waitStart).TotalMilliseconds);

                StreamWriter writer = new StreamWriter(client.GetStream());
                writer.WriteLine("GET /?random HTTP/1.1");
                writer.WriteLine("Host: bash.org");
                writer.WriteLine("User-Agent: {0}", BashQuotesPlugin.UserAgent);
                writer.WriteLine("Accept: text/html");
                writer.WriteLine("Connection: Close");
                writer.WriteLine();
                writer.Flush();

                Console.WriteLine("[Bash Quotes] Downloading data...");
                // Wait for a response.
                do {
                    n = client.GetStream().Read(buffer, 0, 1024);
                    if (n == 0) break;
                    for (int i = 0; i < n; ++i) {
                        if (state == 2)
                            data.Append((char) buffer[i]);
                        else if (data.Length > 0 && buffer[i] == 10 && data[data.Length - 1] == '\r') {
                            // End of the line
                            if (state == 0) {
                                // Response code
                                response = data.ToString();
                                response = response.Split(new char[] { ' ' }, 3).ElementAtOrDefault(1);
                                if (response == "200") {
                                    // HTTP 200 OK
                                    state = 1;
                                } else {
                                    throw new WebException(string.Format("Received a HTTP {0}.", response));
                                }
                            } else {
                                // Header
                                if (data.Length == 0) {
                                    // A blank line indicates the end of the headers.
                                    if (contentType != null && contentType.Equals("text/html", StringComparison.OrdinalIgnoreCase)) {
                                        // Not HTML data.
                                        throw new WebException("The document isn't a HTML page.");
                                    }
                                } else {
                                    string[] fields = data.ToString().Split(new char[] { ':' }, 2);
                                    string key = fields[0]; string value = fields.ElementAtOrDefault(1);

                                    switch (key.ToUpper()) {
                                        case "CONTENT-TYPE":
                                            contentType = value;
                                            break;
                                    }
                                }
                            }
                            data.Clear();
                            cr = false;
                        } else {
                            if (cr) {
                                data.Append('\r');
                                cr = false;
                            }
                            if (buffer[i] == 13)
                                cr = true;
                            else
                                data.Append((char) buffer[i]);
                        }
                    }
                } while ((DateTime.Now - waitStart) < TimeSpan.FromSeconds(30));
                if (data == null) throw new WebException("The request timed out.");
                client.Close();
                File.WriteAllText("bash.html", data.ToString());

                Console.WriteLine("[Bash Quotes] Parsing data...");
                MatchCollection matches = Regex.Matches(data.ToString());

                Quotes2 = new SortedDictionary<int, Quote>();
                if (matches.Count == 0) {
                    ++this.FailureCount;
                    if (this.FailureCount == 3) {
                        this.FailureMessage = "\u00034No quotes were found three times in a row. Retrying in 20 minutes.";
                    }
                    Console.WriteLine("[Bash Quotes] Did not find any quotes. Failure count: {1}", Quotes2.Count, this.FailureCount);
                } else {
                    this.FailureCount = 0;
                    this.QuoteTimer.Interval = 60e+3;
                    foreach (Match match in matches) {
                        if (match.Groups[3].Value == "") continue;

                        // Reject quotes that are more than 20 lines long.
                        int lines; int pos = -1;
                        for (lines = 1; lines <= 20; ++lines) {
                            pos = match.Groups[3].Value.IndexOf("<br />", pos + 1);
                            if (pos == -1) break;
                        }
                        if (lines > 20) continue;

                        Quotes2.Add(int.Parse(match.Groups[1].Value), new Quote(System.Web.HttpUtility.HtmlDecode(match.Groups[3].Value), int.Parse(match.Groups[2].Value)));
                    }
                    Console.WriteLine("[Bash Quotes] Got {0} new quotes.", Quotes2.Count);
                }
            } catch (Exception ex) {
                this.FailureMessage = "\u00034Failed to download the quotes page: " + ex.Message;
                this.LogError("GetQuotes", ex);
            }
        }
         */

        private async Task GetQuotes() {
            try {
                SortedDictionary<int, Quote> result = await source2.GetQuotes();
                if (result == null || result.Count == 0) {
                    ++this.FailureCount;
                    if (this.FailureCount == 3) {
                        this.FailureMessage = "\u00034No quotes were found three times in a row. Retrying in 20 minutes.";
                    }
                } else {
                    this.FailureCount = 0;
                    this.Quotes2 = result;
                }
            } catch (Exception ex) {
                this.FailureMessage = "\u00034Failed to download the quotes page: " + ex.Message;
                this.LogError("GetQuotes", ex);
            }
        }
    }
}
