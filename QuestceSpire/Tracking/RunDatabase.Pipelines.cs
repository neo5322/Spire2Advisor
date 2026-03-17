using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// RunDatabase partial — schema and queries for data pipeline tables.
/// </summary>
public partial class RunDatabase
{
	/// <summary>
	/// Create all pipeline-related tables. Called from InitializeDatabase().
	/// </summary>
	internal void CreatePipelineTables()
	{
		if (!EnsureInitialized()) return;

		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"
			CREATE TABLE IF NOT EXISTS patch_changes (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				entity_type TEXT NOT NULL,
				entity_id TEXT NOT NULL,
				property TEXT NOT NULL,
				old_value TEXT,
				new_value TEXT,
				patch_version TEXT NOT NULL,
				patch_date TEXT NOT NULL,
				raw_text TEXT
			);

			CREATE TABLE IF NOT EXISTS card_pair_stats (
				card_a TEXT NOT NULL,
				card_b TEXT NOT NULL,
				character TEXT NOT NULL,
				co_occurrence INTEGER NOT NULL,
				win_rate REAL NOT NULL,
				sample_size INTEGER NOT NULL,
				PRIMARY KEY (card_a, card_b, character)
			);

			CREATE TABLE IF NOT EXISTS floor_card_stats (
				card_id TEXT NOT NULL,
				character TEXT NOT NULL,
				act INTEGER NOT NULL,
				pick_rate REAL NOT NULL,
				win_rate REAL NOT NULL,
				sample_size INTEGER NOT NULL,
				PRIMARY KEY (card_id, character, act)
			);

			CREATE TABLE IF NOT EXISTS upgrade_values (
				card_id TEXT NOT NULL,
				character TEXT NOT NULL,
				upgrade_win_delta REAL NOT NULL,
				upgrade_frequency REAL NOT NULL,
				sample_size INTEGER NOT NULL,
				PRIMARY KEY (card_id, character)
			);

			CREATE TABLE IF NOT EXISTS combat_turns (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				run_id TEXT NOT NULL,
				floor INTEGER NOT NULL,
				enemy_id TEXT,
				turn_number INTEGER NOT NULL,
				cards_played TEXT,
				damage_dealt INTEGER NOT NULL DEFAULT 0,
				damage_taken INTEGER NOT NULL DEFAULT 0,
				block_generated INTEGER NOT NULL DEFAULT 0,
				player_hp INTEGER NOT NULL DEFAULT 0,
				enemy_hp TEXT,
				timestamp TEXT NOT NULL
			);

			CREATE TABLE IF NOT EXISTS card_usage_stats (
				card_id TEXT NOT NULL,
				character TEXT NOT NULL,
				avg_plays_per_combat REAL NOT NULL,
				total_plays INTEGER NOT NULL,
				total_combats INTEGER NOT NULL,
				effectiveness REAL NOT NULL DEFAULT 0.0,
				sample_size INTEGER NOT NULL,
				PRIMARY KEY (card_id, character)
			);

			CREATE TABLE IF NOT EXISTS potion_events (
				id INTEGER PRIMARY KEY AUTOINCREMENT,
				run_id TEXT NOT NULL,
				potion_id TEXT NOT NULL,
				event_type TEXT NOT NULL,
				floor INTEGER NOT NULL DEFAULT 0,
				enemy_id TEXT,
				timestamp TEXT NOT NULL
			);

			CREATE INDEX IF NOT EXISTS idx_combat_turns_run ON combat_turns(run_id, floor);
			CREATE INDEX IF NOT EXISTS idx_potion_events_run ON potion_events(run_id);
			CREATE INDEX IF NOT EXISTS idx_patch_changes_entity ON patch_changes(entity_type, entity_id);
		";
		cmd.ExecuteNonQuery();
		Plugin.Log("Pipeline tables created/verified.");
	}

	// --- Card Pair Stats ---

	public void SaveCardPairStats(List<CardPairStat> stats)
	{
		if (!EnsureInitialized() || stats == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var tx = conn.BeginTransaction();
		using var del = conn.CreateCommand();
		del.CommandText = "DELETE FROM card_pair_stats";
		del.ExecuteNonQuery();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "INSERT INTO card_pair_stats (card_a, card_b, character, co_occurrence, win_rate, sample_size) VALUES (@a, @b, @char, @co, @wr, @ss)";
		var pA = cmd.Parameters.Add("@a", SqliteType.Text);
		var pB = cmd.Parameters.Add("@b", SqliteType.Text);
		var pChar = cmd.Parameters.Add("@char", SqliteType.Text);
		var pCo = cmd.Parameters.Add("@co", SqliteType.Integer);
		var pWr = cmd.Parameters.Add("@wr", SqliteType.Real);
		var pSs = cmd.Parameters.Add("@ss", SqliteType.Integer);
		foreach (var s in stats)
		{
			pA.Value = s.CardA;
			pB.Value = s.CardB;
			pChar.Value = s.Character;
			pCo.Value = s.CoOccurrence;
			pWr.Value = s.WinRate;
			pSs.Value = s.SampleSize;
			cmd.ExecuteNonQuery();
		}
		tx.Commit();
		Plugin.Log($"Saved {stats.Count} card pair stats.");
	}

	public List<CardPairStat> GetCardPairStats(string character, string cardId)
	{
		var list = new List<CardPairStat>();
		if (!EnsureInitialized()) return list;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM card_pair_stats WHERE character=@char AND (card_a=@id OR card_b=@id) ORDER BY win_rate DESC LIMIT 10";
		cmd.Parameters.AddWithValue("@char", character);
		cmd.Parameters.AddWithValue("@id", cardId);
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new CardPairStat
			{
				CardA = reader.GetString(0),
				CardB = reader.GetString(1),
				Character = reader.GetString(2),
				CoOccurrence = reader.GetInt32(3),
				WinRate = reader.GetFloat(4),
				SampleSize = reader.GetInt32(5)
			});
		}
		return list;
	}

	// --- Floor Card Stats ---

	public void SaveFloorCardStats(List<FloorCardStat> stats)
	{
		if (!EnsureInitialized() || stats == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var tx = conn.BeginTransaction();
		using var del = conn.CreateCommand();
		del.CommandText = "DELETE FROM floor_card_stats";
		del.ExecuteNonQuery();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "INSERT INTO floor_card_stats (card_id, character, act, pick_rate, win_rate, sample_size) VALUES (@id, @char, @act, @pr, @wr, @ss)";
		var pId = cmd.Parameters.Add("@id", SqliteType.Text);
		var pChar = cmd.Parameters.Add("@char", SqliteType.Text);
		var pAct = cmd.Parameters.Add("@act", SqliteType.Integer);
		var pPr = cmd.Parameters.Add("@pr", SqliteType.Real);
		var pWr = cmd.Parameters.Add("@wr", SqliteType.Real);
		var pSs = cmd.Parameters.Add("@ss", SqliteType.Integer);
		foreach (var s in stats)
		{
			pId.Value = s.CardId;
			pChar.Value = s.Character;
			pAct.Value = s.Act;
			pPr.Value = s.PickRate;
			pWr.Value = s.WinRate;
			pSs.Value = s.SampleSize;
			cmd.ExecuteNonQuery();
		}
		tx.Commit();
		Plugin.Log($"Saved {stats.Count} floor card stats.");
	}

	public FloorCardStat GetFloorCardStat(string cardId, string character, int act)
	{
		if (!EnsureInitialized()) return null;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM floor_card_stats WHERE card_id=@id AND character=@char AND act=@act";
		cmd.Parameters.AddWithValue("@id", cardId);
		cmd.Parameters.AddWithValue("@char", character);
		cmd.Parameters.AddWithValue("@act", act);
		using var reader = cmd.ExecuteReader();
		if (reader.Read())
		{
			return new FloorCardStat
			{
				CardId = reader.GetString(0),
				Character = reader.GetString(1),
				Act = reader.GetInt32(2),
				PickRate = reader.GetFloat(3),
				WinRate = reader.GetFloat(4),
				SampleSize = reader.GetInt32(5)
			};
		}
		return null;
	}

	// --- Upgrade Values ---

	public void SaveUpgradeValues(List<UpgradeValue> values)
	{
		if (!EnsureInitialized() || values == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var tx = conn.BeginTransaction();
		using var del = conn.CreateCommand();
		del.CommandText = "DELETE FROM upgrade_values";
		del.ExecuteNonQuery();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "INSERT INTO upgrade_values (card_id, character, upgrade_win_delta, upgrade_frequency, sample_size) VALUES (@id, @char, @delta, @freq, @ss)";
		var pId = cmd.Parameters.Add("@id", SqliteType.Text);
		var pChar = cmd.Parameters.Add("@char", SqliteType.Text);
		var pDelta = cmd.Parameters.Add("@delta", SqliteType.Real);
		var pFreq = cmd.Parameters.Add("@freq", SqliteType.Real);
		var pSs = cmd.Parameters.Add("@ss", SqliteType.Integer);
		foreach (var v in values)
		{
			pId.Value = v.CardId;
			pChar.Value = v.Character;
			pDelta.Value = v.UpgradeWinDelta;
			pFreq.Value = v.UpgradeFrequency;
			pSs.Value = v.SampleSize;
			cmd.ExecuteNonQuery();
		}
		tx.Commit();
		Plugin.Log($"Saved {values.Count} upgrade values.");
	}

	public UpgradeValue GetUpgradeValue(string cardId, string character)
	{
		if (!EnsureInitialized()) return null;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM upgrade_values WHERE card_id=@id AND character=@char";
		cmd.Parameters.AddWithValue("@id", cardId);
		cmd.Parameters.AddWithValue("@char", character);
		using var reader = cmd.ExecuteReader();
		if (reader.Read())
		{
			return new UpgradeValue
			{
				CardId = reader.GetString(0),
				Character = reader.GetString(1),
				UpgradeWinDelta = reader.GetFloat(2),
				UpgradeFrequency = reader.GetFloat(3),
				SampleSize = reader.GetInt32(4)
			};
		}
		return null;
	}

	// --- Combat Turns ---

	public void SaveCombatTurn(CombatTurnRecord turn)
	{
		if (!EnsureInitialized() || turn == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"INSERT INTO combat_turns (run_id, floor, enemy_id, turn_number, cards_played,
			damage_dealt, damage_taken, block_generated, player_hp, enemy_hp, timestamp)
			VALUES (@rid, @floor, @eid, @turn, @cards, @dd, @dt, @bg, @php, @ehp, @ts)";
		cmd.Parameters.AddWithValue("@rid", turn.RunId);
		cmd.Parameters.AddWithValue("@floor", turn.Floor);
		cmd.Parameters.AddWithValue("@eid", (object)turn.EnemyId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@turn", turn.TurnNumber);
		cmd.Parameters.AddWithValue("@cards", turn.CardsPlayed != null ? JsonConvert.SerializeObject(turn.CardsPlayed) : (object)DBNull.Value);
		cmd.Parameters.AddWithValue("@dd", turn.DamageDealt);
		cmd.Parameters.AddWithValue("@dt", turn.DamageTaken);
		cmd.Parameters.AddWithValue("@bg", turn.BlockGenerated);
		cmd.Parameters.AddWithValue("@php", turn.PlayerHp);
		cmd.Parameters.AddWithValue("@ehp", turn.EnemyHp != null ? JsonConvert.SerializeObject(turn.EnemyHp) : (object)DBNull.Value);
		cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));
		cmd.ExecuteNonQuery();
	}

	// --- Card Usage Stats ---

	public void SaveCardUsageStats(List<CardUsageStat> stats)
	{
		if (!EnsureInitialized() || stats == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var tx = conn.BeginTransaction();
		using var del = conn.CreateCommand();
		del.CommandText = "DELETE FROM card_usage_stats";
		del.ExecuteNonQuery();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "INSERT INTO card_usage_stats (card_id, character, avg_plays_per_combat, total_plays, total_combats, effectiveness, sample_size) VALUES (@id, @char, @avg, @tp, @tc, @eff, @ss)";
		var pId = cmd.Parameters.Add("@id", SqliteType.Text);
		var pChar = cmd.Parameters.Add("@char", SqliteType.Text);
		var pAvg = cmd.Parameters.Add("@avg", SqliteType.Real);
		var pTp = cmd.Parameters.Add("@tp", SqliteType.Integer);
		var pTc = cmd.Parameters.Add("@tc", SqliteType.Integer);
		var pEff = cmd.Parameters.Add("@eff", SqliteType.Real);
		var pSs = cmd.Parameters.Add("@ss", SqliteType.Integer);
		foreach (var s in stats)
		{
			pId.Value = s.CardId;
			pChar.Value = s.Character;
			pAvg.Value = s.AvgPlaysPerCombat;
			pTp.Value = s.TotalPlays;
			pTc.Value = s.TotalCombats;
			pEff.Value = s.Effectiveness;
			pSs.Value = s.SampleSize;
			cmd.ExecuteNonQuery();
		}
		tx.Commit();
		Plugin.Log($"Saved {stats.Count} card usage stats.");
	}

	// --- Potion Events ---

	public void SavePotionEvent(PotionEvent evt)
	{
		if (!EnsureInitialized() || evt == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "INSERT INTO potion_events (run_id, potion_id, event_type, floor, enemy_id, timestamp) VALUES (@rid, @pid, @et, @floor, @eid, @ts)";
		cmd.Parameters.AddWithValue("@rid", evt.RunId);
		cmd.Parameters.AddWithValue("@pid", evt.PotionId);
		cmd.Parameters.AddWithValue("@et", evt.EventType);
		cmd.Parameters.AddWithValue("@floor", evt.Floor);
		cmd.Parameters.AddWithValue("@eid", (object)evt.EnemyId ?? DBNull.Value);
		cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow.ToString("o"));
		cmd.ExecuteNonQuery();
	}

	// --- Patch Changes ---

	public void SavePatchChanges(List<PatchChange> changes)
	{
		if (!EnsureInitialized() || changes == null) return;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var tx = conn.BeginTransaction();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = @"INSERT OR IGNORE INTO patch_changes (entity_type, entity_id, property, old_value, new_value, patch_version, patch_date, raw_text)
			VALUES (@et, @eid, @prop, @ov, @nv, @pv, @pd, @rt)";
		var pEt = cmd.Parameters.Add("@et", SqliteType.Text);
		var pEid = cmd.Parameters.Add("@eid", SqliteType.Text);
		var pProp = cmd.Parameters.Add("@prop", SqliteType.Text);
		var pOv = cmd.Parameters.Add("@ov", SqliteType.Text);
		var pNv = cmd.Parameters.Add("@nv", SqliteType.Text);
		var pPv = cmd.Parameters.Add("@pv", SqliteType.Text);
		var pPd = cmd.Parameters.Add("@pd", SqliteType.Text);
		var pRt = cmd.Parameters.Add("@rt", SqliteType.Text);
		foreach (var c in changes)
		{
			pEt.Value = c.EntityType;
			pEid.Value = c.EntityId;
			pProp.Value = c.Property;
			pOv.Value = (object)c.OldValue ?? DBNull.Value;
			pNv.Value = (object)c.NewValue ?? DBNull.Value;
			pPv.Value = c.PatchVersion;
			pPd.Value = c.PatchDate;
			pRt.Value = (object)c.RawText ?? DBNull.Value;
			cmd.ExecuteNonQuery();
		}
		tx.Commit();
		Plugin.Log($"Saved {changes.Count} patch changes.");
	}

	public List<PatchChange> GetRecentPatchChanges(string entityType, string entityId, int limit = 5)
	{
		var list = new List<PatchChange>();
		if (!EnsureInitialized()) return list;
		using var conn = new SqliteConnection(_connectionString);
		conn.Open();
		using var cmd = conn.CreateCommand();
		cmd.CommandText = "SELECT * FROM patch_changes WHERE entity_type=@et AND entity_id=@eid ORDER BY patch_date DESC LIMIT @limit";
		cmd.Parameters.AddWithValue("@et", entityType);
		cmd.Parameters.AddWithValue("@eid", entityId);
		cmd.Parameters.AddWithValue("@limit", limit);
		using var reader = cmd.ExecuteReader();
		while (reader.Read())
		{
			list.Add(new PatchChange
			{
				EntityType = reader.GetString(reader.GetOrdinal("entity_type")),
				EntityId = reader.GetString(reader.GetOrdinal("entity_id")),
				Property = reader.GetString(reader.GetOrdinal("property")),
				OldValue = reader.IsDBNull(reader.GetOrdinal("old_value")) ? null : reader.GetString(reader.GetOrdinal("old_value")),
				NewValue = reader.IsDBNull(reader.GetOrdinal("new_value")) ? null : reader.GetString(reader.GetOrdinal("new_value")),
				PatchVersion = reader.GetString(reader.GetOrdinal("patch_version")),
				PatchDate = reader.GetString(reader.GetOrdinal("patch_date")),
				RawText = reader.IsDBNull(reader.GetOrdinal("raw_text")) ? null : reader.GetString(reader.GetOrdinal("raw_text"))
			});
		}
		return list;
	}
}

// --- Data Classes ---

public class CardPairStat
{
	public string CardA { get; set; }
	public string CardB { get; set; }
	public string Character { get; set; }
	public int CoOccurrence { get; set; }
	public float WinRate { get; set; }
	public int SampleSize { get; set; }
}

public class FloorCardStat
{
	public string CardId { get; set; }
	public string Character { get; set; }
	public int Act { get; set; }
	public float PickRate { get; set; }
	public float WinRate { get; set; }
	public int SampleSize { get; set; }
}

public class UpgradeValue
{
	public string CardId { get; set; }
	public string Character { get; set; }
	public float UpgradeWinDelta { get; set; }
	public float UpgradeFrequency { get; set; }
	public int SampleSize { get; set; }
}

public class CombatTurnRecord
{
	public string RunId { get; set; }
	public int Floor { get; set; }
	public string EnemyId { get; set; }
	public int TurnNumber { get; set; }
	public List<string> CardsPlayed { get; set; }
	public int DamageDealt { get; set; }
	public int DamageTaken { get; set; }
	public int BlockGenerated { get; set; }
	public int PlayerHp { get; set; }
	public Dictionary<string, int> EnemyHp { get; set; }
}

public class CardUsageStat
{
	public string CardId { get; set; }
	public string Character { get; set; }
	public float AvgPlaysPerCombat { get; set; }
	public int TotalPlays { get; set; }
	public int TotalCombats { get; set; }
	public float Effectiveness { get; set; }
	public int SampleSize { get; set; }
}

public class PotionEvent
{
	public string RunId { get; set; }
	public string PotionId { get; set; }
	public string EventType { get; set; }
	public int Floor { get; set; }
	public string EnemyId { get; set; }
}

public class PatchChange
{
	public string EntityType { get; set; }
	public string EntityId { get; set; }
	public string Property { get; set; }
	public string OldValue { get; set; }
	public string NewValue { get; set; }
	public string PatchVersion { get; set; }
	public string PatchDate { get; set; }
	public string RawText { get; set; }
}
