using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using QuestceSpire.Tracking;

namespace QuestceSpire.Core;

/// <summary>
/// Generates a comprehensive post-run analysis: decision review, combat efficiency,
/// card usage, and win-rate simulation against community data.
/// </summary>
public class RunSummary
{
	private readonly RunDatabase _db;

	public RunSummary(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Generate a full run summary for the completed run.
	/// </summary>
	public RunSummaryData Generate(string runId, string character, string outcome)
	{
		var summary = new RunSummaryData
		{
			RunId = runId,
			Character = character,
			Outcome = outcome,
			GeneratedAt = DateTime.UtcNow
		};

		var connStr = _db.ConnectionString;
		if (connStr == null) return summary;

		try
		{
			using var conn = new SqliteConnection(connStr);
			conn.Open();

			// ─── Decision Analysis ───
			var decisions = new List<DecisionReview>();
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = @"SELECT d.floor, d.event_type, d.offered_ids, d.chosen_id, d.deck_snapshot
					FROM decisions d WHERE d.run_id = @rid ORDER BY d.floor";
				cmd.Parameters.AddWithValue("@rid", runId);

				using var reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					int floor = reader.GetInt32(0);
					string eventType = reader.GetString(1);
					string offeredJson = reader.IsDBNull(2) ? "[]" : reader.GetString(2);
					string chosenId = reader.IsDBNull(3) ? null : reader.GetString(3);

					var offered = new List<string>();
					try { offered = JsonConvert.DeserializeObject<List<string>>(offeredJson) ?? new(); } catch (Exception ex) { Plugin.Log($"RunSummary: failed to deserialize offered cards JSON: {ex.Message}"); }

					decisions.Add(new DecisionReview
					{
						Floor = floor,
						EventType = eventType,
						OfferedIds = offered,
						ChosenId = chosenId
					});
				}
			}

			// ─── Evaluate each decision against community data ───
			foreach (var decision in decisions)
			{
				if (decision.ChosenId == null || decision.OfferedIds.Count <= 1) continue;

				var stats = new Dictionary<string, CommunityCardStats>();
				foreach (string cardId in decision.OfferedIds)
				{
					var s = _db.GetCommunityCardStats(character, cardId);
					if (s != null) stats[cardId] = s;
				}

				if (stats.Count > 1 && stats.ContainsKey(decision.ChosenId))
				{
					var chosen = stats[decision.ChosenId];
					var bestAlt = stats.Where(s => s.Key != decision.ChosenId)
						.OrderByDescending(s => s.Value.WinRateWhenPicked)
						.FirstOrDefault();

					if (bestAlt.Value != null)
					{
						float delta = bestAlt.Value.WinRateWhenPicked - chosen.WinRateWhenPicked;
						if (delta > 0.03f)
						{
							decision.Feedback = $"커뮤니티 데이터: {bestAlt.Key} 승률 {bestAlt.Value.WinRateWhenPicked:P0} vs 선택한 {decision.ChosenId} {chosen.WinRateWhenPicked:P0}";
							decision.WinRateDelta = -delta;
							decision.WasBetterChoice = false;
						}
						else
						{
							decision.WasBetterChoice = true;
							decision.Feedback = "좋은 선택이었습니다";
						}
					}
				}
			}
			summary.Decisions = decisions;

			// ─── Combat Efficiency ───
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = @"SELECT SUM(damage_dealt), SUM(damage_taken), SUM(block_generated), COUNT(*)
					FROM combat_turns WHERE run_id = @rid";
				cmd.Parameters.AddWithValue("@rid", runId);
				using var reader = cmd.ExecuteReader();
				if (reader.Read() && !reader.IsDBNull(0))
				{
					summary.TotalDamageDealt = reader.GetInt32(0);
					summary.TotalDamageTaken = reader.GetInt32(1);
					summary.TotalBlockGenerated = reader.GetInt32(2);
					summary.TotalTurns = reader.GetInt32(3);
				}
			}

			// ─── Card Play Frequency ───
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = @"SELECT cards_played FROM combat_turns WHERE run_id = @rid AND cards_played IS NOT NULL";
				cmd.Parameters.AddWithValue("@rid", runId);

				var cardPlays = new Dictionary<string, int>();
				int combatCount = 0;
				using var reader = cmd.ExecuteReader();
				while (reader.Read())
				{
					combatCount++;
					try
					{
						var cards = JsonConvert.DeserializeObject<List<string>>(reader.GetString(0));
						if (cards == null) continue;
						foreach (string c in cards)
							cardPlays[c] = cardPlays.TryGetValue(c, out int v) ? v + 1 : 1;
					}
					catch (Exception ex) { Plugin.Log($"RunSummary: failed to deserialize combat card plays: {ex.Message}"); }
				}

				summary.MostPlayedCards = cardPlays.OrderByDescending(kv => kv.Value)
					.Take(5)
					.Select(kv => new CardPlayStat { CardId = kv.Key, PlayCount = kv.Value })
					.ToList();

				summary.LeastPlayedCards = cardPlays
					.Where(kv => kv.Value <= 2 && combatCount > 3)
					.OrderBy(kv => kv.Value)
					.Take(5)
					.Select(kv => new CardPlayStat { CardId = kv.Key, PlayCount = kv.Value })
					.ToList();
			}

			// ─── Gold Usage ───
			using (var cmd = conn.CreateCommand())
			{
				cmd.CommandText = @"SELECT gold FROM decisions WHERE run_id = @rid ORDER BY floor";
				cmd.Parameters.AddWithValue("@rid", runId);
				using var reader = cmd.ExecuteReader();
				int maxGold = 0, minGold = int.MaxValue;
				while (reader.Read())
				{
					if (reader.IsDBNull(0)) continue;
					int g = reader.GetInt32(0);
					maxGold = Math.Max(maxGold, g);
					minGold = Math.Min(minGold, g);
				}
				summary.PeakGold = maxGold;
			}

			// Good/bad decision counts
			summary.GoodDecisions = decisions.Count(d => d.WasBetterChoice == true);
			summary.BadDecisions = decisions.Count(d => d.WasBetterChoice == false);
		}
		catch (Exception ex)
		{
			Plugin.Log($"RunSummary: generation error — {ex.Message}");
		}

		return summary;
	}
}

public class RunSummaryData
{
	public string RunId { get; set; }
	public string Character { get; set; }
	public string Outcome { get; set; }
	public DateTime GeneratedAt { get; set; }

	// Decision analysis
	public List<DecisionReview> Decisions { get; set; } = new();
	public int GoodDecisions { get; set; }
	public int BadDecisions { get; set; }

	// Combat stats
	public int TotalDamageDealt { get; set; }
	public int TotalDamageTaken { get; set; }
	public int TotalBlockGenerated { get; set; }
	public int TotalTurns { get; set; }

	// Card analysis
	public List<CardPlayStat> MostPlayedCards { get; set; } = new();
	public List<CardPlayStat> LeastPlayedCards { get; set; } = new();

	// Gold
	public int PeakGold { get; set; }
}

public class DecisionReview
{
	public int Floor { get; set; }
	public string EventType { get; set; }
	public List<string> OfferedIds { get; set; } = new();
	public string ChosenId { get; set; }
	public string Feedback { get; set; }
	public float WinRateDelta { get; set; }
	public bool? WasBetterChoice { get; set; }
}

public class CardPlayStat
{
	public string CardId { get; set; }
	public int PlayCount { get; set; }
}
