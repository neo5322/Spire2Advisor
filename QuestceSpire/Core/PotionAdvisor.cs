using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using QuestceSpire.Tracking;

namespace QuestceSpire.Core;

/// <summary>
/// Provides potion-related advice: tier ratings, use-now vs save-for-boss recommendations.
/// Uses Spire Codex potion data + historical potion usage patterns.
/// </summary>
public class PotionAdvisor
{
	private readonly RunDatabase _db;
	private Dictionary<string, PotionData> _potionData = new();

	public PotionAdvisor(RunDatabase db, string dataFolder)
	{
		_db = db;
		LoadPotionData(dataFolder);
	}

	private void LoadPotionData(string dataFolder)
	{
		string path = Path.Combine(dataFolder, "codex_potions.json");
		if (!File.Exists(path)) return;

		try
		{
			var list = JsonConvert.DeserializeObject<List<PotionData>>(File.ReadAllText(path));
			if (list == null) return;
			foreach (var p in list)
			{
				if (p.Id != null)
					_potionData[p.Id.ToLowerInvariant()] = p;
			}
			Plugin.Log($"PotionAdvisor: loaded {_potionData.Count} potions from codex.");
		}
		catch (Exception ex)
		{
			Plugin.Log($"PotionAdvisor: failed to load codex data — {ex.Message}");
		}
	}

	/// <summary>
	/// Get advice for a specific potion in the current context.
	/// </summary>
	public PotionAdvice GetAdvice(string potionId, int floor, int actNumber, bool isBossFight, int currentHp, int maxHp)
	{
		var advice = new PotionAdvice { PotionId = potionId };

		// Lookup codex data
		string key = potionId?.ToLowerInvariant() ?? "";
		if (_potionData.TryGetValue(key, out var data))
		{
			advice.PotionName = data.Name ?? potionId;
			advice.Tier = data.Rarity switch
			{
				"rare" => "A",
				"uncommon" => "B",
				_ => "C"
			};
		}
		else
		{
			advice.PotionName = potionId;
			advice.Tier = "C";
		}

		// Historical usage data
		var summary = GetPotionUsageSummary(potionId);
		if (summary != null && summary.TimesObtained >= 3)
		{
			advice.UseRate = summary.UseRate;
			advice.AvgFloorUsed = summary.AvgFloorUsed;
		}

		// Decision: use now or save?
		float hpRatio = maxHp > 0 ? (float)currentHp / maxHp : 1f;
		bool isHealingPotion = key.Contains("heal") || key.Contains("fruit") || key.Contains("regen") || key.Contains("blood");
		bool isOffensivePotion = key.Contains("fire") || key.Contains("explosive") || key.Contains("poison") || key.Contains("strength") || key.Contains("attack");
		bool isDefensivePotion = key.Contains("block") || key.Contains("dexterity") || key.Contains("ghost") || key.Contains("fairy");

		if (isHealingPotion)
		{
			if (hpRatio < 0.4f)
			{
				advice.Recommendation = "지금 사용 — HP가 위험합니다";
				advice.UseNow = true;
			}
			else
			{
				advice.Recommendation = "보스전까지 아끼세요";
				advice.UseNow = false;
			}
		}
		else if (isBossFight)
		{
			advice.Recommendation = "보스전 — 지금 사용하세요!";
			advice.UseNow = true;
		}
		else if (isOffensivePotion && actNumber >= 2)
		{
			// Check if we're near a boss: last 3 floors of each ~17-floor act
			// Boss floors are approximately: Act 1 = 17, Act 2 = 34, Act 3 = 51
			var cfg = ScoringConfig.Instance;
			int floorInAct = ((floor - 1) % cfg.ActLengthFloors) + 1;
			bool nearBoss = floorInAct >= cfg.NearBossFloorThreshold;
			if (nearBoss)
			{
				advice.Recommendation = "보스 임박 — 아끼세요";
				advice.UseNow = false;
			}
			else
			{
				advice.Recommendation = "엘리트전이나 어려운 전투에서 사용";
				advice.UseNow = false;
			}
		}
		else if (isDefensivePotion && hpRatio < 0.5f)
		{
			advice.Recommendation = "지금 사용 — 생존이 우선";
			advice.UseNow = true;
		}
		else
		{
			advice.Recommendation = "보스전에 아끼는 것이 효율적";
			advice.UseNow = false;
		}

		return advice;
	}

	/// <summary>
	/// Centralizes cross-layer dependency on PotionTracker for usage summary access.
	/// </summary>
	private PotionUsageSummary GetPotionUsageSummary(string potionId)
		=> Plugin.PotionTracker?.GetUsageSummary(potionId);

	/// <summary>
	/// Generate potion advice lines for combat overlay.
	/// </summary>
	public List<(string potionId, string text)> GetCombatPotionAdvice(
		List<string> potionIds, int floor, int actNumber, bool isBossFight, int hp, int maxHp)
	{
		var result = new List<(string, string)>();
		if (potionIds == null) return result;

		foreach (string pid in potionIds)
		{
			if (string.IsNullOrEmpty(pid)) continue;
			var advice = GetAdvice(pid, floor, actNumber, isBossFight, hp, maxHp);
			string icon = advice.UseNow ? "\u2714" : "\u23f3";
			result.Add((pid, $"{icon} {advice.PotionName}: {advice.Recommendation}"));
		}

		return result;
	}
}

public class PotionAdvice
{
	public string PotionId { get; set; }
	public string PotionName { get; set; }
	public string Tier { get; set; }
	public string Recommendation { get; set; }
	public bool UseNow { get; set; }
	public float UseRate { get; set; }
	public float AvgFloorUsed { get; set; }
}

public class PotionData
{
	[JsonProperty("id")] public string Id { get; set; }
	[JsonProperty("name")] public string Name { get; set; }
	[JsonProperty("rarity")] public string Rarity { get; set; }
	[JsonProperty("description")] public string Description { get; set; }
}
