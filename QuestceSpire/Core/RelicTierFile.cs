using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuestceSpire.Core;

public class RelicTierFile
{
	[JsonProperty("category")]
	public string Category { get; set; }

	[JsonProperty("relics")]
	public List<RelicTierEntry> Relics { get; set; } = new List<RelicTierEntry>();

	[JsonProperty("patchVersion")]
	public string PatchVersion { get; set; }

	[JsonProperty("lastUpdated")]
	public string LastUpdated { get; set; }
}
