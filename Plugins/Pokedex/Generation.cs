﻿using System.ComponentModel;

namespace Pokedex;
public enum Generation {
	[Description("Pokémon Sword/Shield")]
	SwordShield,
	[Description("Pokémon: Let's Go, Pikachu!/Let's Go, Eevee!")]
	LetsGo,
	[Description("Pokémon Brilliant Diamond/Shining Pearl")]
	BrilliantDiamondShiningPearl
}
