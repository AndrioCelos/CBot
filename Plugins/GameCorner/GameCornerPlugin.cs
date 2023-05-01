using AnIRC;
using CBot;

namespace GameCorner;
[ApiVersion(4, 0)]
public class GameCornerPlugin : Plugin {
	public Dictionary<string, List<IGame>> RunningGames { get; } = new Dictionary<string, List<IGame>>();

	public override string Name => "Game Corner Base";

	public GameCornerPlugin() {
		if (Instance != null)
			throw new InvalidOperationException("Only one instance of " + nameof(GameCornerPlugin) + " may be loaded.");
		Instance = this;
	}

	public static GameCornerPlugin Instance { get; private set; }

	/// <summary>Returns the first instance of the specified type of game running in the specified venue, or null if there is none.</summary>
	public T GetGame<T>(IrcMessageTarget venue) where T : IGame
		=> this.GetGame<T>(toKey(venue));
	/// <summary>Returns the first instance of the specified type of game running in the specified venue, or null if there is none.</summary>
	public T GetGame<T>(string networkName, string channel) where T : IGame
		=> this.GetGame<T>(toKey(networkName, channel));
	private T GetGame<T>(string key) where T : IGame {
		if (!this.RunningGames.TryGetValue(key, out var list)) return default(T);
		return (T) list.FirstOrDefault(g => g is T);
	}

	public void StartGame(IrcMessageTarget venue, IGame game) => this.StartGame(toKey(venue), game);
	public void StartGame(string networkName, string channel, IGame game) => this.StartGame(toKey(networkName, channel), game);
	private void StartGame(string key, IGame game) {
		List<IGame> games;

		// Check for conflicts.
		if (game.GetType().GetCustomAttributes(typeof(InclusiveAttribute), true).Length == 0) {
			if (this.RunningGames.TryGetValue(key, out games)) {
				foreach (var game2 in games) {
					if (game2.GetType().GetCustomAttributes(typeof(InclusiveAttribute), true).Length == 0)
						throw new InvalidOperationException("An exclusive game of " + game2.Name + " is already in progress.");
				}
			}
		}

		if (this.RunningGames.TryGetValue(key, out games))
			games.Add(game);
		else
			this.RunningGames.Add(key, new List<IGame>() { game });
	}

	public bool GetOrStartGame<T>(IrcMessageTarget venue, out T game) where T : IGame, new()
		=> this.GetOrStartGame(toKey(venue), () => new T(), out game);
	public bool GetOrStartGame<T>(string networkName, string channel, out T game) where T : IGame, new()
		=> this.GetOrStartGame(toKey(networkName, channel), () => new T(), out game);
	public bool GetOrStartGame<T>(IrcMessageTarget venue, Func<T> initializer, out T game) where T : IGame
		=> this.GetOrStartGame(toKey(venue), initializer, out game);
	public bool GetOrStartGame<T>(string networkName, string channel, Func<T> initializer, out T game) where T : IGame
		=> this.GetOrStartGame(toKey(networkName, channel), initializer, out game);
	private bool GetOrStartGame<T>(string key, Func<T> initializer, out T game) where T : IGame {
		game = this.GetGame<T>(key);
		if (game != null) return true;

		game = initializer.Invoke();
		this.StartGame(key, game);
		return false;
	}

	private static string toKey(IrcMessageTarget venue) => venue.Client.NetworkName + "/" + venue.Target;
	private static string toKey(string networkName, string channel) => networkName + "/" + channel;
}
