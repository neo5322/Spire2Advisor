using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuestceSpire.Core;

public class CharacterCardTiers
{
	[JsonProperty("character")]
	public string Character { get; set; }

	[JsonProperty("cards")]
	public List<CardTierEntry> Cards { get; set; } = new List<CardTierEntry>();

	[JsonProperty("patchVersion")]
	public string PatchVersion { get; set; }

	[JsonProperty("lastUpdated")]
	public string LastUpdated { get; set; }

	[JsonProperty("source")]
	public string Source { get; set; }
}
