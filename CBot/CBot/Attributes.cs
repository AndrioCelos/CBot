using System;

namespace CBot {
    /// <summary>
    /// When attached to a plugin's main class, specifies the version of CBot that the plugin is written for.
    /// CBot will not load plugins with no APIVersionAttribute, or with one not matching CBot.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class APIVersionAttribute : Attribute {
        public Version Version { get; }

        public APIVersionAttribute(Version version) {
            this.Version = version;
        }
        public APIVersionAttribute(string version) {
            this.Version = new Version(version);
        }
        public APIVersionAttribute(int major, int minor) {
            this.Version = new Version(major, minor);
        }
    }

    /// <summary>
    /// Identifies a method in a plugin's main class as a command handler.
    /// CBot will call the method in response to a command from a user on IRC in one of the plugin's assigned channels.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute {
        /// <summary>The names that can be used to make this command.</summary>
        public string[] Names;
        /// <summary>A human-readable example of the syntax of the command.</summary>
        public string Syntax;
        /// <summary>A brief description of the command.</summary>
        public string Description;
        /// <summary>The minimum number of parameters this command can take.</summary>
        public short MinArgumentCount;
        /// <summary>The maximum number of parameters this command can take.</summary>
        public short MaxArgumentCount;
        /// <summary>
        /// The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.
        /// </summary>
        public string Permission;
        /// <summary>
        /// The message that will be given to users who give this command without permission to use it.
        /// Defaults to "You don't have access to this command."
        /// </summary>
        public string NoPermissionsMessage;
        /// <summary>The scopes in which this command can be used.</summary>
        public CommandScope Scope;

        /// <summary>
        /// Creates a CommandAttribute.
        /// </summary>
        /// <param name="name">The name that can be used to make this command.</param>
        /// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
        /// <param name="maxArgumentCount">The maximum number of parameters this command can take.</param>
        /// <param name="syntax">A human-readable example of the syntax of the command.</param>
        /// <param name="description">A brief description of the command.</param>
        /// <param name="permission">The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.</param>
        /// <param name="scope">The scopes in which this command can be used.</param>
        /// <param name="noPermissionMessage">The message that will be given to users who give this command without permission to use it.</param>
        public CommandAttribute(string name, short minArgumentCount, short maxArgumentCount, string syntax, string description,
            string permission = null, CommandScope scope = CommandScope.Channel | CommandScope.PM, string noPermissionMessage = "You don't have access to that command.")
            : this(new string[] { name }, minArgumentCount, maxArgumentCount, syntax, description, permission, scope, noPermissionMessage) { }
        /// <summary>
        /// Creates a CommandAttribute.
        /// </summary>
        /// <param name="names">The names that can be used to make this command.</param>
        /// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
        /// <param name="maxArgumentCount">The maximum number of parameters this command can take.</param>
        /// <param name="syntax">A human-readable example of the syntax of the command.</param>
        /// <param name="description">A brief description of the command.</param>
        /// <param name="permission">The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.</param>
        /// <param name="scope">The scopes in which this command can be used.</param>
        /// <param name="noPermissionMessage">The message that will be given to users who give this command without permission to use it.</param>
        public CommandAttribute(string[] names, short minArgumentCount, short maxArgumentCount, string syntax, string description,
            string permission = null, CommandScope scope = CommandScope.Channel | CommandScope.PM, string noPermissionMessage = "You don't have access to that command.") {
            this.Names = names;
            this.MinArgumentCount = minArgumentCount;
            this.MaxArgumentCount = maxArgumentCount;
            this.Syntax = syntax;
            this.Description = description;
            this.Permission = permission;
            this.Scope = scope;
            this.NoPermissionsMessage = noPermissionMessage;
        }
    }

    /// <summary>
    /// Identifies a method in a plugin's main class as a regex-bound procedure.
    /// CBot will call the method in response to a message matching the regular expression from a user on IRC in one of the plugin's assigned channels.
    /// </summary>
   [AttributeUsage(AttributeTargets.Method)]
    public class RegexAttribute : Attribute {
        /// <summary>The regular expressions that will trigger this procedure.</summary>
        public string[] Expressions;
        /// <summary>
        /// The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.
        /// </summary>
        public string Permission;
        /// <summary>
        /// The message that will be given to users who give this command without permission to use it.
        /// Defaults to "You don't have access to this command."
        /// </summary>
        public string NoPermissionsMessage;
        /// <summary>The scopes in which this procedure can be triggered.</summary>
        public CommandScope Scope;
        /// <summary>If true, the procedure will only trigger if the message starts with the bot's nickname.</summary>
        public bool MustUseNickname;
        
        /// <summary>
        /// Creates a new RegexAttribute.
        /// </summary>
        /// <param name="expression">The regular expression that will trigger this procedure.</param>
        /// <param name="permission">The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.</param>
        /// <param name="scope">The scopes in which this procedure can be triggered.</param>
        /// <param name="mustUseNickname">If true, the procedure will only trigger if the message starts with the bot's nickname.</param>
        /// <param name="noPermissionMessage">The message that will be given to users who give this command without permission to use it.</param>
        public RegexAttribute(string expression, string permission = null, CommandScope scope = CommandScope.Channel | CommandScope.PM, bool mustUseNickname = false,
            string noPermissionMessage = "You don't have access to that command.")
            : this(new string[] { expression }, permission, scope, mustUseNickname, noPermissionMessage) { }
        /// <summary>
        /// Creates a new RegexAttribute.
        /// </summary>
        /// <param name="expressions">The regular expressions that will trigger this procedure.</param>
        /// <param name="permission">The permission required to use the command. A value of null requires no permission.
        /// If this starts with a dot, it will be considered as prefixed with the plugin's key.</param>
        /// <param name="scope">The scopes in which this procedure can be triggered.</param>
        /// <param name="mustUseNickname">If true, the procedure will only trigger if the message starts with the bot's nickname.</param>
        /// <param name="noPermissionMessage">The message that will be given to users who give this command without permission to use it.</param>
        public RegexAttribute(string[] expressions, string permission = null, CommandScope scope = CommandScope.Channel | CommandScope.PM, bool mustUseNickname = false,
            string noPermissionMessage = "You don't have access to that command.") {
            this.Expressions = expressions;
            this.Permission = permission;
            this.Scope = scope;
            this.MustUseNickname = mustUseNickname;
            this.NoPermissionsMessage = noPermissionMessage;
        }
    }
}