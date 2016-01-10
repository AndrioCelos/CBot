using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

using CBot;
using IRC;

using Demot.RandomOrgApi;

using Timer = System.Timers.Timer;

namespace UNO {
    [APIVersion(3, 2)]
    public class UNOPlugin : Plugin {
        public static readonly string[] Hints = new string[] {
            /*  0 */ "It's your turn. Enter \u0002!play \u001Fcard\u000F to play a card from your hand with a matching colour, number or symbol. Here, you can play a {0} card, a {1} or a Wild card. If you have none, enter \u0002!draw\u0002.",
            /*  1 */ "It's your turn. Enter \u0002!play \u001Fcard\u000F to play a card from your hand with a matching colour, number or symbol. Here, you can play a {0} card or a Wild card. If you have none, enter \u0002!draw\u0002.",
            /*  2 */ "It's your turn. Enter \u0002!play \u001Fcard\u000F to play a card from your hand with a matching colour, number or symbol. Here, no colour was chosen for the Wild card, so you may play anything. If you have none, enter \u0002!draw\u0002.",
            /*  3 */ "If the card you just drew is playable, you may play it now. Otherwise, enter \u0002!pass\u0002.",
            /*  4 */ "Your goal is to go out, by playing all of your cards, before the other players do. There are special action cards that, when played, can hinder the next player from doing this.",
            /*  5 */ "If you lose track of the game, try using these commands: \u0002!hand !upcard !count !turn !time",
            /*  6 */ "Reverse cards act like Skips with two players. Go again!",
            /*  7 */ "In UNO, you must call 'UNO!' when you're down to one card. I don't enforce this rule here, though.",
            /*  8 */ "Keep in mind that I enforce a time limit. If you time out twice in a row, you'll be presumed gone.",
            /*  9 */ "I'm afraid you've timed out. It's still your turn, and you may still play if {0} doesn't jump in first.",
            /* 10 */ "Remember, you're not allowed to play a Wild Draw Four if you hold a card whose colour matches the up-card. You may \u0002!challenge\u0002 this Wild Draw Four if you think it's illegal; otherwise, \u0002!draw\u0002.",
            /* 11 */ "If you want to stop seeing these hints, enter \u0002!uset hints off\u0002. If you would like them reset, enter \u0002!uset hints reset\u0002.",
            /* 12 */ "At the end of the game, those who go out are awarded points based on the cards their opponents still have. You can check the leaderboard with \u0002!utop\u0002.",
            /* 13 */ "Welcome to UNO! A guide to the game can be found at {0}.",
            /* 14 */ "If you want to leave the game, you may do so using \u0002!uquit\u0002.",
            /* 15 */ "Progressive UNO is enabled. You can play your own Draw card of the same type on top of this one to pass it on, or \u0002!draw\u0002 to take the attack."
        };

        public Dictionary<string, Game> Games;
        public Dictionary<string, PlayerSettings> PlayerSettings;
        public Dictionary<string, PlayerStats> ScoreboardCurrent;
        public Dictionary<string, PlayerStats> ScoreboardLast;
        public Dictionary<string, PlayerStats> ScoreboardAllTime;
        public DateTime StatsPeriodEnd;
        public Timer StatsResetTimer;

        public LeaderboardMode JSONLeaderboard;

        public int GameCount { get; set; }
        internal RandomOrgApiClient randomClient;

        // Game rules
        public bool AIEnabled;
        public int OutLimit;
        public bool AllowMidGameJoin;
        public int EntryTime;
        public int EntryWaitLimit;
        public int TurnTime;
        public int TurnWaitLimit;
        public WildDrawFourRule WildDrawFour;
        public bool ShowHandOnChallenge;
        public bool Progressive;
        public int ProgressiveCap;

        // Scoring
        public bool VictoryBonus;
        public int[] VictoryBonusValue;
        public bool VictoryBonusLastPlace;
        public bool VictoryBonusRepeat;
        public bool HandBonus;
        public int ParticipationBonus;
        public int QuitPenalty;

        public string GuideURL;
        public string RandomOrgAPIKey;
        public string UserAgent;
        public bool RecordRandomData;
        public string RandomDataURL;

        public static readonly Regex CardParseExpression = new Regex(@"
            (?# Colour) ^(?:(r(?:ed)?)|(y(?:ellow)?)|(g(?:reen)?)|(b(?:lue)?))\ *
            (?# Rank) (?:(\d)|(zero)|(one)|(two)|(three)|(four)|(five)|(six)|(seven)|(eight)|(nine)|
                      (r(?:everse)?)|(s(?:kip)?)|(d(?:raw)?(?:\s*(?:t(?:wo)?|2))?)) |
            ^(?: (?# Wild Draw Four) (d(?:raw)?\ *(?:f(?:our)?|4)|w(?:ild)?\ *d(?:raw)?(?:\ *(?:f(?:our)?|4))?) |
                 (?# Wild) (w(?:ild)?)
            )    (?# Wild colour) (?:\ +(?:(r(?:ed)?)|(y(?:ellow)?)|(g(?:reen)?)|(b(?:lue)?)))?
        ", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
        public static readonly Regex ColourParseExpression = new Regex(@"^(r(?:ed)?)|(y(?:ellow)?)|(g(?:reen)?)|(b(?:lue)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public override string Name {
            get {
                return "UNO game";
            }
        }

        public override string Help(string topic) {
            if (topic == null || topic == "") {
                return "The \u0002UNO card game\u0002 is hosted in this channel.\r\n" +
                       "Say \u0002!ujoin\u0002 to start a game, or to join a game that someone else starts.\r\n" +
                       "For more information about the game, say \u0002!help UNO\u0002.";
            }
            switch (topic.ToUpperInvariant()) {
                case "UNO":
                    return "The goal in UNO is to play all of your cards before your rivals do.\r\n" +
                           "When it's your turn, play a card of the same colour or with the same number or action as the last card played.\r\n" +
                           "For example, after a Red 9, you may play a Red 4, or a Yellow 9.\r\n" +
                           "To play a card, use \u000311!play \u000310<the name of the card you want to play>\u000F.\r\n" +
                           "If you don't have any cards that you can play, you must \u000311!draw\u000F a card from the deck, then play that card or \u000311!pass\u000F.\r\n" +
                           "See also:  \u000311!help \u000310UNO-commands  \u000311!help \u000310UNO-cards  \u000311!help \u000310UNO-scoring";
                case "UNO-COMMANDS":
                    return "These are the commands that you can use in this game:\r\n" +
                           "\u000311!play \u000310<card>\u000F    Lets you play a card from your hand.\r\n" +
                           "For example, you may enter \u000311yellow 5\u000F, \u000311y5\u000F, \u000311yellow Skip\u000F, \u000311ys\u000F (Skip), \u000311yr\u000F (Reverse), \u000311yd\u000F (Draw Two), \u000311w\u000F (wild), \u000311wd\u000F (Wild Draw Four) among others.\r\n" +
                           "\u000311!draw\u000F    If you can't play, use this command to draw a card from the deck.\r\n" +
                           "\u000311!pass\u000F    If you draw and can't play the card, use this command to end your turn.\r\n" +
                           "\u000311!colour \u000310<colour>\u000F    Chooses a colour for your Wild card.\r\n" +
                           "If you need to leave before the game ends, use \u000311!uquit\u000F.\r\n" +
                           "If you lose track of the game: \u000311!upcard  !turn  !hand  !count\r\n" +
                           "Also, if you're familiar with Marky's Colour UNO plugin, most of those commands work here too.";
                case "UNO-CARDS":
                    return "Some of the cards in UNO have special effects when they are played.\r\n" +
                           "\u0002Reverse\u0002: Reverses the turn order.\r\n" +
                           "\u0002Skip\u0002: The next player is 'skipped' and loses a turn.\r\n" +
                           "\u0002Draw Two\u0002: The next player must draw two cards and lose a turn.\r\n" +
                           "\u0002Wild\u0002: You can play this on top of any card. This lets you choose what colour you want the Wild card to be. The next player must play that colour or another Wild card.\r\n" +
                           "\u0002Wild Draw Four\u0002: This is the best card to have. Not only is it a wild card, it also forces the player after you to draw \u0002four\u0002 cards and lose a turn, a powerful blow.\r\n" +
                           "    There's a catch, though: you can't play this if you have a card of the same colour as the last card played. It also can't show up as the initial up-card. See \u000311!help UNO-DrawFour\u000F for more info.\r\n" +
                           "Be sure not to let someone else win while you have some of these cards, as they're worth a lot of points.";
                case "UNO-SCORING":
                    return "The round ends when someone 'goes out' by playing their last card. That player wins points from the cards everyone else is holding:\r\n" +
                           "\u0002Any number card (0-9)\u0002 is worth that number of points.\r\n" +
                           "\u0002Reverse, skip and draw two cards\u0002 are worth \u000220\u0002 points.\r\n" +
                           "\u0002Wild and Wild Draw Four cards\u0002 are worth \u000250\u0002 points.\r\n" +
                           "In short, if the other players have a lot of cards left, especially if they're action cards, you win a lot of points.";
                case "UNO-WILDDRAWFOUR":
                case "UNO-DRAWFOUR":
                case "UNO-WD":
                case "UNO-WDF":
                case "UNO-WD4":
                    switch (this.WildDrawFour) {
                        case WildDrawFourRule.DisallowBluffing:
                            return "It's against the rules to play a Wild Draw Four if you have another card of a matching colour.\r\n" +
                                   "Bluffing is not enabled here.";
                        case WildDrawFourRule.AllowBluffing:
                            return "It's against the rules to play a Wild Draw Four if you have another card of a matching colour.\r\n" +
                                   "However, you can 'bluff' and play one anyway.\r\n" +
                                   "If you think a Wild Draw Four has been played on you illegally, you may challenge it by entering \u000311!challenge\u000F.\r\n" +
                                   "If your challenge is correct, the person who played the Draw Four must take the four cards instead of you.\r\n" +
                                   "But if you're wrong, you have to take two extra cards on top of the four.";
                        case WildDrawFourRule.Free:
                            return "This game is set so that you can play a Wild Draw Four, regardless of what else you hold.";
                        default:
                            return "It's against the rules to play a Wild Draw Four if you have another card of a matching colour.\r\n" +
                                   "Bluffing is not enabled here.";
                    }
                default:
                    return null;
            }
        }

        public override void Initialize() {
            this.Games = new Dictionary<string, Game>(StringComparer.InvariantCultureIgnoreCase);
            this.PlayerSettings = new Dictionary<string, PlayerSettings>(StringComparer.InvariantCultureIgnoreCase);
            this.ScoreboardCurrent = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            this.ScoreboardLast = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            this.ScoreboardAllTime = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            this.StatsResetTimer = new Timer(3600e+3) { AutoReset = false };  // 1 hour
            this.StatsResetTimer.Elapsed += this.StatsResetTimer_Elapsed;

            // Load the default settings.
            this.JSONLeaderboard = LeaderboardMode.SortedByScore;
            this.AIEnabled = true;
            this.OutLimit = 1;
            this.EntryTime = 30;
            this.TurnTime = 90;
            this.EntryWaitLimit = 120;
            this.TurnWaitLimit = 240;
            this.WildDrawFour = WildDrawFourRule.AllowBluffing;
            this.ShowHandOnChallenge = true;

            this.ProgressiveCap = 8;

            this.VictoryBonus = true;
            this.VictoryBonusValue = new int[] { 30, 10, 5 };
            this.HandBonus = true;

            int version;
            this.LoadConfig(this.Key, out version);
            this.LoadData();
            this.LoadStats();

            if (this.RandomOrgAPIKey != null) {
                this.randomClient = new RandomOrgApiClient(this.RandomOrgAPIKey, this.UserAgent) {
                    MaxBlockingTime = 10000
                };
            }

            if (version < 4) {
                foreach (KeyValuePair<string, PlayerSettings> player in this.PlayerSettings) {
                    PlayerStats stats;
                    player.Value.Hints = !(this.ScoreboardAllTime.TryGetValue(player.Key, out stats) && stats.Plays >= 10);
                }
            }
        }

        public override void OnSave() {
            this.SaveConfig();
            this.SaveData();
            this.SaveStats();
            if (this.JSONLeaderboard != LeaderboardMode.None)
                this.GenerateJSONScoreboard();
        }

        public override void OnUnload() {
            foreach (Game game in this.Games.Values)
                game.GameTimer.Stop();
            base.OnUnload();
        }

#region Filing
        public void LoadConfig(string key, out int version) {
            version = 0;
            string filename = Path.Combine("Config", key + ".ini");
            if (!File.Exists(filename)) return;
            StreamReader reader = new StreamReader(filename);
            int lineNumber = 0;
            string section = null;
            PlayerSettings playerSettings = null;

            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                lineNumber++;
                if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

                Match match = Regex.Match(line, @"^\s*\[(.*)\]\s*$");
                if (match.Success) {
                    section = match.Groups[1].Value;
                    Match match2 = Regex.Match(section, @"(?:Player:|(?!Game|Rules|Scoring))(.*)", RegexOptions.IgnoreCase);
                    if (match2.Success) {
                        if (this.PlayerSettings.TryGetValue(match2.Groups[1].Value, out playerSettings))
                            ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): found a duplicate player name.", this.Key, lineNumber);
                        else {
                            playerSettings = new PlayerSettings();
                            this.PlayerSettings.Add(match2.Groups[1].Value, playerSettings);
                        }
                    } else
                        playerSettings = null;
                } else {
                    match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
                    if (match.Success) {
                        string field = match.Groups[1].Value;
                        string value = match.Groups[2].Value;
                        bool value2; int value3;

                        if (section == null) continue;
                        switch (section.ToUpper()) {
                            case "FILE":
                                switch (field.ToUpper()) {
                                    case "VERSION":
                                        if (int.TryParse(value, out value3) && value3 >= 0) version = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
                            case "CONFIG":
                                switch (field.ToUpper()) {
                                    case "JSONLEADERBOARD":
                                        switch (value.ToUpperInvariant()) {
                                            case "OFF":
                                            case "NONE":
                                            case "0":
                                                this.JSONLeaderboard = LeaderboardMode.None;
                                                break;
                                            case "UNSORTED":
                                            case "1":
                                                this.JSONLeaderboard = LeaderboardMode.Unsorted;
                                                break;
                                            case "NAME":
                                            case "SORTEDBYNAME":
                                            case "2":
                                                this.JSONLeaderboard = LeaderboardMode.SortedByName;
                                                break;
                                            case "ON":
                                            case "SCORE":
                                            case "SORTEDBYSCORE":
                                            case "3":
                                                this.JSONLeaderboard = LeaderboardMode.SortedByScore;
                                                break;
                                            case "PLAYS":
                                            case "SORTEDBYPLAYS":
                                            case "4":
                                                this.JSONLeaderboard = LeaderboardMode.SortedByPlays;
                                                break;
                                            case "WINS":
                                            case "SORTEDBYWINS":
                                            case "5":
                                                this.JSONLeaderboard = LeaderboardMode.SortedByWins;
                                                break;
                                            case "CHALLENGE":
                                            case "SORTEDBYCHALLENGE":
                                            case "6":
                                                this.JSONLeaderboard = LeaderboardMode.SortedByChallenge;
                                                break;
                                            default:
                                                ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'off', 'unsorted', 'sortedbyname', 'sortedbyscore', 'sortedbyplays', 'sortedbywins' or 'sortedbychallenge').", this.Key, lineNumber);
                                                break;
                                        }
                                        break;
                                    case "GAMECOUNT":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.GameCount = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    case "RANDOMORGAPIKEY":
                                        this.RandomOrgAPIKey = value;
                                        break;
                                    case "USERAGENT":
                                        this.UserAgent = value;
                                        break;
                                    case "RECORDRANDOMDATA":
                                        if (Bot.TryParseBoolean(value, out value2)) this.RecordRandomData = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "RANDOMDATAURL":
                                        this.RandomDataURL = value;
                                        break;
                                    case "GUIDEURL":
                                        this.GuideURL = value;
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
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
                                    case "MIDGAMEJOIN":
                                        if (Bot.TryParseBoolean(value, out value2)) this.AllowMidGameJoin = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
                            case "SCORING":
                                switch (field.ToUpper()) {
                                    case "VICTORYBONUS":
                                        if (Bot.TryParseBoolean(value, out value2)) this.VictoryBonus = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "VICTORYBONUSLASTPLACE":
                                        if (Bot.TryParseBoolean(value, out value2)) this.VictoryBonusLastPlace = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "VICTORYBONUSREPEAT":
                                        if (Bot.TryParseBoolean(value, out value2)) this.VictoryBonusRepeat = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "HANDBONUS":
                                        if (Bot.TryParseBoolean(value, out value2)) this.HandBonus = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "PARTICIPATIONBONUS":
                                        if (int.TryParse(value, out value3)) this.ParticipationBonus = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected an integer).", this.Key, lineNumber);
                                        break;
                                    case "QUITPENALTY":
                                        if (int.TryParse(value, out value3)) this.QuitPenalty = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected an integer).", this.Key, lineNumber);
                                        break;
                                    case "VICTORYBONUSVALUE":
                                        string[] fields = value.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        List<int> value4 = new List<int>(fields.Length);
                                        foreach (string s in fields) {
                                            if (int.TryParse(s, out value3)) value4.Add(value3);
                                            else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): '{2}' isn't a valid integer.", this.Key, s);
                                        }
                                        this.VictoryBonusValue = value4.ToArray();
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
                            case "RULES":
                                switch (field.ToUpper()) {
                                    case "ALLOUT":
                                        if (Bot.TryParseBoolean(value, out value2)) this.OutLimit = value2 ? int.MaxValue : 1;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "OUTLIMIT":
                                        if (value.Equals("None", StringComparison.InvariantCultureIgnoreCase)) this.OutLimit = int.MaxValue;
                                        else if (int.TryParse(value, out value3) && value3 > 0) this.OutLimit = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a positive integer).", this.Key, lineNumber);
                                        break;
                                    case "WILDDRAWFOUR":
                                        switch (value.ToUpperInvariant()) {
                                            case "BLUFFOFF":
                                            case "DISALLOWBLUFFING":
                                            case "0":
                                                this.WildDrawFour = WildDrawFourRule.DisallowBluffing;
                                                break;
                                            case "BLUFFON":
                                            case "ALLOWBLUFFING":
                                            case "1":
                                                this.WildDrawFour = WildDrawFourRule.AllowBluffing;
                                                break;
                                            case "FREE":
                                            case "2":
                                                this.WildDrawFour = WildDrawFourRule.Free;
                                                break;
                                            default:
                                                ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'BluffOff', 'BluffOn' or 'Free').", this.Key, lineNumber);
                                                break;
                                        }
                                        break;
                                    case "SHOWHANDONCHALLENGE":
                                        if (Bot.TryParseBoolean(value, out value2)) this.ShowHandOnChallenge = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "PROGRESSIVE":
                                        if (Bot.TryParseBoolean(value, out value2)) this.Progressive = value2;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                        break;
                                    case "PROGRESSIVECAP":
                                        if (int.TryParse(value, out value3) && value3 >= 0) this.ProgressiveCap = value3;
                                        else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected a non-negative integer).", this.Key, lineNumber);
                                        break;
                                    default:
                                        if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                        break;
                                }
                                break;
                            default:
                                if (playerSettings == null) {
                                    if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): found a stray field or unknown section.", this.Key, lineNumber);
                                    break;
                                } else {
                                    switch (field.ToUpper()) {
                                        case "HIGHLIGHT":
                                            switch (value.ToUpperInvariant()) {
                                                case "OFF":
                                                case "0":
                                                    playerSettings.Highlight = HighlightOptions.Off;
                                                    break;
                                                case "ON":
                                                case "ALWAYS":
                                                case "6":
                                                    playerSettings.Highlight = HighlightOptions.On;
                                                    break;
                                                case "TEMPORARY":
                                                case "4":
                                                    playerSettings.Highlight = HighlightOptions.OnTemporary;
                                                    break;
                                                default:
                                                    ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'Off', 'On' or 'Temporary').", this.Key, lineNumber);
                                                    break;
                                            }
                                            break;
                                        case "AUTOSORT":
                                            switch (value.ToUpperInvariant()) {
                                                case "OFF":
                                                case "0":
                                                    playerSettings.AutoSort = AutoSortOptions.Off;
                                                    break;
                                                case "BYCOLOUR":
                                                case "COLOUR":
                                                case "1":
                                                    playerSettings.AutoSort = AutoSortOptions.ByColour;
                                                    break;
                                                case "BYRANK":
                                                case "RANK":
                                                case "2":
                                                    playerSettings.AutoSort = AutoSortOptions.ByRank;
                                                    break;
                                                default:
                                                    ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'Off', 'Colour' or 'Rank').", this.Key, lineNumber);
                                                    break;
                                            }
                                            break;
                                        case "DUELWITHBOT":
                                            if (Bot.TryParseBoolean(value, out value2)) playerSettings.AllowDuelWithBot = value2;
                                            else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                            break;
                                        case "HINTS":
                                            if (Bot.TryParseBoolean(value, out value2)) playerSettings.Hints = value2;
                                            else ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the value is invalid (expected 'yes' or 'no').", this.Key, lineNumber);
                                            break;
                                        default:
                                            if (!string.IsNullOrWhiteSpace(field)) ConsoleUtils.WriteLine("[{0}] Problem loading the configuration (line {1}): the field name is unknown.", this.Key, lineNumber);
                                            break;
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            reader.Close();
        }

        public void LoadData() {
            if (File.Exists(Path.Combine("data", this.Key, "gamecount.txt"))) {
                var text = File.ReadAllText(Path.Combine("data", this.Key, "gamecount.txt"));
                this.GameCount = int.Parse(text);
            }
        }

        public void SaveConfig() {
            if (!Directory.Exists("Config"))
                Directory.CreateDirectory("Config");
            StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false);
            writer.WriteLine("[File]");
            writer.WriteLine("Version=4");
            writer.WriteLine();
            writer.WriteLine("[Config]");
            writer.WriteLine("JSONLeaderboard={0}", this.JSONLeaderboard.ToString());
            writer.WriteLine("GameCount={0}", this.GameCount);
            if (this.RandomOrgAPIKey != null) writer.WriteLine("RandomOrgAPIKey={0}", this.RandomOrgAPIKey);
            if (this.UserAgent != null) writer.WriteLine("UserAgent={0}", this.UserAgent);
            writer.WriteLine("RecordRandomData={0}", this.RecordRandomData ? "Yes" : "No");
            if (this.RandomDataURL != null) writer.WriteLine("RandomDataURL={0}", this.RandomDataURL);
            if (this.GuideURL != null) writer.WriteLine("GuideURL={0}", this.GuideURL);
            writer.WriteLine();
            writer.WriteLine("[Game]");
            writer.WriteLine("AI={0}", this.AIEnabled ? "On" : "Off");
            writer.WriteLine("EntryTime={0}", this.EntryTime);
            writer.WriteLine("EntryWaitLimit={0}", this.EntryWaitLimit);
            writer.WriteLine("TurnTime={0}", this.TurnTime);
            writer.WriteLine("TurnWaitLimit={0}", this.TurnWaitLimit);
            writer.WriteLine("MidGameJoin={0}", this.AllowMidGameJoin ? "Yes" : "No");
            writer.WriteLine();
            writer.WriteLine("[Rules]");
            writer.WriteLine("OutLimit={0}", this.OutLimit == int.MaxValue ? "None" : this.OutLimit.ToString());
            if (this.WildDrawFour == WildDrawFourRule.DisallowBluffing)
                writer.WriteLine("WildDrawFour=BluffOff");
            else if (this.WildDrawFour == WildDrawFourRule.AllowBluffing)
                writer.WriteLine("WildDrawFour=BluffOn");
            else if (this.WildDrawFour == WildDrawFourRule.Free)
                writer.WriteLine("WildDrawFour=Free");
            writer.WriteLine("ShowHandOnChallenge={0}", this.ShowHandOnChallenge ? "On" : "Off");
            writer.WriteLine("Progressive={0}", this.Progressive ? "On" : "Off");
            writer.WriteLine("ProgressiveCap={0}", this.ProgressiveCap);
            writer.WriteLine();
            writer.WriteLine("[Scoring]");
            writer.WriteLine("VictoryBonus={0}", this.VictoryBonus ? "On" : "Off");
            writer.WriteLine("VictoryBonusValue={0}", string.Join(",", this.VictoryBonusValue));
            writer.WriteLine("VictoryBonusLastPlace={0}", this.VictoryBonusLastPlace ? "On" : "Off");
            writer.WriteLine("VictoryBonusRepeat={0}", this.VictoryBonusRepeat ? "On" : "Off");
            writer.WriteLine("HandBonus={0}", this.HandBonus ? "On" : "Off");
            writer.WriteLine("ParticipationBonus={0}", this.ParticipationBonus);
            writer.WriteLine("QuitPenalty={0}", this.QuitPenalty);

            foreach (KeyValuePair<string, PlayerSettings> playerSettings in this.PlayerSettings) {
                if (playerSettings.Value.IsDefault()) continue;
                writer.WriteLine();
                writer.WriteLine("[Player:{0}]", playerSettings.Key);
                if (playerSettings.Value.Highlight == HighlightOptions.On)
                    writer.WriteLine("Highlight=On");
                else if (playerSettings.Value.Highlight == HighlightOptions.OnTemporary ||
                         playerSettings.Value.Highlight == HighlightOptions.OnTemporaryOneGame)
                    writer.WriteLine("Highlight=Temporary");
                else
                    writer.WriteLine("Highlight=Off");

                if (playerSettings.Value.AutoSort == AutoSortOptions.ByColour)
                    writer.WriteLine("AutoSort=Colour");
                else if (playerSettings.Value.AutoSort == AutoSortOptions.ByRank)
                    writer.WriteLine("AutoSort=Rank");
                else
                    writer.WriteLine("AutoSort=Off");

                if (playerSettings.Value.Hints)
                    writer.WriteLine("Hints=On");
                else
                    writer.WriteLine("Hints=Off");

                writer.WriteLine("DuelWithBot={0}", playerSettings.Value.AllowDuelWithBot ? "Yes" : "No");
            }
            writer.Close();
        }

        public void SaveData() {
            Directory.CreateDirectory(Path.Combine("data", this.Key));
            File.WriteAllText(Path.Combine("data", this.Key, "gamecount.txt"), this.GameCount.ToString());
        }

        public void LoadStats() {
            var filename = Path.Combine("data", this.Key, "stats.dat");
            if (!File.Exists(filename)) filename = this.Key + "-stats.dat";

            if (File.Exists(filename)) {
                using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open, FileAccess.Read))) {
                    short version = reader.ReadInt16();
                    if (version == 2) this.LoadStats2(reader);
                    else if (version == 3) this.LoadStats3(reader);
                    else if (version == 4) this.LoadStats4(reader);
                    else throw new UnknownFileVersionException();
                }
            } else {
                this.ScoreboardCurrent = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
                this.ScoreboardLast = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
                this.ScoreboardAllTime = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
                this.StatsResetTimer = new Timer(3600e+3) { AutoReset = false };  // 1 hour
            }
        }

        internal void LoadStats4(BinaryReader reader) {
            short count; long time;

            // Current period stats
            this.ScoreboardCurrent = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                time = reader.ReadInt64();
                player.RecordTime = DateTime.FromBinary(time);
                player.ChallengePoints = reader.ReadInt64();
                time = reader.ReadInt64();
                player.StartedPlaying = DateTime.FromBinary(time);
                this.ScoreboardCurrent.Add(player.Name, player);
            }

            // Last period stats
            this.ScoreboardLast = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                time = reader.ReadInt64();
                player.RecordTime = DateTime.FromBinary(time);
                player.ChallengePoints = reader.ReadInt64();
                time = reader.ReadInt64();
                player.StartedPlaying = DateTime.FromBinary(time);
                this.ScoreboardLast.Add(player.Name, player);
            }

            // All-time stats
            this.ScoreboardAllTime = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                time = reader.ReadInt64();
                player.RecordTime = DateTime.FromBinary(time);
                player.ChallengePoints = reader.ReadInt64();
                time = reader.ReadInt64();
                player.StartedPlaying = DateTime.FromBinary(time);
                player.CurrentStreak = reader.ReadInt16();
                player.BestStreak = reader.ReadInt16();
                time = reader.ReadInt64();
                player.BestStreakTime = DateTime.FromBinary(time);
                player.Placed = new int[5];
                for (int i = 0; i < 5; i++)
                    player.Placed[i] = reader.ReadInt32();
                player.BestPeriodScore = reader.ReadInt64();
                time = reader.ReadInt64();
                player.BestPeriodScoreTime = DateTime.FromBinary(time);
                player.BestPeriodChallengeScore = reader.ReadInt64();
                time = reader.ReadInt64();
                player.BestPeriodChallengeScoreTime = DateTime.FromBinary(time);
                this.ScoreboardAllTime.Add(player.Name, player);
            }

            time = reader.ReadInt64();
            this.StatsPeriodEnd = DateTime.FromBinary(time);
        }
        internal void LoadStats3(BinaryReader reader) {
            short count;
            this.LoadStats2(reader);

            // Streak data
            for (count = reader.ReadInt16(); count > 0; count--) {
                string name = reader.ReadString();
                short value = reader.ReadInt16();
                if (value != 0) {
                    PlayerStats player;
                    if (this.ScoreboardAllTime.TryGetValue(name, out player)) {
                        player.CurrentStreak = value;
                    } else {
                        player = new PlayerStats();
                        player.Name = name;
                        player.CurrentStreak = value;
                        this.ScoreboardAllTime.Add(name, player);
                    }
                }
            }
            for (count = reader.ReadInt16(); count > 0; count--) {
                string name = reader.ReadString();
                short value = reader.ReadInt16();
                if (value != 0) {
                    PlayerStats player;
                    if (this.ScoreboardAllTime.TryGetValue(name, out player)) {
                        player.BestStreak = value;
                    } else {
                        player = new PlayerStats();
                        player.Name = name;
                        player.BestStreak = value;
                        this.ScoreboardAllTime.Add(name, player);
                    }
                }
            }
        }
        internal void LoadStats2(BinaryReader reader) {
            short count;

            // Current period stats
            this.ScoreboardCurrent = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                reader.ReadBytes(12);
                player.ChallengePoints = reader.ReadInt64();
                this.ScoreboardCurrent.Add(player.Name, player);
            }

            // Last period stats
            this.ScoreboardLast = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                reader.ReadBytes(12);
                player.ChallengePoints = reader.ReadInt64();
                this.ScoreboardLast.Add(player.Name, player);
            }

            // All-time stats
            this.ScoreboardAllTime = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);
            for (count = reader.ReadInt16(); count > 0; count--) {
                PlayerStats player = new PlayerStats();
                player.Name = reader.ReadString();
                player.Points = reader.ReadInt64();
                player.Plays = reader.ReadInt32();
                player.Wins = reader.ReadInt32();
                player.Losses = reader.ReadInt32();
                player.RecordPoints = reader.ReadInt32();
                reader.ReadBytes(12);
                player.ChallengePoints = reader.ReadInt64();
                this.ScoreboardAllTime.Add(player.Name, player);
            }

            this.StatsPeriodEnd = DateTime.FromBinary(reader.ReadInt64());
        }

        public void SaveStats() {
            Directory.CreateDirectory(Path.Combine("data", this.Key));
            BinaryWriter writer = new BinaryWriter(File.Open(Path.Combine("data", this.Key, "stats.dat"), FileMode.Create));

            // Version number
            writer.Write((short) 4);

            // Current period stats
            if (this.ScoreboardCurrent == null)
                writer.Write((short) 0);
            else {
                writer.Write((short) this.ScoreboardCurrent.Count);
                foreach (PlayerStats player in this.ScoreboardCurrent.Values) {
                    writer.Write(player.Name);
                    writer.Write(player.Points);
                    writer.Write(player.Plays);
                    writer.Write(player.Wins);
                    writer.Write(player.Losses);
                    writer.Write(player.RecordPoints);
                    writer.Write(player.RecordTime.ToBinary());
                    writer.Write(player.ChallengePoints);
                    writer.Write(player.StartedPlaying.ToBinary());
                }
            }

            // Previous period stats
            if (this.ScoreboardLast == null)
                writer.Write((short) 0);
            else {
                writer.Write((short) this.ScoreboardLast.Count);
                foreach (PlayerStats player in this.ScoreboardLast.Values) {
                    writer.Write(player.Name);
                    writer.Write(player.Points);
                    writer.Write(player.Plays);
                    writer.Write(player.Wins);
                    writer.Write(player.Losses);
                    writer.Write(player.RecordPoints);
                    writer.Write(player.RecordTime.ToBinary());
                    writer.Write(player.ChallengePoints);
                    writer.Write(player.StartedPlaying.ToBinary());
                }
            }

            // All-time stats
            if (this.ScoreboardAllTime == null)
                writer.Write((short) 0);
            else {
                writer.Write((short) this.ScoreboardAllTime.Count);
                foreach (PlayerStats player in this.ScoreboardAllTime.Values) {
                    writer.Write(player.Name);
                    writer.Write(player.Points);
                    writer.Write(player.Plays);
                    writer.Write(player.Wins);
                    writer.Write(player.Losses);
                    writer.Write(player.RecordPoints);
                    writer.Write(player.RecordTime.ToBinary());
                    writer.Write(player.ChallengePoints);
                    writer.Write(player.StartedPlaying.ToBinary());
                    writer.Write((short) player.CurrentStreak);
                    writer.Write((short) player.BestStreak);
                    writer.Write(player.BestStreakTime.ToBinary());
                    for (int i = 0; i < 5; ++i)
                        writer.Write(player.Placed[i]);
                    writer.Write(player.BestPeriodScore);
                    writer.Write(player.BestPeriodScoreTime.ToBinary());
                    writer.Write(player.BestPeriodChallengeScore);
                    writer.Write(player.BestPeriodChallengeScoreTime.ToBinary());
                }
            }

            writer.Write(this.StatsPeriodEnd.ToBinary());
            writer.Close();
        }
#endregion

        [Command(new string[] { "set", "uset" }, 1, 2, "set <property> <value>", "Changes settings for this plugin.")]
        public void CommandSet(object sender, CommandEventArgs e) {
            string property = e.Parameters[0];
            string value = e.Parameters.Length == 1 ? null : e.Parameters[1];
            int value2; bool value3;
            PlayerSettings player;

            switch (property.Replace(" ", "").Replace("-", "").ToUpperInvariant()) {
                case "AI":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.AIEnabled)
                            Bot.Say(e.Client, e.Channel, "I \u00039will\u000F join UNO games.");
                        else
                            Bot.Say(e.Client, e.Channel, "I \u00034will not\u000F join UNO games.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.AIEnabled = value3)
                            Bot.Say(e.Client, e.Channel, "I \u00039will now\u000F join UNO games.");
                        else
                            Bot.Say(e.Client, e.Channel, "I \u00034will no longer\u000F join UNO games.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "ALLOUT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.OutLimit == int.MaxValue)
                            Bot.Say(e.Client, e.Channel, "The game will end only when one player remains.");
                        else if (this.OutLimit == 1)
                            Bot.Say(e.Client, e.Channel, "The game will end when \u0002{0}\u0002 player goes out.", this.OutLimit);
                        else
                            Bot.Say(e.Client, e.Channel, "The game will end when \u0002{0}\u0002 players go out.", this.OutLimit);
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (value3) {
                            this.OutLimit = int.MaxValue;
                            Bot.Say(e.Client, e.Channel, "The game will now end only when one player remains.");
                        } else {
                            this.OutLimit = 1;
                            Bot.Say(e.Client, e.Channel, "The game will now end when \u0002{0}\u0002 player goes out.", 1);
                        }
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "OUTLIMIT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.OutLimit == int.MaxValue)
                            Bot.Say(e.Client, e.Channel, "The game will end only when one player remains.");
                        else if (this.OutLimit == 1)
                            Bot.Say(e.Client, e.Channel, "The game will end when \u0002{0}\u0002 player goes out.", this.OutLimit);
                        else
                            Bot.Say(e.Client, e.Channel, "The game will end when \u0002{0}\u0002 players go out.", this.OutLimit);
                    } else if (value == "0" || value.Equals("none", StringComparison.InvariantCultureIgnoreCase)) {
                        this.OutLimit = int.MaxValue;
                        Bot.Say(e.Client, e.Channel, "The game will now end only when one player remains.");
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.OutLimit = value2;
                            if (value2 == int.MaxValue)
                                Bot.Say(e.Client, e.Channel, "The game will now end only when one player remains.");
                            else if (value2 == 1)
                                Bot.Say(e.Client, e.Channel, "The game will now end when \u0002{0}\u0002 player goes out.", value2);
                            else
                                Bot.Say(e.Client, e.Channel, "The game will now end when \u0002{0}\u0002 players go out.", value2);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, "The number must be positive.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, "That isn't a valid integer.");
                    break;
                case "WILDDRAWFOUR":
                case "WILDDRAW4":
                case "DRAWFOUR":
                case "WD":
                case "WDF":
                case "WD4":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.WildDrawFour == WildDrawFourRule.Free)
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four\u0002 is \u000312freely playable\u000F.");
                        else if (this.WildDrawFour == WildDrawFourRule.AllowBluffing)
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four bluffing\u0002 is \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four bluffing\u0002 is \u00034disabled\u000F.");
                    } else {
                        value = value.Replace(" ", "");
                        if (value == "0" || value.Equals("BluffOff", StringComparison.InvariantCultureIgnoreCase)) {
                            this.WildDrawFour = WildDrawFourRule.DisallowBluffing;
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four bluffing\u0002 is now \u00034disabled\u000F.");
                        } else if (value == "1" || value.Equals("BluffOn", StringComparison.InvariantCultureIgnoreCase)) {
                            this.WildDrawFour = WildDrawFourRule.AllowBluffing;
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four bluffing\u0002 is now \u00039enabled\u000F.");
                        } else if (value == "1" || value.Equals("BluffOn", StringComparison.InvariantCultureIgnoreCase)) {
                            this.WildDrawFour = WildDrawFourRule.Free;
                            Bot.Say(e.Client, e.Channel, "\u0002Wild Draw Four\u0002 is now \u000312freely playable\u000F.");
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid setting. Please enter 'bluff off', 'bluff on' or 'free'.", value));
                    }
                    break;
                case "SHOWHANDONCHALLENGE":
                case "SHOWCHALLENGE":
                case "SHC":
                case "SC":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.ShowHandOnChallenge)
                            Bot.Say(e.Client, e.Channel, "Cards \u00039must\u000F be shown for a Wild Draw Four challenge.");
                        else
                            Bot.Say(e.Client, e.Channel, "Cards \u00034need not\u000F be shown for a Wild Draw Four challenge.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.ShowHandOnChallenge = value3)
                            Bot.Say(e.Client, e.Channel, "Cards \u00039must\u000F now be shown for a Wild Draw Four challenge.");
                        else
                            Bot.Say(e.Client, e.Channel, "Cards now \u00034need not\u000F be shown for a Wild Draw Four challenge.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "ENTRYTIME":
                case "ENTRYPERIOD":
                case "STARTTIME":
                case "ENTRY":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        Bot.Say(e.Client, e.Channel, "The entry period is \u0002{0}\u0002 seconds.", this.EntryTime);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.EntryTime = value2;
                            Bot.Say(e.Client, e.Channel, "The entry period is now \u0002{0}\u0002 seconds.", this.EntryTime);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, "The number must be positive.", value);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "ENTRYWAITLIMIT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        Bot.Say(e.Client, e.Channel, "The entry period may be extended to \u0002{0}\u0002 seconds.", this.EntryWaitLimit);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.EntryWaitLimit = value2;
                            Bot.Say(e.Client, e.Channel, "The entry period may now be extended to \u0002{0}\u0002 seconds.", this.EntryWaitLimit);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, "The number must be positive.", value);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "TURNTIME":
                case "TIMELIMIT":
                case "IDLETIME":
                case "TIME":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.TurnTime == 0)
                            Bot.Say(e.Client, e.Channel, "The turn time limit is disabled.", this.TurnTime);
                        else
                            Bot.Say(e.Client, e.Channel, "The turn time limit is \u0002{0}\u0002 seconds.", this.TurnTime);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 >= 0) {
                            this.TurnTime = value2;
                            if (value2 == 0)
                                Bot.Say(e.Client, e.Channel, "The turn time limit is now disabled.", this.TurnTime);
                            else
                                Bot.Say(e.Client, e.Channel, "The turn time limit is now \u0002{0}\u0002 seconds.", this.TurnTime);
                            // Reset the existing turn timers.
                            foreach (Game game in this.Games.Values)
                                if (!game.IsOpen) game.GameTimer.Interval = this.TurnTime == 0 ? 60e+3 : (this.TurnTime * 1e+3);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, "The number cannot be negative.", value);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "TURNWAITLIMIT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        Bot.Say(e.Client, e.Channel, "The turn time limit may be extended to \u0002{0}\u0002 seconds.", this.TurnWaitLimit);
                    } else if (int.TryParse(value, out value2)) {
                        if (value2 > 0) {
                            this.TurnWaitLimit = value2;
                            Bot.Say(e.Client, e.Channel, "The turn time limit may now be extended to \u0002{0}\u0002 seconds.", this.TurnWaitLimit);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, "The number must be positive.", value);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "VICTORYBONUS":
                case "WINBONUS":
                case "VICTORY":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.VictoryBonus)
                            Bot.Say(e.Client, e.Channel, "Victory bonuses are \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Victory bonuses are \u00034disabled\u000F.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.VictoryBonus = value3)
                            Bot.Say(e.Client, e.Channel, "Victory bonuses are now \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Victory bonuses are now \u00034disabled\u000F.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "HANDBONUS":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.HandBonus)
                            Bot.Say(e.Client, e.Channel, "Hand bonuses are \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Hand bonuses are \u00034disabled\u000F.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.HandBonus = value3)
                            Bot.Say(e.Client, e.Channel, "Hand bonuses are now \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Hand bonuses are now \u00034disabled\u000F.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "VICTORYBONUSLASTPLACE":
                case "VICTORYBONUSLAST":
                case "WINBONUSLASTPLACE":
                case "WINBONUSLAST":
                case "VICTORYLAST":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.VictoryBonusLastPlace)
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00039will\u000F be awarded to the last place player.");
                        else
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00034will not\u000F be awarded to the last place player.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.VictoryBonusLastPlace = value3)
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00039will now\u000F be awarded to the last place player.");
                        else
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00034will no longer\u000F be awarded to the last place player.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "VICTORYBONUSREPEAT":
                case "WINBONUSREPEAT":
                case "VICTORYREPEAT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.VictoryBonus)
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00039will\u000F be awarded to all above last place.");
                        else
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00034may not\u000F be awarded to all above last place.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.VictoryBonus = value3)
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00039will now\u000F be awarded to all above last place.");
                        else
                            Bot.Say(e.Client, e.Channel, "A victory bonus \u00034may no longer\u000F be awarded to all above last place.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "PARTICIPATIONBONUS":
                case "PLAYBONUS":
                case "PLAY":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.ParticipationBonus == 0)
                            Bot.Say(e.Client, e.Channel, "The participation bonus is disabled.", this.ParticipationBonus);
                        else if (this.ParticipationBonus == 1 || this.ParticipationBonus == -1)
                            Bot.Say(e.Client, e.Channel, "The participation bonus is \u0002{0}\u0002 point.", this.ParticipationBonus);
                        else
                            Bot.Say(e.Client, e.Channel, "The participation bonus is \u0002{0}\u0002 points.", this.ParticipationBonus);
                    } else if (int.TryParse(value, out value2)) {
                        this.ParticipationBonus = value2;
                        if (value2 == 0)
                            Bot.Say(e.Client, e.Channel, "The participation bonus is now disabled.", this.TurnTime);
                        else if (value2 == 1 || value2 == -1)
                            Bot.Say(e.Client, e.Channel, "The participation bonus is now \u0002{0}\u0002 point.", this.ParticipationBonus);
                        else
                            Bot.Say(e.Client, e.Channel, "The participation bonus is now \u0002{0}\u0002 points.", this.ParticipationBonus);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "QUITPENALTY":
                case "LEAVEPENALTY":
                case "QUIT":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.QuitPenalty == 0)
                            Bot.Say(e.Client, e.Channel, "The quit penalty is disabled.", this.QuitPenalty);
                        else if (this.QuitPenalty == 1 || this.QuitPenalty == -1)
                            Bot.Say(e.Client, e.Channel, "The quit penalty is \u0002{0}\u0002 point.", this.QuitPenalty);
                        else
                            Bot.Say(e.Client, e.Channel, "The quit penalty is \u0002{0}\u0002 points.", this.QuitPenalty);
                    } else if (int.TryParse(value, out value2)) {
                        this.QuitPenalty = value2;
                        if (value2 == 0)
                            Bot.Say(e.Client, e.Channel, "The quit penalty is now disabled.", this.QuitPenalty);
                        else if (value2 == 1 || value2 == -1)
                            Bot.Say(e.Client, e.Channel, "The quit penalty is now \u0002{0}\u0002 point.", this.QuitPenalty);
                        else
                            Bot.Say(e.Client, e.Channel, "The quit penalty is now \u0002{0}\u0002 points.", this.QuitPenalty);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;
                case "VICTORYBONUSVALUE":
                case "VICTORYBONUSPOINTS":
                case "WINBONUSVALUE":
                case "WINBONUSPOINTS":
                case "VICTORYVALUE":
                case "VICTORYPOINTS":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.VictoryBonusValue.Length == 1)
                            Bot.Say(e.Client, e.Channel, "The victory bonus is \u0002{0}\u0002.", this.VictoryBonusValue[0]);
                        else
                            Bot.Say(e.Client, e.Channel, "The victory bonuses are \u0002{0}\u0002.", string.Join("\u0002, \u0002", this.VictoryBonusValue));
                    } else {
                        string[] fields = value.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        List<int> value4 = new List<int>(fields.Length);
                        foreach (string s in fields) {
                            if (int.TryParse(value, out value2)) value4.Add(value2);
                            else {
                                Bot.Say(e.Client, e.Sender.Nickname, string.Format("'{0}' isn't a valid integer.", value));
                                return;
                            }
                        }
                        if (value4.Count == 0)
                            Bot.Say(e.Client, e.Sender.Nickname, "You must specify at least one number.");
                        else {
                            this.VictoryBonusValue = value4.ToArray();
                            if (this.VictoryBonusValue.Length == 1)
                                Bot.Say(e.Client, e.Channel, "The victory bonus is now \u0002{0}\u0002.", this.VictoryBonusValue[0]);
                            else
                                Bot.Say(e.Client, e.Channel, "The victory bonuses are now \u0002{0}\u0002.", string.Join("\u0002, \u0002", this.VictoryBonusValue));
                        }
                    }
                    break;
                case "MIDGAMEJOIN":
                case "ALLOWMIDGAMEJOIN":
                case "MIDGAMEENTRY":
                case "ALLOWMIDGAMEENTRY":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.AllowMidGameJoin)
                            Bot.Say(e.Client, e.Channel, "Players \u00039may\u000F join during a game.");
                        else
                            Bot.Say(e.Client, e.Channel, "Players \u00034may not\u000F join during a game.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.AllowMidGameJoin = value3)
                            Bot.Say(e.Client, e.Channel, "Players \u00039may\u000F now join during a game.");
                        else
                            Bot.Say(e.Client, e.Channel, "Players \u00034may no longer\u000F join during a game.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "PROGRESSIVE":
                case "STACKING":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.Progressive)
                            Bot.Say(e.Client, e.Channel, "Progressive rules are \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Progressive rules are \u00034disabled\u000F.");
                    } else if (Bot.TryParseBoolean(value, out value3)) {
                        if (this.Progressive = value3)
                            Bot.Say(e.Client, e.Channel, "Progressive rules are now \u00039enabled\u000F.");
                        else
                            Bot.Say(e.Client, e.Channel, "Progressive rules are now\u00034disabled\u000F.");
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'on' or 'off'.", value));
                    break;
                case "PROGRESSIVECAP":
                case "STACKINGCAP":
                    if (!SetPermissionCheck(e)) return;
                    if (value == null) {
                        if (this.ProgressiveCap == int.MaxValue)
                            Bot.Say(e.Client, e.Channel, "There is no stack cap.", this.ProgressiveCap);
                        else
                            Bot.Say(e.Client, e.Channel, "The stack cap is \u0002{0}\u0002 cards.", this.ProgressiveCap);
                    } else if (int.TryParse(value, out value2)) {
                        this.ProgressiveCap = value2;
                        if (value2 == int.MaxValue)
                            Bot.Say(e.Client, e.Channel, "The stack cap is now disabled.", this.ProgressiveCap);
                        else
                            Bot.Say(e.Client, e.Channel, "The stack cap is now \u0002{0}\u0002 cards.", this.ProgressiveCap);
                    } else
                        Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid integer.", value));
                    break;

                case "HIGHLIGHT":
                case "PING":
                case "ALERT":
                    if (value == null) {
                        if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player)) {
                            if (player.Highlight == HighlightOptions.On)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are \u00039enabled\u000F.", e.Sender.Nickname);
                            else if (player.Highlight == HighlightOptions.OnTemporary)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are \u000312enabled for this session\u000F.", e.Sender.Nickname);
                            else
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are \u00034disabled\u000F.", e.Sender.Nickname);
                        } else
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are \u00034disabled\u000F.", e.Sender.Nickname);
                    } else {
                        if (!this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player))
                            this.PlayerSettings.Add(e.Sender.Nickname, player = new PlayerSettings());
                        value = value.Replace(" ", "");
                        if (value == "0" || value.Equals("Off", StringComparison.InvariantCultureIgnoreCase)) {
                            player.Highlight = HighlightOptions.Off;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are now \u00034disabled\u000F.", e.Sender.Nickname);
                        } else if (value == "1" || value.Equals("On", StringComparison.InvariantCultureIgnoreCase) || value.Equals("Permanent", StringComparison.InvariantCultureIgnoreCase)) {
                            player.Highlight = HighlightOptions.On;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are now \u00039enabled\u000F.", e.Sender.Nickname);
                        } else if (value == "2" || value.Equals("Temporary", StringComparison.InvariantCultureIgnoreCase)) {
                            player.Highlight = HighlightOptions.OnTemporary;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your game alerts are now \u000312enabled for this session\u000F.", e.Sender.Nickname);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid setting. Please enter 'off', 'on' or 'temporary'.", value));
                    }
                    break;
                case "AUTOSORT":
                case "SORT":
                    if (value == null) {
                        if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player)) {
                            if (player.AutoSort == AutoSortOptions.Off)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your cards will \u00034not be sorted\u000F.", e.Sender.Nickname);
                            else if (player.AutoSort == AutoSortOptions.ByRank)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your cards will be sorted \u000312by rank\u000F.", e.Sender.Nickname);
                            else
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002,  your cards will be sorted \u00039by colour\u000F.", e.Sender.Nickname);
                        } else
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002,  your cards will be sorted \u00039by colour\u000F.", e.Sender.Nickname);
                    } else {
                        if (!this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player))
                            this.PlayerSettings.Add(e.Sender.Nickname, player = new PlayerSettings());
                        value = value.Replace(" ", "");
                        if (value == "0" || value.Equals("Off", StringComparison.InvariantCultureIgnoreCase)) {
                            player.AutoSort = AutoSortOptions.Off;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your cards will \u00034no longer be sorted\u000F.", e.Sender.Nickname);
                        } else if (value == "1" || value.Equals("On", StringComparison.InvariantCultureIgnoreCase) ||
                                   value.Equals("Colour", StringComparison.InvariantCultureIgnoreCase) || value.Equals("ByColour", StringComparison.InvariantCultureIgnoreCase)) {
                            player.AutoSort = AutoSortOptions.ByColour;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002,  your cards will now be sorted \u00039by colour\u000F.", e.Sender.Nickname);
                        } else if (value == "2" || value.Equals("Rank", StringComparison.InvariantCultureIgnoreCase) || value.Equals("ByRank", StringComparison.InvariantCultureIgnoreCase) ||
                                   value.Equals("Number", StringComparison.InvariantCultureIgnoreCase) || value.Equals("ByNumber", StringComparison.InvariantCultureIgnoreCase)) {
                            player.AutoSort = AutoSortOptions.ByRank;
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your cards will now be sorted \u000312by rank\u000F.", e.Sender.Nickname);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, string.Format("That isn't a valid setting. Please enter 'off', 'colour' or 'rank'.", value));
                    }
                    break;
                case "ALLOWDUELWITHBOT":
                case "ALLOWDUELBOT":
                case "DUELWITHBOT":
                case "DUELBOT":
                    if (value == null) {
                        if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player) && !player.AllowDuelWithBot)
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00034will not\u000F enter a duel with you.", e.Sender.Nickname);
                        else
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00039may\u000F enter a duel with you.", e.Sender.Nickname);
                    } else {
                        if (Bot.TryParseBoolean(value, out value3)) {
                            if (!this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player))
                                this.PlayerSettings.Add(e.Sender.Nickname, player = new PlayerSettings());
                            if (player.AllowDuelWithBot = value3)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00039may now\u000F enter a duel with you.", e.Sender.Nickname);
                            else
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00034will no longer\u000F enter a duel with you.", e.Sender.Nickname);
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'yes' or 'no'.", value));
                    }
                    break;
                case "HINTS":
                    if (value == null) {
                        if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player) && !player.Hints)
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00034will not\u000F give you hints.", e.Sender.Nickname);
                        else
                            Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00039may\u000F give you hints.", e.Sender.Nickname);
                    } else {
                        if (Bot.TryParseBoolean(value, out value3)) {
                            if (!this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player))
                                this.PlayerSettings.Add(e.Sender.Nickname, player = new PlayerSettings());
                            if (player.Hints = value3)
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00039may now\u000F give you hints.", e.Sender.Nickname);
                            else
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, I \u00034will no longer\u000F give you hints.", e.Sender.Nickname);
                        } else if (value.Equals("reset", StringComparison.InvariantCultureIgnoreCase)) {
                            if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player)) {
                                for (int i = 0; i < UNOPlugin.Hints.Length; ++i) {
                                    player.HintsSeen[i] = false;
                                }
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, your hints have been reset.", e.Sender.Nickname);
                            } else {
                                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002, you don't seem to have a configuration entry.", e.Sender.Nickname);
                            }
                        } else
                            Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't recognise '{0}' as a Boolean value. Please enter 'yes', 'no' or 'reset'.", value));
                    }
                    break;
                default:
                    Bot.Say(e.Client, e.Sender.Nickname, string.Format("I don't manage a setting named \u0002{0}\u0002.", property));
                    break;
            }
        }

        internal bool SetPermissionCheck(CommandEventArgs e) {
            if (Bot.UserHasPermission(e.Client, e.Channel, e.Sender, this.Key + ".set"))
                return true;
            Bot.Say(e.Client, e.Channel, "You don't have access to that setting.");
            return false;
        }

        [Command(new string[] { "uhelp" }, 0, 1, "uhelp", "Gives information about the UNO game.")]
        public void CommandHelp(object sender, CommandEventArgs e) {
            Bot.Say(e.Client, e.Sender.Nickname, "For help with this UNO game, see " + (this.GuideURL ?? "http://questers-rest.andriocelos.ml/irc/uno/guide"));
        }

#region Preparation
        [Regex("^jo$")]
        public void RegexJoin(object sender, RegexEventArgs e) {
            Game game;
            if (this.Games.TryGetValue(e.Client.NetworkName + "/" + e.Channel, out game))
                this.EntryCommand(game, e.Sender.Nickname);
        }
        [Command(new string[] { "join", "ujoin", "uno" }, 0, 0, "ujoin", "Enters you into a game of UNO.",
            null, CommandScope.Channel)]
        public void CommandJoin(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (this.Games.TryGetValue(key, out game))
                this.EntryCommand(game, e.Sender.Nickname);
            else {
                // Start a new game.
                game = new Game(this, e.Client, e.Channel, this.EntryTime) { IsOpen = true };
                lock (game.Lock) {
                    this.Games.Add(key, game);
                    game.Players.Add(new Player(e.Sender.Nickname));
                    Bot.Say(e.Client, e.Channel, "\u000313\u0002{0}\u0002 is starting a game of UNO!", e.Sender.Nickname);
                    game.GameTimer.Elapsed += GameTimer_Elapsed;
                    game.HintTimer.Elapsed += HintTimer_Elapsed;
                    Thread.Sleep(600);
                    try {
                        // Alert players.
                        bool anyAlerts = false;
                        StringBuilder messageBuilder = new StringBuilder("\u0001ACTION alerts:");
                        foreach (KeyValuePair<string, PlayerSettings> player in this.PlayerSettings) {
                            if (player.Value.Highlight > 0 && player.Key != e.Sender.Nickname && e.Client.Channels[e.Channel].Users.Contains(player.Key)) {
                                messageBuilder.Append(" ");
                                messageBuilder.Append(player.Key);
                                anyAlerts = true;
                            }
                        }
                        if (anyAlerts) {
                            messageBuilder.Append("\u0001");
                            Bot.Say(e.Client, e.Channel, messageBuilder.ToString());
                            Thread.Sleep(600);
                        }
                    } finally {
                        game.TurnStartTime = DateTime.Now;
                        game.WaitTime = this.EntryTime;
                        game.GameTimer.Start();
                        Bot.Say(e.Client, e.Channel, "\u000312Starting in \u0002{0}\u0002 seconds. Say \u000311!ujoin\u000312 if you wish to join the game.", this.EntryTime);
                        if (!game.Connection.CaseMappingComparer.Equals(e.Sender.Nickname, game.Connection.Me.Nickname))
                            this.EntryHints(game, e.Sender.Nickname);
                    }
                }
            }
        }

        [Command(new string[] { "aichallenge", "aisummon", "aijoin" }, 0, 0, "aichallenge", "Calls me into the game, even if there are already two or more players.",
            null, CommandScope.Channel)]
        public void CommandAIJoin(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.AIEnabled)
                Bot.Say(e.Client, e.Sender.Nickname, "The AI player is disabled.");
            else if (!this.Games.TryGetValue(key, out game))
                Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            else {
                PlayerSettings playerSettings;
                if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out playerSettings) && !playerSettings.AllowDuelWithBot) {
                    Bot.Say(e.Client, e.Sender.Nickname, "You have requested I not enter a duel with you, {0}. To change this, enter \u0002!uset AllowDuelBot yes\u0002.", e.Sender.Nickname);
                    return;
                }

                int index;
                index = game.IndexOf(e.Sender.Nickname);
                if (index == -1)
                    Bot.Say(e.Client, e.Sender.Nickname, "You must be in the game to use that command.");
                else
                    this.EntryCommand(game, e.Client.Me.Nickname);
            }
        }

        [Command(new string[] { "ustart", "start" }, 0, 0, "ustart", "Starts the game immediately.",
            ".start", CommandScope.Channel)]
        public void CommandStart(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game))
                Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            else {
                int index;
                index = game.IndexOf(e.Sender.Nickname);
                if (index == -1)
                    Bot.Say(e.Client, e.Sender.Nickname, "You must be in the game to use that command.");
                else {
                    if (!game.IsOpen) {
                        Bot.Say(game.Connection, e.Sender.Nickname, "The game has already started.");
                        return;
                    } else if (game.Players.Count < 2) {
                        Bot.Say(game.Connection, e.Sender.Nickname, "At least two players must be present.");
                        return;
                    }
                    bool OK = false;
                    foreach (Player player in game.Players) {
                        if (player.Name != game.Connection.Me.Nickname && player.Name != e.Sender.Nickname) {
                            OK = true;
                            break;
                        }
                    }
                    if (!OK && !Bot.UserHasPermission(e.Client, e.Channel, e.Sender, this.Key + ".start.botduel")) {
                        Bot.Say(e.Client, e.Sender.Nickname, "At least two non-bot players must be present.");
                        return;
                    }
                    game.GameTimer.Stop();
                    this.GameClose(game);
                }
            }
        }

        [Command(new string[] { "uwait", "wait" }, 0, 0, "uwait", "Extends the current time limit.",
            ".wait", CommandScope.Channel)]
        public void CommandWait(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game))
                Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            else {
                int index;
                index = game.IndexOf(e.Sender.Nickname);
                if (index == -1)
                    Bot.Say(e.Client, e.Sender.Nickname, "You must be in the game to use that command.");
                else {
                    if (!game.GameTimer.Enabled) {
                        Bot.Say(game.Connection, e.Sender.Nickname, "There's no time limit to extend.");
                    } else if (game.IsOpen) {
                        if (game.WaitTime >= this.EntryWaitLimit) {
                            Bot.Say(game.Connection, e.Sender.Nickname, "You may not extend the delay any more.");
                            return;
                        } else {
                            game.GameTimer.Stop();
                            game.WaitTime += this.EntryTime / 2;
                            game.GameTimer.Interval = Math.Max((game.TurnStartTime.AddSeconds(game.WaitTime) - DateTime.Now).TotalMilliseconds, this.EntryTime * 500);
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has extended the delay to \u0002{1}\u0002 / \u0002{2}\u0002 seconds.", e.Sender.Nickname, (int) game.GameTimer.Interval / 1000, game.WaitTime);
                            game.GameTimer.Start();
                        }
                    } else {
                        if (game.WaitTime >= this.TurnWaitLimit) {
                            Bot.Say(game.Connection, e.Sender.Nickname, "You may not extend the time limit any more.");
                            return;
                        } else {
                            game.GameTimer.Stop();
                            game.WaitTime += (this.TurnTime == 0 ? 30 : this.TurnTime / 2);
                            game.GameTimer.Interval = Math.Max((game.TurnStartTime.AddSeconds(game.WaitTime) - DateTime.Now).TotalMilliseconds, this.EntryTime * 500);
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has extended the time limit to \u0002{1}\u0002 / \u0002{2}\u0002 seconds.", e.Sender.Nickname, (int) game.GameTimer.Interval / 1000, game.WaitTime);
                            game.GameTimer.Start();
                        }
                    }
                }
            }
        }

        protected void EntryCommand(Game game, string nickname) {
            lock (game.Lock) {
                if (!game.IsOpen && !this.AllowMidGameJoin) {
                    Bot.Say(game.Connection, nickname, "Sorry {0}, but this game has already started.", nickname);
                    return;
                }
                if (game.Players.Any(player => game.Connection.CaseMappingComparer.Equals(player.Name, nickname))) {
                    Bot.Say(game.Connection, nickname, "You've already entered the game.", nickname);
                    return;
                }
                game.Players.Add(new Player(nickname));
                Bot.Say(game.Connection, game.Channel, "\u000313\u0002{0}\u0002 has joined the game.", nickname);
                if (!game.IsOpen)
                    this.DealCards(game, game.Players.Count - 1, 7, true);
                this.CheckPlayerCount(game);
                if (!game.Connection.CaseMappingComparer.Equals(nickname, game.Connection.Me.Nickname))
                    this.EntryHints(game, nickname);
            }
        }

        protected void EntryHints(Game game, string nickname) {
            PlayerSettings playerSettings;
            if (!this.PlayerSettings.TryGetValue(nickname, out playerSettings))
                this.PlayerSettings.Add(nickname, playerSettings = new PlayerSettings());
            else if (!playerSettings.Hints) return;
            if (!playerSettings.HintsSeen[13])
                this.ShowHint(game, game.Players.Count - 1, 13, 3, this.GuideURL ?? "http://questers-rest.andriocelos.ml/irc/uno/guide");
            else if (!playerSettings.HintsSeen[14])
                this.ShowHint(game, game.Players.Count - 1, 14, 3);
            else if (playerSettings.HintsSeen[0] && !playerSettings.HintsSeen[11])
                this.ShowHint(game, game.Players.Count - 1, 11, 3);
        }

        [Command(new string[] { "quit", "uquit", "leave", "uleave", "part", "upart" }, 0, 0, "uquit", "Removes you from the game of UNO.",
            null, CommandScope.Channel)]
        public void CommandQuit(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game))
                Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            else {
                lock (game.Lock) {
                    int index = game.IndexOf(e.Sender.Nickname);
                    if (index == -1)
                        Bot.Say(e.Client, e.Sender.Nickname, "You're not in this game.");
                    else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has left the game.", e.Sender.Nickname);
                        this.RemovePlayer(game, index);
                    }
                }
            }
        }

        public override bool OnNicknameChange(object sender, NicknameChangeEventArgs e) {
            Game game;
            foreach (KeyValuePair<string, Game> entry in this.Games) {
                if (entry.Key.StartsWith(((IRCClient) sender).NetworkName, StringComparison.InvariantCultureIgnoreCase)) {
                    game = entry.Value;
                    lock (game.Lock) {
                        int index = game.IndexOf(e.Sender.Nickname);
                        if (index != -1)
                            game.Players[index].Name = e.NewNickname;
                    }
                }
            }
            return base.OnNicknameChange(sender, e);
        }

        public override bool OnNicknameChangeSelf(object sender, NicknameChangeEventArgs e) {
            Game game;
            foreach (KeyValuePair<string, Game> entry in this.Games) {
                if (entry.Key.StartsWith(((IRCClient) sender).NetworkName, StringComparison.InvariantCultureIgnoreCase)) {
                    game = entry.Value;
                    lock (game.Lock) {
                        int index = game.IndexOf(e.Sender.Nickname);
                        if (index != -1)
                            game.Players[index].Name = e.NewNickname;
                    }
                }
            }
            return base.OnNicknameChangeSelf(sender, e);
        }

        public override bool OnChannelJoin(object sender, ChannelJoinEventArgs e) {
            if (e.Sender.Nickname == ((IRCClient) sender).Me.Nickname) this.StartResetTimer();
            return base.OnChannelJoin(sender, e);
        }

        public override bool OnChannelLeave(object sender, ChannelPartEventArgs e) {
            // Turn off their alerts if appropriate.
            PlayerSettings player;
            if (this.PlayerSettings.TryGetValue(e.Sender.Nickname, out player) &&
                player.Highlight == HighlightOptions.OnTemporary)
                player.Highlight = HighlightOptions.Off;

            Game game;
            if (this.Games.TryGetValue(((IRCClient) sender).NetworkName + "/" + e.Channel, out game)) {
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
                this.CheckPlayerCount(game);
            } else {
                game.Players[index].Rank = game.Players.Count(player => player.Presence != PlayerPresence.Left);
                game.Players[index].Presence = PlayerPresence.Left;
                game.Players[index].BasePoints -= this.QuitPenalty;

                this.StreakLoss(game, game.Players[index]);

                // If only one player remains, declare them the winner.
                int survivor = -1; int outCount = 0;
                for (int i = 0; i < game.Players.Count; i++) {
                    if (game.Players[i].Presence == PlayerPresence.Playing) {
                        if (survivor == -1)
                            survivor = i;
                        else {
                            survivor = -2;
                            break;
                        }
                    } else if (game.Players[i].Presence == PlayerPresence.Out)
                        outCount++;
                }
                if (survivor != -2) {
                    game.GameTimer.Stop();
                    if (survivor != -1) {
                        game.Turn = survivor;
                        this.AwardPoints(game, survivor);
                    }
                    this.EndGame(game);
                } else {
                    // Was it the leaving player's turn?
                    if (game.Turn == index) {
                        game.DrawnCard = 255;
                        game.WildColour &= 191;

                        // Advance the turn.
                        game.GameTimer.Stop();
                        game.Advance();

                        Thread.Sleep(600);
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002, it's now your turn.", game.Players[game.Turn].Name);
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

        public void CheckPlayerCount(Game game) {
            int index;
            if (game.Players.Count == 0) {
                game.GameTimer.Dispose();
                this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
            } else if (game.Players.Count == 2 && (index = game.IndexOf(game.Connection.Me.Nickname)) != -1) {
                foreach (Player player in game.Players) {
                    if (player.Name != game.Connection.Me.Nickname) {
                        PlayerSettings playerSettings;
                        if (this.PlayerSettings.TryGetValue(player.Name, out playerSettings) && !playerSettings.AllowDuelWithBot) {
                            Thread.Sleep(600);
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 has left the game.", game.Connection.Me.Nickname);
                            this.RemovePlayer(game, index);
                            break;
                        }
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

                // Update temporary highlight values.
                foreach (KeyValuePair<string, PlayerSettings> player in this.PlayerSettings) {
                    if (player.Value.Highlight == HighlightOptions.OnTemporaryOneGame) {
                        if (game.IndexOf(player.Key) == -1)
                            player.Value.Highlight = HighlightOptions.Off;
                        else
                            player.Value.Highlight = HighlightOptions.OnTemporary;
                    } else if (player.Value.Highlight == HighlightOptions.OnTemporary &&
                               game.IndexOf(player.Key) == -1)
                        player.Value.Highlight = HighlightOptions.OnTemporaryOneGame;
                }

                // Enter the bot.
                if (game.Players.Count == 1 && this.AIEnabled) {
                    PlayerSettings player;
                    if (!this.PlayerSettings.TryGetValue(game.Players[0].Name, out player) || player.AllowDuelWithBot)
                        this.EntryCommand(game, game.Connection.Me.Nickname);
                }

                if (game.Players.Count < 2) {
                    Bot.Say(game.Connection, game.Channel, "\u000312Not enough players joined. Please say \u000311!ujoin\u000312 when you're ready for a game.");
                    this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
                    return;
                }

                // Start the game.
                game.IsOpen = false;
                game.PlayersOut = new List<int>(game.Players.Count);

                // Wait for the shuffle to finish.
                lock (Game.LockShuffle) { }

                for (int i = 0; i < game.Players.Count; i++) {
                    this.GetStats(this.ScoreboardCurrent, game.Connection, game.Channel, game.Players[i].Name, true).Plays++;
                    this.GetStats(this.ScoreboardAllTime, game.Connection, game.Channel, game.Players[i].Name, true).Plays++;
                    this.DealCards(game, i, 7, true);
                    Thread.Sleep(600);
                }
                Bot.Say(game.Connection, game.Channel, "\u000313The game of UNO has started!");

                // Give the participation bonus.
                for (int i = 0; i < game.Players.Count; i++)
                    game.Players[i].BasePoints = this.ParticipationBonus;

                // Draw the first card.
                byte card;
                do {
                    Thread.Sleep(600);
                    game.Discards.Add(card = this.DrawCards(game, 1)[0]);
                    string message1 = "\u000312The first up-card is " + UNOPlugin.ShowCard(card) + "\u000312.";
                    string message2;
                    bool draw = false;

                    switch (card) {
                        case 65:
                            // Wild Draw Four; put it back.
                            game.Deck.Add(card);
                            game.Discards.RemoveAt(0);
                            continue;
                        case 10: case 26: case 42: case 58:
                            // Reverse card
                            game.Turn = game.Players.Count - 1;
                            game.IsReversed = true;
                            message2 = string.Format("\u000312Play will begin with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                            break;
                        case 11: case 27: case 43: case 59:
                            // Skip card
                            game.Turn = 1;
                            message2 = string.Format("\u000312\u0002{0}\u0002 is skipped; play will begin with \u0002{1}\u0002.", game.Players[0].Name, game.Players[game.Turn].Name);
                            break;
                        case 12: case 28: case 44: case 60:
                            // Draw Two card
                            if (Progressive) {
                                game.Turn = 0;
                                game.DrawCount = 2;
                                message2 = string.Format("\u000312Now waiting on \u0002{0}\u0002's response.", game.Players[game.Turn].Name);
                            } else {
                                game.Turn = 1;
                                message2 = string.Format("\u000312\u0002{0}\u0002 takes two cards; play will begin with \u0002{1}\u0002.", game.Players[0].Name, game.Players[game.Turn].Name);
                                draw = true;
                            }
                            break;
                        case 64:
                            // Wild card
                            game.Turn = 0;
                            game.WildColour = 128;
                            message2 = string.Format("\u000312\u0002{0}\u0002, play a card or choose the colour.", game.Players[game.Turn].Name);
                            break;
                        default:
                            game.Turn = 0;
                            message2 = string.Format("\u000312Play will begin with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                            break;
                    }
                    Bot.Say(game.Connection, game.Channel, message1 + " " + message2);
                    if (draw) this.DealCards(game, 0, 2, false);
                } while (card == 65);

                game.Players[game.Turn].CanMove = true;
                game.IdleTurn = game.Turn;
                this.ShowHand(game, game.Turn);
                game.StartTime = DateTime.Now;
                game.record.time = DateTime.UtcNow;
                this.StartGameTimer(game);
            }
            this.AICheck(game);
        }

        [Command("ustop", 0, 0, "ustop", "Stops the game of UNO without scoring. Use only in emergencies.",
            ".stop")]
        public void CommandStop(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game))
                Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            else {
                lock (game.Lock) {
                    game.GameTimer.Stop();
                    game.Ended = true;
                    game.record.duration = DateTime.UtcNow - game.record.time;
                    if (this.RecordRandomData) game.WriteRecord();
                    this.Games.Remove(key);
                    Bot.Say(e.Client, e.Channel, "\u000313The game has been cancelled.");
                }
            }
        }
#endregion

#region Gameplay
        public byte[] DealCards(Game game, int playerIndex, int number, bool initialDraw = false, bool showMessage = true) {
            byte[] cards = this.DrawCards(game, number);

            StringBuilder messageBuilder = new StringBuilder();

            if (initialDraw)
                messageBuilder.Append("You were dealt:");
            else
                messageBuilder.Append("You draw:");

            foreach (byte card in cards) {
                if (card == 128) break;
                messageBuilder.Append(" ");
                messageBuilder.Append(UNOPlugin.ShowCard(card));
                game.Players[playerIndex].Hand.Add(card);
            }
            if (showMessage && game.Players[playerIndex].Name != game.Connection.Me.Nickname)
                Bot.Say(game.Connection, game.Players[playerIndex].Name, messageBuilder.ToString());

            return cards;
        }

        public static string ShowCard(byte card) {
            if ((card & 64) != 0) {
                // Wild card
                if (card == 64)
                    return "\u00030,14\u0002 Wild \u000F";
                else if (card == 65)
                    return "\u00030,14 Wild \u0002\u00034D\u00038r\u00039a\u000312w \u00034F\u00038o\u00039u\u000312r \u000F";
                else
                    return "\u00034,14\u0002 ??? \u000F";
            } else {
                string colour; string colourCode; string rank;
                switch (card & 48) {
                    case  0: colour = "Red";    colourCode = "\u00030,4"; break;
                    case 16: colour = "Yellow"; colourCode = "\u00031,8"; break;
                    case 32: colour = "Green";  colourCode = "\u00031,9"; break;
                    case 48: colour = "Blue";   colourCode = "\u00030,12"; break;
                    default: colour = "???";    colourCode = "\u00034,14"; break;
                }
                switch (card & 15) {
                    case  0: rank = "0";        break;
                    case  1: rank = "1";        break;
                    case  2: rank = "2";        break;
                    case  3: rank = "3";        break;
                    case  4: rank = "4";        break;
                    case  5: rank = "5";        break;
                    case  6: rank = "6";        break;
                    case  7: rank = "7";        break;
                    case  8: rank = "8";        break;
                    case  9: rank = "9";        break;
                    case 10: rank = "Reverse";  break;
                    case 11: rank = "Skip";     break;
                    case 12: rank = "Draw Two"; break;
                    default: rank = "???";      break;
                }
                return string.Format("{0} {1} \u0002{2} \u000F", colourCode, colour, rank);
            }
        }

        public void ShowHand(Game game, int playerIndex) {
            if (game.Players[playerIndex].Name == game.Connection.Me.Nickname) return;

            StringBuilder messageBuilder = new StringBuilder();
            PlayerSettings playerSettings;
            if (this.PlayerSettings.TryGetValue(game.Players[playerIndex].Name, out playerSettings)) {
                if (playerSettings.AutoSort == AutoSortOptions.ByColour)
                    game.Players[playerIndex].SortHandByColour();
                else if (playerSettings.AutoSort == AutoSortOptions.ByRank)
                    game.Players[playerIndex].SortHandByRank();
            } else
                game.Players[playerIndex].SortHandByColour();

            messageBuilder.Append("You hold:");
            foreach (byte card in game.Players[playerIndex].Hand) {
                messageBuilder.Append(" ");
                messageBuilder.Append(UNOPlugin.ShowCard(card));
            }
            Bot.Say(game.Connection, game.Players[playerIndex].Name, messageBuilder.ToString());
        }

        public byte[] DrawCards(Game game, int number) {
            byte[] cards = new byte[number];
            bool deckEmptyMessage = false;
            for (int i = 0; i < number; i++) {
                // First, make sure that there are cards left.
                if (game.EndOfDeck) {
                    if (game.Discards.Count <= 1) {
                        // There are no cards left to draw!
                        Bot.Say(game.Connection, game.Channel, "\u000312There are \u0002still\u0002 no cards left!");
                        Thread.Sleep(600);
                        for (; i < number; i++)
                            cards[i] = 128;
                        return cards;
                    }
                    if (!deckEmptyMessage) {
                        Bot.Say(game.Connection, game.Channel, "\u000312There are no cards left. Let's shuffle the discards back into the deck...");
                        Thread.Sleep(600);
                        deckEmptyMessage = true;
                    }
                    game.Shuffle();
                }
                cards[i] = game.DrawCard();
            }
            return cards;
        }

        [Regex(@"^pl\s*(.*)", null, CommandScope.Channel)]
        public void RegexPlay(object sender, RegexEventArgs e) {
            Game game; int index; byte card; byte colour;
            UNOPlugin.TryParseCard(e.Match.Groups[1].Value, out card, out colour);
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, card != 128, out game, out index))
                return;
            lock (game.Lock) {
                if (index == game.Turn && (game.WildColour & 64) != 0 && (game.Discards[game.Discards.Count - 1] != 65 || (this.WildDrawFour == WildDrawFourRule.AllowBluffing && game.DrawFourBadColour != 128) ||
                      game.Players.Where(player => player.Presence == PlayerPresence.Playing).Skip(2).Any())) {
                    // In a two-player game, you can play a card right on top of your own Wild Draw Four.
                    Bot.Say(e.Client, e.Sender.Nickname, "Please choose a colour for your wild card. Say \u0002red\u0002, \u0002yellow\u0002, \u0002green\u0002 or \u0002blue\u0002.");
                } else if (game.DrawFourChallenger == index && (!Progressive || card != 65)) {
                    Bot.Say(e.Client, e.Sender.Nickname, "That's a wild draw four. You must either \u0002!challenge\u0002 it, or say \u0002!draw\u0002 to take the four cards. Enter \u0002!uhelp drawfour\u0002 for more info.");
                } else if (Progressive && index == game.Turn && (game.DrawCount > 0 && (card & 15) != (game.Discards[game.Discards.Count - 1] & 15))) {
                    Bot.Say(e.Client, e.Sender.Nickname, "A Draw card has been played against you. You must either stack your own card of the same type, or say \u0002!draw\u0002 to take the penalty.");
                } else if (Progressive && game.DrawCount >= ProgressiveCap) {
                    Bot.Say(e.Client, e.Sender.Nickname, "You cannot stack any more.");
                } else if (card == 128) {
                    Bot.Say(e.Client, e.Sender.Nickname, "Oops! That's not a valid card. Enter \u0002!uhelp commands\u0002 if you're stuck.");
                } else {
                    this.PlayCheck(game, index, card, colour);
                }
            }
        }
        [Command(new string[] { "play", "pl", "uplay" }, 1, 1, "play <card>", "Allows you to play a card on your turn.")]
        public void CommandPlay(object sender, CommandEventArgs e) {
            Game game; int index; byte card; byte colour;
            UNOPlugin.TryParseCard(e.Parameters[0], out card, out colour);
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                if (index == game.Turn && (game.WildColour & 64) != 0 && (game.Discards[game.Discards.Count - 1] != 65 || (this.WildDrawFour == WildDrawFourRule.AllowBluffing && game.DrawFourBadColour != 128) ||
                      game.Players.Where(player => player.Presence == PlayerPresence.Playing).Skip(2).Any())) {
                    // In a two-player game, you can play a card right on top of your own Wild Draw Four.
                    Bot.Say(e.Client, e.Sender.Nickname, "Please choose a colour for your wild card. Say \u0002red\u0002, \u0002yellow\u0002, \u0002green\u0002 or \u0002blue\u0002.");
                } else if (game.DrawFourChallenger == index && (!Progressive || card != 65)) {
                    Bot.Say(e.Client, e.Sender.Nickname, "That's a wild draw four. You must either \u0002!challenge\u0002 it, or say \u0002!draw\u0002 to take the four cards. Enter \u0002!uhelp drawfour\u0002 for more info.");
                } else if (Progressive && index == game.Turn && (game.DrawCount > 0 && (card & 15) != (game.Discards[game.Discards.Count - 1] & 15))) {
                    Bot.Say(e.Client, e.Sender.Nickname, "A Draw card has been played against you. You must either stack your own card of the same type, or say \u0002!draw\u0002 to take the penalty.");
                } else if (Progressive && game.DrawCount >= ProgressiveCap) {
                    Bot.Say(e.Client, e.Sender.Nickname, "You cannot stack any more.");
                } else if (card == 128) {
                    Bot.Say(e.Client, e.Sender.Nickname, "Oops! That's not a valid card. Enter \u0002!uhelp commands\u0002 if you're stuck.");
                } else {
                    this.PlayCheck(game, index, card, colour);
                }
            }
        }

        public bool GameTurnCheck(IRCClient connection, string channel, string nickname, bool showMessages, out Game game, out int index) {
            if (!this.Games.TryGetValue(connection.NetworkName + "/" + channel, out game)) {
                if (showMessages)
                    Bot.Say(connection, nickname, "There's no game going on at the moment.");
                index = -1;
                return false;
            } else if (game.IsOpen) {
                if (showMessages)
                    Bot.Say(connection, nickname, "The game hasn't started yet!");
                index = -1;
                return false;
            } else {
                lock (game.Lock) {
                    index = game.IndexOf(nickname);
                    if (index == -1) {
                        if (showMessages)
                            Bot.Say(connection, nickname, "You're not in this game, {0}.", nickname);
                        return false;
                    } else if (!game.Players[index].CanMove) {
                        if (showMessages)
                            Bot.Say(connection, nickname, "It's not your turn.");
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool TryParseCard(string s, out byte value, out byte colour) {
            Match match = UNOPlugin.CardParseExpression.Match(s);
            if (match.Success) {
                if (match.Groups[19].Success)
                    value = 65;
                else if (match.Groups[20].Success)
                    value = 64;
                else {
                    if (match.Groups[1].Success)
                        value = 0;
                    else if (match.Groups[2].Success)
                        value = 16;
                    else if (match.Groups[3].Success)
                        value = 32;
                    else if (match.Groups[4].Success)
                        value = 48;
                    else
                        value = 0;

                    if (match.Groups[5].Success)
                        value |= (byte) (match.Groups[5].Value[0] - '0');
                    else if (match.Groups[6].Success)
                        value |= 0;
                    else if (match.Groups[7].Success)
                        value |= 1;
                    else if (match.Groups[8].Success)
                        value |= 2;
                    else if (match.Groups[9].Success)
                        value |= 3;
                    else if (match.Groups[10].Success)
                        value |= 4;
                    else if (match.Groups[11].Success)
                        value |= 5;
                    else if (match.Groups[12].Success)
                        value |= 6;
                    else if (match.Groups[13].Success)
                        value |= 7;
                    else if (match.Groups[14].Success)
                        value |= 8;
                    else if (match.Groups[15].Success)
                        value |= 9;
                    else if (match.Groups[16].Success)
                        value |= 10;
                    else if (match.Groups[17].Success)
                        value |= 11;
                    else if (match.Groups[18].Success)
                        value |= 12;
                    colour = (byte) Colour.None;
                    return true;
                }
                // Wild colour
                if (match.Groups[21].Success)
                    colour = (byte) Colour.Red;
                else if (match.Groups[22].Success)
                    colour = (byte) Colour.Yellow;
                else if (match.Groups[23].Success)
                    colour = (byte) Colour.Green;
                else if (match.Groups[24].Success)
                    colour = (byte) Colour.Blue;
                else
                    colour = (byte) Colour.Pending;
                return true;
            } else {
                value = 128;
                colour = (byte) Colour.None;
                return false;
            }
        }

        public static bool TryParseColour(string s, out byte colour) {
            Match match = UNOPlugin.ColourParseExpression.Match(s);
            if (match.Success) {
                if (match.Groups[1].Success)
                    colour = (byte) Colour.Red;
                else if (match.Groups[2].Success)
                    colour = (byte) Colour.Yellow;
                else if (match.Groups[3].Success)
                    colour = (byte) Colour.Green;
                else if (match.Groups[4].Success)
                    colour = (byte) Colour.Blue;
                else
                    colour = (byte) Colour.Pending;
                return true;
            } else {
                colour = (byte) Colour.None;
                return false;
            }
        }

        public void PlayCheck(Game game, int playerIndex, byte card, byte colour) {
            // After drawing a card, you can't play a different card.
            if (game.DrawnCard != 255 && playerIndex == game.Turn && card != game.DrawnCard) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You've already drawn a card this turn. You must play that card or pass.");
                return;
            }

            byte upCard = game.Discards[game.Discards.Count - 1];
            byte currentColour;
            if ((upCard & 64) != 0)
                currentColour = (byte) game.WildColour;
            else
                currentColour = (byte) (game.Discards[game.Discards.Count - 1] & 48);

            // Check that the card is legal.
            if ((currentColour & 128) == 0) {
                if (card == 65) {
                    if (this.WildDrawFour == WildDrawFourRule.DisallowBluffing) {
                        foreach (byte card2 in game.Players[playerIndex].Hand) {
                            if ((card2 & 64) == 0 && (card2 & 48) == currentColour) {
                                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You can't play a wild draw four, because you have a matching colour card.");
                                return;
                            }
                        }
                    }
                } else {
                    if ((card & 64) == 0 &&
                        (card & 48) != currentColour &&
                        ((upCard & 64) != 0 || (card & 15) != (upCard & 15))) {
                        Bot.Say(game.Connection, game.Players[playerIndex].Name, "You can't play that card right now. Please choose a different card, or enter \u000311!draw\u000F to draw from the deck.");
                        return;
                    }
                }
            }

            // Check that the player actually has this card.
            if (!game.Players[playerIndex].Hand.Contains(card)) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You don't have that card. Enter \u000311!hand\u000F to check your hand.");
                return;
            }

            game.GameTimer.Stop();
            game.WildColour = 128;
            this.IdleSkip(game, playerIndex);
            if (game.Ended) return;

            // Check the Wild Draw Four colour.
            if (card == 65)
                game.DrawFourBadColour = currentColour;

            game.Players[playerIndex].Hand.Remove(card);
            game.Discards.Add(card);

            // Did they go out?
            bool goneOut = (game.Players[playerIndex].Hand.Count == 0);
            bool hasUNO = (game.Players[playerIndex].Hand.Count == 1);
            bool stackEnded = false, endOfGame = false;

            if ((card & 15) == (byte) Rank.DrawTwo) {
                game.DrawCount += 2;
            } else if (card == 65) {
                game.DrawCount += 4;
            }

            // Check whether anyone has gone out.
            if (goneOut) {
                // They aren't yet out for certain if their play can be stacked onto.
                if (!Progressive || game.DrawCount == 0 || game.DrawCount >= ProgressiveCap) {
                    game.Players[playerIndex].Presence = PlayerPresence.Out;
                    // Count the remaining players.
                    int inCount = 0; int outCount = 0;
                    foreach (Player player in game.Players) {
                        if (player.Presence == PlayerPresence.Playing) ++inCount;
                        else if (player.Presence == PlayerPresence.Out) ++outCount;
                    }
                    if (inCount < 2) endOfGame = true;
                    else endOfGame = (outCount >= this.OutLimit);
                }
            }

            if (endOfGame) {
                Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1} \u000312and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card));
                // If it's a Draw card, deal the cards.
                if (game.DrawCount != 0) {
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {1} cards.", game.Players[game.NextPlayer()].Name, game.DrawCount);
                    // DrawCount will never be one, so the singular isn't needed.
                    Thread.Sleep(600);
                    stackEnded = true;
                }
            } else if ((card & 64) != 0) {
                string colourMessage = "\u00035???";
                if (colour == (byte) Colour.Red)
                    colourMessage = "\u00034red";
                else if (colour == (byte) Colour.Yellow)
                    colourMessage = "\u00038yellow";
                else if (colour == (byte) Colour.Green)
                    colourMessage = "\u00039green";
                else if (colour == (byte) Colour.Blue)
                    colourMessage = "\u000312blue";

                if (card == 65) {
                    // Wild Draw Four
                    string message1, message2 = "", message3;
                    bool draw = false; int victim = game.NextPlayer();

                    if (!goneOut && this.WildDrawFour == WildDrawFourRule.AllowBluffing && (currentColour & 128) == 0) {
                        // It can be challenged.
                        game.DrawFourUser = playerIndex;
                        game.DrawFourChallenger = game.NextPlayer();
                        message3 = "\u000312Now waiting on \u0002{3}\u0002's response.";
                        if (goneOut && Progressive && game.DrawCount < ProgressiveCap) goneOut = false;
                    } else if (Progressive && game.DrawCount < ProgressiveCap) {
                        // It can be stacked on.
                        if (goneOut) goneOut = false;
                        message3 = "\u000312Now waiting on \u0002{3}\u0002's response.";
                    } else {
                        // Deal the four-card punishment!
                        message2 = "\u000312\u0002{3}\u0002 draws {4} cards. ";
                        draw = true;
                        message3 = "\u000312Play continues with \u0002{5}\u0002.";
                    }

                    if (colour == (byte) Colour.Pending) {
                        game.WildColour = 192;
                        message1 = "\u000312\u0002{0}\u0002 plays {1}\u000312";
                    } else {
                        game.WildColour = colour;
                        if (goneOut) message1 = "\u000312\u0002{0}\u0002 plays {1}\u000312, chooses {2}";
                        else message1 = "\u000312\u0002{0}\u0002 plays {1}\u000312 and chooses {2}";
                    }

                    if (goneOut) message1 += " and \u0002goes out\u0002!";
                    else if (game.Players[game.Turn].Hand.Count == 0) message1 += " to \u0002go out\u0002!";  // It's not over yet; it can be stacked onto.
                    else message1 += ".";

                    if (colour != (byte) Colour.Pending) game.Advance();
                    else message3 = "\u000312Choose a colour, \u0002{0}\u0002.";

                    Bot.Say(game.Connection, game.Channel, message1, game.Players[playerIndex].Name, ShowCard(card), colourMessage, game.Players[victim].Name, game.DrawCount, game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, message2 + message3, game.Players[playerIndex].Name, ShowCard(card), colourMessage, game.Players[victim].Name, game.DrawCount, game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    if (draw) {
                        DealCards(game, victim, game.DrawCount);
                        game.DrawCount = 0;
                        stackEnded = true;
                        game.Advance();
                    }
                } else {
                    // Wild
                    if (colour == (byte) Colour.Pending) {
                        game.WildColour = 192;
                        if (goneOut)
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1} \u000312and \u0002goes out\u0002! Choose a colour, {0}.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card));
                        else
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1} \u000312Choose a colour, {0}.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card));
                    } else {
                        game.WildColour = colour;
                        game.Advance();
                        if (goneOut) {
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312, chooses {2}\u000312 and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), colourMessage);
                            Thread.Sleep(600);
                            Bot.Say(game.Connection, game.Channel, "\u000312Play continues with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                        } else
                            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1} \u000312to \u0002{3}\u0002 and chooses {2}\u000312.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), colourMessage, game.Players[game.Turn].Name);
                    }
                }
            } else if ((card & 15) == (byte) Rank.DrawTwo) {
                game.Advance();
                if (Progressive && game.DrawCount < ProgressiveCap) {
                    // It can be stacked onto.
                    if (goneOut) {
                        goneOut = false;
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 to \u0002go out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card));
                        Thread.Sleep(600);
                        Bot.Say(game.Connection, game.Channel, "\u000312Now waiting on \u0002{0}\u0002's response.", game.Players[game.Turn].Name);
                    } else
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312. Now waiting on \u0002{2}\u0002's response.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                } else {
                    if (goneOut) {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                        Thread.Sleep(600);
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {1} cards; play continues with \u0002{2}\u0002.", game.Players[game.Turn].Name, game.DrawCount, game.Players[game.NextPlayer()].Name);
                    } else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 \u0002{2}\u0002 draws two cards; play continues with \u0002{3}\u0002.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name, game.Players[game.NextPlayer()].Name);
                    }
                    Thread.Sleep(600);
                    this.DealCards(game, game.Turn, game.DrawCount, false);
                    game.DrawCount = 0;
                    stackEnded = true;
                    game.Advance();
                }
            } else if ((card & 15) == (byte) Rank.Reverse && (goneOut || game.Players.Where(player => player.Presence == PlayerPresence.Playing).Skip(2).Any())) {
                // Reverse card with more than two players
                game.IsReversed = !game.IsReversed;
                game.Advance();
                if (goneOut) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, "\u000312Play continues with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 Play continues with \u0002{2}\u0002.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                }
            } else if ((card & 14) == 10) {
                // Skip card, or Reverse card with two players
                game.Advance();
                int nextPlayer = game.NextPlayer();
                if (goneOut) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{2}\u0002 is skipped; play continues with \u0002{3}\u0002.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name, game.Players[nextPlayer].Name);
                    game.Advance();
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 \u0002{2}\u0002 is skipped; play continues with \u0002{3}\u0002.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name, game.Players[nextPlayer].Name);
                    game.Advance();
                }
            } else {
                // Number card
                game.Advance();
                if (goneOut) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 and \u0002goes out\u0002!", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, "\u000312Play continues with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 plays {1}\u000312 to \u0002{2}\u0002.", game.Players[playerIndex].Name, UNOPlugin.ShowCard(card), game.Players[game.Turn].Name);
                }
            }

            game.DrawnCard = 255;
            if (goneOut) {
                game.Players[playerIndex].Presence = PlayerPresence.Out;
                this.AwardPoints(game, playerIndex);
            }
            if (endOfGame) {
                this.EndGame(game);
            } else {
                if (goneOut) {
                    int totalPoints = game.Players[playerIndex].BasePoints + game.Players[playerIndex].HandPoints;
                    if (totalPoints > 0 && game.Players[playerIndex].Name != game.Connection.Me.Nickname) {
                        if (totalPoints == 1)
                            Bot.Say(game.Connection, game.Players[playerIndex].Name, "Congratulations: you won \u0002{0}\u0002 point.", totalPoints);
                        else
                            Bot.Say(game.Connection, game.Players[playerIndex].Name, "Congratulations: you won \u0002{0}\u0002 points.", totalPoints);
                    }
                } else if (hasUNO) {
                    Thread.Sleep(600);
                    Bot.Say(game.Connection, game.Channel, "\u000313\u0002{0}\u0002 has UNO!", game.Players[playerIndex].Name);
                }

                if (stackEnded) EndStack(game);
                if (!game.Ended) {
                    if ((game.WildColour & (byte) Colour.Pending) == 0 && game.DrawFourChallenger == -1) {
                        Thread.Sleep(600);
                        this.ShowHand(game, game.Turn);
                    }
                    this.StartGameTimer(game);
                    this.AICheck(game);
                }
            }
        }

        public void EndStack(Game game) {
            // This handles the case of someone going out with a Draw card.
            bool endOfGame; var newlyOut = new List<int>();

            // Count the remaining players.
            int inCount = 0; int outCount = 0;
            for (int i = 0; i < game.Players.Count; ++i) {
                var player = game.Players[i];

                if (player.Presence == PlayerPresence.Playing && player.Hand.Count == 0) {
                    player.Presence = PlayerPresence.Out;
                    newlyOut.Add(i);
                    this.AwardPoints(game, i);
                }

                if (player.Presence == PlayerPresence.Playing) ++inCount;
                else if (player.Presence == PlayerPresence.Out) ++outCount;
            }
            if (inCount < 2) endOfGame = true;
            else endOfGame = (this.OutLimit != int.MaxValue && outCount >= this.OutLimit);

            if (endOfGame) {
                this.EndGame(game);
            } else {
                // Tell players their score.
                foreach (int i in newlyOut) {
                    int totalPoints = game.Players[i].BasePoints + game.Players[i].HandPoints;
                    if (totalPoints > 0 && game.Players[i].Name != game.Connection.Me.Nickname) {
                        if (totalPoints == 1)
                            Bot.Say(game.Connection, game.Players[i].Name, "Congratulations: you won \u0002{0}\u0002 point.", totalPoints);
                        else
                            Bot.Say(game.Connection, game.Players[i].Name, "Congratulations: you won \u0002{0}\u0002 points.", totalPoints);
                    }
                }
            }
        }

        [Regex(@"^dr(?!\S)", null, CommandScope.Channel)]
        public void RegexDraw(object sender, RegexEventArgs e) {
            Game game; int index;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                this.DrawCheck(game, index);
            }
        }
        [Command("draw", 0, 0, "draw", "Allows you to draw a card from the deck",
            null, CommandScope.Channel)]
        public void CommandDraw(object sender, CommandEventArgs e) {
            Game game; int index;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                this.DrawCheck(game, index);
            }
        }

        public void DrawCheck(Game game, int playerIndex) {
            if (playerIndex == game.Turn && (game.WildColour & 64) != 0) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "Please choose a colour for your wild card. Say \u0002red\u0002, \u0002yellow\u0002, \u0002green\u0002 or \u0002blue\u0002.");
            } else if ((game.WildColour & 128) != 0 && (game.Discards[game.Discards.Count - 1] & 64) != 0 && playerIndex != game.DrawFourChallenger) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You must choose a colour for the wild card if you decline to discard.");
            } else if (game.DrawnCard != 255 && playerIndex == game.Turn) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You've already drawn a card this turn. Say \u0002!pass\u0002 to end your turn.");
            } else {
                this.IdleSkip(game, playerIndex);
                if (game.Ended) return;

                if (!game.Ended) {
                    if (game.DrawCount > 0) {
                        // The victim of a Wild Draw Four may enter the draw command if they don't want to challenge it.
                        // We deal the four cards here.
                        game.GameTimer.Stop();
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {1} cards.", game.Players[playerIndex].Name, game.DrawCount);
                        Thread.Sleep(600);
                        this.DealCards(game, playerIndex, game.DrawCount, false);

                        game.DrawCount = 0;
                        game.DrawFourChallenger = -1;
                        game.DrawFourUser = -1;
                        EndStack(game);

                        if (!game.Ended) {
                            game.Advance();
                            Bot.Say(game.Connection, game.Channel, "\u000312Play continues with \u0002{0}\u0002.", game.Players[game.Turn].Name);
                            this.ShowHand(game, game.Turn);
                            this.StartGameTimer(game);
                            this.AICheck(game);
                        }
                    } else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws a card.", game.Players[playerIndex].Name);
                        Thread.Sleep(600);
                        game.DrawnCard = this.DealCards(game, playerIndex, 1, false)[0];
                        this.AICheck(game);

                        PlayerSettings player;
                        if (!this.PlayerSettings.TryGetValue(game.Players[playerIndex].Name, out player))
                            this.PlayerSettings.Add(game.Players[playerIndex].Name, player = new PlayerSettings());
                        else if (!player.Hints) return;
                        if (!player.HintsSeen[5])
                            this.ShowHint(game, game.Turn, 5, 15);
                    }
                }
            }
        }

        [Regex(@"^pa(?!\S)", null, CommandScope.Channel)]
        public void RegexPass(object sender, RegexEventArgs e) {
            Game game; int index;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                this.PassCheck(game, index);
            }
        }
        [Command("pass", 0, 0, "pass", "Use this command after drawing a card to end your turn.",
            null, CommandScope.Channel)]
        public void CommandPass(object sender, CommandEventArgs e) {
            Game game; int index;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                this.PassCheck(game, index);
            }
        }

        public void PassCheck(Game game, int playerIndex) {
            if (playerIndex == game.Turn && (game.WildColour & 64) != 0) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "Please choose a colour for your wild card. Say \u0002red\u0002, \u0002yellow\u0002, \u0002green\u0002 or \u0002blue\u0002.");
            } else if ((game.WildColour & 128) != 0 && (game.Discards[game.Discards.Count - 1] & 64) != 0) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You must choose a colour for the wild card if you decline to discard.");
            } else if (game.DrawFourChallenger == playerIndex) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "That's a wild draw four. You must either \u0002!challenge\u0002 it, or say \u0002!draw\u0002 to take the four cards. Enter \u0002!uhelp drawfour\u0002 for more info.");
            } else if ((game.DrawnCard == 255 || playerIndex != game.Turn) && (game.Deck.Count >= 1 || game.Discards.Count >= 2)) {
                Bot.Say(game.Connection, game.Players[playerIndex].Name, "You must \u0002!draw\u0002 a card before passing.");
            } else {
                game.GameTimer.Stop();
                this.IdleSkip(game, playerIndex);
                if (game.Ended) return;
                game.Advance();
                Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 passes to \u0002{1}\u0002.", game.Players[playerIndex].Name, game.Players[game.Turn].Name);
                Thread.Sleep(600);
                this.ShowHand(game, game.Turn);
                this.StartGameTimer(game);
                this.AICheck(game);
            }
        }

        [Regex(@"^co (.*)", null, CommandScope.Channel)]
        public void RegexColour(object sender, RegexEventArgs e) {
            Game game; int index; byte colour;
            UNOPlugin.TryParseColour(e.Match.Groups[1].Value, out colour);
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, colour != 128, out game, out index))
                return;
            lock (game.Lock) {
                if (colour != 128)
                    this.ColourCheck(game, index, colour);
            }
        }
        [Regex(@"^(?:(Red)|(Yellow)|(Green)|(Blue))(?:!|~|\.*)$", null, CommandScope.Channel)]
        public void RegexColour2(object sender, RegexEventArgs e) {
            Game game; int index; byte colour;
            if (e.Match.Groups[1].Success)
                colour = (byte) Colour.Red;
            else if (e.Match.Groups[2].Success)
                colour = (byte) Colour.Yellow;
            else if (e.Match.Groups[3].Success)
                colour = (byte) Colour.Green;
            else if (e.Match.Groups[4].Success)
                colour = (byte) Colour.Blue;
            else
                return;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, false, out game, out index))
                return;
            lock (game.Lock) {
                this.ColourCheck(game, index, colour, false);
            }
        }
        [Command(new string[] { "colour", "color", "ucolour", "ucolor", "co" }, 1, 1, "colour <colour>", "Chooses a colour for your wild card.",
            null, CommandScope.Channel)]
        public void CommandColour(object sender, CommandEventArgs e) {
            Game game; int index; byte colour;
            UNOPlugin.TryParseColour(e.Parameters[0], out colour);
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                if (colour == 128)
                    Bot.Say(e.Client, e.Sender.Nickname, "That isn't a valid colour.");
                else
                    this.ColourCheck(game, index, colour);
            }
        }

        public void ColourCheck(Game game, int playerIndex, byte colour, bool showMessages = true) {
            if ((game.WildColour & 64) == 0 && ((game.WildColour & 128) == 0 || (game.Discards[game.Discards.Count - 1] & 64) == 0)) {
                if (showMessages) Bot.Say(game.Connection, game.Players[playerIndex].Name, "Use that command after you play a wild card.");
            } else if (game.DrawFourChallenger == playerIndex) {
                if (showMessages) Bot.Say(game.Connection, game.Players[playerIndex].Name, "That's a wild draw four. You must either \u0002!challenge\u0002 it, or say \u0002!draw\u0002 to take the four cards. Enter \u0002!uhelp drawfour\u0002 for more info.");
            } else {
                string colourMessage = "\u00035???";
                if (colour == (byte) Colour.Red)
                    colourMessage = "\u00034red";
                else if (colour == (byte) Colour.Yellow)
                    colourMessage = "\u00038yellow";
                else if (colour == (byte) Colour.Green)
                    colourMessage = "\u00039green";
                else if (colour == (byte) Colour.Blue)
                    colourMessage = "\u000312blue";

                game.GameTimer.Stop();
                if ((game.WildColour & 64) != 0 && game.Turn == playerIndex) {
                    game.WildColour = colour;
                    if (game.Discards[game.Discards.Count - 1] == 65 && game.DrawCount == 0)
                        game.Advance();
                    game.Advance();

                    if (game.DrawCount > 0)
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 chooses {1}\u000312. Now waiting on \u0002{2}\u0002's response.", game.Players[playerIndex].Name, colourMessage, game.Players[game.Turn].Name);
                    else {
                        Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 chooses {1}\u000312. Play continues with \u0002{2}\u0002.", game.Players[playerIndex].Name, colourMessage, game.Players[game.Turn].Name);
                        Thread.Sleep(600);
                        this.ShowHand(game, game.Turn);
                    }
                } else {
                    this.IdleSkip(game, playerIndex);
                    if (game.Ended) return;
                    game.WildColour = colour;
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 chooses {1}\u000312.", game.Players[playerIndex].Name, colourMessage);
                }
                this.StartGameTimer(game);
                this.AICheck(game);
            }
        }

        [Command(new string[] { "challenge", "uchallenge" }, 0, 0, "challenge", "Allows you to challenge a Wild Draw Four played on you.",
            null, CommandScope.Channel)]
        public void CommandChallenge(object sender, CommandEventArgs e) {
            Game game; int index;
            if (!this.GameTurnCheck(e.Client, e.Channel, e.Sender.Nickname, true, out game, out index))
                return;
            lock (game.Lock) {
                if (game.DrawFourChallenger == index) {
                    this.DrawFourChallenge(game, index);
                } else {
                    Bot.Say(game.Connection, game.Players[index].Name, "There's nothing to challenge.");
                }
            }
        }

        public void DrawFourChallenge(Game game, int playerIndex) {
            // Challenging a Wild Draw Four.
            game.GameTimer.Stop();
            this.IdleSkip(game, playerIndex);
            if (game.Ended) return;

            if (this.ShowHandOnChallenge && game.Players[game.DrawFourChallenger].Name != game.Connection.Me.Nickname) {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendFormat("\u0002{0}\u0002 holds:", game.Players[game.DrawFourUser].Name);
                foreach (byte card in game.Players[game.DrawFourUser].Hand) {
                    messageBuilder.Append(" ");
                    messageBuilder.Append(UNOPlugin.ShowCard(card));
                }
                Bot.Say(game.Connection, game.Players[game.Turn].Name, messageBuilder.ToString());
            }

            bool success = false;
            Thread.Sleep(1500);
            // Check the user's hand.
            foreach (byte card in game.Players[game.DrawFourUser].Hand) {
                if ((card & 112) == game.DrawFourBadColour) {
                    // An illegal card
                    success = true;
                    break;
                }
            }
            if (success) {
                Bot.Say(game.Connection, game.Channel, "\u000313The challenge succeeds.");
                Thread.Sleep(600);
                if (game.Players[game.DrawFourUser].Presence == PlayerPresence.Playing) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {2} cards; play continues with \u0002{1}\u0002.", game.Players[game.DrawFourUser].Name, game.Players[playerIndex].Name, game.DrawCount);
                    this.DealCards(game, game.DrawFourUser, game.DrawCount, false);
                    // That's right: you cop the entire accumulated penalty.
                } else {
                    Bot.Say(game.Connection, game.Channel, "\u000312Play continues with \u0002{0}\u0002.", game.Players[playerIndex].Name);
                    this.DealCards(game, game.DrawFourUser, game.DrawCount, false, false);
                }
            } else {
                Bot.Say(game.Connection, game.Channel, "\u000313The challenge fails.");
                Thread.Sleep(600);
                game.Advance();
                Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {2} cards; play continues with \u0002{1}\u0002.", game.Players[playerIndex].Name, game.Players[game.Turn].Name, game.DrawCount + 2);
                this.DealCards(game, playerIndex, game.DrawCount + 2, false);
            }
            game.DrawCount = 0;
            game.DrawFourChallenger = -1;
            game.DrawFourUser = -1;
            this.ShowHand(game, game.Turn);
            this.StartGameTimer(game);
            this.AICheck(game);
        }

        private void HintTimer_Elapsed(object sender, ElapsedEventArgs e) {
            foreach (Game game in this.Games.Values) {
                if (game.HintTimer == sender) {
                    PlayerSettings player;
                    if (!this.PlayerSettings.TryGetValue(game.Players[game.HintRecipient].Name, out player))
                        this.PlayerSettings.Add(game.Players[game.HintRecipient].Name, player = new PlayerSettings());
                    else if (!player.Hints) return;

                    if (game.HintParameters == null)
                        game.Connection.Send("NOTICE " + game.Players[game.HintRecipient].Name + " :\u00032[\u000312?\u00032]\u000F " + UNOPlugin.Hints[game.Hint]);
                    else
                        game.Connection.Send("NOTICE " + game.Players[game.HintRecipient].Name + " :\u00032[\u000312?\u00032]\u000F " + string.Format(UNOPlugin.Hints[game.Hint], game.HintParameters));
                    
                    if (game.Hint <= 2) game.Hint = 0;
                    player.HintsSeen[game.Hint] = true;
                }
                return;
            }
            ConsoleUtils.WriteLine("%cRED[{0}] Error: a game hint timer triggered, and I can't find which game it belongs to!", this.Key);
        }
        
        public void ShowHint(Game game, int recipient, int index, int delay, params object[] parameters) {
            game.HintRecipient = recipient;
            game.Hint = index;
            game.HintTimer.Interval = delay * 1000;
            game.HintParameters = parameters;
            game.HintTimer.Start();
        }

        public void AICheck(Game game) {
            game.HintTimer.Stop();
            if (game.Ended) {
                ConsoleUtils.WriteLine("%cRED[{0}] Error: The AI has been invoked on a game that already ended ({1}/{2}).", this.Key, game.Connection.NetworkName, game.Channel);
                return;
            }
            if (game.IsAIUp) {
                Thread AIThread = new Thread(() => this.AITurn(game));
                AIThread.Start();
            } else if (game.DrawnCard == 255 && game.Hint != 6) {
                PlayerSettings player;
                if (!this.PlayerSettings.TryGetValue(game.Players[game.Turn].Name, out player))
                    this.PlayerSettings.Add(game.Players[game.Turn].Name, player = new PlayerSettings());
                else if (!player.Hints) return;
                if (game.IdleTurn == game.Turn) {
                    if (game.DrawFourChallenger == game.Turn) {
                        if (!player.HintsSeen[10])
                            this.ShowHint(game, game.Turn, 10, 15);
                    } else {
                        if (!player.HintsSeen[15] && Progressive && game.DrawCount > 0)
                            this.ShowHint(game, game.Turn, 15, 15);
                        else if (!player.HintsSeen[0]) {
                            byte card = game.Discards[game.Discards.Count - 1];
                            string colour = ""; string rank = ""; int index = 0;
                            if (card >= 64) {
                                index = 1;
                                if (game.WildColour == 0) {
                                    colour = "red";
                                } else if (game.WildColour == 1) {
                                    colour = "yellow";
                                } else if (game.WildColour == 2) {
                                    colour = "green";
                                } else if (game.WildColour == 3) {
                                    colour = "blue";
                                } else {
                                    index = 2;
                                }
                            } else {
                                if ((card & 48) == 0) {
                                    colour = "red";
                                } else if ((card & 48) == 16) {
                                    colour = "yellow";
                                } else if ((card & 48) == 32) {
                                    colour = "green";
                                } else if ((card & 48) == 48) {
                                    colour = "blue";
                                } else {
                                    colour = "\u00034unknown colour\u000F";
                                }
                                if ((card & 15) < 10) {
                                    rank = (card & 15).ToString();
                                } else if ((card & 15) == 10) {
                                    rank = "Reverse";
                                } else if ((card & 15) == 11) {
                                    rank = "Skip";
                                } else if ((card & 15) == 12) {
                                    rank = "Draw Two";
                                } else {
                                    rank = "\u00034unknown rank\u000F";
                                }
                            }
                            this.ShowHint(game, game.Turn, index, 15, colour, rank);
                        } else if (!player.HintsSeen[4]) {
                            this.ShowHint(game, game.Turn, 4, 15);
                        } else if (!player.HintsSeen[5]) {
                            this.ShowHint(game, game.Turn, 5, 30);
                        } else if (!player.HintsSeen[8] && this.TurnTime != 0) {
                            this.ShowHint(game, game.Turn, 8, this.TurnTime * 2 / 3);
                        }
                    }
                } else {
                    if (!player.HintsSeen[9])
                        this.ShowHint(game, game.Turn, 9, 5, game.Players[game.IdleTurn].Name);
                }
            }
        }

        public void AITurn(Game game) {
            Thread.Sleep(600);
            lock (game.Lock) {
                int playerIndex = game.IndexOf(game.Connection.Me.Nickname);

                if (playerIndex != -1 && game.Players[playerIndex].CanMove) {
                    byte currentColour;
                    if ((game.Discards[game.Discards.Count - 1] & 64) != 0)
                        currentColour = (byte) game.WildColour;
                    else
                        currentColour = (byte) (game.Discards[game.Discards.Count - 1] & 48);

                    byte drawStack = 255;
                    if (game.DrawCount > 0) {
                        // Someone has played a Draw card on the bot.
                        byte upCard = game.Discards[game.Discards.Count - 1];
                        foreach (byte card in game.Players[playerIndex].Hand) {
                            if (upCard != 65 && (card & 15) == (byte) Rank.DrawTwo) {
                                drawStack = card;
                                break;
                            } else if (upCard == 65 && card == 65) {
                                // Check the legality of the Wild Draw Four.
                                foreach (byte card2 in game.Players[playerIndex].Hand) {
                                    if ((card2 & 64) == 0 && (card2 & 48) == currentColour) {
                                        continue;
                                    }
                                }
                                drawStack = card;
                                break;
                            }
                        }
                    }
                    if (drawStack != 255) {
                        // Stack a Draw card.
                        if ((drawStack & 64) != 0)
                            PlayCheck(game, playerIndex, drawStack, this.AIChooseColour(game, playerIndex));
                        else
                            PlayCheck(game, playerIndex, drawStack, 128);
                    } else if (playerIndex == game.Turn && (game.WildColour & 64) != 0) {
                        // We need to choose a colour for a wild card.
                        this.ColourCheck(game, playerIndex, this.AIChooseColour(game, playerIndex));
                    } else if (game.DrawFourChallenger == playerIndex) {
                        // Someone has played a Wild Draw Four on the bot.
                        int score = 0;
                        score -= game.Players[playerIndex].Hand.Count * 2;
                        score += game.Players[game.DrawFourUser].Hand.Count;
                        if (game.WildColour == game.DrawFourBadColour) score += 8;

                        if (score >= 4 && game.RNG.NextDouble() < (score * 0.08)) {
                            Bot.Say(game.Connection, game.Channel, "\u000313\u0002I\u0002 challenge the Wild Draw Four.");
                            this.DrawFourChallenge(game, playerIndex);
                        } else
                            this.DrawCheck(game, playerIndex);
                    } else if (game.DrawCount > 0) {
                        this.DrawCheck(game, playerIndex);
                    } else if (playerIndex == game.Turn && game.DrawnCard != 255) {
                        // We've already drawn a card this turn.
                        // Check that it is valid.
                        if (game.DrawnCard == 65) {
                            if (this.WildDrawFour == WildDrawFourRule.DisallowBluffing ||
                                (this.WildDrawFour == WildDrawFourRule.AllowBluffing && game.RNG.Next(100) < 75)) {
                                foreach (byte card in game.Players[playerIndex].Hand) {
                                    if ((card & 64) == 0 && (card & 48) == currentColour) {
                                        PassCheck(game, playerIndex);
                                        return;
                                    }
                                }
                            }
                        } else if ((game.DrawnCard & 64) == 0 && currentColour != 128) {
                            // If it's not a wild card, it must be the same colour or rank.
                            if ((game.DrawnCard & 48) != currentColour &&
                                ((game.Discards[game.Discards.Count - 1] & 64) != 0 || (game.DrawnCard & 15) != (game.Discards[game.Discards.Count - 1] & 15))) {
                                PassCheck(game, playerIndex);
                                return;
                            }
                        }
                        if ((game.DrawnCard & 64) != 0)
                            PlayCheck(game, playerIndex, game.DrawnCard, this.AIChooseColour(game, playerIndex));
                        else
                            PlayCheck(game, playerIndex, game.DrawnCard, 128);
                    } else {
                        // We need to play a card or draw.
                        List<byte> cards = new List<byte>(8);
                        foreach (byte card in game.Players[playerIndex].Hand) {
                            bool legal = false;
                            if (currentColour == 128)
                                legal = true;
                            else if (card == 65) {
                                legal = true;
                                if (this.WildDrawFour == WildDrawFourRule.DisallowBluffing ||
                                    (this.WildDrawFour == WildDrawFourRule.AllowBluffing && game.RNG.Next(8) != 0)) {
                                    // Check the legality of the Wild Draw Four.
                                    foreach (byte card2 in game.Players[playerIndex].Hand) {
                                        if ((card2 & 64) == 0 && (card2 & 48) == currentColour) {
                                            legal = false;
                                            break;
                                        }
                                    }
                                }
                            } else if (card == 64)
                                legal = true;
                            else {
                                // If it's not a wild card, it must be the same colour or rank.
                                if ((card & 48) == currentColour ||
                                    ((game.Discards[game.Discards.Count - 1] & 64) == 0 && (card & 15) == (game.Discards[game.Discards.Count - 1] & 15)))
                                    legal = true;
                            }
                            if (legal)
                                cards.Add(card);
                        }

                        if (cards.Count == 0) {
                            // No cards; we have to draw.
                            if ((game.WildColour & 128) != 0 && (game.Discards[game.Discards.Count - 1] & 64) != 0)
                                this.ColourCheck(game, playerIndex, this.AIChooseColour(game, playerIndex));
                            this.DrawCheck(game, playerIndex);
                        } else {
                            // Pick a random card to play.
                            byte card = cards[game.RNG.Next(cards.Count)];
                            if ((card & 64) != 0)
                                PlayCheck(game, playerIndex, card, this.AIChooseColour(game, playerIndex));
                            else
                                PlayCheck(game, playerIndex, card, 128);
                        }
                    }
                }
            }
        }
        internal byte AIChooseColour(Game game, int playerIndex) {
            int[] colourCount = new int[4];
            int colour = -1;

            foreach (byte card in game.Players[playerIndex].Hand) {
                if ((card & 64) == 0)
                    ++colourCount[(card & 48) >> 4];
            }
            for (int i = 0; i < 4; ++i) {
                if (colour == -1) {
                    if (colourCount[i] > 0) colour = i;
                } else {
                    if (colourCount[i] > colourCount[colour]) colour = i;
                    else if (colourCount[i] == colourCount[colour] && game.RNG.Next(2) == 0) colour = i;
                }
            }
            if (colour == -1)
                colour = game.RNG.Next(4);
            return (byte) (colour << 4);
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
            int nextPlayer = game.NextPlayer(game.IdleTurn);
            if (nextPlayer == game.Turn) {
                // Stop the game if everyone idles out and it goes full circle.
                game.Ended = true;
                this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
                Bot.Say(game.Connection, game.Channel, "\u000313The game has been cancelled.");
                return false;
            }
            game.IdleTurn = nextPlayer;
            game.Players[nextPlayer].CanMove = true;
            Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 may play now.", game.Players[game.IdleTurn].Name);
            this.StartGameTimer(game);
            return true;
        }

        public void IdleSkip(Game game, int skipTo) {
            while (game.Turn != skipTo) {
                if (game.DrawCount > 0) {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 draws {1} cards.", game.Players[game.Turn].Name, game.DrawCount);
                    Thread.Sleep(600);
                    this.DealCards(game, game.Turn, game.DrawCount, false);
                    game.DrawCount = 0;
                    game.DrawFourChallenger = -1;
                    game.DrawFourUser = -1;
                    EndStack(game);
                } else if (game.DrawnCard != 255)
                    game.DrawnCard = 255;
                else if ((game.WildColour & 64) != 0)
                    game.WildColour ^= 64;
                else {
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 takes one card.", game.Players[game.Turn].Name);
                    Thread.Sleep(600);
                    this.DealCards(game, game.Turn, 1, false);
                }
                game.Players[game.Turn].CanMove = false;
                game.Turn = game.NextPlayer();
            }
            game.Players[skipTo].IdleCount = 0;
        }

        public int AwardPoints(Game game, int playerIndex) {
            Player player = game.Players[playerIndex];
            player.Rank = game.PlayersOut.Count + 1;
            game.PlayersOut.Add(playerIndex);

            // Hand bonus
            if (this.HandBonus) {
                foreach (Player player2 in game.Players) {
                    if (player2 == player) continue;
                    player.HandPoints += UNOPlugin.GetHandTotal(player2.Hand);
                }
            }

            // Victory bonus
            if (this.VictoryBonus) {
                if (player.Rank >= game.Players.Count && !this.VictoryBonusLastPlace) { }
                else if (player.Rank <= this.VictoryBonusValue.Length)
                    player.BasePoints += this.VictoryBonusValue[player.Rank - 1];
                else if (this.VictoryBonusRepeat)
                    player.BasePoints += this.VictoryBonusValue[this.VictoryBonusValue.Length - 1];
            }

            // The participation bonus and quit penalty are handled when the game starts and when the player leaves respectively.

            // Total points
            int totalPoints = player.BasePoints + player.HandPoints;

            PlayerStats currentStats = this.GetStats(this.ScoreboardCurrent, game.Connection, game.Channel, player.Name, true);
            currentStats.Points += totalPoints;
            currentStats.ChallengePoints += totalPoints;
            if (player.Rank == 1) {
                ++currentStats.Wins;
                this.StreakWin(game, player);
            }

            PlayerStats allTimeStats = this.GetStats(this.ScoreboardAllTime, game.Connection, game.Channel, player.Name, true);
            allTimeStats.Points += totalPoints;
            allTimeStats.ChallengePoints += totalPoints;
            if (player.Rank == 1) ++allTimeStats.Wins;

            // Check the single-round record.
            if (totalPoints > currentStats.RecordPoints) {
                currentStats.RecordPoints = totalPoints;
                currentStats.RecordTime = DateTime.Now;
            }
            if (totalPoints > allTimeStats.RecordPoints) {
                allTimeStats.RecordPoints = totalPoints;
                allTimeStats.RecordTime = DateTime.Now;
                game.RecordBreakers.Add(player.Name);
            }

            return totalPoints;
        }

        public void CountHandPoints(Game game, int playerIndex) {
            foreach (Player player in game.Players) {
                if (player == game.Players[playerIndex]) continue;
                game.Players[playerIndex].HandPoints += UNOPlugin.GetHandTotal(player.Hand);
            }
        }

        public static int GetHandTotal(List<byte> hand) {
            int total = 0;
            foreach (byte card in hand) {
                if ((card & 64) != 0)
                    total += 50;
                else if ((card & 15) >= 10)
                    total += 20;
                else
                    total += (card & 15);
            }
            return total;
        }

        public void EndGame(Game game) {
            game.Ended = true;

            // Calculate the duration.
            TimeSpan time; string timeMessage; string minutes = null; string seconds = null;
            time = DateTime.Now - game.StartTime;
            game.record.duration = time;
            if (this.RecordRandomData) game.WriteRecord();

            int winnerCount = game.PlayersOut.Count;
            int playerCount = game.Players.Count(player => player.Presence != PlayerPresence.Left);
            for (int i = 0; i < game.Players.Count; ++i) {
                Player player = game.Players[i];
                if (player.Rank == 0) {
                    player.Rank = playerCount;
                    game.PlayersOut.Add(i);
                } else if (player.Rank > winnerCount)
                    game.PlayersOut.Add(i);
            }
            Thread.Sleep(2000);
            Bot.Say(game.Connection, game.Channel, "\u000313This game is finished.");
            Thread.Sleep(2000);

            // Show the duration.
            if (time.Minutes != 0) {
                if (time.Minutes == 1)
                    minutes = "{0} minute";
                else
                    minutes = "{0} minutes";
            }
            if (minutes == null || time.Seconds != 0) {
                if (time.Seconds == 1)
                    seconds = "{1} second";
                else
                    seconds = "{1} seconds";
            }
            if (minutes == null)
                timeMessage = seconds;
            else if (seconds == null)
                timeMessage = minutes;
            else
                timeMessage = string.Format("{0}, {1}", minutes, seconds);
            timeMessage = string.Format(timeMessage, time.Minutes, time.Seconds);
            Bot.Say(game.Connection, game.Channel, "\u000312The game lasted \u0002{0}\u0002.", timeMessage);
            Thread.Sleep(600);

            // Show players' hands.
            foreach (Player player in game.Players) {
                if (player.Hand.Count == 0) continue;

                StringBuilder messageBuilder = new StringBuilder();
                foreach (byte card in player.Hand) {
                    if (messageBuilder.Length != 0)
                        messageBuilder.Append(" ");
                    messageBuilder.Append(UNOPlugin.ShowCard(card));
                }
                int handTotal = UNOPlugin.GetHandTotal(player.Hand);
                if (!this.HandBonus)
                    Bot.Say(game.Connection, game.Channel, "\u0002{0}\u0002 still held: {1}", player.Name, messageBuilder.ToString(), handTotal);
                else if (handTotal == 1)
                    Bot.Say(game.Connection, game.Channel, "\u0002{0}\u0002 still held: {1} \u000F: \u0002{2}\u0002 point", player.Name, messageBuilder.ToString(), handTotal);
                else
                    Bot.Say(game.Connection, game.Channel, "\u0002{0}\u0002 still held: {1} \u000F: \u0002{2}\u0002 points", player.Name, messageBuilder.ToString(), handTotal);

                // Take points from their challenge score.
                // If the game ends prematurely, and the remaining player wins without going out, it'll still be their turn.
                // If the game ends normally, the turn will still be set to the last player to go out.
                if (this.HandBonus && game.Players[game.Turn] != player)
                    player.HandPoints -= handTotal;

                Thread.Sleep(600);
            }

            // Award points.
            for (int i = 0; i < game.PlayersOut.Count; ++i) {
                Player player = game.Players[game.PlayersOut[i]];

                PlayerStats currentStats = this.GetStats(this.ScoreboardCurrent, game.Connection, game.Channel, player.Name, true);
                if (player.HandPoints < 0)
                    currentStats.ChallengePoints += player.HandPoints;
                if (player.Presence != PlayerPresence.Out && game.Players[game.Turn] != player) {
                    // If the player never went out and never had their points counted, do that now.
                    ++currentStats.Losses;
                    currentStats.Points += player.BasePoints;
                    currentStats.ChallengePoints += player.BasePoints;
                }

                PlayerStats allTimeStats = this.GetStats(this.ScoreboardAllTime, game.Connection, game.Channel, player.Name, true);
                if (player.HandPoints < 0)
                    allTimeStats.ChallengePoints += player.HandPoints;
                if (player.Presence != PlayerPresence.Out && game.Players[game.Turn] != player) {
                    ++allTimeStats.Losses;
                    allTimeStats.Points += player.BasePoints;
                    allTimeStats.ChallengePoints += player.BasePoints;
                }

                int totalPoints = (player.HandPoints < 0 ? 0 : player.HandPoints) + player.BasePoints;

                if (totalPoints == 1)
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 takes a total of \u0002{1}\u0002 point.", player.Name, totalPoints);
                else if (totalPoints > 1)
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 takes a total of \u0002{1}\u0002 points.", player.Name, totalPoints);
                else if (totalPoints == 0) {
                    if (player.Presence == PlayerPresence.Out)
                        Bot.Say(game.Connection, game.Channel, "\u000312Aww... \u0002{0}\u0002 didn't take any points.", player.Name, totalPoints);
                } else if (totalPoints == -1)
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 must lose \u0002{1}\u0002 point...", player.Name, -totalPoints);
                else
                    Bot.Say(game.Connection, game.Channel, "\u000312\u0002{0}\u0002 must lose \u0002{1}\u0002 points...", player.Name, -totalPoints);

                Thread.Sleep(600);
            }

            if (game.RecordBreakers.Count == 1) {
                Bot.Say(game.Connection, game.Channel, "\u000313That's a new record for \u0002{0}\u0002!", game.RecordBreakers[0]);
                Thread.Sleep(600);
            } else if (game.RecordBreakers.Count > 1) {
                Bot.Say(game.Connection, game.Channel, "\u000313That's a new record for \u0002{0}\u0002 and \u0002{1}\u0002!", string.Join("\u0002, \u0002", game.RecordBreakers.Take(game.RecordBreakers.Count - 1)), game.RecordBreakers[game.RecordBreakers.Count - 1]);
                Thread.Sleep(600);
            }

            // Check the streak.
            foreach (Player player in game.Players) {
                if (player.Presence != PlayerPresence.Out && game.Players[game.Turn] != player)
                    this.StreakLoss(game, player);

                if (player.StreakMessage != null) {
                    Bot.Say(game.Connection, game.Channel, player.StreakMessage);
                    Thread.Sleep(600);
                }
            }

            if (this.RandomDataURL != null)
                Bot.Say(game.Connection, game.Channel, "\u000312Random number data for this game may be found here: " + this.RandomDataURL, game.index);

            // Remove the game and save the scores.
            game.GameTimer.Dispose();
            this.Games.Remove(game.Connection.NetworkName + "/" + game.Channel);
            this.OnSave();
            this.StartResetTimer();

            foreach (Player player in game.Players) {
                if (player.Name == game.Connection.Me.Nickname) continue;

                PlayerSettings playerSettings;
                if (!this.PlayerSettings.TryGetValue(player.Name, out playerSettings))
                    this.PlayerSettings.Add(player.Name, playerSettings = new PlayerSettings());
                else if (!playerSettings.Hints || playerSettings.HintsSeen[12]) continue;

                game.Connection.Send("NOTICE " + player.Name + " :\u00032[\u000312?\u00032]\u000F " + UNOPlugin.Hints[12]);
                playerSettings.HintsSeen[12] = true;
            }
        }

        [Command(new string[] { "ainudge", "nudge", "uainudge", "unudge" }, 0, 0, "ainudge", "Reminds me to take my turn")]
        public void ComandAINudge(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            e.Cancel = false;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            } else if (game.IsOpen) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                this.AICheck(game);
            }
        }
#endregion

#region Reminder commands
        [Regex(@"^tu(?!\S)", null, CommandScope.Channel)]
        public void RegexTurn(object sender, RegexEventArgs e) {
            this.CommandTurn(sender, new CommandEventArgs(e.Client, e.Channel, e.Sender,
                new string[] { e.Match.Length > 2 ? "" : null }));
        }
        [Command(new string[] { "turn", "uturn", "tu" }, 0, 0, "turn", "Reminds you whose turn it is.",
            null, CommandScope.Channel)]
        public void CommandTurn(object sender, CommandEventArgs e) {
            Game game; int index;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            } else if (game.IsOpen) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    this.CheckTimerReset(game);
                    index = game.IndexOf(e.Sender.Nickname);
                    if (game.Turn == index)
                        Bot.Say(e.Client, e.Channel, "It's your turn, \u0002{0}\u0002.", game.Players[game.Turn].Name);
                    else
                        Bot.Say(e.Client, e.Channel, "It is \u0002{0}\u0002's turn.", game.Players[game.Turn].Name);
                }
            }
        }

        [Regex(@"^cd(?!\S)", null, CommandScope.Channel)]
        public void RegexUpCard(object sender, RegexEventArgs e) {
            this.CommandUpCard(sender, new CommandEventArgs(e.Client, e.Channel, e.Sender,
                new string[] { e.Match.Length > 2 ? "" : null }));
        }
        [Command(new string[] { "card", "upcard", "ucard", "uupcard", "cd" }, 0, 0, "turn", "Shows you the current up-card; that is, the most recent discard.",
            null, CommandScope.Channel)]
        public void CommandUpCard(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            } else if (game.IsOpen) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    this.CheckTimerReset(game);
                    byte card = game.Discards[game.Discards.Count - 1];
                    Bot.Say(e.Client, e.Channel, "The last discard was: {0}", UNOPlugin.ShowCard(card));
                    if ((card & 64) != 0) {
                        if ((game.WildColour & 64) != 0)
                            Bot.Say(e.Client, e.Channel, "A colour hasn't been chosen yet.");
                        else if ((game.WildColour & 128) != 0)
                            Bot.Say(e.Client, e.Channel, "No colour was chosen. You may play any card.");
                        else {
                            string colourMessage = "\u00035???";
                            if (game.WildColour == (byte) Colour.Red)
                                colourMessage = "\u00034red";
                            else if (game.WildColour == (byte) Colour.Yellow)
                                colourMessage = "\u00038yellow";
                            else if (game.WildColour == (byte) Colour.Green)
                                colourMessage = "\u00039green";
                            else if (game.WildColour == (byte) Colour.Blue)
                                colourMessage = "\u000312blue";
                            Bot.Say(e.Client, e.Channel, "The colour chosen is {0}\u000F.", colourMessage);
                        }
                    }
                    if (game.DrawCount > 0)
                        Bot.Say(e.Client, e.Channel, "The draw stack is at \u0002{0}\u0002.", game.DrawCount);
                }
            }
        }

        [Regex(@"^ca(?!\S)", null, CommandScope.Channel)]
        public void RegexHand(object sender, RegexEventArgs e) {
            this.CommandHand(sender, new CommandEventArgs(e.Client, e.Channel, e.Sender,
                new string[] { e.Match.Length > 2 ? "" : null }));
        }
        [Command(new string[] { "hand", "cards", "uhand", "ucards", "ca" }, 0, 0, "hand", "Shows you the cards in your hand",
            null, CommandScope.Channel)]
        public void CommandHand(object sender, CommandEventArgs e) {
            Game game; int index;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            } else if (game.IsOpen) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    this.CheckTimerReset(game);
                    index = game.IndexOf(e.Sender.Nickname);
                    if (index == -1) {
                        if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                            Bot.Say(e.Client, e.Sender.Nickname, "You're not in this game, {0}.", e.Sender.Nickname);
                    } else
                        this.ShowHand(game, index);
                }
            }
        }

        [Regex(@"^ct(?!\S)", null, CommandScope.Channel)]
        public void RegexCount(object sender, RegexEventArgs e) {
            this.CommandCount(sender, new CommandEventArgs(e.Client, e.Channel, e.Sender,
                new string[] { e.Match.Length > 2 ? "" : null }));
        }
        [Command(new string[] { "count", "ucount", "ct" }, 0, 0, "count", "Shows you the number of cards in each player's hand",
            null, CommandScope.Channel)]
        public void CommandCount(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            //} else if (game.IsOpen) {
            //    if (e.Parameters.Length == 0 || e.Parameters[0] != null)
            //        Bot.Say(e.Connection, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    this.CheckTimerReset(game);
                    StringBuilder messageBuilder = new StringBuilder();
                    int n = 0;
                    for (int i = 0; i < game.Players.Count; i++) {
                        Player player = game.Players[i];

                        if (messageBuilder.Length != 0)
                            messageBuilder.Append(" \u000315| ");
                        messageBuilder.Append("\u0002");
                        messageBuilder.Append(IRC.Colours.NicknameColour(player.Name));
                        messageBuilder.Append(player.Name);

                        if (player.Presence == PlayerPresence.Left)
                            messageBuilder.Append(" \u0002\u000314left the game");
                        else if (player.Presence == PlayerPresence.Out)
                            messageBuilder.Append(" \u0002\u000315is out");
                        else if (player.Hand.Count == 1)
                            messageBuilder.Append(" \u0002\u00034has UNO");
                        else
                            messageBuilder.AppendFormat(" \u0002\u000Fholds {0} cards", player.Hand.Count);

                        ++n;
                        if (n == 4) {
                            Bot.Say(e.Client, e.Channel, messageBuilder.ToString());
                            messageBuilder.Clear();
                            n = 0;
                        }
                    }
                    if (n != 0)
                        Bot.Say(e.Client, e.Channel, messageBuilder.ToString());
                }
            }
        }

        [Regex(@"^ti(?!\S)", null, CommandScope.Channel)]
        public void RegexTime(object sender, RegexEventArgs e) {
            this.CommandTime(sender, new CommandEventArgs(e.Client, e.Channel, e.Sender,
                new string[] { e.Match.Length > 2 ? "" : null }));
        }
        [Command(new string[] { "time", "utime", "ti" }, 0, 0, "time", "Tells you how long the game has lasted",
            null, CommandScope.Channel)]
        public void CommandTime(object sender, CommandEventArgs e) {
            Game game;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "There's no game going on at the moment.");
            } else if (game.IsOpen) {
                if (e.Parameters.Length == 0 || e.Parameters[0] != null)
                    Bot.Say(e.Client, e.Sender.Nickname, "The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    this.CheckTimerReset(game);
                    TimeSpan time; string timeMessage; string minutes = null; string seconds = null;
                    time = DateTime.Now - game.StartTime;
                    if (time.Minutes != 0) {
                        if (time.Minutes == 1)
                            minutes = "{0} minute";
                        else
                            minutes = "{0} minutes";
                    }
                    if (minutes == null || time.Seconds != 0) {
                        if (time.Seconds == 1)
                            seconds = "{1} second";
                        else
                            seconds = "{1} seconds";
                    }
                    if (minutes == null)
                        timeMessage = seconds;
                    else if (seconds == null)
                        timeMessage = minutes;
                    else
                        timeMessage = string.Format("{0}, {1}", minutes, seconds);
                    timeMessage = string.Format(timeMessage, time.Minutes, time.Seconds);
                    Bot.Say(e.Client, e.Channel, "We are \u0002{0}\u0002 into this game.", timeMessage);
                }
            }
        }

        public void CheckTimerReset(Game game) {
            if (this.TurnTime != 0 && !game.NoTimerReset && game.WaitTime == this.TurnTime) {
                game.NoTimerReset = true;
                game.GameTimer.Stop();
                game.GameTimer.Start();
            }
        }
#endregion

#region Cheats
#if (DEBUG)
        [Command(new string[] { "gimme", "ugimme" }, 0, 1, "ugimme [card]", "Gives you any card. If you're not a developer, you shouldn't be seeing this...")]
        public void CommandCheatGive(object sender, CommandEventArgs e) {
            Game game; int index;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 There's no game going on at the moment.");
            } else if (game.IsOpen) {
                Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    index = game.IndexOf(e.Sender.Nickname);
                    if (index == -1) {
                        Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 You're not in this game.", e.Sender.Nickname);
                    } else if (e.Parameters.Length == 0)
                        game.Players[index].Hand.Add(65);
                    else
                        game.Players[index].Hand.Add(byte.Parse(e.Parameters[0]));
                }
            }
        }

        [Command(new string[] { "clear", "uclear" }, 0, 0, "uclear", "Removes all of your cards. If you're not a developer, you shouldn't be seeing this...")]
        public void CommandCheatClear(object sender, CommandEventArgs e) {
            Game game; int index;
            string key = e.Client.NetworkName + "/" + e.Channel;
            if (!this.Games.TryGetValue(key, out game)) {
                Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 There's no game going on at the moment.");
            } else if (game.IsOpen) {
                Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 The game hasn't started yet!");
            } else {
                lock (game.Lock) {
                    index = game.IndexOf(e.Sender.Nickname);
                    if (index == -1) {
                        Bot.Say(e.Client, e.Sender.Nickname, "\u0002Thwarted!\u0002 You're not in this game.", e.Sender.Nickname);
                    } else
                        game.Players[index].Hand.Clear();
                }
            }
        }
#endif
#endregion

#region Statistics
        public PlayerStats GetStats(Dictionary<string, PlayerStats> list, IRCClient connection, string channel, string nickname, bool add = false) {
            string name = nickname;  // TODO: Add some sort of authentication to this.
            PlayerStats stats;

            if (list.TryGetValue(name, out stats))
                return stats;
            if (add) {
                stats = new PlayerStats() { Name = name, StartedPlaying = DateTime.Now };
                list.Add(name, stats);
                return stats;
            }
            return null;
        }

        public void StreakWin(Game game, Player player) {
            PlayerStats stats;
            stats = this.GetStats(this.ScoreboardAllTime, game.Connection, game.Channel, player.Name, true);

            if (stats.CurrentStreak < 0) {
                // A losing streak has been broken.
                IRCChannel channel; IRCChannelUser user; string gender = "their";
                if (game.Connection.Channels.TryGetValue(game.Channel, out channel)) {
                    if (channel.Users.TryGetValue(player.Name, out user)) {
                        gender = user.User.GenderRefTheir.ToLowerInvariant();
                    }
                }
                if (stats.CurrentStreak <= -2)
                    player.StreakMessage = string.Format("\u000313\u0002{0}\u0002 has broken {1} \u0002{2}\u0002-loss streak.", player.Name, gender, -stats.CurrentStreak);
                stats.CurrentStreak = 1;
            } else if (stats.CurrentStreak < short.MaxValue) {
                ++stats.CurrentStreak;
                if (stats.CurrentStreak == 3 || stats.CurrentStreak == 6 || (stats.CurrentStreak >= 10 && stats.CurrentStreak % 5 == 0))
                    player.StreakMessage = string.Format("\u000313\u0002{0}\u0002 has won \u0002{1}\u0002 games in a row!", player.Name, stats.CurrentStreak);
            }
        }
        public void StreakLoss(Game game, Player player) {
            PlayerStats stats;
            stats = this.GetStats(this.ScoreboardAllTime, game.Connection, game.Channel, player.Name, true);

            if (stats.CurrentStreak > 0) {
                // A winning streak has been broken.
                if (stats.CurrentStreak >= 2)
                    player.StreakMessage = string.Format("\u000313\u0002{0}\u0002's \u0002{1}\u0002-win streak has ended.", player.Name, stats.CurrentStreak);
                if (stats.CurrentStreak > stats.BestStreak) {
                    stats.BestStreak = stats.CurrentStreak;
                    stats.BestStreakTime = DateTime.Now;
                }
                stats.CurrentStreak = -1;
            } else if (stats.CurrentStreak > short.MinValue) {
                --stats.CurrentStreak;
            }
        }

        public void StartResetTimer() {
            if (this.StatsPeriodEnd == default(DateTime)) {
                this.StatsPeriodEnd = DateTime.UtcNow.Date.Add(new TimeSpan(14, 8, 0, 0));
                this.StatsResetTimer.Interval = 3600e+3;
            }
            if (this.ScoreboardCurrent.Count != 0)
                this.StatsResetTimer_Elapsed(null, null);
        }

        [Command(new string[] { "score", "rank", "uscore", "urank" }, 0, 1, "uscore [name]", "Shows you a player's (by default, your own) total score.")]
        public void CommandScore(object sender, CommandEventArgs e) {
            string target;
            if (e.Parameters.Length == 0)
                target = e.Sender.Nickname;
            else
                target = e.Parameters[0];

            PlayerStats stats;
            stats = this.GetStats(this.ScoreboardCurrent, e.Client, e.Channel, target, false);

            if (stats == null)
                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 hasn't played a game yet.", target);
            else {
                string rankString; bool tie;
                this.GetRank(stats, this.ScoreboardCurrent, out rankString, out tie);
                if (tie) {
                    if (stats.Points == 1)
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 is tying for \u0002{1}\u0002 place, with \u0002{2}\u0002 point.", target, rankString, stats.Points);
                    else
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 is tying for \u0002{1}\u0002 place, with \u0002{2}\u0002 points.", target, rankString, stats.Points);
                } else {
                    if (stats.Points == 1)
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 is in \u0002{1}\u0002 place, with \u0002{2}\u0002 point.", target, rankString, stats.Points);
                    else
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 is in \u0002{1}\u0002 place, with \u0002{2}\u0002 points.", target, rankString, stats.Points);
                }
            }
        }

        [Command(new string[] { "scorelast", "ranklast", "uscorelast", "uranklast" }, 0, 1, "uscore [name]", "Shows you a player's (by default, your own) total score last period.")]
        public void CommandScoreLast(object sender, CommandEventArgs e) {
            string target;

            if (this.ScoreboardLast == null || this.ScoreboardLast.Count == 0) {
                Bot.Say(e.Client, e.Channel, "We haven't had a complete scoring period yet.");
                return;
            }

            if (e.Parameters.Length == 0)
                target = e.Sender.Nickname;
            else
                target = e.Parameters[0];

            PlayerStats stats;
            stats = this.GetStats(this.ScoreboardLast, e.Client, e.Channel, target, false);

            if (stats == null)
                Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 didn't play last period.", target);
            else {
                string rankString; bool tie;
                this.GetRank(stats, this.ScoreboardLast, out rankString, out tie);
                if (tie) {
                    if (stats.Points == 1)
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 tied for \u0002{1}\u0002 place last period, with \u0002{2}\u0002 point.", target, rankString, stats.Points);
                    else
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 tied for \u0002{1}\u0002 place last period, with \u0002{2}\u0002 points.", target, rankString, stats.Points);
                } else {
                    if (stats.Points == 1)
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 was ranked \u0002{1}\u0002 last period, with \u0002{2}\u0002 point.", target, rankString, stats.Points);
                    else
                        Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 was ranked \u0002{1}\u0002 last period, with \u0002{2}\u0002 points.", target, rankString, stats.Points);
                }
            }
        }

        public int GetRank(PlayerStats player, Dictionary<string, PlayerStats> list, out string rankString, out bool tie) {
            int rank = 1; tie = false;
            foreach (PlayerStats player2 in list.Values) {
                if (player2 != player) {
                    if (player2.Points > player.Points)
                        ++rank;
                    else if (player2.Points == player.Points)
                        tie = true;
                }
            }
            rankString = UNOPlugin.RankString(rank);
            return rank;
        }

        public static string RankString(int rank) {
            StringBuilder builder = new StringBuilder(rank.ToString("N0"));
            if ((rank % 100) / 10 == 1)
                builder.Append("th");
            else {
                switch (rank % 10) {
                    case 1: builder.Append("st"); break;
                    case 2: builder.Append("nd"); break;
                    case 3: builder.Append("rd"); break;
                    default: builder.Append("th"); break;
                }
            }
            return builder.ToString();
        }

        [Command(new string[] { "top", "top10", "utop", "utop10", "unotop10", "leaderboard", "uleaderboard", "scoreboard", "uscoreboard" }, 0, short.MaxValue,
            "utop [top|nearme] [current|last|alltime] [total|challenge|wins|plays]",
            "Shows you the leaderboard. \u0002!utop\u0002 with no parameters shows the top 10 total scores. If you specify 'nearme', you'll see entries near yourself if you haven't quite made the top 10.")]
        public void CommandTop(object sender, CommandEventArgs e) {
            Dictionary<string, PlayerStats> list = this.ScoreboardCurrent; bool rivals = false; LeaderboardMode sortKey = LeaderboardMode.SortedByScore; string title = "Top scores"; string periodMessage = "";
            foreach (string s in e.Parameters) {
                if (s.Equals("top", StringComparison.InvariantCultureIgnoreCase)) {
                    rivals = false;
                } else if (s.Equals("near", StringComparison.InvariantCultureIgnoreCase) ||
                           s.Equals("nearme", StringComparison.InvariantCultureIgnoreCase) ||
                           s.Equals("rival", StringComparison.InvariantCultureIgnoreCase) ||
                           s.Equals("rivals", StringComparison.InvariantCultureIgnoreCase)) {
                    rivals = true;
                } else if (s.Equals("current", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardCurrent;
                    periodMessage = "";
                } else if (s.Equals("last", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardLast;
                    periodMessage = "last period";
                } else if (s.Equals("alltime", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardAllTime;
                    periodMessage = "all time";
                } else if (s.Equals("total", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByScore;
                    title = "Top scores";
                } else if (s.Equals("challenge", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByChallenge;
                    title = "Top challenge scores";
                } else if (s.Equals("wins", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByWins;
                    title = "Most victories";
                } else if (s.Equals("plays", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByPlays;
                    title = "Top participants";
                } else if (s.Equals("record", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByRecord;
                    title = "Highest single-round scores";
                } else if (s.Equals("streak", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByStreak;
                    title = "Top winning streaks";
                } else if (s.Equals("periodscore", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByBestPeriod;
                    title = "Top period scores";
                } else if (s.Equals("periodchallenge", StringComparison.InvariantCultureIgnoreCase)) {
                    sortKey = LeaderboardMode.SortedByBestPeriodChallenge;
                    title = "Top period challenge scores";
                }
            }
            if (list.Count == 0) {
                if (list == this.ScoreboardLast)
                    Bot.Say(e.Client, e.Channel, "We haven't had a complete scoring period yet.");
                else
                    Bot.Say(e.Client, e.Channel, "No one has scored yet.");
                return;
            }
            if (sortKey >= LeaderboardMode.SortedByRecord) {
                list = this.ScoreboardAllTime;
                periodMessage = "";
            }

            List<PlayerStats> top = UNOPlugin.SortLeaderboard(list.Values, sortKey);

            StringBuilder messageBuilder = new StringBuilder();

            int minRank = 0; int maxRank; int realRank = -1; long checkValue = -1L;
            if (rivals) {
                for (int i = 0; i < top.Count; ++i) {
                    if (e.Client.CaseMappingComparer.Equals(top[i].Name, e.Sender.Nickname)) {
                        minRank = i;
                        break;
                    }
                }
                minRank = Math.Max(minRank - 5, 0);
            }
            maxRank = Math.Min(minRank + 9, top.Count - 1);
            for (int i = minRank; i <= maxRank; ++i) {
                long value = -1L;

                if (sortKey == LeaderboardMode.SortedByScore)
                    value = top[i].Points;
                else if (sortKey == LeaderboardMode.SortedByPlays)
                    value = top[i].Plays;
                else if (sortKey == LeaderboardMode.SortedByWins)
                    value = top[i].Wins;
                else if (sortKey == LeaderboardMode.SortedByChallenge)
                    value = top[i].ChallengePoints;
                else if (sortKey == LeaderboardMode.SortedByRecord)
                    value = top[i].RecordPoints;
                else if (sortKey == LeaderboardMode.SortedByStreak)
                    value = top[i].BestStreak;
                else if (sortKey == LeaderboardMode.SortedByBestPeriod)
                    value = top[i].BestPeriodScore;
                else if (sortKey == LeaderboardMode.SortedByBestPeriodChallenge)
                    value = top[i].BestPeriodChallengeScore;

                // Find the player's real rank (which may be different from their position
                //   in the list if there's a tie).
                if (realRank == -1) {
                    realRank = i;
                    checkValue = value;
                    for (int j = i - 1; j > 0; --j) {
                        if (value > checkValue) break;
                        --realRank;
                    }
                } else if (value < checkValue) {
                    realRank = i;
                    checkValue = value;
                }

                messageBuilder.Append("  \u000314|  ");
                if (realRank == 0)
                    messageBuilder.Append("\u000312\u00021st\u0002  ");
                else if (realRank == 1)
                    messageBuilder.Append("\u00034\u00022nd\u0002  ");
                else if (realRank == 2)
                    messageBuilder.Append("\u00039\u00023rd\u0002  ");
                else if (realRank < 10)
                    messageBuilder.AppendFormat("\u000310\u0002{0}\u0002  ", UNOPlugin.RankString(realRank + 1));
                else
                    messageBuilder.AppendFormat("\u00036\u0002{0}\u0002  ", UNOPlugin.RankString(realRank + 1));

                if (e.Client.CaseMappingComparer.Equals(top[i].Name, e.Sender.Nickname))
                    messageBuilder.AppendFormat("\u000309{0}  \u000303{1:N0}", top[i].Name, value);
                else
                    messageBuilder.AppendFormat("\u000312{0}  \u000302{1:N0}", top[i].Name, value);
            }

            Bot.Say(e.Client, e.Channel, "\u000312\u0002{0} {1}\u0002{2}", title, periodMessage, messageBuilder.ToString());

            if (this.StatsPeriodEnd == default(DateTime) || list != this.ScoreboardCurrent) return;
            // Show the time remaining until the period ends.
            TimeSpan time = TimeSpan.FromMinutes(Math.Ceiling((this.StatsPeriodEnd - DateTime.UtcNow).TotalMinutes));  // This rounds it up to the nearext minute.
            string timeMessage; string[] timePart = new string[] { null, null, null };

            if (time.TotalMinutes < 1) {
                timeMessage = "\u0002less than 1\u0002 minute";
            } else {
                if (time.Days != 0) {
                    if (time.Days == 1)
                        timePart[0] = string.Format("\u0002{0}\u0002 day", time.Days);
                    else
                        timePart[0] = string.Format("\u0002{0}\u0002 days", time.Days);
                }
                if (time.Hours != 0) {
                    if (time.Hours == 1)
                        timePart[1] = string.Format("\u0002{0}\u0002 hour", time.Hours);
                    else
                        timePart[1] = string.Format("\u0002{0}\u0002 hours", time.Hours);
                }
                if (time.Minutes != 0) {
                    if (time.Minutes == 1)
                        timePart[2] = string.Format("\u0002{0}\u0002 minute", time.Minutes);
                    else
                        timePart[2] = string.Format("\u0002{0}\u0002 minutes", time.Minutes);
                }
                timeMessage = string.Join(", ", timePart.Where(part => part != null));
            }
            Bot.Say(e.Client, e.Channel, "This scoreboard resets in \u000312{0}\u000F.", timeMessage);
        }

        public static List<PlayerStats> SortLeaderboard(IEnumerable<PlayerStats> list, LeaderboardMode sortKey) {
            List<PlayerStats> result = new List<PlayerStats>(list);
            if (sortKey <= LeaderboardMode.Unsorted) return result;
            UNOPlugin.SortLeaderboardSub(result, sortKey, 0, result.Count - 1);
            return result;
        }
        private static void SortLeaderboardSub(List<PlayerStats> list, LeaderboardMode sortKey, int min, int max) {
            if (max <= min) return;
            PlayerStats swap;
            if (max - min == 1) {
                if (UNOPlugin.Compare(list[min], list[max], sortKey) > 0) {
                    swap = list[min];
                    list[min] = list[max];
                    list[max] = swap;
                }
                return;
            }

            PlayerStats pivot = list[max];
            int index = min;

            for (int i = min; i < max; ++i) {
                if (UNOPlugin.Compare(list[i], pivot, sortKey) < 0) {
                    if (i != index) {
                        // Swap this entry to the pointer position.
                        swap = list[index];
                        list[index] = list[i];
                        list[i] = swap;
                    }
                    ++index;
                }
            }

            // Enter the pivot.
            swap = list[index];
            list[index] = list[max];
            list[max] = swap;

            // Recursively sort the list.
            UNOPlugin.SortLeaderboardSub(list, sortKey, min, index - 1);
            UNOPlugin.SortLeaderboardSub(list, sortKey, index + 1, max);
        }
        private static int Compare(PlayerStats value1, PlayerStats value2, LeaderboardMode sortKey) {
            switch (sortKey) {
                case LeaderboardMode.SortedByName:
                    return IRC.IRCStringComparer.ASCII.Compare(value1.Name, value2.Name);
                case LeaderboardMode.SortedByScore:
                    if (value1.Points > value2.Points) return -1;
                    if (value1.Points < value2.Points) return 1;
                    return 0;
                case LeaderboardMode.SortedByPlays:
                    if (value1.Plays > value2.Plays) return -1;
                    if (value1.Plays < value2.Plays) return 1;
                    return 0;
                case LeaderboardMode.SortedByWins:
                    if (value1.Wins > value2.Wins) return -1;
                    if (value1.Wins < value2.Wins) return 1;
                    return 0;
                case LeaderboardMode.SortedByChallenge:
                    if (value1.ChallengePoints > value2.ChallengePoints) return -1;
                    if (value1.ChallengePoints < value2.ChallengePoints) return 1;
                    return 0;
                case LeaderboardMode.SortedByRecord:
                    if (value1.RecordPoints > value2.RecordPoints) return -1;
                    if (value1.RecordPoints < value2.RecordPoints) return 1;
                    if (value1.RecordTime < value2.RecordTime) return -1;
                    if (value1.RecordTime > value2.RecordTime) return 1;
                    return 0;
                case LeaderboardMode.SortedByStreak:
                    if (value1.BestStreak > value2.BestStreak) return -1;
                    if (value1.BestStreak < value2.BestStreak) return 1;
                    if (value1.BestStreakTime < value2.BestStreakTime) return -1;
                    if (value1.BestStreakTime > value2.BestStreakTime) return 1;
                    return 0;
                case LeaderboardMode.SortedByBestPeriod:
                    if (value1.BestPeriodScore > value2.BestPeriodScore) return -1;
                    if (value1.BestPeriodScore < value2.BestPeriodScore) return 1;
                    if (value1.BestPeriodScoreTime < value2.BestPeriodScoreTime) return -1;
                    if (value1.BestPeriodScoreTime > value2.BestPeriodScoreTime) return 1;
                    return 0;
                case LeaderboardMode.SortedByBestPeriodChallenge:
                    if (value1.BestPeriodChallengeScore > value2.BestPeriodChallengeScore) return -1;
                    if (value1.BestPeriodChallengeScore < value2.BestPeriodChallengeScore) return 1;
                    if (value1.BestPeriodChallengeScoreTime < value2.BestPeriodChallengeScoreTime) return -1;
                    if (value1.BestPeriodChallengeScoreTime > value2.BestPeriodChallengeScoreTime) return 1;
                    return 0;
                default:
                    return 0;
            }
        }

        [Command(new string[] { "stats", "ustats" }, 0, 3, "ustats [player] [current|last|alltime]", "Shows you a player's (by default, your own) extended statistics.")]
        public void CommandStats(object sender, CommandEventArgs e) {
            string target = e.Sender.Nickname;
            Dictionary<string, PlayerStats> list = this.ScoreboardCurrent;
            string periodMessage = ""; bool escape = false;
            foreach (string s in e.Parameters) {
                if (escape) {
                    target = s;
                    break;
                } else if (s.Equals("current", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardCurrent;
                    periodMessage = "";
                } else if (s.Equals("last", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardLast;
                    periodMessage = " last period";
                } else if (s.Equals("alltime", StringComparison.InvariantCultureIgnoreCase)) {
                    list = this.ScoreboardAllTime;
                    periodMessage = " all time";
                } else if (s == "--") {
                    escape = true;
                } else {
                    target = s;
                }
            }

            PlayerStats stats;
            stats = this.GetStats(list, e.Client, e.Channel, target, false);

            if (stats == null) {
                if (list == this.ScoreboardLast)
                    Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 didn't play during the last period.", target);
                else
                    Bot.Say(e.Client, e.Channel, "\u0002{0}\u0002 hasn't played a game yet.", target);
            } else {
                // Display the stats.
                string record;
                if (stats.RecordPoints == 0L)
                    record = "none";
                else
                    record = string.Format("{0} \u00032(\u000312{1}\u00032)", stats.RecordPoints.ToString("N0"), UNOPlugin.GetShortTimeDifferenceString(stats.RecordTime));

                Bot.Say(e.Client, e.Channel, "\u000313\u0002{0}\u0002's stats{7} \u000315|\u00034 Total points\u000312 {1} \u000315|\u00034 Games entered\u000312 {2} \u000315|\u00034 Wins\u000312 {3} \u000315|\u00034 Losses\u000312 {4} \u000315|\u00034 Single-round record\u000312 {5} \u000315|\u00034 Challenge score\u000312 {6}",
                    stats.Name, stats.Points.ToString("N0"), stats.Plays.ToString("N0"), stats.Wins.ToString("N0"), stats.Losses.ToString("N0"), record, stats.ChallengePoints.ToString("N0"), periodMessage);
                if (list == this.ScoreboardAllTime) {
                    string currentStreak; string bestStreak; string bestStreakLabel; string bestPeriodScore; string bestChallengeScore;
                    string[] placed = new string[3];

                    if (stats.CurrentStreak == 0)
                        currentStreak = "none";
                    else {
                        if (stats.CurrentStreak == -1)
                            currentStreak = string.Format("{0} loss", -stats.CurrentStreak);
                        else if (stats.CurrentStreak < 0)
                            currentStreak = string.Format("{0} losses", -stats.CurrentStreak);
                        else if (stats.CurrentStreak == 1)
                            currentStreak = string.Format("{0} win", stats.CurrentStreak);
                        else
                            currentStreak = string.Format("{0} wins", stats.CurrentStreak);
                    }

                    if (stats.BestStreak == 0) {
                        bestStreak = "none";
                        bestStreakLabel = "Best streak";
                    } else {
                        if (stats.BestStreak == 1)
                            bestStreak = string.Format("{0} win", stats.BestStreak);
                        else
                            bestStreak = string.Format("{0} wins", stats.BestStreak);
                        bestStreak = string.Format("{0} \u00032(\u000312{1}\u00032)", bestStreak, UNOPlugin.GetShortTimeDifferenceString(stats.BestStreakTime));

                        if (stats.CurrentStreak > stats.BestStreak)
                            bestStreakLabel = "Former best";
                        else
                            bestStreakLabel = "Best streak";
                    }

                    if (stats.BestPeriodScore == 0L)
                        bestPeriodScore = "none";
                    else
                        bestPeriodScore = string.Format("{0} \u00032(\u000312{1}\u00032)", stats.BestPeriodScore.ToString("N0"), UNOPlugin.GetShortTimeDifferenceString(stats.BestPeriodScoreTime));

                    if (stats.BestPeriodChallengeScore == 0L)
                        bestChallengeScore = "none";
                    else
                        bestChallengeScore = string.Format("{0} \u00032(\u000312{1}\u00032)", stats.BestPeriodChallengeScore.ToString("N0"), UNOPlugin.GetShortTimeDifferenceString(stats.BestPeriodChallengeScoreTime));

                    for (int i = 0; i < 3; ++i) {
                        if (stats.Placed[i] == 1)
                            placed[i] = string.Format("{0} time", stats.Placed[i]);
                        else
                            placed[i] = string.Format("{0} times", stats.Placed[i]);
                    }

                    Bot.Say(e.Client, e.Channel, "\u000313\u0002{0}\u0002's stats all time \u000315|\u00034 Current streak\u000312 {1} \u000315|\u00034 {2}\u000312 {3} \u000315|\u00034 Best period score\u000312 {4} \u000315|\u00034 Best period challenge score\u000312 {5} \u000315| \u00034Placed \u000312\u00021st\u0002 {6} \u000315| \u00034\u00022nd\u0002 \u000312{7} \u000315| \u00039\u00023rd\u0002 \u000312{8}",
                        stats.Name, currentStreak, bestStreakLabel, bestStreak, bestPeriodScore, bestChallengeScore, placed[0], placed[1], placed[2]);
                }
            }
        }

        public static string GetShortTimeDifferenceString(DateTime date) {
            if (date == default(DateTime))
                return "a long time ago";
            else {
                TimeSpan time = DateTime.Now - date;
                if (time.TotalSeconds <= 2.0)
                    return "a moment ago";
                else if (time.TotalMinutes <= 2.0)
                    return ((int) time.TotalSeconds).ToString() + " seconds ago";
                else if (time.TotalHours <= 2.0)
                    return ((int) time.TotalMinutes).ToString() + " minutes ago";
                else if (time.TotalDays <= 2.0)
                    return ((int) time.TotalHours).ToString() + " hours ago";
                else
                    return ((int) time.TotalDays).ToString("N0") + " days ago";
            }
        }

        internal void StatsResetTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (this.StatsPeriodEnd == default(DateTime))
                return;
            if (StatsPeriodEnd <= DateTime.UtcNow)
                this.ResetScoreboard();
            else if (this.StatsPeriodEnd <= DateTime.UtcNow.AddHours(1)) {
                this.StatsResetTimer.Interval = 60e+3;
                this.StatsResetTimer.Start();
            } else {
                this.StatsResetTimer.Interval = 3600e+3;
                this.StatsResetTimer.Start();
            }
        }

        public void ResetScoreboard() {
            if (this.Games.Count == 0) {
                // Assemble the top 5 list.
                List<Tuple<string, long>> top = new List<Tuple<string, long>>(5);
                foreach (PlayerStats player in this.ScoreboardCurrent.Values) {
                    int i = 0; long value;
                    value = player.Points;
                    for (i = 0; i < top.Count; ++i) {
                        if (value > top[i].Item2) break;
                    }
                    if (i < 5) {
                        if (top.Count == 5) top.RemoveAt(4);
                        top.Insert(i, new Tuple<string, long>(player.Name, value));
                    }
                }

                // Show the message.
                if (top.Count == 1)
                    this.SayToAllChannels(string.Format("The UNO top scores have been \u0002reset\u0002! Only \u0002{0}\u0002 (with {1}) scored any points this time.", top[0].Item1, top[0].Item2));
                else if (top.Count == 2)
                    this.SayToAllChannels(string.Format("The UNO top scores have been \u0002reset\u0002! The top player was \u0002{0}\u0002 ({1}), followed by \u0002{2}\u0002 ({3}).", top[0].Item1, top[0].Item2, top[1].Item1, top[1].Item2));
                else if (top.Count >= 3)
                    this.SayToAllChannels(string.Format("The UNO top scores have been \u0002reset\u0002! The top player was \u0002{0}\u0002 ({1}), followed by \u0002{2}\u0002 ({3}), then \u0002{4}\u0002 ({5}).", top[0].Item1, top[0].Item2, top[1].Item1, top[1].Item2, top[2].Item1, top[2].Item2));

                // Process players' statictics.
                foreach (PlayerStats player in this.ScoreboardCurrent.Values) {
                    PlayerStats playerAllTime;
                    if (!this.ScoreboardAllTime.TryGetValue(player.Name, out playerAllTime)) {
                        ConsoleUtils.WriteLine("%cRED[{0}] Error: {1} was not found in the all-time scoreboard!", this.Key, player.Name);
                        playerAllTime = new PlayerStats() {
                            Name = player.Name, Points = player.Points, Plays = player.Plays, Wins = player.Wins, Losses = player.Losses, ChallengePoints = player.ChallengePoints, RecordPoints = player.RecordPoints, RecordTime = player.RecordTime, StartedPlaying = player.StartedPlaying,
                            BestPeriodScore = player.Points, BestPeriodScoreTime = DateTime.Now, BestPeriodChallengeScore = player.BestPeriodChallengeScore, BestPeriodChallengeScoreTime = DateTime.Now
                        };
                        this.ScoreboardAllTime.Add(player.Name, playerAllTime);
                    }
                    if (top.Count > 0 && player.Points == top[0].Item2)
                        ++playerAllTime.Placed[0];
                    else if (top.Count > 1 && player.Points == top[1].Item2)
                        ++playerAllTime.Placed[1];
                    else if (top.Count > 2 && player.Points == top[2].Item2)
                        ++playerAllTime.Placed[2];
                    else if (top.Count > 3 && player.Points == top[3].Item2)
                        ++playerAllTime.Placed[3];
                    else if (top.Count > 4 && player.Points == top[4].Item2)
                        ++playerAllTime.Placed[4];

                    if (player.Points > playerAllTime.BestPeriodScore) {
                        playerAllTime.BestPeriodScore = player.Points;
                        playerAllTime.BestPeriodScoreTime = DateTime.Now;
                    }
                    if (player.ChallengePoints > playerAllTime.BestPeriodChallengeScore) {
                        playerAllTime.BestPeriodChallengeScore = player.ChallengePoints;
                        playerAllTime.BestPeriodChallengeScoreTime = DateTime.Now;
                    }
                }

                this.ScoreboardLast = this.ScoreboardCurrent;
                this.ScoreboardCurrent = new Dictionary<string, PlayerStats>(StringComparer.InvariantCultureIgnoreCase);

                this.StatsPeriodEnd = default(DateTime);
                this.StatsResetTimer.Interval = 3600e+3;

                this.OnSave();
            }
        }

        private bool generating;
        private object Lock = new object();
        public void GenerateJSONScoreboard() {
            lock (this.Lock) {
                if (this.generating) return;
                this.generating = true;
            }
            try {
                using (StreamWriter writer = new StreamWriter(File.Open(this.Key + "-stats.json", FileMode.Create))) {
                    writer.Write("{\"version\":4,\"current\":[");
                    UNOPlugin.WriteJSONList(writer, this.ScoreboardCurrent.Values, this.JSONLeaderboard);
                    writer.Write("],\"last\":[");
                    UNOPlugin.WriteJSONList(writer, this.ScoreboardLast.Values, this.JSONLeaderboard);
                    writer.Write("],\"alltime\":[");
                    UNOPlugin.WriteJSONList(writer, this.ScoreboardAllTime.Values, this.JSONLeaderboard);
                    writer.Write("],\"periodend\":");
                    UNOPlugin.WriteJSONString(writer, this.StatsPeriodEnd.ToUniversalTime().ToString("yyyy-mm-ddTHH:mm:ssZ"));
                    writer.Write("}");
                    writer.Close();
                }
            } finally {
                this.generating = false;
            }
        }
        public static void WriteJSONList(StreamWriter writer, IEnumerable<PlayerStats> list, LeaderboardMode sortKey) {
            bool firstEntry = true;
            List<PlayerStats> top = UNOPlugin.SortLeaderboard(list, sortKey);
            foreach (PlayerStats entry in top) {
                if (firstEntry) 
                    firstEntry = false;
                else
                    writer.Write(",");
                writer.Write("{\"name\":");
                UNOPlugin.WriteJSONString(writer, entry.Name);
                writer.Write(",\"points\":");
                writer.Write(entry.Points);
                writer.Write(",\"plays\":");
                writer.Write(entry.Plays);
                writer.Write(",\"wins\":");
                writer.Write(entry.Wins);
                writer.Write(",\"losses\":");
                writer.Write(entry.Losses);
                writer.Write(",\"challenge\":");
                writer.Write(entry.ChallengePoints);
                writer.Write("}");
            }
        }
        public static void WriteJSONString(StreamWriter writer, string text) {
            if (text == null) {
                writer.Write("null");
                return;
            }
            writer.Write("\"");
            for (int i = 0; i < text.Length; ++i) {
                char c = text[i];
                if (c == '"')
                    writer.Write("\\\"");
                else if (c == '\\')
                    writer.Write("\\\\");
                else
                    writer.Write(c);
            }
            writer.Write("\"");
        }

#endregion


    }

    [Serializable]
    public class UnknownFileVersionException : Exception {
        public UnknownFileVersionException() : base("The stats file is from a newer or unknown version of the plugin.") { }
        public UnknownFileVersionException(string message) : base(message) { }
        public UnknownFileVersionException(string message, Exception inner) : base(message, inner) { }
        protected UnknownFileVersionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
