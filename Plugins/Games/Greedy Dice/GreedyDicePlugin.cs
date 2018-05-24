using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CBot;
using AnIRC;

namespace GreedyDice {
	[ApiVersion(3, 7)]
	public class GreedyDicePlugin : Plugin {
        private static readonly int[] RollValues = new int[] { 0, 0, 50, 50, 100, 150 };
        private static readonly string[] RollFaces = new string[] { "\u00031,8 X ", "\u00031,8 X ", "\u000313,6 ♥ ", "\u00039,3 * ", "\u00034,5* *", "\u000312,2***" };

        public Dictionary<string, Game> Games;

        public int EntryTime;
        public int EntryWaitLimit;
        public int TurnTime;
        public int TurnWaitLimit;

        public int TurnsPerGame { get; set; }
        public int PointsPerGame { get; set; }
        public WinCondition WinCondition { get; set; }
        public bool AIEnabled { get; set; }

        public override string Name => "Greedy Dice game";

        public GreedyDicePlugin(string key) {
            this.Games = new Dictionary<string, Game>(StringComparer.InvariantCultureIgnoreCase);

            this.TurnsPerGame = 4;
            this.PointsPerGame = 4000;
            this.WinCondition = WinCondition.Turns;
            this.AIEnabled = true;
            this.EntryTime = 30;
            this.TurnTime = 120;
            this.EntryWaitLimit = 120;
            this.TurnWaitLimit = 240;

            this.LoadConfig(key);
        }

        public override void OnSave() {
            this.SaveConfig();
        }

        #region Filing
        public void LoadConfig(string key) {
            string filename = Path.Combine("Config", key + ".ini");
            if (!File.Exists(filename)) return;
            StreamReader reader = new StreamReader(filename);
            int lineNumber = 0;
            string section = null;

            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                lineNumber++;
                if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

                Match match = Regex.Match(line, @"^\s*\[(.*)\]\s*$");
                if (match.Success) {
                    section = match.Groups[1].Value;
                    Match match2 = Regex.Match(section, @"(?:Player:|(?!Game|Rules|Scoring))(.*)", RegexOptions.IgnoreCase);
                } else {
                    match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
                    if (match.Success) {
                        string field = match.Groups[1].Value;
                        string value = match.Groups[2].Value;
                        bool value2; int value3;

                        if (section == null) continue;
                        switch (section.ToUpper()) {
                            case "GAME":
                                switch (field.ToUpper()) {
                                    case "AI":
                                        if (Bot.TryParseBoolean(value, out value2)) this.AIEnabled = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "ENTRYTIME":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.EntryTime = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    case "ENTRYWAITLIMIT":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.EntryWaitLimit = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    case "TURNTIME":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.TurnTime = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    case "TURNWAITLIMIT":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.TurnWaitLimit = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
                            default:
                                if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): found a stray field or unknown section.", this.Key, lineNumber);
                                break;
                        }
                    }
                }
            }
            reader.Close();
        }

        public void SaveConfig() {
            if (!Directory.Exists("Config"))
                Directory.CreateDirectory("Config");
            using (StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false)) {
                writer.WriteLine("[Game]");
                writer.WriteLine("AI={0}", this.AIEnabled ? "On" : "Off");
                writer.WriteLine("EntryTime={0}", this.EntryTime);
                writer.WriteLine("EntryWaitLimit={0}", this.EntryWaitLimit);
                writer.WriteLine("TurnTime={0}", this.TurnTime);
                writer.WriteLine("TurnWaitLimit={0}", this.TurnWaitLimit);
                writer.Close();
            }
        }
        #endregion

        [Command(new string[] { "set", "dset" }, 1, 2, "set <property> <value>", "Changes settings for this plugin.")]
        public async void CommandSet(object sender, CommandEventArgs e) {
            string property = e.Parameters[0];
            string value = e.Parameters.Length == 1 ? null : e.Parameters[1];
            int value2; bool value3;

            switch (property.Replace(" ", "").Replace("-", "").ToUpperInvariant()) {
                case "AI":
                    if (!await SetPermissionCheckAsync(e)) return;
                    if (value == null) {
                        if (this.AIEnabled)
                            e.Reply("I \u00039will\u000F join Greedy Dice games.");
                        else
                            e.Reply("I \u00034will not\u000F join Greedy Dice games.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.AIEnabled = value3)
                            e.Reply("I \u00039will now\u000F join Greedy Dice games.");
                        else
                            e.Reply("I \u00034will no longer\u000F join Greedy Dice games.");
                    } else
                        e.Whisper(string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "ENTRYTIME":
                case "ENTRYPERIOD":
                case "STARTTIME":
                case "ENTRY":
                    if (!await SetPermissionCheckAsync(e)) return;
                    if (value == null) {
                        e.Reply("The entry period is \u0002{0}\u0002 seconds.", this.EntryTime);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.EntryTime = value2;
                            e.Reply("The entry period is now \u0002{0}\u0002 seconds.", this.EntryTime);
                        } else
                            e.Whisper("The number must be positive.", value);
                    } else
                        e.Whisper(string.Format("That isn't a valid integer.", value));
                    break;
                case "ENTRYWAITLIMIT":
                    if (!await SetPermissionCheckAsync(e)) return;
                    if (value == null) {
                        e.Reply("The entry period may be extended to \u0002{0}\u0002 seconds.", this.EntryWaitLimit);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.EntryWaitLimit = value2;
                            e.Reply("The entry period may now be extended to \u0002{0}\u0002 seconds.", this.EntryWaitLimit);
                        } else
                            e.Whisper("The number must be positive.", value);
                    } else
                        e.Whisper(string.Format("That isn't a valid integer.", value));
                    break;
                case "TURNTIME":
                case "TIMELIMIT":
                case "IDLETIME":
                case "TIME":
                    if (!await SetPermissionCheckAsync(e)) return;
                    if (value == null) {
                        if (this.TurnTime == 0)
                            e.Reply("The turn time limit is disabled.", this.TurnTime);
                        else
                            e.Reply("The turn time limit is \u0002{0}\u0002 seconds.", this.TurnTime);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 >= 0) {
                            this.TurnTime = value2;
                            if (value2 == 0)
                                e.Reply("The turn time limit is now disabled.", this.TurnTime);
                            else
                                e.Reply("The turn time limit is now \u0002{0}\u0002 seconds.", this.TurnTime);
                            // Reset the existing turn timers.
                            foreach (Game game in this.Games.Values)
                                game.GameTimer.Interval = this.TurnTime == 0 ? 60e+3 : (this.TurnTime * 1e+3);
                        } else
                            e.Whisper("The number cannot be negative.", value);
                    } else
                        e.Whisper(string.Format("That isn't a valid integer.", value));
                    break;
                case "TURNWAITLIMIT":
                    if (!await SetPermissionCheckAsync(e)) return;
                    if (value == null) {
                        e.Reply("The turn time limit may be extended to \u0002{0}\u0002 seconds.", this.TurnWaitLimit);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.TurnWaitLimit = value2;
                            e.Reply("The turn time limit may now be extended to \u0002{0}\u0002 seconds.", this.TurnWaitLimit);
                        } else
                            e.Whisper("The number must be positive.", value);
                    } else
                        e.Whisper(string.Format("That isn't a valid integer.", value));
                    break;
                default:
                    e.Whisper(string.Format("I don't manage a setting named \u0002{0}\u0002.", property));
                    break;
            }
        }

        internal async Task<bool> SetPermissionCheckAsync(CommandEventArgs e) {
            if (await Bot.CheckPermissionAsync(e.Sender, this.Key + ".set"))
                return true;
            e.Reply("You don't have access to that setting.");
            return false;
        }



        [Command(new string[] { "join", "djoin" }, 0, 0, "djoin", "Enters you into a game of Greedy Dice.", Scope = CommandScope.Channel)]
        public void CommandJoin(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Target;
            if (this.Games.TryGetValue(key, out game))
                this.EntryCommand(game, e.Sender.Nickname);
            else {
                // Start a new game.
                game = new Game(e.Client, e.Target.Target, this.EntryTime);
                lock (game.Lock) {
                    this.Games.Add(key, game);
                    game.Players.Add(new Player(e.Sender.Nickname));
                    e.Reply("\u00039\u0002{0}\u0002 is starting a game of Greedy Dice!", e.Sender.Nickname);
                    game.GameTimer.Elapsed += GameTimer_Elapsed;
                    Thread.Sleep(600);

                    game.GameTimer.Start();
                    e.Reply("\u000312Starting in \u0002{0}\u0002 seconds. Say \u000311!djoin\u000312 if you wish to join the game.", this.EntryTime);
                }
            }
        }

        protected void EntryCommand(Game game, string nickname) {
            lock (game.Lock) {
                if (!game.IsOpen) {
                    Bot.Say(game.Connection, nickname, "Sorry {0}, but this game has already started.", nickname);
                    return;
                }
                if (game.Players.Any(player => game.Connection.CaseMappingComparer.Equals(player.Name, nickname))) {
                    Bot.Say(game.Connection, nickname, "You've already entered the game.", nickname);
                    return;
                }
                game.Players.Add(new Player(nickname));
                Bot.Say(game.Connection, game.Channel, "\u00039\u0002{0}\u0002 has joined the game.", nickname);
            }
        }

        [Command(new string[] { "quit", "dquit", "leave", "dleave", "part", "dpart" }, 0, 0, "uquit", "Removes you from the game of Greedy Dice.",
            Scope = CommandScope.Channel)]
        public void CommandQuit(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.Extensions.NetworkName + "/" + e.Target;
            if (!this.Games.TryGetValue(key, out game))
                e.Whisper("There's no game going on at the moment.");
            else {
                lock (game.Lock) {
                    int index = game.IndexOf(e.Sender.Nickname);
                    if (index == -1)
                        e.Whisper("You're not in this game.");
                    else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has left the game.", e.Sender.Nickname);
                        this.RemovePlayer(game, index);
                    }
                }
            }
        }

        public override bool OnNicknameChange(object sender, NicknameChangeEventArgs e) {
            this.RenamePlayer(((IrcClient) sender).Extensions.NetworkName, e.Sender.Nickname, e.NewNickname);
            return base.OnNicknameChange(sender, e);
        }
        public void RenamePlayer(string network, string oldName, string newName) {
            Game game;
            foreach (KeyValuePair<string, Game> entry in this.Games) {
                if (entry.Key.StartsWith(network, StringComparison.InvariantCultureIgnoreCase)) {
                    game = entry.Value;
                    lock (game.Lock) {
                        int index = game.IndexOf(oldName);
                        if (index != -1)
                            game.Players[index].Name = newName;
                    }
                }
            }
        }

        private void StartGameTimer(Game game) {
            game.NoTimerReset = false;
            game.TurnStartTime = DateTime.Now;
            game.WaitTime = this.TurnTime;
            if (this.TurnTime != 0) {
                game.GameTimer.Interval = this.TurnTime * 1000;
                game.GameTimer.Start();
            } else if (!game.Connection.Channels[game.Channel].Users.Contains(game.Players[game.IdleTurn].Name)) {
                // Start the timer if the current player is missing from the channel.
                game.GameTimer.Interval = 60e+3;
                game.WaitTime = 60;
                game.GameTimer.Start();
            }
        }

        private void GameTimer_Elapsed(object sender, ElapsedEventArgs e) {
            foreach (Game game in this.Games.Values) {
                if (game.GameTimer == sender) {
                    if (game.IsOpen) {
                        this.GameClose(game);
                        return;
                    } else {
                        this.IdleCheck(game);
                        return;
                    }
                }
            }
            ConsoleUtils.WriteLine("%cRED[{0}] Error: a game timer triggered, and I can't find which game it belongs to!", this.Key);
        }

        private void GameClose(Game game) {
            lock (game.Lock) {
                if (!game.IsOpen) return;

                // Enter the bot.
                if (game.Players.Count == 1 && this.AIEnabled) {
                    this.EntryCommand(game, game.Connection.Me.Nickname);
                }

                if (game.Players.Count < 2) {
                    Bot.Say(game.Connection, game.Channel, "\u000312Not enough players joined. Please say \u000311!djoin\u000312 when you're ready for a game.");
                    this.Games.Remove(game.Connection.Extensions.NetworkName + "/" + game.Channel);
                    return;
                }

                // Start the game.
                game.IsOpen = false;
                game.TurnNumber = 1;
                game.RNG = new Random();
                Bot.Say(game.Connection, game.Channel, "\u00039The game of Greedy Dice has started!");
                Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002, it's your turn. Enter \u000311{1}\u000312 to roll the dice.", game.Players[game.Turn].Name, Bot.ReplaceCommands("!droll", game.Connection, game.Channel));
                game.Players[game.Turn].CanMove = true;
                this.StartGameTimer(game);
            }
            this.AICheck(game);
        }

        public bool GameTurnCheck(IrcClient connection, string channel, string nickname, bool showMessages, out Game game, out int index) {
            if (!this.Games.TryGetValue(connection.NetworkName + "/" + channel, out game)) {
                if (showMessages) Bot.Say(connection, nickname, "There's no game going on at the moment.");
                index = -1;
                return false;
            } else if (game.IsOpen) {
                if (showMessages) Bot.Say(connection, nickname, "The game hasn't started yet!");
                index = -1;
                return false;
            } else {
                lock (game.Lock) {
                    index = game.IndexOf(nickname);
                    if (index == -1) {
                        if (showMessages) Bot.Say(connection, nickname, "You're not in this game, {0}.", nickname);
                        return false;
                    } else if (!game.Players[index].CanMove) {
                        if (showMessages) Bot.Say(connection, nickname, "It's not your turn.");
                        return false;
                    }
                }
            }
            return true;
        }

        [Command(new string[] { "roll", "droll" }, 0, 0, "roll", "Allows you to roll the dice for points during your turn.",
            Scope = CommandScope.Channel)]
        public void CommandRoll(object sender, CommandEventArgs e) {
            Game game; int playerIndex;
            if (!this.GameTurnCheck(e.Client, e.Target.Target, e.Sender.Nickname, true, out game, out playerIndex)) return;
            lock (game.Lock) {
                this.RollCheck(game, playerIndex);
            }
        }

        public void RollCheck(Game game, int playerIndex) {
            game.GameTimer.Stop();
            this.IdleSkip(game, playerIndex);
            int roll1 = game.RNG.Next(6);
            int roll2 = game.RNG.Next(6);

            int score = GreedyDicePlugin.RollValues[roll1] + GreedyDicePlugin.RollValues[roll2];

            if (score == 0) {  // A double X: you're out!
                if (game.TurnScore == 0) {
                    if (game.IsAIUp)
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 rolled \u0002{1}\u0003 {2}\u000312,99\u0002 Since no points have been scored yet, I won't count that.",
                            game.Players[playerIndex].Name, GreedyDicePlugin.RollFaces[roll1], GreedyDicePlugin.RollFaces[roll2]);
                    else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 rolled \u0002{1}\u0003 {2}\u000312,99\u0002 Since you haven't got any points, I won't count that.",
                            game.Players[playerIndex].Name, GreedyDicePlugin.RollFaces[roll1], GreedyDicePlugin.RollFaces[roll2]);
                        Thread.Sleep(600);
                        Bot.Say(game.Connection, game.Channel, Bot.ReplaceCommands("\u000312Say \u000311!droll\u000312 to roll the dice again.", game.Connection, game.Channel));
                    }
                    this.AICheck(game);
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 rolled \u0002{1}\u0003 {2}\u000312,99 Got too greedy! {0}\u0002 loses {3} score for this turn.",
                        game.Players[playerIndex].Name, GreedyDicePlugin.RollFaces[roll1], GreedyDicePlugin.RollFaces[roll2], GreedyDicePlugin.GetGender(game.Connection, game.Channel, game.Players[playerIndex].Name));
                    Thread.Sleep(1500);
                    this.NextPlayer(game, true);
                }
            } else {
                if (roll1 == roll2) {
                    score *= 2;
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 rolled \u0002{1}\u0003 {2}\u000312,99 Double! {0}\u0002 wins \u0002{3}\u0002 points.",
                        game.Players[playerIndex].Name, GreedyDicePlugin.RollFaces[roll1], GreedyDicePlugin.RollFaces[roll2], score);
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 rolled \u0002{1}\u0003 {2}\u000312,99\u0002 for \u0002{3}\u0002 points.",
                        game.Players[playerIndex].Name, GreedyDicePlugin.RollFaces[roll1], GreedyDicePlugin.RollFaces[roll2], score);
                }
                game.TurnScore += score;
                if (game.TurnScore == score && !game.IsAIUp) {
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Players[playerIndex].Name, Bot.ReplaceCommands("\u000312Enter \u000311!droll\u000312 to roll again, or \u000311!dpass\u000312 to take your score.", game.Connection, game.Channel));
                }
                this.StartGameTimer(game);
                this.AICheck(game);
            }
        }

        [Command(new string[] { "pass", "dpass" }, 0, 0, "pass", "Ends your turn, keeping all the points you've won.",
            Scope = CommandScope.Channel)]
        public void CommandPass(object sender, CommandEventArgs e) {
            Game game; int playerIndex;
            if (!this.GameTurnCheck(e.Client, e.Target.Target, e.Sender.Nickname, true, out game, out playerIndex)) return;
            lock (game.Lock) {
                this.PassCheck(game, playerIndex);
            }
        }

        public void PassCheck(Game game, int playerIndex) {
            if (playerIndex != game.Turn || game.TurnScore == 0) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You haven't rolled the dice yet!");
                return;
            }
            game.GameTimer.Stop();
            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 passes, taking \u0002{1}\u0002 points.",
                game.Players[playerIndex].Name, game.TurnScore);
            game.Players[playerIndex].Score += game.TurnScore;
            Thread.Sleep(1500);
            this.NextPlayer(game, true);
        }

        public void AICheck(Game game) {
            int playerIndex = game.IndexOf(game.Connection.Me.Nickname);
            if (playerIndex != -1 && game.Players[playerIndex].CanMove) {
                Thread AIThread = new Thread(() => this.AITurn(game));
                AIThread.Start();
            }
        }

        public void AITurn(Game game) {
            Thread.Sleep(2500);
            int playerIndex = game.IndexOf(game.Connection.Me.Nickname);
            if (playerIndex != -1 && game.Players[playerIndex].CanMove) {
                lock (game.Lock) {
                    int position = 1; int totalScore = game.Players[playerIndex].Score + game.TurnScore;
                    for (int i = 0; i < game.Players.Count; ++i) {
                        if (i != playerIndex && game.Players[i].Score > totalScore) ++position;
                    }
                    if (game.TurnScore >= 700 + 200 * position)
                        this.PassCheck(game, playerIndex);
                    else
                        this.RollCheck(game, playerIndex);
                }
            }
        }

        public void NextPlayer(Game game, bool enable) {
            game.TurnScore = 0;
            game.Players[game.Turn].CanMove = false;
            ++game.Turn;
            if (game.Turn == game.Players.Count) {
                if ((WinCondition & WinCondition.Turns) != 0) {
                    int turnsLeft = this.TurnsPerGame - game.TurnNumber;
                    if (turnsLeft <= 0) {
                        this.EndGame(game);
                        return;
                    } else {
                        if (turnsLeft == 1)
                            Bot.Say(game.Connection, game.Channel, "\u00039This is the \u0002last turn\u0002! Make it count!");
                        else
                            Bot.Say(game.Connection, game.Channel, "\u00039\u0002{0}\u0002 turns are left in this game.", turnsLeft);
                        ++game.TurnNumber;
                        game.Turn = 0;
                        Thread.Sleep(1000);
                        this.AnnounceScores(game, false);
                        Thread.Sleep(1000);
                    }
                }
            }
            game.IdleTurn = game.Turn;
            if (enable) {
                game.Players[game.Turn].CanMove = true;
                if (game.IsAIUp)
                    Bot.Say(game.Connection, game.Channel, "\u000312It's now \u0002my\u0002 turn.", game.Players[game.Turn].Name, Bot.ReplaceCommands("!droll", game.Connection, game.Channel));
                else
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002, it's your turn. Enter \u000311{1}\u000312 to roll the dice.", game.Players[game.Turn].Name, Bot.ReplaceCommands("!droll", game.Connection, game.Channel));
                this.StartGameTimer(game);
                this.AICheck(game);
            }
        }

        public void AnnounceScores(Game game, bool finalScores) {
            int topScore = 0;
            for (int i = 0; i < game.Players.Count; ++i) {
                if (game.Players[i].Quit) continue;
                if (game.Players[i].Score > topScore) topScore = game.Players[i].Score;
            }
            StringBuilder messageBuilder = new StringBuilder(200);
            if (finalScores)
                messageBuilder.Append("\u000312Final scores: ");
            else
                messageBuilder.Append("\u000312Current scores: ");

            for (int i = 0; i < game.Players.Count; ++i) {
                if (i != 0) messageBuilder.Append("\u000F | ");
                if (!finalScores && i == game.Turn)
                    messageBuilder.Append("\u000311");
                else
                    messageBuilder.Append("\u000302");
                messageBuilder.Append(game.Players[i].Name);
                messageBuilder.Append("\u0003 ");
                if (game.Players[i].Score == topScore)
                    messageBuilder.Append("\u000307");
                else
                    messageBuilder.Append("\u000303");
                messageBuilder.Append(game.Players[i].Score);
            }
            Bot.Say(game.Connection, game.Channel, messageBuilder.ToString());
        }

        public void IdleCheck(Game game) {
            bool AI = false;
            lock (game.Lock) {
                if (game.IdleTurn == -1) game.IdleTurn = game.Turn;

                // Remove the player if they left the channel.
                if ((this.TurnTime == 0 || game.Players[game.IdleTurn].DisconnectedAt - DateTime.Now >= TimeSpan.FromSeconds(60)) &&
                    !game.Connection.Channels[game.Channel].Users.Contains(game.Players[game.IdleTurn].Name)) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 left the channel, and has been removed from the game.", game.Players[game.IdleTurn].Name);
                    this.RemovePlayer(game, game.IdleTurn);
                } else if (this.TurnTime > 0) {
                    if (++game.Players[game.IdleTurn].IdleCount >= 2) {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has timed out twice in a row, and has been removed from the game. :-(", game.Players[game.IdleTurn].Name);
                        this.RemovePlayer(game, game.IdleTurn);
                    } else {
                        Bot.Say(game.Connection, game.Channel, "\u0002{0}\u0002 is taking too long...", game.Players[game.IdleTurn].Name);
                        // Advance the turn.
                        if (this.IdleAdvance(game))
                            AI = true;
                    }
                }
            }
            if (AI) this.AICheck(game);
        }

        public bool IdleAdvance(Game game) {
            int nextPlayer = game.IdleTurn;
            do {
                ++nextPlayer;
                if (nextPlayer == game.Players.Count) {
                    if (game.TurnNumber == this.TurnsPerGame) {
                        this.EndGame(game);
                        return false;
                    }
                    nextPlayer = 0;
                }
            } while (game.Players[nextPlayer].Quit);
            if (nextPlayer == game.Turn) {
                // Stop the game if everyone idles out and it goes full circle.
                this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
                Bot.Say(game.Connection, game.Channel, "\u00039The game has been cancelled.");
                return false;
            }
            game.IdleTurn = nextPlayer;
            game.Players[nextPlayer].CanMove = true;
            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 may play now.", game.Players[game.IdleTurn].Name);
            this.StartGameTimer(game);
            return true;
        }

        public void IdleSkip(Game game, int skipTo) {
            game.Players[skipTo].IdleCount = 0;
            if (game.IdleTurn != skipTo) {
                for (int i = 0; i < game.Players.Count; i++) {
                    if (i != skipTo) game.Players[i].CanMove = false;
                }
                game.IdleTurn = skipTo;
            }
            if (game.Turn == skipTo) return;
            if (game.TurnScore != 0) {
                Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 takes \u0002{1}\u0002 points.", game.Players[game.Turn].Name, game.TurnScore);
                game.Players[game.Turn].Score += game.TurnScore;
            }
            while (game.Turn != skipTo)
                this.NextPlayer(game, false);
        }

        public override bool OnChannelLeave(object sender, ChannelPartEventArgs e) {
            Game game;
            if (this.Games.TryGetValue(((IrcClient) sender).Extensions.NetworkName + "/" + e.Channel, out game)) {
                lock (game.Lock) {
                    int index = game.IndexOf(e.Sender.Nickname);
                    if (index != -1) {
                        if (game.IsOpen) {
                            // Remove the player immediately if they disconnect before the game starts.
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has left the game.", e.Sender.Nickname);
                            this.RemovePlayer(game, index);
                        } else {
                            game.Players[index].DisconnectedAt = DateTime.Now;
                            // Start the turn timer if it's this player's turn.
                            if (game.IdleTurn == index && !game.GameTimer.Enabled) {
                                game.TurnStartTime = DateTime.Now;
                                game.WaitTime = (this.TurnTime == 0 ? 60 : this.TurnTime);
                                game.GameTimer.Interval = game.WaitTime * 1000;
                                game.NoTimerReset = false;
                                game.GameTimer.Start();
                            }
                        }

                    }
                }
            }
            return base.OnChannelLeave(sender, e);
        }

        public void RemovePlayer(Game game, int index) {
            if (game == null) throw new ArgumentNullException("game");
            if (game.IsOpen) {
                game.Players.RemoveAt(index);
                if (game.Players.Count == 0) {
                    game.GameTimer.Dispose();
                    this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
                }
            } else {
                game.Players[index].Quit = true;

                // If only one player remains, declare them the winner.
                int survivor = -1;
                for (int i = 0; i < game.Players.Count; i++) {
                    if (!game.Players[i].Quit) {
                        if (survivor == -1)
                            survivor = i;
                        else {
                            survivor = -2;
                            break;
                        }
                    }
                }
                if (survivor != -2) {
                    game.GameTimer.Stop();
                    if (survivor != -1) {
                        game.Turn = survivor;
                    }
                    this.EndGame(game);
                } else {
                    // Was it the leaving player's turn?
                    if (game.Turn == index) {
                        this.NextPlayer(game, true);
                        Thread.Sleep(600);
                        this.StartGameTimer(game);
                        this.AICheck(game);
                    } else if (game.IdleTurn == index) {
                        // Advance the idle turn.
                        if (this.IdleAdvance(game))
                            this.AICheck(game);
                    }
                }
            }
        }

        public void EndGame(Game game) {
            List<string> winners = new List<string>(2); int topScore = 0;

            Bot.Say(game.Connection, game.Channel, "\u00039This game is now finished.");
            Thread.Sleep(3000);

            // It's time to announce the winner.
            for (int i = 0; i < game.Players.Count; ++i) {
                if (game.Players[i].Quit) continue;
                if (game.Players[i].Score > topScore) {
                    winners.Clear();
                    winners.Add(game.Players[i].Name);
                    topScore = game.Players[i].Score;
                } else if (game.Players[i].Score == topScore) {
                    winners.Add(game.Players[i].Name);
                }
            }
            if (topScore == 0) {
                Bot.Say(game.Connection, game.Channel, "\u000312No one scored at all this time...");
            } else {
                if (winners.Count > 1) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 and \u0002{1}\u0002 have won the game, each with \u0002{2}\u0002 points!",
                        string.Join("\u0002, \u0002", winners.Take(winners.Count - 1)), winners[winners.Count - 1], topScore);
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has won the game with \u0002{1}\u0002 points!",
                        winners[0], topScore);
                }
                Thread.Sleep(3000);
                this.AnnounceScores(game, true);
            }
            this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
            this.OnSave();
        }

        public static string GetGender(IrcClient client, string channel, string nickname) {
            IrcChannel _channel; IrcChannelUser user;
            if (client.Channels.TryGetValue(channel, out _channel)) {
                if (_channel.Users.TryGetValue(nickname, out user)) {
                    return user.User.GenderRefTheir.ToLowerInvariant();
                }
            }
            return "their";
        }

    }
}
