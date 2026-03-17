using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Computes co-pick synergy statistics: which card pairs appear together in winning runs.
/// Produces card_pair_stats for SynergyScorer to use as a co-pick bonus.
/// </summary>
public class CoPickSynergyComputer
{
	private readonly RunDatabase _db;
	private const int MinSamples = 5;

	public CoPickSynergyComputer(RunDatabase db)
	{
		_db = db;
	}

	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null)
		{
			Plugin.Log("CoPickSynergy: database not initialized — skipping.");
			return;
		}

		Plugin.Log("CoPickSynergy: computing card pair statistics...");

		// Key: (cardA, cardB, character) → (wins, total)
		var pairs = new Dictionary<(string, string, string), (int wins, int total)>();

		using var conn = new SqliteConnection(connStr);
		conn.Open();

		// Get final deck snapshots from runs with known outcomes
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			SELECT d.deck_snapshot, r.character, r.outcome
			FROM decisions d
			JOIN runs r ON d.run_id = r.run_id
			WHERE r.outcome IS NOT NULL
			AND d.id = (SELECT MAX(d2.id) FROM decisions d2 WHERE d2.run_id = d.run_id)";

		using var reader = cmd.ExecuteReader();
		int runCount = 0;

		while (reader.Read())
		{
			string deckJson = reader.GetString(0);
			string character = reader.GetString(1);
			string outcome = reader.GetString(2);
			bool isWin = outcome == "Win";

			List<string> deck;
			try { deck = JsonConvert.DeserializeObject<List<string>>(deckJson); }
			catch { continue; }
			if (deck == null || deck.Count < 2) continue;

			// Deduplicate deck (same card can appear multiple times)
			var unique = new HashSet<string>(deck);
			var sorted = unique.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

			// Generate all pairs
			for (int i = 0; i < sorted.Count; i++)
			{
				for (int j = i + 1; j < sorted.Count; j++)
				{
					var key = (sorted[i], sorted[j], character);
					var prev = pairs.TryGetValue(key, out var v) ? v : (0, 0);
					pairs[key] = (prev.wins + (isWin ? 1 : 0), prev.total + 1);
				}
			}
			runCount++;
		}

		// Filter to minimum sample size and convert to stats
		var stats = new List<CardPairStat>();
		foreach (var kvp in pairs)
		{
			if (kvp.Value.total < MinSamples) continue;
			stats.Add(new CardPairStat
			{
				CardA = kvp.Key.Item1,
				CardB = kvp.Key.Item2,
				Character = kvp.Key.Item3,
				CoOccurrence = kvp.Value.total,
				WinRate = (float)kvp.Value.wins / kvp.Value.total,
				SampleSize = kvp.Value.total
			});
		}

		_db.SaveCardPairStats(stats);
		Plugin.Log($"CoPickSynergy: computed {stats.Count} card pairs from {runCount} runs.");
	}

	/// <summary>
	/// Get co-pick bonus for a candidate card given current deck.
	/// Returns bonus score (0 to 0.4) based on how well this card pairs with existing deck cards.
	/// </summary>
	public float GetCoPickBonus(string cardId, List<string> deckCards, string character)
	{
		if (deckCards == null || deckCards.Count == 0) return 0f;

		var pairStats = _db.GetCardPairStats(character, cardId);
		if (pairStats.Count == 0) return 0f;

		// Find the best matching pair already in the deck
		float bestBonus = 0f;
		foreach (var pair in pairStats)
		{
			string partner = pair.CardA == cardId ? pair.CardB : pair.CardA;
			if (!deckCards.Contains(partner)) continue;

			// Bonus based on how much the win rate exceeds baseline (50%)
			float delta = pair.WinRate - 0.5f;
			if (delta > 0f)
			{
				float bonus = Math.Min(0.4f, delta * 2f);
				bestBonus = Math.Max(bestBonus, bonus);
			}
		}

		return bestBonus;
	}
}
