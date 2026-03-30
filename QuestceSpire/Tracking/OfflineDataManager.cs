using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Manages offline data availability. Ensures bundled data files are present,
/// provides disk cache for remote data, and enables graceful degradation when offline.
/// </summary>
public class OfflineDataManager
{
	private readonly string _dataPath;
	private readonly string _cachePath;

	/// <summary>
	/// Files that must exist for basic functionality (bundled with the mod).
	/// </summary>
	private static readonly string[] RequiredFiles =
	{
		"archetypes.json",
		"scoring_config.json",
		"BossData/bosses.json",
		"EventAdvice/events.json",
		"EnemyTips/enemies.json"
	};

	/// <summary>
	/// Files fetched from remote sources that are cached locally.
	/// </summary>
	private static readonly string[] CacheableFiles =
	{
		"codex_cards.json",
		"codex_relics.json",
		"codex_potions.json",
		"codex_monsters.json",
		"leaderboard_cache.json",
		"meta_archetypes.json"
	};

	/// <summary>
	/// Write content to file atomically using temp file + rename pattern.
	/// Prevents data corruption if process crashes mid-write.
	/// </summary>
	public static void AtomicWriteAllText(string path, string content)
	{
		string tempPath = path + ".tmp";
		try
		{
			File.WriteAllText(tempPath, content);
			if (File.Exists(path))
				File.Replace(tempPath, path, null);
			else
				File.Move(tempPath, path);
		}
		catch
		{
			// Clean up temp file on failure
			try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
			throw;
		}
	}

	public OfflineDataManager(string dataPath)
	{
		_dataPath = dataPath;
		_cachePath = Path.Combine(dataPath, ".cache");
	}

	/// <summary>
	/// Verify all required data files exist. Returns missing file list.
	/// </summary>
	public List<string> VerifyRequiredFiles()
	{
		var missing = new List<string>();
		foreach (string file in RequiredFiles)
		{
			string path = Path.Combine(_dataPath, file);
			if (!File.Exists(path))
				missing.Add(file);
		}
		if (missing.Count > 0)
			Plugin.Log($"OfflineDataManager: {missing.Count} required files missing: {string.Join(", ", missing)}");
		else
			Plugin.Log("OfflineDataManager: all required files present.");
		return missing;
	}

	/// <summary>
	/// Save remote data to disk cache with timestamp.
	/// </summary>
	public void CacheData(string filename, string jsonContent)
	{
		try
		{
			if (!Directory.Exists(_cachePath))
				Directory.CreateDirectory(_cachePath);

			string filePath = Path.Combine(_cachePath, filename);
			File.WriteAllText(filePath, jsonContent);

			// Save metadata
			string metaPath = filePath + ".meta";
			var meta = new CacheMeta
			{
				CachedAt = DateTime.UtcNow,
				SizeBytes = jsonContent.Length
			};
			File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta));
		}
		catch (Exception ex)
		{
			Plugin.Log($"OfflineDataManager: cache write error for {filename} — {ex.Message}");
		}
	}

	/// <summary>
	/// Load cached data if available and not expired.
	/// </summary>
	public string LoadCachedData(string filename, TimeSpan maxAge)
	{
		try
		{
			string filePath = Path.Combine(_cachePath, filename);
			string metaPath = filePath + ".meta";

			if (!File.Exists(filePath)) return null;

			// Check age
			if (File.Exists(metaPath))
			{
				var meta = JsonConvert.DeserializeObject<CacheMeta>(File.ReadAllText(metaPath));
				if (meta != null && DateTime.UtcNow - meta.CachedAt > maxAge)
				{
					Plugin.Log($"OfflineDataManager: cache expired for {filename} (age: {DateTime.UtcNow - meta.CachedAt})");
					return null;
				}
			}

			return File.ReadAllText(filePath);
		}
		catch (Exception ex)
		{
			Plugin.Log($"OfflineDataManager: cache read error for {filename} — {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Try loading from main data path first, then cache, then return null.
	/// </summary>
	public string LoadWithFallback(string filename, TimeSpan cacheMaxAge)
	{
		// 1. Try main data path
		string mainPath = Path.Combine(_dataPath, filename);
		if (File.Exists(mainPath))
		{
			try { return File.ReadAllText(mainPath); }
			catch (Exception ex) { Plugin.Log($"OfflineDataManager: failed to read cached file '{filename}': {ex.Message}"); }
		}

		// 2. Try cache
		return LoadCachedData(filename, cacheMaxAge);
	}

	/// <summary>
	/// Check if we have any cached data available for offline mode.
	/// </summary>
	public OfflineStatus GetOfflineStatus()
	{
		var status = new OfflineStatus();
		foreach (string file in CacheableFiles)
		{
			string mainPath = Path.Combine(_dataPath, file);
			string cachePath = Path.Combine(_cachePath, file);

			if (File.Exists(mainPath))
				status.AvailableFiles.Add(file);
			else if (File.Exists(cachePath))
				status.CachedFiles.Add(file);
			else
				status.MissingFiles.Add(file);
		}
		status.IsFullyOfflineCapable = status.MissingFiles.Count == 0;
		return status;
	}

	/// <summary>
	/// Clean up old cache files beyond retention period.
	/// </summary>
	public void CleanupOldCache(TimeSpan maxRetention)
	{
		if (!Directory.Exists(_cachePath)) return;
		try
		{
			int cleaned = 0;
			foreach (string file in Directory.GetFiles(_cachePath, "*.meta"))
			{
				var meta = JsonConvert.DeserializeObject<CacheMeta>(File.ReadAllText(file));
				if (meta != null && DateTime.UtcNow - meta.CachedAt > maxRetention)
				{
					string dataFile = file.Replace(".meta", "");
					if (File.Exists(dataFile)) File.Delete(dataFile);
					File.Delete(file);
					cleaned++;
				}
			}
			if (cleaned > 0)
				Plugin.Log($"OfflineDataManager: cleaned {cleaned} expired cache files.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"OfflineDataManager: cache cleanup error — {ex.Message}");
		}
	}

	private class CacheMeta
	{
		public DateTime CachedAt { get; set; }
		public long SizeBytes { get; set; }
	}
}

public class OfflineStatus
{
	public List<string> AvailableFiles { get; set; } = new();
	public List<string> CachedFiles { get; set; } = new();
	public List<string> MissingFiles { get; set; } = new();
	public bool IsFullyOfflineCapable { get; set; }
}
