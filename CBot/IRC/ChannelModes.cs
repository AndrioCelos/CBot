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
        public IEnumerable<char> TypeA => this.modes.Where(mode => mode.Value == 'A').Select(mode => mode.Key);
        /// <summary>The type B modes available. These always take a parameter, but do not include nickname status modes such as o.</summary>
        public IEnumerable<char> TypeB => this.modes.Where(mode => mode.Value == 'B').Select(mode => mode.Key);
        /// <summary>The type C modes available. These always take a parameter except when unset.</summary>
        public IEnumerable<char> TypeC => this.modes.Where(mode => mode.Value == 'C').Select(mode => mode.Key);
        /// <summary>The type D modes available. These never take a parameter.</summary>
        public IEnumerable<char> TypeD => this.modes.Where(mode => mode.Value == 'D').Select(mode => mode.Key);
        /// <summary>The status modes available. These are applied to users on a channel, so always take a parameter.</summary>
        public ReadOnlyCollection<char> Status { get; private set; }

        /// <summary>Returns a dictionary mapping the listed channel modes to their categories ('A' through 'D' or 'S').</summary>
        private ReadOnlyDictionary<char, char> Modes;
        private Dictionary<char, char> modes = new Dictionary<char, char>(32);

        public ChannelModes(IEnumerable<char> typeA, IEnumerable<char> typeB, IEnumerable<char> typeC, IEnumerable<char> typeD, IEnumerable<char> status) {
            if (typeA != null) foreach (var mode in typeA) this.modes[mode] = 'A';
            if (typeB != null) foreach (var mode in typeB) this.modes[mode] = 'B';
            if (typeC != null) foreach (var mode in typeC) this.modes[mode] = 'C';
            if (typeD != null) foreach (var mode in typeD) this.modes[mode] = 'D';
            if (status != null)
                this.setStatusModes(status);
            else
                this.Status = new ReadOnlyCollection<char>(new char[0]);

            this.Modes = new ReadOnlyDictionary<char, char>(this.modes);
        }

        internal void setStatusModes(IEnumerable<char> modes) {
            this.Status = new ReadOnlyCollection<char>(modes.ToArray());
            foreach (var mode in modes) this.modes[mode] = 'S';
        }

        /// <summary>Contains a ChannelModes structure representing the standard modes defined by RFC 1459. These modes are b,k,l,imnpst.</summary>
        public static ChannelModes RFC1459 { get; } = new ChannelModes("b", "k", "l", "imnpst", "ov");
        /// <summary>Contains a ChannelModes structure representing the standard modes defined by RFC 2811. These modes are Ibe,k,l,aimnpqsrt.</summary>
        public static ChannelModes RFC2811 { get; } = new ChannelModes("Ibe", "k", "l", "aimnpqrst", "ov");

        /// <summary>Returns the type of a given mode character, as 'A', 'B', 'C', 'D', 'S' or the null character if the given mode is not listed.</summary>
        /// <param name="mode">The mode character to search for.</param>
        /// <returns>'A', 'B', 'C', 'D' or 'S' if the given mode belongs to the corresponding category, or the null character ('\0') if the given mode is not listed.</returns>
        public char ModeType(char mode) {
            char c;
            if (this.modes.TryGetValue(mode, out c)) return c;
            return '\0';
        }

        public override string ToString() {
            var lists = new[] { new List<char>(), new List<char>(), new List<char>(), new List<char>() };

            foreach (var mode in this.modes) {
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
