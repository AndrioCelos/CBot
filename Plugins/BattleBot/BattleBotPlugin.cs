using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using CBot;
using AnIRC;

using Timer = System.Timers.Timer;

namespace BattleBot {
    [ApiVersion(3, 7)]
    public class BattleBotPlugin : Plugin {
        private List<ArenaTrigger> arenaTriggers = new List<ArenaTrigger>();

        public string LoggedIn;
        public ArenaVersion Version;
        public bool IsBattleDungeon;
        public bool VersionPreCTCP;

        public int Level;
        public int TurnNumber;

        public Character ViewingCharacter;
        public Combatant ViewingCombatant;
        public Character ViewingStatsCharacter;
        public Combatant ViewingStatsCombatant;
        public Weapon ViewingWeapon;
        public Technique ViewingTechnique;
        public string ViewingItem;
        public string ViewingSkill;
        public short RepeatCommand;
        public Timer AnalysisTimer;

        public string TempSkills;
        public bool WaitingForOwnTechniques;
        public bool WaitingForOwnSkills;
        public bool WaitingForBattleList;

        public IrcClient ArenaConnection;
        public string ArenaChannel;
        public string ArenaNickname;
        public string ArenaDirectory;
        public bool NoMonsterFix;
		public bool LongAiBattleFix;

        public Dictionary<string, OwnCharacter> OwnCharacters;
        public Dictionary<string, Character> Characters;
        public Dictionary<string, Technique> Techniques;
        public Dictionary<string, Weapon> Weapons;
        public Dictionary<string, ActivityReport> ActivityReports;

        private Dictionary<string, TaskState> infoTasks = new Dictionary<string, TaskState>(StringComparer.InvariantCultureIgnoreCase);
		// We need to remember what list is currently being processed, as many of them can span multiple lines.
		private InfoListType infoType;
		private Character infoCharacter;
		private Timer infoTimer;
		private List<Tuple<string, int>> infoList;
		private int infoFragmentLength;

        public bool Entering;

        public bool BattleOpen;
        public bool BattleStarted;

        public bool IsAdmin;
        public DateTime IsAdminChecked;
        public bool IsAdminChecking;
        public Timer IsAdminCheckTimer;

        public Dictionary<string, Combatant> BattleList;
        private List<string> PlayersEntering = new List<string>();
        public List<UnmatchedName> UnmatchedFullNames;
        public List<UnmatchedName> UnmatchedShortNames;

        public string BattleListAliveColour;
        public bool DCCBattleChat;
        public IrcClient DccClient;

        public int number_of_monsters_needed;
        public string Weather;
        public BattleType BattleType;
        public BattleCondition BattleConditions;

        public DateTime BattleStartTime;
        public DateTime DarknessWarning;
        public bool Darkness;
        public short DarknessTurns;
        public string HolyAuraUser;
        public DateTime HolyAuraEnd;
        public short HolyAuraTurns;

        public string Turn;
        public string TurnAction;
        public string TurnAbility;
        public string TurnTarget;
        public bool TurnAoE;
        public string TurnCounterer;

        public int BetAmount;
        public int BetTotal;
        public bool BetOnAlly;

        public bool EnableParticipation;
        public int MinPlayers;
        public bool EnableAnalysis;
        public bool EnableUpgrades;
        public bool EnablePurchases;
        public bool EnableGambling;
        public AI AI;
        public short AIType;
        protected Thread GetAbilitiesThread;

        public List<string> Controlling;

        protected internal Random RNG;
        public short debugLevel = 3;

        public event EventHandler<BattleOpenEventArgs> eBattleOpen;
        public event EventHandler eBattleStart;
        public event EventHandler eBattleEnd;

        public BattleBotPlugin() {
            // Register triggers.
            foreach (var method in this.GetType().GetMethods(BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
                foreach (var attribute in method.GetCustomAttributes()) {
                    if (attribute is ArenaRegexAttribute) {
                        arenaTriggers.Add(new ArenaTrigger((ArenaRegexAttribute) attribute, (PluginTriggerHandler) method.CreateDelegate(typeof(PluginTriggerHandler), this)));
                    }
                }
            }
        }

        public override string Name => "Battlebot";

        public override string[] Channels {
            get { return base.Channels; }
            set {
                base.Channels = value;
                this.CheckChannels();
            }
        }

        private void CheckChannels() {
			// We take the first channel in the list as the Arena channel, excluding wildcard channels.
            this.ArenaConnection = null;
            this.ArenaChannel = null;
            foreach (string channel in this.Channels) {
                string[] fields = channel.Split(new char[] { '/' }, 2);
                if (fields.Length == 1)
                    fields = new string[] { null, fields[0] };
                if (fields[1] == "*") continue;
                foreach (ClientEntry clientEntry in Bot.Clients) {
                    IrcClient client = clientEntry.Client;
                    if (client.Address == "!Console") continue;
                    if (client is DccClient) continue;
                    if (fields[0] == null || fields[0] == "*" ||
                        fields[0].Equals(clientEntry.Name, StringComparison.InvariantCultureIgnoreCase) ||
                        fields[0].Equals(clientEntry.Address, StringComparison.InvariantCultureIgnoreCase)) {
                        if (client.Channels.Contains(fields[1])) {
                            this.ArenaConnection = client;
                            this.ArenaChannel = fields[1];
                            return;
                        }
                    }
                }
            }
        }

        public override void Initialize() {
            this.ArenaNickname = "BattleArena";
            this.OwnCharacters = new Dictionary<string, OwnCharacter>(StringComparer.OrdinalIgnoreCase);
            this.Characters = new Dictionary<string, Character>(StringComparer.OrdinalIgnoreCase);
            this.Techniques = new Dictionary<string, Technique>(StringComparer.OrdinalIgnoreCase);
            this.Weapons = new Dictionary<string, Weapon>(StringComparer.OrdinalIgnoreCase);
            this.BattleList = new Dictionary<string, Combatant>(StringComparer.OrdinalIgnoreCase);
            this.ActivityReports = new Dictionary<string, ActivityReport>(StringComparer.OrdinalIgnoreCase);
            this.UnmatchedFullNames = new List<UnmatchedName>();
            this.UnmatchedShortNames = new List<UnmatchedName>();
            this.IsAdminCheckTimer = new Timer();
            this.AnalysisTimer = new Timer(10000) { AutoReset = false };
            this.AnalysisTimer.Elapsed += (sender, e) => { this.ViewInfoCharacterCheck(true); };
            this.DarknessTurns = -1;
            this.HolyAuraTurns = -1;
            this.MinPlayers = 1;
            this.EnableAnalysis = true;
            this.AI = new AI2(this);
            this.AIType = 2;

            this.Controlling = new List<string>();
            this.RNG = new Random();

            this.LoadConfig(Key);
            this.LoadData("BattleArena-" + Key + ".ini");
        }

        public override void OnSave() {
            this.SaveConfig();
            this.SaveData();
        }

        public override bool OnChannelJoin(object sender, ChannelJoinEventArgs e) {
			var client = (IrcClient) sender;

			if (e.Sender.IsMe) {
                if (this.ArenaConnection == null) this.CheckChannels();
				if (this.ArenaConnection == null) return base.OnChannelJoin(sender, e);

				if (this.ArenaConnection.NetworkName == client.NetworkName && client.CaseMappingComparer.Equals(this.ArenaChannel, e.Channel.Name)) {
                    if (this.OwnCharacters.ContainsKey(client.Me.Nickname)) {
                        // Identify to the Arena bot.
                        this.identifyOnJoin(e);
                    }
                    // Get the Arena bot version.
                    if (this.Version == default(ArenaVersion) && !this.VersionPreCTCP)
                        new IrcMessageTarget(client, this.ArenaNickname).Ctcp("BOTVERSION");
                }
            }
            return base.OnChannelJoin(sender, e);
        }

        private async Task identifyOnJoin(ChannelJoinEventArgs e) {
			try {
				await e.NamesTask;
				if (!e.Channel.Users[this.ArenaConnection.Me.Nickname].Status.Contains('v'))
					Bot.Say(ArenaConnection, ArenaNickname, "!id " + this.OwnCharacters[ArenaConnection.Me.Nickname].Password, SayOptions.NoticeNever);
			} catch (Exception ex) {
				this.WriteLine(1, 4, "Identifying on join failed: " + ex.Message);
			}
        }

        public override bool OnChannelMessage(object sender, ChannelMessageEventArgs e) {
            if (!IsActiveChannel(e.Channel)) return false;

            if (this.WaitingForBattleList && Regex.IsMatch(e.Message, @"(?x) ^\x03\d{1,2} [A-}](?>[0-9A-}]*) (,Â?\s \x03\d{1,2} [A-}](?>[0-9A-}]*) )* (?:Â?\s){0,2}")) {
                this.WaitingForBattleList = false;
                this.OnBattleListLegacy(e.Message);
            } else {
                if (sender is DccClient || (sender == this.ArenaConnection && ((IrcClient) sender).CaseMappingComparer.Equals(e.Channel.Name, this.ArenaChannel) &&
                                            ((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname)))
                    this.RunArenaTriggers((IrcClient) sender, e.Channel, e.Sender, e.Message);
                else if (e.Message.StartsWith("!enter", StringComparison.InvariantCultureIgnoreCase) && !this.PlayersEntering.Contains(e.Sender.Nickname))
                    this.PlayersEntering.Add(e.Sender.Nickname);
            }
            return base.OnChannelMessage(sender, e);
        }

        public override bool OnPrivateMessage(object sender, PrivateMessageEventArgs e) {
            if (sender is DccClient || (sender == this.ArenaConnection && ((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname)))
                this.RunArenaTriggers((IrcClient) sender, e.Sender, e.Sender, e.Message);
            return base.OnPrivateMessage(sender, e);
        }

        public override bool OnPrivateNotice(object sender, PrivateMessageEventArgs e) {
            if (sender == this.ArenaConnection && ((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname)) {
                if (e.Message.StartsWith("\u0001BOTVERSION ", StringComparison.InvariantCultureIgnoreCase)) {
                    if (e.Message.StartsWith("\u0001BOTVERSION Battle Dungeon ", StringComparison.InvariantCultureIgnoreCase)) {
                        this.IsBattleDungeon = true;
                        this.Version = new ArenaVersion(e.Message.Substring(27).TrimEnd(new char[] { (char) 1 }));
                    } else {
                        this.IsBattleDungeon = false;
                        this.Version = new ArenaVersion(e.Message.Substring(12).TrimEnd(new char[] { (char) 1 }));
                    }
                    this.VersionPreCTCP = false;
                    this.WriteLine(1, 12, string.Format("The Arena is running {0} version {1}.", this.IsBattleDungeon ? "Battle Dungeon" : "Battle Arena", this.Version));
                }
            }
            return base.OnPrivateNotice(sender, e);
        }

        public override bool OnPrivateCTCP(object sender, PrivateMessageEventArgs e) {
            if (this.LoggedIn != null) return false;
            if (!((IrcClient) sender).CaseMappingComparer.Equals(e.Sender.Nickname, this.ArenaNickname)) return false;

            Match match = Regex.Match(e.Message, @"DCC CHAT chat (\d+) (\d+)", RegexOptions.IgnoreCase);
            if (match.Success) {
                long IPNumeric = long.Parse(match.Groups[1].Value);
                System.Net.IPAddress IP = new System.Net.IPAddress(new byte[] { (byte) ((IPNumeric >> 24) & 255),
                                                                                (byte) ((IPNumeric >> 16) & 255),
                                                                                (byte) ((IPNumeric >>  8) & 255),
                                                                                (byte) ((IPNumeric      ) & 255) });
                int port = int.Parse(match.Groups[2].Value);

                this.WriteLine(1, 4, "Received a DCC CHAT request from {0}.  IP: {1}  Port: {2}", e.Sender.Nickname, IP, port);
                // Create the DCC connection.
                this.DccClient = new DccClient(this);
                ClientEntry newEntry = new ClientEntry("!" + this.Key, IP.ToString(), port, this.DccClient) { SaveToConfig = false };
                Bot.Clients.Add(newEntry);
                Bot.SetUpClientEvents(this.DccClient);
                try {
                    ((DccClient) this.DccClient).Target = e.Sender;
                    ((DccClient) this.DccClient).Connect(IP, port);
                    this.WriteLine(1, 4, "Connected to the DCC session successfully.");
                    if (!this.DCCBattleChat) ((DccClient) this.DccClient).SendSub("!toggle battle chat");
                } catch (Exception ex) {
                    this.SayToAllChannels(string.Format("My DCC connection failed: {0}", ex.Message));
                    Bot.Clients.Remove(newEntry);
                }
                return true;
            }
            return base.OnPrivateCTCP(sender, e);
        }

        public bool RunArenaTriggers(IrcClient connection, IrcMessageTarget channel, IrcUser sender, string message) {
            string stripped = null;

            foreach (var trigger in arenaTriggers) {
                string input;

                if (trigger.Attribute.StripFormats) {
                    if (stripped == null) stripped = IrcClient.RemoveCodes(message);
                    input = stripped;
                } else
                    input = message;

                foreach (var regex in trigger.Attribute.Patterns) {
                    Match match = regex.Match(input);
                    if (match.Success) {
                        try {
                            trigger.Handler.Invoke(this, new TriggerEventArgs(connection, channel, sender, match));
                        } catch (Exception ex) {
                            this.LogError(trigger.Handler.GetMethodInfo().Name, ex);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public void BattleAction(bool PM, string message) {
            if (this.DccClient != null && this.DccClient.State == IrcClientState.Online)
                ((DccClient) this.DccClient).SendSub(message);
            else if (PM)
                Bot.Say(this.ArenaConnection, this.ArenaNickname, message, SayOptions.NoticeNever);
            else
                Bot.Say(this.ArenaConnection, this.ArenaChannel, message, SayOptions.NoticeNever);
        }

        private async Task delay() {
            if (this.DccClient == null || this.DccClient.State < IrcClientState.Online) await Task.Delay(1000);
        }

#region Filing
        public void LoadConfig(string key) {
            string filename = Path.Combine("Config", key + ".ini");
            if (!File.Exists(filename)) return;
            StreamReader reader = new StreamReader(filename);
            string section = null;

            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

                Match match = Regex.Match(line, @"^\s*\[(.*?)\]?\s*$");
                if (match.Success) {
                    section = match.Groups[1].Value;
                } else {
                    match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
                    if (match.Success) {
                        string field = match.Groups[1].Value;
                        string value = match.Groups[2].Value;

                        if (section == null) continue;
                        switch (section.ToUpper()) {
                            case "ENABLE":
                                switch (field.ToUpper()) {
                                    case "PARTICIPATION":
                                        this.EnableParticipation = Bot.ParseBoolean(value);
                                        break;
                                    case "ANALYSIS":
                                        this.EnableAnalysis = Bot.ParseBoolean(value);
                                        break;
                                    case "UPGRADES":
                                        this.EnableUpgrades = Bot.ParseBoolean(value);
                                        break;
                                    case "PURCHASES":
                                        this.EnablePurchases = Bot.ParseBoolean(value);
                                        break;
                                    case "GAMBLING":
                                        this.EnableGambling = Bot.ParseBoolean(value);
                                        break;
                                    case "AI":
                                        this.AIType = short.Parse(value);
                                        break;
                                    case "MINPLAYERS":
                                        this.MinPlayers = int.Parse(value);
                                        break;
                                }
                                break;
                            case "ARENA":
                                switch (field.ToUpper()) {
                                    case "BOTNICKNAME":
                                        this.ArenaNickname = value;
                                        break;
                                    case "NOMONSTERFIX":
                                        this.NoMonsterFix = Bot.ParseBoolean(value);
                                        break;
                                    case "DATAFOLDER":
                                        this.ArenaDirectory = value;
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
            reader.Close();
        }

        /// <summary>
        /// Saves plugin settings to the plugin's configuration file
        /// </summary>
        public void SaveConfig() {
            if (!Directory.Exists("Config"))
                Directory.CreateDirectory("Config");
            StreamWriter writer = new StreamWriter(Path.Combine("Config", this.Key + ".ini"), false);
            writer.WriteLine("[Enable]");
            writer.WriteLine("Analysis={0}", this.EnableAnalysis ? "True" : "False");
            writer.WriteLine("Participation={0}", this.EnableParticipation ? "True" : "False");
            writer.WriteLine("Upgrades={0}", this.EnableUpgrades ? "True" : "False");
            writer.WriteLine("Purchases={0}", this.EnablePurchases ? "True" : "False");
            writer.WriteLine("Gambling={0}", this.EnableGambling ? "True" : "False");
            writer.WriteLine("AI={0}", this.AIType);
            writer.WriteLine("MinPlayers={0}", this.MinPlayers);
            writer.WriteLine("");
            writer.WriteLine("[Arena]");
            writer.WriteLine("BotNickname={0}", this.ArenaNickname);
			writer.WriteLine("NoMonsterFix={0}", this.NoMonsterFix ? "On" : "Off");
			writer.WriteLine("LongAiBattleFix={0}", this.LongAiBattleFix ? "On" : "Off");
            if (this.ArenaDirectory != null)
                writer.WriteLine("DataFolder={0}", this.ArenaDirectory);
            writer.Close();
        }

        private void LoadData() {
            this.LoadData("BattleArena-" + this.Key + ".ini");
        }
        private void LoadData(string filename) {
            if (!File.Exists(filename)) return;
            StreamReader reader = new StreamReader(filename);
            string[] section = new string[0];
            OwnCharacter ownCharacter = null; Character character = null; Weapon weapon = null; Technique technique = null;

            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                if (Regex.IsMatch(line, @"^(?>\s*);")) continue;  // Comment check

                Match match = Regex.Match(line, @"^\s*\[(.*?)\]?\s*$");
                if (match.Success) {
                    section = match.Groups[1].Value.Split(new char[] { ':' }, 2);
                    if (section.Length != 2) continue;
                    switch (section[0].ToUpper()) {
                        case "ME":
                            ownCharacter = new OwnCharacter();
                            this.OwnCharacters.Add(section[1], ownCharacter);
                            break;
                        case "CHARACTER":
                            character = new Character() { ShortName = section[1] };
                            this.Characters.Add(section[1], character);
                            break;
                        case "WEAPON":
                            weapon = new Weapon() { Name = section[1] };
                            this.Weapons.Add(section[1], weapon);
                            break;
                        case "TECHNIQUE":
                            technique = new Technique() { Name = section[1] };
                            this.Techniques.Add(section[1], technique);
                            break;
                    }
                } else {
                    if (section.Length != 2) continue;
                    match = Regex.Match(line, @"^\s*((?>[^=]*))=(.*)$");
                    if (match.Success) {
                        string field = match.Groups[1].Value;
                        string value = match.Groups[2].Value;

                        switch (section[0].ToUpper()) {
                            case "ME":
                                switch (field.ToUpper()) {
                                    case "NAME":
                                        ownCharacter.FullName = value;
                                        break;
                                    case "PASSWORD":
                                        ownCharacter.Password = value;
                                        break;
                                }
                                break;
                            case "CHARACTER":
                                switch (field.ToUpper()) {
                                    case "NAME":
                                        character.Name = value;
                                        break;
                                    case "GENDER":
                                        switch (value.ToUpper()) {
                                            case "MALE":
                                                character.Gender = Gender.Male;
                                                break;
                                            case "FEMALE":
                                                character.Gender = Gender.Female;
                                                break;
                                            case "NONE":
                                                character.Gender = Gender.None;
                                                break;
                                            case "UNKNOWN":
                                                character.Gender = Gender.Unknown;
                                                break;
                                        }
                                        break;
                                    case "CATEGORY":
                                        switch (value.ToUpper()) {
                                            case "PLAYER":
                                            case "1":
                                                character.Category = Category.Player;
                                                break;
                                            case "ALLY":
                                            case "2":
                                                character.Category = Category.Ally;
                                                break;
                                            case "MONSTER":
                                            case "4":
                                                character.Category = Category.Monster;
                                                break;
                                            case "3":
                                            case "5":
                                            case "6":
                                            case "7":
                                                character.Category = (Category) short.Parse(value);
                                                break;
                                        }
                                        break;
                                    case "DESCRIPTION":
                                        character.Description = value;
                                        break;
                                    case "STR":
                                        character.BaseSTR = int.Parse(value);
                                        break;
                                    case "DEF":
                                        character.BaseDEF = int.Parse(value);
                                        break;
                                    case "INT":
                                        character.BaseINT = int.Parse(value);
                                        break;
                                    case "SPD":
                                        character.BaseSPD = int.Parse(value);
                                        break;
                                    case "IG":
                                        character.IgnitionCapacity = int.Parse(value);
                                        break;
                                    case "WEAPON":
                                        character.EquippedWeapon = value;
                                        break;
                                    case "WEAPON2":
                                        character.EquippedWeapon2 = value;
                                        break;
                                    case "ACCESSORY":
                                        character.EquippedAccessory = value;
                                        break;
                                    case "ELEMENTALWEAKNESSES":
                                        character.ElementalWeaknesses = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "WEAPONWEAKNESSES":
                                        character.WeaponWeaknesses = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "ELEMENTALRESISTANCES":
                                        character.ElementalResistances = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "WEAPONRESISTANCES":
                                        character.WeaponResistances = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "ABSORBS":
                                        character.ElementalAbsorbs = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "IMMUNITIES":
                                        character.ElementalImmunities = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "HURTBYTAUNT":
                                        character.HurtByTaunt = Bot.ParseBoolean(value);
                                        break;
                                    case "UNDEAD":
                                        character.IsUndead = Bot.ParseBoolean(value);
                                        break;
                                    case "ELEMENTAL":
                                        character.IsElemental = Bot.ParseBoolean(value);
                                        break;
                                    case "ETHEREAL":
                                        character.IsEthereal = Bot.ParseBoolean(value);
                                        break;
                                    case "WEAPONS":
                                        character.Weapons = new Dictionary<string, int>();
                                        foreach (string _weapon in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _weapon.Split(new char[] { '|' });
                                            character.Weapons.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "TECHNIQUES":
                                        character.Techniques = new Dictionary<string, int>();
                                        foreach (string _technique in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _technique.Split(new char[] { '|' });
                                            character.Techniques.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "SKILLS":
                                        character.Skills = new Dictionary<string, int>();
                                        foreach (string _skill in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _skill.Split(new char[] { '|' });
                                            character.Skills.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "STYLES":
                                        character.Styles = new Dictionary<string, int>();
                                        foreach (string _style in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _style.Split(new char[] { '|' });
                                            character.Styles.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "STYLEEXP":
                                        character.StyleExperience = new Dictionary<string, int>();
                                        foreach (string _style in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _style.Split(new char[] { '|' });
                                            character.StyleExperience.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "IGNITIONS":
                                        character.Ignitions = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "ITEMS":
                                        character.Items = new Dictionary<string, int>();
                                        foreach (string _item in value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
                                            string[] fields2 = _item.Split(new char[] { '|' });
                                            character.Items.Add(fields2[0], int.Parse(fields2[1]));
                                        }
                                        break;
                                    case "REDORBS":
                                        character.RedOrbs = int.Parse(value);
                                        break;
                                    case "BLACKORBS":
                                        character.BlackOrbs = int.Parse(value);
                                        break;
                                    case "ALLEDNOTES":
                                        character.AlliedNotes = int.Parse(value);
                                        break;
                                    case "DOUBLEDOLLARS":
                                        character.DoubleDollars = int.Parse(value);
                                        break;
                                    case "RATING":
                                        character.Rating = int.Parse(value);
                                        break;
                                    case "NPCBATTLES":
                                        character.NPCBattles = int.Parse(value);
                                        break;
                                    case "ISWELLKNOWN":
                                        character.IsWellKnown = Bot.ParseBoolean(value);
                                        break;
                                }
                                break;
                            case "WEAPON":
                                switch (field.ToUpper()) {
                                    case "TYPE":
                                        weapon.Type = value;
                                        break;
                                    case "COST":
                                        weapon.Cost = int.Parse(value);
                                        break;
                                    case "UPGRADECOST":
                                        weapon.UpgradeCost = int.Parse(value);
                                        break;
                                    case "POWER":
                                        weapon.Power = int.Parse(value);
                                        break;
                                    case "HITSMIN":
                                        weapon.HitsMin = short.Parse(value);
                                        break;
                                    case "HITSMAX":
                                        weapon.HitsMax = short.Parse(value);
                                        break;
                                    case "ELEMENT":
                                        weapon.Element = value;
                                        break;
                                    case "TECHNIQUES":
                                        weapon.Techniques = new List<string>(value.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries));
                                        break;
                                    case "ISWELLKNOWN":
                                        weapon.IsWellKnown = Bot.ParseBoolean(value);
                                        break;
                                }
                                break;
                            case "TECHNIQUE":
                                switch (field.ToUpper()) {
                                    case "TYPE":
                                        switch (value.ToUpper()) {
                                            case "ATTACK":
                                                technique.Type = TechniqueType.Attack;
                                                break;
                                            case "AOEATTACK":
                                                technique.Type = TechniqueType.AoEAttack;
                                                break;
                                            case "HEAL":
                                                technique.Type = TechniqueType.Heal;
                                                break;
                                            case "AOEHEAL":
                                                technique.Type = TechniqueType.AoEHeal;
                                                break;
                                            case "SUICIDE":
                                                technique.Type = TechniqueType.Suicide;
                                                break;
                                            case "AOESUICIDE":
                                                technique.Type = TechniqueType.AoESuicide;
                                                break;
                                            case "STEALPOWER":
                                                technique.Type = TechniqueType.StealPower;
                                                break;
                                            case "BOOST":
                                                technique.Type = TechniqueType.Boost;
                                                break;
                                            case "FINALGETSUGA":
                                                technique.Type = TechniqueType.FinalGetsuga;
                                                break;
                                            case "BUFF":
                                                technique.Type = TechniqueType.Buff;
                                                break;
                                            case "CLEARSTATUSNEGATIVE":
                                                technique.Type = TechniqueType.ClearStatusNegative;
                                                break;
                                            case "CLEARSTATUSPOSITIVE":
                                                technique.Type = TechniqueType.ClearStatusPositive;
                                                break;
                                            default:
                                                technique.Type = TechniqueType.Unknown;
                                                break;
                                        }
                                        break;
                                    case "DESCRIPTION":
                                        technique.Description = value;
                                        break;
                                    case "POWER":
                                        technique.Power = int.Parse(value);
                                        break;
                                    case "STATUS":
                                        technique.Status = value;
                                        break;
                                    case "TP":
                                        technique.TP = int.Parse(value);
                                        break;
                                    case "COST":
                                        technique.Cost = int.Parse(value);
                                        break;
                                    case "AOE":
                                        technique.IsAoE = Bot.ParseBoolean(value);
                                        break;
                                    case "MAGIC":
                                        technique.IsMagic = Bot.ParseBoolean(value);
                                        break;
                                    case "ELEMENT":
                                        technique.Element = value;
                                        break;
                                    case "ISWELLKNOWN":
                                        technique.IsWellKnown = Bot.ParseBoolean(value);
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
            reader.Close();

            foreach (Character _character in this.Characters.Values) {
                Weapon _weapon;
                if (_character.EquippedWeapon != null && _character.Techniques != null) {
                    if (this.Weapons.TryGetValue(_character.EquippedWeapon, out _weapon)) {
                        if (_character.EquippedTechniques == null)
                            _character.EquippedTechniques = new List<string>();
                        foreach (string _technique in _weapon.Techniques) {
                            if (_character.Techniques.ContainsKey(_technique))
                                _character.EquippedTechniques.Add(_technique);
                        }
                    }
                }
                if (_character.EquippedWeapon2 != null && _character.Techniques != null) {
                    if (this.Weapons.TryGetValue(_character.EquippedWeapon2, out _weapon)) {
                        if (_character.EquippedTechniques == null)
                            _character.EquippedTechniques = new List<string>();
                        foreach (string _technique in _weapon.Techniques) {
                            if (_character.Techniques.ContainsKey(_technique))
                                _character.EquippedTechniques.Add(_technique);
                        }
                    }
                }
            }

            // Read activity reports.
            if (Directory.Exists(this.Key + "-Activity")) {
                foreach (var file in Directory.GetFiles(this.Key + "-Activity")) {
                    var data = new int[168];
                    int[] lastData;
                    using (var reader2 = new BinaryReader(File.Open(file, FileMode.Open, FileAccess.Read))) {
                        if (reader2.ReadInt16() != 1) continue;

                        for (int i = 0; i < 168; ++i)
                            data[i] = reader2.ReadInt32();

                        if (reader2.ReadBoolean()) {
                            lastData = new int[168];
                            for (int i = 0; i < 168; ++i)
                                lastData[i] = reader2.ReadInt32();
                        } else
                            lastData = null;

                        this.ActivityReports[Path.GetFileNameWithoutExtension(file)] = new ActivityReport(data, lastData, DateTime.FromBinary(reader2.ReadInt64()));

                        reader2.Close();
                    }
                }
            }
        }

        public void SaveData() {
            this.SaveData("BattleArena-" + this.Key + ".ini");
        }

        public void SaveData(string filename) {
            using (var writer = new StreamWriter(filename)) {
                foreach (KeyValuePair<string, OwnCharacter> character in this.OwnCharacters) {
                    writer.WriteLine("[Me:{0}]", character.Key);
                    if (character.Value.FullName != null)
                        writer.WriteLine("Name={0}", character.Value.FullName);
                    writer.WriteLine("Password={0}", character.Value.Password);
                    writer.WriteLine();
                }

                foreach (Character character in this.Characters.Values) {
                    writer.WriteLine("[Character:{0}]", character.ShortName);
                    writer.WriteLine("Name={0}", character.Name);
                    switch (character.Category) {
                        case Category.Player:
                            writer.WriteLine("Category=Player"); break;
                        case Category.Ally:
                            writer.WriteLine("Category=Ally"); break;
                        case Category.Monster:
                            writer.WriteLine("Category=Monster"); break;
                        default:
                            writer.WriteLine("Category={0}", ((short) character.Category).ToString()); break;
                    }
                    switch (character.Gender) {
                        case Gender.Male:
                            writer.WriteLine("Gender=Male"); break;
                        case Gender.Female:
                            writer.WriteLine("Gender=Female"); break;
                        case Gender.None:
                            writer.WriteLine("Gender=None"); break;
                    }
                    if (character.Description != null)
                        writer.WriteLine("Description={0}", character.Description);
                    if (character.BaseSTR != 0)
                        writer.WriteLine("STR={0}", character.BaseSTR);
                    if (character.BaseDEF != 0)
                        writer.WriteLine("DEF={0}", character.BaseDEF);
                    if (character.BaseINT != 0)
                        writer.WriteLine("INT={0}", character.BaseINT);
                    if (character.BaseSPD != 0)
                        writer.WriteLine("SPD={0}", character.BaseSPD);
                    if (character.IgnitionCapacity != 0)
                        writer.WriteLine("IG={0}", character.IgnitionCapacity);
                    if (character.EquippedWeapon != null)
                        writer.WriteLine("Weapon={0}", character.EquippedWeapon);
                    if (character.EquippedWeapon2 != null)
                        writer.WriteLine("Weapon2={0}", character.EquippedWeapon2);
                    if (character.EquippedAccessory != null)
                        writer.WriteLine("Accessory={0}", character.EquippedAccessory);
                    if (character.ElementalWeaknesses != null)
                        writer.WriteLine("ElementalWeaknesses={0}", string.Join(".", character.ElementalWeaknesses));
                    if (character.WeaponWeaknesses != null)
                        writer.WriteLine("WeaponWeaknesses={0}", string.Join(".", character.WeaponWeaknesses));
                    if (character.ElementalResistances != null)
                        writer.WriteLine("ElementalResistances={0}", string.Join(".", character.ElementalResistances));
                    if (character.WeaponResistances != null)
                        writer.WriteLine("WeaponResistances={0}", string.Join(".", character.WeaponResistances));
                    if (character.ElementalAbsorbs != null)
                        writer.WriteLine("Absorbs={0}", string.Join(".", character.ElementalAbsorbs));
                    if (character.ElementalImmunities != null)
                        writer.WriteLine("Immunities={0}", string.Join(".", character.ElementalImmunities));
                    if (character.HurtByTaunt)
                        writer.WriteLine("HurtByTaunt=Yes");
                    if (character.IsUndead)
                        writer.WriteLine("Undead=Yes");
                    if (character.IsElemental)
                        writer.WriteLine("Elemental=Yes");
                    if (character.IsEthereal)
                        writer.WriteLine("Ethereal=Yes");
                    if (character.Weapons != null)
                        writer.WriteLine("Weapons={0}", string.Join(".", character.Weapons.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.Techniques != null)
                        writer.WriteLine("Techniques={0}", string.Join(".", character.Techniques.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.Skills != null)
                        writer.WriteLine("Skills={0}", string.Join(".", character.Skills.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.Styles != null)
                        writer.WriteLine("Styles={0}", string.Join(".", character.Styles.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.StyleExperience != null)
                        writer.WriteLine("StyleExp={0}", string.Join(".", character.StyleExperience.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.Ignitions != null)
                        writer.WriteLine("Ignitions={0}", string.Join(".", character.Ignitions));
                    if (character.Items != null)
                        writer.WriteLine("Items={0}", string.Join(".", character.Items.Select(e => e.Key + "|" + e.Value.ToString())));
                    if (character.RedOrbs != 0)
                        writer.WriteLine("RedOrbs={0}", character.RedOrbs);
                    if (character.BlackOrbs != 0)
                        writer.WriteLine("BlackOrbs={0}", character.BlackOrbs);
                    if (character.AlliedNotes != 0)
                        writer.WriteLine("AlliedNotes={0}", character.AlliedNotes);
                    if (character.DoubleDollars != 0)
                        writer.WriteLine("DoubleDollars={0}", character.DoubleDollars);
                    if (character.Rating != 0)
                        writer.WriteLine("Rating={0}", character.Rating);
                    if (character.NPCBattles != 0)
                        writer.WriteLine("NPCBattles={0}", character.NPCBattles);
                    if (character.IsWellKnown)
                        writer.WriteLine("IsWellKnown=Yes");
                    writer.WriteLine();
                }

                foreach (Weapon weapon in this.Weapons.Values) {
                    writer.WriteLine("[Weapon:{0}]", weapon.Name);
                    if (weapon.Type != null)
                        writer.WriteLine("Type={0}", weapon.Type);
                    if (weapon.Cost != 0)
                        writer.WriteLine("Cost={0}", weapon.Cost);
                    if (weapon.UpgradeCost != 0)
                        writer.WriteLine("UpgradeCost={0}", weapon.UpgradeCost);
                    if (weapon.Power != 0)
                        writer.WriteLine("Power={0}", weapon.Power);
                    if (weapon.HitsMin != 0)
                        writer.WriteLine("HitsMin={0}", weapon.HitsMin);
                    if (weapon.HitsMax != 0)
                        writer.WriteLine("HitsMax={0}", weapon.HitsMax);
                    if (weapon.Element != null)
                        writer.WriteLine("Element={0}", weapon.Element);
                    if (weapon.Techniques != null)
                        writer.WriteLine("Techniques={0}", string.Join(".", weapon.Techniques));
                    if (weapon.IsWellKnown)
                        writer.WriteLine("IsWellKnown=Yes");
                    writer.WriteLine();
                }

                foreach (Technique technique in this.Techniques.Values) {
                    writer.WriteLine("[Technique:{0}]", technique.Name);
                    if (technique.Type != TechniqueType.Unknown)
                        writer.WriteLine("Type={0}", technique.Type);
                    if (technique.Description != null)
                        writer.WriteLine("Description={0}", technique.Description);
                    if (technique.Power != 0)
                        writer.WriteLine("Power={0}", technique.Power);
                    if (technique.Status != null)
                        writer.WriteLine("Status={0}", technique.Status);
                    if (technique.TP != 0)
                        writer.WriteLine("TP={0}", technique.TP);
                    if (technique.Cost != 0)
                        writer.WriteLine("Cost={0}", technique.Cost);
                    if (technique.IsAoE)
                        writer.WriteLine("AoE=Yes");
                    if (technique.IsMagic)
                        writer.WriteLine("Magic=Yes");
                    if (technique.Element != null)
                        writer.WriteLine("Element={0}", technique.Element);
                    if (technique.IsWellKnown)
                        writer.WriteLine("IsWellKnown=Yes");
                    writer.WriteLine();
                }

                writer.Close();
            }

            // Save activity reports.
            if (this.ActivityReports.Count != 0) {
                Directory.CreateDirectory(this.Key + "-Activity");
                foreach (var report in this.ActivityReports) {
                    using (var writer = new BinaryWriter(File.Open(Path.Combine(this.Key + "-Activity", report.Key + ".dat"), FileMode.Create, FileAccess.Write))) {
                        writer.Write((short) 1);  // Version field

                        for (int i = 0; i < report.Value.Data.Count; ++i)
                            writer.Write(report.Value.Data[i]);

                        if (report.Value.LastData != null) {
                            writer.Write(true);
                            for (int i = 0; i < report.Value.LastData.Count; ++i)
                                writer.Write(report.Value.LastData[i]);
                        } else
                            writer.Write(false);

                        writer.Write(report.Value.LastCheck.ToBinary());

                        writer.Close();
                    }
                }
            }
        }
#endregion

#region Commands
        [Command("set", 1, 2, "set <property> [value]", "Changes settings for this plugin", Permission = ".set")]
        public void CommandSet(object sender, CommandEventArgs e) {
            string property; string value;
            property = e.Parameters[0];
            if (e.Parameters.Length == 2)
                value = e.Parameters[1];
            else
                value = null;
            switch (property.ToUpper().Replace("_", "")) {
                case "ANALYSIS":
                case "ENABLEANALYSIS":
                    if (value == null) {
                        if (this.EnableAnalysis)
                            e.Reply(Bot.Choose("\u0002Analysis\u0002 is currently \u00039enabled\u000F.", "I \u00039will\u000F analyse the Arena combatants."));
                        else
                            e.Reply(Bot.Choose("\u0002Analysis\u0002 is currently \u00034disabled\u000F.", "I \u00034will not\u000F analyse the Arena combatants."));
                    } else {
                        try {
                            if (this.EnableAnalysis = Bot.ParseBoolean(value))
                                e.Reply(Bot.Choose("\u0002Analysis\u0002 is now \u00039enabled\u000F.", "I \u00039will\u000F now analyse the Arena combatants."));
                            else
                                e.Reply(Bot.Choose("\u0002Analysis\u0002 is now \u00034disabled\u000F.", "I will \u00034no longer\u000F analyse the Arena combatants."));
                        } catch (ArgumentException) {
                            e.Whisper(string.Format("I don't recognise '\u0002{0}\u000F' as a Boolean value. Please enter \u0002on\u0002 or \u0002off\u0002.", value));
                        }
                    }
                    break;
                case "PARTICIPATION":
                case "ENABLEPARTICIPATION":
                    if (value == null) {
                        if (this.EnableParticipation)
                            e.Reply(Bot.Choose("\u0002Participation\u0002 is currently \u00039enabled\u000F.", "I \u00039will\u000F participate in battles."));
                        else
                            e.Reply(Bot.Choose("\u0002Participation\u0002 is currently \u00034disabled\u000F.", "I \u00034will not\u000F participate in battles."));
                    } else {
                        try {
                            if (this.EnableParticipation = Bot.ParseBoolean(value)) {
                                e.Reply(Bot.Choose("\u0002Participation\u0002 is now \u00039enabled\u000F.", "I \u00039will\u000F now participate in battles."));
                                if (this.EnableParticipation && (this.GetAbilitiesThread == null || !this.GetAbilitiesThread.IsAlive)) {
									this.GetAbilitiesAsync();
                                }
                            }  else
                                e.Reply(Bot.Choose("\u0002Participation\u0002 is now \u00034disabled\u000F.", "I will \u00034no longer\u000F participate in battles."));
                        } catch (ArgumentException) {
                            e.Whisper(string.Format("I don't recognise '\u0002{0}\u000F' as a Boolean value. Please enter \u0002on\u0002 or \u0002off\u0002.", value));
                        }
                    }
                    break;
                case "GAMBLING":
                case "ENABLEGAMBLING":
                    if (value == null) {
                        if (this.EnableGambling)
                            e.Reply(Bot.Choose("\u0002Gambling\u0002 is currently \u00039enabled\u000F.", "I \u00039will\u000F bet on NPC battles."));
                        else
                            e.Reply(Bot.Choose("\u0002Gambling\u0002 is currently \u00034disabled\u000F.", "I \u00034will not\u000F bet on NPC battles."));
                    } else {
                        try {
                            if (this.EnableGambling = Bot.ParseBoolean(value))
                                e.Reply(Bot.Choose("\u0002Gambling\u0002 is now \u00039enabled\u000F.", "I \u00039will\u000F now bet on NPC battles."));
                            else
                                e.Reply(Bot.Choose("\u0002Gambling\u0002 is now \u00034disabled\u000F.", "I will \u00034no longer\u000F bet on NPC battles."));
                        } catch (ArgumentException) {
                            e.Whisper(string.Format("I don't recognise '\u0002{0}\u000F' as a Boolean value. Please enter \u0002on\u0002 or \u0002off\u0002.", value));
                        }
                    }
                    break;
                case "ARENANICKNAME":
                case "BOTNICKNAME":
                    if (value == null) {
                        e.Reply(string.Format("The Arena bot's nickname is assumed to be \u0002{0}\u000F.", this.ArenaNickname));
                    } else {
                        e.Reply(string.Format("The Arena bot's nickname is now assumed to be \u0002{0}\u000F.", this.ArenaNickname = value));
                    }
                    break;
                case "NOMONSTERFIX":
                    if (value == null) {
                        if (this.NoMonsterFix)
                            e.Reply(Bot.Choose("The \u0002no-monster fix\u0002 is currently \u00039enabled\u000F.", "I \u00039will\u000F stop empty battles."));
                        else
                            e.Reply(Bot.Choose("The \u0002no-monster fix\u0002 is currently \u00034disabled\u000F.", "I \u00034will not\u000F stop empty battles."));
                    } else {
                        try {
                            if (this.NoMonsterFix = Bot.ParseBoolean(value))
                                e.Reply(Bot.Choose("The \u0002no-monster fix\u0002 is now \u00039enabled\u000F.", "I \u00039will\u000F now stop empty battles."));
                            else
                                e.Reply(Bot.Choose("The \u0002no-monster fix\u0002 is now \u00034disabled\u000F.", "I will \u00034no longer\u000F stop empty battles."));
                        } catch (ArgumentException) {
                            e.Whisper(string.Format("I don't recognise '\u0002{0}\u000F' as a Boolean value. Please enter \u0002on\u0002 or \u0002off\u0002.", value));
                        }
                    }
                    break;
				case "LONGAIBATTLEFIX":
					if (value == null) {
						if (this.NoMonsterFix)
							e.Reply("The \u0002long AI battle fix\u0002 is currently \u00039enabled\u000F.");
						else
							e.Reply("The \u0002long AI battle fix\u0002 is currently \u00034disabled\u000F.");
					} else {
						try {
							if (this.NoMonsterFix = Bot.ParseBoolean(value))
								e.Reply("The \u0002long AI battle fix\u0002 is now \u00039enabled\u000F.");
							else
								e.Reply("The \u0002long AI battle fix\u0002 is now \u00034disabled\u000F.");
						} catch (ArgumentException) {
							e.Whisper(string.Format("I don't recognise '\u0002{0}\u000F' as a Boolean value. Please enter \u0002on\u0002 or \u0002off\u0002.", value));
						}
					}
					break;
				case "ARENADIRECTORY":
                case "ARENADIR":
                case "ARENAFOLDER":
                case "ARENAPATH":
                case "DIRECTORY":
                case "DIR":
                case "FOLDER":
                case "PATH":
                    if (value == null) {
                        if (this.ArenaDirectory == null)
                            e.Reply("I don't have access to the Arena data folder.");
                        else
                            e.Reply(string.Format("The Arena data folder is found at \u0002{0}\u000F.", this.ArenaDirectory));
                    } else {
                        if (value == "" ||
                            value.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                            value.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                            value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
                            value.Equals("cancel", StringComparison.OrdinalIgnoreCase))
                            e.Reply("The Arena data folder was disassociated.");
                        else if (!Directory.Exists(value))
                            e.Whisper("That folder doesn't seem to exist.");
                        else if (!File.Exists(Path.Combine(value, "system.dat")))
                            e.Whisper("That folder doesn't seem to be an Arena data folder (no \u0002system.dat\u0002 was found).");
                        else
                            e.Reply(string.Format("The Arena data folder is now set to \u0002{0}\u000F.", this.ArenaDirectory = value));
                    }
                    break;
                case "MINPLAYERS":
                case "MINIMUMPLAYERS":
                    if (value == null) {
                        if (this.MinPlayers == 1)
                            e.Reply(string.Format("I will enter with at least \u0002{0}\u0002 other player.", this.MinPlayers));
                        else
                            e.Reply(string.Format("I will enter with at least \u0002{0}\u0002 other players.", this.MinPlayers));
                    } else {
                        int value2;
                        if (int.TryParse(value, out value2)) {
                            if (value2 >= 0) {
                                if (value2 == 1)
                                    e.Reply(string.Format("I will now enter with at least \u0002{0}\u0002 other player.", this.MinPlayers = value2));
                                else
                                    e.Reply(string.Format("I will now enter with at least \u0002{0}\u0002 other players.", this.MinPlayers = value2));
                            } else
                                e.Whisper(string.Format("The number can't be negative.", value2));
                        } else
                            e.Whisper(string.Format("'\u0002{0}\u000F' is not a valid integer.", value));
                    }
                    break;
                default:
                    e.Whisper(string.Format("I don't have a setting named \u0002{0}\u000F here.", property));
                    break;
            }
        }

        [Command(new string[] { "time", "timeleft" }, 0, 0, "time", "Tells you how long you have left to defeat the enemy force.",
            Permission = ".time")]
        public void CommandTime(object sender, CommandEventArgs e) {
            if (!BattleStarted)
                e.Reply("There's no battle going on at the moment.");
            else if (Darkness)
                e.Reply("Darkness has already risen.");
            else {
                string timeMessage = null; string timeColour = null; string demonWallMessage = null;
                string holyAuraTimeMessage = null; bool holyAuraOut = false;

                if (DarknessTurns != -1) {
                    if (DarknessTurns == 0) {
                        timeMessage = "{0} turns";
                        demonWallMessage = "200%";
                        timeColour = Colours.DarkRed;
                    } else {
                        if (DarknessTurns == 1)
                            timeMessage = "{0} turn";
                        else
                            timeMessage = "{0} turns";
                        if (DarknessTurns < 3)
                            timeColour = Colours.Red;
                        else if (DarknessTurns < 5)
                            timeColour = Colours.Orange;
                        else if (DarknessTurns < 10)
                            timeColour = Colours.Yellow;
                        else
                            timeColour = Colours.Green;

                        if (this.HolyAuraTurns != -1) {
                            // Show the time remaining for Holy Aura.
                            if (HolyAuraTurns <= 0)
                                holyAuraOut = true;
                            else if (HolyAuraTurns == 1)
                                holyAuraTimeMessage = "{0} turn";
                            else
                                holyAuraTimeMessage = "{0} turns";
                            holyAuraTimeMessage = string.Format(holyAuraTimeMessage, this.HolyAuraTurns);

                        }

                        if (this.BattleList.ContainsKey("Demon_Wall")) {
                            // The Demon Wall's attack power steadily increases to twice its starting power
                            //   over the course of the battle.
                            int maxTime = this.TurnNumber + this.DarknessTurns - 1;
                            float boostFactor = (float) this.TurnNumber / (float) maxTime + 1.0F;
                            demonWallMessage = boostFactor.ToString("P0");
                        }
                        timeMessage = string.Format(timeMessage, this.DarknessTurns);
                    }
                } else {
                    TimeSpan time; string minutes = null; string seconds = null;
                    if (this.DarknessWarning != default(DateTime))
                        time = (this.DarknessWarning.AddMinutes(5)) - DateTime.Now;
                    else if (this.BattleType == BattleBot.BattleType.Boss || this.BattleType == BattleBot.BattleType.President)
                        time = (this.DarknessWarning.AddMinutes(25)) - DateTime.Now;
                    else
                        time = (this.DarknessWarning.AddMinutes(35)) - DateTime.Now;
                    if (this.DarknessWarning == default(DateTime) && time < TimeSpan.FromMinutes(5))
                        time = TimeSpan.FromMinutes(5);

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

                    if (time < TimeSpan.FromSeconds(5))
                        timeColour = Colours.DarkRed;
                    else if (time < TimeSpan.FromMinutes(2))
                        timeColour = Colours.Red;
                    else if (time < TimeSpan.FromMinutes(5))
                        timeColour = Colours.Orange;
                    else if (time < TimeSpan.FromMinutes(10))
                        timeColour = Colours.Yellow;
                    else
                        timeColour = Colours.Green;

                    if (this.HolyAuraEnd != default(DateTime)) {
                        // Show the time remaining for Holy Aura.
                        TimeSpan holyAuraTime = HolyAuraEnd - DateTime.Now;

                        if (holyAuraTime.Minutes != 0) {
                            if (holyAuraTime.Minutes == 1)
                                minutes = "{0} minute";
                            else
                                minutes = "{0} minutes";
                        }
                        if (minutes == null || holyAuraTime.Seconds != 0) {
                            if (holyAuraTime.Seconds == 1)
                                seconds = "{1} second";
                            else
                                seconds = "{1} seconds";
                        }
                        if (minutes == null)
                            holyAuraTimeMessage = seconds;
                        else if (seconds == null)
                            holyAuraTimeMessage = minutes;
                        else
                            holyAuraTimeMessage = string.Format("{0}, {1}", minutes, seconds);
                        holyAuraTimeMessage = string.Format(holyAuraTimeMessage, holyAuraTime.Minutes, holyAuraTime.Seconds);
                        holyAuraOut = (holyAuraTime < TimeSpan.FromSeconds(5));
                    }

                    if (this.BattleList.ContainsKey("Demon_Wall")) {
                        // The Demon Wall's attack power increases
                        //   depending on the time left.
                        if (time >= TimeSpan.FromSeconds(270))
                            demonWallMessage = "100%";
                        else if (time >= TimeSpan.FromSeconds(240))
                            demonWallMessage = "150%";
                        else if (time >= TimeSpan.FromSeconds(210))
                            demonWallMessage = "200%";
                        else if (time >= TimeSpan.FromSeconds(180))
                            demonWallMessage = "250%";
                        else if (time >= TimeSpan.FromSeconds(120))
                            demonWallMessage = "300%";
                        else if (time >= TimeSpan.FromSeconds(60))
                            demonWallMessage = "350%";
                        else if (time >= TimeSpan.FromSeconds(30))
                            demonWallMessage = "400%";
                        else
                            demonWallMessage = "500%";
                    }
                }

                // Show the time.
                if (timeColour == Colours.DarkRed)
                    e.Reply("Darkness should rise any second now.");
                else
                    e.Reply(string.Format(Bot.Choose(
                        "Darkness arises in {0}.",
                        "You have {0} until darkness arises."
                        ), timeColour + timeMessage + Colours.Reset));

                // Show the holy aura time.
                if (holyAuraOut)
                    e.Reply(string.Format("{0}'s holy aura is about to expire.", this.HolyAuraUser));
                else if (HolyAuraTurns != -1 || HolyAuraEnd != default(DateTime))
                    e.Reply(string.Format("{0}'s holy aura will last for \u000312{0}\u000F.", holyAuraTimeMessage));

                // Show the demon wall power.
                if (demonWallMessage != null)
                    e.Reply(string.Format("The \u0002Demon Wall\u0002's power is at \u000304{0}\u000F.", demonWallMessage));
            }
        }

        [Command("control", 1, 1, "control <nickname>", "Instructs me to control another character",
			Permission = ".control", Scope = CommandScope.Channel)]
        public async void CommandControl(object sender, CommandEventArgs e) {
            Character character;
            if (!this.Characters.TryGetValue(e.Parameters[0], out character))
                // We can't recognise the turn of someone we don't know about.
                e.Reply("I'm not familiar enough with this character to control them. Have them enter a battle first.");
            // Check if we're already controlling that person.
            else if (this.Controlling.Contains(character.ShortName))
                e.Reply(string.Format("I'm already controlling {0}.", character.ShortName));
            // Check for a non-player.
            // TODO: Allow it for clones.
            else if (character.Category != Category.Player && !await Bot.CheckPermissionAsync(e.Sender, this.Key + ".control.nonplayer"))
                e.Reply(string.Format("You don't have permission to use this command on a non-player.", character.ShortName));
            // We'll need admin status to control non-players.
            else if (character.Category != Category.Player && !this.IsAdmin)
                e.Reply(string.Format("I can't control non-players here.", character.ShortName));
            else if (character.IsReadyToControl) {
                this.Controlling.Add(character.ShortName);
                e.Reply(string.Format("OK. I will now control \u0002{0}\u0002.", character.Name));
            } else {
                // We'll need to know what this character has.
                e.Reply(string.Format("\u0001ACTION looks carefully at {0}...\u0001", character.Name));
				await this.GetAbilitiesAsync(character.ShortName);
            }
        }

        [Command("controlme", 0, 0, "controlme", "Instructs me to control your character", Permission = ".controlme", Scope = CommandScope.Channel)]
        public void CommandControlMe(object sender, CommandEventArgs e) {
            Character character;
            if (!this.Characters.TryGetValue(e.Sender.Nickname, out character))
                // We can't recognise the turn of someone we don't know about.
                e.Reply("I'm not familiar enough with your character to control you. Enter a battle first.");
            // Check if we're already controlling that person.
            else if (this.Controlling.Contains(character.ShortName))
                e.Reply(string.Format("I'm already controlling you, {0}.", character.ShortName));
            else if (character.IsReadyToControl) {
                this.Controlling.Add(character.ShortName);
                e.Reply(string.Format("OK. I will now control \u0002{0}\u0002.", character.Name));
            } else {
                // We'll need to know what this character has.
                e.Reply(string.Format("\u0001ACTION looks carefully at {0}...\u0001", character.Name));
				this.GetAbilitiesAsync(character.ShortName);
            }
        }

        [Command("stopcontrol", 0, 1, "stopcontrol [nickname]", "Instructs me to stop controlling someone, yourself by default", Scope = CommandScope.Channel)]
        public async void CommandControlStop(object sender, CommandEventArgs e) {
            string target;
            if (e.Parameters.Length == 1) {
                if (!e.Client.CaseMappingComparer.Equals(e.Parameters[0], e.Sender.Nickname) &&
                    !await Bot.CheckPermissionAsync(e.Sender, this.Key + ".control")) {
                    e.Whisper("You don't have permission to use that command on others.");
                    return;
                }
                target = e.Parameters[0];
            } else
                target = e.Sender.Nickname;

            if (Controlling.Remove(target))
                e.Reply(string.Format("OK, I will stop controlling \u0002{0}\u0002.", target));
            else
                e.Reply(string.Format("I'm not controlling {0}.", target));
        }

        [Command("arena-id", 0, 1, "arena-id [new]", "Instructs me to identify myself to the Arena. Specify 'new' if I should set up a new character.",
			Permission = ".identify")]
        public void CommandIdentify(object sender, CommandEventArgs e) {
            if (this.OwnCharacters.ContainsKey(this.ArenaConnection.Me.Nickname))
                this.BattleAction(true, "!id " + this.OwnCharacters[this.ArenaConnection.Me.Nickname].Password);
        }

        public bool CharacterNameCheck(string name, out string message) {
            if (name.EndsWith("_clone", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_summon", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Evil_", StringComparison.OrdinalIgnoreCase) ||
                name.Contains(" ") || name.Contains(".")) {
                message = string.Format("\u0002{0}\u0002 is not a legal player name.", name);
                return false;
            }
            foreach (char c in Path.GetInvalidFileNameChars()) {
                if (name.Contains(c)) {
                    message = string.Format("\u0002{0}\u0002 is not a valid file name.", name);
                    return false;
                }
            }
            // Built-in characters
            if (name.Equals("AlliedForces_President", StringComparison.OrdinalIgnoreCase)) {
                message = string.Format("An ally named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            if (name.StartsWith("Bandit_Minion", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Pirate_Scallywag", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Mimic", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Frost_Monster", StringComparison.OrdinalIgnoreCase)) {
                message = string.Format("A monster named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            if (name.Equals("Monster_Warmachine", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Small_Warmachine", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Medium_Warmachine", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Large_Warmachine", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Demon_Wall", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Wall_of_Flesh", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Bandit_Leader", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Pirate_FirstMatey", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Crystal_Shadow", StringComparison.OrdinalIgnoreCase)) {
                message = string.Format("A boss named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            // Other characters
            if (File.Exists(Path.Combine(this.ArenaDirectory, "characters", name + ".char"))) {
                message = string.Format("A player named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            if (File.Exists(Path.Combine(this.ArenaDirectory, "monsters", name + ".char"))) {
                message = string.Format("A monster named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            if (File.Exists(Path.Combine(this.ArenaDirectory, "bosses", name + ".char"))) {
                message = string.Format("A boss named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            if (File.Exists(Path.Combine(this.ArenaDirectory, "npcs", name + ".char"))) {
                message = string.Format("An ally named \u0002{0}\u0002 is already present.", name);
                return false;
            }
            message = null;
            return true;
        }

        [Command("rename", 2, 2, "rename <player> <new name>", "Renames a character file. If they're already in battle, I'll keep their place.",
			Permission = ".admin", Scope = CommandScope.Channel)]
        public void CommandRename(object sender, CommandEventArgs e) {
            if (this.ArenaDirectory == null) {
                e.Whisper("I don't have access to the Arena data folder.");
                return;
            }
            // Make sure the player exists.
            if (!File.Exists(Path.Combine(this.ArenaDirectory, "characters", e.Parameters[0] + ".char"))) {
                e.Whisper(string.Format("No player named \u0002{0}\u0002 is present.", e.Parameters[0]));
                return;
            }
            // Check for conflicts.
            string message;
            if (!this.CharacterNameCheck(e.Parameters[1], out message)) {
                e.Whisper(message);
                return;
            }

            // Create the new character file.
            StreamReader reader = new StreamReader(Path.Combine(this.ArenaDirectory, "characters", e.Parameters[0] + ".char"));
            StreamWriter writer = new StreamWriter(Path.Combine(this.ArenaDirectory, "characters", e.Parameters[1] + ".char"));
            bool baseStatsSection = false;
            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                Match match = Regex.Match(line, @"^\s*\[(BaseStats)|.*?\]?\s*$", RegexOptions.IgnoreCase);
                // We'll replace the full name line with the new name.
                // We can't very well leave the renamed character file still showing the old name.
                if (match.Success) {
                    baseStatsSection = match.Groups[1].Success;
                } else if (baseStatsSection && line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)) {
                    writer.WriteLine("Name=" + e.Parameters[1]);
                    continue;
                }
                writer.WriteLine(line);
            }
            writer.Close();
            reader.Close();

            try {
                // Delete the old character file.
                File.Delete(Path.Combine(this.ArenaDirectory, "characters", e.Parameters[0] + ".char"));
                // Devoice them if we're on the channel.
                if (this.ArenaConnection.Channels.Contains(this.ArenaChannel)) {
                    if (this.ArenaConnection.Channels[this.ArenaChannel].Me.Status >= ChannelStatus.Halfop) {
                        this.ArenaConnection.Send("MODE {0} -v {1}", this.ArenaChannel, e.Parameters[0]);
                    }
                }
            } catch (Exception) { }

            // Update battle.txt
            StringBuilder newBattleTxt; string filePath = null;
            if (File.Exists(Path.Combine(this.ArenaDirectory, "txts", "battle.txt")))
                filePath = Path.Combine(this.ArenaDirectory, "txts", "battle.txt");
            else if (File.Exists(Path.Combine(this.ArenaDirectory, "battle.txt")))
                filePath = Path.Combine(this.ArenaDirectory, "battle.txt");
            if (filePath != null) {
                newBattleTxt = new StringBuilder();
                reader = new StreamReader(filePath);
                while (reader.EndOfStream) {
                    string line = reader.ReadLine();
                    if (line.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase))
                        newBattleTxt.AppendLine(e.Parameters[1]);
                    else
                        newBattleTxt.AppendLine(line);
                }
                reader.Close();
                File.WriteAllText(filePath, newBattleTxt.ToString());

                // Update battle2.txt
                filePath = Path.Combine(Path.GetDirectoryName(filePath), "battle2.txt");
                if (File.Exists(filePath)) {
                    bool battleSection = false; bool styleSection = false;
                    newBattleTxt = new StringBuilder();
                    reader = new StreamReader(filePath);
                    while (!reader.EndOfStream) {
                        string line = reader.ReadLine();
                        Match match = Regex.Match(line, @"^\s*\[(Battle)|(Style)|.*?\]?\s*$", RegexOptions.IgnoreCase);
                        if (match.Success) {
                            battleSection = match.Groups[1].Success;
                            styleSection = match.Groups[2].Success;
                        } else if (battleSection && line.StartsWith("List=", StringComparison.OrdinalIgnoreCase)) {
                            string[] list = line.Substring(5).Split(new char[] { '.' });
                            for (int i = 0; i < list.Length; ++i)
                                if (list[i].Equals(e.Parameters[1], StringComparison.OrdinalIgnoreCase)) list[i] = e.Parameters[1];
                            newBattleTxt.Append("List=");
                            newBattleTxt.AppendLine(string.Join(".", list));
                            continue;
                        } else if (styleSection && line.StartsWith(e.Parameters[0] + "=", StringComparison.OrdinalIgnoreCase)) {
                            newBattleTxt.Append(e.Parameters[0]);
                            newBattleTxt.AppendLine(line.Substring(e.Parameters[0].Length));
                            continue;
                        } else if (styleSection && line.StartsWith(e.Parameters[0] + ".lastaction=", StringComparison.OrdinalIgnoreCase)) {
                            newBattleTxt.Append(e.Parameters[0]);
                            newBattleTxt.AppendLine(line.Substring(e.Parameters[0].Length));
                            continue;
                        }
                        newBattleTxt.AppendLine(line);
                    }
                    reader.Close();
                    File.WriteAllText(filePath, newBattleTxt.ToString());
                }
            }
            e.Reply(string.Format("\u0002{0}\u0002 is now known as \u0002{1}\u0002.", e.Parameters[0], e.Parameters[1]));
            if (this.BattleStarted && this.IsAdmin && this.Turn.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase))
                this.BattleAction(false, "!next");
        }

        [Command("restore", 1, 2, "restore <character> [new name]", "Restores a zapped character", Permission = ".admin", Scope = CommandScope.Channel)]
        public void CommandRestore(object sender, CommandEventArgs e) {
            if (this.ArenaDirectory == null) {
                e.Whisper("I don't have access to the Arena data folder.");
                return;
            }
            string name; string message;
            if (e.Parameters.Length == 1)
                name = e.Parameters[0];
            else
                name = e.Parameters[1];
            // Check for conflicts.
            if (!this.CharacterNameCheck(name, out message)) {
                e.Whisper(message);
                return;
            }

            // Find the zapped character.
            DateTime latestDate = DateTime.MinValue; string latestFile = null;
            foreach (string file in Directory.EnumerateFiles(Path.Combine(this.ArenaDirectory, "characters", "zapped"), e.Parameters[0] + "_??????.char")) {
                DateTime date = File.GetLastWriteTime(file);
                if (date > latestDate) {
                    latestFile = file;
                    latestDate = date;
                }
            }
            if (latestFile == null) {
                e.Whisper(string.Format("No record of \u0002{0}\u0002 was found.", e.Parameters[0]));
                return;
            }

            // Restore the character.
            StreamReader reader = new StreamReader(latestFile);
            StreamWriter writer = new StreamWriter(Path.Combine(this.ArenaDirectory, "characters", name + ".char"));
            bool baseStatsSection = false; bool infoSection = false;
            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                Match match = Regex.Match(line, @"^\s*\[(BaseStats)|(Info)|.*?\]?\s*$", RegexOptions.IgnoreCase);
                // If the last seen date is more than 180 days ago, the Arena bot will erase the character.
                // We will reset the date if that's the case.
                if (match.Success) {
                    baseStatsSection = match.Groups[1].Success;
                    infoSection = match.Groups[2].Success;
                } else if (infoSection && line.StartsWith("LastSeen=", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        latestDate = DateTime.ParseExact(line, "ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        if (DateTime.Now - latestDate > TimeSpan.FromDays(180)) {
                            writer.WriteLine("LastSeen=" + DateTime.Now.ToString("ddd MMM dd HH:mm:ss yyyy"));
                            continue;
                        }
                    } catch (FormatException) { }
                } else if (baseStatsSection && e.Parameters.Length == 2 && line.StartsWith("Name=", StringComparison.OrdinalIgnoreCase)) {
                    writer.WriteLine("Name=" + name);
                    continue;
                }
                writer.WriteLine(line);
            }
            writer.Close();
            reader.Close();

            // Delete the old character.
            File.Delete(latestFile);

            if (name == e.Parameters[0])
                e.Reply(string.Format("\u0002{0}\u0002 has been restored.", e.Parameters[0], name));
            else
                e.Reply(string.Format("\u0002{0}\u0002 has been restored as \u0002{1}\u0002.", e.Parameters[0], name));
        }

        [Command("lateentry", 1, 1, "lateentry <player>", "Enters a player into the battle after it has started.", Permission = ".lateentry")]
        public void CommandLateEntry(object sender, CommandEventArgs e) {
            if (this.ArenaDirectory == null) {
                e.Whisper("I don't have access to the Arena data folder.");
                return;
            }
            string battleFile; string battleFile2;
            // Find the battle data files.
            if (Directory.Exists(Path.Combine(this.ArenaDirectory, "txts"))) {
                battleFile = Path.Combine(this.ArenaDirectory, "txts", "battle.txt");
                battleFile2 = Path.Combine(this.ArenaDirectory, "txts", "battle2.txt");
            } else {
                battleFile = Path.Combine(this.ArenaDirectory, "battle.txt");
                battleFile2 = Path.Combine(this.ArenaDirectory, "battle2.txt");
            }
            if (!File.Exists(battleFile)) {
                e.Whisper(string.Format("\u0002{0}\u0002 is missing. Perhaps there's currently no battle.", "battle.txt"));
                return;
            }
            if (!File.Exists(battleFile2)) {
                e.Whisper(string.Format("\u0002{0}\u0002 is missing. Perhaps there's currently no battle.", "battle2.txt"));
                return;
            }

            // Make sure the character exists.
            if (!File.Exists(Path.Combine(this.ArenaDirectory, "characters", e.Parameters[0] + ".char"))) {
                e.Whisper(string.Format("No character named \u0002{0}\u0002 is present.", e.Parameters[0]));
                return;
            }

            // Write to battle.txt.
            StringBuilder newBattleTxt = new StringBuilder();
            StreamReader reader = new StreamReader(battleFile);
            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                if (line.Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                    e.Whisper(string.Format("\u0002{0}\u0002 is already in the battle.", e.Parameters[0]));
                    return;
                }
                newBattleTxt.AppendLine(line);
            }
            reader.Close();
            File.WriteAllText(battleFile, newBattleTxt.ToString());

            // Write to battle2.txt.
            newBattleTxt = new StringBuilder();
            reader = new StreamReader(battleFile2);
            bool battleSection = false;
            while (!reader.EndOfStream) {
                string line = reader.ReadLine();
                Match match = Regex.Match(line, @"^\s*\[(Battle)|.*?\]?\s*$", RegexOptions.IgnoreCase);
                if (match.Success) {
                    battleSection = match.Groups[1].Success;
                } else if (battleSection && line.StartsWith("List=", StringComparison.OrdinalIgnoreCase)) {
                    // Read the list and append the new entrant to it.
                    newBattleTxt.Append("List=");
                    string[] list = line.Substring(5).Split(new char[] { '.' });
                    string[] newList = new string[list.Length + 1];
                    for (int i = 0; i < list.Length; ++i) {
                        if (list[i].Equals(e.Parameters[0], StringComparison.OrdinalIgnoreCase)) {
                            newBattleTxt.AppendLine(string.Join(".", list));
                            continue;
                        }
                        newList[i] = list[i];
                    }
                    newList[list.Length] = e.Parameters[0];
                    newBattleTxt.AppendLine(string.Join(".", newList));
                    continue;
                }
                newBattleTxt.AppendLine(line);
            }
            reader.Close();
            File.WriteAllText(battleFile2, newBattleTxt.ToString());
        }
#endregion

#region Character information
        private Task GetAbilitiesAsync() => this.GetAbilitiesAsync(null);
		private Task GetAbilitiesAsync(CancellationToken cancellationToken) => this.GetAbilitiesAsync(null, cancellationToken);
		private Task GetAbilitiesAsync(string characterName) => this.GetAbilitiesAsync(characterName, default(CancellationToken));
		private async Task GetAbilitiesAsync(string characterName, CancellationToken cancellationToken) {
            Character character;
            if (characterName == null) {
                if (this.LoggedIn == null) throw new InvalidOperationException("The bot is not logged in.");
                character = Characters[this.LoggedIn];
            } else
                character = Characters[characterName];

            if (character.BaseHP == 0 || character.BaseSTR == 0 || character.BaseDEF == 0 || character.BaseINT == 0 || character.BaseSPD == 0 || character.EquippedWeapon == null) {
                this.WriteLine(2, 12, "[" + character.ShortName + "] Checking attributes.");
                await GetAttributesAsync(character.ShortName);
            }
			if (cancellationToken.IsCancellationRequested) return;

            await delay();
            this.WriteLine(2, 12, "[" + character.ShortName + "] Checking equipment.");
            await GetEquipmentAsync(character.ShortName);
			if (cancellationToken.IsCancellationRequested) return;

			if (character.Skills == null) {
                await delay();
                this.WriteLine(2, 12, "[" + character.ShortName + "] Checking skills.");
                await GetSkillsAsync(character.ShortName);
				if (cancellationToken.IsCancellationRequested) return;
			}

			if (character.Weapons == null) {
                await delay();
                this.WriteLine(2, 12, "[" + character.ShortName + "] Checking weapons.");
                await GetWeaponsAsync(character.ShortName);
				if (cancellationToken.IsCancellationRequested) return;
			}

			// Look up each weapon.
			foreach (string weaponName in character.Weapons.Keys) {
				if (!this.Weapons.TryGetValue(weaponName, out var weapon) || !weapon.IsWellKnown) {
                    await delay();
					if (cancellationToken.IsCancellationRequested) return;
					this.WriteLine(2, 12, string.Format("Looking up weapon \u0002{0}\u0002.", weaponName));
                    await GetWeaponInfoAsync(weaponName);
					if (cancellationToken.IsCancellationRequested) return;

					if (characterName == null) {
                        if (character.EquippedWeapon != weaponName) {
                            await delay();
							if (cancellationToken.IsCancellationRequested) return;
							this.BattleAction(true, "!equip " + weaponName);
                        }

                        // Get the technique list.
                        if (character.EquippedWeapon == weaponName) {
                            await delay();
							if (cancellationToken.IsCancellationRequested) return;
							await GetTechniquesAsync(character.ShortName);
                        }
                    }
					if (cancellationToken.IsCancellationRequested) return;
				}
			}

            /*
            if (character.Styles == null) {
                await delay();
                this.WriteLine(2, 12, "[" + character.ShortName + "] Checking styles.");
                await GetStylesAsync(character.ShortName);
            }
            */

            character.IsWellKnown = true;
            character.IsReadyToControl = true;
            this.WriteLine(2, 12, "Finished setting up.");
        }

        public Task GetAttributesAsync(string character) => getCharacterInfoAsync("!stats", "attributes", character);
        public Task GetEquipmentAsync(string character) => getCharacterInfoAsync("!look", "equipment", character);
        public Task GetSkillsAsync(string character) => getCharacterInfoAsync("!skills", "skills", character);
        public Task GetWeaponsAsync(string character) => getCharacterInfoAsync("!weapons", "weapons", character);
        public Task GetTechniquesAsync(string character) => getCharacterInfoAsync("!techs", "techniques", character);
        public Task GetStylesAsync() => getCharacterInfoAsync("!styles", "styles", null);

        public Task GetSkillInfoAsync(string skill) => getThingInfoAsync("skill", skill);
        public Task GetWeaponInfoAsync(string weapon) => getThingInfoAsync("weapon", weapon);
        public Task GetTechniqueInfoAsync(string technique) => getThingInfoAsync("technique", technique);
        public Task GetItemInfoAsync(string item) => getThingInfoAsync("item", item);

        private Task getCharacterInfoAsync(string command, string category, string character) {
            if (character == null) {
                if (this.LoggedIn == null) throw new InvalidOperationException("The bot is not logged in.");
                character = this.LoggedIn;
            }

			string key = category + "/" + character;
			if (!this.infoTasks.TryGetValue(key, out TaskState taskState)) {
				taskState = new TaskState() { LinesRemaining = new BitVector32(-1) };
				this.infoTasks.Add(key, taskState);
				if (character == null) BattleAction(true, command);
				else BattleAction(true, command + " " + character);
			}

			if (this.infoTimer == null) {
				this.infoTimer = new Timer(10e+3) { AutoReset = false };
				this.infoTimer.Elapsed += infoTimer_Elapsed;
				this.infoTimer.Start();
			}

			return taskState.TaskSource.Task;
        }

        private Task getThingInfoAsync(string category, string thing) {
            string key = category + "/" + thing;
			if (!this.infoTasks.TryGetValue(key, out TaskState taskState)) {
				taskState = new TaskState();
				this.infoTasks.Add(key, taskState);
				BattleAction(true, "!view-info " + category + " " + thing);
			}
			return taskState.TaskSource.Task;
        }

        public Character GetCharacter(string name, bool add = true) {
            foreach (Character character in this.Characters.Values) {
                if (name.Equals(character.Name, StringComparison.OrdinalIgnoreCase))
                    return character;
            }
            if (!add) return null;
            Character character2 = new Character() { Name = name, ShortName = "*" + name };
            this.Characters.Add(character2.ShortName, character2);
            return character2;
        }

        public string GetShortName(string name, bool add = true, bool inBattle = false) {
            if (inBattle) {
                foreach (Combatant combatant in this.BattleList.Values) {
                    if (combatant.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return combatant.ShortName;
                }
                return null;
            } else {
                foreach (Character character in this.Characters.Values) {
                    if (character.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                        return character.ShortName;
                }
                if (!add) return null;
                Character character2 = new Character() { ShortName = "*" + name };
                this.Characters.Add(name, character2);
                return character2.ShortName;
            }
        }

		/// <summary>Handles the end of a list and/or starts a new list.</summary>
		private void endList(InfoListType type, Character character) {
			if (this.infoType != InfoListType.None) {
				switch (this.infoType) {
					case InfoListType.SkillsPassive:
					case InfoListType.SkillsActive:
					case InfoListType.SkillsResistances:
					case InfoListType.SkillsKiller:
						if (this.infoCharacter.Skills == null) this.infoCharacter.Skills = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
						foreach (var entry in this.infoList) {
							this.infoCharacter.Skills[entry.Item1] = entry.Item2;
						}
						this.WriteLine(2, 7, string.Format("Registered {0}'s skills: {1}", this.infoCharacter.Name, string.Join(", ", this.infoCharacter.Skills)));
						thingInfoComplete("skills/" + this.infoCharacter.ShortName, ((int) this.infoType) - 1, BitVector32.CreateSection(15));
						break;
					case InfoListType.Weapons:
						this.infoCharacter.Weapons = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
						foreach (var entry in this.infoList) {
							if (!this.Weapons.ContainsKey(entry.Item1))
								this.Weapons[entry.Item1] = new Weapon() { Name = entry.Item1, Techniques = new List<string>() };
							this.infoCharacter.Weapons[entry.Item1] = entry.Item2;
						}
						this.WriteLine(2, 7, string.Format("Registered {0}'s weapons: {1}", this.infoCharacter.Name, string.Join(", ", this.infoCharacter.Weapons)));
						thingInfoComplete("weapons/" + this.infoCharacter.ShortName);
						break;
					case InfoListType.Shields: thingInfoComplete("shields/" + this.infoCharacter.ShortName); break;
					case InfoListType.Techniques:
						if (this.infoCharacter.Techniques == null) this.infoCharacter.Techniques = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
						foreach (var entry in this.infoList) {
							this.infoCharacter.Techniques[entry.Item1] = entry.Item2;
						}
						this.WriteLine(2, 7, string.Format("Registered {0}'s techniques: {1}", this.infoCharacter.Name, string.Join(", ", this.infoCharacter.Techniques)));
						if (this.infoCharacter.ShortName == this.LoggedIn) this.WaitingForOwnTechniques = false;
						thingInfoComplete("techniques/" + this.infoCharacter.ShortName);
						break;
					case InfoListType.Attributes:
						thingInfoComplete("attributes/" + this.infoCharacter.ShortName);
						break;
					case InfoListType.Equipment:
						thingInfoComplete("equipment/" + this.infoCharacter.ShortName);
						break;
					case InfoListType.Styles:
						this.infoCharacter.Styles = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
						if (this.infoCharacter.StyleExperience == null) this.infoCharacter.StyleExperience = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
						foreach (var entry in this.infoList) {
							this.infoCharacter.Styles[entry.Item1] = entry.Item2;
						}
						this.WriteLine(2, 7, string.Format("Registered {0}'s styles: {1}", this.infoCharacter.Name, string.Join(", ", this.infoCharacter.Styles)));
						thingInfoComplete("styles/" + this.infoCharacter.ShortName);
						break;
				}
			}

			this.infoType = type;
			this.infoCharacter = character;

			if (type != InfoListType.None) {
				this.infoList = new List<Tuple<string, int>>();
				if (this.infoTimer != null) {
					this.infoTimer.Stop();
					this.infoTimer.Start();
				}
			} else if (infoTasks.Count == 0)
				this.infoTimer?.Stop();
			else {
				this.infoTimer.Stop();
				this.infoTimer.Start();
			}
		}

		private void infoTimer_Elapsed(object sender, ElapsedEventArgs e) {
			endList(InfoListType.None, null);

			var tasksToRemove = new List<string>();
			foreach (var task in infoTasks) {
				if (task.Value.LinesRemaining.Data == -1) {
					++task.Value.Time;
					if (task.Value.Time >= 3) {
						task.Value.TaskSource.SetException(new TimeoutException());
						tasksToRemove.Add(task.Key);
					}
				} else {
					// Shorter timeout when at least one list has already been received.
					task.Value.TaskSource.SetResult(null);
					tasksToRemove.Add(task.Key);
				}
			}
			foreach (var task in tasksToRemove) infoTasks.Remove(task);
		}

		private void processList(string message) {
			if (message == null) {
				endList(InfoListType.None, null);
				infoFragmentLength = 0;
			} else {
				var fields = message.Split(new[] { ", " }, 0);
				bool broken = false;
				foreach (var field in fields) {
					if (field == "") {
						// Sometimes the list fragment will be too long to fit in an IRC line, so mIRC splits it.
						// We need to check the length of the fragment (before splitting), so count it here.
						broken = true;
						break;
					}

					var text = IrcClient.RemoveCodes(field);
					var pos = text.IndexOf('(');
					Debug.Assert(pos >= 0);
					var item = text.Substring(0, pos);
					var level = int.Parse(text.Substring(pos + 1, text.Length - 2 - pos));
					infoList.Add(new Tuple<string, int>(item, level));
				}

				infoFragmentLength += fields.Length;
				if (!broken) {
					// Battle Arena will split lists into multiple lines if they contain more than a certain number of elements.
					// We'll assume that a line with fewer elements is the last line.
					int max;
					switch (this.infoType) {
						case InfoListType.Weapons: max = 17; break;  // The limit is actually 18 for fragments after the first.
						case InfoListType.SkillsPassive: max = 13; break;
						case InfoListType.SkillsActive: max = 13; break;
						case InfoListType.SkillsResistances: max = 13; break;
						case InfoListType.SkillsKiller: max = 13; break;
						case InfoListType.Shields: max = 19; break;
						default: max = int.MaxValue; break;
					}
					if (infoFragmentLength < max) {
						endList(InfoListType.None, null);
					}
					infoFragmentLength = 0;
				}
			}
		}

		private void thingInfoComplete(string key) {
			if (this.infoTasks.TryGetValue(key, out TaskState taskState)) {
				this.infoTasks.Remove(key);
				taskState.TaskSource.SetResult(null);
			}
		}
		private void thingInfoComplete(string key, int message, BitVector32.Section mask) {
			if (this.infoTasks.TryGetValue(key, out TaskState taskState)) {
				taskState.LinesRemaining[message] = false;
				if (taskState.LinesRemaining[mask] == 0) {
					this.infoTasks.Remove(key);
					taskState.TaskSource.SetResult(null);
				}
			}
		}

		[ArenaRegex(@"^(?:[^ ]+\(\d+\)(?:$|, ))+$", StripFormats = true)]
		internal void OnListFragment(object sender, TriggerEventArgs e) {
			processList(e.Match.Value);
		}

		[ArenaRegex(@"(?:\x033\x02|\x02\x033)(.*) \x02has the following weapons:(?: (.*))?")]
        internal void OnWeapons(object sender, TriggerEventArgs e) {
            // Check for a bug in Battle Arena.
            if (e.Match.Groups[2].Value == "") {
                ++this.RepeatCommand;
                return;
            }

            var character = this.GetCharacter(e.Match.Groups[1].Value);
			endList(InfoListType.Weapons, character);
			processList(e.Match.Groups[2].Value);
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02(?:knows|has) the following (?:\x0312)?(?:(passive(?:\x033)? skills)|(active(?:\x033)? skills)|(resistances(?:\x033)?)|(monster killer traits(?:\x033)?)):\x02 ([^(), ]+\(\d+\)(?:, [^(), ]+\(\d+\))*)")]
        internal void OnSkills(object sender, TriggerEventArgs e) {
			InfoListType type;
			if (e.Match.Groups[2].Success) type = InfoListType.SkillsPassive;
			else if (e.Match.Groups[3].Success) type = InfoListType.SkillsActive;
			else if (e.Match.Groups[4].Success) type = InfoListType.SkillsResistances;
			else if (e.Match.Groups[5].Success) type = InfoListType.SkillsKiller;
			else throw new FormatException("Unknown skill list");

			var character = this.GetCharacter(e.Match.Groups[1].Value);
			endList(type, character);
			processList(e.Match.Groups[6].Value);
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02currently knows no skills\.")]
        internal void OnSkillsNone(object sender, TriggerEventArgs e) {
            var character = this.GetCharacter(e.Match.Groups[1].Value);
			endList(InfoListType.SkillsPassive, character);
			processList(null);
			endList(InfoListType.None, null);
		}

		[ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02knows the following techniques for (?:(his)|(her)|(its)|\w+) (?:equipped weapons|([^ ]*)):\x02 ([^(), ]+\(\d+\)(?:, [^(), ]+\(\d+\))*)")]
        internal void OnTechniques(object sender, TriggerEventArgs e) {
            var character = this.GetCharacter(e.Match.Groups[1].Value);

            // Check their gender.
            if (e.Match.Groups[2].Success)
                character.Gender = Gender.Male;
            else if (e.Match.Groups[3].Success)
                character.Gender = Gender.Female;
            else if (e.Match.Groups[4].Success)
                character.Gender = Gender.None;
            else
                character.Gender = Gender.Unknown;

			// TODO: Check their weapon and update for the new list format.

			endList(InfoListType.Techniques, character);
			processList(e.Match.Groups[6].Value);
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02does not know any techniques for (?:(his)|(her)|(its)|\w+) ([^ ]*)\.")]
        internal void OnTechniquesNone(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[1].Value);

            // Check their gender.
            if (e.Match.Groups[2].Success)
                character.Gender = Gender.Male;
            else if (e.Match.Groups[3].Success)
                character.Gender = Gender.Female;
            else if (e.Match.Groups[4].Success)
                character.Gender = Gender.None;
            else
                character.Gender = Gender.Unknown;

            character.EquippedWeapon = e.Match.Groups[5].Value;

			/*
            Weapon weapon;
            if (character.Techniques != null) {
                if (this.Weapons.TryGetValue(character.EquippedWeapon, out weapon) && weapon.Techniques != null) {
                    foreach (string techniqueName in weapon.Techniques)
                        character.Techniques.Remove(techniqueName);
                }
                if (character.EquippedWeapon2 != null) {
                    if (this.Weapons.TryGetValue(character.EquippedWeapon2, out weapon) && weapon.Techniques != null) {
                        foreach (string techniqueName in weapon.Techniques)
                            character.Techniques.Remove(techniqueName);
                    }
                }
            }
			*/

			endList(InfoListType.Techniques, character);
			processList(null);
			endList(InfoListType.None, null);
		}

		[ArenaRegex(@"^\x033Here are your current stats:")]
        internal void OnStatsSelf(object sender, TriggerEventArgs e) {
            if (this.ViewingStatsCharacter == null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            } else if (this.ViewingStatsCharacter.ShortName != null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            }
            this.ViewingStatsCharacter.ShortName = this.LoggedIn;
			endList(InfoListType.Attributes, this.ViewingStatsCharacter);
        }

        [ArenaRegex(@"^\x033Here are the current stats for ([^ ]*):")]
        internal void OnStatsOther(object sender, TriggerEventArgs e) {
            if (this.ViewingStatsCharacter == null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            } else if (this.ViewingStatsCharacter.ShortName != null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            }
            this.ViewingStatsCharacter.ShortName = e.Match.Groups[1].Value;
			endList(InfoListType.Attributes, this.ViewingStatsCharacter);
		}

		//[ArenaRegex(@"^\[\x034HP\x0312 (\d*)\x031/\x0312(\d*)(?:\x03\d{0,2}|\x0F)\] \[\x034TP\x0312 (\d*)\x031/\x0312(\d*)(?:\x03\d{0,2}|\x0F)\](?: \[\x034Ignition Gauge\x0312 (\d*)\x031/\x0312(\d*)(?:\x03\d{0,2}|\x0F)\])? \[\x034Status\x0312 \x033((?:[^\]]|\[[^\]]*\])*)(?:\x03\d{0,2}|\x0F)\](?: \[\x034Royal Guard Meter\x0312 (\d*)(?:\x03\d{0,2}|\x0F)\])?")]
		[ArenaRegex(@"^\[HP (\d+)/(\d+)\] \[TP (\d+)/(\d+)\](?: \[Ignition Gauge (\d+)/(\d+)\])?(?: \[Level (\d+)\])?(?: \[Armor Level (\d+)\])?(?: \[Status ([^\]]+)\])?(?: \[Royal Guard Meter (\d+)\])?(?: \[Capacity Points (\d+)/(\d+)\])?(?: \[Enhancement Points (\d+)\])?(?: \[Login Points (\d+)\])?", true)]
        internal void OnStats1(object sender, TriggerEventArgs e) {
            if (this.ViewingStatsCharacter == null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            } else if (this.ViewingStatsCharacter.BaseHP != 0) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            }

            this.ViewingStatsCombatant.HP = int.Parse(e.Match.Groups[1].Value);
            this.ViewingStatsCombatant.TP = int.Parse(e.Match.Groups[3].Value);
            this.ViewingStatsCharacter.BaseHP = int.Parse(e.Match.Groups[2].Value);
            this.ViewingStatsCharacter.BaseTP = int.Parse(e.Match.Groups[4].Value);
            this.ViewingStatsCharacter.IgnitionGauge = e.Match.Groups[5].Success ? int.Parse(e.Match.Groups[5].Value) : 0;
            this.ViewingStatsCharacter.IgnitionCapacity = e.Match.Groups[6].Success ? int.Parse(e.Match.Groups[6].Value) : 0;
            this.ViewingStatsCharacter.RoyalGuardCharge = e.Match.Groups[8].Success ? int.Parse(e.Match.Groups[8].Value) : 0;

            this.WriteLine(2, 7, string.Format("Registered {0}'s attributes.  HP: {1}/{2}  TP: {3}/{4}  IG: {5}/{6}  RG: {7}", this.ViewingStatsCharacter.ShortName ?? "*",
                this.ViewingStatsCombatant.HP, this.ViewingStatsCharacter.BaseHP,
                this.ViewingStatsCombatant.TP, this.ViewingStatsCharacter.BaseTP,
                this.ViewingStatsCharacter.IgnitionGauge, this.ViewingStatsCharacter.IgnitionCapacity, this.ViewingStatsCharacter.RoyalGuardCharge));

            this.viewInfoStatsCheck();
        }

        [ArenaRegex(@"^\[Strength: (\d+)(?: \+(\d+))?\] \[Defense: (\d+)(?: \+(\d+))?\] \[Intelligence: (\d+)(?: \+(\d+))?\] ?\[Speed: (\d+)(?: \+(\d+))?\]", true)]
        internal void OnStats2(object sender, TriggerEventArgs e) {
            if (this.ViewingStatsCharacter == null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            } else if (this.ViewingStatsCombatant.STR != 0) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            }

            // TODO: Fix this for level-synced players.
            this.ViewingStatsCombatant.STR = int.Parse(e.Match.Groups[1].Value);
            this.ViewingStatsCombatant.DEF = int.Parse(e.Match.Groups[3].Value);
            this.ViewingStatsCombatant.INT = int.Parse(e.Match.Groups[5].Value);
            this.ViewingStatsCombatant.SPD = int.Parse(e.Match.Groups[7].Value);
            this.WriteLine(2, 7, string.Format("Registered {0}'s current attributes.  STR: {1}  DEF: {2}  INT: {3}  SPD: {4}",
                this.ViewingStatsCharacter.ShortName ?? "*", this.ViewingStatsCombatant.STR, this.ViewingStatsCombatant.DEF, this.ViewingStatsCombatant.INT, this.ViewingStatsCombatant.SPD));

            this.viewInfoStatsCheck();
        }

        [ArenaRegex(@"^\[\x034Current Weapons? Equipped ?\x0312 ?([^ \x03]*)( )?(?:\x034and\x0312 ([^ \x03]*))?(?:\x03\d{0,2}|\x0F)\](?: \[\x034Current Accessory (?:Equipped )?\x0312([^ ]*)(?:\x03\d{0,2}|\x0F)\](?: \[\x034Current Head Armor \x0312([^ \x03]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Current Body Armor \x0312([^ \x03]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Current Leg Armor \x0312([^ \x03]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Current Feet Armor \x0312([^ \x03]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Current Hand Armor \x0312([^ \x03]*)(?:\x03\d{0,2}|\x0F)\])?)?")]
        internal void OnStats3(object sender, TriggerEventArgs e) {
            if (this.ViewingStatsCharacter == null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            } else if (this.ViewingStatsCharacter.EquippedWeapon != null) {
                this.ViewingStatsCharacter = new Character();
                this.ViewingStatsCombatant = new Combatant();
            }

            this.ViewingStatsCharacter.EquippedAccessory = (!e.Match.Groups[4].Success || e.Match.Groups[4].Value == "nothing" || e.Match.Groups[4].Value == "none") ? null : IrcClient.RemoveCodes(e.Match.Groups[4].Value);
            this.ViewingStatsCharacter.EquippedWeapon = IrcClient.RemoveCodes(e.Match.Groups[1].Value);
            if (e.Match.Groups[3].Success)
                this.ViewingStatsCharacter.EquippedWeapon2 = IrcClient.RemoveCodes(e.Match.Groups[3].Value);
            else
                this.ViewingStatsCharacter.EquippedWeapon2 = null;

            this.WriteLine(2, 7, string.Format("Registered {0}'s equipment.  Weapons: {1}, {2}  Accessory: {3}",
                this.ViewingStatsCharacter.ShortName ?? "*",
                this.ViewingStatsCharacter.EquippedWeapon ?? "nothing",
                this.ViewingStatsCharacter.EquippedWeapon2 ?? "nothing",
                this.ViewingStatsCharacter.EquippedAccessory ?? "nothing"));

            this.viewInfoStatsCheck();
        }

        public void viewInfoStatsCheck() {
            if (this.ViewingStatsCharacter.ShortName != null && this.ViewingStatsCombatant.HP != 0 && this.ViewingStatsCombatant.STR != 0 && this.ViewingStatsCharacter.EquippedWeapon != null) {
                // Register this character.
                Character character; Combatant combatant;
                if (this.ViewingStatsCharacter.ShortName == null) {
                    this.WriteLine(1, 4, string.Format("Error: a cached character name was not set!"));
                } else {
                    character = this.GetCharacter(this.ViewingStatsCharacter.ShortName);
                    if (this.ViewingStatsCharacter.BaseHP != 0) {
                        character.BaseHP = this.ViewingStatsCharacter.BaseHP;
                        character.BaseTP = this.ViewingStatsCharacter.BaseTP;
                    }
                    if (this.ViewingStatsCharacter.EquippedWeapon != null) {
                        character.EquippedWeapon = this.ViewingStatsCharacter.EquippedWeapon;
                        character.EquippedWeapon2 = this.ViewingStatsCharacter.EquippedWeapon2;
                        character.EquippedAccessory = this.ViewingStatsCharacter.EquippedAccessory;
                        character.EquippedTechniques = new List<string>();
                        this.GetEquippedTechniques(character);
                    }

                    if (this.BattleList.TryGetValue(character.ShortName, out combatant)) {
                        if (this.ViewingStatsCharacter.BaseHP != 0) {
                            combatant.HP = this.ViewingStatsCombatant.HP;
                            combatant.TP = this.ViewingStatsCombatant.TP;
                        }
                        if (this.ViewingStatsCombatant.STR != 0) {
                            combatant.STR = this.ViewingStatsCombatant.STR;
                            combatant.DEF = this.ViewingStatsCombatant.DEF;
                            combatant.INT = this.ViewingStatsCombatant.INT;
                            combatant.SPD = this.ViewingStatsCombatant.SPD;
                        }
                    } else {
                        character.BaseSTR = this.ViewingStatsCombatant.STR;
                        character.BaseDEF = this.ViewingStatsCombatant.DEF;
                        character.BaseINT = this.ViewingStatsCombatant.INT;
                        character.BaseSPD = this.ViewingStatsCombatant.SPD;
                    }

                    character.IsWellKnown = true;
                    this.WriteLine(2, 10, string.Format("Registered data for {0} ({1}).", character.Name ?? "*", this.ViewingStatsCharacter.ShortName));
                }

				endList(InfoListType.None, null);
                this.ViewingStatsCharacter = null;
                this.ViewingStatsCombatant = null;
            }
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)([^\x02]*) \x02is wearing\x02 ([^\x02]*) \x02on (\w+) head[,;]\x02 ([^\x02]*) \x02on \3 body[,;]\x02 ([^\x02]*) \x02on \3 legs[,;]\x02 ([^\x02]*) \x02on \3 feet[,;]\x02 ([^\x02]*) \x02on \3 hands\. \1 also has[\x02\x03\d ]*([^\x02]*)(?:[\x02\x03\d]* and\x02 ([^\x02]*))?[\x02\x03\d]* ?(?:equipped )?as an accessory and is currently using the\x02 ([^\x02]*) [\x02\x03\d]*(?:weapon|and\x02 ([^\x02]*) [\x02\x03\d]*weapons)")]
        internal void OnEquipment(object sender, TriggerEventArgs e) {
            var character = this.GetCharacter(e.Match.Groups[1].Value);
            character.EquippedAccessory = IrcClient.RemoveCodes(e.Match.Groups[8].Value) == "nothing" ? null : IrcClient.RemoveCodes(e.Match.Groups[8].Value);
            character.EquippedWeapon = IrcClient.RemoveCodes(e.Match.Groups[10].Value);
            if (e.Match.Groups[11].Success)
                character.EquippedWeapon2 = IrcClient.RemoveCodes(e.Match.Groups[11].Value);
            else
                character.EquippedWeapon2 = null;
            this.WriteLine(2, 7, string.Format("Registered {0}'s equipment.  Weapons: {1}, {2}  Accessory: {3}", character.Name,
                character.EquippedWeapon ?? "nothing",
                character.EquippedWeapon2 ?? "nothing",
                character.EquippedAccessory ?? "nothing"));

            character.EquippedTechniques = new List<string>();
            this.GetEquippedTechniques(character);

			endList(InfoListType.Equipment, character);
			endList(InfoListType.None, null);
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02is currently using the\x02 ([^ ]+) \x02style\. \[(?:XP: (\d+) / (\d+)|([^\]]*Max[^\]]*))\]")]
        internal void OnStyle(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[1].Value);
            character.CurrentStyle = e.Match.Groups[2].Value;

            // Add this style to this character.
            if (character.Styles == null) character.Styles = new Dictionary<string, int>();
            character.Styles[character.CurrentStyle] = e.Match.Groups[5].Success ? 10 : int.Parse(e.Match.Groups[4].Value) / 500;

            // Add the experience to this character.
            if (character.StyleExperience == null) character.StyleExperience = new Dictionary<string, int>();
            character.StyleExperience[character.CurrentStyle] = e.Match.Groups[5].Success ? 0 : int.Parse(e.Match.Groups[3].Value);

            this.WriteLine(2, 7, string.Format("Registered {0}'s current style: {1}  Level: {2}  Experience: {3}/{4}", character.Name,
                character.CurrentStyle, character.Styles[character.CurrentStyle], character.StyleExperience[character.CurrentStyle],
                character.Styles[character.CurrentStyle] == 10 ? 0 : character.Styles[character.CurrentStyle] * 500));
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02knows the following styles: (\x02[^ (]+\(\d+\)\x02(?:, \x02[^ (]+\(\d+\)\x02)*)")]
        internal void OnStyles(object sender, TriggerEventArgs e) {
            var character = this.GetCharacter(e.Match.Groups[1].Value);
			endList(InfoListType.Styles, character);
		}

		[ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*)\x02's ([^ ]+) style has leveled up! It is now\x02 level (\d+)\x02!")]
        internal void OnStyleLevelUp(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[1].Value);
            character.CurrentStyle = e.Match.Groups[2].Value;

            // Add this style to this character.
            if (character.Styles == null) character.Styles = new Dictionary<string, int>();
            character.Styles[character.CurrentStyle] = int.Parse(e.Match.Groups[3].Value);
            if (character.StyleExperience == null) character.StyleExperience = new Dictionary<string, int>();
            character.StyleExperience[character.CurrentStyle] = 0;

            this.WriteLine(2, 7, string.Format("{0}'s {1} style level has increased to {2}!", character.Name, character.CurrentStyle, character.Styles[character.CurrentStyle]));
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)(.*) \x02has switched to the\x02 ([^ ]+) \x02style!")]
        internal void OnStyleChange(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[1].Value);
            character.CurrentStyle = e.Match.Groups[2].Value;

            // Add this style to this character.
            if (character.Styles == null) character.Styles = new Dictionary<string, int>();
            if (!character.Styles.ContainsKey(character.CurrentStyle)) character.Styles.Add(character.CurrentStyle, 1);
            if (character.StyleExperience == null) character.StyleExperience = new Dictionary<string, int>();

            this.WriteLine(2, 7, string.Format("{0} changes to the {1} style.", character.Name, character.CurrentStyle));
        }

        [ArenaRegex(@"^\x033\x02([^\x02]*) \x02has\x02 \$\$([\d,]*) \x02double dollars\.")]
        internal void OnDoubleDollars(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[1].Value);
            character.DoubleDollars = int.Parse(e.Match.Groups[2].Value.Replace(",", ""));
            this.WriteLine(2, 7, string.Format("{0} has $${1}.", character.Name, character.DoubleDollars));
        }
#endregion

#region My character
        [ArenaRegex(@"^\x032You enter the arena with a total of\x02 (\d+) \x02.* to spend.")]
        internal void OnNewCharacterSelf(object sender, TriggerEventArgs e) {
            if (!this.OwnCharacters.ContainsKey(e.Client.Me.Nickname))
                this.OwnCharacters.Add(e.Client.Me.Nickname, new OwnCharacter());

            Character character = new Character() {
                Category = Category.Player, Name = e.Client.Me.Nickname, ShortName = e.Client.Me.Nickname, Gender = Gender.Male,
                BaseHP = 100, BaseTP = 20, BaseSTR = 5, BaseDEF = 5, BaseINT = 5, BaseSPD = 5,
                EquippedWeapon = "Fists",
                ElementalResistances = new List<string>(), ElementalWeaknesses = new List<string>(),
                WeaponResistances = new List<string>(), WeaponWeaknesses = new List<string>(),
                Weapons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Techniques = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Skills = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Styles = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                StyleExperience = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                CurrentStyle = "Trickster",
                Ignitions = new List<string>(),
                RedOrbs = int.Parse(e.Match.Groups[1].Value), BlackOrbs = 1,
                IsReadyToControl = true, IsWellKnown = true };
            this.Characters.Add(character.ShortName, character);
        }

        [ArenaRegex(@"^\x032Your password has been set to\x02 (battlearena\d\d\w) \x02and it is recommended you change it using the command\x02 !newpass battlearena\d\d\w newpasswordhere \x02in private or at least write the password down\.")]
        internal void OnNewCharacterPassword(object sender, TriggerEventArgs e) {
            OwnCharacter ownCharacter;
            if (this.OwnCharacters.TryGetValue(e.Client.Me.Nickname, out ownCharacter)) {
                // Set the password.
                if (ownCharacter.Password != null)
                    this.BattleAction(true, string.Format("!newpass {0} {1}", e.Match.Groups[1].Value, ownCharacter.Password));
                else
                    ownCharacter.Password = e.Match.Groups[1].Value;
                this.WriteLine(1, 7, "Finished setting up my character.");
            }
            this.LoggedIn = e.Client.Me.Nickname;
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)Your \x02gender has been set to\x02 (?:(male)|(female)|neither|none|its)")]
        internal void OnGender(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[1].Success)
                    character.Gender = Gender.Male;
                else if (e.Match.Groups[2].Success)
                    character.Gender = Gender.Female;
                else
                    character.Gender = Gender.None;
            }
        }

        [ArenaRegex(@"^\x033You spend\x02 ([\d,]+) \x02.* for\x02 (\d+) ([^ ]+?)(?:\(s\))?\x02!(?: \x033You have\x02 ([\d,]+) \x02.* left)")]
        internal void OnOrbSpendItems(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[4].Success)
                    character.RedOrbs = int.Parse(e.Match.Groups[4].Value.Replace(",", ""));
                else
                    character.RedOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                if (character.Items == null)
                    character.Items = new Dictionary<string, int> { { e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value) } };
                else if (!character.Items.ContainsKey(e.Match.Groups[3].Value))
                    character.Items.Add(e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value));
                else
                    character.Items[e.Match.Groups[3].Value] += int.Parse(e.Match.Groups[2].Value);
            }
        }

        [ArenaRegex(@"^\x033You spend\x02 ([\d,]*) \x02.* for\x02 \+(\d*) \x02to your\x02 ([^ ]*) technique\x02!(?: \x033You have\x02 ([\d,]+) \x02.* left)")]
        internal void OnOrbSpendTechniques(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[4].Success)
                    character.RedOrbs = int.Parse(e.Match.Groups[4].Value.Replace(",", ""));
                else
                    character.RedOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                if (character.Techniques == null)
                    character.Techniques = new Dictionary<string, int> { { e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value) } };
                else if (!character.Techniques.ContainsKey(e.Match.Groups[3].Value))
                    character.Techniques.Add(e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value));
                else
                    character.Techniques[e.Match.Groups[3].Value] += int.Parse(e.Match.Groups[2].Value);
            }
        }

        [ArenaRegex(@"^\x033You spend\x02 ([\d,]*) \x02.* for\x02 \+(\d*) \x02to your\x02 ([^ ]*) skill\x02!(?: \x033You have\x02 ([\d,]+) \x02.* left)")]
        internal void OnOrbSpendSkills(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[4].Success)
                    character.RedOrbs = int.Parse(e.Match.Groups[4].Value.Replace(",", ""));
                else
                    character.RedOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                if (character.Skills == null)
                    character.Skills = new Dictionary<string, int> { { e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value) } };
                else if (!character.Skills.ContainsKey(e.Match.Groups[3].Value))
                    character.Skills.Add(e.Match.Groups[3].Value, int.Parse(e.Match.Groups[2].Value));
                else
                    character.Skills[e.Match.Groups[3].Value] += int.Parse(e.Match.Groups[2].Value);
            }
        }

        [ArenaRegex(@"^^\x033You spend\x02 ([\d,]*) \x02.* for\x02 \+([\d,]*) \x02to your ([^ ]*)!(?: \x033You have\x02 ([\d,]+) \x02.* left)")]
        internal void OnOrbSpendAttributes(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[4].Success)
                    character.RedOrbs = int.Parse(e.Match.Groups[4].Value.Replace(",", ""));
                else
                    character.RedOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                switch (e.Match.Groups[3].Value) {
                    case "HP":
                        character.BaseHP           += int.Parse(e.Match.Groups[2].Value); break;
                    case "TP":
                        character.BaseTP           += int.Parse(e.Match.Groups[2].Value); break;
                    case "IG":
                        character.IgnitionCapacity += int.Parse(e.Match.Groups[2].Value); break;
                    case "STR":
                        character.BaseSTR          += int.Parse(e.Match.Groups[2].Value); break;
                    case "DEF":
                        character.BaseDEF          += int.Parse(e.Match.Groups[2].Value); break;
                    case "INT":
                        character.BaseINT          += int.Parse(e.Match.Groups[2].Value); break;
                    case "SPD":
                        character.BaseSPD          += int.Parse(e.Match.Groups[2].Value); break;
                }
            }
        }

        /* This is ambiguous between weapons, styles and ignitions.
        [ArenaRegex(@"^\x033You spend\x02 ([\d,]*) \x02black orb\(?s?\)? to purchase\x02 ([^ ]*)\x02!(?: \x033You have\x02 ([\d,]+) \x02black orbs? \x02left)?")]
        internal void OnOrbSpendWeapon(object sender, RegexEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Connection.Nickname, out character)) {
                if (e.Match.Groups[3].Success)
                    character.BlackOrbs = int.Parse(e.Match.Groups[3].Value.Replace(",", ""));
                else
                    character.BlackOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                if (character.Weapons == null)
                    character.Weapons = new Dictionary<string, int> { { e.Match.Groups[2].Value, 1 } };
                character.Weapons[e.Match.Groups[2].Value] = 1;
            }
        }
         */

        [ArenaRegex(@"^\x033You spend\x02 ([\d,]*) \x02.* to upgrade your\x02 ([^ ]*)\x02!(?: \x033You have\x02 ([\d,]+) \x02.* left)?")]
        internal void OnOrbSpendUpgrades(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[3].Success)
                    character.RedOrbs = int.Parse(e.Match.Groups[3].Value.Replace(",", ""));
                else
                    character.RedOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));

                if (character.Weapons == null)
                    character.Weapons = new Dictionary<string, int> { { e.Match.Groups[2].Value, 1 } };
                else if (!character.Weapons.ContainsKey(e.Match.Groups[2].Value))
                    character.Weapons.Add(e.Match.Groups[2].Value, 1);
            }
        }

        [ArenaRegex(@"^\x033You spend\x02 ([\d,]*) \x02black orb\(?s?\)? for \x02([\d,]*) .*!(?: \x033You have\x02 ([\d,]+) \x02black orbs? \x02left)?")]
        internal void OnOrbSpendOrbs(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(e.Client.Me.Nickname, out character)) {
                if (e.Match.Groups[3].Success)
                    character.BlackOrbs = int.Parse(e.Match.Groups[3].Value.Replace(",", ""));
                else
                    character.BlackOrbs -= int.Parse(e.Match.Groups[1].Value.Replace(",", ""));
                character.RedOrbs += int.Parse(e.Match.Groups[2].Value.Replace(",", ""));
            }
        }
#endregion

        [ArenaRegex(@"^\x033\x02Battle Chat\x02 has been (?:(enabled)|disabled)\.")]
        internal void OnDCCModeToggle(object sender, TriggerEventArgs e) {
            this.DCCBattleChat = e.Match.Groups[1].Success;
            if (!this.DCCBattleChat) ((DccClient) this.DccClient).SendSub("!toggle battle chat");
        }

        [ArenaRegex(@"^\x034The \x02Ai System\x02 has been turned (off|on)\.")]
        internal void OnAIToggle(object sender, TriggerEventArgs e) {
            if (this.IsAdminChecking && (DateTime.Now - this.IsAdminChecked) < TimeSpan.FromSeconds(15)) {
                this.IsAdmin = true;
                this.IsAdminChecking = false;
                this.IsAdminCheckTimer.Interval = 21600000;  // 8 hours
                this.IsAdminCheckTimer.Start();
                this.BattleAction(true, "!toggle AI system");
                this.WriteLine(2, 7, "I'm a bot admin.");
            }
        }

        private void IsAdminCheckTimer_Elapsed(object sender, ElapsedEventArgs e) {
            if (this.IsAdminChecking) {
                this.IsAdmin = false;
                this.IsAdminChecking = false;
                this.IsAdminCheckTimer.Interval = 21600000;  // 8 hours
                this.IsAdminCheckTimer.Start();
                this.WriteLine(2, 7, "I'm not a bot admin.");
            } else {
                this.CheckAdmin();
            }
        }

        public void CheckAdmin() {
            // Check for admin status.
            this.IsAdminChecking = true;
            this.IsAdminChecked = DateTime.Now;
            this.BattleAction(true, "!toggle AI system");
            this.IsAdminCheckTimer.Interval = 30000;
            this.IsAdminCheckTimer.Start();
        }

        [ArenaRegex(@"^\x0310\x02([^\x02]*) \x02(.*)")]
        internal async void OnIdentify(object sender, TriggerEventArgs e) {
            // Boost actions also match this pattern.
            // TODO: if (this.Version >= new ArenaVersion("3.1_beta101415")) return;
            if (this.Turn != null && this.BattleList[this.Turn].Name == e.Match.Groups[1].Value)
                return;

            if (this.LoggedIn == null) {
                Character character;
                if ((this.Characters.TryGetValue(e.Client.Me.Nickname, out character) && character.Name == e.Match.Groups[1].Value) ||
                    (character == default(Character) && e.Match.Groups[1].Value == e.Client.Me.Nickname)) {
                    this.LoggedIn = e.Client.Me.Nickname;
                    this.WriteLine(2, 7, "Logged in successfully.");

                    if (character == default(Character))
                        this.Characters.Add(e.Client.Me.Nickname, new Character() {
                            ShortName = e.Client.Me.Nickname, Name = e.Match.Groups[1].Value, Category = Category.Player
                        });

                    this.IsAdmin = false;
                    this.CheckAdmin();

					try {
						await this.GetAbilitiesAsync(this.LoggedIn);
					} catch (Exception ex) {
						this.WriteLine(2, 4, "Failed to check my attributes: {0}", ex.Message);
					}
                }
            }
        }

#region !view-info
        [ArenaRegex(@"\[\x034Name\x0312 ([^]]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Weapon Type\x0312 ([^]]*)(?:\x03\d{0,2}|\x0F)\] (?:\[\x034Weapon Size\x0312 (?:(small)|(medium)|(large))(?:\x03\d{0,2}|\x0F)\] )?\[\x034# of Hits ?\x0312 ([^]]*)(?:\x03\d{0,2}|\x0F)\]")]
        internal void OnViewInfoWeapon1(object sender, TriggerEventArgs e) {
            if (this.ViewingWeapon == null)
                this.ViewingWeapon = new Weapon();
            else if (this.ViewingWeapon.Name != null) {
                this.ViewingWeapon = new Weapon();
            }

            this.ViewingWeapon.Name = e.Match.Groups[1].Value;
            this.ViewingWeapon.Type = e.Match.Groups[2].Value;

            if (short.TryParse(e.Match.Groups[6].Value, out this.ViewingWeapon.HitsMax)) {
                this.ViewingWeapon.HitsMin = this.ViewingWeapon.HitsMax;
            } else {
                Match match = Regex.Match(e.Match.Groups[6].Value, @"random\(\s*(\d+)\s*-\s*(\d+)\s*\)", RegexOptions.IgnoreCase);
                if (match.Success) {
                    this.ViewingWeapon.HitsMin = short.Parse(match.Groups[1].Value);
                    this.ViewingWeapon.HitsMax = short.Parse(match.Groups[2].Value);
                    if (this.ViewingWeapon.HitsMin > this.ViewingWeapon.HitsMax) {
                        short s = this.ViewingWeapon.HitsMin;
                        this.ViewingWeapon.HitsMin = this.ViewingWeapon.HitsMax;
                        this.ViewingWeapon.HitsMax = s;
                    }
                } else {
                    this.ViewingWeapon.HitsMax = 0;
                    this.ViewingWeapon.HitsMin = 0;
                }
            }

            if (e.Match.Groups[3].Success)
                this.ViewingWeapon.Size = Size.Small;
            else if (e.Match.Groups[4].Success)
                this.ViewingWeapon.Size = Size.Medium;
            else if (e.Match.Groups[5].Success)
                this.ViewingWeapon.Size = Size.Large;

            this.Weapons[this.ViewingWeapon.Name] = this.ViewingWeapon;
            this.WriteLine(2, 10, string.Format("Registered weapon from !view-info: {0} ({1})", this.ViewingWeapon.Name, this.ViewingWeapon.Type));
            this.viewInfoWeaponCheck();
        }

        [ArenaRegex(@"\[\x034Base Power\x0312 (\d*)(?:\x03\d{0,2}|\x0F)\] \[\x034Cost\x0312 (\d*) black orb\(?s?\)?(?:\x03\d{0,2}|\x0F)\] \[\x034Element of Weapon\x0312 ([^\x03\x0F\]]*)(?:\x03\d{0,2}|\x0F)\](?: \[\x034Is the weapon 2 Handed\?\x0312 (?:(yes)|(no))\x034\])?")]
        internal void OnViewInfoWeapon2(object sender, TriggerEventArgs e) {
            if (this.ViewingWeapon == null)
                this.ViewingWeapon = new Weapon();
            else if (this.ViewingWeapon.Power != -1) {
                this.ViewingWeapon = new Weapon();
            }

            this.ViewingWeapon.Power = int.Parse(e.Match.Groups[1].Value);
            this.ViewingWeapon.Cost = int.Parse(e.Match.Groups[2].Value);
            this.ViewingWeapon.Element = e.Match.Groups[3].Value;
            this.ViewingWeapon.IsTwoHanded = !e.Match.Groups[5].Success;

            this.WriteLine(2, 10, string.Format("Registered weapon info for {0} from !view-info  Power: {1}  Cost: {2}  Element: {3}  Two-handed: {4}", this.ViewingWeapon.Name ?? "*", this.ViewingWeapon.Power, this.ViewingWeapon.Cost, this.ViewingWeapon.Element, this.ViewingWeapon.IsTwoHanded ? "yes" : "no"));
            this.viewInfoWeaponCheck();
        }

        [ArenaRegex(@"\[\x034Abilities of the Weapon\x0312 ([^, ]+(?:, [^, ]+)*)?(?:\x03\d{0,2}|\x0F)\]")]
        internal void OnViewInfoWeapon3(object sender, TriggerEventArgs e) {
            if (this.ViewingWeapon == null)
                this.ViewingWeapon = new Weapon();
            else if (this.ViewingWeapon.Techniques != null) {
                this.ViewingWeapon = new Weapon();
            }

            this.ViewingWeapon.Techniques = new List<string>(e.Match.Groups[1].Value.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries));

            this.WriteLine(2, 10, string.Format("Registered technique list for {0} from !view-info: {1}", this.ViewingWeapon.Name, string.Join(", ", this.ViewingWeapon.Techniques)));
            this.viewInfoWeaponCheck();
        }

        public void viewInfoWeaponCheck() {
            if (this.ViewingWeapon.Name != null && this.ViewingWeapon.Power != -1 && this.ViewingWeapon.Techniques != null) {
                this.ViewingWeapon.IsWellKnown = true;
                thingInfoComplete("weapon/" + this.ViewingWeapon.Name);
                this.ViewingWeapon = null;
            }
        }

        [ArenaRegex(@"^\[\x034Name\x0312 ([^ \]]*)(?:\x03\d{0,2}|\x0F)\] \[\x034Target Type\x0312 (?i:(Single|Status)|(AoE)|(Heal)|(Heal-AoE)|(Suicide)|(Suicide-AoE)|(StealPower)|(Boost)|(FinalGetsuga)|(Buff)|ClearStatus(?:(Negative)|(Positive)))(?:\x03\d{0,2}|\x0F)\] \[\x034TP needed to use\x0312 (\d+)(?:\x03\d{0,2}|\x0F)\](?: \[\x034# of Hits\x0312 ([^\]]*)(?:\x03\d{0,2}|\x0F)\])?(?: \[\x034Stats Type\x0312 ([^\]]*)(?:\x03\d{0,2}|\x0F)\])?( \[\x034Magic\x0312 Yes(?:\x03\d{0,2}|\x0F)\])?(?: \[\x034Ignore Target Defense by\x0312 ([^\]]*)%(?:\x03\d{0,2}|\x0F)\])?")]
        internal void OnViewInfoTechnique1(object sender, TriggerEventArgs e) {
            if (this.ViewingTechnique == null)
                this.ViewingTechnique = new Technique();
            else if (this.ViewingTechnique.Name != null) {
                this.ViewingTechnique = new Technique();
            }

            this.ViewingTechnique.Name = e.Match.Groups[1].Value;

            if (e.Match.Groups[2].Success)
                this.ViewingTechnique.Type = TechniqueType.Attack;
            else if (e.Match.Groups[3].Success)
                this.ViewingTechnique.Type = TechniqueType.AoEAttack;
            else if (e.Match.Groups[4].Success)
                this.ViewingTechnique.Type = TechniqueType.Heal;
            else if (e.Match.Groups[5].Success)
                this.ViewingTechnique.Type = TechniqueType.AoEHeal;
            else if (e.Match.Groups[6].Success)
                this.ViewingTechnique.Type = TechniqueType.Suicide;
            else if (e.Match.Groups[7].Success)
                this.ViewingTechnique.Type = TechniqueType.AoESuicide;
            else if (e.Match.Groups[8].Success)
                this.ViewingTechnique.Type = TechniqueType.StealPower;
            else if (e.Match.Groups[9].Success)
                this.ViewingTechnique.Type = TechniqueType.Boost;
            else if (e.Match.Groups[10].Success)
                this.ViewingTechnique.Type = TechniqueType.FinalGetsuga;
            else if (e.Match.Groups[11].Success)
                this.ViewingTechnique.Type = TechniqueType.Buff;
            else if (e.Match.Groups[12].Success)
                this.ViewingTechnique.Type = TechniqueType.ClearStatusNegative;
            else if (e.Match.Groups[13].Success)
                this.ViewingTechnique.Type = TechniqueType.ClearStatusPositive;
            else
                this.ViewingTechnique.Type = TechniqueType.Unknown;

            if (e.Match.Groups[16].Success)
                this.ViewingTechnique.Status = e.Match.Groups[16].Value;
            else
                this.ViewingTechnique.Status = "None";

            this.ViewingTechnique.TP = int.Parse(e.Match.Groups[14].Value);
            this.ViewingTechnique.IsMagic = e.Match.Groups[17].Success;

            if (e.Match.Groups[15].Success)
                this.ViewingTechnique.Hits = short.Parse(e.Match.Groups[15].Value);
            else
                this.ViewingTechnique.Hits = 1;

            this.Techniques[this.ViewingTechnique.Name] = this.ViewingTechnique;
            this.WriteLine(2, 10, string.Format("Registered technique from !view-info: {0} ({1}  TP cost: {2}  Effect: {3}  Magic: {4})", this.ViewingTechnique.Name, this.ViewingTechnique.Type, this.ViewingTechnique.TP, this.ViewingTechnique.Status, this.ViewingTechnique.IsMagic ? "Yes" : "No"));
            this.viewInfoTechniqueCheck();
        }

        [ArenaRegex(@"^\[\x034Base Power\x0312 (\d*)(?:\x03\d{0,2}|\x0F)\] \[\x034Base Cost \(before Shop Level\)\x0312 (\d+) [^\]]*(?:\x03\d{0,2}|\x0F)\] \[\x034Element of Tech\x0312 ([^\]]*)(?:\x03\d{0,2}|\x0F)\](?: \[\x034Stat Modifier\x0312 (?i:(STR)|(DEF)|(INT)|(SPD)|(HP)|(TP)|(IgnitionGauge))(?:\x03\d{0,2}|\x0F)\])?")]
        internal void OnViewInfoTechnique2(object sender, TriggerEventArgs e) {
            if (this.ViewingTechnique == null)
                this.ViewingTechnique = new Technique();
            else if (this.ViewingTechnique.Element != null) {
                this.ViewingTechnique = new Technique();
            }

            int.TryParse(e.Match.Groups[1].Value, out this.ViewingTechnique.Power);
            this.ViewingTechnique.Cost = int.Parse(e.Match.Groups[2].Value);
            this.ViewingTechnique.Element = e.Match.Groups[3].Value;
            this.ViewingTechnique.UsesINT = e.Match.Groups[6].Success;

            this.WriteLine(2, 10, string.Format("Registered technique info for {0} from !view-info  Power: {1}  Cost: {2}  Element: {3}  Attribute: {4}", this.ViewingTechnique.Name ?? "*", this.ViewingTechnique.Power, this.ViewingTechnique.Cost, this.ViewingTechnique.Element, this.ViewingTechnique.UsesINT ? "INT" : "STR"));
            this.viewInfoTechniqueCheck();
        }

        public void viewInfoTechniqueCheck() {
            if (this.ViewingTechnique.Name != null && (this.ViewingTechnique.Type == TechniqueType.Buff || this.ViewingTechnique.Element != null)) {
                this.ViewingTechnique.IsWellKnown = true;
                thingInfoComplete("technique/" + this.ViewingTechnique.Name);
                this.ViewingTechnique = null;
            }
        }

        [ArenaRegex(@"^\x034\x02(Error:\x02 )?Invalid (weapon|technique|item|skill|ignition)$")]
        internal void OnViewInfoInvalid(object sender, TriggerEventArgs e) {
            ++this.RepeatCommand;
        }

        [ArenaRegex(@"^You analyze (.*?) and determine (?:(he)|(she)|(it)|\w+) (?:has|have) (\d*) HP (?:and (\d*) TP )?left\.", true)]
        internal void OnAnalysis1(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.Name != null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            this.ViewingCharacter.Name = e.Match.Groups[1].Value;
            this.ViewingCombatant.Name = e.Match.Groups[1].Value;

            if (e.Match.Groups[2].Success)
                this.ViewingCharacter.Gender = Gender.Male;
            else if (e.Match.Groups[3].Success)
                this.ViewingCharacter.Gender = Gender.Female;
            else if (e.Match.Groups[4].Success)
                this.ViewingCharacter.Gender = Gender.None;
            else
                this.ViewingCharacter.Gender = Gender.Unknown;

            this.ViewingCombatant.HP = int.Parse(e.Match.Groups[5].Value);
            if (e.Match.Groups[6].Success) {
                this.ViewingCombatant.TP = int.Parse(e.Match.Groups[6].Value);
                this.WriteLine(2, 10, string.Format("{0} has {1} HP and {2} TP.", this.ViewingCharacter.Name ?? "*", this.ViewingCombatant.HP, this.ViewingCombatant.TP));
            } else {
                this.WriteLine(2, 10, string.Format("{0} has {1} HP.", this.ViewingCharacter.Name ?? "*", this.ViewingCombatant.HP));
            }

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^You also determine (.*?) has the following stats: \[str: (\d*)\] \[def: (\d*)\] \[int: (\d*)\] \[spd: (\d*)\]", true)]
        internal void OnAnalysis2Old(object sender, TriggerEventArgs e) {
            Analysis2(
                e.Match.Groups[1].Value,
                int.Parse(e.Match.Groups[2].Value),
                int.Parse(e.Match.Groups[3].Value),
                int.Parse(e.Match.Groups[4].Value),
                int.Parse(e.Match.Groups[5].Value));
        }
        [ArenaRegex(@"^\x033(.*?) has the following stats: \[str: \x033\x02(\d+)\x0F\x033\] \[def: \x033\x02(\d+)\x0F\x033\] \[int: \x033\x02(\d+)\x0F\x033\] \[spd: \x033\x02(\d+)\x0F\x033\]")]
        internal void OnAnalysis2New(object sender, TriggerEventArgs e) {
            Analysis2(
                e.Match.Groups[1].Value,
                int.Parse(e.Match.Groups[2].Value),
                int.Parse(e.Match.Groups[3].Value),
                int.Parse(e.Match.Groups[4].Value),
                int.Parse(e.Match.Groups[5].Value));
        }

        internal void Analysis2(string name, int str, int def, int mag, int spd) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCombatant.STR != int.MinValue ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != name)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            this.ViewingCombatant.STR = str;
            this.ViewingCombatant.DEF = def;
            this.ViewingCombatant.INT = mag;
            this.ViewingCombatant.SPD = spd;

            this.WriteLine(2, 10, string.Format("{0} has {1} STR, {2} DEF, {3} INT, {4} SPD.", this.ViewingCharacter.Name ?? "*", this.ViewingCombatant.STR, this.ViewingCombatant.DEF, this.ViewingCombatant.INT, this.ViewingCombatant.SPD));

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.*?) is also (?:resistant|strong) against the following weapon types:\x02 (?:none|([^\x02]*)) \x02and is (?:resistant|strong) against the following elements:\x02 (?:none|([^\x02]*)(?!\x02\|))( \x02\| \1 is weak against the following weapon types:\x02 (?:none|([^\x02]*)) \x02and weak against the following elements:\x02 (?:none|([^\x02]*)))?")]
        internal void OnAnalysis3Old(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalResistances != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[2].Success)
                this.ViewingCharacter.WeaponResistances = new List<string>(e.Match.Groups[2].Value.Split(new string[] { ", " }, StringSplitOptions.None));
            else
                this.ViewingCharacter.WeaponResistances = new List<string>();

            if (e.Match.Groups[3].Success)
                this.ViewingCharacter.ElementalResistances = new List<string>(e.Match.Groups[3].Value.Split(new string[] { ", " }, StringSplitOptions.None));
            else
                this.ViewingCharacter.ElementalResistances = new List<string>();


            this.WriteLine(2, 10, string.Format("{0} is resistant to {1} and {2}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.WeaponResistances), string.Join(", ", this.ViewingCharacter.ElementalResistances)));
            if (e.Match.Groups[4].Success) {
                if (e.Match.Groups[5].Success)
                    this.ViewingCharacter.WeaponWeaknesses = new List<string>(e.Match.Groups[5].Value.Split(new string[] { ", " }, StringSplitOptions.None));
                else
                    this.ViewingCharacter.WeaponWeaknesses = new List<string>();

                if (e.Match.Groups[6].Success)
                    this.ViewingCharacter.ElementalWeaknesses = new List<string>(e.Match.Groups[6].Value.Split(new string[] { ", " }, StringSplitOptions.None));
                else
                    this.ViewingCharacter.ElementalWeaknesses = new List<string>();

                this.WriteLine(2, 10, string.Format("{0} is weak to {1} and {2}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.WeaponWeaknesses), string.Join(", ", this.ViewingCharacter.ElementalWeaknesses)));
            }

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.*?) is completely immune to the following elements:\x02 (?:none|(.*))")]
        internal void OnAnalysis4Old(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalImmunities != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[2].Success) {
                this.ViewingCharacter.ElementalImmunities = new List<string>(e.Match.Groups[2].Value.Split(new string[] { ", " }, StringSplitOptions.None));
                this.WriteLine(2, 10, string.Format("{0} is immune to {1}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.ElementalImmunities)));
            } else {
                this.ViewingCharacter.ElementalImmunities = new List<string>();
                this.WriteLine(2, 10, string.Format("{0} has no immunities.", this.ViewingCharacter.Name ?? "*"));
            }

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.*?) will (?:absorb and )?be healed by the following elements:\x02 (?:none|(.*))")]
        internal void OnAnalysis5Old(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalAbsorbs != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[2].Success) {
                this.ViewingCharacter.ElementalAbsorbs = new List<string>(e.Match.Groups[2].Value.Split(new string[] { ", " }, StringSplitOptions.None));
                this.WriteLine(2, 10, string.Format("{0} will absorb {1}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.ElementalAbsorbs)));
            } else {
                this.ViewingCharacter.ElementalAbsorbs = new List<string>();
                this.WriteLine(2, 10, string.Format("{0} absorbs no elements.", this.ViewingCharacter.Name ?? "*"));
            }

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.+?) can be hurt normally with the following weapon types: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x033\x02[^\x0F]+\x0F\x033(?:, )?)+)) and is resistant against the following weapon types: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x036\x02[^\x0F]+\x0F\x033(?:, )?)+)) and weak against the following weapon types:\x02? (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x037\x02[^\x0F]+\x0F\x033(?:, )?)+))")]
        internal void OnAnalysis3New(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalResistances != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[4].Success)
                this.ViewingCharacter.WeaponResistances = new List<string>();
            else if (e.Match.Groups[5].Success) {
                this.ViewingCharacter.WeaponResistances = new List<string>();
                this.ViewingCharacter.WeaponResistances.AddRange(IrcClient.RemoveCodes(e.Match.Groups[5].Value).Split(new string[] { ", " }, 0));
            }

            if (e.Match.Groups[6].Success)
                this.ViewingCharacter.WeaponWeaknesses = new List<string>();
            else if (e.Match.Groups[7].Success) {
                this.ViewingCharacter.WeaponWeaknesses = new List<string>();
                this.ViewingCharacter.WeaponWeaknesses.AddRange(IrcClient.RemoveCodes(e.Match.Groups[7].Value).Split(new string[] { ", " }, 0));
            }

            if (e.Match.Groups[3].Success) {
                foreach (string weaponType in IrcClient.RemoveCodes(e.Match.Groups[3].Value).Split(new string[] { ", " }, 0)) {
                    this.ViewingCharacter.WeaponResistances.RemoveAll(s => s.Equals(weaponType, StringComparison.OrdinalIgnoreCase));
                    this.ViewingCharacter.WeaponWeaknesses.RemoveAll(s => s.Equals(weaponType, StringComparison.OrdinalIgnoreCase));
                }
            }

            this.WriteLine(2, 10, string.Format("Registered {0}'s weapon modifiers.", this.ViewingCharacter.Name ?? "*"));

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.+?) is resistant against the following elements: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x036\x02[^\x0F]+\x0F\x033(?:, )?)+)) and weak against the following elements: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x037\x02[^\x0F]+\x0F\x033(?:, )?)+))")]
        internal void OnAnalysis4New(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalResistances != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[2].Success)
                this.ViewingCharacter.ElementalResistances = new List<string>();
            else if (e.Match.Groups[3].Success) {
                this.ViewingCharacter.ElementalResistances = new List<string>();
                this.ViewingCharacter.ElementalResistances.AddRange(IrcClient.RemoveCodes(e.Match.Groups[3].Value).Split(new string[] { ", " }, 0));
            }

            if (e.Match.Groups[4].Success)
                this.ViewingCharacter.ElementalWeaknesses = new List<string>();
            else if (e.Match.Groups[5].Success) {
                this.ViewingCharacter.ElementalWeaknesses = new List<string>();
                this.ViewingCharacter.ElementalWeaknesses.AddRange(IrcClient.RemoveCodes(e.Match.Groups[5].Value).Split(new string[] { ", " }, 0));
            }

            this.WriteLine(2, 10, string.Format("Registered {0}'s element modifiers.", this.ViewingCharacter.Name ?? "*"));

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.+?) is completely immune to the following elements: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x034\x02[^\x0F]+\x0F\x033(?:, )?)+))")]
        internal void OnAnalysis5New(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalImmunities != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[3].Success) {
                this.ViewingCharacter.ElementalImmunities = new List<string>(IrcClient.RemoveCodes(e.Match.Groups[3].Value).Split(new string[] { ", " }, StringSplitOptions.None));
                this.WriteLine(2, 10, string.Format("{0} is immune to {1}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.ElementalImmunities)));
            } else if (e.Match.Groups[2].Success) {
                this.ViewingCharacter.ElementalImmunities = new List<string>();
                this.WriteLine(2, 10, string.Format("{0} has no immunities.", this.ViewingCharacter.Name ?? "*"));
            }

            this.ViewInfoCharacterCheck();
        }

        [ArenaRegex(@"^\x033(.+?) will be healed by the following elements: (?:\x031,1[\[\]]*\x0F\x033|(\x033\x02none\x02)|((?:\x034\x02[^\x0F]+\x0F\x033(?:, )?)+))")]
        internal void OnAnalysis6New(object sender, TriggerEventArgs e) {
            if (this.ViewingCharacter == null) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            } else if (this.ViewingCharacter.ElementalAbsorbs != null ||
                       (this.ViewingCharacter.Name != null && this.ViewingCharacter.Name != e.Match.Groups[1].Value)) {
                this.ViewingCharacter = new Character();
                this.ViewingCombatant = new Combatant() { STR = int.MinValue };
            }

            if (e.Match.Groups[3].Success) {
                this.ViewingCharacter.ElementalAbsorbs = new List<string>(IrcClient.RemoveCodes(e.Match.Groups[3].Value).Split(new string[] { ", " }, StringSplitOptions.None));
                this.WriteLine(2, 10, string.Format("{0} will absorb {1}.", this.ViewingCharacter.Name ?? "*", string.Join(", ", this.ViewingCharacter.ElementalAbsorbs)));
            } else if (e.Match.Groups[2].Success) {
                this.ViewingCharacter.ElementalAbsorbs = new List<string>();
                this.WriteLine(2, 10, string.Format("{0} can absorb no elements.", this.ViewingCharacter.Name ?? "*"));
            }

            this.ViewInfoCharacterCheck();
        }

        public void ViewInfoCharacterCheck(bool timeout = false) {
            this.AnalysisTimer.Stop();
            if (timeout) {
                Character character; Combatant combatant;
                if (this.ViewingCharacter.Name == null) {
                    this.WriteLine(1, 4, string.Format("Error: a cached character name was not set!"));
                } else {
                    // Show the report.
                    this.BattleAction(false, "\u000311The report on \u0002" + this.ViewingCharacter.Name + "\u0002:");
                    if (this.ViewingCombatant.TP == 0)
                        this.BattleAction(false, string.Format("\u000312{0} \u0002{1}\u0002 HP left.",
                            this.ViewingCharacter.GenderRefTheyHave, this.ViewingCombatant.HP, this.ViewingCombatant.TP));
                    else
                        this.BattleAction(false, string.Format("\u000312{0} \u0002{1}\u0002 HP and \u0002{2}\u0002 TP left.",
                            this.ViewingCharacter.GenderRefTheyHave , this.ViewingCombatant.HP, this.ViewingCombatant.TP));
                    if (this.ViewingCombatant.STR != int.MinValue) {
                        this.BattleAction(false, string.Format("\u000312{0} \u0002{1}\u0002 strength, \u0002{2}\u0002 defense, \u0002{3}\u0002 magical power, \u0002{4}\u0002 speed.",
                            this.ViewingCharacter.GenderRefTheyHave, this.ViewingCombatant.STR, this.ViewingCombatant.DEF, this.ViewingCombatant.INT, this.ViewingCombatant.SPD));
                        if (this.ViewingCharacter.ElementalResistances != null) {
                            if (this.ViewingCharacter.ElementalResistances.Count == 0) {
                                if (this.ViewingCharacter.WeaponResistances.Count == 0)
                                    this.BattleAction(false, string.Format("\u000312{0} no resistances.", this.ViewingCharacter.GenderRefTheyHave));
                                else
                                    this.BattleAction(false, string.Format("\u000312{0} resistant to \u0002{1}\u0002 attacks.",
                                        this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.WeaponResistances)));
                            } else {
                                if (this.ViewingCharacter.WeaponResistances.Count == 0)
                                    this.BattleAction(false, string.Format("\u000312{0} resistant to \u0002{1}\u0002.",
                                        this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.ElementalResistances)));
                                else
                                    this.BattleAction(false, string.Format("\u000312{0} resistant to \u0002{1}\u0002, and to \u0002{2}\u0002 attacks.",
                                        this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.ElementalResistances), string.Join(", ", this.ViewingCharacter.WeaponResistances)));
                            }
                            if (this.ViewingCharacter.ElementalWeaknesses != null) {
                                if (this.ViewingCharacter.ElementalWeaknesses.Count == 0) {
                                    if (this.ViewingCharacter.WeaponWeaknesses.Count == 0)
                                        this.BattleAction(false, string.Format("\u000312{0} no weaknesses.", this.ViewingCharacter.GenderRefTheyHave));
                                    else
                                        this.BattleAction(false, string.Format("\u000312{0} weak to \u0002{1}\u0002 attacks.",
                                            this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.WeaponWeaknesses)));
                                } else {
                                    if (this.ViewingCharacter.WeaponWeaknesses.Count == 0)
                                        this.BattleAction(false, string.Format("\u000312{0} weak to \u0002{1}\u0002.",
                                            this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.ElementalWeaknesses)));
                                    else
                                        this.BattleAction(false, string.Format("\u000312{0} weak to \u0002{1}\u0002, and to \u0002{2}\u0002 attacks.",
                                            this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.ElementalWeaknesses), string.Join(", ", this.ViewingCharacter.WeaponWeaknesses)));
                                }
                            }
                            if (this.ViewingCharacter.ElementalImmunities != null && this.ViewingCharacter.ElementalImmunities.Count != 0) {
                                this.BattleAction(false, string.Format("\u000312{0} immune to \u0002{1}\u0002.",
                                    this.ViewingCharacter.GenderRefTheyAre, string.Join(", ", this.ViewingCharacter.ElementalImmunities)));

                                if (this.ViewingCharacter.ElementalAbsorbs != null && this.ViewingCharacter.ElementalAbsorbs.Count != 0) {
                                    this.BattleAction(false, string.Format("\u000312{0} can absorb to \u0002{1}\u0002.",
                                        this.ViewingCharacter.GenderRefThey, string.Join(", ", this.ViewingCharacter.ElementalAbsorbs)));
                                }
                            }
                        }
                    }

                    // Register this character.
                    character = this.GetCharacter(this.ViewingCharacter.Name);
                    character.Gender = this.ViewingCharacter.Gender;
                    if (this.ViewingCharacter.ElementalResistances != null) {
                        character.WeaponResistances = this.ViewingCharacter.WeaponResistances;
                        character.ElementalResistances = this.ViewingCharacter.ElementalResistances;
                        if (this.ViewingCharacter.ElementalWeaknesses != null) {
                            character.WeaponWeaknesses = this.ViewingCharacter.WeaponWeaknesses;
                            character.ElementalWeaknesses = this.ViewingCharacter.ElementalWeaknesses;
                            if (this.ViewingCharacter.ElementalImmunities != null) {
                                character.ElementalImmunities = this.ViewingCharacter.ElementalImmunities;
                                if (this.ViewingCharacter.ElementalAbsorbs != null) {
                                    character.ElementalAbsorbs = this.ViewingCharacter.ElementalAbsorbs;
                                }
                            }
                        }
                    }
                    if (this.BattleList.TryGetValue(character.ShortName, out combatant)) {
                        combatant.HP = this.ViewingCombatant.HP;
                        if (this.ViewingCombatant.TP != 0) {
                            combatant.TP = this.ViewingCombatant.TP;
                            if (this.ViewingCombatant.STR != int.MinValue) {
                                combatant.STR = this.ViewingCombatant.STR;
                                combatant.DEF = this.ViewingCombatant.DEF;
                                combatant.INT = this.ViewingCombatant.INT;
                                combatant.SPD = this.ViewingCombatant.SPD;
                            }
                        }
                    }
                    character.IsWellKnown = true;
                    this.WriteLine(2, 10, string.Format("Registered data for {0} ({1}).", this.ViewingCharacter.Name ?? "*", character.ShortName));
                }
                this.ViewingCharacter = null;
                this.ViewingCombatant = null;
            } else {
                int analysisLevel; Character me;
                if (this.Characters.TryGetValue(this.LoggedIn, out me)) {
                    if (!me.Skills.TryGetValue("Analysis", out analysisLevel))
                        analysisLevel = 0;
                } else
                    analysisLevel = 0;

                if (this.ViewingCharacter.Name != null &&
                    (analysisLevel < 3 || (this.ViewingCombatant.STR != int.MinValue &&
                     (analysisLevel < 4 || (this.ViewingCharacter.ElementalResistances != null &&
                      (analysisLevel < 5 || (this.ViewingCharacter.ElementalImmunities != null &&
                       (analysisLevel < 6 || this.ViewingCharacter.ElementalAbsorbs != null)))))))) {
                    this.AnalysisTimer.Interval = 1500;
                    this.AnalysisTimer.Start();
                } else {
                    this.AnalysisTimer.Interval = 10000;
                    this.AnalysisTimer.Start();
                }
            }
        }
#endregion

#region Battle preparation
        [ArenaRegex(@"^\x034(?:(A dimensional portal has been detected\. The enemy force will arrive)|(A powerful dimensional rift has been detected\. The enemy force will arrive)|(The Allied Forces have detected an orb fountain! The party will be sent to destroy it)|(The Allied Forces have opened the coliseum to allow players to fight one another. The PVP battle will begin)|(A Manual battle has been started. Bot Admins will need to add monsters, npcs and bosses individually\. The battle will begin)|(An outpost of the Allied Forces HQ \x02is under attack\x02! Reinforcements are requested immediately! The reinforcements will depart)|(The Allied Forces are putting together an assault team to take out an enemy outpost\. If you wish to be a part of this team, type \x02!enter\x02 to join\. The reinforcements will depart)) in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\.(?: (?:Players \S+ )?[Tt]ype \x02!enter\x02 (?:if you wish to join the battle|if they wish to join the battle|to join))?(?:! \x032\[Current Battle Level: ([-\d]+)\])?")]
        internal void OnBattleOpenNormal(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[8].Success) time += float.Parse(e.Match.Groups[8].Value) * 60f;
            if (e.Match.Groups[9].Success) time += float.Parse(e.Match.Groups[9].Value);

            if (e.Match.Groups[10].Success) this.Level = int.Parse(e.Match.Groups[10].Value);

            if (e.Match.Groups[2].Success)
                this.OnBattleOpen(BattleType.Boss, time);
            else if (e.Match.Groups[3].Success)
                this.OnBattleOpen(BattleType.OrbFountain, time);
            else if (e.Match.Groups[4].Success)
                this.OnBattleOpen(BattleType.PvP, time);
            else if (e.Match.Groups[6].Success)
                this.OnBattleOpen(BattleType.Siege, time);
            else if (e.Match.Groups[7].Success)
                this.OnBattleOpen(BattleType.Assault, time);
            else
                this.OnBattleOpen(BattleType.Normal, time);
        }

        [ArenaRegex(@"^\x034The doors to the \x02gauntlet\x02 are open\. Anyone willing to brave the gauntlet has(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)? to enter before the doors close\. Type \x02!enter\x02 if you wish to join the battle!")]
        internal void OnBattleOpenGauntlet(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[1].Success) time += float.Parse(e.Match.Groups[1].Value) * 60f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value);

            this.OnBattleOpen(BattleType.Gauntlet, time);
        }

        [ArenaRegex(@"^\x0314\x02The President of the Allied Forces\x02 has been \x02kidnapped by monsters\x02! Are you a bad enough dude to save the president\? \x034The rescue party will depart in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\. Type \x02!enter\x02 if you wish to join the battle!")]
        internal void OnBattleOpenPresident(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[1].Success) time += float.Parse(e.Match.Groups[1].Value) * 60f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value);

            this.OnBattleOpen(BattleType.President, time);
        }

        [ArenaRegex(@"^\x034An \x02evil treasure chest Mimic\x02 is ready to fight\S? The battle will begin in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\. Type \x02!enter\x02 if you wish to join the battle!")]
        internal void OnBattleOpenMimic(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[1].Success) time += float.Parse(e.Match.Groups[1].Value) * 60f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value);

            this.OnBattleOpen(BattleType.Mimic, time);
        }

        [ArenaRegex(@"\x034A \x021 vs 1 AI Match\x02 is about to begin! The battle will begin in(?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\.")]
        internal void OnBattleOpenNPC(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[1].Success) time += float.Parse(e.Match.Groups[1].Value) * 60f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value);

            this.OnBattleOpen(BattleType.NPC, time);
        }

        [ArenaRegex(@"^\x0312\x02[^\x03]* \x02\x034has discovered the lair of the fearsome dragon known as\x0312\x02 [^\x03]+\x034\x02! A raiding party has been set up to put an end to the beast\. To join the party type \x0312\x02!enter\x034\x02 now! The raiding party will depart in\x0312\x02 (?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)?\x034\x02\. \[This is a level\x0312\x02 (\d+) \x034\x02battle\. Anyone more than 5 levels over will be level synced\.]")]
        internal void OnBattleOpenDragon(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[1].Success) time += float.Parse(e.Match.Groups[1].Value) * 60f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value);

            this.OnBattleOpen(BattleType.DragonHunt, time);
        }

        [ArenaRegex(@"^\x034\x02[^\x02]*\x02's Torment Orb shatters and creates a large demonic-looking red portal\. All brave souls level\x02\x0312 ([\d.]+) \x02\x034or higher should use\x02\x0312 !enter \x02\x034now to enter this portal if they so wish.  The portal will close in \x0312\x02 (?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)")]
        internal void OnBattleOpenTorment(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[2].Success) time += float.Parse(e.Match.Groups[2].Value) * 60f;
            if (e.Match.Groups[3].Success) time += float.Parse(e.Match.Groups[3].Value);

            this.OnBattleOpen(BattleType.Torment, time);
        }

        [ArenaRegex(@"^\x034This dungeon is level\x0312\x02 (\d+) \x034\x02and requires\x0312\x02 (\d+) players \x034\x02to enter\. The dungeon will begin in\x0312\x02 (?: (\d+(?:\.\d+)?) ?min(?:ute)?\(?s?\)?)?(?: (\d+(?:\.\d+)?) ?sec(?:ond)?\(?s?\)?)\x034\x02. Use\x02 !enter\x02 if you wish to join the party\.")]
        internal void OnBattleOpenDungeon(object sender, TriggerEventArgs e) {
            float time = 0f;
            if (e.Match.Groups[3].Success) time += float.Parse(e.Match.Groups[3].Value) * 60f;
            if (e.Match.Groups[4].Success) time += float.Parse(e.Match.Groups[4].Value);

            this.OnBattleOpen(BattleType.Dungeon, time);
        }

        internal void OnBattleOpen(BattleType type, float time) {
            this.WriteLine(1, 8, "A battle is starting. Type is {0}.", type);

            this.BattleOpen = true;
            this.BattleStarted = false;
            this.BattleType = type;
            this.BattleList.Clear();
            this.UnmatchedFullNames.Clear();
            this.UnmatchedShortNames.Clear();
            this.TurnNumber = 0;
            this.Turn = null;

            var e = new BattleOpenEventArgs(type, TimeSpan.FromSeconds(time), (EnableParticipation && type != BattleType.NPC && this.LoggedIn != null));
            this.eBattleOpen?.Invoke(this, e);

            if (e.Enter) {
                // Enter the battle.
                if (this.EnableParticipation && this.MinPlayers <= 0) {
                    Character character;
                    if (this.Characters.TryGetValue(this.LoggedIn, out character) && character.IsReadyToControl) {
                        Thread thread = new Thread(delegate() {
                            this.Entering = true;
                            Thread.Sleep(this.RNG.Next(1500, 4500));
                            this.BattleAction(false, "!enter");
                        });
                        thread.Start();
                    }
                }
            }
        }

        [ArenaRegex(@"^\x032The betting period is now\x02 open")]
        internal void OnBettingPeriodOpen(object sender, TriggerEventArgs e) {
            Character character;

            this.WriteLine(1, 8, "Betting is now open.");
            if (!this.EnableGambling) return;
            if (!this.Characters.TryGetValue(this.LoggedIn, out character) || character.DoubleDollars < 10)
                return;

            Character ally = null; Character monster = null;
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.Category == Category.Ally)
                    ally = this.Characters[combatant.ShortName];
                else if (combatant.Category == Category.Monster)
                    monster = this.Characters[combatant.ShortName];
            }

            this.WriteLine(1, 12, string.Format("Combatants:  {0} [{1}]  vs.  {2} [{3}]", ally.Name, ally.Rating, monster.Name, monster.Rating));
            Thread.Sleep(this.RNG.Next(5000, 15000));

            // Place a bet based on the ratings.
            if (ally.Rating >= monster.Rating) {
                this.BetAmount = 10;
                this.BetOnAlly = true;
                this.WriteLine(1, 12, string.Format("Betting $${0} on {1}.", this.BetAmount, ally.Name));
                this.BattleAction(true, string.Format("!bet NPC {0}", this.BetAmount));
            } else {
                this.BetAmount = 10;
                this.BetOnAlly = false;
                this.WriteLine(1, 12, string.Format("Betting $${0} on {1}.", this.BetAmount, monster.Name));
                this.BattleAction(true, string.Format("!bet monster {0}", this.BetAmount));
            }
        }

        [ArenaRegex(@"^\x032The betting period is now\x02 closed")]
        internal void OnBettingPeriodClose(object sender, TriggerEventArgs e) {
            this.BattleOpen = false;
            this.WriteLine(1, 8, "Betting is now closed.");
        }

        [ArenaRegex(new string[] {
            @"^\x034\x02([^\x02]*) \x02has entered the battle!",
            @"^\x02\x034([^\x02]*) \x02has entered the battle( to help the forces of good)?!"
        })]
        internal void OnEntry(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;

            // See if we've already registered this character.
            Character character = null; string found = null;
            foreach (Character _character in this.Characters.Values) {
                if (_character.Name == e.Match.Groups[1].Value && !this.BattleList.ContainsKey(_character.ShortName) &&
                    (!e.Match.Groups[2].Success || (_character.Category & Category.Ally) != 0)) {
                        character = _character;
                        if (found != null) {
                            found = ".";
                            break;
                        }
                        found = _character.ShortName;
                }
            }

            if (character == null) {
                // This is a new character, so we should register them now.
                this.EnteredNewCharacter(e.Match.Groups[1].Value, null, e.Match.Groups[2].Success);
            } else if (found == ".") {
                // Multiple matching characters were found (perhaps one of the two Alucards).
                this.UnmatchedFullNames.Add(new UnmatchedName() { Name = "." + e.Match.Groups[1].Value,
                                                                  Category = e.Match.Groups[2].Success ? Category.Ally : (this.BattleOpen ? (Category) 7 : Category.Monster)
                                                                });
            } else {
                this.Entered(character);
                this.EntryCheck();
            }
        }

        [ArenaRegex(@"^\x0312\x02([^\x02]*) \x02(?!gets another turn\.$)(.*)")]
        internal void OnEntryDescription(object sender, TriggerEventArgs e) {
            // If this matches on the character whose turn it is, it's probably a skill description.
            if (this.Turn != null && e.Match.Groups[1].Value == this.BattleList[this.Turn].Name) return;

            // Check if this is a known or unmatched character with no known description.
            Character character; UnmatchedName name = null;
            bool unknownDescription = true;

            character = this.GetCharacter(e.Match.Groups[1].Value, false);
            unknownDescription = (character == null || character.Description == null);

            if (unknownDescription) {
                foreach (UnmatchedName _name in this.UnmatchedFullNames) {
                    if (_name.Name == e.Match.Groups[1].Value) {
                        name = _name;
                        unknownDescription = (name.Description == null);
                        break;
                    }
                }
            }
            if (unknownDescription) {
                if (character != null) {
                    character.Description = e.Match.Groups[2].Value;
                    this.WriteLine(2, 12, "Registered {0}'s description.", character.ShortName, character.Description);
                } else if (name != null) {
                    name.Description = e.Match.Groups[2].Value;
                    this.WriteLine(2, 12, "Registered one {0}'s description.", name.Name, name.Description);
                }
            }

            // See if this is an ambiguous name due to multiple characters with the same name.
            for (int i = 0; i < this.UnmatchedFullNames.Count - 1; ++i) {
                UnmatchedName _name = this.UnmatchedFullNames[i];
                if (_name.Description == null && _name.Name == "." + e.Match.Groups[1].Value) {
                    string found = null; ;
                    foreach (Character _character in this.Characters.Values) {
                        if (_character.Name == e.Match.Groups[1].Value && (_character.Description == null || _character.Description == e.Match.Groups[2].Value)) {
                            if (found == null) {
                                found = _character.ShortName;
                                character = _character;
                            } else {
                                found = ".";
                                break;
                            }
                        }
                    }
                    if (found == null) {
                        this.UnmatchedFullNames[i].Description = e.Match.Groups[2].Value;
                    } else if (found == ".") {
                        this.UnmatchedFullNames[i].Description = e.Match.Groups[2].Value;
                    } else {
                        character.Description = e.Match.Groups[2].Value;
                        this.UnmatchedFullNames.RemoveAt(i);
                        this.Entered(character);
                    }
                }

            }

        }

        private void Entered(Character character) {
            this.WriteLine(1, 12, "{0} ({1}) enters the battle.", character.Name, character.ShortName);
            this.BattleList.Add(character.ShortName, new Combatant(character));
        }

        private void EnteredNewCharacter(string name, string description, bool ally) {
            if (this.BattleOpen) {
                // The entry phase is still open.
                // This means that the entrant can be a player, monster or ally.
                this.WriteLine(1, 12, "{0} (?) enters the battle.", name);
                UnmatchedFullNames.Add(new UnmatchedName() { Name = name, Category = (Category) 7, Description = description });
            } else {
                // The entry phase is closed.
                // This means that players can no longer enter, and things can no longer be summoned.
                if (ally || name == "Allied Forces Troop") {
                    this.WriteLine(1, 12, "An ally {0} (?) enters the battle.", name);
                    UnmatchedFullNames.Add(new UnmatchedName() { Name = name, Category = Category.Ally, Description = description });
                } else {
                    this.WriteLine(1, 12, "A monster {0} (?) enters the battle.", name);
                    UnmatchedFullNames.Add(new UnmatchedName() { Name = name, Category = Category.Monster, Description = description });
                }
            }
        }

        public void EntryCheck() {
            if (!this.BattleOpen || !this.EnableParticipation || this.Entering) return;
            int players = 0;
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.Category == Category.Player ||
                    ((combatant.Category & Category.Player) != 0 && this.ArenaConnection.Channels[this.ArenaChannel].Users.Contains(combatant.ShortName))) {
                        ++players;
                        if (players >= this.MinPlayers) {
                            Character character;
                            if (this.Characters.TryGetValue(this.LoggedIn, out character) && character.IsReadyToControl) {
                                this.Entering = true;
                                Thread thread = new Thread(delegate() {
                                    Thread.Sleep(this.RNG.Next(1500, 4500));
                                    this.BattleAction(false, "!enter");
                                });
                                thread.Start();
                            }
                        }
                }
            }
        }

        [ArenaRegex(@"^\x032You place a (\d*) double dollar bet on\x02 (.*)")]
        internal void OnBetPlaced(object sender, TriggerEventArgs e) {
            Character character;
            if (this.Characters.TryGetValue(this.LoggedIn, out character)) {
                character.DoubleDollars -= int.Parse(e.Match.Groups[1].Value);
                if (character.DoubleDollars < 0) character.DoubleDollars = 0;
            }
        }

        [ArenaRegex(@"^\x032\x02(.*) \x02looks at the heroes and says ""(.*)""")]
        internal void OnBossSpeech(object sender, TriggerEventArgs e) {
            if (this.BattleType == BattleType.Normal) this.BattleType = BattleType.Boss;
        }

        [ArenaRegex(@"^\x0310\x02The\x02 weather changes. It is now\x02 (.*)")]
        internal void OnWeather(object sender, TriggerEventArgs e) {
            // This is normally the first message that the Arena bot sends after the entry period closes.

            // Let DCC users know the battle has started.
            if (this.BattleOpen && this.DccClient != null && this.DccClient.State == IrcClientState.Online)
                ((DccClient) this.DccClient).SendSub("\u000312\u0002The battle has started.");

            this.BattleOpen = false;
            this.Weather = e.Match.Groups[1].Value;
            this.WriteLine(1, 8, $"The weather is now {this.Weather}.");
        }

        [ArenaRegex(@"^\x0310(?!\x02)\s*(?:\S+\s+)*?\x02?(?:(calm)|(bright)|(gloom)|(rain)|(storm)|(snow)|(wind)|(hot|heat)|(dry))")]
        internal void OnWeatherNew(object sender, TriggerEventArgs e) {
            // Let DCC users know the battle has started.
            if (this.BattleOpen && this.DccClient != null && this.DccClient.State == IrcClientState.Online)
                ((DccClient) this.DccClient).SendSub("\u000312\u0002The battle has started.");

            this.BattleOpen = false;

            if (e.Match.Groups[1].Success)
                this.Weather = "calm";
            else if (e.Match.Groups[2].Success)
                this.Weather = "bright";
            else if (e.Match.Groups[3].Success)
                this.Weather = "gloomy";
            else if (e.Match.Groups[4].Success)
                this.Weather = "rainy";
            else if (e.Match.Groups[5].Success)
                this.Weather = "stormy";
            else if (e.Match.Groups[6].Success)
                this.Weather = "snowy";
            else if (e.Match.Groups[7].Success)
                this.Weather = "windy";
            else if (e.Match.Groups[8].Success)
                this.Weather = "hot";
            else if (e.Match.Groups[9].Success)
                this.Weather = "dry";

            this.WriteLine(1, 8, $"The weather is now {this.Weather}.");
        }

        [ArenaRegex(@"^\x034\[Darkness will occur in:\x0312\x02 (\d*) ?\x02\x034turns\]")]
        internal void OnDarknessTime(object sender, TriggerEventArgs e) {
            this.DarknessTurns = short.Parse(e.Match.Groups[1].Value);
            this.Darkness = false;
        }

        [ArenaRegex(@"^\x034\[Darkness\x02\x0312 has overcome \x02\x034the battlefield\]")]
        internal void OnDarknessOvercome(object sender, TriggerEventArgs e) {
            this.DarknessTurns = 0;
            this.Darkness = true;
        }

        [ArenaRegex(@"\x0304\[Turn #:\x0312\x02 (\d*)\x02\x0304\] \[Weather:\x0312\x02 ([^\x03]*)\x0304\x02\] \[Moon Phase:\x0312\x02 ([^\x03]*)\x0304\x02\] \[Time of Day:\x0312\x02 ([^\x03]*)\x0304\x02\] \[Battlefield:\x0312\x02 ([^\x02]*)\x02\x0304\](?: \[Conditions:\x0312\x02 ([^\x02]*)\x02\x0304\])?")]
        internal void OnBattleInfo(object sender, TriggerEventArgs e) {
            // Check the battlefield conditions.
            if (e.Match.Groups[6].Success) {
                this.BattleConditions &= ~(BattleCondition.CurseNight | BattleCondition.BloodMoon);
                var matches = Regex.Matches(e.Match.Groups[6].Value, @"(?<=no-)([a-vx-z]+)|(?<=enhance-)([a-vx-z]+)|weatherlock", RegexOptions.IgnoreCase);
                foreach (Match match in matches) {
                    if (match.Groups[2].Success) {
                        if (match.Groups[2].Value.StartsWith("melee", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.EnhanceMelee;
                        } else if (match.Groups[2].Value.StartsWith("tech", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.EnhanceTechniques;
                        } else if (match.Groups[2].Value.StartsWith("item", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.EnhanceItems;
                        }
                    } else if (match.Groups[1].Success) {
                        if (match.Groups[2].Value.StartsWith("tech", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoTechniques;
                        } else if (match.Groups[2].Value.StartsWith("item", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.ItemLock;
                        } else if (match.Groups[2].Value.StartsWith("flee", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoFleeing;
                        } else if (match.Groups[2].Value.StartsWith("skill", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoSkills;
                        } else if (match.Groups[2].Value.StartsWith("quicksilver", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoQuicksilver;
                        } else if (match.Groups[2].Value.StartsWith("ignition", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoIgnitions;
                        } else if (match.Groups[2].Value.StartsWith("playerignition", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoPlayerIgnitions;
                        } else if (match.Groups[2].Value.StartsWith("mech", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoMech;
                        } else if (match.Groups[2].Value.StartsWith("summon", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoSummons;
                        } else if (match.Groups[2].Value.StartsWith("npc", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoAllies;
                        } else if (match.Groups[2].Value.StartsWith("trust", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoTrusts;
                        } else if (match.Groups[2].Value.StartsWith("battlefieldeffect", StringComparison.OrdinalIgnoreCase)) {
                            this.BattleConditions |= BattleCondition.NoBattlefieldEvents;
                        }
                    } else {
                        this.BattleConditions |= BattleCondition.WeatherLock;
                    }
                }
            }
        }

        [ArenaRegex(@"^\x034\[Battle Order: (.*)\x034\]")]
        internal void OnBattleList(object sender, TriggerEventArgs e) {
            // Check for the no-monster fix.
            // If there are no monsters, and the Arena bot is early enough, it cannot continue.
            if (this.BattleStarted && this.NoMonsterFix && this.BattleType != BattleType.PvP &&
                !e.Match.Groups[1].Value.Contains("\u00035") && !e.Match.Groups[1].Value.Contains("\u00036")) {
                    this.BattleAction(false, "There are no monsters on the battlefield.");
                    this.BattleAction(true, "!end battle victory");
                    return;
            }

            string[] entries = e.Match.Groups[1].Value.Split(new string[] { ", " }, StringSplitOptions.None);
            if (!this.EnableAnalysis) {
                // If analysis is off, we won't try to match names.
                // Instead, just read the list.
                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (realEntry.StartsWith("\u00033"))
                        this.BattleList.Add(shortName, new Combatant() { Category = Category.Player, HP = -1, ShortName = shortName });
                    else if (realEntry.StartsWith("\u00035") || realEntry.StartsWith("\u00036"))
                        this.BattleList.Add(shortName, new Combatant() { Category = Category.Monster, HP = -1, ShortName = shortName });
                    else if (realEntry.StartsWith("\u000312"))
                        this.BattleList.Add(shortName, new Combatant() { Category = Category.Ally, HP = -1, ShortName = shortName });
                    else if (realEntry.StartsWith("\u00034"))
                        this.BattleList.Add(shortName, new Combatant() { Category = (Category) 7, HP = 0, ShortName = shortName });
                    else
                        this.BattleList.Add(shortName, new Combatant() { Category = (Category) 7, HP = -1, ShortName = shortName });
                }
                return;
            }

            // Check for any characters that aren't listed.
            for (int i = this.BattleList.Count - 1; i >= 0; --i) {
                Combatant combatant = this.BattleList.Values.ElementAt(i);
                bool found = false;

                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (shortName == combatant.ShortName) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    this.BattleList.Remove(combatant.ShortName);
                    this.UnmatchedFullNames.Add(new UnmatchedName() { Name = combatant.ShortName, Category = (Category) 7 });
                }
            }

            if (this.BattleOpen) {
                // If the entry period is still open, the speed order hasn't been calculated yet.
                // This means that combatants are listed in the order in which they entered.
                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (this.BattleList.ContainsKey(shortName)) continue;

                    // Pick the first entry from the list of unmatched names.
                    string fullName = this.UnmatchedFullNames[0].Name.TrimStart('.');
                    Character character;
                    UnmatchedFullNames.RemoveAt(0);

                    if (this.Characters.TryGetValue("*" + fullName, out character)) {
                        Characters.Remove(character.ShortName);
                        character.ShortName = shortName;
                        this.WriteLine(2, 9, "Reregistering *{0}.", fullName);
                    } else {
                        string upperName = fullName.ToUpperInvariant();
                        character = new Character() {
                            Name = fullName, ShortName = shortName,
                            IsUndead = upperName.Contains("UNDEAD") || upperName.Contains("ZOMBIE") || upperName.Contains("GHOST") || upperName.Contains("VAMPIRE"),
                            IsElemental = upperName.Contains("ELEMENTAL"),
                            IsEthereal = upperName.Contains("GHOST"),
                            HurtByTaunt = (upperName == "CRYSTAL SHADOW WARRIOR")
                        };
                        if (realEntry.StartsWith("\u00035") || realEntry.StartsWith("\u00036"))
                            character.Category = Category.Monster;
                        else if (realEntry.StartsWith("\u00033"))
                            character.Category = Category.Player;
                        else if (realEntry.StartsWith("\u000312"))
                            character.Category = Category.Ally;

                        this.Characters.Add(character.ShortName, character);
                        this.WriteLine(2, 9, "Registered {0} to {1}.", fullName, character.ShortName);
                        this.RegisterEnteredCharacter(character);
                    }
                }
            } else if (!this.BattleStarted || this.UnmatchedFullNames.Count > 0) {
                // Try to deduce who's whom by matching the names.
                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (this.BattleList.ContainsKey(shortName)) continue;

                    if (realEntry.StartsWith("\u00035") || realEntry.StartsWith("\u00036"))
                        this.UnmatchedShortNames.Add(new UnmatchedName() { Name = shortName, Category = Category.Monster });
                    else if (realEntry.StartsWith("\u00033"))
                        this.UnmatchedShortNames.Add(new UnmatchedName() { Name = shortName, Category = Category.Player });
                    else if (realEntry.StartsWith("\u000312"))
                        this.UnmatchedShortNames.Add(new UnmatchedName() { Name = shortName, Category = Category.Ally });
                    else  // This shouldn't ever happen, but...
                        this.UnmatchedShortNames.Add(new UnmatchedName() { Name = shortName, Category = (Category) 7 });
                }
                this.MatchNames();

                this.AICheck();
            }
        }

        [ArenaRegex(@"^\x03\d*AI Battle information:(?:\x0312| \[NPC\])\x02 ([^\x02]*) \x02(?:\x03\d*)vs(?:\x030?4| \[Monster\])\x02 ([^\x02]*) \x02(?:\x03\d*)on (?:Arena Level|\[Streak Level\])\x02 (\d*) \x02\[Favorite to Win\]\x034\x02 (?:\x03\d{0,2})?(?:(\1)|(\2)|.*)")]
        internal void OnNPCBattleInfo(object sender, TriggerEventArgs e) {
            this.BattleOpen = false;

            Character ally; Combatant allyEntry;
            Character monster; Combatant monsterEntry;

            ally = GetCharacter(e.Match.Groups[1].Value);
            ally.Category = Category.Ally;
            monster = GetCharacter(e.Match.Groups[2].Value);
            monster.Category = Category.Monster;

            this.BattleList.Add(ally.ShortName, allyEntry = new Combatant(ally));
            this.BattleList.Add(monster.ShortName, monsterEntry = new Combatant(monster));

            this.WriteLine(1, 12, "{0} ({1}) enters the battle.", ally.Name, ally.ShortName);
            this.WriteLine(1, 12, "{0} ({1}) enters the battle.", monster.Name, monster.ShortName);

            // Let's give a few rating points to the favourite.
            if (e.Match.Groups[4].Success)
                ally.Rating += 50;
            else if (e.Match.Groups[5].Success)
                monster.Rating += 50;
        }

        [ArenaRegex(@"^\x03(?:(?:(12)|0?4)\x02|12\[(?:(NPC)|Monster)\]\x02 )([^\x02]*) \x02Information \[Number of Techs:\x02 (\d*)\x02\] \[Has an Ignition:\x02 (?:(yes)|no)\x02\] \[Has a Mech:\x02 (?:(yes)|no)\x02\]")]
        internal void OnNPCInfo(object sender, TriggerEventArgs e) {
            Character character = this.GetCharacter(e.Match.Groups[3].Value, true);

            character.TechniqueCount = int.Parse(e.Match.Groups[4].Value);
            character.HasIgnition = e.Match.Groups[5].Success;
            character.HasMech = e.Match.Groups[6].Success;
        }

        [ArenaRegex(@"^\x034\[Total Betting Amount:\x0312\x02 \$\$(\d*)\x02\x034\] \[Odds:\x0312\x02 ([0-9.]*):([0-9.]*)\x02\x034\]")]
        internal void OnNPCBattleOdds(object sender, TriggerEventArgs e) {
            this.BetTotal = int.Parse(e.Match.Groups[1].Value);
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.Category == Category.Ally)
                    combatant.Odds = float.Parse(e.Match.Groups[2].Value);
                else if (combatant.Category == Category.Monster)
                    combatant.Odds = float.Parse(e.Match.Groups[3].Value);
            }
        }

        [ArenaRegex(@"^\x0310-=BATTLE LIST=-")]
        internal void OnBattleListLegacyHeader(object sender, TriggerEventArgs e) {
            this.WaitingForBattleList = true;
            if (this.BattleType == BattleType.PvP) this.BattleType = BattleType.Normal;
        }

        internal void OnBattleListLegacy(string message) {
            message = message.Replace("Â ", " ").Trim();
            string[] entries = message.Split(new string[] { ", ", ",\u00A0", "," }, StringSplitOptions.RemoveEmptyEntries);

            if (!this.EnableAnalysis) {
                // If analysis is off, we won't try to match names.
                // Instead, just read the list.
                foreach (string entry in entries) {
                    string shortName = IrcClient.RemoveCodes(entry).Trim();
                    this.BattleList.Add(shortName, new Combatant() { Category = (Category) 7, HP = -1, ShortName = shortName });
                }
                return;
            }

            // Check for any characters that aren't listed.
            for (int i = this.BattleList.Count - 1; i >= 0; --i) {
                Combatant combatant = this.BattleList.Values.ElementAt(i);
                bool found = false;

                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (shortName == combatant.ShortName) {
                        found = true;
                        break;
                    }
                }
                if (!found) {
                    this.BattleList.Remove(combatant.ShortName);
                    this.UnmatchedFullNames.Add(new UnmatchedName() { Name = combatant.ShortName, Category = (Category) 7 });
                }
            }

            if (!this.BattleStarted) {
                // Every character will be alive before the battle starts.
                // We use this fact to determine the colour code.
                string aliveColour = message.Substring(0, 2);
                if (char.IsDigit(message[2])) aliveColour += message[2];
                this.BattleListAliveColour = aliveColour;
            }

            if (this.BattleOpen) {
                // If the entry period is still open, the speed order hasn't been calculated yet.
                // This means that combatants are listed in the order in which they entered.
                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (this.BattleList.ContainsKey(shortName)) continue;

                    // Pick the first entry from the list of unmatched names.
                    string fullName = this.UnmatchedFullNames[0].Name.TrimStart('.');
                    Character character;
                    UnmatchedFullNames.RemoveAt(0);

                    if (this.Characters.TryGetValue("*" + fullName, out character)) {
                        Characters.Remove(character.ShortName);
                        character.ShortName = shortName;
                        this.WriteLine(2, 9, "Reregistering *{0}.", fullName);
                    } else {
                        string upperName = fullName.ToUpperInvariant();
                        character = new Character() {
                            Name = fullName, ShortName = shortName,
                            IsUndead = upperName.Contains("UNDEAD") || upperName.Contains("ZOMBIE") || upperName.Contains("GHOST") || upperName.Contains("VAMPIRE"),
                            IsElemental = upperName.Contains("ELEMENTAL"),
                            IsEthereal = upperName.Contains("GHOST"),
                            HurtByTaunt = (upperName == "CRYSTAL SHADOW WARRIOR")
                        };

                        this.Characters.Add(character.ShortName, character);
                        this.WriteLine(2, 9, "Registered {0} to {1}.", fullName, character.ShortName);
                        this.RegisterEnteredCharacter(character);
                    }
                }
            } else if (!this.BattleStarted || this.UnmatchedFullNames.Count > 0) {
                // Try to deduce who's whom by matching the names.
                foreach (string entry in entries) {
                    string realEntry = entry.Trim();
                    string shortName = IrcClient.RemoveCodes(realEntry);
                    if (this.BattleList.ContainsKey(shortName)) continue;
                    this.UnmatchedShortNames.Add(new UnmatchedName() { Name = shortName, Category = (Category) 7 });
                }
                this.MatchNames();
            }
        }

        internal void MatchNames() {
            // DUBIOUS: Resolve ambiguous names.
            for (int i = this.UnmatchedFullNames.Count - 1; i >= 0; --i) {
                UnmatchedName entry = this.UnmatchedFullNames[i];
                if (entry.Name.StartsWith(".")) {
                    for (int j = this.UnmatchedShortNames.Count - 1; j >= 0; --j) {
                        UnmatchedName entry2 = this.UnmatchedShortNames[i];
                        if (Characters[entry2.Name].Name == entry.Name.TrimStart('.')) {
                            this.UnmatchedFullNames.RemoveAt(i);
                            this.UnmatchedShortNames.RemoveAt(j);
                            this.Entered(this.Characters[entry2.Name]);
                            this.WriteLine(2, 9, "Registered {0} to {1}.", this.Characters[entry2.Name].Name, entry2.Name);
                        }
                    }
                }
            }

            // We'll compare each full name with each short name, and use the pairs that match best.
            List<object[]> matches = new List<object[]>();
            for (int i = this.UnmatchedFullNames.Count - 1; i >= 0; --i) {
                UnmatchedName fullName = this.UnmatchedFullNames[i];

                List<object[]> possibleMatches = new List<object[]>();
                foreach (UnmatchedName shortName in this.UnmatchedShortNames) {
                    if ((shortName.Category & fullName.Category) > 0) {
                        float matchScore = BattleBotPlugin.NameMatch(shortName.Name, fullName.Name);
                        this.WriteLine(3, 3, "{0,-16} <- {1,-20} : {2,6:0.00} %", shortName.Name, fullName.Name, matchScore * 100.0F);
                        possibleMatches.Add(new object[] { shortName, fullName, matchScore });
                    }
                }

                if (possibleMatches.Count == 1 && this.UnmatchedFullNames.Count == this.UnmatchedShortNames.Count) {
                    // This full name only matches with one short name.
                    // This often happens with allies, because there's only one of them, and thus only one blue name.
                    UnmatchedName shortName = (UnmatchedName) possibleMatches[0][0];
                    Character character;

                    if (this.Characters.TryGetValue("*" + fullName.Name, out character)) {
                        this.Characters.Remove(character.ShortName);
                        character.ShortName = shortName.Name;
                        this.WriteLine(2, 9, "Reregistering *{0}.", fullName);
                    } else {
                        string upperName = fullName.Name.ToUpperInvariant();
                        character = new Character() {
                            Name = fullName.Name, ShortName = shortName.Name,
                            IsUndead = upperName.Contains("UNDEAD") || upperName.Contains("ZOMBIE") || upperName.Contains("GHOST") || upperName.Contains("VAMPIRE"),
                            IsElemental = upperName.Contains("ELEMENTAL"),
                            IsEthereal = upperName.Contains("GHOST"),
                            HurtByTaunt = (upperName == "CRYSTAL SHADOW WARRIOR")
                        };
                        if (shortName.Category == Category.Monster)
                            character.Category = Category.Monster;
                        else if (shortName.Category == Category.Player)
                            character.Category = Category.Player;
                        else if (shortName.Category == Category.Ally)
                            character.Category = Category.Ally;

                        this.Characters.Add(character.ShortName, character);
                        this.WriteLine(2, 9, "Registered {0} to {1}.", fullName.Name, character.ShortName);
                        this.RegisterEnteredCharacter(character);

                        // Remove this character from the list of considered matches.
                        for (int j = matches.Count - 1; j >= 0; --j) {
                            if (((UnmatchedName) matches[j][1]).Name == fullName.Name)
                                matches.RemoveAt(j);
                        }
                    }
                } else {
                    matches.AddRange(possibleMatches);
                }
            }

            // Look for the best matches.
            while (matches.Count > 0 && this.UnmatchedFullNames.Count > 0 && this.UnmatchedShortNames.Count > 0) {
                object[] bestMatch = null;

                foreach (object[] match in matches) {
                    if (bestMatch == null || (float) match[2] > (float) bestMatch[2])
                        bestMatch = match;
                }

                UnmatchedName shortName = (UnmatchedName) bestMatch[0];
                UnmatchedName fullName = (UnmatchedName) bestMatch[1];
                matches.Remove(bestMatch);
                if (!this.UnmatchedFullNames.Contains(fullName) || this.Characters.ContainsKey(shortName.Name))
                    continue;
                this.UnmatchedFullNames.Remove(fullName);
                this.UnmatchedShortNames.Remove(shortName);

                Character character;

                if (this.Characters.TryGetValue("*" + fullName.Name, out character)) {
                    this.Characters.Remove(character.ShortName);
                    character.ShortName = shortName.Name;
                    this.WriteLine(2, 9, "Reregistering *{0}.", fullName);
                } else {
                    string upperName = fullName.Name.ToUpperInvariant();
                    character = new Character() {
                        Name = fullName.Name, ShortName = shortName.Name,
                        IsUndead = upperName.Contains("UNDEAD") || upperName.Contains("ZOMBIE") || upperName.Contains("GHOST") || upperName.Contains("VAMPIRE"),
                        IsElemental = upperName.Contains("ELEMENTAL"),
                        IsEthereal = upperName.Contains("GHOST"),
                        HurtByTaunt = (upperName == "CRYSTAL SHADOW WARRIOR")
                    };
                    if (shortName.Category == Category.Monster)
                        character.Category = Category.Monster;
                    else if (shortName.Category == Category.Player)
                        character.Category = Category.Player;
                    else if (shortName.Category == Category.Ally)
                        character.Category = Category.Ally;

                    this.Characters.Add(character.ShortName, character);
                    this.WriteLine(2, 9, "Registered {0} to {1}.", fullName.Name, character.ShortName);
                    this.RegisterEnteredCharacter(character);

                    // Remove this character from the list of considered matches.
                    for (int j = matches.Count - 1; j >= 0; --j) {
                        if (((UnmatchedName) matches[j][1]).Name == fullName.Name)
                            matches.RemoveAt(j);
                    }
                }
            }
            this.UnmatchedFullNames.Clear();
        }

        public void RegisterEnteredCharacter(Character character) {
            Combatant combatant = new Combatant(character);
            this.BattleList.Add(character.ShortName, combatant);
        }

        [ArenaRegex(@"^\x0314\x02What a horrible night for a curse!")]
        internal void OnCurse(object sender, TriggerEventArgs e) {
            this.BattleConditions |= BattleCondition.CurseNight;
            foreach (Combatant combatant in this.BattleList.Values) {
                combatant.Status.Add("Cursed");
                combatant.TP = 0;
            }
        }

        [ArenaRegex(@"^\x0314\x02An ancient Melee-Only symbol glows on the ground of the battlefield\.")]
        internal void OnMeleeLock(object sender, TriggerEventArgs e) {
            this.BattleConditions |= BattleCondition.NoTechniques;
        }

        [ArenaRegex(@"^(?:\x032\x02|\x02\x032)([^\x02]*) \x02steps up first in the battle!")]
        internal void OnBattleStart(object sender, TriggerEventArgs e) {
            // This is the last message sent during setup.
            // This is when we know the battle has started.
            if (this.BattleOpen && this.DccClient != null && this.DccClient.State == IrcClientState.Online)
                ((DccClient) this.DccClient).SendSub("\u000312\u0002The battle has started.");

            this.BattleOpen = false;
            this.BattleStarted = true;
            this.BattleStartTime = DateTime.UtcNow;
			this.TurnNumber = 1;

            // If there are no monsters at the start of the battle, we can assume it's a PvP battle.
            // But not if the Arena bot has the legacy list format, and so is too old to include PvP mode.
            if (this.BattleListAliveColour == null) {
                bool noMonsters = true;
                foreach (Combatant combatant in this.BattleList.Values) {
                    if ((combatant.Category & Category.Monster) != 0) {
                        noMonsters = false;
                        break;
                    }
                }
                if (noMonsters) this.BattleType = BattleType.PvP;
            }

            this.eBattleStart?.Invoke(this, EventArgs.Empty);

            if (!this.EnableAnalysis) return;

            // Show the debug message.
            this.WriteLine(3, 11, "Combatants present: {0}", string.Join("\u000F, ",
                this.BattleList.Values.Select(delegate(Combatant combatant) {
                    // This big delegate function just chooses a colour code to display the name with.
                    switch (combatant.Category) {
                        case Category.Player:
                            return "\u000309" + combatant.ShortName;
                        case Category.Ally:
                            return "\u000312" + combatant.ShortName;
                        case Category.Ally | Category.Player:
                            return "\u000311" + combatant.ShortName;
                        case Category.Monster:
                            return "\u000304" + combatant.ShortName;
                        case Category.Monster | Category.Player:
                            return "\u000308" + combatant.ShortName;
                        case Category.Monster | Category.Ally:
                            return "\u000313" + combatant.ShortName;
                        case Category.Monster | Category.Ally | Category.Player:
                            return combatant.ShortName;
                        default:
                            return "\u000315" + combatant.ShortName;
                    }
                })
            ));

            this.Turn = GetShortName(e.Match.Groups[1].Value, false, true);
            ++this.BattleList[this.Turn].TurnNumber;

            this.AI.BattleStart();

            // If it's the bot's turn, act.
            this.AICheck();
        }
#endregion

#region Battle events
        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02performs an? (?:(double)|(triple)|(four hit)|(five hit)|(six hit)|(seven hit)|(eight hit)) attack(?: against\x02 ([^\x02]*) ?\x02)?")]
        internal void OnAttackMulti(object sender, TriggerEventArgs e) {
            this.Turn = GetShortName(e.Match.Groups[1].Value, false, true);
            if (e.Match.Groups[9].Success)
                this.TurnTarget = GetShortName(e.Match.Groups[9].Value, false, true);
            this.TurnAoE = false;
        }

        [ArenaRegex(@"^\x02\x033((?>[^\x02]*))\x02 ((?>[^\x03]*)(?:\x03(?>[^\x03]*))*?\x033\.)")]
        internal void OnAttackStandard(object sender, TriggerEventArgs e) {
            // Standard attack actions get "\x033." appended to them.
            // Technique actions, on the other hand, don't.
            if (!this.EnableAnalysis) return;
            if (this.TurnCounterer == null) {
                string attacker = GetShortName(e.Match.Groups[1].Value, false, true);
                if (this.Turn != attacker) {
                    this.TurnTarget = null;
                    this.Turn = attacker;
                }
            } else {
                // This is a counterattack, so the target will be whoever has the turn.
                this.TurnTarget = this.Turn;
            }
            this.TurnAction = e.Match.Groups[2].Value;
            this.TurnAoE = false;
            this.WriteLine(3, 3, "Standard attack detected");
        }

        [ArenaRegex(@"^\x037\x02((?>.*))\x02's melee attack is countered by ((?>[^!]*)(?:!(?>[^!]*))*?)!")]
        internal void OnCounter(object sender, TriggerEventArgs e) {
            this.TurnCounterer = GetShortName(e.Match.Groups[2].Value, false, true);
        }

        [ArenaRegex(@"^\x033\x02((?!Battle Chat|Who's Online|Your|With\x02)[^\x02\x03]*)(?<!'s)(?:\x02 | \x02)(?!has entered the dimensional rift to join the battle arena\.$)(?!has (?:gained|restored|regained|been healed for)\x02)(?!absorbs\x02)(?!is wearing\x02)(?!'s (?:HP|TP|Ignition Gauge) is:\x02)(?!has\x02)(?!has the following (?:\x0312)?(?:weapons|resistances|monster killer traits|\+?\w+ items|gems|keys|\w+ armor|accessor(?:y|ies)|runes))(?!currently has no)(?!is roughly level\x02)(?!is currently using the\x02)(?!has switched to the\x02)(?!knows the following (?:\x0312)?(?:styles|(?:ignition )?techniques|(?:passive|active)(?:\x033)? skills|augments))(?!currently (?:knows|has) no)(?!does not know any)(?!has obtained the following Ignitions)(?!has equipped\x02)(?!unequipped the)(?!'s status is currently:\x03)(?!has (?:equipped|removed) the(?: accessory)?\x02)(?!is no[tw] wearing the \x02)(?!has (?:saved|reloaded) winning streak #\x02)(?!currently has winning streak #\x02)(?! gives \d+)(?!'s [^ ]+ style has leveled up! It is now\x02)(?!has a difficulty of\x02)(?!sets \w+ difficulty to\x02)(?!'s\x02 \w+ is now augmented)(?!uses \x021 RepairHammer\x02 and\x02)(?!has not unlocked any achievements yet\.$)(?!has unlocked the following achievements:\x02)(?!has no augments currently activated\.$)(?!has been defeated\x02)(?!is currently undefeated!$)(?!drops a small \w+ orb that restores\x02)(?!has regained interest in the battle\.$)(?!has sobered up\.$)(?!'s body has fought off the virus\.$)(?!has broken\x02)(?!attack goes right through\x02)(?!has become corporeal\.$)(?!has successfully dug up a\(?:n\)\x02)(?!has stolen and absorbs\x02)(?!absorbs\x02 [0-9,]+ HP)(?!is no longer (?:surrounded by a reflective magical barrier|confused)\.$)(?!weapon lock has broken\.)(?!(?:(?:defense|strength|int) down status|(?:zombie|physical protect|magic shell) status|melee weapon enchantment|\w+) has worn off.$)((?>.*))(?<!\x033\.)")]
        internal void OnAttackTechnique(object sender, TriggerEventArgs e) {
            // Standard attack actions end with "\x033."
            // Technique actions normally don't.
            if (!this.EnableAnalysis) return;
            string attacker = GetShortName(e.Match.Groups[1].Value, false, true);
            if (this.Turn != attacker) {
                this.TurnTarget = null;
                this.Turn = attacker;
            }
            this.TurnAction = e.Match.Groups[2].Value;
            this.TurnAoE = false;
            this.WriteLine(3, 3, "Technique detected");
        }

        internal void RegisterAttack() {
            this.RegisterAttack('\0');
        }
        internal void RegisterAttack(char colour) {
            if (!this.EnableAnalysis) return;
            if (this.Turn == null || this.TurnAction == null) return;

            // Was the action a technique?
            bool isTechnique = !this.TurnAction.EndsWith("\u00033.");
            Technique technique = null;
            string techniqueName;
            string techniqueElement;
            bool techniqueHealing = isTechnique && this.TurnAction.Contains("healing");

            if (!this.TurnAoE) {
                if (this.Turn != null)
                    this.TurnAction = Regex.Replace(this.TurnAction, @"\b" + Regex.Escape(this.BattleList[this.Turn].Name) + @"\b", "%user");
                this.TurnAction = Regex.Replace(this.TurnAction, @"\b(?:him|her|it|his|its|he|she)\b", "%gender");
                if (this.TurnTarget != null)
                    this.TurnAction = Regex.Replace(this.TurnAction, @"\b" + Regex.Escape(this.BattleList[this.TurnTarget].Name) + @"\b", "%target");
            }

            if (isTechnique) {
                techniqueName = null;
                foreach (Technique _technique in this.Techniques.Values) {
                    if (_technique.Description == this.TurnAction) {
                        techniqueName = _technique.Name;
                        technique = _technique;
                        break;
                    }
                }
                if (!this.TurnAoE) {
                    if (techniqueName == null) {
                        // We don't yet know this technique. Let's note all we can see about it.
                        if (this.TurnAbility == null || this.TurnAbility == "?")
                            techniqueName = "UnknownTechnique" + this.Techniques.Count.ToString();
                        else
                            techniqueName = this.TurnAbility;
                        if (!this.Techniques.TryGetValue(techniqueName, out technique) || !technique.IsWellKnown) {
                            // Guess the element of the technique.
                            Match match = Regex.Match(this.TurnAction, @"\b(?:(ice|icy)|(water|tsunami\b|tidal wave\b)|(thunder\b|lightning\b)|(holy\b)|(curse|dark(?:ness)?\b)|(fire(?!d|s|ing)|flam(?:e|ing))|(winds?\b|tornado|cyclone)|(ground\b|earth|boulder|(?-i:rock|stone))|(light\b)|(shock(?!\W?wave)))", RegexOptions.IgnoreCase);
                            if (match.Groups[1].Success)
                                techniqueElement = "Ice";
                            else if (match.Groups[2].Success)
                                techniqueElement = "Water";
                            else if (match.Groups[3].Success)
                                techniqueElement = "Lightning";
                            else if (match.Groups[4].Success)
                                techniqueElement = "Light";
                            else if (match.Groups[5].Success)
                                techniqueElement = "Dark";
                            else if (match.Groups[6].Success)
                                techniqueElement = "Fire";
                            else if (match.Groups[7].Success)
                                techniqueElement = "Wind";
                            else if (match.Groups[8].Success)
                                techniqueElement = "Earth";
                            else if (match.Groups[9].Success)
                                techniqueElement = "Light";
                            else if (match.Groups[10].Success)
                                techniqueElement = "Lightning";
                            else
                                techniqueElement = null;
                            technique = new Technique() { Name = techniqueName, Description = this.TurnAction, Element = techniqueElement };
                            this.Techniques[techniqueName] = technique;
                            this.WriteLine(2, 9, "Found new technique {0}. (Element: {1})", techniqueName, techniqueElement ?? "None");
                        }
                    } else {
                        if (this.TurnAbility != null && this.TurnAbility != "?" && this.TurnAbility != techniqueName) {
                            technique = this.Techniques[techniqueName];
                            this.WriteLine(2, 9, "Reregistering {0} as {1}.", techniqueName, this.TurnAbility);
                            this.Techniques.Remove(techniqueName);
                            foreach (Character character in this.Characters.Values) {
                                if (character.EquippedTechniques != null) {
                                    for (int i = 0; i < character.EquippedTechniques.Count; ++i) {
                                        if (character.EquippedTechniques[i] == techniqueName) {
                                            character.EquippedTechniques[i] = this.TurnAbility;
                                            break;
                                        }
                                    }
                                }
                            }
                            techniqueName = this.TurnAbility;
                            technique.Name = techniqueName;
                            if (!this.Techniques.ContainsKey(techniqueName)) this.Techniques.Add(techniqueName, technique);
                        }
                    }
                }
                if (technique != null) {
                    if (this.TurnTarget == null)
                        this.WriteLine(2, 3, "{0} used {1} on {2}.", this.BattleList[this.Turn].Name, technique.Name, "???");
                    else
                        this.WriteLine(2, 3, "{0} used {1} on {2}.", this.BattleList[this.Turn].Name, technique.Name, this.BattleList[this.TurnTarget].Name);
                    this.BattleList[this.Turn].LastAction = technique.Name;
                }
                if (!this.TurnAoE && !this.BattleList[this.Turn].Status.Contains("conserving TP", StringComparer.InvariantCultureIgnoreCase)) {
                    this.BattleList[this.Turn].TP -= technique.TP;
                    if (this.BattleList[this.Turn].TP < 0) this.BattleList[this.Turn].TP = 0;
                }

                // Register the effectiveness.
                if (this.IsBattleDungeon && this.TurnTarget != null && colour != '\0') {
                    Combatant target = this.BattleList[this.TurnTarget];
                    if (!target.HasUsedMagicShift) {
                        // Element
                        if (technique != null && technique.Element != null && technique.Element != "none") {
                            if (colour == '7') {
                                // The attack was super effective.
                                if (target.Character.ElementalWeaknesses == null) target.Character.ElementalWeaknesses = new List<string>();
                                if (!target.Character.ElementalWeaknesses.Contains(technique.Element, StringComparer.InvariantCultureIgnoreCase)) {
                                    this.WriteLine(2, 9, "{0} is weak to {1}.", target.Name, technique.Element);
                                    target.Character.ElementalWeaknesses.Add(technique.Element);
                                }
                            } else if (colour == '6') {
                                // The attack was not very effective.
                                if (target.Character.ElementalResistances == null) target.Character.ElementalResistances = new List<string>();
                                if (!target.Character.ElementalResistances.Contains(technique.Element, StringComparer.InvariantCultureIgnoreCase)) {
                                    this.WriteLine(2, 9, "{0} is resistant to {1}.", target.Name, technique.Element);
                                    target.Character.ElementalResistances.Add(technique.Element);
                                }
                            } else {
                                if (target.Character.RegularlyEffective == null) target.Character.RegularlyEffective = new List<string>();
                                if (!target.Character.RegularlyEffective.Contains(technique.Element, StringComparer.InvariantCultureIgnoreCase))
                                    target.Character.RegularlyEffective.Add(technique.Element);
                            }
                        }
                    }
                }
            } else {
                this.BattleList[this.Turn].LastAction = this.BattleList[this.Turn].Character.EquippedWeapon;
            }
        }

        [ArenaRegex(@"^The attack did\x03([467])\x02 ([\d,]+) \x02\x0Fdamage(?! \x0F?to)(?: \[([^\]]*)\])?")]
        internal void OnDamageLegacy(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            if (this.TurnAction == null) return;

            int maxOccurrences = 0;
            // We will find out whom the target is by looking for whose name is mentioned the most in the action.
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.ShortName == this.Turn) continue;
                int pos = -1; int occurrences = 0;
                while (true) {
                    pos = this.TurnAction.IndexOf(combatant.Name, pos + 1);
                    if (pos < 0) break;
                    ++occurrences;
                }
                if (occurrences > maxOccurrences) {
                    maxOccurrences = occurrences;
                    this.TurnTarget = combatant.ShortName;
                }
            }
            this.RegisterAttack(e.Match.Groups[1].Value[0]);
            this.TurnAbility = null;
            this.Turn = null;
        }

        [ArenaRegex(@"^The attack did\x03([467])\x02 ([\d,]+) \x02\x0Fdamage to ([^[]*)(?<! )(?: \[([^\]]*)\])?")]
        internal void OnDamageSingle(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            this.TurnTarget = GetShortName(e.Match.Groups[3].Value, false, true);
            this.RegisterAttack(e.Match.Groups[1].Value[0]);
            this.TurnAbility = null;
            this.Turn = null;
        }

        [ArenaRegex(@"^(?:\x031)?The first attack did\x03([467])\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The second attack did\x03\d\x02 ([\d,]+) [\x02\x03]*\x0Fdamage\. +(?:The third attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The fourth attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The fifth attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The sixth attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The seventh attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\. +(?:The eighth? attack did\x03\d\x02 ([\d,]+) \x02\x0Fdamage\.\ +)?)?)?)?)?)?)?Total (?:physical )?damage:\x03\d\x02 ([\d,]+) \x0F(.*)")]
        internal void OnDamageMulti(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;

            int maxOccurrences = 0;
            // We will find out whom the target is by looking for whose name is mentioned the most in the action.
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.ShortName == this.Turn) continue;
                int pos = -1; int occurrences = 0;
                while (true) {
                    pos = this.TurnAction.IndexOf(combatant.Name, pos + 1);
                    if (pos < 0) break;
                    ++occurrences;
                }
                if (occurrences > maxOccurrences) {
                    maxOccurrences = occurrences;
                    this.TurnTarget = combatant.ShortName;
                }
            }
            this.RegisterAttack(e.Match.Groups[1].Value[0]);
            this.TurnAbility = null;
            this.Turn = null;
        }

        [ArenaRegex(@"^The attack did\x03([467])\x02 ([\d,]+) \x02\x0Fdamage \x0Fto\x02 ([^\x02]*)\x02! (\x034\x02[^\x02]*\x02.* )?\x0F(\[([^\]]*)\])?( \x02\x034.*\x02.*\x02.*\x02!)?")]
        internal void OnDamageAoE(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            this.TurnTarget = GetShortName(e.Match.Groups[3].Value, false, true);
            this.RegisterAttack(e.Match.Groups[1].Value[0]);
            this.TurnAoE = true;
        }

        [ArenaRegex(@"^\x033\x02([^\x02]*) \x02has been healed for\x02 ([\d,]+) \x02health!")]
        internal void OnHeal(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            //this.TurnTarget = GetShortName(e.Match.Groups[1].Value, false, true);

            if (this.TurnAbility != null && this.TurnAbility != "?") {
                // Check for monsters absorbing attacks.
                Technique technique;
                if (this.Techniques.TryGetValue(this.TurnAbility, out technique) && technique.Type != TechniqueType.Heal && technique.Type != TechniqueType.AoEHeal &&
                        technique.Element != null && !technique.Element.Equals("None", StringComparison.InvariantCultureIgnoreCase)) {
                    Character character = this.GetCharacter(e.Match.Groups[1].Value, false);
                    if (character != null) {
                        if (character.ElementalAbsorbs == null) character.ElementalAbsorbs = new List<string>();
                        if (!character.ElementalAbsorbs.Contains(technique.Element))
                            character.ElementalAbsorbs.Add(technique.Element);
                        this.WriteLine(2, 9, "{0} absorbed the {1} attack.", character.Name, technique.Element);
                    }
                }
            }

            this.TurnAoE = true;
        }

        [ArenaRegex(@"^\x02\x034(?>[^\x02]*)\x02can only attack monsters!")]
        internal void OnFailedAttackAlly(object sender, TriggerEventArgs e) {
            // This is how we differentiate allies from monsters in old versions
            // of Battle Arena that don't colour the battle list.
            if (!this.EnableAnalysis) return;
            if (this.Turn == this.LoggedIn) {
                Character character; Combatant combatant;
                character = this.Characters[this.TurnTarget];
                combatant = this.BattleList[this.TurnTarget];
                character.Category = character.Category & ~Category.Monster;
                combatant.Category = combatant.Category & ~Category.Monster;
                this.WriteLine(2, 9, "Registered {0} as an ally.", character.ShortName);
                Thread.Sleep(1000);
                this.AITurn();
            }
        }

        [ArenaRegex(@"^\x02\x034([^\x02]*) \x02is now\x02 (?:(frozen in time)|(poisoned)|(silenced)|(blind)|inflicted with (?:(a virus)|(amnesia)|(defense down)|(strength down)|(int down))|(paralyzed)|(a zombie)|(slowed)|(stunned)|(cursed)|(charmed)|(intimidated)|(petrified)|(bored of the battle)|(confused)|(no longer boosted)|((?:gains|under) defense up)|([^\x02]*))\x02!")]
        internal void OnStatusInflicted(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string subject = this.GetShortName(e.Match.Groups[1].Value, false, true);
            if (subject == null) return;
            Combatant combatant = this.BattleList[subject];
            string effect;
            if (e.Match.Groups[2].Success)
                effect = "frozen in time";
            else if (e.Match.Groups[3].Success)
                effect = "poisoned";
            else if (e.Match.Groups[4].Success)
                effect = "silenced";
            else if (e.Match.Groups[5].Success)
                effect = "blind";
            else if (e.Match.Groups[6].Success)
                effect = "virus";
            else if (e.Match.Groups[7].Success)
                effect = "under amnesia";
            else if (e.Match.Groups[8].Success)
                effect = "Defense Down";
            else if (e.Match.Groups[9].Success)
                effect = "Strength Down";
            else if (e.Match.Groups[10].Success)
                effect = "Int Down";
            else if (e.Match.Groups[11].Success)
                effect = "paralyzed";
            else if (e.Match.Groups[12].Success)
                effect = "zombie";
            else if (e.Match.Groups[13].Success)
                effect = "slowed";
            else if (e.Match.Groups[14].Success)
                effect = "stunned";
            else if (e.Match.Groups[15].Success) {
                effect = "cursed";
                combatant.TP = 0;
            } else if (e.Match.Groups[16].Success)
                effect = "charmed";
            else if (e.Match.Groups[17].Success)
                effect = "intimidated";
            else if (e.Match.Groups[18].Success)
                effect = "petrified";
            else if (e.Match.Groups[19].Success)
                effect = "bored";
            else if (e.Match.Groups[20].Success)
                effect = "confused";
            else if (e.Match.Groups[21].Success) {
                // Remove boost
                combatant.Status.Remove("power boosted");
                this.WriteLine(1, 12, "{0} is no longer boosted.", combatant.Name);
                return;
            } else if (e.Match.Groups[22].Success)
                effect = "Defense Up";
            else {
                this.WriteLine(2, 4, "Unrecognised status effect: {0}", e.Match.Groups[23].Value);
                return;
            }
            if (combatant.Status.Contains(effect, StringComparer.InvariantCultureIgnoreCase)) {
                if (e.Match.Groups[3].Success) {
                    effect = "poisoned heavily";
                    combatant.Status[combatant.Status.IndexOf("poisoned")] = effect;
                    this.WriteLine(1, 12, "{0} is now inflicted with {1}.", combatant.Name, effect);
                }
            } else if (!e.Match.Groups[3].Success || !combatant.Status.Contains("poisoned heavily", StringComparer.InvariantCultureIgnoreCase)) {
                combatant.Status.Add(effect);
                this.WriteLine(1, 12, "{0} is now inflicted with {1}.", combatant.Name, effect);
            }
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02is immune to the ([^ ]*) status!")]
        internal void OnStatusImmune(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string subject = this.GetShortName(e.Match.Groups[1].Value, false, true);
            Character character = this.Characters[subject];
            if (character.StatusImmunities == null) character.StatusImmunities = new List<string>();
            if (!character.StatusImmunities.Contains(e.Match.Groups[2].Value)) {
                character.StatusImmunities.Add(e.Match.Groups[2].Value);
                this.WriteLine(1, 12, "{0} is immune to the {1} effect.", character.Name, e.Match.Groups[2].Value);
            }
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)([^\x02]*)'s\x02 attack goes right through\x02 ([^\x02]*) \x02doing no damage!")]
        internal void OnEtherealAvoid(object sender, TriggerEventArgs e) {
            try {
                if (!this.EnableAnalysis) return;
                string subject = this.GetShortName(e.Match.Groups[2].Value, false, true);
                Character character = this.Characters[subject];
                Combatant combatant = this.BattleList[subject];
                character.IsEthereal = true;
                if (!combatant.Status.Contains("ethereal", StringComparer.InvariantCultureIgnoreCase))
                    combatant.Status.Add("ethereal");
            } catch (KeyNotFoundException) { }
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02is immune to the (?i:(fire)|(ice)|(water)|(lightning)|(earth)|(wind)|(light)|(dark)|([^ ]+)) element!")]
        internal void OnElementImmune(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            Character character = this.Characters[e.Match.Groups[1].Value];
            if (!character.ElementalImmunities.Contains(e.Match.Groups[2].Value)) {
                character.ElementalImmunities.Add(e.Match.Groups[2].Value);
                this.WriteLine(1, 12, "{0} is immune to {1}.", character.Name, e.Match.Groups[2].Value);
            }
            this.RegisterAttack();
        }

        [ArenaRegex(@"^\x032\x02([^\x02]*) \x02looks at (.*?) and says ""(.*)""\x0F")]
        internal void OnTaunt(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            this.TurnAction = "taunt";
        }

        [ArenaRegex(@"^\x034The\x02 ([^ ]+) \x02explodes and summons\x02 (?:[^\x02]*)\x02! \x02\x03(12\2 \x02(.*))?")]
        internal void OnSummon(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            Character character;
            string shortName = e.Match.Groups[2].Value;
            if (!Characters.TryGetValue(shortName, out character))
                this.Characters.Add(shortName, character = new Character() { ShortName = shortName, Name = e.Match.Groups[2].Value, Category = Category.Ally, Description = e.Match.Groups[3].Success ? e.Match.Groups[3].Value : null });
            this.BattleList.Add(this.Turn + "_summon", new Combatant() { ShortName = shortName, Name = e.Match.Groups[2].Value, Character = character, Category = Category.Ally });
        }

        [ArenaRegex(@"^\x033It is\x02 ([^\x02]*)\x02's turn \[[^:]*Health[^:]*:\x02? (\x02?\x03[01]?\d\x02?.*)\x02?\x0F?\x033\] \[[^:]*Status[^:]*:\x02?\x034\x02? ?(?:\x034)*((?:\[[^\]]*\]|[^\]])*)\x02?\x0F?\x033\]")]
        internal void OnTurn(object sender, TriggerEventArgs e) {
            this.BattleOpen = false;
            this.BattleStarted = true;
            if (!this.EnableAnalysis) return;
            this.Turn = null;
            this.TurnAction = null;
            this.TurnTarget = null;

            Combatant combatant; Character character;

            // Make sure we know the character whose turn it is.
            bool known = false;
            foreach (UnmatchedName name in this.UnmatchedFullNames) {
                if (name.Name == e.Match.Groups[1].Value) {
                    return;
                }
            }
            if (!known) {
                this.Turn = this.GetShortName(e.Match.Groups[1].Value, false, true);
                if (this.Turn == null) {
                    this.Turn = this.GetShortName(e.Match.Groups[1].Value, false, false);
                    if (this.Turn == null) {
                        // We don't know them.
                        if (e.Match.Groups[1].Value.StartsWith("Clone of ")) {
                            this.Turn = this.GetShortName(e.Match.Groups[1].Value.Substring(9), false, true);
                            if (this.Turn != null) {
                                // Register the clone.
                                this.RegisterClone(this.Turn, e.Match.Groups[1].Value);
                                this.Turn += "_clone";
                            } else {
                                // We'll need to find them in the battle list later.
                                this.UnmatchedFullNames.Add(new UnmatchedName() { Name = e.Match.Groups[1].Value, Category = (Category) 7 });
                                return;
                            }
                        } else {
                            // We'll need to find them in the battle list later.
                            this.UnmatchedFullNames.Add(new UnmatchedName() { Name = e.Match.Groups[1].Value, Category = (Category) 7 });
                            return;
                        }
                    }
                }
            }

            character = this.Characters[this.Turn];
            if (!this.BattleList.TryGetValue(this.Turn, out combatant)) {
                combatant = new Combatant(character);
                this.BattleList.Add(this.Turn, combatant);
            }

            // Count the turn.
            ++combatant.TurnNumber;
            if (combatant.TurnNumber > this.TurnNumber) {
                ++this.TurnNumber;
                if (this.DarknessTurns > 0) --this.DarknessTurns;
                if (this.HolyAuraTurns > 0) --this.HolyAuraTurns;
				if (this.LongAiBattleFix) this.CheckLongAiBattle();
			} else if (combatant.TurnNumber > this.TurnNumber) {
                combatant.TurnNumber = this.TurnNumber;
            }

            // Check health.
            combatant.Health = IrcClient.RemoveCodes(e.Match.Groups[2].Value);

            // Check status.
            string status = IrcClient.RemoveCodes(e.Match.Groups[3].Value);
            combatant.Status.Clear();
            if (status != "none" && status != "normal")
                combatant.Status.AddRange(status.Split(new char[] { '|', ' ', 'Â' }, StringSplitOptions.RemoveEmptyEntries));

            // Count TP.
            if (!combatant.Status.Contains("cursed")) {
                combatant.TP += 5;
                int zenLevel;
                if (character.Skills != null && character.Skills.TryGetValue("Zen", out zenLevel))
                    combatant.TP += zenLevel * 5;
                if (combatant.TP > character.BaseTP)
                    combatant.TP = character.BaseTP;
            }

            // Invoke the AI.
            foreach (string effect in combatant.Status) {
                if (effect == "staggered"   || effect == "blind"    || effect == "petrified" || effect == "evolving"       ||
                    effect == "intimidated" || effect == "asleep"   || effect == "stunned"   || effect == "frozen in time" ||
                    effect == "charmed"     || effect == "confused" || effect == "paralyzed" || effect == "bored"          ||
                    effect == "drunk")
                    return;
            }
            this.AICheck();
        }

		public void CheckLongAiBattle() {
			/*
				This feature will call off an AI battle that appears to be a stalemate.
				The bot must have access to the Arena data directory for this to work.
				If 20 turns have passed since the start of the battle, and no one has lost 1/3 of their HP, the battle is called off.
				If 40 turns have passed since the start of the battle, and no one has lost 2/3 of their HP, the battle is called off.
				If 60 turns have passed since the start of the battle, it's called off.
				If a battle is called off this way, all bets are refunded.
			*/
			const int CheckInterval = 20;
			const int CheckLimit = 3;

			if (this.BattleType == BattleType.NPC && this.TurnNumber > 1 && this.TurnNumber % CheckInterval == 1 && this.ArenaDirectory != null && this.IsAdmin
				&& this.DarknessWarning == default(DateTime)) {  // No time over if there is a demon wall in the battle.
				// Read the character files only every 20 turns.
				var listBuilder = new StringBuilder();

				if (this.TurnNumber < CheckInterval * CheckLimit) {
					using (var reader = new StreamReader(Path.Combine(this.ArenaDirectory, "txts", "battle.txt"))) {
						// Need to read the battle list because we might not know the file names by other means.
						while (!reader.EndOfStream) {
							var name = reader.ReadLine();
							if (listBuilder.Length != 0) listBuilder.Append(", ");
							listBuilder.Append(name);

							var file = IniFile.FromFile(Path.Combine(this.ArenaDirectory, "characters", name + ".char"), StringComparer.InvariantCultureIgnoreCase);

							var hpThreshold = int.Parse(file["BaseStats", "HP"]);
							hpThreshold -= hpThreshold * (this.TurnNumber / CheckInterval) / CheckLimit;

							var hp = int.Parse(file["Battle", "HP"]);
							if (hp < hpThreshold) return;
						}
					}
				}

				Directory.CreateDirectory("data");
				File.AppendAllText(Path.Combine("data", this.Key + "-StoppedAiBattles.log"), DateTime.Now.ToString() + " - " + listBuilder.ToString() + " - stopped after " + (this.TurnNumber - 1) + " turns." + Environment.NewLine);

				// No one has taken enough damage.
				this.BattleAction(false, this.TurnNumber < CheckInterval * CheckLimit ? "No one seems to be doing much damage here. I'll refund all bets." : "Time Over.");

				using (var reader = new StreamReader(Path.Combine(this.ArenaDirectory, "txts", "1vs1bet.txt"))) {
					while (!reader.EndOfStream) {
						var line = reader.ReadLine();
						var match = Regex.Match(line, @"^([^=]*)\.betamount=(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
						if (match.Success) {
							this.BattleAction(true, "!add " + match.Groups[1].Value + " doubledollars " + match.Groups[2].Value);
						}
					}
				}

				this.BattleAction(true, "!end battle draw");
			}
		}

        [ArenaRegex(@"^\x0312\x02([^\x02]*) \x02gets another turn\.")]
        internal void OnTurnExtra(object sender, TriggerEventArgs e) {
            this.Turn = GetShortName(e.Match.Groups[1].Value, false, true);
            this.AICheck();
        }

        public void RegisterClone(string original, string name) {
            Combatant combatant = this.BattleList[original];
            Character character = this.Characters[original];
            Character newCharacter;

            this.Characters.Add(original + "_clone", newCharacter = new Character() {
                Name = name, ShortName = original + "_clone", Category = character.Category < Category.Monster ? Category.Ally : Category.Monster,
                BaseHP = (int) Math.Round((float) combatant.HP * 0.4F), BaseTP = character.BaseTP, IgnitionGauge = character.IgnitionGauge, IgnitionCapacity = character.IgnitionCapacity,
                BaseSTR = character.BaseSTR, BaseDEF = character.BaseDEF, BaseINT = character.BaseINT, BaseSPD = character.BaseSPD,
                EquippedWeapon = character.EquippedWeapon, EquippedWeapon2 = character.EquippedWeapon2, EquippedAccessory = character.EquippedAccessory, EquippedTechniques = (character.EquippedTechniques == null ? null : new List<string>(character.EquippedTechniques)),
                IsUndead = character.IsUndead, IsElemental = character.IsElemental, IsEthereal = character.IsEthereal,
                ElementalResistances = character.ElementalResistances, ElementalWeaknesses = character.ElementalWeaknesses, ElementalImmunities = character.ElementalImmunities, ElementalAbsorbs = character.ElementalAbsorbs,
                WeaponResistances = character.WeaponResistances, WeaponWeaknesses = character.WeaponWeaknesses,
                IsReadyToControl = character.IsReadyToControl, IsWellKnown = character.IsWellKnown
            });
            this.BattleList.Add(original + "_clone", new Combatant() {
                Name = name, ShortName = original + "_clone", Character = newCharacter, Category = character.Category < Category.Monster ? Category.Ally : Category.Monster, Presence = Presence.Alive, Status = new List<string>(combatant.Status),
                HP = (int) Math.Round((float) combatant.HP * 0.4F), TP = combatant.TP,
                STR = combatant.STR, DEF = combatant.DEF, INT = combatant.INT, SPD = combatant.SPD,
                IsUnderMightyStrike = combatant.IsUnderMightyStrike, IsUnderElementalSeal = combatant.IsUnderElementalSeal,
                IsUnderRoyalGuard = combatant.IsUnderRoyalGuard, IsUnderManaWall = combatant.IsUnderManaWall, UtsusemiShadows = combatant.UtsusemiShadows,
            });
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*)(?:\x02 | \x02)has been defeated by\x02 ([^\x02]*)\x02!( \x037\<\<\x02OVERKILL\x02\>\>)?")]
        internal void OnDefeatNormal(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            this.OnDefeat(victim, e.Match.Groups[3].Success);
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*)\x02 .*")]
        internal void OnDefeatCustom(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            if (victim == null) return;
            this.OnDefeat(victim, e.Match.Value.EndsWith("\u00037<<\u0002OVERKILL\u0002>>"));
        }

        internal void OnDefeat(string victim, bool overkill) {
            if (victim == null) return;
            Combatant combatant = this.BattleList[victim];
            this.WriteLine(2, 8, "{0} is defeated.", combatant.Name);
            if (this.BattleType == BattleType.NPC) return;
            combatant.Presence = Presence.Dead;
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)([^\x02]*) \x02has equipped\x02 (?:\x03\d{0,2})?([^ .\x03]*)(?:\x03\d{0,2})?(?:$|(\.)|\x02 in (\w*) (?:(left)|(right)) hand\.)")]
        internal void OnEquip(object sender, TriggerEventArgs e) {
            Character character;
            string shortName = GetShortName(e.Match.Groups[1].Value, false, true);
            if (shortName == null) {
                character = GetCharacter(e.Match.Groups[1].Value, false);
                if (character == null) return;
            } else
                character = this.Characters[shortName];

            if (e.Match.Groups[5].Success) {
                // Left hand
                character.EquippedWeapon2 = e.Match.Groups[2].Value;
            } else if (e.Match.Groups[6].Success) {
                // Right hand
                character.EquippedWeapon = e.Match.Groups[2].Value;
            } else {
                // Single-handed weapon
                character.EquippedWeapon = e.Match.Groups[2].Value;
                character.EquippedWeapon2 = null;
            }

            character.EquippedTechniques = new List<string>();

            if (!this.Weapons.ContainsKey(character.EquippedWeapon))
                this.Weapons.Add(character.EquippedWeapon, new Weapon() { Name = character.EquippedWeapon, Techniques = new List<string>(), IsTwoHanded = !(e.Match.Groups[5].Success || e.Match.Groups[6].Success) });
            if (character.EquippedWeapon2 != null && !this.Weapons.ContainsKey(character.EquippedWeapon2))
                this.Weapons.Add(character.EquippedWeapon2, new Weapon() { Name = character.EquippedWeapon2, Techniques = new List<string>(), IsTwoHanded = false });

            this.GetEquippedTechniques(character);
        }

        [ArenaRegex(@"^(?:\x033\x02|\x02\x033)([^\x02]*) \x02unequipped the \w+(?: and the \w+)?")]
        internal void OnUnequip(object sender, TriggerEventArgs e) {
            Character character;
            string shortName = GetShortName(e.Match.Groups[1].Value, false, true);
            if (shortName == null) {
                character = GetCharacter(e.Match.Groups[1].Value, false);
                if (character == null) return;
            } else
                character = this.Characters[shortName];

            character.EquippedWeapon = "Fists";
            character.EquippedWeapon2 = null;
            character.EquippedTechniques = new List<string>();

            this.GetEquippedTechniques(character);
        }

        public void GetEquippedTechniques(Character character) {
            if (character.Techniques == null) return;
            Weapon weapon;
            if (character.EquippedWeapon != null && this.Weapons.TryGetValue(character.EquippedWeapon, out weapon)) {
                foreach (string technique in weapon.Techniques) {
                    if (!character.EquippedTechniques.Contains(technique) && character.Techniques.ContainsKey(technique))
                        character.EquippedTechniques.Add(technique);
                }
            }
            if (character.EquippedWeapon2 != null && this.Weapons.TryGetValue(character.EquippedWeapon2, out weapon)) {
                foreach (string technique in weapon.Techniques) {
                    if (!character.EquippedTechniques.Contains(technique) && character.Techniques.ContainsKey(technique))
                        character.EquippedTechniques.Add(technique);
                }
            }
        }

        [ArenaRegex(new string[] { @"^\x034\x02Darkness\x02 will overcome the battlefield in 5 minutes\.",
                                   @"^\x034\x02The heroes\x02 estimate they have 5 minutes before the wall will crush them at the end of the hall\."})]
        internal void OnDarknessWarning(object sender, TriggerEventArgs e) {
            this.DarknessWarning = DateTime.Now;
            this.WriteLine(2, 8, "Darkness will overcome the battlefield in 5 minutes.");
        }

        [ArenaRegex(@"^\x034\x02Darkness\x02 covers the battlefield enhancing the strength of all remaining monsters!")]
        internal void OnDarkness(object sender, TriggerEventArgs e) {
            this.Darkness = true;
            this.WriteLine(2, 8, "Darkness has overcome the battlefield.");
        }

        [ArenaRegex(@"^\x0312\x02([^\x02]*) \x02releases a holy aura that covers the battlefield and keeps the darkness at bay for\x02 (\d+) minute\(s\)\x02\.")]
        internal void OnHolyAura(object sender, TriggerEventArgs e) {
            int time = int.Parse(e.Match.Groups[2].Value);
            if (this.DarknessWarning != default(DateTime))
                this.DarknessWarning -= TimeSpan.FromMinutes(time);
            this.BattleStartTime -= TimeSpan.FromMinutes(time);
            this.HolyAuraTurns = -1;
            this.HolyAuraUser = e.Match.Groups[1].Value;
            this.HolyAuraEnd = DateTime.Now.AddMinutes(time);
            if (time == 1)
                this.WriteLine(2, 8, "Darkness will be held back for {0} minute.", time);
            else
                this.WriteLine(2, 8, "Darkness will be held back for {0} minutes.", time);
        }

        [ArenaRegex(@"^\x0312\x02([^\x02]*) \x02releases a holy aura that covers the battlefield and keeps the darkness at bay for an additional\x02 (\d+) turns?\x02\.")]
        internal void OnHolyAuraTurns(object sender, TriggerEventArgs e) {
            this.HolyAuraTurns = short.Parse(e.Match.Groups[2].Value);
            this.DarknessTurns += this.HolyAuraTurns;
            this.HolyAuraUser = e.Match.Groups[1].Value;
            this.HolyAuraEnd = default(DateTime);
            if (this.HolyAuraTurns == 1)
                this.WriteLine(2, 8, "Darkness will be held back for {0} turn.", this.HolyAuraTurns);
            else
                this.WriteLine(2, 8, "Darkness will be held back for {0} turns.", this.HolyAuraTurns);
        }

        [ArenaRegex(@"^\x0312\x02([^\x02]*)\x02's holy aura has faded\. The darkness begins to move towards the battlefield once more\.")]
        internal void OnHolyAuraEnd(object sender, TriggerEventArgs e) {
            this.HolyAuraUser = null;
            this.HolyAuraEnd = default(DateTime);
            this.HolyAuraTurns = -1;
            this.WriteLine(2, 8, "The holy aura fades.");
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02uses all of (?:(his)|(her)|(its)|their) health to perform this technique!")]
        internal void OnSuicide(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            if (victim == null) return;
            Combatant combatant = this.BattleList[victim];
            this.WriteLine(2, 8, "{0} is committing suicide!", combatant.Name);
            if (this.BattleType == BattleType.NPC) return;
            combatant.Presence = Presence.Dead;
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02disappears back into (.*?)'s shadow\.")]
        internal void OnCloneDisappearance(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            if (victim == null) return;
            Combatant combatant = this.BattleList[victim];
            this.WriteLine(2, 8, "{0} disappears.", combatant.Name);
            combatant.Presence = Presence.Dead;
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02fades away\.")]
        internal void OnSummonDisappearance(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            if (victim == null) return;
            Combatant combatant = this.BattleList[victim];
            this.WriteLine(2, 8, "{0} fades away.", combatant.Name);
            combatant.Presence = Presence.Dead;
        }

        [ArenaRegex(@"^\x034\x02([^\x02]*) \x02has run away from the battle!")]
        internal void OnFlight(object sender, TriggerEventArgs e) {
            if (!this.EnableAnalysis) return;
            string victim = GetShortName(e.Match.Groups[1].Value, false, true);
            if (victim == null) return;
            Combatant combatant = this.BattleList[victim];
            this.WriteLine(2, 8, "{0} flees.", combatant.Name);
            combatant.Presence = Presence.RunAway;
        }
#endregion

        public void AICheck() {
            bool act; Character character;

            if (this.Turn == null) return;
            if (this.Turn == this.LoggedIn)
                // My turn
                act = this.EnableParticipation;
            else if (this.Turn == this.LoggedIn + "_clone") {
                // My clone's turn
                if ((character = this.Characters[this.LoggedIn]).CurrentStyle != null &&
                    character.CurrentStyle.Equals("doppelganger", StringComparison.InvariantCultureIgnoreCase))
                    act = this.EnableParticipation;
                else
                    act = false;
            } else if (this.Turn.EndsWith("_clone") && this.Controlling.Contains(this.Turn.Substring(0, this.Turn.Length - 6)) &&
                (character = this.Characters[this.Turn.Substring(0, this.Turn.Length - 6)]).CurrentStyle != null && character.CurrentStyle.Equals("doppelganger", StringComparison.InvariantCultureIgnoreCase)) {
                    // Someone else's clone's turn.
                    act = true;
            } else {
                act = this.Controlling.Contains(this.Turn);
            }

            if (act) this.AITurn();
        }

        public void AITurn() {
            Thread thread = new Thread(this.AI.Turn);
            thread.Start();
        }

#region Battle conclusion
        [ArenaRegex(new string[] { @"^\x034The Battle is Over",
            @"^\x034There were no players to meet the monsters on the battlefield! \x02The battle is over\x02."})]
        internal void OnBattleEnd(object sender, TriggerEventArgs e) {
            this.WriteLine(1, 8, "The battle has ended.");
            this.AI.BattleEnd();

            this.eBattleEnd?.Invoke(this, EventArgs.Empty);

            // Update activity reports.
            var startTime = this.BattleStartTime;
            foreach (var combatant in this.BattleList) {
                if (combatant.Value.Category == Category.Player && this.PlayersEntering.Contains(combatant.Key)) {
                    ActivityReport report;
                    if (!this.ActivityReports.TryGetValue(combatant.Key, out report)) {
                        report = new ActivityReport();
                        this.ActivityReports.Add(combatant.Key, report);
                    }

                    int index = (int) startTime.DayOfWeek * 24 + startTime.Hour;
                    DateTime endTime = DateTime.UtcNow.AddMinutes(3);  // Add three minutes for the time between battles.

                    if (endTime.Hour == startTime.Hour) {
                        report.AddSeconds(index, (int) (endTime - startTime).TotalSeconds);
                    } else {
                        int lastIndex = (int) endTime.DayOfWeek * 24 + endTime.Hour;
                        report.AddSeconds(index, (59 - startTime.Minute) * 60 + 60 - startTime.Second);
                        ++index;
                        while (index != lastIndex) {
                            report.AddSeconds(index, 3600);
                            ++index;
                            if (index == 168) index = 0;
                        }
                        report.AddSeconds(index, endTime.Minute * 60 + endTime.Second);
                    }
                }
            }

            this.ClearBattle();

            // Roll over activity reports.
            foreach (var report in this.ActivityReports.Values) {
                if (DateTime.UtcNow.Month != report.LastCheck.Month)
                    report.RollOver();
                report.LastCheck = DateTime.UtcNow;
            }
        }

        [ArenaRegex(@"^\x034The Battle is Over! \x0312Winner: \[(?:(NPC)|Monster)\]\x02 (.*)")]
        internal void OnBattleEndNPC(object sender, TriggerEventArgs e) {
            float winnerOdds = 1.0F; float loserOdds = 1.0F;
            Character winner = null; Character loser = null;

            this.WriteLine(1, 8, "The battle has ended.");
            this.AI.BattleEnd();

            foreach (Combatant combatant in this.BattleList.Values) {
                Character character = this.Characters[combatant.ShortName];
                ++character.NPCBattles;
                if ((combatant.Category == Category.Ally && e.Match.Groups[1].Success) ||
                    (combatant.Category == Category.Monster && !e.Match.Groups[1].Success)) {
                    winner = character;
                    winnerOdds = combatant.Odds;
                } else {
                    loser = character;
                    loserOdds = combatant.Odds;
                }
            }

            // Award rating points.
            int ratingDifference = winner.Rating - loser.Rating;
            int points = (int) (winnerOdds * 100);

            if (ratingDifference >= 4000)
                points += 100;
            else if (ratingDifference >= 2000)
                points += 200;
            else if (ratingDifference >= 1000)
                points += 300;
            else if (ratingDifference >= 0)
                points += 400;
            else if (ratingDifference >= -1000)
                points += 500;
            else if (ratingDifference >= -2000)
                points += 600;
            else if (ratingDifference >= -4000)
                points += 700;
            else
                points += 800;

            winner.Rating += points;
            loser.Rating -= points;

            this.ClearBattle();
        }

        public void ClearBattle() {
            this.Entering = false;
            this.UnmatchedFullNames.Clear();
            this.UnmatchedShortNames.Clear();
            this.BetAmount = 0;
            this.BetOnAlly = false;
            this.BetTotal = 0;
            this.Turn = null;
            this.TurnAction = null;
            this.TurnAbility = null;
            this.TurnTarget = null;
            this.TurnNumber = 0;
            this.BattleStarted = false;
            this.BattleOpen = false;
            this.BattleStartTime = default(DateTime);
            this.DarknessWarning = default(DateTime);
            this.DarknessTurns = -1;
            this.Darkness = false;
            this.BattleConditions = BattleCondition.None;
            this.number_of_monsters_needed = 0;
            this.HolyAuraEnd = default(DateTime);
            this.HolyAuraTurns = -1;
            this.HolyAuraUser = null;

            // Remove summons from the database.
            foreach (Combatant combatant in this.BattleList.Values) {
                if (combatant.ShortName.EndsWith("_clone") || combatant.ShortName.EndsWith("_summon"))
                    this.Characters.Remove(combatant.ShortName);
            }
            this.BattleList.Clear();
            this.PlayersEntering.Clear();
        }

        [ArenaRegex(@"^\x034\x02Another\x02 wave of monsters has arrived to the battlefield!( \[\x02Gauntlet Round: (\d+)\x02\])?")]
        internal void OnWave(object sender, TriggerEventArgs e) {
            // Remove all existing monsters from the battle list.
            foreach (Combatant combatant in new List<Combatant>(this.BattleList.Values)) {
                if (combatant.Category == Category.Monster) this.BattleList.Remove(combatant.ShortName);
            }
            this.Turn = null;
            this.TurnAction = null;
            this.TurnAbility = null;
            if (e.Match.Groups[1].Success) ++this.Level;
            this.WriteLine(1, 8, "Another wave of monsters draws near!");
        }

        [ArenaRegex(@"^\x02(?!\x03)([^\x02]*) \x02(.*)")]
        internal void OnPortal(object sender, TriggerEventArgs e) {
            // Remove all existing monsters from the battle list.
            foreach (Combatant combatant in new List<Combatant>(this.BattleList.Values)) {
                if (combatant.Category == Category.Monster) this.BattleList.Remove(combatant.ShortName);
            }
        }

        [ArenaRegex(@"^\x0312The forces of good have won this battle \(level\x02 (\d*)\x02\) in (\d*) ?turn\(?s?\)?! \[Current record is: (\d*)\]")]
        internal void OnBattleVictory(object sender, TriggerEventArgs e) {
            this.Level = int.Parse(e.Match.Groups[1].Value) + 1;
        }

        [ArenaRegex(@"^\x0312The forces of evil have won this battle \(level\x02 (\d*)\x02\) after (\d*) ?turn\(?s?\)?! The heroes have lost\x02 (\d*) \x02battle\(s\) in a row!")]
        internal void OnBattleDefeat(object sender, TriggerEventArgs e) {
            this.Level = -int.Parse(e.Match.Groups[3].Value);
        }

        [ArenaRegex(@"^\x03\x02Players\x02 have been rewarded with\x02 (\d+) \x02.* for their (efforts\.|victory!)")]
        internal void OnOrbRewardLegacy(object sender, TriggerEventArgs e) {
            if (this.LoggedIn != null) {
                Character character = this.Characters[this.LoggedIn];
                int orbs = int.Parse(e.Match.Groups[1].Value);
                int orbHunterLevel;
                if (character.EquippedAccessory == "Blood-Ring") orbs = (int) ((float) orbs * 1.1F);
                else if (character.EquippedAccessory == "Blood-Pendant") orbs = (int) ((float) orbs * 1.15F);
                else if (character.EquippedAccessory == "Blood-Crown") orbs = (int) ((float) orbs * 1.2F);
                if (character.Skills.TryGetValue("OrbHunter", out orbHunterLevel))
                    orbs += orbHunterLevel * 15;
                character.RedOrbs += orbs;
            }
        }

        [ArenaRegex(@"^\x033Gambling Winners?: (.*)")]
        internal void OnGamblingReward(object sender, TriggerEventArgs e) {
            foreach (string winner in e.Match.Groups[1].Value.Split(new char[] { ',' })) {
                Character character;
                Match match = Regex.Match(winner, @"^\s*\x02([^\x02]*)\x02\(\$\$(\d*)\)\s*$");
                if (match.Success && this.Characters.TryGetValue(match.Groups[1].Value, out character))
                    character.DoubleDollars += int.Parse(match.Groups[2].Value);
            }
        }
#endregion

        public static float NameMatch(string shortName, string fullName) {
            // Replace underscores with spaces for the comparison.
            shortName = shortName.Replace('_', ' ');

            short score = 0;
            Dictionary<char, int> charPos = new Dictionary<char, int>();
            int pos = -1;

            for (int i = 0; i < fullName.Length; ++i) {
                char c = fullName[i];
                if (pos > -2 && pos + 1 < shortName.Length && char.ToUpperInvariant(c) == char.ToUpperInvariant(shortName[pos + 1])) {
                    score += 2;
                    ++pos;
                } else {
                    int pos3;
                    int pos2 = shortName.IndexOf(c, (charPos.TryGetValue(c, out pos3) ? pos3 : 0) + 1);
                    if (pos2 < 0)
                        pos = -2;
                    else {
                        score += (pos2 == pos + 1) ? (short) 2 : (short) 1;
                        pos = pos2;
                        charPos[c] = pos;
                    }
                }
            }
            return (float) score / ((float) fullName.Length * 2.0F);
        }

        public float AttackMultiplier(short hits) {
            return this.AttackMultiplier(hits, true);
        }
        public float AttackMultiplier(short hits, bool technique) {
            return BattleBotPlugin.AttackMultiplier(hits, true, this.Version >= new ArenaVersion(2, 3, 1));
        }
        public static float AttackMultiplier(short hits, bool technique, bool newVersion) {
            if (technique) {
                if (hits > 8) hits = 8;
            } else {
                if (hits > 6) hits = 6;
            }

            switch (hits) {
                case 2:
                    if (newVersion) return 1F + 1F / 2.1F;
                    return 1F + 1F / 3F;
                case 3:
                    if (newVersion) return 1F + 1F / 2.1F + 1F / 3.2F;
                    return 1F + 1F / 2.1F + 1F / 2.2F;
                case 4:
                    if (newVersion) return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 4.1F;
                    return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 3.9F;
                case 5:
                    return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 4.1F + 1F / 4.9F;
                case 6:
                    return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 4.1F + 1F / 4.9F + 1F / 6.9F;
                case 7:
                    return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 4.1F + 1F / 4.9F + 1F / 6.9F + 1F / 8.9F;
                case 8:
                    return 1F + 1F / 2.1F + 1F / 3.2F + 1F / 4.1F + 1F / 4.9F + 1F / 6.9F + 1F / 8.9F + 1F / 9.9F;
                default:
                    if (technique) return 1F;
                    if (newVersion) return 1F + 1F / 21F;  // The 21 is to account for random double attacks.
                    return 1F + 1F / 30F;
            }
        }

        internal new void LogError(string Procedure, Exception ex) {
            base.LogError(Procedure, ex);
        }

        protected internal void WriteLine(short level, short colour, string text) {
            if (level > this.debugLevel) return;
            foreach (ClientEntry clientEntry in Bot.Clients) {
                IrcClient client = clientEntry.Client;
                if (client.Address == "!Console") {
                    Bot.Say(client, "#", this.Key + " \u0003" + colour + "***\u0003 " + text);
                    return;
                }
            }
        }
        protected internal void WriteLine(short level, short colour, string format, params object[] args) {
            this.WriteLine(level, colour, string.Format(format, args));
        }
    }

	/// <summary>Records what type of list is currently being received.</summary>
	internal enum InfoListType {
		None,
		SkillsPassive,
		SkillsActive,
		SkillsResistances,
		SkillsKiller,
		Weapons,
		Shields,
		Styles,
		Techniques,
		Attributes,
		Equipment
	}

	internal class TaskState {
		public TaskCompletionSource<object> TaskSource { get; } = new TaskCompletionSource<object>();
		public BitVector32 LinesRemaining;
		public int Time;
	}

    [Serializable]
    public class ArenaDisconnectedException : Exception {
        public ArenaDisconnectedException() : this("The connection to the Arena was lost.") { }
        public ArenaDisconnectedException(string message) : base(message) { }
        public ArenaDisconnectedException(string message, Exception inner) : base(message, inner) { }
        protected ArenaDisconnectedException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
