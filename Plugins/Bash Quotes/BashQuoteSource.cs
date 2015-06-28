using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using CBot;

namespace BashQuotes {
    public class BashQuoteSource : QuoteSource {
        public override string Name { get { return "Bash"; } }
        public static Regex Regex = new Regex(@"<p class=""quote""><a (?>[^>]*)><b>#(\d+)</b>.*?\((?:<font (?>[^>]*)>)?(-?\d+)(?:</font>)?\).*?<p class=""qt"">((?>[^<]*)(?:<br />(?>[^<]*))*)</p>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public string UserAgent { get; private set; }

        public BashQuoteSource(string userAgent) {
            this.UserAgent = userAgent;
        }

        public override async Task<SortedDictionary<int, Quote>> GetQuotes() {
            ConsoleUtils.WriteLine("[Bash Quotes] Downloading the page...");
            string data;

            using (HttpClient client = new HttpClient()) {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                client.DefaultRequestHeaders.UserAgent.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", this.UserAgent);

                data = await client.GetStringAsync(new Uri("http://bash.org/?random"));
            }

            ConsoleUtils.WriteLine("[Bash Quotes] Parsing data...");
            MatchCollection matches = Regex.Matches(data);

            if (matches.Count == 0) return null;

            var quotes = new SortedDictionary<int, Quote>();

            foreach (Match match in matches) {
                if (match.Groups[3].Value == "") continue;

                // Reject quotes that are more than 20 lines long.
                int lines; int pos = -1;
                for (lines = 1; lines <= 20; ++lines) {
                    pos = match.Groups[3].Value.IndexOf("<br />", pos + 1);
                    if (pos == -1) break;
                }
                if (lines > 20) continue;

                quotes.Add(int.Parse(match.Groups[1].Value), new Quote(HttpUtility.HtmlDecode(match.Groups[3].Value), int.Parse(match.Groups[2].Value)));
            }
            Console.WriteLine("[Bash Quotes] Got {0} new quotes.", quotes.Count);
            return quotes;
        }
    }
}
