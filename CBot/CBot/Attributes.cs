using System;

namespace CBot {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class APIVersionAttribute : Attribute {
        public Version Version { get; private set; }

        public APIVersionAttribute(Version Version) {
            this.Version = Version;
        }
        public APIVersionAttribute(string Version) {
            this.Version = new Version(Version);
        }
        public APIVersionAttribute(int Major, int Minor) {
            this.Version = new Version(Major, Minor);
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute {
        public string[] Names;
        public string Syntax;
        public string Description;
        public short MinArgumentCount;
        public short MaxArgumentCount;
        public string Permission;
        public string NoPermissionsMessage;
        public CommandScope Scope;

        public CommandAttribute(string Name, short MinArgumentCount, short MaxArgumentCount, string Syntax, string Description, string Permission = null, CommandScope Scope = CommandScope.Channel | CommandScope.PM, string NoPermissionsMessage = "You don't have access to that command.")
            : this(new string[] { Name }, MinArgumentCount, MaxArgumentCount, Syntax, Description, Permission, Scope, NoPermissionsMessage) { }
        public CommandAttribute(string[] Aliases, short MinArgumentCount, short MaxArgumentCount, string Syntax, string Description, string Permission = null, CommandScope Scope = CommandScope.Channel | CommandScope.PM, string NoPermissionsMessage = "You don't have access to that command.") {
            this.Names = Aliases;
            this.MinArgumentCount = MinArgumentCount;
            this.MaxArgumentCount = MaxArgumentCount;
            this.Syntax = Syntax;
            this.Description = Description;
            this.Permission = Permission;
            this.Scope = Scope;
            this.NoPermissionsMessage = NoPermissionsMessage;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RegexAttribute : Attribute {
        public string[] Expressions;
        public string Permission;
        public string NoPermissionsMessage;
        public CommandScope Scope;
        public bool MustUseNickname;

        public RegexAttribute(string Expression, string Permission = null, CommandScope Scope = (CommandScope)3, bool MustUseNickname = false, string NoPermissionsMessage = "You don't have access to that command.")
            : this(new string[] { Expression }, Permission, Scope, MustUseNickname, NoPermissionsMessage) { }
        public RegexAttribute(string[] Expressions, string Permission = null, CommandScope Scope = (CommandScope)3, bool MustUseNickname = false, string NoPermissionsMessage = "You don't have access to that command.") {
            this.Expressions = Expressions;
            this.Permission = Permission;
            this.Scope = Scope;
            this.MustUseNickname = MustUseNickname;
            this.NoPermissionsMessage = NoPermissionsMessage;
        }
    }
}