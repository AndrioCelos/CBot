using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IRC {
    /// <summary>
    /// Represents an IRCv3 capability.
    /// </summary>
    public class IrcCapability {
        public string Name { get; }
        public string Parameter { get; }
        public bool Sticky { get; }
        public bool AckRequired { get; }

        public IrcCapability(string name) : this(name, null, false, false) { }
        public IrcCapability(string name, string parameter) : this(name, parameter, false, false) { }
        public IrcCapability(string name, bool sticky, bool ackRequired) : this(name, null, sticky, ackRequired) { }
        public IrcCapability(string name, string parameter, bool sticky, bool ackRequired) {
            this.Name = name;
            this.Parameter = parameter;
            this.Sticky = sticky;
            this.AckRequired = ackRequired;
        }

        public override int GetHashCode() => this.Name.GetHashCode();
        public override bool Equals(object other) => (other != null && other is IrcCapability && this.Name == ((IrcCapability) other).Name);
    }
}
