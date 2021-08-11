using System;
using System.Collections.Generic;

namespace BattleBot {
	public class Combatant {
		public string ShortName;
		public string? Name;
		public Character Character;
		public Category Category = Category.Player | Category.Ally | Category.Monster;
		public Presence Presence;
		public bool EnteredSelf;

		public int HP;
		public string Health = "Perfect";
		public int Damage;
		public float DamagePercent;
		public int TP;
		public int STR;
		public int DEF;
		public int INT;
		public int SPD;

		public List<string> Status = new();

		public int TurnNumber;

		public bool IsUnderRoyalGuard;
		public bool IsUnderManaWall;
		public short UtsusemiShadows;
		public bool IsUnderMightyStrike;
		public bool IsUnderElementalSeal;
		public bool IsUnderThirdEye;
		public bool HasUsedShadowCopy;
		public bool HasUsedBloodPact;
		public bool HasUsedScavenge;
		public bool HasUsedMagicShift;

		public string? LastAction;

		public float Odds = 1;

		[Obsolete("Use the other constructor instead.")]
		public Combatant(string shortName) {
			this.ShortName = shortName ?? throw new ArgumentNullException(nameof(shortName));
			this.Character = new(shortName);
		}
		public Combatant(Character character) {
			if (character == null) throw new ArgumentNullException(nameof(character));
			this.ShortName = character.ShortName;
			this.Name = character.Name;
			this.Character = character;
			this.Category = character.Category;
			this.HP = character.BaseHP;
			this.TP = character.BaseTP;
			this.STR = character.BaseSTR;
			this.DEF = character.BaseDEF;
			this.INT = character.BaseINT;
			this.SPD = character.BaseSPD;
			this.IsUnderRoyalGuard = character.IsUnderRoyalGuard;
			this.IsUnderManaWall = character.IsUnderManaWall;
			this.UtsusemiShadows = character.UtsusemiShadows;
			this.IsUnderMightyStrike = character.IsUnderMightyStrike;
			this.IsUnderElementalSeal = character.IsUnderElementalSeal;
			this.IsUnderThirdEye = character.IsUnderThirdEye;
		}
	}
}
