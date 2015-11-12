using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IRC {
    /// <summary>
    /// Stores the channel modes available on an IRC server, and the types they belong to.
    /// See http://www.irc.org/tech_docs/draft-brocklesby-irc-isupport-03.txt for more information.
    /// </summary>
    public class ChannelModes {
        /// <summary>The type A modes available. These are also known as list modes.</summary>
        public IEnumerable<char> TypeA => this.table.Where(mode => mode.Value == 'A').Select(mode => mode.Key);
        /// <summary>The type B modes available. These always take a parameter, but do not include nickname status modes such as o.</summary>
        public IEnumerable<char> TypeB => this.table.Where(mode => mode.Value == 'B').Select(mode => mode.Key);
        /// <summary>The type C modes available. These always take a parameter except when unset.</summary>
        public IEnumerable<char> TypeC => this.table.Where(mode => mode.Value == 'C').Select(mode => mode.Key);
        /// <summary>The type D modes available. These never take a parameter.</summary>
        public IEnumerable<char> TypeD => this.table.Where(mode => mode.Value == 'D').Select(mode => mode.Key);

        public ReadOnlyDictionary<char, char> Modes;

        private Dictionary<char, char> table = new Dictionary<char, char>(64);

        public ChannelModes(IEnumerable<char> typeA, IEnumerable<char> typeB, IEnumerable<char> typeC, IEnumerable<char> typeD) {
            foreach (var mode in typeA) table[mode] = 'A';
            foreach (var mode in typeB) table[mode] = 'B';
            foreach (var mode in typeC) table[mode] = 'C';
            foreach (var mode in typeD) table[mode] = 'D';

            this.Modes = new ReadOnlyDictionary<char, char>(table);
        }

        /// <summary>Contains a ChannelModes structure representing the standard modes defined by RFC 2812. These modes are Ibe,k,l,aimnqpsrt.</summary>
        public readonly static ChannelModes RFC2812 = new ChannelModes("Ibe", "k", "l", "aimnpqrst");

        /// <summary>Returns the type of a given mode character, as 'A', 'B', 'C', 'D' or the null character if the given mode is not listed.</summary>
        /// <param name="mode">The mode character to search for.</param>
        /// <returns>'A', 'B', 'C', 'D' if the given mode belongs to the corresponding category, or the null character ('\0') if the given mode is not listed.</returns>
        public char ModeType(char mode) {
            char c;
            if (this.table.TryGetValue(mode, out c)) return c;
            return '\0';
        }

        public override string ToString() {
            var lists = new List<char>[] { new List<char>(), new List<char>(), new List<char>(), new List<char>() };

            foreach (var mode in this.table) {
                if (mode.Value >= 'A' && mode.Value <= 'D') lists[mode.Value - 'A'].Add(mode.Key);
            }

            lists[0].Sort();
            lists[1].Sort();
            lists[2].Sort();
            lists[3].Sort();

            return new string(lists[0].ToArray()) + "," + new string(lists[1].ToArray()) + "," + new string(lists[2].ToArray()) + "," + new string(lists[3].ToArray());
        }
    }
}
