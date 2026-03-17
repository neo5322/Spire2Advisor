using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuestceSpire.Tracking;

/// <summary>
/// Syncs game entity data (cards, relics, potions, monsters) from the Spire Codex API.
/// Provides structured numerical data that CardPropertyScorer, EnemyAdvisor, and BossAdvisor consume.
/// </summary>
public class SpireCodexSync
{
	private const string BaseUrl = "https://spire-codex.com";
	private static readonly TimeSpan RateLimit = TimeSpan.FromSeconds(1);

	private readonly string _dataFolder;
	private string _lastVersion;

	public SpireCodexSync(string dataFolder)
	{
		_dataFolder = dataFolder;
		LoadLastVersion();
	}

	public async Task SyncAll()
	{
		Plugin.Log("SpireCodexSync: starting sync...");

		// Check version first
		string currentVersion = await FetchVersion();
		if (currentVersion != null && currentVersion == _lastVersion)
		{
			Plugin.Log("SpireCodexSync: data is up to date.");
			return;
		}

		int synced = 0;
		synced += await SyncEndpoint("cards", "codex_cards.json") ? 1 : 0;
		synced += await SyncEndpoint("relics", "codex_relics.json") ? 1 : 0;
		synced += await SyncEndpoint("potions", "codex_potions.json") ? 1 : 0;
		synced += await SyncEndpoint("monsters", "codex_monsters.json") ? 1 : 0;

		if (currentVersion != null)
		{
			_lastVersion = currentVersion;
			SaveLastVersion();
		}

		Plugin.Log($"SpireCodexSync: synced {synced} endpoints.");
	}

	private async Task<string> FetchVersion()
	{
		try
		{
			var json = await PipelineHttp.GetAsync($"{BaseUrl}/version", RateLimit);
			var obj = JObject.Parse(json);
			return obj["version"]?.ToString();
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: version check failed — {ex.Message}");
			return null;
		}
	}

	private async Task<bool> SyncEndpoint(string endpoint, string localFile)
	{
		try
		{
			var json = await PipelineHttp.RetryAsync(
				() => PipelineHttp.GetAsync($"{BaseUrl}/{endpoint}", RateLimit));

			string path = Path.Combine(_dataFolder, localFile);
			File.WriteAllText(path, json);

			// Parse and log count
			var arr = JToken.Parse(json);
			int count = arr is JArray ja ? ja.Count : 0;
			Plugin.Log($"SpireCodexSync: {endpoint} → {count} entries saved to {localFile}");
			return true;
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: {endpoint} failed — {ex.Message}");
			return false;
		}
	}

	private void LoadLastVersion()
	{
		try
		{
			string path = Path.Combine(_dataFolder, ".codex_version");
			if (File.Exists(path))
				_lastVersion = File.ReadAllText(path).Trim();
		}
		catch { }
	}

	private void SaveLastVersion()
	{
		try
		{
			string path = Path.Combine(_dataFolder, ".codex_version");
			File.WriteAllText(path, _lastVersion);
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: failed to save version — {ex.Message}");
		}
	}

	/// <summary>
	/// Load cached codex monster data for EnemyAdvisor/BossAdvisor enrichment.
	/// Returns list of {id, name, hp, damage, ...} objects.
	/// </summary>
	public static List<CodexMonster> LoadCachedMonsters(string dataFolder)
	{
		try
		{
			string path = Path.Combine(dataFolder, "codex_monsters.json");
			if (!File.Exists(path)) return new List<CodexMonster>();
			var json = File.ReadAllText(path);
			return JsonConvert.DeserializeObject<List<CodexMonster>>(json) ?? new List<CodexMonster>();
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: failed to load cached monsters — {ex.Message}");
			return new List<CodexMonster>();
		}
	}

	/// <summary>
	/// Load cached codex card data for CardPropertyScorer enrichment.
	/// </summary>
	public static List<CodexCard> LoadCachedCards(string dataFolder)
	{
		try
		{
			string path = Path.Combine(dataFolder, "codex_cards.json");
			if (!File.Exists(path)) return new List<CodexCard>();
			var json = File.ReadAllText(path);
			return JsonConvert.DeserializeObject<List<CodexCard>>(json) ?? new List<CodexCard>();
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: failed to load cached cards — {ex.Message}");
			return new List<CodexCard>();
		}
	}

	/// <summary>
	/// Load cached codex potion data.
	/// </summary>
	public static List<CodexPotion> LoadCachedPotions(string dataFolder)
	{
		try
		{
			string path = Path.Combine(dataFolder, "codex_potions.json");
			if (!File.Exists(path)) return new List<CodexPotion>();
			var json = File.ReadAllText(path);
			return JsonConvert.DeserializeObject<List<CodexPotion>>(json) ?? new List<CodexPotion>();
		}
		catch (Exception ex)
		{
			Plugin.Log($"SpireCodexSync: failed to load cached potions — {ex.Message}");
			return new List<CodexPotion>();
		}
	}
}

// Codex data models — flexible to match API response
public class CodexMonster
{
	[JsonProperty("id")] public string Id { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("hp")] public int? Hp { get; set; }
	[JsonProperty("hp_min")] public int? HpMin { get; set; }
	[JsonProperty("hp_max")] public int? HpMax { get; set; }
	[JsonProperty("type")] public string Type { get; set; }
	[JsonProperty("act")] public int? Act { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JToken> Extra { get; set; }
}

public class CodexCard
{
	[JsonProperty("id")] public string Id { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("character")] public string Character { get; set; }
	[JsonProperty("type")] public string Type { get; set; }
	[JsonProperty("rarity")] public string Rarity { get; set; }
	[JsonProperty("cost")] public int? Cost { get; set; }
	[JsonProperty("damage")] public int? Damage { get; set; }
	[JsonProperty("block")] public int? Block { get; set; }
	[JsonProperty("description")] public string Description { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JToken> Extra { get; set; }
}

public class CodexPotion
{
	[JsonProperty("id")] public string Id { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("rarity")] public string Rarity { get; set; }
	[JsonProperty("description")] public string Description { get; set; }

	[JsonExtensionData]
	public Dictionary<string, JToken> Extra { get; set; }
}
