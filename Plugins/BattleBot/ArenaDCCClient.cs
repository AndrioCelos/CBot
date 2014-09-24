using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using IRC;

namespace BattleBot {
    internal class ArenaDCCClient : IRCClient {
        private TcpClient client;
        private StreamWriter clientWriter;
        private byte[] buffer;

        private string targetNickname;
        public string TargetNickname {
            get { return targetNickname; }
            set {
                if (this.IsRegistered)
                    this.ReceivedLine(string.Format(":{0}!*@* NICK {1}", targetNickname, value));
                targetNickname = value;
            }
        }

        internal ArenaDCCClient(string key, string nickname, string targetNickname) {
            this.Address = "!" + key + "/DCCChat";
            this.Port = 0;
            this.Nickname = nickname;
        }

        public override void Connect() {
            try {
                this.client = new TcpClient(this.IP.ToString(), this.Port);
                this.clientWriter = new StreamWriter(client.GetStream());
            } catch (Exception) {
                return;
            }
            this.buffer = new byte[512];
            this.LastSpoke = DateTime.Now;
            this.VoluntarilyQuit = false;
            this.IsConnected = true;
            this.IsRegistered = true;
            this.ReceivedLine(":" + this.Nickname + "!*@* JOIN #");
            this.ReceivedLine(string.Format(":{0}!*@* JOIN #", targetNickname));
        }

        public override void Disconnect() {
            this.client.Close();
            this.IsConnected = false;
            this.IsRegistered = false;
        }

        public override void Send(string t) {
            string Prefix; string Command; string[] Parameters; string Trail = null;
            IRCClient.ParseIRCLine(t, out Prefix, out Command, out Parameters, out Trail, true);

            if ((Command.Equals("PRIVMSG", StringComparison.OrdinalIgnoreCase) || Command.Equals("NOTICE", StringComparison.OrdinalIgnoreCase)) && (Parameters[0] == "#" || IRCStringComparer.RFC1459CaseInsensitiveComparer.Equals(Parameters[0], targetNickname))) {
                // Emulate a channel message to # or PM to the target by sending it to them over the DCC connection.
                this.clientWriter.Write(t);
                this.clientWriter.Write("\r\n");
            }
        }

        public static void writeMessage(string message) {
            ConsoleColor originalBackground; ConsoleColor originalForeground;
            originalBackground = Console.BackgroundColor;
            originalForeground = Console.ForegroundColor;

            short colour = -1; short backgroundColour = -2; bool bold = false; bool italic = false; bool underline = false; bool strikethrough = false;
            int i = 0;

            while (true) {
                for (; i < message.Length; i++) {
                    char c = message[i];
                    if (c == '\u0002') {  // Bold
                        bold = !bold;
                        break;
                    } else if (c == '\u001C') {  // Italic
                        italic = !italic;
                        break;
                    } else if (c == '\u001F') {  // Underline
                        underline = !underline;
                        break;
                    } else if (c == '\u0013') {  // Strikethrough
                        strikethrough = !strikethrough;
                        break;
                    } else if (c == '\u0016') {  // Reverse
                        short num = colour;
                        backgroundColour = colour;
                        colour = num;
                        break;
                    } else if (c == '\u000F') {  // Reset
                        colour = -1;
                        backgroundColour = -2;
                        bold = false;
                        italic = false;
                        underline = false;
                        strikethrough = false;
                        break;
                    } else if (c == '\u0003') {  // Colour
                        Match match = Regex.Match(message.Substring(i), @"^\x03(\d\d?)(?:,(\d\d?))?");
                        if (match.Success) {
                            colour = short.Parse(match.Groups[1].Value);
                            if (match.Groups[2].Success) {
                                backgroundColour = short.Parse(match.Groups[2].Value);
                                if (backgroundColour == 99) backgroundColour = -2;
                            }
                        } else {
                            colour = -1;
                            backgroundColour = -2;
                        }
                        i += match.Length - 1;
                        break;
                    } else {
                        Console.Write(c);
                    }
                }
                if (i >= message.Length) break;
                switch (colour % 16) {
                    case -2: Console.ForegroundColor = originalBackground; break;
                    case -1: Console.ForegroundColor = originalForeground; break;
                    case 0: Console.ForegroundColor = ConsoleColor.White; break;
                    case 1: Console.ForegroundColor = ConsoleColor.Black; break;
                    case 2: Console.ForegroundColor = ConsoleColor.DarkBlue; break;
                    case 3: Console.ForegroundColor = ConsoleColor.DarkGreen; break;
                    case 4: Console.ForegroundColor = ConsoleColor.Red; break;
                    case 5: Console.ForegroundColor = ConsoleColor.DarkRed; break;
                    case 6: Console.ForegroundColor = ConsoleColor.DarkMagenta; break;
                    case 7: Console.ForegroundColor = ConsoleColor.DarkYellow; break;
                    case 8: Console.ForegroundColor = ConsoleColor.Yellow; break;
                    case 9: Console.ForegroundColor = ConsoleColor.Green; break;
                    case 10: Console.ForegroundColor = ConsoleColor.DarkCyan; break;
                    case 11: Console.ForegroundColor = ConsoleColor.Cyan; break;
                    case 12: Console.ForegroundColor = ConsoleColor.Blue; break;
                    case 13: Console.ForegroundColor = ConsoleColor.Magenta; break;
                    case 14: Console.ForegroundColor = ConsoleColor.DarkGray; break;
                    case 15: Console.ForegroundColor = ConsoleColor.Gray; break;
                }
                switch (backgroundColour % 16) {
                    case -2: Console.BackgroundColor = originalBackground; break;
                    case -1: Console.BackgroundColor = originalForeground; break;
                    case 0: Console.BackgroundColor = ConsoleColor.White; break;
                    case 1: Console.BackgroundColor = ConsoleColor.Black; break;
                    case 2: Console.BackgroundColor = ConsoleColor.DarkBlue; break;
                    case 3: Console.BackgroundColor = ConsoleColor.DarkGreen; break;
                    case 4: Console.BackgroundColor = ConsoleColor.Red; break;
                    case 5: Console.BackgroundColor = ConsoleColor.DarkRed; break;
                    case 6: Console.BackgroundColor = ConsoleColor.DarkMagenta; break;
                    case 7: Console.BackgroundColor = ConsoleColor.DarkYellow; break;
                    case 8: Console.BackgroundColor = ConsoleColor.Yellow; break;
                    case 9: Console.BackgroundColor = ConsoleColor.Green; break;
                    case 10: Console.BackgroundColor = ConsoleColor.DarkCyan; break;
                    case 11: Console.BackgroundColor = ConsoleColor.Cyan; break;
                    case 12: Console.BackgroundColor = ConsoleColor.Blue; break;
                    case 13: Console.BackgroundColor = ConsoleColor.Magenta; break;
                    case 14: Console.BackgroundColor = ConsoleColor.DarkGray; break;
                    case 15: Console.BackgroundColor = ConsoleColor.Gray; break;
                }
                if (bold) {
                    if (Console.ForegroundColor >= ConsoleColor.DarkBlue && Console.ForegroundColor <= ConsoleColor.DarkYellow)
                        Console.ForegroundColor += 8;
                    else if (Console.ForegroundColor == ConsoleColor.DarkGray)
                        Console.ForegroundColor = ConsoleColor.Gray;
                    else if (Console.ForegroundColor == ConsoleColor.Gray)
                        Console.ForegroundColor = ConsoleColor.White;
                }
                i++;
            }
            Console.WriteLine();
            Console.BackgroundColor = originalBackground;
            Console.ForegroundColor = originalForeground;
        }

        public void Put(string Text) {
            this.ReceivedLine(":User!User@console PRIVMSG " + this.Nickname + " :" + Text);
        }
    }
}
