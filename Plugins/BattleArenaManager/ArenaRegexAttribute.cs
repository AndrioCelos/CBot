using System;

namespace BattleArenaManager {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ArenaRegexAttribute : Attribute {
        public string[] Expressions;

        public ArenaRegexAttribute(string expression)
            : this(new string[] { expression }) { }
        public ArenaRegexAttribute(string[] expressions) {
            this.Expressions = expressions;
        }
    }
}
