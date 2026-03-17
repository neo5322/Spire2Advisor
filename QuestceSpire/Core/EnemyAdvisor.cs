using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace QuestceSpire.Core;

public class EnemyAdvisor
{
	private readonly Dictionary<string, EnemyTipEntry> _tipsByEnemyId = new(StringComparer.OrdinalIgnoreCase);
	private readonly string _dataFolder;

	public EnemyAdvisor(string dataFolder)
	{
		_dataFolder = dataFolder;
		LoadData();
	}

	/// <summary>
	/// Reload enemy data from disk. Call after DataUpdater downloads new files.
	/// </summary>
	public void Reload()
	{
		_tipsByEnemyId.Clear();
		LoadData();
		Plugin.Log("EnemyAdvisor: reloaded enemy data.");
	}

	private void LoadData()
	{
		try
		{
			string path = Path.Combine(_dataFolder, "EnemyTips", "enemies.json");
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				var data = JsonConvert.DeserializeObject<EnemyTipData>(json);
				if (data?.Enemies != null)
				{
					foreach (var entry in data.Enemies)
					{
						if (!string.IsNullOrEmpty(entry.EnemyId))
						{
							_tipsByEnemyId[entry.EnemyId] = entry;
							// Also index by UPPER_SNAKE_CASE, PascalCase, and Title Case variants
							string snake = PascalToSnake(entry.EnemyId);
							if (snake != entry.EnemyId)
								_tipsByEnemyId.TryAdd(snake, entry);
							string pascal = SnakeToPascal(entry.EnemyId);
							if (pascal != entry.EnemyId)
								_tipsByEnemyId.TryAdd(pascal, entry);
							// Title Case with spaces (game's EncounterModel.Id.Entry format)
							string titleCase = entry.EnemyId.Replace("_", " ");
							if (titleCase != entry.EnemyId)
								_tipsByEnemyId.TryAdd(titleCase, entry);
						}
					}
				}
				Plugin.Log($"EnemyAdvisor loaded {_tipsByEnemyId.Count} enemy entries.");
			}
			else
			{
				Plugin.Log("EnemyAdvisor: enemies.json not found, enemy-specific tips unavailable.");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"EnemyAdvisor init error: {ex.Message}");
		}
	}

	/// <summary>PascalCase → UPPER_SNAKE_CASE (e.g., ShrinkerBeetleWeak → SHRINKER_BEETLE_WEAK)</summary>
	private static string PascalToSnake(string pascal)
	{
		return Regex.Replace(pascal, @"(?<!^)([A-Z])", "_$1").ToUpperInvariant();
	}

	/// <summary>UPPER_SNAKE_CASE → PascalCase (e.g., SHRINKER_BEETLE_WEAK → ShrinkerBeetleWeak)</summary>
	private static string SnakeToPascal(string snake)
	{
		if (!snake.Contains('_')) return snake;
		string[] parts = snake.Split('_');
		var sb = new StringBuilder(snake.Length);
		foreach (string part in parts)
		{
			if (part.Length > 0)
			{
				sb.Append(char.ToUpper(part[0]));
				if (part.Length > 1)
					sb.Append(part, 1, part.Length - 1);
			}
		}
		// Convert to proper casing: first char upper, rest lower per segment
		// Re-process to get PascalCase from UPPER_SNAKE
		sb.Clear();
		foreach (string part in parts)
		{
			if (part.Length > 0)
			{
				sb.Append(char.ToUpper(part[0]));
				for (int i = 1; i < part.Length; i++)
					sb.Append(char.ToLower(part[i]));
			}
		}
		return sb.ToString();
	}

	public List<EnemyTipEntry> GetTips(List<string> enemyIds)
	{
		if (enemyIds == null || enemyIds.Count == 0)
			return null;

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var result = new List<EnemyTipEntry>();
		foreach (var id in enemyIds)
		{
			if (seen.Contains(id)) continue;
			seen.Add(id);
			// Try exact match first, then strip variant suffixes (_WEAK, _ELITE, etc.)
			if (_tipsByEnemyId.TryGetValue(id, out var entry))
				result.Add(entry);
			else
			{
				string baseId = Regex.Replace(id, @"_(WEAK|ELITE|STRONG|BOSS|HARD|EASY)$", "", RegexOptions.IgnoreCase);
				if (baseId != id && _tipsByEnemyId.TryGetValue(baseId, out var baseEntry) && !seen.Contains(baseId))
				{
					seen.Add(baseId);
					result.Add(baseEntry);
				}
			}
		}

		return result.Count > 0 ? result.OrderByDescending(e => e.Priority).ToList() : null;
	}
}
