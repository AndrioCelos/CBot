using System;
using System.Collections.Generic;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SplatNet {
	public class Rotation {
		[JsonProperty("start_time"), JsonConverter(typeof(UnixDateTimeConverter))]
		public DateTime StartTime { get; }
		[JsonProperty("end_time"), JsonConverter(typeof(UnixDateTimeConverter))]
		public DateTime EndTime { get; }

		[JsonConstructor]
		public Rotation(DateTime startTime, DateTime endTime) {
			this.StartTime = startTime;
			this.EndTime = endTime;
		}
	}

	public class BattleRotation : Rotation {
		[JsonProperty("id")]
		public long ID { get; }
		[JsonProperty("game_mode")]
		public GameMode GameMode { get; }
		[JsonProperty("rule")]
		public GameMode Rule { get; }
		[JsonProperty("stage_a")]
		public Entity StageA { get; }
		[JsonProperty("stage_b")]
		public Entity StageB { get; }

		[JsonConstructor]
		public BattleRotation(long id, GameMode gameMode, GameMode rule, DateTime startTime, DateTime endTime, Entity stageA, Entity stageB) : base(startTime, endTime) {
			this.ID = id;
			this.GameMode = gameMode ?? throw new ArgumentNullException(nameof(gameMode));
			this.Rule = rule ?? throw new ArgumentNullException(nameof(rule));
			this.StageA = stageA ?? throw new ArgumentNullException(nameof(stageA));
			this.StageB = stageB ?? throw new ArgumentNullException(nameof(stageB));
		}
	}

	public class GameMode {
		[JsonProperty("key")]
		public string Key { get; }
		[JsonProperty("name")]
		public string Name { get; }

		[JsonConstructor]
		public GameMode(string key, string name) {
			this.Key = key ?? throw new ArgumentNullException(nameof(key));
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}
	}

	public class Entity {
		[JsonProperty("id")]
		public string ID { get; }
		[JsonProperty("name")]
		public string Name { get; }

		[JsonConstructor]
		public Entity(string id, string name) {
			this.ID = id ?? throw new ArgumentNullException(nameof(id));
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}
	}

	public class Stage {
		[JsonProperty("name")]
		public string Name { get; }

		[JsonConstructor]
		public Stage(string name) {
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}
	}

	public class SalmonRunRotation : Rotation {
		[JsonProperty("stage")]
		public Stage Stage { get; }
		[JsonProperty("weapons")]
		public IList<Weapon> Weapons { get; }

		[JsonConstructor]
		public SalmonRunRotation(DateTime startTime, DateTime endTime, Stage stage, IList<Weapon> weapons) : base(startTime, endTime) {
			this.Stage = stage ?? throw new ArgumentNullException(nameof(stage));
			this.Weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
		}
	}

	public class Weapon {
		[JsonProperty("id")]
		public string ID { get; }
		[JsonProperty("weapon")]
		public Entity? Data { get; }
		[JsonProperty("coop_special_weapon")]
		public CoopSpecialWeapon? CoopSpecialWeapon { get; }

		[JsonConstructor]
		public Weapon(string id, Entity? data, CoopSpecialWeapon? coopSpecialWeapon) {
			if ((data == null && coopSpecialWeapon == null) || (data != null && coopSpecialWeapon != null))
				throw new ArgumentException($"Exactly one of {nameof(data)} or {nameof(coopSpecialWeapon)} must be provided.");
			this.ID = id ?? throw new ArgumentNullException(nameof(id));
			this.Data = data;
			this.CoopSpecialWeapon = coopSpecialWeapon;
		}
	}

	public class CoopSpecialWeapon {
		[JsonProperty("name")]
		public string Name { get; }

		[JsonConstructor]
		public CoopSpecialWeapon(string name) => this.Name = name ?? throw new ArgumentNullException(nameof(name));
	}

	public class SalmonRunData {
		[JsonProperty("details")]
		public IList<SalmonRunRotation> Details { get; }

		public SalmonRunData(IList<SalmonRunRotation> details) => this.Details = details ?? throw new ArgumentNullException(nameof(details));
	}

}
