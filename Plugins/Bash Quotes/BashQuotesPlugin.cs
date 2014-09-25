﻿using System;
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
    }

    [APIVersion(3, 0)]
    public class BashQuotesPlugin : Plugin
    {
        private System.Timers.Timer QuoteTimer;
        private Thread GetQuotesThread;

        private SortedDictionary<int, Quote> Quotes1;
        private SortedDictionary<int, Quote> Quotes2;
        private int Index;
        private int FailureCount;
        private string FailureMessage;

        public const string UserAgent = "CBot (annihilator127@gmail.com)";
        public static Regex Regex = new Regex(@"<p class=""quote""><a (?>[^>]*)><b>#(\d+)</b>.*?\((?:<font (?>[^>]*)>)?(-?\d+)(?:</font>)?\).*?<p class=""qt"">((?>[^<]*)(?:<br />(?>[^<]*))*)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public override string Name {
            get {
                return "Bash Quotes";
            }
        }

        public override string Help(string Topic) {
            if (Topic == null) return "bash.org quotes are being provided in this channel.";
            return null;
        }

        public BashQuotesPlugin() {
            this.Initialise();
        }

        public override void OnUnload() {
            base.OnUnload();
            QuoteTimer.Dispose();
            GetQuotesThread = null;
            Quotes1 = null;
            Quotes2 = null;
        }

        private void Initialise() {
            if (this.QuoteTimer == null) {
                this.QuoteTimer = new System.Timers.Timer(60e+3);
                QuoteTimer.Elapsed += QuoteTimer_Elapsed;
                this.QuoteTimer.Start();
                this.GetQuotesThread = new Thread(GetQuotes);
                this.GetQuotesThread.Start();
            }
        }

        void QuoteTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (Quotes1 == null && Quotes2 != null) {
                Quotes1 = Quotes2;
                Quotes2 = null;
            }
            if (this.Channels.Length == 0) return;
            if (Quotes1 != null && Index < Quotes1.Count) {
                // Show a quote.
                this.SayToAllChannels(string.Format("\u0002-------- bash #{0} - \u0002Rating:\u0002 {1} --------", Quotes1.Keys.ElementAt(Index), Quotes1.Values.ElementAt(Index).Rating));
                foreach (string line in Quotes1.Values.ElementAt(Index).Text.Split(new string[] { "<br />", '\r'.ToString(), '\n'.ToString() }, StringSplitOptions.RemoveEmptyEntries)) {
                    if (line.Length <= 4) continue;
                    Thread.Sleep(1000);
                    if (line.Length > 350) {
                        int i;
                        for (i = 350; i >= 300; --i)
                            if (line[i] == ' ' || line[i] == (char) 9)
                                break;
                        this.SayToAllChannels(line.Substring(0, i));
                        Thread.Sleep(1000);
                        this.SayToAllChannels("    " + line.Substring(i + 1));
                    } else
                        this.SayToAllChannels(line);
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
                if (!GetQuotesThread.IsAlive && (Quotes1 == null || Index >= Quotes1.Count - 5)) {
                    GetQuotesThread = new Thread(GetQuotes);
                    GetQuotesThread.Start();
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

                        Quotes2.Add(int.Parse(match.Groups[1].Value), new Quote() {
                            Rating = int.Parse(match.Groups[2].Value),
                            Text = System.Web.HttpUtility.HtmlDecode(match.Groups[3].Value)
                        });
                    }
                    Console.WriteLine("[Bash Quotes] Got {0} new quotes.", Quotes2.Count);
                }
            } catch (Exception ex) {
                this.FailureMessage = "\u00034Failed to download the quotes page: " + ex.Message;
                this.LogError("GetQuotes", ex);
            }
        }
    }
}