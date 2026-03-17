using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace QuestceSpire.Tracking;

/// <summary>
/// Computes per-act card tier adjustments from decision history.
/// Cards that perform differently in Act 1 vs Act 3 get act-specific scores.
/// </summary>
public class FloorTierComputer
{
	private readonly RunDatabase _db;
	private const int MinSamples = 3;

	public FloorTierComputer(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Recompute floor_card_stats from decisions + runs tables.
	/// Groups by act (1, 2, 3) and calculates per-card pick rate and win rate per act.
	/// </summary>
	public void Compute()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null)
		{
			Plugin.Log("FloorTier: database not initialized — skipping.");
			return;
		}

		Plugin.Log("FloorTier: computing act-based card statistics...");

		using var conn = new SqliteConnection(connStr);
		conn.Open();

		var stats = new List<FloorCardStat>();

		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			SELECT
				j.value AS card_id,
				r.character,
				d.act,
				CAST(SUM(CASE WHEN d.chosen_id IS j.value THEN 1 ELSE 0 END) AS REAL)
					/ COUNT(DISTINCT d.id) AS pick_rate,
				CASE WHEN SUM(CASE WHEN d.chosen_id IS j.value THEN 1 ELSE 0 END) > 0
					THEN CAST(SUM(CASE WHEN d.chosen_id IS j.value AND r.outcome = 'Win' THEN 1 ELSE 0 END) AS REAL)
						 / SUM(CASE WHEN d.chosen_id IS j.value THEN 1 ELSE 0 END)
					ELSE 0.0 END AS win_rate,
				COUNT(DISTINCT d.id) AS sample_size
			FROM decisions d
			JOIN runs r ON d.run_id = r.run_id
			JOIN json_each(d.offered_ids) j
			WHERE d.event_type IN ('CardReward', 'CardTransform', 'ShopCard')
			AND r.outcome IS NOT NULL
			GROUP BY j.value, r.character, d.act
			HAVING COUNT(DISTINCT d.id) >= @minSamples";
		cmd.Parameters.AddWithValue("@minSamples", MinSamples);

		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			stats.Add(new FloorCardStat
			{
				CardId = reader.GetString(0),
				Character = reader.GetString(1),
				Act = reader.GetInt32(2),
				PickRate = reader.GetFloat(3),
				WinRate = reader.GetFloat(4),
				SampleSize = reader.GetInt32(5)
			});
		}

		_db.SaveFloorCardStats(stats);
		Plugin.Log($"FloorTier: computed {stats.Count} act-specific card stats.");
	}

	/// <summary>
	/// Get floor adjustment for a card. Compares the card's act-specific win rate
	/// against its overall win rate. Returns a bonus/penalty (-0.5 to +0.5).
	/// </summary>
	public float GetFloorAdjustment(string cardId, string character, int act)
	{
		var actStat = _db.GetFloorCardStat(cardId, character, act);
		if (actStat == null || actStat.SampleSize < MinSamples) return 0f;

		// Get overall community stat for comparison
		var overall = _db.GetCommunityCardStats(character, cardId);
		if (overall == null || overall.SampleSize < MinSamples) return 0f;

		float delta = actStat.WinRate - overall.WinRateWhenPicked;
		// Clamp to ±0.5 and scale
		return Math.Max(-0.5f, Math.Min(0.5f, delta * 3f));
	}
}
