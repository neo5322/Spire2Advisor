using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestceSpire.Tracking;

namespace QuestceSpire.Core;

public class TierEngine
{
	private readonly Dictionary<string, CharacterCardTiers> _cardTiers = new Dictionary<string, CharacterCardTiers>();

	private readonly Dictionary<string, RelicTierFile> _relicTiers = new Dictionary<string, RelicTierFile>();

	// Pre-indexed lookups: character -> normalizedId -> entry
	private readonly Dictionary<string, Dictionary<string, CardTierEntry>> _cardIndex = new Dictionary<string, Dictionary<string, CardTierEntry>>();
	private readonly Dictionary<string, Dictionary<string, RelicTierEntry>> _relicIndex = new Dictionary<string, Dictionary<string, RelicTierEntry>>();

	// Auto-tier fallback: character -> normalizedId -> generated entry
	private readonly Dictionary<string, Dictionary<string, CardTierEntry>> _autoTierIndex = new Dictionary<string, Dictionary<string, CardTierEntry>>();

	private readonly string _dataPath;

	/// <summary>
	/// The patch version from loaded tier data (e.g., "v0.100.0").
	/// Returns the most common patchVersion across all loaded tier files, or null if not set.
	/// </summary>
	public string TierDataVersion { get; private set; }

	/// <summary>
	/// The detected STS2 game version from the loaded assembly, or null if not detected.
	/// </summary>
	public string GameVersion { get; private set; }

	/// <summary>
	/// True if tier data patchVersion doesn't match the detected game version.
	/// </summary>
	public bool IsTierDataStale => TierDataVersion != null && GameVersion != null &&
		TierDataVersion.TrimStart('v') != GameVersion.TrimStart('v');

	public TierEngine(string dataPath)
	{
		_dataPath = dataPath;
		LoadCardTiers(Path.Combine(dataPath, "CardTiers"));
		LoadRelicTiers(Path.Combine(dataPath, "RelicTiers"));
		LoadAutoTiers(Path.Combine(dataPath, "auto_tiers"));
		DetectVersions();
	}

	/// <summary>
	/// Reload all tier data from disk. Call after DataUpdater downloads new files.
	/// </summary>
	public void Reload()
	{
		_cardTiers.Clear();
		_relicTiers.Clear();
		_cardIndex.Clear();
		_relicIndex.Clear();
		_autoTierIndex.Clear();
		LoadCardTiers(Path.Combine(_dataPath, "CardTiers"));
		LoadRelicTiers(Path.Combine(_dataPath, "RelicTiers"));
		LoadAutoTiers(Path.Combine(_dataPath, "auto_tiers"));
		DetectVersions();
		Plugin.Log("TierEngine: reloaded all tier data.");
	}

	private void LoadCardTiers(string folder)
	{
		if (!Directory.Exists(folder))
		{
			Plugin.Log("Card tier folder not found: " + folder);
			return;
		}
		string[] files = Directory.GetFiles(folder, "*.json");
		foreach (string text in files)
		{
			try
			{
				CharacterCardTiers characterCardTiers = JsonConvert.DeserializeObject<CharacterCardTiers>(File.ReadAllText(text));
				if (characterCardTiers != null && characterCardTiers.Character != null)
				{
					string charKey = characterCardTiers.Character.ToLowerInvariant();
					_cardTiers[charKey] = characterCardTiers;
					var index = new Dictionary<string, CardTierEntry>(StringComparer.OrdinalIgnoreCase);
					if (characterCardTiers.Cards != null)
					{
						foreach (var card in characterCardTiers.Cards)
						{
							string normId = NormalizeId(card.Id);
							if (!string.IsNullOrEmpty(normId))
								index[normId] = card;
						}
					}
					_cardIndex[charKey] = index;
					Plugin.Log($"Loaded {characterCardTiers.Cards?.Count ?? 0} card tiers for {characterCardTiers.Character}");
				}
			}
			catch (Exception ex)
			{
				Plugin.Log("Failed to load card tiers from " + text + ": " + ex.Message);
			}
		}
	}

	private void LoadRelicTiers(string folder)
	{
		if (!Directory.Exists(folder))
		{
			Plugin.Log("Relic tier folder not found: " + folder);
			return;
		}
		string[] files = Directory.GetFiles(folder, "*.json");
		foreach (string text in files)
		{
			try
			{
				RelicTierFile relicTierFile = JsonConvert.DeserializeObject<RelicTierFile>(File.ReadAllText(text));
				if (relicTierFile != null && relicTierFile.Category != null)
				{
					string catKey = relicTierFile.Category.ToLowerInvariant();
					_relicTiers[catKey] = relicTierFile;
					var index = new Dictionary<string, RelicTierEntry>(StringComparer.OrdinalIgnoreCase);
					if (relicTierFile.Relics != null)
					{
						foreach (var relic in relicTierFile.Relics)
						{
							string normId = NormalizeId(relic.Id);
							if (!string.IsNullOrEmpty(normId))
								index[normId] = relic;
						}
					}
					_relicIndex[catKey] = index;
					Plugin.Log($"Loaded {relicTierFile.Relics?.Count ?? 0} relic tiers for {relicTierFile.Category}");
				}
			}
			catch (Exception ex)
			{
				Plugin.Log("Failed to load relic tiers from " + text + ": " + ex.Message);
			}
		}
	}

	private void LoadAutoTiers(string folder)
	{
		if (!Directory.Exists(folder)) return;
		string[] files = Directory.GetFiles(folder, "*.json");
		int total = 0;
		foreach (string file in files)
		{
			try
			{
				var json = JsonConvert.DeserializeObject<AutoTierFile>(File.ReadAllText(file));
				if (json?.Cards == null || json.Character == null) continue;
				string charKey = json.Character.ToLowerInvariant();
				var index = new Dictionary<string, CardTierEntry>(StringComparer.OrdinalIgnoreCase);
				foreach (var entry in json.Cards)
				{
					string normId = NormalizeId(entry.Id);
					if (string.IsNullOrEmpty(normId)) continue;
					index[normId] = new CardTierEntry
					{
						Id = entry.Id,
						BaseTier = entry.BaseTier,
						Notes = $"Auto-tier (score: {entry.Score:F2})"
					};
				}
				_autoTierIndex[charKey] = index;
				total += index.Count;
			}
			catch (Exception ex)
			{
				Plugin.Log($"Failed to load auto tiers from {file}: {ex.Message}");
			}
		}
		if (total > 0)
			Plugin.Log($"Loaded {total} auto-tier entries as fallback.");
	}

	private void DetectVersions()
	{
		// Extract tier data version from loaded files
		var versions = _cardTiers.Values
			.Where(t => !string.IsNullOrEmpty(t.PatchVersion))
			.Select(t => t.PatchVersion)
			.GroupBy(v => v)
			.OrderByDescending(g => g.Count())
			.FirstOrDefault();
		TierDataVersion = versions?.Key;

		// Detect game version from sts2 assembly
		try
		{
			var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault(a => a.GetName().Name == "sts2" ||
				                     (a.GetName().Name?.Contains("MegaCrit") == true));
			if (gameAssembly != null)
			{
				var ver = gameAssembly.GetName().Version;
				if (ver != null)
					GameVersion = $"{ver.Major}.{ver.Minor}.{ver.Build}";
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"TierEngine: failed to detect game version: {ex.Message}");
		}

		if (TierDataVersion != null)
			Plugin.Log($"TierEngine: tier data version={TierDataVersion}, game version={GameVersion ?? "unknown"}");
		if (IsTierDataStale)
			Plugin.Log($"WARNING: Tier data ({TierDataVersion}) may be outdated for game version {GameVersion}");

		ValidateSynergyTags();
	}

	private void ValidateSynergyTags()
	{
		// Collect all known archetype/synergy tags
		var knownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		// Add common gameplay tags that aren't archetypes
		knownTags.UnionWith(new[] {
			"draw", "scaling", "damage", "defense", "aoe", "block",
			"energy", "exhaust", "flexible", "upgraded", "healing",
			"gold", "card_manipulation", "status", "debuff", "buff",
			"multi_hit", "retain", "innate", "ethereal"
		});

		// Add archetype tags (core + support) from all characters
		foreach (var (_, archetypes) in ArchetypeDefinitions.ByCharacter)
		{
			foreach (var arch in archetypes)
			{
				knownTags.Add(arch.Id);
				if (arch.CoreTags != null) knownTags.UnionWith(arch.CoreTags);
				if (arch.SupportTags != null) knownTags.UnionWith(arch.SupportTags);
			}
		}

		int unknownCount = 0;
		var unknownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (var (character, tiers) in _cardTiers)
		{
			if (tiers.Cards == null) continue;
			foreach (var card in tiers.Cards)
			{
				if (card.Synergies == null) continue;
				foreach (var tag in card.Synergies)
				{
					if (!knownTags.Contains(tag) && unknownTags.Add(tag))
						unknownCount++;
				}
			}
		}

		if (unknownCount > 0)
			Plugin.Log($"TierEngine: {unknownCount} unrecognized synergy tags found: {string.Join(", ", unknownTags.Take(10))}");
	}

	private class AutoTierFile
	{
		[JsonProperty("character")] public string Character { get; set; }
		[JsonProperty("cards")] public List<AutoTierEntry> Cards { get; set; }
	}

	private static string NormalizeId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return "";
		}
		return id.Replace(' ', '_').Replace('-', '_').Replace("'", "").TrimEnd('+');
	}

	public CardTierEntry GetCardTier(string character, string cardId)
	{
		string normId = NormalizeId(cardId);
		string charKey = character?.ToLowerInvariant();
		if (charKey != null && _cardIndex.TryGetValue(charKey, out var charIdx) && charIdx.TryGetValue(normId, out var entry))
		{
			return entry;
		}
		if (_cardIndex.TryGetValue("colorless", out var colorlessIdx) && colorlessIdx.TryGetValue(normId, out var colorlessEntry))
		{
			return colorlessEntry;
		}
		// Fallback to auto-generated tiers from data pipelines
		if (charKey != null && _autoTierIndex.TryGetValue(charKey, out var autoIdx) && autoIdx.TryGetValue(normId, out var autoEntry))
		{
			return autoEntry;
		}
		return null;
	}

	public RelicTierEntry GetRelicTier(string character, string relicId)
	{
		string normId = NormalizeId(relicId);
		string charKey = character?.ToLowerInvariant();
		if (charKey != null && _relicIndex.TryGetValue(charKey, out var charIdx) && charIdx.TryGetValue(normId, out var entry))
		{
			return entry;
		}
		if (_relicIndex.TryGetValue("common", out var commonIdx) && commonIdx.TryGetValue(normId, out var commonEntry))
		{
			return commonEntry;
		}
		return null;
	}

	public static TierGrade ParseGrade(string grade)
	{
		if (string.IsNullOrEmpty(grade))
		{
			return TierGrade.C;
		}
		return grade.Trim().ToUpperInvariant() switch
		{
			"S" => TierGrade.S, 
			"A" => TierGrade.A, 
			"B" => TierGrade.B, 
			"C" => TierGrade.C, 
			"D" => TierGrade.D, 
			"F" => TierGrade.F, 
			_ => TierGrade.C, 
		};
	}

	public static TierGrade ScoreToGrade(float score)
	{
		if (score >= 4.5f)
		{
			return TierGrade.S;
		}
		if (score >= 3.5f)
		{
			return TierGrade.A;
		}
		if (score >= 2.5f)
		{
			return TierGrade.B;
		}
		if (score >= 1.5f)
		{
			return TierGrade.C;
		}
		if (score >= 0.5f)
		{
			return TierGrade.D;
		}
		return TierGrade.F;
	}

	/// <summary>
	/// Returns a sub-grade string (e.g. "A+", "B-") for finer differentiation.
	/// Each major grade spans 1.0 points; +/- split the top/bottom third.
	/// </summary>
	public static string ScoreToSubGrade(float score)
	{
		if (score >= 5.2f) return "S+";
		if (score >= 4.8f) return "S";
		if (score >= 4.5f) return "S-";
		if (score >= 4.17f) return "A+";
		if (score >= 3.83f) return "A";
		if (score >= 3.5f) return "A-";
		if (score >= 3.17f) return "B+";
		if (score >= 2.83f) return "B";
		if (score >= 2.5f) return "B-";
		if (score >= 2.17f) return "C+";
		if (score >= 1.83f) return "C";
		if (score >= 1.5f) return "C-";
		if (score >= 1.0f) return "D+";
		if (score >= 0.5f) return "D";
		return "F";
	}
}
