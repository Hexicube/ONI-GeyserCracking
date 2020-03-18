using Newtonsoft.Json;
using PeterHan.PLib;
using PeterHan.PLib.Options;
using System;

namespace HexiGeyserCracking {
	[ModInfo("Geyser Cracking"), ConfigFile("config.json", true)]
	[Serializable]
	public class ConfigData : POptions.SingletonOptions<ConfigData> {
		[JsonProperty]
		[Option("Max Cracking", "The maximum performance allowed for a Geyser"), Limit(1, 25)]
		public float MaxCracking { get; set; } = 4f;
		[JsonProperty]
		[Option("Min Per Crack", "The minimum performance increase when cracking"), Limit(0, 1)]
		public float MinPerCrack { get; set; } = 0.15f;
		[JsonProperty]
		[Option("Max Per Crack", "The maximum performance increase when cracking"), Limit(0, 1)]
		public float MaxPerCrack { get; set; } = 0.3f;
		[JsonProperty]
		[Option("KG Per Crack", "The amount of Sulfur to use when cracking"), Limit(1, 10000)]
		public float KgPerCrack { get; set; } = 100f;
	}
}
