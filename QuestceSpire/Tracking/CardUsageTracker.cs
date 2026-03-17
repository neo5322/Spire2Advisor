using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Computes card usage statistics from combat_turns data.
/// Tracks how often each card is actually played vs. just sitting in the deck.
/// Used for card removal recommendations.
/// </summary>
public class CardUsageTracker
{
	private readonly RunDatabase _db;
	private const int MinCombats = 3;

	public CardUsageTracker(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Recompute card usage stats from combat_turns table.
	/// </summary>
	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null)
		{
			Plugin.Log("CardUsageTracker: database not initialized — skipping.");
			return;
		}

		Plugin.Log("CardUsageTracker: computing card usage statistics...");

		// card_id → character → {plays, combats}
		var usage = new Dictionary<(string cardId, string character), (int plays, int combats)>();

		using var conn = new SqliteConnection(connStr);
		conn.Open();

		// Get cards played per combat (one row per floor/run)
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			SELECT ct.run_id, ct.floor, ct.cards_played, r.character
			FROM combat_turns ct
			JOIN runs r ON ct.run_id = r.run_id
			WHERE ct.cards_played IS NOT NULL
			GROUP BY ct.run_id, ct.floor, ct.turn_number";

		using var reader = cmd.ExecuteReader();

		// Track unique combats per run/floor
		var combatsSeen = new HashSet<string>();

		while (reader.Read())
		{
			string runId = reader.GetString(0);
			int floor = reader.GetInt32(1);
			string cardsJson = reader.GetString(2);
			string character = reader.GetString(3);

			List<string> cards;
			try { cards = JsonConvert.DeserializeObject<List<string>>(cardsJson); }
			catch { continue; }
			if (cards == null) continue;

			string combatKey = $"{runId}|{floor}";
			bool newCombat = combatsSeen.Add(combatKey);

			foreach (var cardId in cards)
			{
				var key = (cardId, character);
				var prev = usage.TryGetValue(key, out var v) ? v : (0, 0);
				usage[key] = (prev.plays + 1, prev.combats + (newCombat ? 1 : 0));
			}

			// Also mark all cards in the combat (even unplayed) for denominator
			// We only have played cards, so effectiveness = plays / combats_present
		}

		// Convert to stats
		var stats = new List<CardUsageStat>();
		foreach (var kvp in usage)
		{
			if (kvp.Value.combats < MinCombats) continue;

			float avgPlays = (float)kvp.Value.plays / kvp.Value.combats;
			// Effectiveness: higher is better. Cards played more than once per combat are very effective.
			float effectiveness = Math.Min(1f, avgPlays / 2f);

			stats.Add(new CardUsageStat
			{
				CardId = kvp.Key.cardId,
				Character = kvp.Key.character,
				AvgPlaysPerCombat = avgPlays,
				TotalPlays = kvp.Value.plays,
				TotalCombats = kvp.Value.combats,
				Effectiveness = effectiveness,
				SampleSize = kvp.Value.combats
			});
		}

		_db.SaveCardUsageStats(stats);
		Plugin.Log($"CardUsageTracker: computed usage stats for {stats.Count} cards.");
	}
}
