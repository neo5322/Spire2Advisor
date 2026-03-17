using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using QuestceSpire.Core;

namespace QuestceSpire.Tracking;

/// <summary>
/// Computes meta archetype rankings: which archetypes are winning the most in the current meta.
/// Identifies core cards for each winning archetype.
/// </summary>
public class MetaArchetypeComputer
{
	private readonly RunDatabase _db;
	private readonly string _dataFolder;

	public MetaArchetypeComputer(RunDatabase db, string dataFolder)
	{
		_db = db;
		_dataFolder = dataFolder;
	}

	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null)
		{
			Plugin.Log("MetaArchetype: database not initialized — skipping.");
			return;
		}

		Plugin.Log("MetaArchetype: computing meta archetype rankings...");

		var tierEngine = Plugin.TierEngine;
		if (tierEngine == null)
		{
			Plugin.Log("MetaArchetype: TierEngine not available — skipping.");
			return;
		}

		// archetype → character → {wins, total, card_counts}
		var archStats = new Dictionary<(string archId, string character), ArchetypeRunData>();

		using var conn = new SqliteConnection(connStr);
		conn.Open();

		// Get final deck snapshot per run
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			SELECT d.deck_snapshot, r.character, r.outcome
			FROM decisions d
			JOIN runs r ON d.run_id = r.run_id
			WHERE r.outcome IS NOT NULL
			AND d.id = (SELECT MAX(d2.id) FROM decisions d2 WHERE d2.run_id = d.run_id)";

		using var reader = cmd.ExecuteReader();
		int processedRuns = 0;

		while (reader.Read())
		{
			string deckJson = reader.GetString(0);
			string character = reader.GetString(1);
			string outcome = reader.GetString(2);
			bool isWin = outcome == "Win";

			List<string> deck;
			try { deck = JsonConvert.DeserializeObject<List<string>>(deckJson); }
			catch { continue; }
			if (deck == null || deck.Count == 0) continue;

			// Count synergy tags in deck
			var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (string cardId in deck)
			{
				var tier = tierEngine.GetCardTier(character, cardId);
				if (tier?.Synergies == null) continue;
				foreach (string syn in tier.Synergies)
				{
					string tag = syn.ToLowerInvariant();
					tagCounts[tag] = tagCounts.TryGetValue(tag, out int c) ? c + 1 : 1;
				}
			}

			// Match against archetype definitions
			string charKey = character?.ToLowerInvariant() ?? "";
			if (!ArchetypeDefinitions.ByCharacter.TryGetValue(charKey, out var archetypes))
				continue;

			foreach (var arch in archetypes)
			{
				if (arch.CoreTags == null || arch.CoreTags.Count == 0) continue;
				int coreCount = 0;
				foreach (string coreTag in arch.CoreTags)
				{
					if (tagCounts.TryGetValue(coreTag, out int v))
						coreCount += v;
				}
				if (coreCount < arch.CoreThreshold) continue;

				var key = (arch.Id, character);
				if (!archStats.TryGetValue(key, out var data))
				{
					data = new ArchetypeRunData();
					archStats[key] = data;
				}

				data.Total++;
				if (isWin) data.Wins++;

				// Track card frequencies in this archetype
				foreach (string cardId in deck)
				{
					data.CardCounts[cardId] = data.CardCounts.TryGetValue(cardId, out int cc) ? cc + 1 : 1;
				}
			}

			processedRuns++;
		}

		// Build output per character
		var result = new Dictionary<string, List<MetaArchetypeEntry>>();

		foreach (var kvp in archStats)
		{
			if (kvp.Value.Total < 3) continue;

			string character = kvp.Key.character;
			if (!result.ContainsKey(character))
				result[character] = new List<MetaArchetypeEntry>();

			// Find core cards: top 5 cards by frequency in this archetype
			var coreCards = kvp.Value.CardCounts
				.OrderByDescending(c => c.Value)
				.Take(5)
				.Select(c => c.Key)
				.ToList();

			result[character].Add(new MetaArchetypeEntry
			{
				Archetype = kvp.Key.archId,
				WinRate = (float)kvp.Value.Wins / kvp.Value.Total,
				PickRate = kvp.Value.Total, // absolute count
				CoreCards = coreCards,
				SampleSize = kvp.Value.Total
			});
		}

		// Sort each character's archetypes by win rate
		foreach (var list in result.Values)
			list.Sort((a, b) => b.WinRate.CompareTo(a.WinRate));

		// Save
		string path = Path.Combine(_dataFolder, "meta_archetypes.json");
		File.WriteAllText(path, JsonConvert.SerializeObject(result, Formatting.Indented));

		int totalArchs = result.Values.Sum(l => l.Count);
		Plugin.Log($"MetaArchetype: computed {totalArchs} archetypes across {result.Count} characters from {processedRuns} runs.");
	}

	private class ArchetypeRunData
	{
		public int Wins;
		public int Total;
		public Dictionary<string, int> CardCounts = new();
	}
}

public class MetaArchetypeEntry
{
	[JsonProperty("archetype")] public string Archetype { get; set; }
	[JsonProperty("winRate")] public float WinRate { get; set; }
	[JsonProperty("pickRate")] public int PickRate { get; set; }
	[JsonProperty("coreCards")] public List<string> CoreCards { get; set; }
	[JsonProperty("sampleSize")] public int SampleSize { get; set; }
}
