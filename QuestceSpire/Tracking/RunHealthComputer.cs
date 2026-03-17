using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace QuestceSpire.Tracking;

/// <summary>
/// Computes a "run health" score (0-100) by comparing the current run's state
/// against historical winning runs at the same floor.
/// </summary>
public class RunHealthComputer
{
	private readonly RunDatabase _db;

	// Cached percentile distributions from winning runs: floor → (avgHpRatio, avgGold, avgDeckSize)
	private Dictionary<int, (float hpRatio, float gold, float deckSize)> _winningBenchmarks;

	public RunHealthComputer(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Precompute benchmarks from winning runs. Call at startup and after run ends.
	/// </summary>
	public void ComputeBenchmarks()
	{
		var connStr = _db.ConnectionString;
		if (connStr == null) return;

		_winningBenchmarks = new Dictionary<int, (float, float, float)>();

		try
		{
			using var conn = new SqliteConnection(connStr);
			conn.Open();

			// Average HP ratio, gold, and deck size per floor from winning runs
			using var cmd = conn.CreateCommand();
			cmd.CommandText = @"
				SELECT d.floor,
					AVG(CAST(d.current_hp AS REAL) / NULLIF(d.max_hp, 0)) AS avg_hp_ratio,
					AVG(d.gold) AS avg_gold,
					AVG(json_array_length(d.deck_snapshot)) AS avg_deck_size
				FROM decisions d
				JOIN runs r ON d.run_id = r.run_id
				WHERE r.outcome = 'Win'
				GROUP BY d.floor
				HAVING COUNT(*) >= 2";

			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				int floor = reader.GetInt32(0);
				float hpRatio = reader.IsDBNull(1) ? 0.7f : reader.GetFloat(1);
				float gold = reader.IsDBNull(2) ? 100f : reader.GetFloat(2);
				float deckSize = reader.IsDBNull(3) ? 20f : reader.GetFloat(3);
				_winningBenchmarks[floor] = (hpRatio, gold, deckSize);
			}

			Plugin.Log($"RunHealth: computed benchmarks for {_winningBenchmarks.Count} floors.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"RunHealth: benchmark computation failed — {ex.Message}");
		}
	}

	/// <summary>
	/// Calculate current run health score (0-100).
	/// </summary>
	/// <param name="currentHp">Current HP</param>
	/// <param name="maxHp">Maximum HP</param>
	/// <param name="gold">Current gold</param>
	/// <param name="deckSize">Current deck size</param>
	/// <param name="floor">Current floor number</param>
	/// <param name="archetypeStrength">Top archetype strength (0-1), from DeckAnalyzer</param>
	/// <param name="bossReadiness">Boss readiness score (0-100), from BossAdvisor</param>
	/// <returns>Health score 0-100</returns>
	public int CalculateHealth(int currentHp, int maxHp, int gold, int deckSize, int floor,
		float archetypeStrength = 0f, int bossReadiness = 50)
	{
		float score = 50f; // Base score

		// 1. HP comparison to winning runs (±15 points)
		float hpRatio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
		if (_winningBenchmarks != null && _winningBenchmarks.TryGetValue(floor, out var bench))
		{
			float hpDelta = hpRatio - bench.hpRatio;
			score += hpDelta * 30f; // ±15 max

			// 2. Gold comparison (±10 points)
			float goldDelta = gold - bench.gold;
			score += Math.Max(-10f, Math.Min(10f, goldDelta / 30f));

			// 3. Deck size — penalize extreme deviation from benchmark
			float sizeDelta = Math.Abs(deckSize - bench.deckSize);
			score -= Math.Min(10f, sizeDelta * 0.8f);
		}
		else
		{
			// No benchmark data — use absolute thresholds
			if (hpRatio < 0.3f) score -= 15f;
			else if (hpRatio > 0.7f) score += 10f;

			if (gold < 50) score -= 5f;
			else if (gold > 200) score += 5f;
		}

		// 4. Archetype strength (0-15 points)
		score += archetypeStrength * 15f;

		// 5. Boss readiness (0-10 points, scaled from 0-100)
		score += (bossReadiness - 50f) / 5f;

		return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
	}
}
