using System.Collections.Generic;

namespace BattleBot {
    public class Character {
        public string Name;
        public string ShortName;
        public Category Category;
        public Gender Gender;
        public string Description;

        public int RedOrbs;
        public int BlackOrbs;
        public int AlliedNotes;
        public int DoubleDollars;

        public int BaseHP;
        public int BaseTP;
        public int IgnitionCapacity;
        public int BaseSTR;
        public int BaseDEF;
        public int BaseINT;
        public int BaseSPD;

        public int IgnitionGauge;
        public int RoyalGuardCharge;

        public string EquippedWeapon;
        public string EquippedWeapon2;
        public string EquippedAccessory;

        public List<string> ElementalResistances;
        public List<string> ElementalWeaknesses;
        public List<string> WeaponResistances;
        public List<string> WeaponWeaknesses;
        public List<string> ElementalAbsorbs;
        public List<string> ElementalImmunities;
        public List<string> StatusImmunities;
        public List<string> RegularlyEffective;
        public int EffectivenessKnown;

        public bool IsUndead;
        public bool IsElemental;
        public bool IsSummon;
        public bool IsEthereal;

        public bool AttacksAllies;

        public int TechniqueCount;
        public bool HasIgnition;
        public bool HasMech;
        public int Rating;
        public int NPCBattles;

        public Dictionary<string, int> Weapons;
        public Dictionary<string, int> Techniques;
        public Dictionary<string, int> Skills;
        public Dictionary<string, int> Items;
        public Dictionary<string, int> Styles;
        public Dictionary<string, int> StyleExperience;
        public List<string> Ignitions;

        public string CurrentStyle;

        public bool IsUnderRoyalGuard;
        public bool IsUnderManaWall;
        public short UtsusemiShadows;
        public bool IsUnderMightyStrike;
        public bool IsUnderElementalSeal;
        public bool IsUnderThirdEye;
        public string ShadowCopyName;

        public bool HurtByTaunt;

        public List<string> EquippedTechniques;

        public bool IsWellKnown;
        public bool IsReadyToControl;

        public float Level {
            get {
                return (this.BaseSTR + this.BaseDEF + this.BaseINT + this.BaseSPD * 0.6F) / 18.0F;
            }
        }

        public string GenderRefThey {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "He";
                    case Gender.Female: return "She";
                    case Gender.None: return "It";
                    default: return "They";
                }
            }
        }
        public string GenderRefThem {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "Him";
                    case Gender.Female: return "Her";
                    case Gender.None: return "It";
                    default: return "Them";
                }
            }
        }
        public string GenderRefTheir {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "His";
                    case Gender.Female: return "Her";
                    case Gender.None: return "Its";
                    default: return "Their";
                }
            }
        }
        public string GenderRefTheyHave {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "He has";
                    case Gender.Female: return "She has";
                    case Gender.None: return "It has";
                    default: return "They have";
                }
            }
        }
        public string GenderRefTheyAre {
            get {
                switch (this.Gender) {
                    case Gender.Male: return "He is";
                    case Gender.Female: return "She is";
                    case Gender.None: return "It is";
                    default: return "They are";
                }
            }
        }

    }
}
