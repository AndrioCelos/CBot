using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pokedex {
	public enum Generation {
		[Description("Pokémon Sword/Shield")]
		SwordShield,
		[Description("Pokémon: Let's Go, Pikachu!/Let's Go, Eevee!")]
		LetsGo,
		[Description("Pokémon Brilliant Diamond/Shining Pearl")]
		BrilliantDiamondShiningPearl
	}
}
