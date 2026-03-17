using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

public partial class RunDatabase : IRunDatabase
{
	private string _connectionString;

	private readonly string _pluginFolder;

	private bool _initialized;

	internal string ConnectionString
	{
		get
		{
			if (!_initialized)
			{
				return null;
			}
			return _connectionString;
		}
	}

	public RunDatabase(string pluginFolder)
	{
		_pluginFolder = pluginFolder;
	}

	private bool EnsureInitialized()
	{
		if (!_initialized || _connectionString == null)
		{
			Plugin.Log("RunDatabase not initialized — skipping operation.");
			return false;
		}
		return true;
	}

	public void InitializeDatabase()
	{
		string text = Path.Combine(_pluginFolder, "questcespire.db");
		_connectionString = "Data Source=" + text;
		using (SqliteConnection sqliteConnection = new SqliteConnection(_connectionString))
		{
			sqliteConnection.Open();
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.CommandText = "\r\n                        CREATE TABLE IF NOT EXISTS runs (\r\n                            run_id TEXT PRIMARY KEY,\r\n                            player_id TEXT NOT NULL,\r\n                            character TEXT NOT NULL,\r\n                            seed TEXT,\r\n                            start_time TEXT NOT NULL,\r\n                            end_time TEXT,\r\n                            outcome TEXT,\r\n                            final_floor INTEGER,\r\n                            final_act INTEGER,\r\n                            ascension_level INTEGER NOT NULL,\r\n                            synced INTEGER NOT NULL DEFAULT 0\r\n                        );\r\n\r\n                        CREATE TABLE IF NOT EXISTS decisions (\r\n                            id INTEGER PRIMARY KEY AUTOINCREMENT,\r\n                            run_id TEXT NOT NULL,\r\n                            floor INTEGER NOT NULL,\r\n                            act INTEGER NOT NULL,\r\n                            event_type TEXT NOT NULL,\r\n                            offered_ids TEXT NOT NULL,\r\n                            chosen_id TEXT,\r\n                            deck_snapshot TEXT NOT NULL,\r\n                            relic_snapshot TEXT NOT NULL,\r\n                            current_hp INTEGER NOT NULL,\r\n                            max_hp INTEGER NOT NULL,\r\n                            gold INTEGER NOT NULL,\r\n                            timestamp TEXT NOT NULL,\r\n                            FOREIGN KEY (run_id) REFERENCES runs(run_id)\r\n                        );\r\n\r\n                        CREATE TABLE IF NOT EXISTS community_card_stats (\r\n                            card_id TEXT NOT NULL,\r\n                            character TEXT NOT NULL,\r\n                            pick_rate REAL NOT NULL,\r\n                            win_rate_when_picked REAL NOT NULL,\r\n                            win_rate_when_skipped REAL NOT NULL,\r\n                            sample_size INTEGER NOT NULL,\r\n                            avg_floor_picked REAL NOT NULL,\r\n                            archetype_context TEXT,\r\n                            PRIMARY KEY (card_id, character)\r\n                        );\r\n\r\n                        CREATE TABLE IF NOT EXISTS community_relic_stats (\r\n                            relic_id TEXT NOT NULL,\r\n                            character TEXT NOT NULL,\r\n                            pick_rate REAL NOT NULL,\r\n                            win_rate_when_picked REAL NOT NULL,\r\n                            win_rate_when_skipped REAL NOT NULL,\r\n                            sample_size INTEGER NOT NULL,\r\n                            avg_floor_picked REAL NOT NULL,\r\n                            archetype_context TEXT,\r\n                            PRIMARY KEY (relic_id, character)\r\n                        );\r\n\r\n                        CREATE INDEX IF NOT EXISTS idx_decisions_run_id ON decisions(run_id);\r\n                        CREATE INDEX IF NOT EXISTS idx_runs_synced ON runs(synced);\r\n                    ";
			sqliteCommand.ExecuteNonQuery();
		}
		_initialized = true;
		Plugin.Log("RunDatabase initialized.");
		CreatePipelineTables();
	}

	public void SaveRun(RunLog run, List<DecisionEvent> decisions)
	{
		if (!EnsureInitialized())
		{
			return;
		}
		using SqliteConnection sqliteConnection = new SqliteConnection(_connectionString);
		sqliteConnection.Open();
		using SqliteTransaction sqliteTransaction = sqliteConnection.BeginTransaction();
		try
		{
			using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
			{
				sqliteCommand.CommandText = "\r\n                            INSERT INTO runs (run_id, player_id, character, seed, start_time, end_time,\r\n                                              outcome, final_floor, final_act, ascension_level, synced)\r\n                            VALUES (@runId, @playerId, @character, @seed, @startTime, @endTime,\r\n                                    @outcome, @finalFloor, @finalAct, @ascensionLevel, @synced)";
				sqliteCommand.Parameters.AddWithValue("@runId", run.RunId);
				sqliteCommand.Parameters.AddWithValue("@playerId", run.PlayerId);
				sqliteCommand.Parameters.AddWithValue("@character", run.Character);
				sqliteCommand.Parameters.AddWithValue("@seed", ((object)run.Seed) ?? ((object)DBNull.Value));
				sqliteCommand.Parameters.AddWithValue("@startTime", run.StartTime.ToString("o"));
				sqliteCommand.Parameters.AddWithValue("@endTime", run.EndTime.HasValue ? ((IConvertible)run.EndTime.Value.ToString("o")) : ((IConvertible)DBNull.Value));
				sqliteCommand.Parameters.AddWithValue("@outcome", run.Outcome.HasValue ? ((IConvertible)run.Outcome.Value.ToString()) : ((IConvertible)DBNull.Value));
				sqliteCommand.Parameters.AddWithValue("@finalFloor", ((object)run.FinalFloor) ?? DBNull.Value);
				sqliteCommand.Parameters.AddWithValue("@finalAct", ((object)run.FinalAct) ?? DBNull.Value);
				sqliteCommand.Parameters.AddWithValue("@ascensionLevel", run.AscensionLevel);
				sqliteCommand.Parameters.AddWithValue("@synced", run.Synced ? 1 : 0);
				sqliteCommand.ExecuteNonQuery();
			}
			foreach (DecisionEvent decision in decisions)
			{
				using SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand();
				sqliteCommand2.CommandText = "\r\n                                INSERT INTO decisions (run_id, floor, act, event_type, offered_ids, chosen_id,\r\n                                                       deck_snapshot, relic_snapshot, current_hp, max_hp, gold, timestamp)\r\n                                VALUES (@runId, @floor, @act, @eventType, @offeredIds, @chosenId,\r\n                                        @deckSnapshot, @relicSnapshot, @currentHp, @maxHp, @gold, @timestamp)";
				sqliteCommand2.Parameters.AddWithValue("@runId", decision.RunId);
				sqliteCommand2.Parameters.AddWithValue("@floor", decision.Floor);
				sqliteCommand2.Parameters.AddWithValue("@act", decision.Act);
				sqliteCommand2.Parameters.AddWithValue("@eventType", decision.EventType.ToString());
				sqliteCommand2.Parameters.AddWithValue("@offeredIds", JsonConvert.SerializeObject(decision.OfferedIds));
				sqliteCommand2.Parameters.AddWithValue("@chosenId", ((object)decision.ChosenId) ?? ((object)DBNull.Value));
				sqliteCommand2.Parameters.AddWithValue("@deckSnapshot", JsonConvert.SerializeObject(decision.DeckSnapshot));
				sqliteCommand2.Parameters.AddWithValue("@relicSnapshot", JsonConvert.SerializeObject(decision.RelicSnapshot));
				sqliteCommand2.Parameters.AddWithValue("@currentHp", decision.CurrentHP);
				sqliteCommand2.Parameters.AddWithValue("@maxHp", decision.MaxHP);
				sqliteCommand2.Parameters.AddWithValue("@gold", decision.Gold);
				sqliteCommand2.Parameters.AddWithValue("@timestamp", decision.Timestamp.ToString("o"));
				sqliteCommand2.ExecuteNonQuery();
			}
			sqliteTransaction.Commit();
		}
		catch (Exception ex)
		{
			try { sqliteTransaction.Rollback(); } catch { }
			Plugin.Log($"SaveRun error: {ex.Message}");
		}
	}

	public List<(RunLog Run, List<DecisionEvent> Decisions)> GetUnsynced()
	{
		List<(RunLog, List<DecisionEvent>)> list = new List<(RunLog, List<DecisionEvent>)>();
		if (!EnsureInitialized())
		{
			return list;
		}
		using SqliteConnection sqliteConnection = new SqliteConnection(_connectionString);
		sqliteConnection.Open();
		List<RunLog> list2 = new List<RunLog>();
		using (SqliteCommand sqliteCommand = sqliteConnection.CreateCommand())
		{
			sqliteCommand.CommandText = "SELECT * FROM runs WHERE synced = 0";
			using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
			while (sqliteDataReader.Read())
			{
				list2.Add(ReadRunLog(sqliteDataReader));
			}
		}
		foreach (RunLog item in list2)
		{
			List<DecisionEvent> list3 = new List<DecisionEvent>();
			using (SqliteCommand sqliteCommand2 = sqliteConnection.CreateCommand())
			{
				sqliteCommand2.CommandText = "SELECT * FROM decisions WHERE run_id = @runId ORDER BY timestamp";
				sqliteCommand2.Parameters.AddWithValue("@runId", item.RunId);
				using SqliteDataReader sqliteDataReader2 = sqliteCommand2.ExecuteReader();
				while (sqliteDataReader2.Read())
				{
					list3.Add(ReadDecisionEvent(sqliteDataReader2));
				}
			}
			list.Add((item, list3));
		}
		return list;
	}

	public void MarkSynced(string runId)
	{
		if (!EnsureInitialized())
		{
			return;
		}
		using SqliteConnection sqliteConnection = new SqliteConnection(_connectionString);
		sqliteConnection.Open();
		using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
		sqliteCommand.CommandText = "UPDATE runs SET synced = 1 WHERE run_id = @runId";
		sqliteCommand.Parameters.AddWithValue("@runId", runId);
		sqliteCommand.ExecuteNonQuery();
	}

	private static RunLog ReadRunLog(SqliteDataReader reader)
	{
		RunLog runLog = new RunLog
		{
			RunId = reader.GetString(reader.GetOrdinal("run_id")),
			PlayerId = reader.GetString(reader.GetOrdinal("player_id")),
			Character = reader.GetString(reader.GetOrdinal("character")),
			Seed = (reader.IsDBNull(reader.GetOrdinal("seed")) ? null : reader.GetString(reader.GetOrdinal("seed"))),
			StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("start_time")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
			AscensionLevel = reader.GetInt32(reader.GetOrdinal("ascension_level")),
			Synced = (reader.GetInt32(reader.GetOrdinal("synced")) == 1)
		};
		if (!reader.IsDBNull(reader.GetOrdinal("end_time")))
		{
			runLog.EndTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("end_time")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
		}
		if (!reader.IsDBNull(reader.GetOrdinal("outcome")) && Enum.TryParse<RunOutcome>(reader.GetString(reader.GetOrdinal("outcome")), out var result))
		{
			runLog.Outcome = result;
		}
		if (!reader.IsDBNull(reader.GetOrdinal("final_floor")))
		{
			runLog.FinalFloor = reader.GetInt32(reader.GetOrdinal("final_floor"));
		}
		if (!reader.IsDBNull(reader.GetOrdinal("final_act")))
		{
			runLog.FinalAct = reader.GetInt32(reader.GetOrdinal("final_act"));
		}
		return runLog;
	}

	private static DecisionEvent ReadDecisionEvent(SqliteDataReader reader)
	{
		DecisionEventType result;
		return new DecisionEvent
		{
			RunId = reader.GetString(reader.GetOrdinal("run_id")),
			Floor = reader.GetInt32(reader.GetOrdinal("floor")),
			Act = reader.GetInt32(reader.GetOrdinal("act")),
			EventType = (Enum.TryParse<DecisionEventType>(reader.GetString(reader.GetOrdinal("event_type")), out result) ? result : DecisionEventType.CardReward),
			OfferedIds = JsonConvert.DeserializeObject<List<string>>(reader.GetString(reader.GetOrdinal("offered_ids"))) ?? new List<string>(),
			ChosenId = (reader.IsDBNull(reader.GetOrdinal("chosen_id")) ? null : reader.GetString(reader.GetOrdinal("chosen_id"))),
			DeckSnapshot = JsonConvert.DeserializeObject<List<string>>(reader.GetString(reader.GetOrdinal("deck_snapshot"))) ?? new List<string>(),
			RelicSnapshot = JsonConvert.DeserializeObject<List<string>>(reader.GetString(reader.GetOrdinal("relic_snapshot"))) ?? new List<string>(),
			CurrentHP = reader.GetInt32(reader.GetOrdinal("current_hp")),
			MaxHP = reader.GetInt32(reader.GetOrdinal("max_hp")),
			Gold = reader.GetInt32(reader.GetOrdinal("gold")),
			Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp")), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
		};
	}

	public (int wins, int total) GetCharacterWinRate(string character)
	{
		if (!EnsureInitialized())
			return (0, 0);
		try
		{
			using var conn = new SqliteConnection(_connectionString);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT COUNT(*) as total, SUM(CASE WHEN outcome='Win' THEN 1 ELSE 0 END) as wins FROM runs WHERE character=@char AND outcome IS NOT NULL";
			cmd.Parameters.AddWithValue("@char", character);
			using var reader = cmd.ExecuteReader();
			if (reader.Read())
			{
				int total = reader.GetInt32(0);
				int wins = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
				return (wins, total);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetCharacterWinRate error: {ex.Message}");
		}
		return (0, 0);
	}

	public int GetTotalRunCount()
	{
		if (!EnsureInitialized())
			return 0;
		try
		{
			using var conn = new SqliteConnection(_connectionString);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT COUNT(*) FROM runs";
			return Convert.ToInt32(cmd.ExecuteScalar());
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetTotalRunCount error: {ex.Message}");
			return 0;
		}
	}

}
