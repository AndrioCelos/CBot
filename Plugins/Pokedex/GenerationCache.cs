using System.Data.Common;
using System.Data.SQLite;
using System.Text.RegularExpressions;

namespace Pokedex;
internal class GenerationCache : IDisposable {
	internal SQLiteConnection connection;
	internal Dictionary<string, SQLiteCommand> commandCache = new(StringComparer.InvariantCultureIgnoreCase);

	internal readonly HashSet<string> keysPokemon = new(StringComparer.InvariantCultureIgnoreCase);
	internal readonly HashSet<string> keysAbilities = new(StringComparer.InvariantCultureIgnoreCase);
	internal readonly HashSet<string> keysMoves = new(StringComparer.InvariantCultureIgnoreCase);
	internal readonly HashSet<string> keysItems = new(StringComparer.InvariantCultureIgnoreCase);
	internal readonly HashSet<string> keysTags = new(StringComparer.InvariantCultureIgnoreCase);

	private bool isDisposed;

	public GenerationCache(SQLiteConnection connection) => this.connection = connection ?? throw new ArgumentNullException(nameof(connection));

	public async Task ConnectAsync(bool useAbilities) {
		await this.connection.OpenAsync();
		await this.CacheKeysAsync(this.keysPokemon, "SELECT Key FROM Pokemon");
		await this.CacheKeysAsync(this.keysMoves, "SELECT Key FROM Moves");
		await this.CacheKeysAsync(this.keysItems, "SELECT Key FROM Items");
		if (useAbilities) await this.CacheKeysAsync(this.keysAbilities, "SELECT Key FROM Abilities");
	}

	private async Task CacheKeysAsync(HashSet<string> cache, string sql) {
		using var command = this.connection.CreateCommand();
		command.CommandText = sql;
		using var reader = await command.ExecuteReaderAsync();
		cache.UnionWith(reader.Cast<DbDataRecord>().Select(r => r.GetString(0)));
	}

	internal SQLiteCommand GetCommand(string sql) {
		if (!this.commandCache.TryGetValue(sql, out var command)) {
			command = this.connection.CreateCommand();
			command.CommandText = sql;
			foreach (var match in Regex.Matches(sql, @"\$\w+").Cast<Match>()) command.Parameters.Add(new(match.Value, null));
			this.commandCache[sql] = command;
		}
		return command;
	}

	protected virtual void Dispose(bool disposing) {
		if (!isDisposed) {
			foreach (var command in this.commandCache.Values) command.Dispose();
			this.connection.Dispose();

			if (disposing) {
				this.commandCache.Clear();
			}

			isDisposed = true;
		}
	}

	~GenerationCache() => this.Dispose(false);

	public void Dispose() {
		this.Dispose(true);
		GC.SuppressFinalize(this);
	}
}
