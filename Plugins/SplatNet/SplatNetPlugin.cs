using System.Net;
using System.Reflection;
using CBot;
using Newtonsoft.Json;

namespace SplatNet;
[ApiVersion(4, 0)]
public class SplatNetPlugin : Plugin {
	private static (Dictionary<string, List<BattleRotation>> data, DateTime expiry)? battleRotations;
	private static (SalmonRunData data, DateTime expiry)? salmonRunRotations;

	public override string Name => "SplatNet 2";

	[Command(new[] { "stages2", "maps2", "currentstages2", "currentmaps2" }, 0, 1, "stages [mode]", "Returns the current rotation for the specified mode.")]
	public void CommandStages(object? sender, CommandEventArgs e)
		=> HandleBattleRotationsCommand(e, (list, now) => list.First(r => now >= r.StartTime.ToUniversalTime() && now < r.EndTime.ToUniversalTime()), (d, r) => r.EndTime < d ? r.EndTime : d, "{0} ({1} remain)");
	[Command(new[] { "nextstages2", "nextmaps2" }, 0, 1, "nextstages [mode]", "Returns the next rotation for the specified mode.")]
	public void CommandNextStages(object? sender, CommandEventArgs e)
		=> HandleBattleRotationsCommand(e, (list, now) => {
			BattleRotation? rotation = null;
			foreach (var rotation2 in list.Where(r => now < r.StartTime.ToUniversalTime())) {
				if (rotation == null || rotation2.StartTime < rotation.StartTime) rotation = rotation2;
			}
			return rotation ?? throw new WebException("No rotation found?!");
		}, (d, r) => r.StartTime < d ? r.StartTime : d, "{0} (in {1})");

	public static void HandleBattleRotationsCommand(CommandEventArgs e, Func<IList<BattleRotation>, DateTime, BattleRotation> rotationSelector, Func<DateTime, BattleRotation, DateTime> dateTimeAccumulator, string formatString) {
		IEnumerable<string> modes;
		if (e.Parameters.Length == 1) {
			switch (e.Parameters[0].Replace(" ", "").ToLowerInvariant()) {
				case "regular": case "regularbattle": case "turf": case "turfwar": case "turfies":
					modes = new[] { "regular" }; break;
				case "ranked": case "rankedbattle": case "gachi":
					modes = new[] { "gachi" }; break;
				case "league": case "leaguuebattle":
					modes = new[] { "league" }; break;
				default:
					e.Fail("Unknown mode: " + e.Parameters[0]);
					return;
			}
		} else
			modes = new[] { "regular", "gachi", "league" };

		var now = DateTime.UtcNow;

		if (battleRotations == null || now >= battleRotations.Value.expiry) {
			var request = WebRequest.CreateHttp("https://splatoon2.ink/data/schedules.json");
			request.Accept = "application/json";
			request.UserAgent = $"Angelina/{Assembly.GetExecutingAssembly().GetName().Version} CBot/{Assembly.GetExecutingAssembly().GetName().Version} (mailto:andrio@questers-rest.andriocelos.net)";
			using var response = request.GetResponse();
			using var reader = new StreamReader(response.GetResponseStream());
			var json = reader.ReadToEnd();
			var data = JsonConvert.DeserializeObject<Dictionary<string, List<BattleRotation>>>(json) ?? throw new WebException("API returned null?!");
			var nextRotationEndTime = data.SelectMany(entry => entry.Value.Select(r => r.EndTime.ToUniversalTime())).Where(d => d > now).Min();
			battleRotations = (data, nextRotationEndTime);
		}

		var time = DateTime.MaxValue;
		var rotationInfo = string.Join(" | ", modes.Select(mode => {
			var rotation = rotationSelector(battleRotations.Value.data[mode], now);
			time = dateTimeAccumulator(time, rotation);
			return $"{rotation.GameMode.Name}: {rotation.Rule.Name} on {rotation.StageA.Name} and {rotation.StageB.Name}";
		}));
		e.Reply(string.Format(formatString, rotationInfo, FormatDuration(time - DateTime.UtcNow)));
	}

	internal static string FormatDuration(TimeSpan duration) {
		if (duration < TimeSpan.FromHours(1)) return $"{duration.Minutes}m";
		return $"{duration.Hours:0}h {duration.Minutes}m";
	}

	[Command(new[] { "sr2", "currentsr2" }, 0, 0, "sr", "Returns the current Salmon Run rotation.")]
	public void CommandSalmonRunStages(object? sender, CommandEventArgs e)
		=> HandleSalmonRunRotationsCommand(e, (list, now) => list.FirstOrDefault(r => now >= r.StartTime.ToUniversalTime() && now < r.EndTime.ToUniversalTime()), r => r.EndTime, "{0} ({1} remain)");
	[Command(new[] { "nextsr2" }, 0, 0, "nextsr", "Returns the next Salmon Run rotation.")]
	public void CommandNextSalmonRunStages(object? sender, CommandEventArgs e)
		=> HandleSalmonRunRotationsCommand(e, (list, now) => {
			SalmonRunRotation? rotation = null;
			foreach (var rotation2 in list.Where(r => now < r.StartTime.ToUniversalTime())) {
				if (rotation == null || rotation2.StartTime < rotation.StartTime) rotation = rotation2;
			}
			return rotation;
		}, r => r.StartTime, "{0} (in {1})");

	public static void HandleSalmonRunRotationsCommand(CommandEventArgs e, Func<IList<SalmonRunRotation>, DateTime, SalmonRunRotation?> rotationSelector, Func<SalmonRunRotation, DateTime> dateTimeSelector, string formatString) {
		var now = DateTime.UtcNow;

		if (salmonRunRotations == null || now >= salmonRunRotations.Value.expiry) {
			var request = WebRequest.CreateHttp("https://splatoon2.ink/data/coop-schedules.json");
			request.Accept = "application/json";
			request.UserAgent = $"Angelina/{Assembly.GetExecutingAssembly().GetName().Version} CBot/{Assembly.GetExecutingAssembly().GetName().Version} (mailto:andrio@questers-rest.andriocelos.net)";
			using var response = request.GetResponse();
			using var reader = new StreamReader(response.GetResponseStream());
			var json = reader.ReadToEnd();
			var salmonRunData = JsonConvert.DeserializeObject<SalmonRunData>(json) ?? throw new WebException("API returned null?!");
			var nextRotationEndTime = salmonRunData.Details.Select(r => r.EndTime.ToUniversalTime()).Where(d => d > now).Min();
			salmonRunRotations = (salmonRunData, nextRotationEndTime);
		}

		var rotation = rotationSelector(salmonRunRotations.Value.data.Details, now);
		if (rotation == null) {
			e.Reply($"Salmon Run is currently closed. The next rotation starts in {FormatDuration(salmonRunRotations.Value.data.Details.Where(r => r.StartTime >= now).Min(r => r.StartTime) - now)}.");
			return;
		}
		var rotationInfo = $"{rotation.Stage.Name} with {string.Join(", ", rotation.Weapons.Select(w => w.Data != null ? w.Data.Name : w.CoopSpecialWeapon != null ? w.CoopSpecialWeapon.Name : "null"))}";
		e.Reply(string.Format(formatString, rotationInfo, FormatDuration(dateTimeSelector(rotation) - DateTime.UtcNow)));
	}
}
