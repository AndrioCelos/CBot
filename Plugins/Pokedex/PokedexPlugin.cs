using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using CBot;

using System.Data.SQLite;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using System.Drawing;
using System.Threading.Channels;
using AnIRC;

namespace Pokedex {
	[ApiVersion(4, 0)]
	public class PokedexPlugin : Plugin {
		public override string Name => "Pokédex";

		private readonly Dictionary<Generation, GenerationCache> cache = new();

		private readonly HashSet<string> keysEggGroups = new(StringComparer.InvariantCultureIgnoreCase) {
			"Monster", "Water1", "Bug", "Flying", "Field", "Fairy", "Grass", "HumanLike", "Water3", "Mineral", "Amorphous", "Water2", "Ditto", "Dragon", "Undiscovered"
		};
		private readonly Dictionary<string, (string name, string? boostedStat, string? reducedStat)> natures = new(StringComparer.InvariantCultureIgnoreCase) {
			{ "Hardy", ("Hardy", null, null) },
			{ "Lonely", ("Lonely", "Attack", "Defense") },
			{ "Brave", ("Brave", "Attack", "Speed") },
			{ "Adamant", ("Adamant", "Attack", "Special Attack") },
			{ "Naughty", ("Naughty", "Attack", "Special Defense") },
			{ "Bold", ("Bold", "Defense", "Attack") },
			{ "Docile", ("Docile", null, null) },
			{ "Relaxed", ("Relaxed", "Defense", "Speed") },
			{ "Impish", ("Impish", "Defense", "Special Attack") },
			{ "Lax", ("Lax", "Defense", "Special Defense") },
			{ "Timid", ("Timid", "Speed", "Attack") },
			{ "Hasty", ("Hasty", "Speed", "Defense") },
			{ "Serious", ("Serious", null, null) },
			{ "Jolly", ("Jolly", "Speed", "Special Attack") },
			{ "Naive", ("Naive", "Speed", "Special Defense") },
			{ "Modest", ("Modest", "Special Attack", "Attack") },
			{ "Mild", ("Mild", "Special Attack", "Defense") },
			{ "Quiet", ("Quiet", "Special Attack", "Speed") },
			{ "Bashful", ("Bashful", null, null) },
			{ "Rash", ("Rash", "Special Attack", "Special Defense") },
			{ "Calm", ("Calm", "Special Defense", "Attack") },
			{ "Gentle", ("Gentle", "Special Defense", "Defense") },
			{ "Sassy", ("Sassy", "Special Defense", "Speed") },
			{ "Careful", ("Careful", "Special Defense", "Special Attack") },
			{ "Quirky", ("Quirky", null, null) },
		};

		public override void Initialize() {
			_ = this.PreloadCache();
		}
		private async Task PreloadCache() {
			var entry = await this.GetCacheEntryAsync(Generation.SwordShield);
			entry.keysTags.Add("Spooky");
		}
		private async Task<GenerationCache> GetCacheEntryAsync(Generation generation) {
			if (this.cache.TryGetValue(generation, out var cacheData)) return cacheData;
			cacheData = new(new(new SQLiteConnectionStringBuilder() {
				DataSource = Path.Combine("data", this.Key, generation switch {
					Generation.SwordShield => "swsh.db",
					Generation.LetsGo => "lgpe.db",
					Generation.BrilliantDiamondShiningPearl => "bdsp.db",
					_ => throw new ArgumentException("Invalid generation", nameof(generation))
				})
			}.ToString()));
			this.cache[generation] = cacheData;
			await cacheData.ConnectAsync(generation != Generation.LetsGo);
			return cacheData;
		}

		public override void OnUnload() {
			foreach (var entry in this.cache.Values) entry.Dispose();
			this.cache.Clear();
		}

		private async Task<SQLiteCommand> GetCommandAsync(Generation generation, string sql)
			=> (await this.GetCacheEntryAsync(generation)).GetCommand(sql);
		private async Task<SQLiteCommand> GetCommandAsync(Generation generation, string sql, params (string parameterName, object? value)[] parameterValues) {
			var command = await this.GetCommandAsync(generation, sql);
			foreach (var (name, value) in parameterValues) command.Parameters["$" + name].Value = value;
			return command;
		}

		private static readonly (string word, string replacement)[] prefixWords = new[] {
			("Primal", "Primal"),
			("Gigantamax", "Gmax"),
			("Gmax", "Gmax"),
			("Eternamax", "Eternamax"),
			("Kantonian", ""),
			("Kanto", ""),
			("Hoennian", ""),
			("Hoenn", ""),
			("Unovan", ""),
			("Unova", ""),
			("Alolan", "Alola"),
			("Alola", "Alola"),
			("Galarian", "Galar"),
			("Galar", "Galar"),
			("SingleStrike", "SingleStrike"),
			("RapidStrike", "RapidStrike"),
		};
		private Task<string> GetKeyAsync(Generation gen, string input) => this.GetKeyAsync(gen, input, true);
		private async Task<string> GetKeyAsync(Generation gen, string input, bool checkAliases) {
			var key = GetKeySimple(input);
			if (!checkAliases) return key;

			var command = await this.GetCommandAsync(gen, "SELECT Value FROM Aliases WHERE Key = $Key", ("Key", key));
			var result = (string) command.ExecuteScalar();
			return result != null ? GetKeySimple(result) : key;
		}

		private static string GetKeySimple(string input) {
			var key = Regex.Replace(input.Replace('é', 'e'), @"\W", "");

			if (key.StartsWith("Mega", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("Meganium", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("MegaDrain", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("MegaHorn", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("MegaKick", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("MegaPunch", StringComparison.InvariantCultureIgnoreCase)
				&& !key.Equals("MegaLauncher", StringComparison.InvariantCultureIgnoreCase))
				key = key[^1] is 'X' or 'x' or 'Y' or 'y' ? key[4..^1] + "Mega" + key[^1] : key[4..] + "Mega";
			else {
				var replaced = false;
				foreach (var (word, replacement) in prefixWords) {
					if (key.StartsWith(word, StringComparison.InvariantCultureIgnoreCase)) {
						key = key[word.Length..] + (replacement ?? word);
						replaced = true;
						break;
					}
				}
				if (!replaced) key = Regex.Replace(key, @"(?<!Cast)Forme?(?=.)", "$'$`");
			}
			return key;
		}

		private async Task<IList<Type>> GetTypesAsync(Generation gen, string pokemon) {
			using var reader = await (await this.GetCommandAsync(gen, "SELECT Type FROM PokemonTypes WHERE Pokemon = $Key ORDER BY Slot", ("Key", pokemon))).ExecuteReaderAsync();
			return reader.Cast<DbDataRecord>().Select(r => Enum.Parse<Type>(r.GetString("Type")!)).ToList();
		}
		private async Task<string> FormatTypes(Generation gen, string pokemon, bool emotesOnly)
			=> this.FormatTypes(await this.GetTypesAsync(gen, pokemon), emotesOnly);
		private string FormatTypes(IEnumerable<Type> types, bool emotesOnly) {
			Type? firstType = null;

			var typesString = string.Join(emotesOnly ? "" : " / ", types.Select(t => {
				firstType ??= t;
				return this.FormatType(t, emotesOnly);
			}));

			return typesString;
		}
		private string FormatType(Type type, bool emoteOnly)
			=> emoteOnly ? "" : type.ToString();

		[Command(new[] { "stats" }, 1, 1, "stats <species>[/form]", "Shows information about a Pokémon form.")]
		public async void CommandStats(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			var search = await this.GetKeyAsync(gen, e.Parameters[0]);
			var command = await this.GetCommandAsync(gen, "SELECT Name, Form, BaseHP, BaseAttack, BaseDefense, BaseSpecialAttack, BaseSpecialDefense, BaseSpeed FROM Pokemon WHERE Key = $Key",
				("Key", search));

			using var reader = await command.ExecuteReaderAsync();
			if (!await reader.ReadAsync()) {
				e.Fail("No such Pokémon was found.");
				return;
			}

			e.Reply($"Base stats: HP {reader.GetInt64(reader.GetOrdinal("BaseHP"))}; Atk {reader.GetInt64(reader.GetOrdinal("BaseAttack"))}; Def {reader.GetInt64(reader.GetOrdinal("BaseDefense"))}; SpA {reader.GetInt64(reader.GetOrdinal("BaseSpecialAttack"))}; SpD {reader.GetInt64(reader.GetOrdinal("BaseSpecialDefense"))}; Spe {reader.GetInt64(reader.GetOrdinal("BaseSpeed"))}");
		}

		private Generation GetGeneration(IrcChannel? channel) {
			return channel == null ? Generation.BrilliantDiamondShiningPearl
				: channel.Client.Address?.Contains("youtube") ?? false ? Generation.BrilliantDiamondShiningPearl : Generation.BrilliantDiamondShiningPearl;
		}

		[Command(new[] { "data" }, 1, 1, "data <species>[/form]", "Shows information about a Pokémon, move, Ability or item.")]
		public async void CommandData(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			var cacheEntry = await this.GetCacheEntryAsync(gen);
			var search = await this.GetKeyAsync(gen, e.Parameters[0]);
			(bool success, string[] messages) result = this.natures.TryGetValue(search, out var natureDetails)
				? (true, new[] { $"{natureDetails.name} nature:"
					+ natureDetails.boostedStat == null || natureDetails.reducedStat == null
						? "No effect."
						: $"Boosts {natureDetails.boostedStat} and reduces {natureDetails.reducedStat}." })
				: cacheEntry.keysPokemon.Contains(search) ? await checkPokemonForm()
				: cacheEntry.keysAbilities.Contains(search) ? await checkAbility()
				: cacheEntry.keysMoves.Contains(search) ? await checkMove(search)
				: cacheEntry.keysItems.Contains(search) ? await checkItem()
				: (gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysPokemon.Contains(search)) ? (false, new[] { "This Pokémon is not present in the specified games." })
				: (gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysAbilities.Contains(search)) ? (false, new[] { "This Ability is not present in the specified games." })
				: (gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysMoves.Contains(search)) ? (false, new[] { "This move is not present in the specified games." })
				: (gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysItems.Contains(search)) ? (false, new[] { "This item is not present in the specified games." })
				: (false, new[] { $"I don't have anything matching '{search}'." });
			if (result.success)
				foreach (var message in result.messages) e.Reply(message);
			else
				foreach (var message in result.messages) e.Fail(message);

			async Task<(bool success, string[] message)> checkPokemonForm() {
				var command = await this.GetCommandAsync(gen,
					gen != Generation.LetsGo
					? "SELECT Number, Name, Form, Category, BaseHP, BaseAttack, BaseDefense, BaseSpecialAttack, BaseSpecialDefense, BaseSpeed, EVHP, EVAttack, EVDefense, EVSpecialAttack, EVSpecialDefense, EVSpeed FROM Pokemon WHERE Key = $Key"
					: "SELECT Number, Name, Form, Category, BaseHP, BaseAttack, BaseDefense, BaseSpecialAttack, BaseSpecialDefense, BaseSpeed FROM Pokemon WHERE Key = $Key",
					("Key", search));

				using var reader = await command.ExecuteReaderAsync();
				if (!await reader.ReadAsync()) return (false, new[] {
					gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysPokemon.Contains(search)
						? $"This Pokémon is not present in the specified games."
						: $"I don't have anything matching '{search}'."
				});

				// Get type and Ability information.
				var typesString = await this.FormatTypes(gen, search, false);

				string? abilitiesString;
				if (gen == Generation.LetsGo) abilitiesString = null;
				else {
					using var reader2 = await (await this.GetCommandAsync(gen, "SELECT Slot, Ability FROM PokemonAbilities WHERE Pokemon = $Key ORDER BY Slot", ("Key", search))).ExecuteReaderAsync();
					abilitiesString = string.Join("", reader2.Cast<DbDataRecord>().Select(r => r.GetInt64(r.GetOrdinal("Slot")) switch { 2 => ", Hidden: ", 3 => ", Special: ", _ => ", " } + r.GetString(r.GetOrdinal("Ability")))).TrimStart(',', ' ');
				}

				var evs = new List<string>();
				if (gen != Generation.LetsGo) {
					if (reader.GetInt32("EVHP") > 0) evs.Add($"{reader.GetInt32("EVHP")} HP");
					if (reader.GetInt32("EVAttack") > 0) evs.Add($"{reader.GetInt32("EVAttack")} Attack");
					if (reader.GetInt32("EVDefense") > 0) evs.Add($"{reader.GetInt32("EVDefense")} Defense");
					if (reader.GetInt32("EVSpecialAttack") > 0) evs.Add($"{reader.GetInt32("EVSpecialAttack")} Sp. Atk");
					if (reader.GetInt32("EVSpecialDefense") > 0) evs.Add($"{reader.GetInt32("EVSpecialDefense")} Sp. Def");
					if (reader.GetInt32("EVSpeed") > 0) evs.Add($"{reader.GetInt32("EVSpeed")} Speed");
				}

				var name = reader.GetString("Name")!;
				var (hp, atk, def, spa, spd, spe) = (reader.GetInt32("BaseHP"), reader.GetInt32("BaseAttack"), reader.GetInt32("BaseDefense"), reader.GetInt32("BaseSpecialAttack"), reader.GetInt32("BaseSpecialDefense"), reader.GetInt32("BaseSpeed"));
				var message = $"{DescribeForm(name, reader.GetString("Form"))} ({reader.GetString("Category")} Pokémon) - {typesString} - Abilities: {abilitiesString}";
				var message2 = $"Base stats: HP {reader.GetInt64(reader.GetOrdinal("BaseHP"))}; Atk {reader.GetInt64(reader.GetOrdinal("BaseAttack"))}; Def {reader.GetInt64(reader.GetOrdinal("BaseDefense"))}; SpA {reader.GetInt64(reader.GetOrdinal("BaseSpecialAttack"))}; SpD {reader.GetInt64(reader.GetOrdinal("BaseSpecialDefense"))}; Spe {reader.GetInt64(reader.GetOrdinal("BaseSpeed"))}; EV: {string.Join(", ", evs)}";
				return (true, new[] { message, message2 });
			}

			async Task<(bool success, string[] message)> checkAbility() {
				var command = await this.GetCommandAsync(gen, "SELECT Name, ShortDescription FROM Abilities WHERE Key = $Key", ("Key", search));

				using var reader = await command.ExecuteReaderAsync();
				return await reader.ReadAsync()
					? (true, new[] { $"{reader.GetString("Name")}: {reader.GetString("ShortDescription")}" })
					: (false, new[] {
					gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysAbilities.Contains(search)
						? $"This Ability is not present in the specified games."
						: $"I don't have anything matching '{search}'."
				});
			}

			async Task<(bool success, string[] message)> checkMove(string search, string prefix = "") {
				var command = await this.GetCommandAsync(gen, "SELECT Name, ShortDescription, Target, BasePower, Accuracy, PP, Category, Type FROM Moves WHERE Key = $Key",
					("Key", search));

				using var reader = await command.ExecuteReaderAsync();
				if (!await reader.ReadAsync()) return (false, new[] {
					gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysMoves.Contains(search)
						? $"This move is not present in the specified games."
						: $"I don't have anything matching '{search}'."
				});

				var type = Enum.Parse<Type>(reader.GetString("Type")!);
				var message = $"{reader.GetString("Name")}: {reader.GetString("Type")}, {reader.GetString("Category")}, {reader.GetInt32("PP")} PP";
				if (reader.GetString("Category") != "Status") {
					var power = reader.GetNullableInt32("BasePower");
					if (power != null) message += $", {power} power";
				}
				if (reader.IsNull("Accuracy")) {
					if (reader.GetString("Category") != "Status")
						message += ", always hits";
				} else message += $", {reader.GetInt32("Accuracy")}% accuracy";

				if (reader.GetString("Target") != "normal") {
					message += reader.GetString("Target") switch {
						"adjacentAlly" => ", targets an adjacent ally",
						"adjacentAllyOrSelf" => ", targets the user or an adjacent ally",
						"adjacentFoe" => ", targets an adjacent opponent",
						"all" => ", targets everyone",
						"allAdjacent" => ", targets all adjacent Pokémon",
						"allAdjacentFoes" => ", targets all adjacent opponents",
						"allies" => ", targets all allies",
						"allySide" => ", targets the user's side",
						"allyTeam" => ", targets the user's team",
						"any" => ", targets any other Pokémon",
						"foeSide" => ", targets the opposing side",
						"normal" => "",
						"randomNormal" => ", targets a random adjacent opponent",
						"scripted" => "",
						"self" => "",
						_ => ""
					};
				}

				return (true, new[] { message + ". " + reader.GetString("ShortDescription") });
			}

			async Task<(bool success, string[] message)> checkItem() {
				var command = await this.GetCommandAsync(gen,
					gen == Generation.SwordShield
						? "SELECT Name, Category, Description, Type, Value, FlingPower, Move FROM Items LEFT JOIN Machines ON Items.Key = Machines.Item WHERE Key = $Key"
						: "SELECT Name, Category, Description, FlingPower, Move FROM Items LEFT JOIN Machines ON Items.Key = Machines.Item WHERE Key = $Key",
					("Key", search));

				using var reader = await command.ExecuteReaderAsync();
				if (!await reader.ReadAsync()) return (false, new[] {
					gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysItems.Contains(search)
						? $"This item is not present in the specified games."
						: $"I don't have anything matching '{search}'."
				});

				var move = reader.GetString("Move");
				if (move != null) return await checkMove(move, $"{reader.GetString("Name")}: ");

				switch (reader.GetString("Category")) {
					case "Berry":
						if (gen == Generation.BrilliantDiamondShiningPearl) {
							var command2 = await this.GetCommandAsync(gen, "SELECT TagNumber, Type, NaturalGiftPower, Spicy, Dry, Sweet, Bitter, Sour, GrowthTime, DrainRate, MaximumYield, Smoothness FROM Berries WHERE Key = $Key",
								("Key", search));

							using var reader2 = await command2.ExecuteReaderAsync();
							if (await reader2.ReadAsync() && reader2.GetNullableInt32("TagNumber") != null) {
								var flavourDescrpition = string.Join(", ", new[] { "Spicy", "Dry", "Sweet", "Bitter", "Sour" }.Select(s => (flavour: s, value: reader2.GetInt32(s))).Where(e => e.value > 0).Select(e => $"{e.value} {e.flavour}"));
								return (true, new[] {
									$"{reader.GetString("Name")} (#{reader2.GetInt32("TagNumber")}): {flavourDescrpition}. Grows in {reader2.GetInt32("GrowthTime")} hours; drain rate {reader2.GetInt32("DrainRate")}; maximum yield {reader2.GetInt32("MaximumYield")}; smoothness {reader2.GetInt32("Smoothness")}",
									(reader.GetString("Description")?.StartsWith("Cannot be eaten") ?? true) ? "No battle effect." : (reader.GetString("Description") ?? "")
								});
							}
						}
						break;
				}

				if (gen == Generation.SwordShield) {
					var type = Enum.Parse<Type>(reader.GetString("Type")!);

					var builder = new StringBuilder();
					builder.Append(reader.GetString("Name"));
					if (!string.IsNullOrEmpty(reader.GetString("Type"))) {
						builder.Append(" (");
						builder.Append(reader.GetString("Type"));
						builder.Append("; ");
						builder.Append(reader.GetNullableInt32("Value"));
						builder.Append(')');
					}
					builder.Append(": ");
					builder.Append(reader.GetString("Description"));
					return (true, new[] { builder.ToString() });
				} else {
					return (true, new[] { $"{reader.GetString("Name")}: {reader.GetString("Description")}" });
				}
			}
		}

		[Command(new[] { "grow", "plant", "berry" }, 1, 1, "grow <berry>[, <mulch>]", "Shows information about Berry growth.")]
		public async void CommandGrow(object? sender, CommandEventArgs e) {
			var gen = Generation.BrilliantDiamondShiningPearl;
			var cacheEntry = await this.GetCacheEntryAsync(gen);

			var parameters = e.Parameters[0].Split(',');
			if (parameters.Length is not (1 or 2)) {
				e.Fail(this.Bot.ReplaceCommands("Usage: !grow <berry>[, <mulch>]", e.Target));
				return;
			}

			var itemKey = parameters[0].Trim();
			if (!itemKey.EndsWith("Berry", StringComparison.InvariantCultureIgnoreCase)) itemKey += "Berry";
			itemKey = await this.GetKeyAsync(gen, itemKey);
			string? mulchKey;
			if (parameters.Length > 1) {
				mulchKey = parameters[1].Trim();
				if (!mulchKey.EndsWith("Mulch", StringComparison.InvariantCultureIgnoreCase)) mulchKey += "Mulch";
				mulchKey = await this.GetKeyAsync(gen, mulchKey);
			} else
				mulchKey = null;

			var command = await this.GetCommandAsync(gen, "SELECT Name, Category, Spicy, Dry, Sweet, Bitter, Sour, GrowthTime, DrainRate, MaximumYield, Smoothness FROM Items LEFT JOIN Berries ON Items.Key = Berries.Key WHERE Items.Key = $Key",
				("Key", itemKey));

			using var reader = await command.ExecuteReaderAsync();
			if (!await reader.ReadAsync()) {
				e.Fail("No such item is known.");
				return;
			}
			if (!reader.GetString("Category")!.Equals("Berry", StringComparison.InvariantCultureIgnoreCase)) {
				e.Fail("That item is not a Berry.");
				return;
			}
			if (reader.IsNull("DrainRate")) {
				e.Fail("I don't have data on that Berry.");
				return;
			}

			var flavourDescrpition = string.Join(", ", new[] { "Spicy", "Dry", "Sweet", "Bitter", "Sour" }.Select(s => (flavour: s, value: reader.GetInt32(s))).Where(e => e.value > 0).Select(e => $"{e.value} {e.flavour}"));
			int growthTime = reader.GetInt32("GrowthTime"), drainRate = reader.GetInt32("DrainRate");

			if (mulchKey != null) {
				switch (mulchKey.ToLowerInvariant()) {
					case "growthmulch":
						drainRate += drainRate / 2;
						growthTime -= growthTime / 4;
						break;
					case "dampmulch":
						drainRate /= 2;
						growthTime += growthTime / 2;
						break;
						// Ignore other mulches for now.
				}
			}

			e.Reply($"{reader.GetString("Name")}: {flavourDescrpition}. It will take {growthTime} hours to grow, and should be watered at least every {(int) Math.Ceiling(100.0 / drainRate) + 1} hours. The maximum yield is {reader.GetInt32("MaximumYield")}.");
		}

		[Command(new[] { "evolve" }, 1, 1, "evolve <species>[/form]", "Shows a Pokémon's pre-evolution and evolutions.")]
		public async void CommandEvolve(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			var pokemon = await this.GetKeyAsync(gen, e.Parameters[0]);

			var command = await this.GetCommandAsync(gen,
				(gen != Generation.LetsGo
					? "SELECT p1.Name, p1.Form, p1.EvolvesFrom, p1.EvolutionType, p1.EvolutionItem, p1.EvolutionLevel, p1.EvolutionMove, p1.EvolutionCondition, p2.Name AS PrevoName, p2.Form AS PrevoForm "
					: "SELECT p1.Name, p1.Form, p1.EvolvesFrom, p1.EvolutionType, p1.EvolutionItem, p1.EvolutionLevel, p2.Name AS PrevoName, p2.Form AS PrevoForm "
				) + "FROM Pokemon AS p1 LEFT JOIN Pokemon AS p2 ON p1.EvolvesFrom = p2.Key WHERE p1.Key = $Key",
				("Key", pokemon));
			var command2 = await this.GetCommandAsync(gen,
				(gen != Generation.LetsGo
					? "SELECT Name, Form, EvolutionType, EvolutionItem, EvolutionLevel, EvolutionMove, EvolutionCondition "
					: "SELECT Name, Form, EvolutionType, EvolutionItem, EvolutionLevel "
				) + "FROM Pokemon WHERE EvolvesFrom = $Key ORDER BY Number",
				("Key", pokemon));

			using var reader = await command.ExecuteReaderAsync();
			if (!reader.Read()) {
				e.Fail(gen != Generation.SwordShield && this.cache[Generation.SwordShield].keysPokemon.Contains(pokemon)
					? $"❌ This Pokémon is not present in the specified games."
					: $"❌ No Pokémon matching '{pokemon}' was found.");
			} else {
				static string describeEvolution(EvolutionType evolutionType, string? evolutionItem, int evolutionLevel, string? evolutionMove, string? evolutionCondition) {
					var s = evolutionType switch {
						EvolutionType.None => "never",
						EvolutionType.LevelUp => "at level " + evolutionLevel,
						EvolutionType.LevelUpWithFriendship => "when leveled up with at least 160 friendship",
						EvolutionType.Item => $"when exposed to {evolutionItem}",
						EvolutionType.Trade => "when traded" + (evolutionItem != null ? " while holding " + evolutionItem : ""),
						EvolutionType.LevelUpWithItem => "when leveled up while holding " + evolutionItem,
						EvolutionType.LevelUpWithMove => "when leveled up while knowing " + evolutionMove,
						EvolutionType.LevelUpSpecial => "when leveled up",
						EvolutionType.Special => "",
						_ => throw new ArgumentException("Unknown evolution type")
					};
					if (evolutionCondition != null) s += (s.Length > 0 ? " " : "") + evolutionCondition;
					return s;
				}

				static EvolutionType parseEvolutionType(string? s) => s switch {
					null => EvolutionType.LevelUp,
					"levelExtra" => EvolutionType.LevelUpSpecial,
					"levelFriendship" => EvolutionType.LevelUpWithFriendship,
					"levelHold" => EvolutionType.LevelUpWithItem,
					"levelMove" => EvolutionType.LevelUpWithMove,
					"other" => EvolutionType.Special,
					"trade" => EvolutionType.Trade,
					"useItem" => EvolutionType.Item,
					_ => throw new ArgumentException("Unknown EvolutionType")
				};

				string? message1 = !reader.IsNull("EvolvesFrom")
					? $"{DescribeForm(reader.GetString("Name")!, reader.GetString("Form"))} evolves from {DescribeForm(reader.GetString("PrevoName")!, reader.GetString("PrevoForm"))} {describeEvolution(parseEvolutionType(reader.GetString("EvolutionType")), reader.GetString("EvolutionItem"), reader.GetNullableInt32("EvolutionLevel") ?? 0, gen != Generation.LetsGo ? reader.GetString("EvolutionMove") : null, gen != Generation.LetsGo ? reader.GetString("EvolutionCondition") : null)}."
					: null;
				using var reader2 = await command2.ExecuteReaderAsync();
				var messages = reader2.Cast<DbDataRecord>().Select(r => DescribeForm(r.GetString("Name")!, r.GetString("Form")) + " " + describeEvolution(parseEvolutionType(r.GetString("EvolutionType")), r.GetString("EvolutionItem"), r.GetNullableInt32("EvolutionLevel") ?? 0, gen != Generation.LetsGo ? r.GetString("EvolutionMove") : null, gen != Generation.LetsGo ? r.GetString("EvolutionCondition") : null))
					.ToList();
				string? message2 = messages.Count == 0 ? null :
					messages.Count == 1 ? $"{DescribeForm(reader.GetString("Name")!, reader.GetString("Form"))} evolves into {messages[0]}." :
					$"{DescribeForm(reader.GetString("Name")!, reader.GetString("Form"))} can evolve into: {string.Join("; ", messages)}.";

				var message =
					(message1 == null && message2 == null) ? $"{DescribeForm(reader.GetString("Name")!, reader.GetString("Form"))} does not evolve." :
					message1 == null ? message2! :
					message2 == null ? message1 :
					message1 + " " + message2;
				e.Reply(message);
			}
		}

		private static string DescribeForm(string name, string? form)
			=> form switch {
				null => name,
				"Alola" => "Alolan " + name,
				"Galar" => "Galarian " + name,
				"Mega" => "Mega " + name,
				_ => name + "-" + form
			};

		[Command(new[] { "bosses" }, 1, 2, "bosses <type> [sword|shield]", "Shows Dynamax Adventure bosses of the specified type.")]
		public async void CommandBosses(object? sender, CommandEventArgs e) {
			if (!Enum.TryParse<Type>(e.Parameters[0], true, out var type)) {
				e.Fail("That is not a valid type.");
				return;
			}

			string where;
			SQLiteCommand command;
			if (e.Parameters.Length > 1) {
				if (e.Parameters[1].Equals("Sword", StringComparison.InvariantCultureIgnoreCase))
					where = "Sword = 1 AND ";
				else if (e.Parameters[1].Equals("Shield", StringComparison.InvariantCultureIgnoreCase))
					where = "Shield = 1 AND ";
				else {
					e.Fail("The game should be Sword or Shield.");
					return;
				}
			} else
				where = "";
			command = await this.GetCommandAsync(Generation.SwordShield, $"SELECT Key, Name FROM DynamaxAdventureBosses INNER JOIN Pokemon ON DynamaxAdventureBosses.Pokemon = Pokemon.Key INNER JOIN PokemonTypes ON Pokemon.Key = PokemonTypes.Pokemon WHERE {where}Type = $Type",
				("Type", type.ToString()));

			using var reader = await command.ExecuteReaderAsync();
			var pokemon = reader.Cast<DbDataRecord>().Select(r => (key: r.GetString("Key")!, name: r.GetString("Name"), types: new List<Type>())).ToList();
			if (pokemon.Count == 0) {
				e.Fail("No Pokémon were found.");
				return;
			}

			foreach (var (key, _, types) in pokemon) {
				types.AddRange(await this.GetTypesAsync(Generation.SwordShield, key));
			}

			var pokemonString = string.Join(", ", pokemon.Select(e => e.name));

			// Recommend offensive types that are super effective against most of the bosses.
			// Sometimes this can appear counter-intuitive.
			// For example, Grass is not recommended against Ground because only one Ground boss is weak to it (Groudon).
			// Ice is recommended against Electric and Dark because three out of six Electric-type bosses and both Dark-type bosses are weak to Ice.
			var scores = new int[18];
			for (int i = 0; i < 18; ++i) {
				var type2 = (Type) i;
				foreach (var (_, _, types) in pokemon) {
					var matchup = TypeChart.Standard.GetMatchup(type2, types);
					scores[i] += matchup >= 4 ? 4 : matchup >= 2 ? 2 : 0;
				}
			}

			e.Reply($"{type}-type bosses: {pokemonString}");
			e.Reply($"Effective types: {string.Join(", ", scores.Select((s, i) => (s, i)).Where(e => e.s >= pokemon.Count).OrderByDescending(e => e.s).Select(e => $"{this.FormatType((Type) e.i, false)}"))}");
		}

		[Command(new[] { "learn" }, 1, 1, "learn <pokemon>, <move>", "Shows how the specified Pokémon can learn the specified move.")]
		public async void CommandLearn(object? sender, CommandEventArgs e) {
			var parameters = e.Parameters[0].Split(',');
			if (parameters.Length == 1) parameters = e.Parameters[0].Split((char[]?) null, 2, StringSplitOptions.RemoveEmptyEntries);
			if (parameters.Length != 2) {
				e.Fail(this.Bot.ReplaceCommands("Usage: !learn <pokemon>, <move>", e.Target));
				return;
			}

			var gen = this.GetGeneration(e.Channel);
			var pokemonKey = await this.GetKeyAsync(gen, parameters[0]);
			var moveKey = await this.GetKeyAsync(gen, parameters[1]);

			// Get Pokémon and move names.
			string pokemonName; string? prevo;
			var command2 = await this.GetCommandAsync(gen, "SELECT Name, Form, EvolvesFrom FROM Pokemon WHERE Key = $Pokemon", ("Pokemon", pokemonKey));
			using (var reader2 = await command2.ExecuteReaderAsync()) {
				if (!reader2.Read()) {
					e.Fail("No such Pokémon was found.");
					return;
				}
				pokemonName = DescribeForm(reader2.GetString("Name")!, reader2.GetString("Form")!);
				prevo = reader2.GetString("EvolvesFrom");
			}

			string moveName;
			var command3 = await this.GetCommandAsync(gen, "SELECT Name FROM Moves WHERE Key = $Move", ("Move", moveKey));
			moveName = (string) command3.ExecuteScalar();
			if (moveName == null) {
				e.Fail("No such move was found.");
				return;
			}

			var command = await this.GetCommandAsync(gen, "SELECT * FROM Learnsets WHERE Pokemon = $Pokemon AND Move = $Move", ("Pokemon", pokemonKey), ("Move", moveKey));

			string? pokemonName2 = null;
			while (true) {
				using var reader = await command.ExecuteReaderAsync();
				if (reader.Read()) {
					var messages = new List<string>();
					if (reader.GetBool("Evolution")) messages.Add("upon evolving");
					if (reader.GetBool("Level")) {
						var command4 = await this.GetCommandAsync(gen, "SELECT Level FROM LearnsetsLevel WHERE Pokemon = $Pokemon AND Move = $Move", ("Pokemon", pokemonKey), ("Move", moveKey));
						using var reader4 = await command4.ExecuteReaderAsync();
						messages.AddRange(reader4.Cast<DbDataRecord>().Select(r => $"at level {r.GetInt32("Level")}"));
					}
					if (reader.GetBool("Machine")) {
						var command4 = await this.GetCommandAsync(gen, "SELECT Item FROM Machines WHERE Move = $Move", ("Move", moveKey));
						messages.Add($"using {((string) command4.ExecuteScalar()).ToUpperInvariant()}");
					}
					if (reader.GetBool("Special")) messages.Add("by a special method");
					if (reader.GetBool("Tutor")) messages.Add("from a Move Tutor");
					if (gen != Generation.LetsGo && reader.GetBool("Egg")) {
						// Find possible partners.
						var command4 = await this.GetCommandAsync(gen, @$"
							SELECT Key, Name, Form, EvolvesFrom, Egg FROM
							(SELECT DISTINCT Pokemon FROM PokemonEggGroups WHERE Pokemon IS NOT $Pokemon AND Pokemon IS NOT $Prevo AND EggGroup IN (SELECT EggGroup FROM PokemonEggGroups WHERE Pokemon = $Pokemon)) AS PokemonEggGroups
							INNER JOIN Pokemon ON PokemonEggGroups.Pokemon = Pokemon.Key
							INNER JOIN Learnsets ON Learnsets.Pokemon = Pokemon.Key
							WHERE Pokemon.EvolvesFrom IS NOT $Pokemon AND Pokemon.GenderRatio != 255 AND GenderRatio != 254 AND Learnsets.Move = $move AND (Learnsets.Egg OR Learnsets.Level OR Learnsets.Machine OR Learnsets.Special OR Learnsets.Tutor) AND ChangesFrom IS NULL AND Form IS NOT 'Mega'",
							("Pokemon", pokemonKey), ("Prevo", prevo), ("Move", moveKey));

						using var reader3 = await command4.ExecuteReaderAsync();
						var partners = new Dictionary<string, (string name, bool chained, string? prevo)>(StringComparer.InvariantCultureIgnoreCase);
						foreach (DbDataRecord row in reader3) {
							partners.Add(row.GetString("Key")!, (DescribeForm(row.GetString("Name")!, row.GetString("Form")), row.GetBool("Egg"), row.GetString("EvolvesFrom")));
						}
						// Remove evolutions for brevity
						var toRemove = partners.Where(p => p.Value.prevo != null && partners.ContainsKey(p.Value.prevo)).Select(p => p.Key).ToList();
						foreach (var key in toRemove) partners.Remove(key);

						if (partners.Count == 0) messages.Add($"by breeding (no partner found)");
						else {
							var count = 0;
							var primaryMessage = string.Join(", ", partners.Where(p => !p.Value.chained).Select(p => p.Value.name).OrderBy(n => n).Take(11).Select(n => ++count > 10 ? "..." : n));
							if (primaryMessage.Length > 0) primaryMessage = "from " + primaryMessage;
							var secondaryMessage = string.Join(", ", partners.Where(p => p.Value.chained).Select(p => p.Value.name).OrderBy(n => n).Take(11 - count).Select(n => ++count > 10 ? "..." : n));
							if (secondaryMessage.Length > 0) secondaryMessage = "chained from " + secondaryMessage;
							messages.Add($"by breeding ({string.Join("; ", new[] { primaryMessage, secondaryMessage }.Where(s => s.Length > 0))})");
						}
					}
					if (messages.Count == 0 && reader.GetBool("Event")) {
						var command4 = await this.GetCommandAsync(gen, "SELECT Generation, EventNumber FROM LearnsetsEvent WHERE Pokemon = $Pokemon AND Move = $Move ORDER BY Generation DESC, EventNumber", ("Pokemon", pokemonKey), ("Move", moveKey));
						using var reader4 = await command4.ExecuteReaderAsync();
						var gens = reader4.Cast<DbDataRecord>().Select(r => $"gen {r.GetInt32("Generation")} event #{r.GetInt32("EventNumber")}");
						messages.Add($"only from an event ({string.Join(", ", gens)})");
					}
					if (messages.Count == 0 && gen != Generation.LetsGo && reader.GetBool("Transfer")) {
						var command4 = await this.GetCommandAsync(gen, "SELECT Generation FROM LearnsetsTransfer WHERE Pokemon = $Pokemon AND Move = $Move ORDER BY Generation", ("Pokemon", pokemonKey), ("Move", moveKey));
						using var reader4 = await command4.ExecuteReaderAsync();
						var gens = reader4.Cast<DbDataRecord>().Select(r => r.GetInt64(0));
						messages.Add($"only when transferred from a previous generation ({string.Join(", ", gens)})");
					}
					if (messages.Count == 0 && gen != Generation.LetsGo && reader.GetBool("LegacyEvent")) {
						var command4 = await this.GetCommandAsync(gen, "SELECT Generation, EventNumber FROM LearnsetsEvent WHERE Pokemon = $Pokemon AND Move = $Move ORDER BY Generation DESC, EventNumber", ("Pokemon", pokemonKey), ("Move", moveKey));
						using var reader4 = await command4.ExecuteReaderAsync();
						var gens = reader4.Cast<DbDataRecord>().Select(r => $"gen {r.GetInt32("Generation")} event #{r.GetInt32("EventNumber")}");
						messages.Add($"only from a previous generation event ({string.Join(", ", gens)})");
					}
					e.Reply(messages.Count == 0 ? throw new InvalidOperationException("Learnset entry found in database but no flags or no level set?!")
						: messages.Count == 1
							? $"{pokemonName} can learn {moveName}{(pokemonName2 != null ? $" as {pokemonName2}" : "")} {messages[0]}."
							: $"{pokemonName} can learn {moveName}{(pokemonName2 != null ? $" as {pokemonName2}" : "")}: {string.Join(", ", messages)}.");
					return;
				} else if (prevo != null) {
					pokemonKey = prevo;
					command.Parameters["$Pokemon"].Value = prevo;
					command2.Parameters["$Pokemon"].Value = prevo;
					using var reader3 = await command2.ExecuteReaderAsync();
					if (!reader3.Read()) throw new InvalidOperationException($"Prevo {prevo} not found?!");
					pokemonName2 = DescribeForm(reader3.GetString("Name")!, reader3.GetString("Form")!);
					prevo = reader3.GetString("EvolvesFrom");
				} else {
					e.Reply($"{pokemonName} cannot learn {moveName}.");
					return;
				}
			}
		}

		[Command(new[] { "weak", "weakness" }, 1, 1, "weakness <species>|<type>, <type>", "Shows type matchups against the specified opponent.")]
		public async void CommandWeakness(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			var parameters = e.Parameters[0].Split(',');
			var types = new List<Type>();

			string label = "";
			string? isPokemon = null;

			for (int i = 0; i < parameters.Length; ++i) {
				if (Enum.TryParse(parameters[i], true, out Type type)) {
					if (!types.Contains(type)) {
						types.Add(type);
						label += (label.Length > 0 ? "/" : "") + type;
					}
				} else {
					// If a Pokémon is specified, use its types.
					if (isPokemon != null) {
						e.Fail("Cannot specify more than one Pokémon.");
						return;
					}
					isPokemon = await this.GetKeyAsync(gen, parameters[i]);

					var command = await this.GetCommandAsync(gen, "SELECT Name, Form FROM Pokemon WHERE Key = $Key", ("Key", isPokemon));
					using var reader = await command.ExecuteReaderAsync();
					if (!reader.Read()) {
						e.Fail("No such Pokémon or type was found.");
						return;
					}
					label += (label.Length > 0 ? "/" : "") + DescribeForm(reader.GetString("Name")!, reader.GetString("Form")!);

					var command2 = await this.GetCommandAsync(gen, "SELECT Type FROM PokemonTypes WHERE Pokemon = $Key", ("Key", isPokemon));
					using var reader2 = await command2.ExecuteReaderAsync();
					types.AddRange(reader2.Cast<DbDataRecord>().Select(t => (Type) Enum.Parse(typeof(Type), reader2.GetString("Type")!)));
				}
				if (types.Count > 3) {
					e.Fail("Too many types specified.");
					return;
				}
			}

			List<(Type type, string? ability, float multiplier)> results = new();

			for (var type = Type.Normal; type <= Type.Fairy; ++type) {
				var multiplier = TypeChart.Standard.GetMatchup(type, types);
				results.Add((type, null, multiplier));
			}

			// Take into account abilities that provide resistances or immunities.
			if (isPokemon != null && gen != Generation.LetsGo) {
				var command3 = await this.GetCommandAsync(gen, "SELECT Ability FROM PokemonAbilities WHERE Pokemon = $Key ORDER BY Slot", ("Key", isPokemon));
				using var reader3 = await command3.ExecuteReaderAsync();

				while (reader3.Read()) {
					// Ignore Filter/Prism Armor/Solid Rock for now.
					var abilityName = reader3.GetString("Ability")!;
					var ability = (await this.GetKeyAsync(gen, abilityName, false)).ToLowerInvariant();
					if (ability == "wonderguard") {
						for (int i = 0; i < results.Count; ++i)
							if (results[i].multiplier is > 0 and <= 1)
								results[i] = (results[i].type, abilityName, 0);
					} else {
						(Type type, float multiplier)[]? extraMultipliers = ability switch {
							"dryskin" => new[] { (Type.Water, 0f), (Type.Fire, 1.25f) },
							"flashfire" => new[] { (Type.Fire, 0f) },
							"fluffy" => new[] { (Type.Fire, 2f) },
							"heatproof" => new[] { (Type.Fire, 0.5f) },
							"levitate" => new[] { (Type.Ground, 0f) },
							"lightningrod" => new[] { (Type.Electric, 0f) },
							"motordrive" => new[] { (Type.Electric, 0f) },
							"sapsipper" => new[] { (Type.Grass, 0f) },
							"stormdrain" => new[] { (Type.Water, 0f) },
							"voltabsorb" => new[] { (Type.Electric, 0f) },
							"waterabsorb" => new[] { (Type.Water, 0f) },
							"waterbubble" => new[] { (Type.Fire, 0.5f) },
							_ => null,
						};
						if (extraMultipliers != null) {
							foreach (var (type3, extraMultiplier) in extraMultipliers) {
								var multiplier = TypeChart.Standard.GetMatchup(type3, types) * extraMultiplier;
								if (multiplier == 1) {
									var i = results.FindIndex(e => e.type == type3);
									if (i >= 0) {
										results[i] = (type3, "without " + abilityName, results[i].multiplier);
									}
								} else 
									results.Add((type3, abilityName, multiplier));
							}
						}
					}
				}
			}

			var resultBuilder = new StringBuilder($"Matchups against {label}:");
			float lastMultiplier = float.NaN;
			foreach (var result in from r in results where r.multiplier != 1 orderby r.multiplier descending select r) {
				if (result.multiplier != lastMultiplier) {
					lastMultiplier = result.multiplier;
					resultBuilder.Append($" [×{lastMultiplier:0.###}] ");
				} else {
					resultBuilder.Append(", ");
				}
				resultBuilder.Append(this.FormatType(result.type, false) + (result.ability != null ? $" ({result.ability})" : ""));
			}
			e.Reply(resultBuilder.ToString());
		}

		private static readonly Dictionary<string, string> PokemonColumnAliases = new(StringComparer.InvariantCultureIgnoreCase) {
			{ "Num", "Number" },
			{ "Forme", "Forme" },
			{ "Gender", "GenderRatio" },
			{ "HP", "BaseHP" },
			{ "Attack", "BaseAttack" },
			{ "Atk", "BaseAttack" },
			{ "Defense", "BaseDefense" },
			{ "Def", "BaseDefense" },
			{ "SpecialAttack", "BaseSpecialAttack" },
			{ "SpAtk", "BaseSpecialAttack" },
			{ "SpA", "BaseSpecialAttack" },
			{ "SpecialDefense", "BaseSpecialDefense" },
			{ "SpDef", "BaseSpecialDefense" },
			{ "SpD", "BaseSpecialDefense" },
			{ "Speed", "BaseSpeed" },
			{ "Spe", "BaseSpeed" },
			{ "BaseStatTotal", "(BaseHP + BaseAttack + BaseDefense + BaseSpecialAttack + BaseSpecialDefense + BaseSpeed)" },
			{ "BST", "(BaseHP + BaseAttack + BaseDefense + BaseSpecialAttack + BaseSpecialDefense + BaseSpeed)" },
			{ "Exp", "Experience" },
			{ "EV", "EVYield" },
			{ "EVs", "EVYield" },
			{ "EVAtk", "EVAttack" },
			{ "EVDef", "EVDefense" },
			{ "EVSpAtk", "EVSpecialAttack" },
			{ "EVSpA", "EVSpecialAttack" },
			{ "EVSpDef", "EVSpecialDefense" },
			{ "EVSpD", "EVSpecialDefense" },
			{ "EVTotal", "(EVHP + EVAttack + EVDefense + EVSpecialAttack + EVSpecialDefense + EVSpeed)" },
			{ "Friendship", "BaseFriendship" },
			{ "Happiness", "BaseFriendship" },
			{ "Color", "Colour" }
		};

		[Command(new[] { "dexsearch" }, 1, 1, "dexsearch <query>[, <query>]*", "Searches for Pokémon. Queries can be types, Egg groups, Abilities or stats (as stat = value, or <, >, <=, >=, !=). To sort, specify max=<stat> or min=<stat>.")]
		public async void CommandDexSearch(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			try {
				var baseClause = "SELECT Name, Form FROM Pokemon";
				var extraClauses = new List<string>();
				var conditions = new List<string>();
				string? orderBy = null;
				int types = 0, eggGroups = 0;

				var cacheEntry = await this.GetCacheEntryAsync(gen);
				using var command = cacheEntry.connection.CreateCommand();
				var parameterIndex = 0;

				foreach (var clause in e.Parameters[0].Split(',')) {
					var match = Regex.Match(clause, @"[<=>]=?|!=");
					if (match.Success) {
						var key = await this.GetKeyAsync(gen, clause.Substring(0, match.Index), false);
						var value = clause[(match.Index + match.Length)..];
						key = PokemonColumnAliases.TryGetValue(key, out var key2) ? key2 : await this.GetKeyAsync(gen, key);

						switch (key.ToLowerInvariant()) {
							case "min":
							case "max":
								if (orderBy != null) throw new ArgumentException("Can't specify more than one sort order.");
								var column = await this.GetKeyAsync(gen, value, false);
								column = PokemonColumnAliases.TryGetValue(column, out key2) ? key2 : await this.GetKeyAsync(gen, column);
								orderBy = column + " " + (key.Equals("min", StringComparison.InvariantCultureIgnoreCase) ? " ASC" : " DESC");
								baseClause = $"SELECT Name, Form, {column} FROM Pokemon";
								break;
							case "evyield":
								// EV yield can be specified as:
								//   <stat> - Pokémon yielding EVs in that stat only
								//   <n> <stat> - Pokémon yielding <n> EVs in that stat only
								//   <stat>+<stat> - Pokémon yielding EVs in those stats only
								int hp = 0, atk = 0, def = 0, spa = 0, spd = 0, spe = 0;
								foreach (var field in value.Split('+')) {
									var match2 = Regex.Match(await this.GetKeyAsync(gen, field, false), @"^(\d+)?(.*)");
									var v = match2.Groups[1].Success ? int.Parse(match2.Groups[1].Value) : -1;

									switch (Regex.Replace(match2.Groups[2].Value, @"\W", "").ToLowerInvariant()) {
										case "hp": hp = v; break;
										case "attack": case "atk": atk = v; break;
										case "defense": case "def": def = v; break;
										case "specialattack": case "spatk": case "spa": spa = v; break;
										case "specialdefense": case "spdef": case "spd": spd = v; break;
										case "speed": case "spe": spe = v; break;
										default: throw new FormatException("Unknown EV stat: " + match2.Groups[2].Value);
									}
								}

								conditions.Add(hp < 0 ? "EVHP > 0" : "EVHP = " + hp);
								conditions.Add(atk < 0 ? "EVAttack > 0" : "EVAttack = " + atk);
								conditions.Add(def < 0 ? "EVDefense > 0" : "EVDefense = " + def);
								conditions.Add(spa < 0 ? "EVSpecialAttack > 0" : "EVSpecialAttack = " + spa);
								conditions.Add(spd < 0 ? "EVSpecialDefense > 0" : "EVSpecialDefense = " + spd);
								conditions.Add(spe < 0 ? "EVSpeed > 0" : "EVSpeed = " + spe);
								break;
							case "number":
							case "isdefaultform":
							case "genderratio":
							case "basehp":
							case "baseattack":
							case "basedefense":
							case "basespecialattack":
							case "basespecialdefense":
							case "basespeed":
							case "height":
							case "weight":
							case "catchrate":
							case "eggcycles":
							case "experience":
							case "evhp":
							case "evattack":
							case "evdefense":
							case "evspecialattack":
							case "evspecialdefense":
							case "evspeed":
							case "shape":
							case "basefriendship":
							case "canhatch":
							case "candynamax":
							case "evolutionlevel":
							case "maxhp":
								if (!int.TryParse(value.Trim(), out var valueInt)) throw new FormatException(key + " must be an integer.");
								conditions.Add($"{key} {match.Value} {valueInt}");
								break;
							default:
								conditions.Add($"{key} {match.Value} $v{parameterIndex}");
								command.Parameters.AddWithValue("$v" + parameterIndex, value.Trim());
								++parameterIndex;
								break;
						}
					} else {
						var field = await this.GetKeyAsync(gen, clause);
						if (Enum.TryParse<Type>(field, true, out var type)) {
							// Type
							if (types >= 2) throw new ArgumentException("Can't specify more than two types.");
							extraClauses.Add($"INNER JOIN (SELECT * FROM PokemonTypes WHERE Type = \"{type}\") AS type{extraClauses.Count} ON Pokemon.Key = type{extraClauses.Count}.Pokemon");
							++types;
						} else if (this.keysEggGroups.Contains(field)) {
							// Egg Group
							if (gen == Generation.LetsGo) throw new ArgumentException("Can't search for Egg groups in Pokémon: Let's Go, Pikachu!/Let's Go, Eevee!");
							if (eggGroups >= 2) throw new ArgumentException("Can't specify more than two Egg groups.");
							if (field.Equals("HumanLike", StringComparison.InvariantCultureIgnoreCase)) field = "Human-Like";
							else if (field.StartsWith("Water", StringComparison.InvariantCultureIgnoreCase)) field = "Water " + field[5..];
							extraClauses.Add($"INNER JOIN (SELECT * FROM PokemonEggGroups WHERE EggGroup = \"{field}\") AS eggGroup{extraClauses.Count} ON Pokemon.Key = eggGroup{extraClauses.Count}.Pokemon");
							++eggGroups;
						} else if (cacheEntry.keysMoves.Contains(field)) {
							// Move
							command.Parameters.AddWithValue("$move" + extraClauses.Count, field);
							extraClauses.Add($"INNER JOIN (SELECT * FROM Learnsets WHERE Move = $move{extraClauses.Count} AND (Egg = 1 OR Evolution = 1 OR Level = 1 OR Machine = 1 OR Tutor = 1)) AS move{extraClauses.Count} ON Pokemon.Key = move{extraClauses.Count}.Pokemon");
						} else if (cacheEntry.keysAbilities.Contains(field)) {
							// Ability
							if (gen == Generation.LetsGo) throw new ArgumentException("Can't search for Abilities in Pokémon: Let's Go, Pikachu!/Let's Go, Eevee!");
							var command2 = await this.GetCommandAsync(gen, "SELECT Name FROM Abilities WHERE Key = $Key", ("Key", field));
							var abilityName = command2.ExecuteScalar();
							if (abilityName == null) throw new ArgumentException("No such ability: " + field);

							command.Parameters.AddWithValue("$ability" + extraClauses.Count, abilityName);
							extraClauses.Add($"INNER JOIN (SELECT * FROM PokemonAbilities WHERE Ability = $ability{extraClauses.Count}) AS ability{extraClauses.Count} ON Pokemon.Key = ability{extraClauses.Count}.Pokemon");
						} else if (cacheEntry.keysTags.Contains(field)) {
							command.Parameters.AddWithValue("$tag" + extraClauses.Count, field);
							extraClauses.Add($"INNER JOIN (SELECT * FROM PokemonTags WHERE Tag = $tag{extraClauses.Count}) AS tag{extraClauses.Count} ON Pokemon.Key = tag{extraClauses.Count}.Pokemon");
						} else {
							switch (field.ToLowerInvariant()) {
								case "common": conditions.Add("Status = \"Common\""); break;
								case "legendary": conditions.Add("Status = \"Legendary\""); break;
								case "mythical": conditions.Add("Status = \"Mythical\""); break;
								case "ultrabeast": case "ub": conditions.Add("Status = \"Ultra Beast\""); break;
								default: throw new ArgumentException($"Don't know how to search for '{field}'.");
							}
						}
					}
				}

				command.CommandText = "SELECT COUNT(*) FROM Pokemon " + string.Join(' ', extraClauses)
					+ (conditions.Count > 0 ? "\nWHERE " + string.Join(" AND ", conditions) : "");

				var rowCount = (long) (await command.ExecuteScalarAsync())!;
				if (rowCount == 0) {
					e.Reply("No matching forms were found.");
					return;
				}

				const int MaxRows = 10;
				command.CommandText = baseClause + " " + string.Join(' ', extraClauses)
					+ (conditions.Count > 0 ? "\nWHERE " + string.Join(" AND ", conditions) : "")
					+ $"\nORDER BY {orderBy ?? "Number ASC"} LIMIT {MaxRows}";

				using var reader = await command.ExecuteReaderAsync();
				var message = string.Join(", ", reader.Cast<DbDataRecord>().Select(r => DescribeForm(r.GetString("Name")!, r.GetString("Form")) + (reader.FieldCount > 2 ? $"({reader.GetValue(2)})" : "")));
				if (rowCount > MaxRows) message += $", _{rowCount - MaxRows} more_";
				e.Reply(message);
			} catch (Exception ex) {
				e.Fail(ex.Message);
			}
		}

		private static readonly Dictionary<string, string> MoveColumnAliases = new(StringComparer.InvariantCultureIgnoreCase) {
			{ "Power", "BasePower" },
			{ "MaxPP", "(PP * 8 / 5)" },
			{ "Condition", "ContestCondition" },
			{ "Biting", "Bite" },
			{ "Charges", "Charge" },
			{ "MakesContact", "Contact" },
			{ "Defrosts", "Defrost" },
			{ "Healing", "Heal" },
			{ "Punching", "Punch" },
			{ "Recharges", "Recharge" },
			{ "Snatchable", "Snatch" },
			{ "SoundBased", "Sound" }
		};

		[Command(new[] { "movesearch" }, 1, 1, "movesearch <query>[, <query>]*", "Searches for moves. Queries can be types, Pokémon, effective: Pokémon, categories, flags. To sort, specify max=<stat> or min=<stat>.")]
		public async void CommandMoveSearch(object? sender, CommandEventArgs e) {
			var gen = this.GetGeneration(e.Channel);
			try {
				var baseClause = "SELECT Name FROM Moves";
				var extraClauses = new List<string>();
				var conditions = new List<string>();
				string? orderBy = null;
				bool typeSpecified = false, categorySpecified = false, conditionSpecified = false;
				string? pokemonKey = null;
				bool useLevel = false;

				var cacheEntry = await this.GetCacheEntryAsync(gen);
				using var command = cacheEntry.connection.CreateCommand();
				var parameterIndex = 0;

				foreach (var rawClause in e.Parameters[0].Split(',')) {
					var clause = rawClause.Trim().Equals("level", StringComparison.InvariantCultureIgnoreCase) ? "sort:level" : rawClause;
					var match = Regex.Match(clause, @"[<=>]=?|!=|:");
					if (match.Success) {
						var key = await this.GetKeyAsync(gen, clause.Substring(0, match.Index), false);
						var value = clause[(match.Index + match.Length)..];
						key = MoveColumnAliases.TryGetValue(key, out var key2) ? key2 : await this.GetKeyAsync(gen, key);

						switch (key.ToLowerInvariant()) {
							case "min":
							case "max":
							case "sort":
								if (orderBy != null) throw new ArgumentException("Can't specify more than one sort order.");
								var column = await this.GetKeyAsync(gen, value, false);
								if (column == "level") {
									useLevel = true;
									baseClause = "SELECT Name, LearnsetsLevelFiltered.Level FROM Moves";
									orderBy = $"LearnsetsLevelFiltered.Level {(key.Equals("max", StringComparison.InvariantCultureIgnoreCase) ? " DESC" : " ASC")}";
								} else {
									column = MoveColumnAliases.TryGetValue(column, out key2) ? key2 : await this.GetKeyAsync(gen, column);
									orderBy = column + " " + (key.Equals("max", StringComparison.InvariantCultureIgnoreCase) ? " DESC" : " ASC");
									baseClause = $"SELECT Name, {column} FROM Moves";
								}
								break;
							case "eff":
							case "effective":
								var types = new List<Type>();
								foreach (var token in value.Split(new[] { '/' })) {
									var tokenKey = await this.GetKeyAsync(gen, token, false);
									if (Enum.TryParse<Type>(tokenKey, true, out var type)) types.Add(type);
									else types.AddRange(await this.GetTypesAsync(gen, await this.GetKeyAsync(gen, tokenKey, false)));
								}
								if (types.Count == 0) throw new ArgumentException($"No such Pokémon or type '{value}' was found.");
								conditions.Add($"Category IN ('Physical', 'Special')");
								conditions.Add($"Type IN ('{string.Join("', '", Enumerable.Range(0, (int) Type.Fairy).Cast<Type>().Where(t => TypeChart.Standard.GetMatchup(t, types) > 1))}')");
								break;
							case "level":
								key = "LearnsetsLevelFiltered.Level";
								useLevel = true;
								goto case "number";
							case "number":
							case "basepower":
							case "accuracy":
							case "critrate":
							case "pp":
							case "priority":
							case "iszmove":
							case "ismaxmove":
							case "zpower":
							case "maxpower":

							case "animatesonally":
							case "bite":
							case "bullet":
							case "bypassessubstitutes":
							case "charge":
							case "contact":
							case "dance":
							case "defrost":
							case "distance":
							case "gravity":
							case "heal":
							case "mirror":
							case "nonsky":
							case "powder":
							case "protect":
							case "pulse":
							case "punch":
							case "recharge":
							case "reflectable":
							case "snatch":
							case "sound":
								if (!int.TryParse(value.Trim(), out var valueInt)) throw new FormatException(key + " must be an integer.");
								conditions.Add($"{key} {match.Value} {valueInt}");
								break;
							default:
								conditions.Add($"{key} {match.Value} $v{parameterIndex}");
								command.Parameters.AddWithValue("$v" + parameterIndex, value.Trim());
								++parameterIndex;
								break;
						}
					} else {
						var field = await this.GetKeyAsync(gen, clause);
						if (Enum.TryParse<Type>(field, true, out var type)) {
							// Type
							if (typeSpecified) throw new ArgumentException("Can't specify more than one type.");
							conditions.Add($"Type = $v{parameterIndex}");
							command.Parameters.AddWithValue("$v" + parameterIndex, type.ToString());
							typeSpecified = true;
						} else if (field.ToLowerInvariant() is "physical" or "special" or "status") {
							// Category
							if (categorySpecified) throw new ArgumentException("Can't specify more than one category.");
							conditions.Add($"Category = $v{parameterIndex}");
							command.Parameters.AddWithValue("$v" + parameterIndex, field);
							categorySpecified = true;
						} else if (field.Equals("attack", StringComparison.InvariantCultureIgnoreCase)) {
							if (categorySpecified) throw new ArgumentException("Can't specify more than one category.");
							conditions.Add($"Category IN ('Physical', 'Special')");
							categorySpecified = true;
						} else if (cacheEntry.keysPokemon.Contains(field)) {
							// Pokémon
							pokemonKey = field;
							command.Parameters.AddWithValue("$pokemon" + extraClauses.Count, field);
							extraClauses.Add($"INNER JOIN (SELECT * FROM Learnsets WHERE Pokemon = $pokemon{extraClauses.Count} AND (Egg = 1 OR Evolution = 1 OR Level = 1 OR Machine = 1 OR Tutor = 1)) AS learnsets{extraClauses.Count} ON Moves.Key = learnsets{extraClauses.Count}.Move");
						} else {
							switch (field.ToLowerInvariant()) {
								case "z": case "zmove": conditions.Add("IsZMove != 0"); break;
								case "max": case "maxmove": conditions.Add("IsMaxMove != 0"); break;
								case "animatesonally":
								case "bite":
								case "bullet":
								case "bypassessubstitutes":
								case "charge":
								case "contact":
								case "dance":
								case "defrost":
								case "distance":
								case "gravity":
								case "heal":
								case "mirror":
								case "nonsky":
								case "powder":
								case "protect":
								case "pulse":
								case "punch":
								case "recharge":
								case "reflectable":
								case "snatch":
								case "sound":
									conditions.Add($"{field} != 0");
									break;
								case "cool":
								case "clever":
								case "tough":
								case "beautiful":
								case "cute":
									// Condition
									if (conditionSpecified) throw new ArgumentException("Can't specify more than one Contest condition.");
									conditions.Add($"ContestCondition = $v{parameterIndex}");
									command.Parameters.AddWithValue("$v" + parameterIndex, field);
									conditionSpecified = true;
									break;
								default: throw new ArgumentException($"Don't know how to search for '{field}'.");
							}
						}
					}
				}

				if (useLevel) {
					if (pokemonKey == null) throw new ArgumentException("Must specify a Pokémon when sorting by level learned.");
					command.Parameters.AddWithValue("$pokemonLL", pokemonKey);
					extraClauses.Add($"INNER JOIN (SELECT * FROM LearnsetsLevel WHERE Pokemon = $pokemonLL) AS LearnsetsLevelFiltered ON Moves.Key = LearnsetsLevelFiltered.Move");
				}

				command.CommandText = "SELECT COUNT(*) FROM Moves " + string.Join(' ', extraClauses)
					+ (conditions.Count > 0 ? "\nWHERE " + string.Join(" AND ", conditions) : "");

				var rowCount = (long) (await command.ExecuteScalarAsync())!;
				if (rowCount == 0) {
					e.Reply("No matching moves were found.");
					return;
				}

				const int MaxRows = 10;
				command.CommandText = baseClause + " " + string.Join(' ', extraClauses)
					+ (conditions.Count > 0 ? "\nWHERE " + string.Join(" AND ", conditions) : "")
					+ $"\nORDER BY {orderBy ?? "Number ASC"} LIMIT {MaxRows}";

				using var reader = await command.ExecuteReaderAsync();
				var message = string.Join(", ", reader.Cast<DbDataRecord>().Select(r => r.GetString("Name") + (reader.FieldCount > 1 ? $"({reader.GetValue(1)})" : "")));
				if (rowCount > MaxRows) message += $", _{rowCount - MaxRows} more_";
				e.Reply(message);
			} catch (Exception ex) {
				e.Fail(ex.Message);
			}
		}

		private static readonly Dictionary<string, (string label, HashSet<string> items, bool preload)> aggregateItemsList = new() {
			{ "anyapricorn", ("Any Apricorn", new() { "blackapricorn", "yellowapricorn", "pinkapricorn", "redapricorn", "greenapricorn", "whiteapricorn", "blueapricorn" }, false) },
			{ "anyincense", ("Any incense", new() { "fullincense", "laxincense", "oddincense", "rockincense", "roseincense", "seaincense", "waveincense", "luckincense", "pureincense" }, false) },
			{ "anyrepel", ("Any Repel", new() { "repel", "superrepel", "maxrepel" }, true) },
			{ "anyvitamin", ("Any EV vitamin", new() { "calcium", "carbos", "hpup", "iron", "protein", "zinc" }, true) },
			{ "anyevolutionstone", ("Any evolution stone", new() { "dawnstone", "duskstone", "firestone", "icestone", "leafstone", "moonstone", "shinystone", "sunstone", "thunderstone", "waterstone" }, false) },
			{ "anyfeather", ("Any feather", new() { "prettyfeather", "cleverfeather", "geniusfeather", "healthfeather", "musclefeather", "resistfeather", "swiftfeather" }, true) },
			{ "anyterrainseed", ("Any terrain seed", new() { "electricseed", "grassyseed", "mistyseed", "psychicseed" }, false) },
			{ "anyfossil", ("Any fossil", new() { "fossilizedbird", "fossilizeddino", "fossilizeddrake", "fossilizedfish" }, false) },
			{ "anymint", ("Any mint", new() { "gentlemint", "hastymint", "laxmint", "lonelymint", "mildmint", "naivemint", "naughtymint", "rashmint", "seriousmint", "boldmint", "calmmint", "carefulmint", "impishmint", "relaxedmint", "sassymint", "bravemint", "quietmint", "adamantmint", "jollymint", "modestmint", "timidmint" }, true) },
			{ "anypoweritem", ("Any Power item", new() { "poweranklet", "powerband", "powerbelt", "powerbracer", "powerlens", "powerweight" }, false) },
			{ "anysweet", ("Any sweet", new() { "berrysweet", "cloversweet", "flowersweet", "lovesweet", "strawberrysweet", "ribbonsweet", "starsweet" }, true) },
			{ "anymemory", ("Any memory", new() { "bugmemory", "darkmemory", "dragonmemory", "electricmemory", "fairymemory", "fightingmemory", "firememory", "flyingmemory", "ghostmemory", "grassmemory", "groundmemory", "icememory", "poisonmemory", "psychicmemory", "rockmemory", "steelmemory", "watermemory" }, false) }
		};

		[Command(new[] { "cram" }, 1, 1, "cram <item>|<move>, [items to exclude]", "Searches for a Cram-o-matic recipe for the specified item or TR.")]
		public async void CommandCram(object? sender, CommandEventArgs e) {
			var parameters = e.Parameters[0].Split(',');
			var item = await this.GetKeyAsync(Generation.SwordShield, parameters[0]);
			var cacheEntry = this.cache[Generation.SwordShield];
			if (cacheEntry.keysMoves.Contains(item)) {
				var command = await this.GetCommandAsync(Generation.SwordShield, "SELECT Item FROM Machines WHERE Move = $Key", ("Key", item));
				item = (string) command.ExecuteScalar();
				if (item == null || !item.StartsWith("tr", StringComparison.InvariantCultureIgnoreCase)) {
					e.Fail("That move is not a TR move.");
					return;
				}
			} else if (!cacheEntry.keysItems.Contains(item)) {
				e.Fail("No such item or move was found.");
				return;
			}

			var excludedItems = new HashSet<string>();
			foreach (var p in parameters.Skip(1)) excludedItems.Add(await this.GetKeyAsync(Generation.SwordShield, p));

			var result = await this.CramCalculate(item, excludedItems);
			if (result.success)
				e.Reply(result.message);
			else
				e.Fail(result.message);
		}

		private async Task<(bool success, string message)> CramCalculate(string item, HashSet<string> excludedItems, HashSet<string>? softExcludedItems = null) {
			async Task<string> getItemNameAsync(string key) {
				var command4 = await this.GetCommandAsync(Generation.SwordShield, "SELECT Name FROM Items WHERE Key = $Key", ("Key", key));
				return (string) (await command4.ExecuteScalarAsync())!;
			}

			SQLiteCommand command;

			// Check for an Apricorn recipe.
			switch (item.ToLowerInvariant()) {
				case "pokeball": case "greatball":
					return (true, $"Apricorn recipe for {await getItemNameAsync(item)} (24.7% chance): Any Apricorns");
				case "ultraball":
					return (true, $"Apricorn recipe for {await getItemNameAsync(item)} (24.7% chance): Green, Pink, Red or Yellow Apricorns");
				case "safariball": case "sportball":
					return (true, $"Apricorn recipe for {await getItemNameAsync(item)} (0.1% chance): Any Apricorns");
				default: {
					command = await this.GetCommandAsync(Generation.SwordShield, "SELECT Input, Rarity FROM CramRecipesApricorn WHERE Output = $Key", ("Key", item));
					using var reader = await command.ExecuteReaderAsync();
					if (reader.Read()) {
						var chance = reader.GetString("Rarity") switch { "Common" => "24.7%", "Rare" => "1%", "SuperRare" => "0.1%", _ => "unknown" };
						return (true, $"Apricorn recipe for {await getItemNameAsync(item)} ({chance}% chance): {await getItemNameAsync(reader.GetString("Input")!)}");
					}
					break;
				}
			}

			// Check for a special recipe.
			var anyRecipes = false;
			command = await this.GetCommandAsync(Generation.SwordShield, "SELECT Input FROM CramRecipesSpecial INNER JOIN Items ON CramRecipesSpecial.Input = Items.Key WHERE Output = $Key ORDER BY Items.Rarity", ("Key", item));
			using (var reader = await command.ExecuteReaderAsync()) {
				while (reader.Read()) {
					anyRecipes = true;
					var item0 = reader.GetString("Input")!;
					if (!excludedItems.Contains(item0)) {
						var item0Name = await getItemNameAsync(item0);
						return (true, $"Special recipe for {await getItemNameAsync(item)}: {item0Name}, any item, {item0Name}, {item0Name}");
					}
				}
			}

			// Check for a standard recipe.
			command = await this.GetCommandAsync(Generation.SwordShield, "SELECT * FROM CramRecipes WHERE Output = $Key", ("Key", item));
			using (var reader = await command.ExecuteReaderAsync()) {
				if (reader.Read()) {
					anyRecipes = true;
					// Do some calculations to prepare to search for recipes.
					// Work out the 'least rare/difficult to farm' item for each value.
					var minCostItemByValue = new Dictionary<int, (string item, int cost)>();
					using (var reader2 = await (await this.GetCommandAsync(Generation.SwordShield, "SELECT Key, Value, Rarity FROM Items WHERE Value IS NOT NULL")).ExecuteReaderAsync()) {
						while (reader2.Read()) {
							var (item2, value, cost) = (reader2.GetString("Key")!, reader2.GetInt32("Value"), reader2.GetInt32("Rarity"));
							if (item2 != item && !excludedItems.Contains(item2) && softExcludedItems?.Contains(item2) != true) {
								if (!minCostItemByValue.TryGetValue(value, out var entry) || cost < entry.cost)
									minCostItemByValue[value] = (item2, cost);
							}
						}
					}

					// Order values by the cost of the lowest-cost item with that value.
					var values = minCostItemByValue.ToArray();
					Array.Sort(values, (a, b) => a.Value.cost - b.Value.cost);

					var minSingleItemValue = values.Min(e => e.Key);
					var maxSingleItemValue = values.Max(e => e.Key);

					// Find recipes.
					(Type recipeType, int recipeValue, string[] items, int totalCost) bestRecipe = (default, default, new string[4], int.MaxValue);
					do {
						var type = reader.GetEnum<Type>("Type");
						var maxValue = reader.GetInt32("Value");
						var minValue = maxValue == 20 ? 2 : maxValue - 8;

						// Find possible first items.
						var command2 = await this.GetCommandAsync(Generation.SwordShield, "SELECT Key, Value, Rarity FROM Items WHERE Type = $Type AND Value BETWEEN $Min AND $Max AND Rarity < $Cost",
							("Type", type.ToString()),
							("Min", minValue - maxSingleItemValue * 3),
							("Max", maxValue - minSingleItemValue * 3),
							("Cost", bestRecipe.totalCost)
						);
						using var reader2 = await command2.ExecuteReaderAsync();
						while (reader2.Read()) {
							var (item0, value0, cost0) = (reader2.GetString("Key")!, reader2.GetInt32("Value"), reader2.GetInt32("Rarity"));
							if (item0 == item || excludedItems.Contains(item0) == true) continue;

							// Find possible other items that add to a value within the required range.
							foreach (var (value1, (item1, cost1)) in values) {
								foreach (var (value2, (item2, cost2)) in values) {
									foreach (var (value3, (item3, cost3)) in values) {
										var totalValue = value0 + value1 + value2 + value3;
										if (totalValue < minValue || totalValue > maxValue) continue;

										var totalCost = cost0 + cost1 + cost2 + cost3;
										if (totalCost >= bestRecipe.totalCost) continue;

										var items = bestRecipe.items;
										items[0] = item0; items[1] = item1; items[2] = item2; items[3] = item3;
										// Reorder items to avoid recipes of certain forms.
										while (true) {
											if (items[0] != items[1]) {
												if (items[0] == items[2]) {
													// ABAC -> AABC
													(items[1], items[2]) = (items[2], items[1]);
													continue;
												} else if (items[0] == items[3]) {
													// ABCA -> AACB
													(items[1], items[3]) = (items[3], items[1]);
													continue;
												} else if (items[1] == items[3] && items[1] != items[2]) {
													// ABCB -> ABBC
													(items[2], items[3]) = (items[3], items[2]);
													continue;
												}
											} else if (items[0] != items[2]) {
												if (items[0] == items[3]) {
													// AABA -> AAAB
													(items[2], items[3]) = (items[3], items[2]);
													continue;
												}
											}
											break;
										}
										bestRecipe = (type, maxValue, bestRecipe.items, totalCost);
									}
								}
							}
						}
					} while (reader.Read());
					
					if (bestRecipe.items[0] != null) {
						// Don't generate a standard recipe that matches a special recipe.
						if (bestRecipe.items[0] == bestRecipe.items[2] && bestRecipe.items[0] == bestRecipe.items[3]) {
							var command2 = await this.GetCommandAsync(Generation.SwordShield, "SELECT Output FROM CramRecipesSpecial WHERE Input = $Key", ("Key", bestRecipe.items[0]));
							if ((await command2.ExecuteScalarAsync()) != null) {
								softExcludedItems ??= new();
								softExcludedItems.Add(bestRecipe.items[0]);
								await reader.DisposeAsync();
								return await this.CramCalculate(item, excludedItems, softExcludedItems);
							}
						}

						var itemList = bestRecipe.items.Select((s, i) => {
							var (key, (label, items, preload)) = aggregateItemsList.FirstOrDefault(g => g.Value.items.Contains(s));
							return (label != null && (i > 0 || preload))
								? label
								: getItemNameAsync(s).Result;
						});
						return (true, $"A possible recipe for {await getItemNameAsync(item)} ({bestRecipe.recipeType} {bestRecipe.recipeValue}): {string.Join(", ", itemList)}");
					}
				}
			}
			
			return (false, anyRecipes ? "No more recipes were found for this item." : $"{await getItemNameAsync(item)} can't be produced using the Cram-o-matic.");
		}

		[Command(new[] { "da" }, 1, 1, "da <pokemon>", "Shows the Ability and moves of the specified Dynamax Adventure rental.")]
		public async void CommandDynamaxAdventure(object? sender, CommandEventArgs e) {
			string key = await this.GetKeyAsync(Generation.SwordShield, e.Parameters[0]);
			var command = await this.GetCommandAsync(Generation.SwordShield, "SELECT Pokemon.Name, Form, Level, Abilities.Name AS Ability FROM DynamaxAdventurePokemon INNER JOIN Pokemon ON DynamaxAdventurePokemon.Pokemon = Pokemon.Key INNER JOIN Abilities ON DynamaxAdventurePokemon.Ability = Abilities.Key WHERE DynamaxAdventurePokemon.Pokemon = $Key",
				("Key", key));

			using var reader = await command.ExecuteReaderAsync();
			if (!reader.Read()) {
				e.Fail("No such rental or boss Pokémon was found.");
				return;
			}

			var typesString = await this.FormatTypes(Generation.SwordShield, key, false);

			var command3 = await this.GetCommandAsync(Generation.SwordShield, "SELECT Slot, Moves.Name AS Move FROM DynamaxAdventurePokemonMoves INNER JOIN Moves ON DynamaxAdventurePokemonMoves.Move = Moves.Key WHERE Pokemon = $Key ORDER BY Slot",
				("Key", key));
			using var reader3 = await command3.ExecuteReaderAsync();
			var movesString = string.Join(", ", reader3.Cast<DbDataRecord>().Select(r => r.GetString("Move") + r.GetInt32("Slot") switch { 4 => " (limit; once)", 5 => " (limit; end of each turn)", 6 => " (limit; after normal move)", _ => "" }));

			e.Reply($"{DescribeForm(reader.GetString("Name")!, reader.GetString("Form"))} - {typesString}, level {reader.GetInt32("Level")}, Ability: {reader.GetString("Ability")} - Moves: {movesString}");
		}
	}
}
