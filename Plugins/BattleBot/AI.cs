using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleBot {
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

    public interface AI {
        void BattleStart();
        void BattleEnd();
        void Turn();
    }

    public class AI2 : AI {
        internal BattleBotPlugin plugin;
        private short ListRefreshCount = 0;
        private string analysisTarget;
        private List<Tuple<Action, string, string, float>> Ratings;
        private List<Combatant> allies;
        private List<Combatant> targets;

        public Func<Combatant, bool> targetCondition { get; }

        public AI2(BattleBotPlugin plugin) {
            this.plugin = plugin;
            this.targetCondition = delegate(Combatant combatant) {
                if (combatant.ShortName == this.plugin.Turn) return false;
                if (this.plugin.BattleType == BattleType.PvP) return true;
                return this.plugin.BattleList[this.plugin.Turn].Category < Category.Monster ^ combatant.Category < Category.Monster;
            };
            this.allies = new List<Combatant>();
            this.targets = new List<Combatant>();
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
            foreach (Combatant combatant in this.plugin.BattleList.Values) {
                if (combatant.Presence == Presence.Alive) {
                    if ((combatant.Category & (Category) 7) == (Category) 7) {
                        this.Act(Action.Attack, null, combatant.ShortName);
                        return;
                    } else if (!this.allies.Contains(combatant) && !this.targets.Contains(combatant)) {
                        if (this.targetCondition(combatant)) {
                            targets.Add(combatant);
                        } else {
                            allies.Add(combatant);
                        }
                    }
                }
            }

            string turn = this.plugin.Turn;

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

            Character character = this.plugin.Characters[this.plugin.Turn];
            int topIndex; List<Tuple<Action, string, string, float>> _Ratings = new List<Tuple<Action, string, string, float>>(this.Ratings);
            // Display the top 10.
            for (int i = 1; i <= 10 && _Ratings.Count > 0; ++i) {
                topIndex = -1;

                // Find the top action.
                for (int j = 0; j < _Ratings.Count; ++j) {
                    if (topIndex < 0 || _Ratings[j].Item4 > _Ratings[topIndex].Item4)
                        topIndex = j;
                }

                string message;
                switch (_Ratings[topIndex].Item1) {
                    case Action.Attack   : message = "Attack        {1,-16} on {2,-16}"; break;
                    case Action.Technique: message = "Technique     {1,-16} on {2,-16}"; break;
                    case Action.Skill    :
                        if (_Ratings[topIndex].Item3 == null)
                            message = "Skill         {1,-16}                    ";
                        else
                            message = "Skill         {1,-16} on {2,-16}";
                        break;
                    default              : message = "{0,-12}  {1,-16} on {2,-16}"; break;
                }
                this.plugin.WriteLine(3, 2, "Top action #{3,2}: " + message + ": {4,6:0.00}", _Ratings[topIndex].Item1, _Ratings[topIndex].Item2, _Ratings[topIndex].Item3, i, _Ratings[topIndex].Item4);
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
                this.Ratings[j] = new Tuple<Action,string,string,float>(this.Ratings[j].Item1, this.Ratings[j].Item2, this.Ratings[j].Item3,
                    this.Ratings[j].Item4 * ((float) this.plugin.RNG.NextDouble() * 0.1F + 0.95F));
                if (topIndex < 0 || (float) this.Ratings[j].Item4 > this.Ratings[topIndex].Item4)
                    topIndex = j;
            }
            // Switch weapons if necessary.
            switch (this.Ratings[topIndex].Item1) {
                case Action.Attack:
                    if (character.EquippedWeapon != this.Ratings[topIndex].Item2) {
                        this.Act(Action.EquipWeapon, null, this.Ratings[topIndex].Item2);
                        System.Threading.Thread.Sleep(600);
                    }
                    break;
                case Action.Technique:
                    Weapon topWeapon = null;
                    foreach (string weaponName in this.CanSwitchWeapon() ?
                        (IEnumerable<string>) character.Weapons.Keys :
                        (IEnumerable<string>) new string[] { character.EquippedWeapon }) {
                        Weapon weapon = this.plugin.Weapons[weaponName];
                        if ((topWeapon == null || weapon.Power > topWeapon.Power) &&
                            weapon.Techniques.Contains(this.Ratings[topIndex].Item2))
                            topWeapon = weapon;
                    }
                    if (character.EquippedWeapon != topWeapon.Name) {
                        this.Act(Action.EquipWeapon, null, topWeapon.Name);
                        System.Threading.Thread.Sleep(600);
                    }
                    break;
            }
            if (this.Ratings[topIndex].Item1 == Action.Skill && this.Ratings[topIndex].Item2 == "Analysis")
                this.analysisTarget = (string) this.Ratings[topIndex].Item3;
            else if (this.Ratings[topIndex].Item1 == Action.Skill && this.Ratings[topIndex].Item2 == "ShadowCopy")
                this.plugin.BattleList[this.plugin.Turn].HasUsedShadowCopy = true;
            this.Act(this.Ratings[topIndex].Item1, this.Ratings[topIndex].Item2, this.Ratings[topIndex].Item3);
        }

        public bool CanSwitchWeapon() {
            if (this.plugin.Turn != this.plugin.LoggedIn && this.plugin.Turn != this.plugin.LoggedIn + "_clone")
                return false;
            if (this.plugin.BattleList[this.plugin.Turn].Status.Contains("weapon locked"))
                return false;
            return true;
        }

        private void ActionCheck() {
            Character character = this.plugin.Characters[this.plugin.Turn];
            Combatant combatant = this.plugin.BattleList[this.plugin.Turn];

            this.Ratings = new List<Tuple<Action,string,string,float>>();

            this.CheckAttacks(character, combatant);
            if ((this.plugin.BattleConditions & BattleCondition.NoTechniques) == 0 && character.Techniques != null)
                this.CheckTechniques(character, combatant);
            this.CheckSkills(character, combatant);
            this.CheckTaunt(character, combatant);
        }

        private void CheckAttacks(Character character, Combatant combatant) {
            foreach (string weaponName in this.CanSwitchWeapon() ?
                (IEnumerable<string>) character.Weapons.Keys :
                (IEnumerable<string>) new string[] { character.EquippedWeapon }) {
                Weapon weapon = this.plugin.Weapons[weaponName];
                float score;

                // Weapon power
                score = weapon.Power + character.Weapons[weapon.Name] * 1.5F;
                // Strength
                score += combatant.STR / (combatant.Status.Contains("strength down") ? 4F : 1F);
                // Hand-to-hand fists level bonus
                if (weapon.Type.Equals("HandToHand", StringComparison.InvariantCultureIgnoreCase))
                    score += character.Weapons["Fists"];

                // Mastery skill bonus
                int masteryLevel;
                switch (weapon.Type.ToUpperInvariant()) {
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

                // Disfavour repeated attacks.
                if (weapon.Name == combatant.LastAction) score /= 2.5F;

                // Check targets.
                foreach (Combatant combatant2 in this.targets) {
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
                    if (combatant2.Character.ElementalResistances != null && combatant2.Character.ElementalResistances.Contains(weapon.Element))
                        targetScore = targetScore * 0.5F - 10F;
                    else if (combatant2.Character.ElementalWeaknesses != null && combatant2.Character.ElementalWeaknesses.Contains(weapon.Element))
                        targetScore = targetScore * 1.5F + 10F;
                    else if (combatant2.Character.ElementalImmunities != null && combatant2.Character.ElementalImmunities.Contains(weapon.Element))
                        continue;
                    else if (combatant2.Character.ElementalAbsorbs != null && combatant2.Character.ElementalAbsorbs.Contains(weapon.Element))
                        continue;

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
                                    if (!combatant2.Status.Contains("under amnesia") && (float) combatant2.INT / (float) combatant2.STR >= 1.5F) targetScore += 25F; break;
                                case "PARALYSIS":
                                    if (!combatant2.Status.Contains("paralyzed")) targetScore += 25F; break;
                                case "ZOMBIE":
                                    if (!combatant2.Status.Contains("a zombie")) targetScore += 6F; break;
                                case "STUN":
                                    if (!combatant2.Status.Contains("stunned")) targetScore += 20F; break;
                                case "CURSE":
                                    if (!combatant2.Status.Contains("cursed") && (float) combatant2.INT / (float) combatant2.STR >= 1.5F) targetScore += 25F; break;
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
                    this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Attack, weapon.Name, combatant2.ShortName, targetScore));
                }
            }
        }

        private void CheckTechniques(Character character, Combatant combatant) {
            if ((this.plugin.BattleConditions & BattleCondition.NoTechniques) != 0) return;

            foreach (string techniqueName in this.CanSwitchWeapon() ?
                (IEnumerable<string>) character.Techniques.Keys :
                (IEnumerable<string>) character.EquippedTechniques) {
                Technique technique = this.plugin.Techniques[techniqueName];
                float score;

                // Do we have enough TP?
                if (!combatant.Status.Contains("conserving TP") && combatant.TP < technique.TP)
                    continue;
                if (technique.Type == TechniqueType.FinalGetsuga)
                    continue;

                // Technique power
                score = technique.Power + character.Techniques[techniqueName] * 1.6F;
                // Character power
                if (technique.UsesINT)
                    score += combatant.INT / (combatant.Status.Contains("int down") ? 4F : 1F);
                else
                    score += combatant.STR / (combatant.Status.Contains("strength down") ? 4F : 1F);
                // Multi-hit attack bonus
                score *= this.plugin.AttackMultiplier(technique.Hits, true);

                // Weather bonus
                if (technique.IsMagic && this.plugin.Weather != null) {
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
                    score /= Math.Max((float) technique.TP / Math.Max(score, 1F), 1F);

                float targetScore; float AoEScore; string targetName = null;
                switch (technique.Type) {
                    case TechniqueType.Attack:
                        foreach (Combatant combatant2 in this.targets) {
                            targetScore = this.CheckTechniqueTarget(character, combatant, technique, combatant2, score);
                            if (targetScore > 0F)
                                this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, combatant2.ShortName, targetScore));
                        }
                        break;
                    case TechniqueType.AoEAttack:
                        AoEScore = 0;
                        foreach (Combatant combatant2 in this.targets) {
                            if (targetName == null) targetName = combatant2.ShortName;
                            targetScore = this.CheckTechniqueTarget(character, combatant, technique, combatant2, score);
                            AoEScore += targetScore * 0.6F;
                        }
                        if (AoEScore > 0F)
                            this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, targetName, AoEScore));
                        break;
                    case TechniqueType.Heal:
                        // Allies
                        foreach (Combatant combatant2 in this.allies) {
                            targetScore = this.CheckTechniqueTargetHeal(character, combatant, technique, combatant2, score);
                            if (targetScore > 0F)
                                this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, combatant2.ShortName, targetScore));
                        }
                        // Enemy zombies
                        foreach (Combatant combatant2 in this.targets) {
                            if (combatant2.Status.Contains("zombie") || combatant2.Character.IsUndead) {
                                targetScore = this.CheckTechniqueTarget(character, combatant, technique, combatant2, score);
                                if (targetScore > 0F)
                                    this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, combatant2.ShortName, targetScore));
                            }
                        }
                        break;
                    case TechniqueType.AoEHeal:
                        AoEScore = 0;
                        foreach (Combatant combatant2 in this.allies) {
                            if (targetName == null) targetName = combatant2.ShortName;
                            targetScore = this.CheckTechniqueTargetHeal(character, combatant, technique, combatant2, score);
                            AoEScore += targetScore * 0.6F;
                        }
                        if (AoEScore > 0F)
                            this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, targetName, AoEScore));
                        break;
                    case TechniqueType.Suicide:
                        targetScore = score;
                        // Disfavour suicides if we're not injured.
                        switch (combatant.Health) {
                            case "Enhanced"     : targetScore *= 0.01F; break;
                            case "Perfect"      : targetScore *= 0.02F; break;
                            case "Great"        : targetScore *= 0.03F; break;
                            case "Good"         : targetScore *= 0.04F; break;
                            case "Decent"       : targetScore *= 0.05F; break;
                            case "Scratched"    : targetScore *= 0.10F; break;
                            case "Bruised"      : targetScore *= 0.15F; break;
                            case "Hurt"         : targetScore *= 0.20F; break;
                            case "Injured"      : targetScore *= 0.30F; break;
                            case "Injured Badly": targetScore *= 0.40F; break;
                            case "Critical"     : targetScore *= 0.30F; break;
                            default: targetScore *= 0.25F; break;
                        }
                        foreach (Combatant combatant2 in this.targets) {
                            targetScore = this.CheckTechniqueTarget(character, combatant, technique, combatant2, targetScore);
                            if (targetScore > 0F)
                                this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, combatant2.ShortName, targetScore));
                        }
                        break;
                    case TechniqueType.AoESuicide:
                        // Disfavour suicides if we're not injured.
                        switch (combatant.Health) {
                            case "Enhanced"     : score *= 0.01F; break;
                            case "Perfect"      : score *= 0.02F; break;
                            case "Great"        : score *= 0.03F; break;
                            case "Good"         : score *= 0.05F; break;
                            case "Decent"       : score *= 0.08F; break;
                            case "Scratched"    : score *= 0.15F; break;
                            case "Bruised"      : score *= 0.20F; break;
                            case "Hurt"         : score *= 0.30F; break;
                            case "Injured"      : score *= 0.45F; break;
                            case "Injured Badly": score *= 0.60F; break;
                            case "Critical"     : score *= 0.40F; break;
                            default: score *= 0.30F; break;
                        }
                        AoEScore = 0;
                        foreach (Combatant combatant2 in this.targets) {
                            if (targetName == null) targetName = combatant2.ShortName;
                            targetScore = this.CheckTechniqueTarget(character, combatant, technique, combatant2, score);
                            AoEScore += targetScore * 0.6F;
                        }
                        if (AoEScore > 0F)
                            this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Technique, technique.Name, targetName, AoEScore));
                        break;
                }
            }
        }

        private float CheckTechniqueTarget(Character character, Combatant combatant, Technique technique, Combatant target, float score) {
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
                targetScore = (technique.UsesINT ? (float) combatant.INT : (float) combatant.STR) / 20F;
            }

            // Check elemental modifiers.
            if (target.Character.ElementalResistances != null && target.Character.ElementalResistances.Contains(technique.Element))
                targetScore = targetScore * 0.5F - 10F;
            else if (target.Character.ElementalWeaknesses != null && target.Character.ElementalWeaknesses.Contains(technique.Element))
                targetScore = targetScore * 1.5F + 10F;
            else if (target.Character.ElementalImmunities != null && target.Character.ElementalImmunities.Contains(technique.Element))
                return 0F;
            else if (target.Character.ElementalAbsorbs != null && target.Character.ElementalAbsorbs.Contains(technique.Element))
                targetScore = -targetScore - 100F;

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

        private float CheckTechniqueTargetHeal(Character character, Combatant combatant, Technique technique, Combatant target, float score) {
            float targetScore = score;
            if (target.Character.IsUndead || target.Status.Contains("zombie"))
                targetScore = -targetScore - 100F;
            if (target.Character.IsElemental && technique.IsMagic)
                targetScore *= 1.3F;

            // Check elemental modifiers.
            if (target.Character.ElementalResistances != null && target.Character.ElementalResistances.Contains(technique.Element))
                targetScore = targetScore * 0.5F - 10F;
            else if (target.Character.ElementalWeaknesses != null && target.Character.ElementalWeaknesses.Contains(technique.Element))
                targetScore = targetScore * 1.5F + 10F;
            else if (target.Character.ElementalImmunities != null && target.Character.ElementalImmunities.Contains(technique.Element))
                return 0F;

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

            float topScore = 0F;
            foreach (Tuple<Action, string, string, float> entry in this.Ratings)
                topScore = Math.Max(topScore, entry.Item4);

            if (character.Skills.ContainsKey("ShadowCopy") && !combatant.HasUsedShadowCopy) {
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
                foreach (Combatant monster in this.targets) {
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
                this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Skill, "ShadowCopy", null, score * factor));
            }

            if ((this.plugin.Turn == this.plugin.LoggedIn || this.plugin.Turn == this.plugin.LoggedIn + "_clone") && this.plugin.ViewingCharacter == null && character.Skills.ContainsKey("Analysis") && character.Skills["Analysis"] >= 4) {
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
                foreach (Combatant monster in this.plugin.BattleList.Values.Where(c => c.Category == Category.Monster && c.ShortName != this.analysisTarget)) {
                    if (!monster.Character.IsWellKnown && !monster.ShortName.StartsWith("evil_", StringComparison.InvariantCultureIgnoreCase))
                        this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Skill, "Analysis", monster.ShortName, score));
                }
            }

        }

        private void CheckTaunt(Character character, Combatant combatant) {
            // Taunt targets that are weak to it.
            foreach (Combatant combatant2 in this.targets) {
                if (combatant2.Character.HurtByTaunt) {
                    this.Ratings.Add(new Tuple<Action, string, string, float>(Action.Taunt, null, combatant2.ShortName, 200));
                }
            }
        }

        public void Act(Action action, string ability, string target) {
            Character character = this.plugin.Characters[this.plugin.Turn];
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
                        technique = this.plugin.Techniques[ability];
                        if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
                            this.plugin.WriteLine(1, 12, "Using technique {1}.", this.plugin.Turn, ability);
                            this.plugin.BattleAction(false, "\u0001ACTION goes " + target + "\u0001");
                        } else {
                            this.plugin.WriteLine(1, 12, "Using technique {1} on {2}.", this.plugin.Turn, ability, target);
                            this.plugin.BattleAction(false, "\u0001ACTION uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + target + "\u0001");
                        }
                        break;
                    case Action.Skill:
                        if (target == null)
                            this.plugin.WriteLine(1, 12, "Using skill {1}.", this.plugin.Turn, ability);
                        else
                            this.plugin.WriteLine(1, 12, "Using skill {1} on {2}.", this.plugin.Turn, ability, target);
                        switch (ability) {
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
                        this.plugin.WriteLine(1, 12, "Using item {1} on {2}.", this.plugin.Turn, ability, target);
                        this.plugin.BattleAction(false, "!use " + ability + " on " + target);
                        break;
                    case Action.EquipWeapon:
                        this.plugin.WriteLine(1, 12, "Switching weapon to {1}.", this.plugin.Turn, target);
                        if (ability == null)
                            this.plugin.BattleAction(false, "!equip " + target);
                        else
                            this.plugin.BattleAction(false, "!equip " + ability + " " + target);
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
                        technique = this.plugin.Techniques[ability];
                        if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
                            this.plugin.WriteLine(1, 12, "[{0}] Using technique {1}.", this.plugin.Turn, ability);
                        } else {
                            this.plugin.WriteLine(1, 12, "[{0}] Using technique {1} on {2}.", this.plugin.Turn, ability, target);
                        }
                        this.plugin.BattleAction(false, "!shadow tech " + technique.Name + " " + target);
                        break;
                    case Action.Skill:
                        if (target == null) {
                            this.plugin.WriteLine(1, 12, "[{0}] Using skill {1}.", this.plugin.Turn, ability);
                            this.plugin.BattleAction(false, "!shadow skill " + ability);
                        } else {
                            this.plugin.WriteLine(1, 12, "[{0}] Using skill {1} on {2}.", this.plugin.Turn, ability, target);
                            this.plugin.BattleAction(false, "!shadow skill " + ability + " " + target);
                        }
                        break;
                    case Action.Item:
                        // TODO: Ignore the parameter for keys, portal items and summon items.
                        this.plugin.WriteLine(1, 12, "[{0}] Using item {1} on {2}.", this.plugin.Turn, ability, target);
                        this.plugin.BattleAction(false, this.plugin.Turn + " uses item " + ability + " on " + target);
                        break;
                    case Action.EquipWeapon:
                        this.plugin.WriteLine(1, 12, "[{0}] Switching weapon to {1}.", this.plugin.Turn, target);
                        if (ability == null)
                            this.plugin.BattleAction(false, "!equip " + target);
                        else
                            this.plugin.BattleAction(false, "!equip " + ability + " " + target);
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
                        technique = this.plugin.Techniques[ability];
                        if (this.plugin.Turn == target && (technique.Type == TechniqueType.Boost || technique.Type == TechniqueType.Buff)) {
                            this.plugin.WriteLine(1, 12, "[{0}] Using technique {1}.", this.plugin.Turn, ability);
                            this.plugin.BattleAction(false, this.plugin.Turn + " uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + this.plugin.Turn);
                        } else {
                            this.plugin.WriteLine(1, 12, "[{0}] Using technique {1} on {2}.", this.plugin.Turn, ability, target);
                            this.plugin.BattleAction(false, this.plugin.Turn + " uses " + character.GenderRefTheir.ToLowerInvariant() + " " + technique.Name + " on " + target);
                        }
                        break;
                    case Action.Skill:
                        if (target == null)
                            this.plugin.WriteLine(1, 12, "[{0}] Using skill {1}.", this.plugin.Turn, ability);
                        else
                            this.plugin.WriteLine(1, 12, "[{0}] Using skill {1} on {2}.", this.plugin.Turn, ability, target);
                        if (ability == null)
                            this.plugin.BattleAction(false, this.plugin.Turn + " does " + ability);
                        else
                            this.plugin.BattleAction(false, this.plugin.Turn + " does " + ability + " " + target);
                        break;
                    case Action.Item:
                        // TODO: Ignore the parameter for keys, portal items and summon items.
                        this.plugin.WriteLine(1, 12, "[{0}] Using item {1} on {2}.", this.plugin.Turn, ability, target);
                        this.plugin.BattleAction(false, this.plugin.Turn + " uses item " + ability + " on " + target);
                        break;
                    case Action.EquipWeapon:
                        this.plugin.WriteLine(1, 12, "[{0}] Switching weapon to {1}.", this.plugin.Turn, target);
                        if (ability == null)
                            this.plugin.BattleAction(false, this.plugin.Turn + " equips " + target);
                        else
                            this.plugin.BattleAction(false, this.plugin.Turn + " equips " + ability + " " + target);
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

            if (action == Action.Technique) this.plugin.TurnAbility = ability;
        }
    }
}
