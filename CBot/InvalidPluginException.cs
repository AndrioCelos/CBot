namespace CBot;
/// <summary>
/// The exception that is thrown when an attempt is made to load an invalid plugin file.
/// </summary>
public class InvalidPluginException : Exception {
	/// <summary>The file that an attempt was made to load.</summary>
	public string FilePath { get; }

	/// <summary>Creates an InvalidPluginException object with the specified file path.</summary>
	/// <param name="filePath">The file that an attempt was made to load.</param>
	public InvalidPluginException(string filePath) : this(filePath, "An attempt was made to load an invalid plugin file.") { }
	/// <summary>Creates an InvalidPluginException object with the specified file path and detail message.</summary>
	/// <param name="filePath">The file that an attempt was made to load.</param>
	/// <param name="message">A human-readable message giving details on the problem.</param>
	public InvalidPluginException(string filePath, string message) : base(message) => this.FilePath = filePath;
	/// <summary>Creates an InvalidPluginException object with the specified file path, detail message and inner exception.</summary>
	/// <param name="filePath">The file that an attempt was made to load.</param>
	/// <param name="message">A human-readable message giving details on the problem.</param>
	/// <param name="inner">The exception that caused this exception.</param>
	public InvalidPluginException(string filePath, string message, Exception inner) : base(message, inner) => this.FilePath = filePath;
}
