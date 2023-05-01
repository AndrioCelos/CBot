using System.Reflection;
using System.Text.RegularExpressions;

namespace CBot; 
/// <summary>
/// When attached to a plugin's main class, specifies the version of CBot that the plugin is written for.
/// CBot will not load plugins with no <see cref="ApiVersionAttribute"/>, or with one not matching CBot.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class ApiVersionAttribute : Attribute {
	public Version Version { get; }

	public ApiVersionAttribute(Version version) => this.Version = version;
	public ApiVersionAttribute(string version) => this.Version = new Version(version);
	public ApiVersionAttribute(int major, int minor) => this.Version = new Version(major, minor);
}

/// <summary>
/// Identifies a method in a plugin's main class as a command handler.
/// CBot will call the method in response to a command from a user on IRC in one of the plugin's assigned channels.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute {
	/// <summary>The names that can be used to make this command.</summary>
	public List<string> Names { get; }
	/// <summary>A human-readable example of the syntax of the command.</summary>
	public string Syntax { get; set; }
	/// <summary>A brief description of the command.</summary>
	public string Description { get; set; }
	/// <summary>The minimum number of parameters this command can take.</summary>
	public short MinArgumentCount { get; set; }
	/// <summary>The maximum number of parameters this command can take.</summary>
	public short MaxArgumentCount { get; set; }
	/// <summary>
	/// The permission required to use the command. A value of null requires no permission.
	/// If this starts with a dot, it will be considered as prefixed with the plugin's key.
	/// </summary>
	public string? Permission { get; set; }
	/// <summary>
	/// The message that will be given to users who give this command without permission to use it.
	/// Defaults to "You don't have access to this command."
	/// </summary>
	public string? NoPermissionsMessage { get; set; } = "You don't have access to this command.";
	/// <summary>The scopes in which this command can be used.</summary>
	public CommandScope Scope { get; set; } = CommandScope.Channel | CommandScope.PM | CommandScope.Global;
	private int priority;
	/// <summary>Sets the priority for the command. The priority is used to choose between ambiguous commands.</summary>
	/// <seealso cref="PriorityHandler"/>
	public int Priority {
		get => this.priority;
		set {
			this.priority = value;
			this.PriorityHandler = e => this.priority;
		}
	}
	private string? priorityHandlerName;
	/// <summary>Sets the name of the priority handler for the command. Should only be set from an attribute parameter using the <c>nameof</c> operator.</summary>
	public string? PriorityHandlerName {
		get => this.priorityHandlerName;
		set {
			this.priorityHandlerName = value;
			if (this.plugin != null) this.SetPriorityHandler();
		}
	}
	/// <summary>Returns or sets a delegate that determines the priority for the command. This property cannot be set as an attribute parameter.</summary>
	public PluginCommandPriorityHandler PriorityHandler { get; set; } = e => 10;

	internal Plugin? plugin;

	/// <summary>Initializes a new <see cref="CommandAttribute"/> with the specified parameters.</summary>
	/// <param name="name">The name that can be used to give this command.</param>
	/// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
	/// <param name="syntax">A human-readable example of the syntax of the command.</param>
	/// <param name="description">A brief description of the command.</param>
	public CommandAttribute(string name, short minArgumentCount, string syntax, string description)
		: this(new List<string>(1) { name }, minArgumentCount, short.MaxValue, syntax, description) { }
	/// <summary>Initializes a new <see cref="CommandAttribute"/> with the specified parameters.</summary>
	/// <param name="names">The names that can be used to give this command.</param>
	/// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
	/// <param name="maxArgumentCount">The maximum number of parameters this command can take.</param>
	/// <param name="syntax">A human-readable example of the syntax of the command.</param>
	/// <param name="description">A brief description of the command.</param>
	public CommandAttribute(string[] names, short minArgumentCount, string syntax, string description)
		: this(new List<string>(names), minArgumentCount, short.MaxValue, syntax, description) { }
	/// <summary>Initializes a new <see cref="CommandAttribute"/> with the specified parameters.</summary>
	/// <param name="name">The name that can be used to give this command.</param>
	/// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
	/// <param name="syntax">A human-readable example of the syntax of the command.</param>
	/// <param name="description">A brief description of the command.</param>
	public CommandAttribute(string name, short minArgumentCount, short maxArgumentCount, string syntax, string description)
		: this(new List<string>(1) { name }, minArgumentCount, maxArgumentCount, syntax, description) { }
	/// <summary>Initializes a new <see cref="CommandAttribute"/> with the specified parameters.</summary>
	/// <param name="names">The names that can be used to give this command.</param>
	/// <param name="minArgumentCount">The minimum number of parameters this command can take.</param>
	/// <param name="maxArgumentCount">The maximum number of parameters this command can take.</param>
	/// <param name="syntax">A human-readable example of the syntax of the command.</param>
	/// <param name="description">A brief description of the command.</param>
	public CommandAttribute(string[] names, short minArgumentCount, short maxArgumentCount, string syntax, string description) 
		: this(new List<string>(names), minArgumentCount, maxArgumentCount, syntax, description) { }
	private CommandAttribute(List<string> names, short minArgumentCount, short maxArgumentCount, string syntax, string description) {
		this.Names = names;
		this.MinArgumentCount = minArgumentCount;
		this.MaxArgumentCount = maxArgumentCount;
		this.Syntax = syntax;
		this.Description = description;
	}

	internal void SetPriorityHandler() {
		if (this.priorityHandlerName is null || this.plugin is null) return;

		const BindingFlags bindingFlags = BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance;
		var method = this.plugin.GetType().GetMethod(this.priorityHandlerName, bindingFlags, null, new[] { typeof(CommandEventArgs) }, null);
		if (method is null) throw new MissingMethodException($"Priority handler {this.priorityHandlerName} was not found");
		this.PriorityHandler = (PluginCommandPriorityHandler) method.CreateDelegate(typeof(PluginCommandPriorityHandler), this.plugin);
	}
}

/// <summary>
/// Identifies a method in a plugin's main class as a trigger.
/// CBot will call the method in response to a message matching the regular expression from a user on IRC in one of the plugin's assigned channels.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class TriggerAttribute : Attribute {
	/// <summary>The regular expressions that will trigger this procedure.</summary>
	public List<Regex> Patterns { get; }
	/// <summary>
	/// The permission required to use the command. A value of null requires no permission.
	/// If this starts with a dot, it will be considered as prefixed with the plugin's key.
	/// </summary>
	public string? Permission { get; set; }
	/// <summary>
	/// The message that will be given to users who give this command without permission to use it.
	/// Defaults to "You don't have access to this command."
	/// </summary>
	public string? NoPermissionsMessage { get; set; }
	/// <summary>The scopes in which this procedure can be triggered.</summary>
	public CommandScope Scope { get; set; } = CommandScope.Channel | CommandScope.PM;
	/// <summary>If true, the procedure will only trigger if the message starts with the bot's nickname.</summary>
	public bool MustUseNickname { get; set; }

	/// <summary>Initializes a new <see cref="TriggerAttribute"/> with the specified data.</summary>
	/// <param name="pattern">The regular expression that will trigger this procedure.</param>
	public TriggerAttribute(string pattern) : this(pattern, RegexOptions.IgnoreCase) { }
	/// <summary>Initializes a new <see cref="TriggerAttribute"/> with the specified data.</summary>
	/// <param name="pattern">The regular expression that will trigger this procedure.</param>
	/// <param name="options">The options to apply to the expression. <see cref="RegexOptions.Compiled"/> is implied. <see cref="RegexOptions.IgnoreCase"/> is the default.</param>
	public TriggerAttribute(string pattern, RegexOptions options)
		=> this.Patterns = new List<Regex>(1) { new Regex(pattern, RegexOptions.Compiled | options) };
	/// <summary>Initializes a new <see cref="TriggerAttribute"/> with the specified data.</summary>
	/// <param name="patterns">The regular expressions that will trigger this procedure.</param>
	public TriggerAttribute(string[] patterns) : this(patterns, RegexOptions.IgnoreCase) { }
	/// <summary>Initializes a new <see cref="TriggerAttribute"/> with the specified data.</summary>
	/// <param name="patterns">The regular expressions that will trigger this procedure.</param>
	/// <param name="options">The options to apply to the expressions. <see cref="RegexOptions.Compiled"/> is implied. <see cref="RegexOptions.IgnoreCase"/> is the default.</param>
	public TriggerAttribute(string[] patterns, RegexOptions options) {
		this.Patterns = new List<Regex>(patterns.Length);
		for (int i = 0; i < patterns.Length; ++i) {
			this.Patterns[i] = new Regex(patterns[i], RegexOptions.Compiled | options);
		}
	}
	/// <summary>Initializes a new <see cref="TriggerAttribute"/> with the specified data.</summary>
	/// <param name="patterns">The regular expressions that will trigger this procedure.</param>
	/// <param name="options">The options to apply to the expressions. <see cref="RegexOptions.Compiled"/> is implied. <see cref="RegexOptions.IgnoreCase"/> is the default.</param>
	public TriggerAttribute(string[] patterns, RegexOptions[] options) {
		if (patterns.Length != options.Length) throw new ArgumentException($"{nameof(patterns)} and {nameof(options)} must have the same length.");
		this.Patterns = new List<Regex>(patterns.Length);
		for (int i = 0; i < patterns.Length; ++i) {
			this.Patterns[i] = new Regex(patterns[i], RegexOptions.Compiled | options[i]);
		}
	}
}