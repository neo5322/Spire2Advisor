using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace QuestceSpire.Tracking;

/// <summary>
/// Tracks potion acquisition, usage, and discard events.
/// Provides potion usage advice based on historical patterns.
/// </summary>
public class PotionTracker
{
	private readonly RunDatabase _db;

	public PotionTracker(RunDatabase db)
	{
		_db = db;
	}

	/// <summary>
	/// Record that a potion was obtained.
	/// </summary>
	public void RecordObtained(string runId, string potionId, int floor)
	{
		_db.SavePotionEvent(new PotionEvent
		{
			RunId = runId,
			PotionId = potionId,
			EventType = "obtained",
			Floor = floor
		});
	}

	/// <summary>
	/// Record that a potion was used in combat.
	/// </summary>
	public void RecordUsed(string runId, string potionId, int floor, string enemyId = null)
	{
		_db.SavePotionEvent(new PotionEvent
		{
			RunId = runId,
			PotionId = potionId,
			EventType = "used",
			Floor = floor,
			EnemyId = enemyId
		});
	}

	/// <summary>
	/// Record that a potion was discarded.
	/// </summary>
	public void RecordDiscarded(string runId, string potionId, int floor)
	{
		_db.SavePotionEvent(new PotionEvent
		{
			RunId = runId,
			PotionId = potionId,
			EventType = "discarded",
			Floor = floor
		});
	}

	/// <summary>
	/// Get usage summary for a specific potion: average floor used, most common enemy, use rate.
	/// </summary>
	public PotionUsageSummary GetUsageSummary(string potionId)
	{
		var connStr = _db.ConnectionString;
		if (connStr == null) return null;

		try
		{
			using var conn = new SqliteConnection(connStr);
			conn.Open();

			// Total obtained
			using var cmdObtained = conn.CreateCommand();
			cmdObtained.CommandText = "SELECT COUNT(*) FROM potion_events WHERE potion_id=@pid AND event_type='obtained'";
			cmdObtained.Parameters.AddWithValue("@pid", potionId);
			int obtained = Convert.ToInt32(cmdObtained.ExecuteScalar());

			// Total used
			using var cmdUsed = conn.CreateCommand();
			cmdUsed.CommandText = "SELECT COUNT(*), AVG(floor) FROM potion_events WHERE potion_id=@pid AND event_type='used'";
			cmdUsed.Parameters.AddWithValue("@pid", potionId);
			using var reader = cmdUsed.ExecuteReader();
			int used = 0;
			float avgFloor = 0f;
			if (reader.Read())
			{
				used = reader.GetInt32(0);
				avgFloor = reader.IsDBNull(1) ? 0f : reader.GetFloat(1);
			}

			// Most common enemy
			using var cmdEnemy = conn.CreateCommand();
			cmdEnemy.CommandText = "SELECT enemy_id, COUNT(*) as cnt FROM potion_events WHERE potion_id=@pid AND event_type='used' AND enemy_id IS NOT NULL GROUP BY enemy_id ORDER BY cnt DESC LIMIT 1";
			cmdEnemy.Parameters.AddWithValue("@pid", potionId);
			string topEnemy = cmdEnemy.ExecuteScalar()?.ToString();

			return new PotionUsageSummary
			{
				PotionId = potionId,
				TimesObtained = obtained,
				TimesUsed = used,
				UseRate = obtained > 0 ? (float)used / obtained : 0f,
				AvgFloorUsed = avgFloor,
				MostCommonEnemy = topEnemy
			};
		}
		catch (Exception ex)
		{
			Plugin.Log($"PotionTracker: GetUsageSummary error — {ex.Message}");
			return null;
		}
	}
}

public class PotionUsageSummary
{
	public string PotionId { get; set; }
	public int TimesObtained { get; set; }
	public int TimesUsed { get; set; }
	public float UseRate { get; set; }
	public float AvgFloorUsed { get; set; }
	public string MostCommonEnemy { get; set; }
}
