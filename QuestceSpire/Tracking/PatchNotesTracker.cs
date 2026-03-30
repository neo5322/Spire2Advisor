using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace QuestceSpire.Tracking;

/// <summary>
/// Fetches Steam patch notes and parses balance changes (damage/cost/block adjustments).
/// Stores structured PatchChange records for UI display.
/// </summary>
public class PatchNotesTracker
{
	private const string SteamNewsUrl = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=2868840&count=10&feeds=steam_community_announcements";

	private readonly RunDatabase _db;

	// Patterns for detecting balance changes in patch notes
	private static readonly Regex DamageChangePattern = new(
		@"(?<card>[\w\s']+?)[\s:]+damage\s+(?<old>\d+)\s*[→>-]+\s*(?<new>\d+)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex BlockChangePattern = new(
		@"(?<card>[\w\s']+?)[\s:]+block\s+(?<old>\d+)\s*[→>-]+\s*(?<new>\d+)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex CostChangePattern = new(
		@"(?<card>[\w\s']+?)[\s:]+(?:cost|energy)\s+(?<old>\d+)\s*[→>-]+\s*(?<new>\d+)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private static readonly Regex GenericChangePattern = new(
		@"(?<card>[\w\s']+?)[\s:]+(?<prop>\w+)\s+(?<old>\d+)\s*[→>-]+\s*(?<new>\d+)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	// HTML tag stripper
	private static readonly Regex HtmlTags = new(@"<[^>]+>", RegexOptions.Compiled);

	public PatchNotesTracker(RunDatabase db)
	{
		_db = db;
	}

	public async Task FetchAndParse()
	{
		Plugin.Log("PatchNotesTracker: fetching Steam patch notes...");

		try
		{
			var json = await PipelineHttp.RetryAsync(
				() => PipelineHttp.GetAsync(SteamNewsUrl));

			if (json.Length > 1_000_000) // 1MB limit for patch notes
			{
				Plugin.Log("PatchNotesTracker: response too large, skipping.");
				return;
			}

			var root = JObject.Parse(json);
			var newsItems = root["appnews"]?["newsitems"] as JArray;
			if (newsItems == null || newsItems.Count == 0)
			{
				Plugin.Log("PatchNotesTracker: no news items found.");
				return;
			}

			var allChanges = new List<PatchChange>();

			foreach (JObject item in newsItems)
			{
				string title = item["title"]?.ToString() ?? "";
				string contents = item["contents"]?.ToString() ?? "";
				long dateUnix = item["date"]?.Value<long>() ?? 0;
				string date = DateTimeOffset.FromUnixTimeSeconds(dateUnix).ToString("yyyy-MM-dd");

				// Only process patch/update notes
				if (!IsPatchNote(title)) continue;

				string patchVersion = ExtractVersion(title) ?? date;
				string plainText = StripHtml(contents);

				var changes = ParseBalanceChanges(plainText, patchVersion, date);
				allChanges.AddRange(changes);
			}

			if (allChanges.Count > 0)
			{
				_db.SavePatchChanges(allChanges);
				Plugin.Log($"PatchNotesTracker: parsed {allChanges.Count} balance changes.");
			}
			else
			{
				Plugin.Log("PatchNotesTracker: no balance changes detected.");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"PatchNotesTracker: failed — {ex.Message}");
		}
	}

	private static bool IsPatchNote(string title)
	{
		var lower = title.ToLowerInvariant();
		return lower.Contains("patch") || lower.Contains("update") ||
		       lower.Contains("hotfix") || lower.Contains("balance") ||
		       lower.Contains("v0.") || lower.Contains("v1.");
	}

	private static string ExtractVersion(string title)
	{
		var match = Regex.Match(title, @"v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
		return match.Success ? match.Groups[1].Value : null;
	}

	private static string StripHtml(string html)
	{
		if (string.IsNullOrEmpty(html)) return "";
		string text = HtmlTags.Replace(html, " ");
		text = System.Net.WebUtility.HtmlDecode(text);
		return Regex.Replace(text, @"\s+", " ").Trim();
	}

	internal static List<PatchChange> ParseBalanceChanges(string text, string patchVersion, string patchDate)
	{
		var changes = new List<PatchChange>();
		var seen = new HashSet<string>();

		// Try specific patterns first
		ParseWithPattern(DamageChangePattern, "damage", text, patchVersion, patchDate, changes, seen);
		ParseWithPattern(BlockChangePattern, "block", text, patchVersion, patchDate, changes, seen);
		ParseWithPattern(CostChangePattern, "cost", text, patchVersion, patchDate, changes, seen);

		// Generic fallback for other properties
		foreach (Match m in GenericChangePattern.Matches(text))
		{
			string card = m.Groups["card"].Value.Trim();
			string prop = m.Groups["prop"].Value.ToLowerInvariant();
			string key = $"{card}|{prop}|{m.Groups["old"].Value}|{m.Groups["new"].Value}";
			if (seen.Contains(key)) continue;

			if (prop == "damage" || prop == "block" || prop == "cost") continue; // already handled

			seen.Add(key);
			changes.Add(new PatchChange
			{
				EntityType = "card",
				EntityId = card,
				Property = prop,
				OldValue = m.Groups["old"].Value,
				NewValue = m.Groups["new"].Value,
				PatchVersion = patchVersion,
				PatchDate = patchDate,
				RawText = m.Value
			});
		}

		return changes;
	}

	private static void ParseWithPattern(Regex pattern, string property, string text,
		string patchVersion, string patchDate, List<PatchChange> changes, HashSet<string> seen)
	{
		foreach (Match m in pattern.Matches(text))
		{
			string card = m.Groups["card"].Value.Trim();
			string key = $"{card}|{property}|{m.Groups["old"].Value}|{m.Groups["new"].Value}";
			if (seen.Contains(key)) continue;
			seen.Add(key);

			changes.Add(new PatchChange
			{
				EntityType = "card",
				EntityId = card,
				Property = property,
				OldValue = m.Groups["old"].Value,
				NewValue = m.Groups["new"].Value,
				PatchVersion = patchVersion,
				PatchDate = patchDate,
				RawText = m.Value
			});
		}
	}
}
