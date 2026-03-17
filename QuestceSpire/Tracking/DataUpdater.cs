using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Downloads latest tier/enemy/event/boss data from the API at mod startup.
/// Falls back gracefully to bundled Data/ files if download fails.
/// </summary>
public class DataUpdater
{
	private const string DefaultApiBase = "https://questcespire-api.questcespire.workers.dev/api";

	private readonly string _dataFolder;
	private readonly HttpClient _http;
	private readonly string _apiBase;

	private static readonly string[] DataEndpoints =
	{
		"data/card-tiers",
		"data/relic-tiers",
		"data/enemies",
		"data/events",
		"data/bosses",
		"data/archetypes",
	};

	// Maps API endpoint → local subfolder/file
	private static readonly Dictionary<string, string> EndpointToLocalPath = new()
	{
		["data/card-tiers"] = "CardTiers",
		["data/relic-tiers"] = "RelicTiers",
		["data/enemies"] = Path.Combine("EnemyTips", "enemies.json"),
		["data/events"] = Path.Combine("EventAdvice", "events.json"),
		["data/bosses"] = Path.Combine("BossData", "bosses.json"),
		["data/archetypes"] = "archetypes.json",
	};

	public int UpdatedCount { get; private set; }
	public int FailedCount { get; private set; }

	public DataUpdater(string dataFolder, string apiBase = null)
	{
		_dataFolder = dataFolder;
		_apiBase = apiBase ?? DefaultApiBase;
		_http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
	}

	/// <summary>
	/// Check for data updates and download newer files.
	/// Returns true if any data was updated (callers should reload).
	/// </summary>
	public async Task<bool> CheckAndUpdate()
	{
		UpdatedCount = 0;
		FailedCount = 0;

		try
		{
			// First, get the manifest with version hashes
			var manifestJson = await _http.GetStringAsync($"{_apiBase}/data/manifest");
			var manifest = JsonConvert.DeserializeObject<DataManifest>(manifestJson);
			if (manifest?.Files == null || manifest.Files.Count == 0)
			{
				Plugin.Log("DataUpdater: empty manifest, skipping update.");
				return false;
			}

			var localManifest = LoadLocalManifest();

			foreach (var entry in manifest.Files)
			{
				try
				{
					// Skip if local version matches
					if (localManifest.TryGetValue(entry.Key, out var localHash) && localHash == entry.Value.Hash)
						continue;

					await DownloadDataFile(entry.Key, entry.Value);
					localManifest[entry.Key] = entry.Value.Hash;
					UpdatedCount++;
				}
				catch (Exception ex)
				{
					Plugin.Log($"DataUpdater: failed to update {entry.Key}: {ex.Message}");
					FailedCount++;
				}
			}

			if (UpdatedCount > 0)
			{
				SaveLocalManifest(localManifest);
				Plugin.Log($"DataUpdater: updated {UpdatedCount} file(s), {FailedCount} failed.");
			}
			else
			{
				Plugin.Log("DataUpdater: all data files up to date.");
			}

			return UpdatedCount > 0;
		}
		catch (Exception ex)
		{
			Plugin.Log($"DataUpdater: manifest fetch failed ({ex.Message}), using bundled data.");
			return false;
		}
	}

	private async Task DownloadDataFile(string key, ManifestEntry entry)
	{
		string url = entry.Url;
		if (string.IsNullOrEmpty(url))
			url = $"{_apiBase}/{key}";

		var response = await _http.GetAsync(url);
		response.EnsureSuccessStatusCode();
		var content = await response.Content.ReadAsStringAsync();

		if (string.IsNullOrWhiteSpace(content))
			throw new InvalidOperationException("Empty response");

		string localPath = ResolveLocalPath(key);
		if (localPath == null)
			throw new InvalidOperationException($"Unknown data key: {key}");

		string fullPath = Path.Combine(_dataFolder, localPath);

		// If it's a directory-type endpoint (card-tiers, relic-tiers), the response is a dict of files
		if (key.EndsWith("-tiers"))
		{
			var files = JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
			if (files != null)
			{
				string dir = Path.Combine(_dataFolder, localPath);
				Directory.CreateDirectory(dir);
				foreach (var (fileName, fileContent) in files)
				{
					string filePath = Path.Combine(dir, fileName.EndsWith(".json") ? fileName : fileName + ".json");
					string serialized = fileContent is string s ? s : JsonConvert.SerializeObject(fileContent, Formatting.Indented);
					File.WriteAllText(filePath, serialized);
				}
			}
		}
		else
		{
			// Single file endpoint
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
			File.WriteAllText(fullPath, content);
		}

		Plugin.Log($"DataUpdater: updated {key}");
	}

	private static string ResolveLocalPath(string key)
	{
		if (EndpointToLocalPath.TryGetValue(key, out var path))
			return path;
		// Fallback: strip "data/" prefix and use as-is
		if (key.StartsWith("data/"))
			return key.Substring(5);
		return null;
	}

	private Dictionary<string, string> LoadLocalManifest()
	{
		try
		{
			string path = Path.Combine(_dataFolder, ".data_manifest.json");
			if (File.Exists(path))
			{
				var json = File.ReadAllText(path);
				return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"DataUpdater: failed to read local manifest: {ex.Message}");
		}
		return new Dictionary<string, string>();
	}

	private void SaveLocalManifest(Dictionary<string, string> manifest)
	{
		try
		{
			string path = Path.Combine(_dataFolder, ".data_manifest.json");
			File.WriteAllText(path, JsonConvert.SerializeObject(manifest, Formatting.Indented));
		}
		catch (Exception ex)
		{
			Plugin.Log($"DataUpdater: failed to save local manifest: {ex.Message}");
		}
	}

	private class DataManifest
	{
		[JsonProperty("version")]
		public int Version { get; set; }

		[JsonProperty("files")]
		public Dictionary<string, ManifestEntry> Files { get; set; }
	}

	private class ManifestEntry
	{
		[JsonProperty("hash")]
		public string Hash { get; set; }

		[JsonProperty("url")]
		public string Url { get; set; }

		[JsonProperty("updated")]
		public string Updated { get; set; }
	}
}
