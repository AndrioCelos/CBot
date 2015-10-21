using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleBot {
    public class BattleOpenEventArgs {
        public BattleType Type { get; }
        public TimeSpan Time { get; }
        public bool Enter { get; set; }

        public BattleOpenEventArgs(BattleType type, TimeSpan time) : this(type, time, true) { }
        public BattleOpenEventArgs(BattleType type, TimeSpan time, bool enter) {
            this.Type = type;
            this.Time = time;
            this.Enter = enter;
        }
    }
}
