using System.Diagnostics.CodeAnalysis;

namespace BattleBot;
public enum Action {
	Attack,
	Taunt,
	Technique,
	Skill,
	Item,
	EquipWeapon,
	Ignition,
	StyleChange,
	Flee
}

public interface IArenaAI {
	void BattleStart();
	void BattleEnd();
	void Turn();
}

public class AI2 : IArenaAI {
	internal BattleBotPlugin plugin;
	private short ListRefreshCount = 0;
	private string? analysisTarget;
	private List<(Action action, string? move, string? target, float score)>? Ratings;
	private readonly List<Combatant> allies = new();
	private readonly List<Combatant> targets = new();

	public Func<Combatant, bool> TargetCondition { get; }

	public AI2(BattleBotPlugin plugin) {
		this.plugin = plugin;
		this.TargetCondition = delegate(Combatant combatant) {
			return combatant.ShortName != this.plugin.Turn &&
				(this.plugin.BattleType == BattleType.PvP ||
					(this.plugin.Turn != null && this.plugin.BattleList[this.plugin.Turn].Category < Category.Monster ^ combatant.Category < Category.Monster));
		};
	}

	public void BattleStart() {
		this.ListRefreshCount = 0;
		this.analysisTarget = null;
	}

	public void BattleEnd() { }

	public void Turn() {
		this.allies.Clear();
		this.targets.Clear();

		// Check for unknown categories on older bots.
		foreach (var combatant in this.plugin.BattleList.Values) {
			if (combatant.Presence == Presence.Alive) {
				if ((combatant.Category & (Category) 7) == (Category) 7) {
					this.Act(Action.Attack, null, combatant.ShortName);
					return;
				} else if (!this.allies.Contains(combatant) && !this.targets.Contains(combatant)) {
					if (this.TargetCondition(combatant)) {
						targets.Add(combatant);
					} else {
						allies.Add(combatant);
					}
				}
			}
		}

		var turn = this.plugin.Turn!;

		// Check that there are targets in the battle.
		if (targets.Count == 0) {
			if (this.ListRefreshCount > 0) {
				this.plugin.BattleAction(false, "There are no targets in the battle!");
				this.ListRefreshCount = 0;
			} else {
				this.ListRefreshCount++;
				this.plugin.BattleAction(false, "!bat info");
			}
			return;
		} else {
			this.ListRefreshCount = 0;
		}

		this.ActionCheck();

		var character = this.plugin.Characters[this.plugin.Turn!];
		int topIndex; var _Ratings = new List<(Action action, string? move, string? target, float score)>(this.Ratings);
		// Display the top 10.
		for (int i = 1; i <= 10 && _Ratings.Count > 0; ++i) {
			topIndex = -1;

			// Find the top action.
			for (int j = 0; j < _Ratings.Count; ++j) {
				if (topIndex < 0 || _Ratings[j].score > _Ratings[topIndex].score)
					topIndex = j;
			}

			string message = _Ratings[topIndex].action switch {
				Action.Attack => "Attack        {1,-16} on {2,-16}",
				Action.Technique => "Technique     {1,-16} on {2,-16}",
				Action.Skill => _Ratings[topIndex].target == null ?
					"Skill         {1,-16}                    " :
					"Skill         {1,-16} on {2,-16}",
				_ => "{0,-12}  {1,-16} on {2,-16}",
			};
			this.plugin.WriteLine(3, 2, "Top action #{3,2}: " + message + ": {4,6:0.00}", _Ratings[topIndex].action, _Ratings[topIndex].move, _Ratings[topIndex].target, i, _Ratings[topIndex].score);
			_Ratings.RemoveAt(topIndex);
		}

		System.Threading.Thread.Sleep(this.plugin.RNG.Next(4000, 7000));
		if (this.plugin.Turn != turn) {
			this.plugin.WriteLine(1, 4, string.Format("Error: the turn has changed too quickly!"));
			return;
		}

		topIndex = -1;
		for (int j = 0; j < this.Ratings.Count; ++j) {
			// Randomise it a little bit.
			this.Ratings[j] = (this.Ratings[j].action, this.Ratings[j].move, this.Ratings[j].target, this.Ratings[j].score * ((float) this.plugin.RNG.NextDouble() * 0.1F + 0.95F));
			if (topIndex < 0 || this.Ratings[j].score > this.Ratings[topIndex].score)
				topIndex = j;
		}
		// Switch weapons if necessary.
		switch (this.Ratings[topIndex].action) {
			case Action.Attack:
				if (character.EquippedWeapon != this.Ratings[topIndex].move) {
					this.Act(Action.EquipWeapon, null, this.Ratings[topIndex].move);
					System.Threading.Thread.Sleep(600);
				}
				break;
			case Action.Technique:
				Weapon? topWeapon = null;
				if (character.Weapons == null && character.EquippedWeapon == null) throw new InvalidOperationException("Don't know the character's weapons?!");
				foreach (string weaponName in this.CanSwitchWeapon() && character.Weapons != null ? (IEnumerable<string>) character.Weapons.Keys : new string[] { character.EquippedWeapon! }) {
					var weapon = this.plugin.Weapons[weaponName];
					if ((topWeapon == null || weapon.Power > topWeapon.Power) &&
						weapon.Techniques != null && weapon.Techniques.Contains(this.Ratings[topIndex].move!))
						topWeapon = weapon;
				}
				if (topWeapon == null) throw new InvalidOperationException($"Can't find a weapon with {this.Ratings[topIndex].move}?!");
				if (character.EquippedWeapon != topWeapon.Name) {
					this.Act(Action.EquipWeapon, null, topWeapon.Name);
					System.Threading.Thread.Sleep(600);
				}
				break;
		}
		if (this.Ratings[topIndex].action == Action.Skill && this.Ratings[topIndex].move == "Analysis")
			this.analysisTarget = this.Ratings[topIndex].target!;
		else if (this.Ratings[topIndex].action == Action.Skill && this.Ratings[topIndex].move == "ShadowCopy")
			this.plugin.BattleList[this.plugin.Turn].HasUsedShadowCopy = true;
		this.Act(this.Ratings[topIndex].action, this.Ratings[topIndex].move, this.Ratings[topIndex].target!);
	}

	public bool CanSwitchWeapon()
		=> (this.plugin.Turn == this.plugin.LoggedIn || this.plugin.Turn == this.plugin.LoggedIn + "_clone") && this.plugin.Turn != null &&
			!this.plugin.BattleList[this.plugin.Turn].Status.Contains("weapon locked");

	[MemberNotNull(nameof(Ratings))]
	private void ActionCheck() {
		if (this.plugin.Turn == null) throw new InvalidOperationException("Don't know whose turn it is?!");
		var character = this.plugin.Characters[this.plugin.Turn];
		var combatant = this.plugin.BattleList[this.plugin.Turn];

		this.Ratings = new();

		this.CheckAttacks(character, combatant);
		if ((this.plugin.BattleConditions & BattleCondition.NoTechniques) == 0 && character.Techniques != null)
			this.CheckTechniques(character, combatant);
		this.CheckSkills(character, combatant);
		this.CheckTaunt(character, combatant);
	}

	private void CheckAttacks(Character character, Combatant combatant) {
		if (character.Weapons == null || character.EquippedWeapon == null) throw new InvalidOperationException("Don't know the character's weapons?!");
		foreach (string weaponName in this.CanSwitchWeapon() ? (IEnumerable<string>) character.Weapons.Keys : new string[] { character.EquippedWeapon! }) {
			var weapon = this.plugin.Weapons[weaponName];
			float score;

			// Weapon power
			score = weapon.Power + (float) Math.Log(character.Weapons[weapon.Name]) * 15;
			// Strength
			score += (float) Math.Log(combatant.STR) * 10 / (combatant.Status.Contains("strength down") ? 4F : 1F);
			// Hand-to-hand fists level bonus
			if ("HandToHand".Equals(weapon.Type, StringComparison.InvariantCultureIgnoreCase))
				score += character.Weapons["Fists"];

			// Mastery skill bonus
			if (character.Skills != null) {
				int masteryLevel;
				switch (weapon.Type?.ToUpperInvariant()) {
					case "HANDTOHAND":
					case "NUNCHUKU":
						if (character.Skills.TryGetValue("MartialArts", out masteryLevel))
							score += masteryLevel;
						break;
					case "KATANA":
					case "SWORD":
					case "GREATSWORD":
						if (character.Skills.TryGetValue("Swordmaster", out masteryLevel))
							score += masteryLevel;
						break;
					case "GUN":
					case "RIFLE":
						if (character.Skills.TryGetValue("Gunslinger", out masteryLevel))
							score += masteryLevel;
						break;
					case "WAND":
					case "STAVE":
					case "GLYPH":
						if (character.Skills.TryGetValue("Wizardry", out masteryLevel))
							score += masteryLevel;
						break;
					case "SPEAR":
						if (character.Skills.TryGetValue("Polemaster", out masteryLevel))
							score += masteryLevel;
						break;
					case "BOW":
						if (character.Skills.TryGetValue("Archery", out masteryLevel))
							score += masteryLevel;
						break;
					case "AXE":
						if (character.Skills.TryGetValue("Hatchetman", out masteryLevel))
							score += masteryLevel;
						break;
					case "SCYTHE":
						if (character.Skills.TryGetValue("Harvester", out masteryLevel))
							score += masteryLevel;
						break;
					case "DAGGER":
						if (character.Skills.TryGetValue("SleightOfHand", out masteryLevel))
							score += masteryLevel;
						break;
					case "WHIP":
						if (character.Skills.TryGetValue("Whipster", out masteryLevel))
							score += masteryLevel;
						break;
				}

				// Desperate Blows
				if (character.Skills.ContainsKey("DesperateBlows")) {
					if (combatant.Health == "Injured Badly") score *= 1.5F;
					else if (combatant.Health == "Critical") score *= 2.0F;
					else if (combatant.Health == "Alive by a hair's bredth") score *= 2.5F;
					else if (combatant.Health == "Alive by a hair's breadth") score *= 2.5F;
				}
			}

			// Disfavour repeated attacks.
			if (weapon.Name == combatant.LastAction) score /= 2.5F;

			// Check targets.
			foreach (var combatant2 in this.targets) {
				if (combatant2.Character.HurtByTaunt) break;

				float targetScore = score;
				if (combatant2.Character.AttacksAllies) targetScore /= 1.5F;
				if (combatant2.Status.Contains("ethereal")) continue;
				if (combatant2.Status.Contains("evolving")) continue;
				if (combatant2.Character.IsElemental) targetScore *= 0.7F;

				// Check the monster level.
				float cRatio = targetScore / Math.Max(this.plugin.Level * 4.3875F, 1F);
				int levelDifference = (int) ((combatant.STR + combatant.DEF + combatant.INT + combatant.SPD * 0.6F) / 18F - this.plugin.Level * (this.plugin.BattleType == BattleType.Boss ? 1.05F : 1F));
				if (levelDifference > 50) levelDifference = 50;
				else if (levelDifference < -50) levelDifference = -50;
				cRatio += 0.05F * levelDifference;
				targetScore *= Math.Min(cRatio, 2F) * 0.65F + 0.7F;

				if (targetScore < 5F) {
					// We're below the damage cap.
					targetScore = weapon.Power + combatant.STR / 2F;
				}

				// Check elemental modifiers.
				if (weapon.Element != null) {
					if (combatant2.Character.ElementalResistances != null && combatant2.Character.ElementalResistances.Contains(weapon.Element))
						targetScore = targetScore * 0.5F - 10F;
					else if (combatant2.Character.ElementalWeaknesses != null && combatant2.Character.ElementalWeaknesses.Contains(weapon.Element))
						targetScore = targetScore * 1.5F + 10F;
					else if (combatant2.Character.ElementalImmunities != null && combatant2.Character.ElementalImmunities.Contains(weapon.Element))
						continue;
					else if (combatant2.Character.ElementalAbsorbs != null && combatant2.Character.ElementalAbsorbs.Contains(weapon.Element))
						continue;
				}

				// Favour weakened targets.
				switch (combatant2.Health) {
					case "Enhanced"     : targetScore *= 0.95F; break;
					case "Perfect"      : targetScore *= 1.00F; break;
					case "Great"        : targetScore *= 1.01F; break;
					case "Good"         : targetScore *= 1.02F; break;
					case "Decent"       : targetScore *= 1.05F; break;
					case "Scratched"    : targetScore *= 1.10F; break;
					case "Bruised"      : targetScore *= 1.15F; break;
					case "Hurt"         : targetScore *= 1.20F; break;
					case "Injured"      : targetScore *= 1.25F; break;
					case "Injured Badly": targetScore *= 1.35F; break;
					case "Critical"     : targetScore *= 1.50F; break;
					case "Alive by a hair's bredth":
					case "Alive by a hair's breadth":
						targetScore *= 1.75F; break;
				}

				// Check for status effects.
				if (weapon.Status != null) {
					foreach (string effect in weapon.Status.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
						switch (effect.ToUpperInvariant()) {
							case "STOP":
								if (!combatant2.Status.Contains("frozen in time")) targetScore += 20F; break;
							case "POISON":
								if (!combatant2.Status.Contains("poisoned heavily")) targetScore += 20F; break;
							case "BLIND":
								if (!combatant2.Status.Contains("blind")) targetScore += 20F; break;
							case "VIRUS":
								if (this.plugin.BattleType == BattleType.Boss && !combatant2.Status.Contains("inflicted with a virus")) targetScore += 10F; break;
							case "AMNESIA":
								if (!combatant2.Status.Contains("under amnesia") && (float) combatant2.INT / combatant2.STR >= 1.5F) targetScore += 25F; break;
							case "PARALYSIS":
								if (!combatant2.Status.Contains("paralyzed")) targetScore += 25F; break;
							case "ZOMBIE":
								if (!combatant2.Status.Contains("a zombie")) targetScore += 6F; break;
							case "STUN":
								if (!combatant2.Status.Contains("stunned")) targetScore += 20F; break;
							case "CURSE":
								if (!combatant2.Status.Contains("cursed") && (float) combatant2.INT / combatant2.STR >= 1.5F) targetScore += 25F; break;
							case "CHARM":
								if (!combatant2.Status.Contains("charmed")) targetScore += 25F; break;
							case "INTIMIDATE":
								if (!combatant2.Status.Contains("intimidated")) targetScore += 20F; break;
							case "PETRIFY":
								if (!combatant2.Status.Contains("petrified")) targetScore += 20F; break;
							case "BORED":
								if (!combatant2.Status.Contains("bored")) targetScore += 25F; break;
							case "CONFUSE":
								if (!combatant2.Status.Contains("confused")) targetScore += 20F; break;
							case "RANDOM":
								targetScore += 15F; break;
						}
					}
				}
				this.Ratings!.Add((Action.Attack, weapon.Name, combatant2.ShortName, targetScore));
			}
		}
	}

	private void CheckTechniques(Character character, Combatant combatant) {
		if ((this.plugin.BattleConditions & BattleCondition.NoTechniques) != 0) return;
		if (this.Ratings == null) throw new InvalidOperationException("Battle state not initialised");

		var techniqueNames = this.CanSwitchWeapon() && character.Techniques != null ? (IEnumerable<string>) character.Techniques.Keys : character.EquippedTechniques;
		if (techniqueNames == null) return;
		foreach (string techniqueName in techniqueNames) {
			var technique = this.plugin.Techniques[techniqueName];
			float score;

			// Do we have enough TP?
			if (!combatant.Status.Contains("conserving TP") && combatant.TP < technique.TP)
				continue;
			if (technique.Type == TechniqueType.FinalGetsuga)
				continue;

			// Technique power
			score = technique.Power + character.Techniques?[techniqueName] ?? 0 * 1.6F;
			// Character power
			if (technique.UsesINT)
				score += (float) Math.Log(combatant.INT) * 10 / (combatant.Status.Contains("int down") ? 4F : 1F);
			else
				score += (float) Math.Log(combatant.STR) * 10 / (combatant.Status.Contains("strength down") ? 4F : 1F);
			// Multi-hit attack bonus
			score *= this.plugin.AttackMultiplier(technique.Hits, true);

			// Weather bonus
			if (technique.Element != null && technique.IsMagic && this.plugin.Weather != null) {
				if (technique.Element.Equals("fire", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("hot", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("ice", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("snowy", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("lightning", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("stormy", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("water", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("rainy", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("wind", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("windy", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("earth", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("dry", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("light", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("bright", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
				else if (technique.Element.Equals("dark", StringComparison.InvariantCultureIgnoreCase) &&
					this.plugin.Weather.Equals("gloomy", StringComparison.InvariantCultureIgnoreCase))
					score *= 1.25F;
			}

			// Disfavour repeated attacks.
			if (technique.Name == combatant.LastAction) score /= 2.5F;

			// Disfavour inefficient techniques.
			if (!combatant.Status.Contains("conserving TP") && technique.TP > 15)
				score /= Math.Max(technique.TP / Math.Max(score, 1F), 1F);

			float targetScore; float AoEScore; string? targetName = null;
			switch (technique.Type) {
				case TechniqueType.Attack:
					foreach (var combatant2 in this.targets) {
						targetScore = this.CheckTechniqueTarget( combatant, technique, combatant2, score);
						if (targetScore > 0F)
							this.Ratings.Add((Action.Technique, technique.Name, combatant2.ShortName, targetScore));
					}
					break;
				case TechniqueType.AoEAttack:
					AoEScore = 0;
					foreach (var combatant2 in this.targets) {
						if (targetName == null) targetName = combatant2.ShortName;
						targetScore = this.CheckTechniqueTarget( combatant, technique, combatant2, score);
						AoEScore += targetScore * 0.6F;
					}
					if (AoEScore > 0F)
						this.Ratings.Add((Action.Technique, technique.Name, targetName, AoEScore));
					break;
				case TechniqueType.Heal:
					// Allies
					foreach (var combatant2 in this.allies) {
						targetScore = CheckTechniqueTargetHeal(character, combatant, technique, combatant2, score);
						if (targetScore > 0F)
							this.Ratings.Add((Action.Technique, technique.Name, combatant2.ShortName, targetScore));
					}
					// Enemy zombies
					foreach (var combatant2 in this.targets) {
						if (combatant2.Status.Contains("zombie") || combatant2.Character.IsUndead) {
							targetScore = this.CheckTechniqueTarget( combatant, technique, combatant2, score);
							if (targetScore > 0F)
								this.Ratings.Add((Action.Technique, technique.Name, combatant2.ShortName, targetScore));
						}
					}
					break;
				case TechniqueType.AoEHeal:
					AoEScore = 0;
					foreach (var combatant2 in this.allies) {
						if (targetName == null) targetName = combatant2.ShortName;
						targetScore = CheckTechniqueTargetHeal(character, combatant, technique, combatant2, score);
						AoEScore += targetScore * 0.6F;
					}
					if (AoEScore > 0F)
						this.Ratings.Add((Action.Technique, technique.Name, targetName, AoEScore));
					break;
				case TechniqueType.Suicide:
					targetScore = score;
					// Disfavour suicides if we're not injured.
					targetScore *= combatant.Health switch {
						"Enhanced" => 0.01F,
						"Perfect" => 0.02F,
						"Great" => 0.03F,
						"Good" => 0.04F,
						"Decent" => 0.05F,
						"Scratched" => 0.10F,
						"Bruised" => 0.15F,
						"Hurt" => 0.20F,
						"Injured" => 0.30F,
						"Injured Badly" => 0.40F,
						"Critical" => 0.30F,
						_ => 0.25F,
					};
					foreach (var combatant2 in this.targets) {
						targetScore = this.CheckTechniqueTarget( combatant, technique, combatant2, targetScore);
						if (targetScore > 0F)
							this.Ratings.Add((Action.Technique, technique.Name, combatant2.ShortName, targetScore));
					}
					break;
				case TechniqueType.AoESuicide:
					// Disfavour suicides if we're not injured.
					score *= combatant.Health switch {
						"Enhanced" => 0.01F,
						"Perfect" => 0.02F,
						"Great" => 0.03F,
						"Good" => 0.05F,
						"Decent" => 0.08F,
						"Scratched" => 0.15F,
						"Bruised" => 0.20F,
						"Hurt" => 0.30F,
						"Injured" => 0.45F,
						"Injured Badly" => 0.60F,
						"Critical" => 0.40F,
						_ => 0.30F,
					};
					AoEScore = 0;
					foreach (var combatant2 in this.targets) {
						if (targetName == null) targetName = combatant2.ShortName;
						targetScore = this.CheckTechniqueTarget(combatant, technique, combatant2, score);
						AoEScore += targetScore * 0.6F;
					}
					if (AoEScore > 0F)
						this.Ratings.Add((Action.Technique, technique.Name, targetName, AoEScore));
					break;
			}
		}
	}

	private float CheckTechniqueTarget(Combatant combatant, Technique technique, Combatant target, float score) {
		if (target.Character.HurtByTaunt) return 0;

		float targetScore = score;
		if (target.Character.AttacksAllies) targetScore /= 1.5F;
		if (target.Status.Contains("ethereal", StringComparer.InvariantCultureIgnoreCase) && !technique.IsMagic) return 0F;
		if (target.Status.Contains("evolving", StringComparer.InvariantCultureIgnoreCase)) return 0F;
		if (target.Character.IsElemental && technique.IsMagic) targetScore *= 1.3F;

		// Check the monster level.
		float cRatio = targetScore / Math.Max(this.plugin.Level * 4.3875F, 1F);
		int levelDifference = (int) ((combatant.STR + combatant.DEF + combatant.INT + combatant.SPD * 0.6F) / 18F - this.plugin.Level * (this.plugin.BattleType == BattleType.Boss ? 1.05F : 1F));
		if (levelDifference > 50) levelDifference = 50;
		else if (levelDifference < -50) levelDifference = -50;
		cRatio += 0.05F * levelDifference;
		targetScore *= Math.Min(cRatio, 2F) * 0.65F + 0.7F;

		if (targetScore < 5F) {
			// We're below the damage cap.
			targetScore = (technique.UsesINT ? (float) combatant.INT : combatant.STR) / 20F;
		}

		// Check elemental modifiers.
		if (technique.Element != null) {
			if (target.Character.ElementalResistances != null && target.Character.ElementalResistances.Contains(technique.Element))
				targetScore = targetScore * 0.5F - 10F;
			else if (target.Character.ElementalWeaknesses != null && target.Character.ElementalWeaknesses.Contains(technique.Element))
				targetScore = targetScore * 1.5F + 10F;
			else if (target.Character.ElementalImmunities != null && target.Character.ElementalImmunities.Contains(technique.Element))
				return 0F;
			else if (target.Character.ElementalAbsorbs != null && target.Character.ElementalAbsorbs.Contains(technique.Element))
				targetScore = -targetScore - 100F;
		}

		// Favour weakened targets.
		switch (target.Health) {
			case "Enhanced"     : targetScore *= 0.95F; break;
			case "Perfect"      : targetScore *= 1.00F; break;
			case "Great"        : targetScore *= 1.01F; break;
			case "Good"         : targetScore *= 1.02F; break;
			case "Decent"       : targetScore *= 1.05F; break;
			case "Scratched"    : targetScore *= 1.10F; break;
			case "Bruised"      : targetScore *= 1.15F; break;
			case "Hurt"         : targetScore *= 1.20F; break;
			case "Injured"      : targetScore *= 1.25F; break;
			case "Injured Badly": targetScore *= 1.35F; break;
			case "Critical"     : targetScore *= 1.50F; break;
			case "Alive by a hair's bredth":
			case "Alive by a hair's breadth":
				targetScore *= 1.75F; break;
		}

		// Check for status effects.
		if (technique.Status != null) {
			foreach (string effect in technique.Status.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries)) {
				switch (effect.ToUpperInvariant()) {
					case "STOP":
						if (!target.Status.Contains("frozen in time")) targetScore += 20F; break;
					case "POISON":
						if (!target.Status.Contains("poisoned heavily")) targetScore += 20F; break;
					case "BLIND":
						if (!target.Status.Contains("blind")) targetScore += 20F; break;
					case "VIRUS":
						if (this.plugin.BattleType == BattleType.Boss && !target.Status.Contains("inflicted with a virus")) targetScore += 10F; break;
					case "AMNESIA":
						if (!target.Status.Contains("under amnesia") && (float) target.INT / (float) target.STR >= 1.5F) targetScore += 25F; break;
					case "PARALYSIS":
						if (!target.Status.Contains("paralyzed")) targetScore += 25F; break;
					case "ZOMBIE":
						if (!target.Status.Contains("a zombie")) targetScore += 6F; break;
					case "STUN":
						if (!target.Status.Contains("stunned")) targetScore += 20F; break;
					case "CURSE":
						if (!target.Status.Contains("cursed") && (float) target.INT / (float) target.STR >= 1.5F) targetScore += 25F; break;
					case "CHARM":
						if (!target.Status.Contains("charmed")) targetScore += 25F; break;
					case "INTIMIDATE":
						if (!target.Status.Contains("intimidated")) targetScore += 20F; break;
					case "PETRIFY":
						if (!target.Status.Contains("petrified")) targetScore += 20F; break;
					case "BORED":
						if (!target.Status.Contains("bored")) targetScore += 25F; break;
					case "CONFUSE":
						if (!target.Status.Contains("confused")) targetScore += 20F; break;
					case "RANDOM":
						targetScore += 15F; break;
				}
			}
		}
		return targetScore;
	}

	private static float CheckTechniqueTargetHeal(Character character, Combatant combatant, Technique technique, Combatant target, float score) {
		float targetScore = score;
		if (target.Character.IsUndead || target.Status.Contains("zombie"))
			targetScore = -targetScore - 100F;
		if (target.Character.IsElemental && technique.IsMagic)
			targetScore *= 1.3F;

		// Check elemental modifiers.
		if (technique.Element != null) {
			if (target.Character.ElementalResistances != null && target.Character.ElementalResistances.Contains(technique.Element))
				targetScore = targetScore * 0.5F - 10F;
			else if (target.Character.ElementalWeaknesses != null && target.Character.ElementalWeaknesses.Contains(technique.Element))
				targetScore = targetScore * 1.5F + 10F;
			else if (target.Character.ElementalImmunities != null && target.Character.ElementalImmunities.Contains(technique.Element))
				return 0F;
		}

		// Favour weakened targets.
		switch (target.Health) {
			case "Enhanced"     : targetScore *= 0.30F; break;
			case "Perfect"      : targetScore *= 0.00F; break;
			case "Great"        : targetScore *= 0.05F; break;
			case "Good"         : targetScore *= 0.10F; break;
			case "Decent"       : targetScore *= 0.15F; break;
			case "Scratched"    : targetScore *= 0.25F; break;
			case "Bruised"      : targetScore *= 0.45F; break;
			case "Hurt"         : targetScore *= 0.70F; break;
			case "Injured"      : targetScore *= 1.00F; break;
			case "Injured Badly": targetScore *= 1.40F; break;
			case "Critical"     : targetScore *= 1.90F; break;
			case "Alive by a hair's bredth":
			case "Alive by a hair's breadth":
				targetScore *= 2.50F; break;
		}
		if (combatant.ShortName == "AlliedForces_President") targetScore *= 1.5F;
		return targetScore;
	}

	private void CheckSkills(Character character, Combatant combatant) {
		if ((this.plugin.BattleConditions & BattleCondition.NoSkills) != 0) return;
		if (this.Ratings == null) throw new InvalidOperationException("Battle state not initialised");

		float topScore = 0F;
		foreach (var (action, move, target, score) in this.Ratings)
			topScore = Math.Max(topScore, score);

		if (character.Skills != null && character.Skills.ContainsKey("ShadowCopy") && !combatant.HasUsedShadowCopy) {
			float factor = 0.7F;
			float score = topScore + 10F;
			switch (combatant.Health) {
				case "Enhanced"     : factor += 0.09F; break;
				case "Perfect"      : factor += 0.03F; break;
				case "Great"        : factor += 0.05F; break;
				case "Good"         : factor += 0.07F; break;
				case "Decent"       : factor += 0.09F; break;
				case "Scratched"    : factor += 0.12F; break;
				case "Bruised"      : factor += 0.15F; break;
				case "Hurt"         : factor += 0.12F; break;
				case "Injured"      : factor += 0.08F; break;
				case "Injured Badly": factor += 0.05F; break;
				case "Critical"     : factor += 0.03F; break;
				case "Alive by a hair's bredth":
				case "Alive by a hair's breadth":
					factor += 0.01F; break;
			}
			foreach (var monster in this.targets) {
				factor -= 0.1F;
				switch (monster.Health) {
					case "Enhanced"     : factor += 0.02F; break;
					case "Perfect"      : factor += 0.03F; break;
					case "Great"        : factor += 0.05F; break;
					case "Good"         : factor += 0.07F; break;
					case "Decent"       : factor += 0.10F; break;
					case "Scratched"    : factor += 0.12F; break;
					case "Bruised"      : factor += 0.15F; break;
					case "Hurt"         : factor += 0.10F; break;
					case "Injured"      : factor += 0.08F; break;
					case "Injured Badly": factor += 0.05F; break;
					case "Critical"     : factor += 0.03F; break;
					case "Alive by a hair's bredth":
					case "Alive by a hair's breadth":
						factor += 0.02F; break;
				}
			}
			this.Ratings.Add((Action.Skill, "ShadowCopy", null, score * factor));
		}

		if ((this.plugin.Turn == this.plugin.LoggedIn || this.plugin.Turn == this.plugin.LoggedIn + "_clone") && character.Skills != null && character.Skills.TryGetValue("Analysis", out var level) && level >= 4) {
			float score = (topScore + 30) / this.targets.Count;
			// We'll disfavour the Analysis skill if we're about to faint.
			switch (combatant.Health) {
				case "Enhanced"     : score += 0.95F; break;
				case "Perfect"      : score += 1.00F; break;
				case "Great"        : score += 1.00F; break;
				case "Good"         : score += 1.00F; break;
				case "Decent"       : score += 0.90F; break;
				case "Scratched"    : score += 0.80F; break;
				case "Bruised"      : score += 0.70F; break;
				case "Hurt"         : score += 0.60F; break;
				case "Injured"      : score += 0.50F; break;
				case "Injured Badly": score += 0.40F; break;
				case "Critical"     : score += 0.30F; break;
				case "Alive by a hair's bredth":
				case "Alive by a hair's breadth":
					score += 0.15F; break;
			}
			foreach (var monster in this.plugin.BattleList.Values.Where(c => c.Category == Category.Monster && c.ShortName != this.analysisTarget)) {
				if (!monster.Character.IsWellKnown && !monster.ShortName.StartsWith("evil_", StringComparison.InvariantCultureIgnoreCase))
					this.Ratings.Add((Action.Skill, "Analysis", monster.ShortName, score));
			}
		}

	}

	private void CheckTaunt(Character character, Combatant combatant) {
		if (this.Ratings == null) throw new InvalidOperationException("Battle state not initialised");

		// Taunt targets that are weak to it.
		foreach (var combatant2 in this.targets) {
			if (combatant2.Character.HurtByTaunt) {
				this.Ratings.Add((Action.Taunt, null, combatant2.ShortName, 200));
			}
		}
	}

	public void Act(Action action, string? move, string? target) {
		var character = this.plugin.Characters[this.plugin.Turn ?? throw new InvalidOperationException("Don't know whose turn it is")];
		Technique technique;

		if (this.plugin.Turn == this.plugin.LoggedIn) {
			// The bot must act.
			switch (action) {
				case Action.Attack:
					this.plugin.WriteLine(1, 12, "Attacking {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, "\u0001ACTION attacks " + target + "\u0001");
					break;
				case Action.Taunt:
					this.plugin.WriteLine(1, 12, "Taunting {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, "\u0001ACTION taunts " + target + "\u0001");
					break;
				case Action.Technique:
					technique = this.plugin.Techniques[move!];
					if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
						this.plugin.WriteLine(1, 12, "Using technique {1}.", this.plugin.Turn, move);
						this.plugin.BattleAction(false, "\u0001ACTION goes " + target + "\u0001");
					} else {
						this.plugin.WriteLine(1, 12, "Using technique {1} on {2}.", this.plugin.Turn, move, target);
						this.plugin.BattleAction(false, "\u0001ACTION uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + target + "\u0001");
					}
					break;
				case Action.Skill:
					if (target == null)
						this.plugin.WriteLine(1, 12, "Using skill {1}.", this.plugin.Turn, move);
					else
						this.plugin.WriteLine(1, 12, "Using skill {1} on {2}.", this.plugin.Turn, move, target);
					switch (move) {
						case "Speed": this.plugin.BattleAction(false, "!Speed"); break;
						case "ElementalSeal"  : this.plugin.BattleAction(false, "!Elemental Seal"); break;
						case "MightyStrike"   : this.plugin.BattleAction(false, "!Mighty Strike"); break;
						case "ManaWall"       : this.plugin.BattleAction(false, "!Mana Wall"); break;
						case "RoyalGuard"     : this.plugin.BattleAction(false, "!Royal Guard"); break;
						case "Sugitekai"      : this.plugin.BattleAction(false, "!Sugitekai"); break;
						case "Meditate"       : this.plugin.BattleAction(false, "!Meditate"); break;
						case "ConserveTP"     : this.plugin.BattleAction(false, "!Conserve TP"); break;
						case "BloodBoost"     : this.plugin.BattleAction(false, "!Blood Boost"); break;
						case "DrainSamba"     : this.plugin.BattleAction(false, "!Drain Samba"); break;
						case "Regen"          : this.plugin.BattleAction(false, "!Regen"); break;
						case "Kikouheni"      : this.plugin.BattleAction(false, "!Kikouheni " + target); break;
						case "ShadowCopy"     : this.plugin.BattleAction(false, "!Shadow Copy"); break;
						case "Utsusemi"       : this.plugin.BattleAction(false, "!Utsusemi"); break;
						case "Steal"          : this.plugin.BattleAction(false, "!Steal " + target); break;
						case "Analysis"       : this.plugin.BattleAction(false, "!Analyze " + target); break;
						case "Cover"          : this.plugin.BattleAction(false, "!Cover " + target); break;
						case "Aggressor"      : this.plugin.BattleAction(false, "!Aggressor"); break;
						case "Defender"       : this.plugin.BattleAction(false, "!Defender"); break;
						case "HolyAura"       : this.plugin.BattleAction(false, "!Holy Aura"); break;
						case "Provoke"        : this.plugin.BattleAction(false, "!Provoke " + target); break;
						case "Disarm"         : this.plugin.BattleAction(false, "!Disarm " + target); break;
						case "WeaponLock"     : this.plugin.BattleAction(false, "!WeaponLock " + target); break;
						case "Konzen-Ittai"   : this.plugin.BattleAction(false, "!Konzen-Ittai"); break;
						case "SealBreak"      : this.plugin.BattleAction(false, "!Seal Break"); break;
						case "MagicMirror"    : this.plugin.BattleAction(false, "!MagicMirror"); break;
						case "Gamble"         : this.plugin.BattleAction(false, "!Gamble"); break;
						case "ThirdEye"       : this.plugin.BattleAction(false, "!Third Eye"); break;
						case "Scavenge"       : this.plugin.BattleAction(false, "!Scavenge"); break;
						case "JustRelease"    : this.plugin.BattleAction(false, "!JustRelease " + target); break;
						case "PerfectDefense" : this.plugin.BattleAction(false, "!Perfect Defense"); break;
						case "FormlessStrikes": this.plugin.BattleAction(false, "!Formless Strikes"); break;
						case "Retaliation"    : this.plugin.BattleAction(false, "!Retaliation"); break;
						default: this.plugin.WriteLine(1, 12, "The AI tried to use an unknown skill: {0}.", target); break;
					}
					break;
				case Action.Item:
					// TODO: Ignore the parameter for keys, portal items and summon items.
					this.plugin.WriteLine(1, 12, "Using item {1} on {2}.", this.plugin.Turn, move, target);
					this.plugin.BattleAction(false, "!use " + move + " on " + target);
					break;
				case Action.EquipWeapon:
					this.plugin.WriteLine(1, 12, "Switching weapon to {1}.", this.plugin.Turn, target);
					if (move == null)
						this.plugin.BattleAction(false, "!equip " + target);
					else
						this.plugin.BattleAction(false, "!equip " + move + " " + target);
					break;
				case Action.StyleChange:
					this.plugin.WriteLine(1, 12, "Changing style to {1}.", this.plugin.Turn, character.Name, target);
					this.plugin.BattleAction(false, "!style change " + target);
					break;
				default:
					this.plugin.WriteLine(1, 12, "The AI tried to perform an unknown action: {0}.", action);
					break;
			}
		} else if (character.CurrentStyle == "Doppelganger" && this.plugin.Turn == this.plugin.LoggedIn + "_clone") {
			// The bot's clone must act.
			switch (action) {
				case Action.Attack:
					this.plugin.WriteLine(1, 12, "[{0}] Attacking {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, "!shadow attack " + target);
					break;
				case Action.Taunt:
					this.plugin.WriteLine(1, 12, "[{0}] Taunting {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, "!shadow taunt " + target);
					break;
				case Action.Technique:
					technique = this.plugin.Techniques[move!];
					if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
						this.plugin.WriteLine(1, 12, "[{0}] Using technique {1}.", this.plugin.Turn, move);
					} else {
						this.plugin.WriteLine(1, 12, "[{0}] Using technique {1} on {2}.", this.plugin.Turn, move, target);
					}
					this.plugin.BattleAction(false, "!shadow tech " + technique.Name + " " + target);
					break;
				case Action.Skill:
					if (target == null) {
						this.plugin.WriteLine(1, 12, "[{0}] Using skill {1}.", this.plugin.Turn, move);
						this.plugin.BattleAction(false, "!shadow skill " + move);
					} else {
						this.plugin.WriteLine(1, 12, "[{0}] Using skill {1} on {2}.", this.plugin.Turn, move, target);
						this.plugin.BattleAction(false, "!shadow skill " + move + " " + target);
					}
					break;
				case Action.Item:
					// TODO: Ignore the parameter for keys, portal items and summon items.
					this.plugin.WriteLine(1, 12, "[{0}] Using item {1} on {2}.", this.plugin.Turn, move, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " uses item " + move + " on " + target);
					break;
				case Action.EquipWeapon:
					this.plugin.WriteLine(1, 12, "[{0}] Switching weapon to {1}.", this.plugin.Turn, target);
					if (move == null)
						this.plugin.BattleAction(false, "!equip " + target);
					else
						this.plugin.BattleAction(false, "!equip " + move + " " + target);
					break;
				case Action.StyleChange:
					this.plugin.WriteLine(1, 12, "[{0}] Changing style to {1}.", this.plugin.Turn, character.Name, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " style change to " + target);
					break;
				default:
					this.plugin.WriteLine(1, 12, "The AI tried to perform an unknown action: {0}.", action);
					break;
			}
		} else {
			// Someone else must act.
			switch (action) {
				case Action.Attack:
					this.plugin.WriteLine(1, 12, "[{0}] Attacking {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " attacks " + target);
					break;
				case Action.Taunt:
					this.plugin.WriteLine(1, 12, "[{0}] Taunting {1}.", this.plugin.Turn, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " taunts " + target);
					break;
				case Action.Technique:
					technique = this.plugin.Techniques[move!];
					if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
						this.plugin.WriteLine(1, 12, "[{0}] Using technique {1}.", this.plugin.Turn, move);
						this.plugin.BattleAction(false, this.plugin.Turn + " uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + this.plugin.Turn);
					} else {
						this.plugin.WriteLine(1, 12, "[{0}] Using technique {1} on {2}.", this.plugin.Turn, move, target);
						this.plugin.BattleAction(false, this.plugin.Turn + " uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + target);
					}
					break;
				case Action.Skill:
					if (target == null)
						this.plugin.WriteLine(1, 12, "[{0}] Using skill {1}.", this.plugin.Turn, move);
					else
						this.plugin.WriteLine(1, 12, "[{0}] Using skill {1} on {2}.", this.plugin.Turn, move, target);
					if (move == null)
						this.plugin.BattleAction(false, this.plugin.Turn + " does " + move);
					else
						this.plugin.BattleAction(false, this.plugin.Turn + " does " + move + " " + target);
					break;
				case Action.Item:
					// TODO: Ignore the parameter for keys, portal items and summon items.
					this.plugin.WriteLine(1, 12, "[{0}] Using item {1} on {2}.", this.plugin.Turn, move, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " uses item " + move + " on " + target);
					break;
				case Action.EquipWeapon:
					this.plugin.WriteLine(1, 12, "[{0}] Switching weapon to {1}.", this.plugin.Turn, target);
					if (move == null)
						this.plugin.BattleAction(false, this.plugin.Turn + " equips " + target);
					else
						this.plugin.BattleAction(false, this.plugin.Turn + " equips " + move + " " + target);
					break;
				case Action.StyleChange:
					this.plugin.WriteLine(1, 12, "[{0}] Changing style to {1}.", this.plugin.Turn, character.Name, target);
					this.plugin.BattleAction(false, this.plugin.Turn + " style change to " + target);
					break;
				default:
					this.plugin.WriteLine(1, 12, "The AI tried to perform an unknown action: {0}.", action);
					break;
			}
		}

		if (action == Action.Technique) this.plugin.TurnAbility = move!;
	}
}
