using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleBot {
    public class Technique {
        public string Name;
        public string Description;
        public TechniqueType Type;
        public short Hits;

        public bool UsesINT;
        public int Power;
        public string Status;
        public int TP;
        public int Cost;
        public bool IsAoE;
        public bool IsMagic;
        public string Element;

        public bool IsWellKnown;
    }
}
