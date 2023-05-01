namespace Pokedex;
public enum Type {
	Normal,
	Fighting,
	Flying,
	Poison,
	Ground,
	Rock,
	Bug,
	Ghost,
	Steel,
	Fire,
	Water,
	Grass,
	Electric,
	Psychic,
	Ice,
	Dragon,
	Dark,
	Fairy,
	/// <summary>The type of Curse (until it became Ghost-type in Generation V) and Weather Ball under Shadow Sky.</summary>
	Mystery,
	/// <summary>The glitch type of one of the MissingNo.</summary>
	Bird
}

public class TypeChart {
	private static readonly int[,] ircColours = new[,] {
		/* Normal   */ {  1,  0,  0 },
		/* Fighting */ {  0,  5,  5 },
		/* Flying   */ {  0, 11, 15 },
		/* Poison   */ {  0,  6,  6 },
		/* Ground   */ {  1,  7,  8 },
		/* Rock     */ {  0,  7,  7 },
		/* Bug      */ {  0,  3,  3 },
		/* Ghost    */ {  0,  2,  2 },
		/* Steel    */ {  0, 15, 15 },
		/* Fire     */ {  0,  4,  4 },
		/* Water    */ {  0, 12, 12 },
		/* Grass    */ {  0,  9,  9 },
		/* Electric */ {  1,  8,  8 },
		/* Psychic  */ {  0, 13, 13 },
		/* Ice      */ {  1, 11, 11 },
		/* Dragon   */ {  0, 12,  4 },
		/* Dark     */ {  0, 14, 14 },
		/* Fairy    */ {  0, 13, 15 },
		/* Mystery  */ {  0, 10, 10 },
		/* Bird     */ {  0, 10, 10 }
	};
	private readonly float[,] matchups = new float[18, 18];

	public static TypeChart Standard { get; } = new(new[,] {
		// Attacking  |  Defending type
		//   type     |  Norm  Figh  Flyi  Pois  Grou  Rock  Bug   Ghos  Stee  Fire  Wate  Gras  Elec  Psyc  Ice   Drag  Dark  Fair
		/* Normal   */ { 1f  , 1f  , 1f  , 1f  , 1f  , 1/2f, 1f  , 0f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Fighting */ { 2f  , 1f  , 1/2f, 1/2f, 1f  , 2f  , 1/2f, 0f  , 2f  , 1f  , 1f  , 1f  , 1f  , 1/2f, 2f  , 1f  , 2f  , 1/2f },
		/* Flying   */ { 1f  , 2f  , 1f  , 1f  , 1f  , 1/2f, 2f  , 1f  , 1/2f, 1f  , 1f  , 2f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Poison   */ { 1f  , 1f  , 1f  , 1/2f, 1/2f, 1/2f, 1f  , 1/2f, 0f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f   },
		/* Ground   */ { 1f  , 1f  , 0f  , 2f  , 1f  , 2f  , 1/2f, 1f  , 2f  , 2f  , 1f  , 1/2f, 2f  , 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Rock     */ { 1f  , 1/2f, 2f  , 1f  , 1/2f, 1f  , 2f  , 1f  , 1/2f, 2f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1f   },
		/* Bug      */ { 1f  , 1/2f, 1/2f, 1/2f, 1f  , 1f  , 1f  , 1/2f, 1/2f, 1/2f, 1f  , 2f  , 1f  , 2f  , 1f  , 1f  , 2f  , 1/2f },
		/* Ghost    */ { 0f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1/2f, 1f   },
		/* Steel    */ { 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1/2f, 1/2f, 1/2f, 1f  , 1/2f, 1f  , 2f  , 1f  , 1f  , 2f   },
		/* Fire     */ { 1f  , 1f  , 1f  , 1f  , 1f  , 1/2f, 2f  , 1f  , 2f  , 1/2f, 1/2f, 2f  , 1f  , 1f  , 2f  , 1/2f, 1f  , 1f   },
		/* Water    */ { 1f  , 1f  , 1f  , 1f  , 2f  , 2f  , 1f  , 1f  , 1f  , 2f  , 1/2f, 1/2f, 1f  , 1f  , 1f  , 1/2f, 1f  , 1f   },
		/* Grass    */ { 1f  , 1f  , 1/2f, 1/2f, 2f  , 2f  , 1/2f, 1f  , 1/2f, 1/2f, 2f  , 1/2f, 1f  , 1f  , 1f  , 1/2f, 1f  , 1f   },
		/* Electric */ { 1f  , 1f  , 2f  , 1f  , 0f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1/2f, 1/2f, 1f  , 1f  , 1/2f, 1f  , 1f   },
		/* Psychic  */ { 1f  , 2f  , 1f  , 2f  , 1f  , 1f  , 1f  , 1f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1/2f, 1f  , 1f  , 0f  , 1f   },
		/* Ice      */ { 1f  , 1f  , 2f  , 1f  , 2f  , 1f  , 1f  , 1f  , 1/2f, 1/2f, 1/2f, 2f  , 1f  , 1f  , 1/2f, 2f  , 1f  , 1f   },
		/* Dragon   */ { 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 0f   },
		/* Dark     */ { 1f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 1f  , 1f  , 1/2f, 1/2f },
		/* Fairy    */ { 1f  , 2f  , 1f  , 1/2f, 1f  , 1f  , 1f  , 1f  , 1/2f, 1/2f, 1f  , 1f  , 1f  , 1f  , 1f  , 2f  , 2f  , 1f   }
	});

	private const float GR  = 0.714f;
	private const float GR2 = 0.51f;
	private const float GW  = 1.4f;

	public static TypeChart PokemonGo { get; } = new(new[,] {
		// Attacking  |  Defending type
		//   type     |  Norm  Figh  Flyi  Pois  Grou  Rock  Bug   Ghos  Stee  Fire  Wate  Gras  Elec  Psyc  Ice   Drag  Dark  Fair
		/* Normal   */ { 1f  , 1f  , 1f  , 1f  , 1f  , GR  , 1f  , GR2 , GR  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Fighting */ { GW  , 1f  , GR  , GR  , 1f  , GW  , GR  , GR2 , GW  , 1f  , 1f  , 1f  , 1f  , GR  , GW  , 1f  , GW  , GR   },
		/* Flying   */ { 1f  , GW  , 1f  , 1f  , 1f  , GR  , GW  , 1f  , GR  , 1f  , 1f  , GW  , GR  , 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Poison   */ { 1f  , 1f  , 1f  , GR  , GR  , GR  , 1f  , GR  , GR2 , 1f  , 1f  , GW  , 1f  , 1f  , 1f  , 1f  , 1f  , GW   },
		/* Ground   */ { 1f  , 1f  , GR2 , GW  , 1f  , GW  , GR  , 1f  , GW  , GW  , 1f  , GR  , GW  , 1f  , 1f  , 1f  , 1f  , 1f   },
		/* Rock     */ { 1f  , GR  , GW  , 1f  , GR  , 1f  , GW  , 1f  , GR  , GW  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , 1f   },
		/* Bug      */ { 1f  , GR  , GR  , GR  , 1f  , 1f  , 1f  , GR  , GR  , GR  , 1f  , GW  , 1f  , GW  , 1f  , 1f  , GW  , GR   },
		/* Ghost    */ { GR2 , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , GR  , 1f   },
		/* Steel    */ { 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , GR  , GR  , GR  , 1f  , GR  , 1f  , GW  , 1f  , 1f  , GW   },
		/* Fire     */ { 1f  , 1f  , 1f  , 1f  , 1f  , GR  , GW  , 1f  , GW  , GR  , GR  , GW  , 1f  , 1f  , GW  , GR  , 1f  , 1f   },
		/* Water    */ { 1f  , 1f  , 1f  , 1f  , GW  , GW  , 1f  , 1f  , 1f  , GW  , GR  , GR  , 1f  , 1f  , 1f  , GR  , 1f  , 1f   },
		/* Grass    */ { 1f  , 1f  , GR  , GR  , GW  , GW  , GR  , 1f  , GR  , GR  , GW  , GR  , 1f  , 1f  , 1f  , GR  , 1f  , 1f   },
		/* Electric */ { 1f  , 1f  , GW  , 1f  , GR2 , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , GR  , GR  , 1f  , 1f  , GR  , 1f  , 1f   },
		/* Psychic  */ { 1f  , GW  , 1f  , GW  , 1f  , 1f  , 1f  , 1f  , GR  , 1f  , 1f  , 1f  , 1f  , GR  , 1f  , 1f  , GR2 , 1f   },
		/* Ice      */ { 1f  , 1f  , GW  , 1f  , GW  , 1f  , 1f  , 1f  , GR  , GR  , GR  , GW  , 1f  , 1f  , GR  , GW  , 1f  , 1f   },
		/* Dragon   */ { 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , GR  , 1f  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , GR2  },
		/* Dark     */ { 1f  , GR  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , 1f  , 1f  , GR  , GR   },
		/* Fairy    */ { 1f  , GW  , 1f  , GR  , 1f  , 1f  , 1f  , 1f  , GR  , GR  , 1f  , 1f  , 1f  , 1f  , 1f  , GW  , GW  , 1f   }
	});

	public TypeChart(float[,] matchups) {
		if (matchups.Rank != 2 || matchups.GetUpperBound(0) != 17 || matchups.GetUpperBound(1) != 17)
			throw new ArgumentException("The array must have dimensions 18 × 18.", nameof(matchups));

		for (int i = 0; i < 18; ++i) {
			for (int j = 0; j < 18; ++j) {
				this.matchups[i, j] = matchups[i, j];
			}
		}
	}
	public TypeChart(IList<IList<float>> matchups) {
		if (matchups.Count != 18)
			throw new ArgumentException("The list must have exactly 18 elements, each containing exactly 18 elements.", nameof(matchups));

		for (int i = 0; i < 18; ++i) {
			if (matchups[i].Count != 18)
				throw new ArgumentException("The list must have exactly 18 elements, each containing exactly 18 elements.", nameof(matchups));

			for (int j = 0; j < 18; ++j) {
				this.matchups[i, j] = matchups[i][j];
			}
		}
	}

	public static string TypeLabel(Type type) {
		int index = (int) type;

		return type == Type.Mystery	? $"\u00030,10 ??? \u000F" :
			index is < 0 or >= 18 ? $"\u00030,10 {type} \u000F" :
			ircColours[index, 1] == ircColours[index, 2] ? $"\u0003{ircColours[index, 0]},{ircColours[index, 1]} {type} \u000F" :
			$"\u0003{ircColours[index, 0]},{ircColours[index, 1]} {type.ToString().Substring(0, type.ToString().Length / 2)}\u0003{ircColours[index, 0]},{ircColours[index, 2]} {type.ToString()[(type.ToString().Length / 2)..]} \u000F";
	}

	/// <summary>
	/// Returns the type effectiveness multiplier that occurs when an attack of the specified type
	/// is used against a Pokémon with the specified defending types.
	/// </summary>
	/// <param name="attackingType">The type of the attack.</param>
	/// <param name="defendingTypes">The types of the defending Pokémon.</param>
	/// <returns>The type effectiveness multiplier (between 1/8 and 8, or 0).</returns>
	public float GetMatchup(Type attackingType, IList<Type> defendingTypes) {
		if (attackingType is < Type.Normal or > Type.Fairy) return 1;

		float result = 1;
		foreach (var defendingType in defendingTypes) {
			if (defendingType is < Type.Normal or > Type.Fairy)
				continue;
			if (this.matchups[(int) attackingType, (int) defendingType] == 0)
				return 0;
			result *= this.matchups[(int) attackingType, (int) defendingType];
		}
		return result;
	}

	/// <summary>
	/// Returns the type effectiveness multiplier that occurs when the move Freeze-Dry
	/// is used against a Pokémon with the specified defending types.
	/// </summary>
	/// <param name="attackingType">The type of the attack.</param>
	/// <param name="defendingTypes">The types of the defending Pokémon.</param>
	/// <returns>The type effectiveness multiplier (between 1/8 and 8, or 0).</returns>
	/// <remarks>
	///     <para>Freeze-Dry is always super effective against the Water type (even in Inverse Battles).</para>
	///     <para>Freeze-Dry may not actually be Ice-type.
	///         If affected by Normalize, for instance, its type changes,
	///         but it's still super effective against the Water type.</para>
	/// </remarks>
	public float FreezeDry(Type attackingType, IList<Type> defendingTypes) {
		if (attackingType is < Type.Normal or > Type.Fairy)
			return defendingTypes.Contains(Type.Water) ? 2 : 1;

		float result = 1;
		foreach (var defendingType in defendingTypes) {
			if (defendingType == Type.Water)
				result *= 2;
			else if (this.matchups[(int) attackingType, (int) defendingType] == 0)
				return 0;
			else
				result *= this.matchups[(int) attackingType, (int) defendingType];
		}
		return result;
	}

	/// <summary>
	/// Returns the type effectiveness multiplier that occurs when the move Flying Press
	/// is used against a Pokémon with the specified defending types.
	/// </summary>
	/// <param name="attackingType">The type of the attack (normally Fighting).</param>
	/// <param name="defendingTypes">The types of the defending Pokémon.</param>
	/// <returns>The type effectiveness multiplier (between 1/16 and 8, or 0).</returns>
	/// <remarks>
	///     <para>Flying Press, though a Fighting-type move, combines the Flying type in its
	///         effectiveness. Thus, it is super effective against both the Dark and Grass types,
	///         but not the Steel type.</para>
	///     <para>Flying Press may not actually be Fighting-type.
	///         For instance, if affected by Electrify, it becomes super effective against
	///         the Water and Bug types, but is no longer so against the Dark type.</para>
	/// </remarks>
	public float FlyingPress(Type attackingType, IList<Type> defendingTypes) {
		var result = this.GetMatchup(attackingType, defendingTypes);
		return result == 0 ? 0 : result * this.GetMatchup(Type.Flying, defendingTypes);
	}
}
