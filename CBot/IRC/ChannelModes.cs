using System;
using System.Linq;

namespace IRC {
    /// <summary>
    /// Stores the channel modes available on an IRC server, and the types they belong to.
    /// See http://www.irc.org/tech_docs/draft-brocklesby-irc-isupport-03.txt for more information.
    /// </summary>
    public struct ChannelModes {
        /// <summary>The type A modes available. These are also known as list modes.</summary>
        public char[] TypeA { get; private set; }
        /// <summary>The type B modes available. These always take a parameter, but do not include nickname status modes such as o.</summary>
        public char[] TypeB { get; private set; }
        /// <summary>The type C modes available. These always take a parameter except when unset.</summary>
        public char[] TypeC { get; private set; }
        /// <summary>The type D modes available. These never take a parameter.</summary>
        public char[] TypeD { get; private set; }

        public ChannelModes(char[] typeA, char[] typeB, char[] typeC, char[] typeD)
            : this() {
            this.TypeA = typeA;
            this.TypeB = typeB;
            this.TypeC = typeC;
            this.TypeD = typeD;
        }

        /// <summary>Contains a ChannelModes structure representing the standard modes defined by RFC 2812. These modes are beI,k,l,aimnqpsrt.</summary>
        public readonly static ChannelModes RFC2812 = new ChannelModes(new char[] { 'b', 'e', 'I' },
                                                                       new char[] { 'k' },
                                                                       new char[] { 'l' },
                                                                       new char[] { 'a', 'i', 'm', 'n', 'q', 'p', 's', 'r', 't' });

        /// <summary>Returns the type of a given mode character, as 'A', 'B', 'C', 'D' or the null character if the given mode is not listed.</summary>
        /// <param name="mode">The mode character to search for.</param>
        /// <returns>'A', 'B', 'C', 'D' if the given mode belongs to the corresponding category, or the null character ('\0') if the given mode is not listed.</returns>
        public char ModeType(char mode) {
            if (this.TypeA.Contains(mode)) return 'A';
            if (this.TypeD.Contains(mode)) return 'D';
            if (this.TypeC.Contains(mode)) return 'C';
            if (this.TypeB.Contains(mode)) return 'B';
            return '\0';
        }
    }
}
