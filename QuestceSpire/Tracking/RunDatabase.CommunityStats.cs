using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

public partial class RunDatabase
{
	public void SaveCommunityCardStats(List<CommunityCardStats> statsList)
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
			foreach (CommunityCardStats stats in statsList)
			{
				using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
				sqliteCommand.CommandText = "\r\n                                INSERT OR REPLACE INTO community_card_stats\r\n                                    (card_id, character, pick_rate, win_rate_when_picked, win_rate_when_skipped,\r\n                                     sample_size, avg_floor_picked, archetype_context)\r\n                                VALUES (@cardId, @character, @pickRate, @winPicked, @winSkipped,\r\n                                        @sampleSize, @avgFloor, @archetypeContext)";
				sqliteCommand.Parameters.AddWithValue("@cardId", stats.CardId);
				sqliteCommand.Parameters.AddWithValue("@character", stats.Character);
				sqliteCommand.Parameters.AddWithValue("@pickRate", stats.PickRate);
				sqliteCommand.Parameters.AddWithValue("@winPicked", stats.WinRateWhenPicked);
				sqliteCommand.Parameters.AddWithValue("@winSkipped", stats.WinRateWhenSkipped);
				sqliteCommand.Parameters.AddWithValue("@sampleSize", stats.SampleSize);
				sqliteCommand.Parameters.AddWithValue("@avgFloor", stats.AvgFloorPicked);
				sqliteCommand.Parameters.AddWithValue("@archetypeContext", JsonConvert.SerializeObject(stats.ArchetypeContext));
				sqliteCommand.ExecuteNonQuery();
			}
			sqliteTransaction.Commit();
		}
		catch (Exception ex)
		{
			try { sqliteTransaction.Rollback(); } catch { }
			Plugin.Log($"SaveCommunityCardStats error: {ex.Message}");
		}
	}

	public void SaveCommunityRelicStats(List<CommunityRelicStats> statsList)
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
			foreach (CommunityRelicStats stats in statsList)
			{
				using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
				sqliteCommand.CommandText = "\r\n                                INSERT OR REPLACE INTO community_relic_stats\r\n                                    (relic_id, character, pick_rate, win_rate_when_picked, win_rate_when_skipped,\r\n                                     sample_size, avg_floor_picked, archetype_context)\r\n                                VALUES (@relicId, @character, @pickRate, @winPicked, @winSkipped,\r\n                                        @sampleSize, @avgFloor, @archetypeContext)";
				sqliteCommand.Parameters.AddWithValue("@relicId", stats.RelicId);
				sqliteCommand.Parameters.AddWithValue("@character", stats.Character);
				sqliteCommand.Parameters.AddWithValue("@pickRate", stats.PickRate);
				sqliteCommand.Parameters.AddWithValue("@winPicked", stats.WinRateWhenPicked);
				sqliteCommand.Parameters.AddWithValue("@winSkipped", stats.WinRateWhenSkipped);
				sqliteCommand.Parameters.AddWithValue("@sampleSize", stats.SampleSize);
				sqliteCommand.Parameters.AddWithValue("@avgFloor", stats.AvgFloorPicked);
				sqliteCommand.Parameters.AddWithValue("@archetypeContext", JsonConvert.SerializeObject(stats.ArchetypeContext));
				sqliteCommand.ExecuteNonQuery();
			}
			sqliteTransaction.Commit();
		}
		catch (Exception ex)
		{
			try { sqliteTransaction.Rollback(); } catch { }
			Plugin.Log($"SaveCommunityRelicStats error: {ex.Message}");
		}
	}

	public CommunityCardStats GetCommunityCardStats(string character, string cardId)
	{
		if (!EnsureInitialized())
		{
			return null;
		}
		using (SqliteConnection sqliteConnection = new SqliteConnection(_connectionString))
		{
			sqliteConnection.Open();
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.CommandText = "\r\n                        SELECT * FROM community_card_stats\r\n                        WHERE character = @character AND card_id = @cardId";
			sqliteCommand.Parameters.AddWithValue("@character", character);
			sqliteCommand.Parameters.AddWithValue("@cardId", cardId);
			using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
			if (sqliteDataReader.Read())
			{
				return new CommunityCardStats
				{
					CardId = sqliteDataReader.GetString(sqliteDataReader.GetOrdinal("card_id")),
					Character = sqliteDataReader.GetString(sqliteDataReader.GetOrdinal("character")),
					PickRate = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("pick_rate")),
					WinRateWhenPicked = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("win_rate_when_picked")),
					WinRateWhenSkipped = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("win_rate_when_skipped")),
					SampleSize = sqliteDataReader.GetInt32(sqliteDataReader.GetOrdinal("sample_size")),
					AvgFloorPicked = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("avg_floor_picked")),
					ArchetypeContext = DeserializeDict(sqliteDataReader, "archetype_context")
				};
			}
		}
		return null;
	}

	public CommunityRelicStats GetCommunityRelicStats(string character, string relicId)
	{
		if (!EnsureInitialized())
		{
			return null;
		}
		using (SqliteConnection sqliteConnection = new SqliteConnection(_connectionString))
		{
			sqliteConnection.Open();
			using SqliteCommand sqliteCommand = sqliteConnection.CreateCommand();
			sqliteCommand.CommandText = "\r\n                        SELECT * FROM community_relic_stats\r\n                        WHERE character = @character AND relic_id = @relicId";
			sqliteCommand.Parameters.AddWithValue("@character", character);
			sqliteCommand.Parameters.AddWithValue("@relicId", relicId);
			using SqliteDataReader sqliteDataReader = sqliteCommand.ExecuteReader();
			if (sqliteDataReader.Read())
			{
				return new CommunityRelicStats
				{
					RelicId = sqliteDataReader.GetString(sqliteDataReader.GetOrdinal("relic_id")),
					Character = sqliteDataReader.GetString(sqliteDataReader.GetOrdinal("character")),
					PickRate = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("pick_rate")),
					WinRateWhenPicked = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("win_rate_when_picked")),
					WinRateWhenSkipped = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("win_rate_when_skipped")),
					SampleSize = sqliteDataReader.GetInt32(sqliteDataReader.GetOrdinal("sample_size")),
					AvgFloorPicked = sqliteDataReader.GetFloat(sqliteDataReader.GetOrdinal("avg_floor_picked")),
					ArchetypeContext = DeserializeDict(sqliteDataReader, "archetype_context")
				};
			}
		}
		return null;
	}

	public List<CommunityCardStats> GetAllCommunityCardStats()
	{
		var list = new List<CommunityCardStats>();
		if (!EnsureInitialized())
			return list;
		try
		{
			using var conn = new SqliteConnection(_connectionString);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT * FROM community_card_stats";
			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				list.Add(new CommunityCardStats
				{
					CardId = reader.GetString(reader.GetOrdinal("card_id")),
					Character = reader.GetString(reader.GetOrdinal("character")),
					PickRate = reader.GetFloat(reader.GetOrdinal("pick_rate")),
					WinRateWhenPicked = reader.GetFloat(reader.GetOrdinal("win_rate_when_picked")),
					WinRateWhenSkipped = reader.GetFloat(reader.GetOrdinal("win_rate_when_skipped")),
					SampleSize = reader.GetInt32(reader.GetOrdinal("sample_size")),
					AvgFloorPicked = reader.GetFloat(reader.GetOrdinal("avg_floor_picked")),
					ArchetypeContext = DeserializeDict(reader, "archetype_context")
				});
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetAllCommunityCardStats error: {ex.Message}");
		}
		return list;
	}

	public List<CommunityRelicStats> GetAllCommunityRelicStats()
	{
		var list = new List<CommunityRelicStats>();
		if (!EnsureInitialized())
			return list;
		try
		{
			using var conn = new SqliteConnection(_connectionString);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT * FROM community_relic_stats";
			using var reader = cmd.ExecuteReader();
			while (reader.Read())
			{
				list.Add(new CommunityRelicStats
				{
					RelicId = reader.GetString(reader.GetOrdinal("relic_id")),
					Character = reader.GetString(reader.GetOrdinal("character")),
					PickRate = reader.GetFloat(reader.GetOrdinal("pick_rate")),
					WinRateWhenPicked = reader.GetFloat(reader.GetOrdinal("win_rate_when_picked")),
					WinRateWhenSkipped = reader.GetFloat(reader.GetOrdinal("win_rate_when_skipped")),
					SampleSize = reader.GetInt32(reader.GetOrdinal("sample_size")),
					AvgFloorPicked = reader.GetFloat(reader.GetOrdinal("avg_floor_picked")),
					ArchetypeContext = DeserializeDict(reader, "archetype_context")
				});
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetAllCommunityRelicStats error: {ex.Message}");
		}
		return list;
	}

	public void MergeCommunityCardStats(List<CommunityCardStats> imported)
	{
		if (!EnsureInitialized() || imported == null)
			return;
		var merged = new List<CommunityCardStats>();
		foreach (var imp in imported)
		{
			var local = GetCommunityCardStats(imp.Character, imp.CardId);
			if (local != null)
			{
				int totalSamples = local.SampleSize + imp.SampleSize;
				if (totalSamples == 0) continue;
				float lw = (float)local.SampleSize / totalSamples;
				float iw = (float)imp.SampleSize / totalSamples;
				// Weight win rates by pick/skip counts, not total offers
				float localPicks = local.SampleSize * local.PickRate;
				float impPicks = imp.SampleSize * imp.PickRate;
				float totalPicks = localPicks + impPicks;
				float localSkips = local.SampleSize * (1f - local.PickRate);
				float impSkips = imp.SampleSize * (1f - imp.PickRate);
				float totalSkips = localSkips + impSkips;
				merged.Add(new CommunityCardStats
				{
					CardId = imp.CardId,
					Character = imp.Character,
					PickRate = local.PickRate * lw + imp.PickRate * iw,
					WinRateWhenPicked = totalPicks > 0
						? (local.WinRateWhenPicked * localPicks + imp.WinRateWhenPicked * impPicks) / totalPicks
						: 0f,
					WinRateWhenSkipped = totalSkips > 0
						? (local.WinRateWhenSkipped * localSkips + imp.WinRateWhenSkipped * impSkips) / totalSkips
						: 0f,
					SampleSize = totalSamples,
					AvgFloorPicked = totalPicks > 0
						? (local.AvgFloorPicked * localPicks + imp.AvgFloorPicked * impPicks) / totalPicks
						: 0f,
					ArchetypeContext = MergeArchetypes(local.ArchetypeContext, local.SampleSize, imp.ArchetypeContext, imp.SampleSize)
				});
			}
			else
			{
				merged.Add(imp);
			}
		}
		SaveCommunityCardStats(merged);
	}

	public void MergeCommunityRelicStats(List<CommunityRelicStats> imported)
	{
		if (!EnsureInitialized() || imported == null)
			return;
		var merged = new List<CommunityRelicStats>();
		foreach (var imp in imported)
		{
			var local = GetCommunityRelicStats(imp.Character, imp.RelicId);
			if (local != null)
			{
				int totalSamples = local.SampleSize + imp.SampleSize;
				if (totalSamples == 0) continue;
				float lw = (float)local.SampleSize / totalSamples;
				float iw = (float)imp.SampleSize / totalSamples;
				float localPicks = local.SampleSize * local.PickRate;
				float impPicks = imp.SampleSize * imp.PickRate;
				float totalPicks = localPicks + impPicks;
				float localSkips = local.SampleSize * (1f - local.PickRate);
				float impSkips = imp.SampleSize * (1f - imp.PickRate);
				float totalSkips = localSkips + impSkips;
				merged.Add(new CommunityRelicStats
				{
					RelicId = imp.RelicId,
					Character = imp.Character,
					PickRate = local.PickRate * lw + imp.PickRate * iw,
					WinRateWhenPicked = totalPicks > 0
						? (local.WinRateWhenPicked * localPicks + imp.WinRateWhenPicked * impPicks) / totalPicks
						: 0f,
					WinRateWhenSkipped = totalSkips > 0
						? (local.WinRateWhenSkipped * localSkips + imp.WinRateWhenSkipped * impSkips) / totalSkips
						: 0f,
					SampleSize = totalSamples,
					AvgFloorPicked = totalPicks > 0
						? (local.AvgFloorPicked * localPicks + imp.AvgFloorPicked * impPicks) / totalPicks
						: 0f,
					ArchetypeContext = MergeArchetypes(local.ArchetypeContext, local.SampleSize, imp.ArchetypeContext, imp.SampleSize)
				});
			}
			else
			{
				merged.Add(imp);
			}
		}
		SaveCommunityRelicStats(merged);
	}

	public (float localWinRate, int localRuns, float communityWinRate, int communitySamples) GetStatsComparison(string character)
	{
		if (!EnsureInitialized())
			return (0f, 0, 0f, 0);
		try
		{
			// Local stats
			var (wins, total) = GetCharacterWinRate(character);
			float localWinRate = total > 0 ? (float)wins / total * 100f : 0f;

			// Community average: sample-weighted mean of card win_rate_when_picked for this character
			float communityWinRate = 0f;
			int communitySamples = 0;
			using var conn = new SqliteConnection(_connectionString);
			conn.Open();
			using var cmd = conn.CreateCommand();
			cmd.CommandText = "SELECT CASE WHEN SUM(sample_size) > 0 THEN SUM(win_rate_when_picked * sample_size) / SUM(sample_size) ELSE 0.0 END as avg_wr, SUM(sample_size) as total_samples FROM community_card_stats WHERE character=@char AND sample_size > 0";
			cmd.Parameters.AddWithValue("@char", character);
			using var reader = cmd.ExecuteReader();
			if (reader.Read() && !reader.IsDBNull(0))
			{
				communityWinRate = reader.GetFloat(0) * 100f;
				communitySamples = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
			}

			return (localWinRate, total, communityWinRate, communitySamples);
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetStatsComparison error: {ex.Message}");
			return (0f, 0, 0f, 0);
		}
	}

	private static Dictionary<string, float> MergeArchetypes(
		Dictionary<string, float> a, int aSamples,
		Dictionary<string, float> b, int bSamples)
	{
		var result = new Dictionary<string, float>();
		int total = aSamples + bSamples;
		if (total == 0) return result;
		float aw = (float)aSamples / total;
		float bw = (float)bSamples / total;

		var allKeys = new HashSet<string>();
		if (a != null) foreach (var k in a.Keys) allKeys.Add(k);
		if (b != null) foreach (var k in b.Keys) allKeys.Add(k);

		foreach (var key in allKeys)
		{
			float va = a != null && a.TryGetValue(key, out var av) ? av : 0f;
			float vb = b != null && b.TryGetValue(key, out var bv) ? bv : 0f;
			bool inA = a != null && a.ContainsKey(key);
			bool inB = b != null && b.ContainsKey(key);
			if (inA && inB)
				result[key] = va * aw + vb * bw;
			else if (inA)
				result[key] = va;
			else
				result[key] = vb;
		}
		return result;
	}

	private static Dictionary<string, float> DeserializeDict(SqliteDataReader reader, string column)
	{
		if (reader.IsDBNull(reader.GetOrdinal(column)))
		{
			return new Dictionary<string, float>();
		}
		return JsonConvert.DeserializeObject<Dictionary<string, float>>(reader.GetString(reader.GetOrdinal(column))) ?? new Dictionary<string, float>();
	}
}
