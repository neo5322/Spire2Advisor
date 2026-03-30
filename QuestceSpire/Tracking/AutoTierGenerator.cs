using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestceSpire.Core;

namespace QuestceSpire.Tracking;

/// <summary>
/// Generates automatic tier lists from multiple data signals:
/// community win rates, pick rates, co-pick synergy, and card usage effectiveness.
/// </summary>
public class AutoTierGenerator
{
	private readonly RunDatabase _db;
	private readonly string _dataFolder;

	// Weights for multi-signal scoring
	private const float WinRateWeight = 0.40f;
	private const float PickRateWeight = 0.20f;
	private const float SynergyWeight = 0.20f;
	private const float UsageWeight = 0.20f;

	public AutoTierGenerator(RunDatabase db, string dataFolder)
	{
		_db = db;
		_dataFolder = dataFolder;
	}

	public void Generate()
	{
		Plugin.Log("AutoTierGenerator: generating automatic tier lists...");

		var allCardStats = _db.GetAllCommunityCardStats();
		if (allCardStats.Count == 0)
		{
			Plugin.Log("AutoTierGenerator: no community card stats available — skipping.");
			return;
		}

		// Group by character
		var byCharacter = allCardStats.GroupBy(s => s.Character).ToDictionary(g => g.Key, g => g.ToList());

		string autoTierDir = Path.Combine(_dataFolder, "auto_tiers");
		Directory.CreateDirectory(autoTierDir);

		int totalCards = 0;
		foreach (var (character, stats) in byCharacter)
		{
			var entries = new List<AutoTierEntry>();

			foreach (var stat in stats)
			{
				if (stat.SampleSize < 5) continue;

				// Signal 1: Win rate (normalized 0-1)
				float winScore = NormalizeWinRate(stat.WinRateWhenPicked);

				// Signal 2: Pick rate (higher = more valued by community)
				float pickScore = Math.Min(1f, stat.PickRate * 2f);

				// Signal 3: Co-pick synergy potential (from card_pair_stats)
				float synergyScore = GetSynergyPotential(stat.CardId, character);

				// Signal 4: Usage effectiveness (from card_usage_stats)
				float usageScore = GetUsageScore(stat.CardId, character);

				// Weighted combination
				float combined = winScore * WinRateWeight + pickScore * PickRateWeight +
				                 synergyScore * SynergyWeight + usageScore * UsageWeight;

				// Convert to tier grade
				string tier = ScoreToTier(combined);

				entries.Add(new AutoTierEntry
				{
					Id = stat.CardId,
					BaseTier = tier,
					Score = combined,
					WinRate = stat.WinRateWhenPicked,
					PickRate = stat.PickRate,
					SampleSize = stat.SampleSize,
					Signals = new Dictionary<string, float>
					{
						["win_rate"] = winScore,
						["pick_rate"] = pickScore,
						["synergy"] = synergyScore,
						["usage"] = usageScore
					}
				});
			}

			// Sort by score descending
			entries.Sort((a, b) => b.Score.CompareTo(a.Score));

			// Compare with manual tiers and log large discrepancies
			LogDiscrepancies(character, entries);

			// Save auto tiers
			string path = Path.Combine(autoTierDir, $"{character}.json");
			var output = new { character, generated = DateTime.UtcNow.ToString("o"), cards = entries };
			File.WriteAllText(path, JsonConvert.SerializeObject(output, Formatting.Indented));
			totalCards += entries.Count;
		}

		Plugin.Log($"AutoTierGenerator: generated tiers for {totalCards} cards across {byCharacter.Count} characters.");

		// Integrate new cards detected by RuntimeCardExtractor
		IntegrateNewCards(autoTierDir);
	}

	private float NormalizeWinRate(float winRate)
	{
		// Map 35%-58% win rate range to 0-1
		float normalized = (winRate - 0.35f) / (0.58f - 0.35f);
		return Math.Max(0f, Math.Min(1f, normalized));
	}

	private float GetSynergyPotential(string cardId, string character)
	{
		try
		{
			var pairs = _db.GetCardPairStats(character, cardId);
			if (pairs.Count == 0) return 0.5f; // neutral when no data

			// Average win rate of top 5 pairs
			float sum = 0f;
			int count = Math.Min(5, pairs.Count);
			for (int i = 0; i < count; i++)
				sum += pairs[i].WinRate;

			float avgPairWinRate = sum / count;
			return NormalizeWinRate(avgPairWinRate);
		}
		catch (Exception ex)
		{
			Plugin.Log($"AutoTierGenerator: failed to compute synergy potential: {ex.Message}");
			return 0.5f;
		}
	}

	private float GetUsageScore(string cardId, string character)
	{
		try
		{
			var connStr = _db.ConnectionString;
			if (connStr == null) return 0.5f;

			using var conn = new Microsoft.Data.Sqlite.SqliteConnection(connStr);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT effectiveness FROM card_usage_stats WHERE card_id=@id AND character=@char";
			cmd.Parameters.AddWithValue("@id", cardId);
			cmd.Parameters.AddWithValue("@char", character);
			var result = cmd.ExecuteScalar();
			return result != null ? Convert.ToSingle(result) : 0.5f;
		}
		catch (Exception ex)
		{
			Plugin.Log($"AutoTierGenerator: failed to query card usage score: {ex.Message}");
			return 0.5f;
		}
	}

	private static string ScoreToTier(float score)
	{
		return score switch
		{
			>= 0.85f => "S",
			>= 0.70f => "A",
			>= 0.50f => "B",
			>= 0.35f => "C",
			>= 0.20f => "D",
			_ => "F"
		};
	}

	private void LogDiscrepancies(string character, List<AutoTierEntry> autoEntries)
	{
		try
		{
			var tierEngine = Plugin.TierEngine;
			if (tierEngine == null) return;

			int discrepancies = 0;
			foreach (var entry in autoEntries)
			{
				var manualTier = tierEngine.GetCardTier(character, entry.Id);
				if (manualTier == null) continue;

				int autoRank = TierToRank(entry.BaseTier);
				int manualRank = TierToRank(manualTier.BaseTier);
				if (Math.Abs(autoRank - manualRank) >= 2)
				{
					discrepancies++;
					if (discrepancies <= 5) // Only log first 5
						Plugin.Log($"AutoTier: {entry.Id} [{character}] manual={manualTier.BaseTier} auto={entry.BaseTier} (score={entry.Score:F2})");
				}
			}

			if (discrepancies > 0)
				Plugin.Log($"AutoTier: {discrepancies} tier discrepancies found for {character}.");
		}
		catch (Exception ex) { Plugin.Log($"AutoTierGenerator: failed to compare auto tiers with manual tiers: {ex.Message}"); }
	}

	private static int TierToRank(string tier) => tier?.ToUpperInvariant() switch
	{
		"S" => 5, "A" => 4, "B" => 3, "C" => 2, "D" => 1, "F" => 0, _ => -1
	};

	private void IntegrateNewCards(string autoTierDir)
	{
		string newCardsPath = Path.Combine(_dataFolder, "new_cards.json");
		if (!File.Exists(newCardsPath)) return;

		try
		{
			var newCards = JsonConvert.DeserializeObject<List<NewCardEntry>>(File.ReadAllText(newCardsPath));
			if (newCards == null || newCards.Count == 0) return;

			var tierEngine = Plugin.TierEngine;
			int integrated = 0;

			// Group new cards by character (inferred from TypeName namespace)
			var byCharacter = newCards
				.Where(c => !string.IsNullOrEmpty(InferCharacter(c.TypeName)))
				.GroupBy(c => InferCharacter(c.TypeName).ToLowerInvariant());

			foreach (var group in byCharacter)
			{
				string character = group.Key;
				string autoPath = Path.Combine(autoTierDir, $"{character}.json");

				// Load existing auto-tiers if any
				List<AutoTierEntry> existingEntries = new();
				if (File.Exists(autoPath))
				{
					try
					{
						var existing = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(autoPath));
						if (existing?.cards != null)
						{
							existingEntries = JsonConvert.DeserializeObject<List<AutoTierEntry>>(
								existing.cards.ToString());
						}
					}
					catch (Exception ex) { Plugin.Log($"AutoTierGenerator: failed to read existing auto-tier file: {ex.Message}"); }
				}

				var existingIds = new HashSet<string>(
					existingEntries.Select(e => e.Id),
					StringComparer.OrdinalIgnoreCase);

				foreach (var card in group)
				{
					// Skip if already has a manual tier or auto-tier
					if (tierEngine?.GetCardTier(character, card.Id) != null) continue;
					if (existingIds.Contains(card.Id)) continue;

					// Add as default C-tier
					existingEntries.Add(new AutoTierEntry
					{
						Id = card.Id,
						BaseTier = "C",
						Score = 0.5f,
						WinRate = 0f,
						PickRate = 0f,
						SampleSize = 0,
						Signals = new Dictionary<string, float>
						{
							["win_rate"] = 0f,
							["pick_rate"] = 0f,
							["synergy"] = 0f,
							["usage"] = 0f
						}
					});
					integrated++;
				}

				if (integrated > 0)
				{
					existingEntries.Sort((a, b) => b.Score.CompareTo(a.Score));
					var output = new { character, generated = DateTime.UtcNow.ToString("o"), cards = existingEntries };
					File.WriteAllText(autoPath, JsonConvert.SerializeObject(output, Formatting.Indented));
				}
			}

			if (integrated > 0)
				Plugin.Log($"AutoTierGenerator: integrated {integrated} new cards with default C-tier.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"AutoTierGenerator: failed to integrate new cards: {ex.Message}");
		}
	}

	/// <summary>
	/// Attempts to infer the character name from the fully qualified type name.
	/// E.g. "STS2.Cards.Ironclad.Bash" → "ironclad"
	/// </summary>
	private static string InferCharacter(string typeName)
	{
		if (string.IsNullOrEmpty(typeName)) return null;

		// Known STS2 character names to look for in the namespace
		string[] knownCharacters = { "ironclad", "silent", "defect", "regent", "necrobinder" };
		string lower = typeName.ToLowerInvariant();
		foreach (var ch in knownCharacters)
		{
			if (lower.Contains(ch))
				return ch;
		}

		// Fallback: try extracting from namespace segments (e.g. "Namespace.Cards.CharName.CardName")
		var parts = typeName.Split('.');
		if (parts.Length >= 3)
			return parts[^2].ToLowerInvariant();

		return null;
	}

	/// <summary>
	/// Matches the ExtractedEntity JSON format from RuntimeCardExtractor (new_cards.json).
	/// </summary>
	private class NewCardEntry
	{
		[JsonProperty("Id")] public string Id { get; set; }
		[JsonProperty("TypeName")] public string TypeName { get; set; }
		[JsonProperty("EntityType")] public string EntityType { get; set; }
	}
}

public class AutoTierEntry
{
	[JsonProperty("id")] public string Id { get; set; }
	[JsonProperty("baseTier")] public string BaseTier { get; set; }
	[JsonProperty("score")] public float Score { get; set; }
	[JsonProperty("winRate")] public float WinRate { get; set; }
	[JsonProperty("pickRate")] public float PickRate { get; set; }
	[JsonProperty("sampleSize")] public int SampleSize { get; set; }
	[JsonProperty("signals")] public Dictionary<string, float> Signals { get; set; }
}
