using System.Collections.Generic;

namespace BattleBot {
    public class Weapon {
        public string Name;

        public string Type;
        public Size Size;
        public bool IsTwoHanded;
        public int Cost;
        public int UpgradeCost;

        public short HitsMin;
        public short HitsMax;
        public int Power;
        public string Status;
        public string Element;

        public List<string> Techniques;

        public bool IsWellKnown;

        public Weapon() {
            this.Power = -1;
        }
    }
}
