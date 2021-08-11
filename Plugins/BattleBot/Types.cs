using System;
using System.Linq;
using System.Text.RegularExpressions;
using CBot;

namespace BattleBot {
	public enum Category : short {
		Player = 1,
		Ally = 2,
		Monster = 4
	}

	public enum Gender : short {
		Male,
		Female,
		None,
		Unknown = -1
	}

	public enum Size : short {
		Small = 1,
		Medium = 0,
		Large = 2,
		Other = -1
	}

	public enum Presence : short {
		Alive,
		Dead,
		RunAway
	}

	public enum StatusEffect : short {
		None,
		TimeStop,
		Poison,
		HeavyPoison,
		Silence,
		Blind,
		Drunk,
		Virus,
		Amnesia,
		Paralysis,
		Zombie,
		Slow,
		Stun,
		Curse,
		Charm,
		Intimidate,
		DefenseDown,
		StrengthDown,
		IntDown,
		Petrify,
		Bored,
		Confuse,
		RemoveBoost,
		DefenseUp,
		Random = short.MaxValue
	}

	[Flags]
	public enum BattleCondition {
		None = 0,
		CurseNight = 1,
		BloodMoon = 2,
		NoTechniques = 4,
		ItemLock = 8,
		WeatherLock = 16,
		NoFleeing = 32,
		NoSkills = 64,
		NoQuicksilver = 128,
		NoIgnitions = 256,
		NoPlayerIgnitions = 512,
		NoMech = 1024,
		NoSummons = 2048,
		NoAllies = 4096,
		NoTrusts = 8192,
		NoBattlefieldEvents = 16384,
		EnhanceMelee = 32768,
		EnhanceTechniques = 65536,
		EnhanceItems = 131072
	}

	public enum BattleType : short {
		Normal,
		Boss,
		OrbFountain,
		Gauntlet,
		Mimic,
		NPC,
		President,
		PvP,
		Siege,
		Assault,
		Dungeon,
		DragonHunt,
		Torment
	}

	public enum TechniqueType : short {
		Attack,
		AoEAttack,
		Heal,
		AoEHeal,
		Suicide,
		AoESuicide,
		StealPower,
		Boost,
		FinalGetsuga,
		Buff,
		ClearStatusNegative,
		ClearStatusPositive,
		Unknown = short.MaxValue
	}

	public class UnmatchedName {
		public string Name;
		public Category Category;
		public string? Description;

		public UnmatchedName(string name, Category category) {
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Category = category;
		}
		public UnmatchedName(string name, Category category, string? description) {
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Category = category;
			this.Description = description;
		}
	}

	public class OwnCharacter {
		public string FullName;
		public string Password;

		public OwnCharacter(string fullName, string password) {
			this.FullName = fullName ?? throw new ArgumentNullException(nameof(fullName));
			this.Password = password ?? throw new ArgumentNullException(nameof(password));
		}
	}

	[AttributeUsage(AttributeTargets.Method)]
	public class ArenaTriggerAttributeAttribute : Attribute {
		public Regex[] Patterns { get; set; }
		public bool StripFormats { get; set; }

		public ArenaTriggerAttributeAttribute(string pattern)
			: this(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled) { }
		public ArenaTriggerAttributeAttribute(string pattern, RegexOptions options)
			: this(new[] { new Regex(pattern, options) }) { }
		public ArenaTriggerAttributeAttribute(string pattern, bool stripFormats)
			: this(new[] { new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled) }, stripFormats) { }
		public ArenaTriggerAttributeAttribute(string pattern, RegexOptions options, bool stripFormats)
			: this(new[] { new Regex(pattern, options) }, stripFormats) { }
		public ArenaTriggerAttributeAttribute(string[] pattern)
			: this(pattern.Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToArray()) { }
		public ArenaTriggerAttributeAttribute(string[] pattern, bool stripFormats)
			: this(pattern.Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled)).ToArray(), stripFormats) { }
		public ArenaTriggerAttributeAttribute(Regex[] patterns)
			: this(patterns, false) { }
		public ArenaTriggerAttributeAttribute(Regex[] patterns, bool stripFormats) {
			this.Patterns = patterns;
			this.StripFormats = stripFormats;
		}
	}

	public class ArenaTrigger {
		public ArenaTriggerAttributeAttribute Attribute { get; }
		public PluginTriggerHandler Handler { get; }

		public ArenaTrigger(ArenaTriggerAttributeAttribute attribute, PluginTriggerHandler handler) {
			this.Attribute = attribute;
			this.Handler = handler;
		}
	}
}