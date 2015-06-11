using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        MeleeLock = 4,
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
        Siege
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
        public string Description;
    }

    public class OwnCharacter {
        public string FullName;
        public string Password;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class ArenaRegexAttribute : Attribute {
        public string[] Expressions;

        public ArenaRegexAttribute(string expression)
            : this(new string[] { expression }) { }
        public ArenaRegexAttribute(string[] expressions) {
            this.Expressions = expressions;
        }
    }
}