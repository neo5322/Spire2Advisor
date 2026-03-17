using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace QuestceSpire.Tracking;

/// <summary>
/// Fetches global statistics from Steam leaderboards.
/// Caches results locally for "Your stats vs. global average" comparison.
/// </summary>
public class SteamLeaderboardSync
{
	private const string LeaderboardUrl = "https://steamcommunity.com/stats/2868840/leaderboards/?xml=1";

	private readonly string _dataFolder;

	public LeaderboardCache Cache { get; private set; }

	public SteamLeaderboardSync(string dataFolder)
	{
		_dataFolder = dataFolder;
		LoadCache();
	}

	public async Task Sync()
	{
		Plugin.Log("SteamLeaderboardSync: fetching leaderboards...");

		try
		{
			var xml = await PipelineHttp.RetryAsync(
				() => PipelineHttp.GetAsync(LeaderboardUrl, TimeSpan.FromSeconds(2)));

			var boards = ParseLeaderboardList(xml);
			if (boards.Count == 0)
			{
				Plugin.Log("SteamLeaderboardSync: no leaderboards found.");
				return;
			}

			Cache = new LeaderboardCache
			{
				LastUpdated = DateTime.UtcNow.ToString("o"),
				Leaderboards = boards
			};

			SaveCache();
			Plugin.Log($"SteamLeaderboardSync: cached {boards.Count} leaderboards.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"SteamLeaderboardSync: failed — {ex.Message}");
		}
	}

	/// <summary>
	/// Parse the XML leaderboard listing. Extracts board name, entry count, and sort method.
	/// This is a simple regex-based parser since we don't want to add System.Xml.Linq dependency.
	/// </summary>
	private static List<LeaderboardEntry> ParseLeaderboardList(string xml)
	{
		var boards = new List<LeaderboardEntry>();

		var nameMatches = Regex.Matches(xml, @"<name>([^<]+)</name>");
		var entryCountMatches = Regex.Matches(xml, @"<entries>(\d+)</entries>");
		var sortMatches = Regex.Matches(xml, @"<sortmethod>(\d+)</sortmethod>");

		int count = Math.Min(nameMatches.Count, entryCountMatches.Count);
		for (int i = 0; i < count; i++)
		{
			boards.Add(new LeaderboardEntry
			{
				Name = nameMatches[i].Groups[1].Value,
				EntryCount = int.TryParse(entryCountMatches[i].Groups[1].Value, out var ec) ? ec : 0,
				SortMethod = i < sortMatches.Count ? sortMatches[i].Groups[1].Value : "0"
			});
		}

		return boards;
	}

	private void LoadCache()
	{
		try
		{
			string path = Path.Combine(_dataFolder, "leaderboard_cache.json");
			if (File.Exists(path))
			{
				var json = File.ReadAllText(path);
				Cache = JsonConvert.DeserializeObject<LeaderboardCache>(json);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"SteamLeaderboardSync: failed to load cache — {ex.Message}");
		}
	}

	private void SaveCache()
	{
		try
		{
			string path = Path.Combine(_dataFolder, "leaderboard_cache.json");
			File.WriteAllText(path, JsonConvert.SerializeObject(Cache, Formatting.Indented));
		}
		catch (Exception ex)
		{
			Plugin.Log($"SteamLeaderboardSync: failed to save cache — {ex.Message}");
		}
	}
}

public class LeaderboardCache
{
	[JsonProperty("last_updated")] public string LastUpdated { get; set; }
	[JsonProperty("leaderboards")] public List<LeaderboardEntry> Leaderboards { get; set; } = new();
}

public class LeaderboardEntry
{
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("entry_count")] public int EntryCount { get; set; }
	[JsonProperty("sort_method")] public string SortMethod { get; set; }
}
