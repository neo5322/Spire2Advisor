using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Analyzes relic-card co-occurrence win rates from past runs.
/// Identifies which cards perform best when a specific relic is present.
/// </summary>
public class RelicCardCrossRef
{
	private readonly RunDatabase _db;

	public RelicCardCrossRef(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Compute relic-card co-occurrence stats from historical runs.
	/// </summary>
	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null) return;

		try
		{
			var stats = new Dictionary<(string relic, string card, string character), (int wins, int total)>();

			using var conn = new SqliteConnection(connStr);
			conn.Open();

			// Get all completed runs with their relics and final deck
			using var cmd = conn.CreateCommand();
			cmd.CommandText = @"
				SELECT r.character, r.outcome,
				       d.deck_snapshot, d.offered_ids
				FROM runs r
				INNER JOIN decisions d ON d.run_id = r.run_id
				WHERE r.outcome IS NOT NULL
				  AND d.deck_snapshot IS NOT NULL
				ORDER BY r.run_id, d.floor DESC";

			// We need relics from game state — they're tracked in decisions or run data
			// Alternative: query final deck snapshots and correlate with relics
			// For now, use a simpler approach: query runs with their final state
			cmd.CommandText = @"
				SELECT r.run_id, r.character, r.outcome
				FROM runs r
				WHERE r.outcome IS NOT NULL AND r.character IS NOT NULL";

			var runs = new List<(string runId, string character, bool won)>();
			using (var reader = cmd.ExecuteReader())
			{
				while (reader.Read())
				{
					string runId = reader.GetString(0);
					string character = reader.GetString(1);
					string outcome = reader.GetString(2);
					bool won = outcome.Equals("Win", StringComparison.OrdinalIgnoreCase);
					runs.Add((runId, character, won));
				}
			}

			foreach (var (runId, character, won) in runs)
			{
				// Get the last deck snapshot for this run
				string deckJson = null;
				using (var dCmd = conn.CreateCommand())
				{
					dCmd.CommandText = @"SELECT deck_snapshot FROM decisions
						WHERE run_id = @rid AND deck_snapshot IS NOT NULL
						ORDER BY floor DESC LIMIT 1";
					dCmd.Parameters.AddWithValue("@rid", runId);
					deckJson = dCmd.ExecuteScalar() as string;
				}
				if (string.IsNullOrEmpty(deckJson)) continue;

				List<string> deckCards;
				try { deckCards = JsonConvert.DeserializeObject<List<string>>(deckJson); }
				catch { continue; }
				if (deckCards == null || deckCards.Count == 0) continue;

				// Get relics for this run from relic decisions
				var relics = new HashSet<string>();
				using (var rCmd = conn.CreateCommand())
				{
					rCmd.CommandText = @"SELECT chosen_id FROM decisions
						WHERE run_id = @rid AND event_type IN ('RelicReward', 'BossRelic', 'Shop')
						AND chosen_id IS NOT NULL";
					rCmd.Parameters.AddWithValue("@rid", runId);
					using var rReader = rCmd.ExecuteReader();
					while (rReader.Read())
					{
						string relicId = rReader.GetString(0);
						// Heuristic: if it was offered as a relic reward, it's a relic
						relics.Add(relicId);
					}
				}

				// Cross-reference each relic with each card in the final deck
				foreach (string relicId in relics)
				{
					foreach (string cardId in deckCards.Distinct())
					{
						var key = (relicId, cardId, character);
						if (!stats.TryGetValue(key, out var cur))
							cur = (0, 0);
						stats[key] = (cur.wins + (won ? 1 : 0), cur.total + 1);
					}
				}
			}

			// Convert to list, filter by minimum sample size
			var result = stats
				.Where(kv => kv.Value.total >= 3)
				.Select(kv => new RelicCardCrossStat
				{
					RelicId = kv.Key.relic,
					CardId = kv.Key.card,
					Character = kv.Key.character,
					CoOccurrence = kv.Value.total,
					WinRate = (float)kv.Value.wins / kv.Value.total,
					SampleSize = kv.Value.total
				})
				.ToList();

			_db.SaveRelicCardCrossStats(result);
			Plugin.Log($"RelicCardCrossRef: computed {result.Count} relic-card pairs from {runs.Count} runs.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"RelicCardCrossRef: compute error — {ex.Message}");
		}
	}

	/// <summary>
	/// Get cards that synergize well with a given relic based on historical data.
	/// </summary>
	public List<RelicCardCrossStat> GetTopCards(string character, string relicId)
	{
		return _db.GetRelicCardCrossStats(character, relicId);
	}
}
