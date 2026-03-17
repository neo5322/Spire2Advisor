using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

/// <summary>
/// Analyzes deck readiness for known bosses per act.
/// STS2 bosses (Early Access): Act 1, Act 2, Act 3 + Heart.
/// </summary>
public static class BossAdvisor
{
	private static Dictionary<string, List<BossTemplate>> _bossesByAct;

	public class BossCheckResult
	{
		public string BossName { get; set; } = "";
		public string Verdict { get; set; } = ""; // 준비 완료 / 주의 / 위험
		public List<string> Strengths { get; set; } = new();
		public List<string> Weaknesses { get; set; } = new();
		public float ReadinessScore { get; set; } // 0-100
	}

	/// <summary>
	/// Load boss templates from JSON file. Call once at init.
	/// </summary>
	public static void LoadFromJson(string jsonPath)
	{
		if (!File.Exists(jsonPath))
		{
			Plugin.Log("BossAdvisor: bosses.json not found, using hardcoded defaults.");
			_bossesByAct = null;
			return;
		}
		try
		{
			string json = File.ReadAllText(jsonPath);
			var loaded = JsonConvert.DeserializeObject<Dictionary<string, List<BossTemplate>>>(json);
			if (loaded != null && loaded.Count > 0)
			{
				_bossesByAct = loaded;
				int total = loaded.Values.Sum(v => v.Count);
				Plugin.Log($"BossAdvisor loaded {total} boss templates from JSON.");
			}
			else
			{
				Plugin.Log("BossAdvisor: JSON was empty, using hardcoded defaults.");
				_bossesByAct = null;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"BossAdvisor: failed to load JSON ({ex.Message}), using hardcoded defaults.");
			_bossesByAct = null;
		}
	}

	/// <summary>
	/// Diagnose deck readiness for the upcoming boss fight.
	/// </summary>
	public static List<BossCheckResult> Diagnose(DeckAnalysis deck, int actNumber, string character, int currentHP, int maxHP)
	{
		var results = new List<BossCheckResult>();
		if (deck == null) return results;

		var bosses = GetBossesForAct(actNumber);
		foreach (var boss in bosses)
		{
			var result = AnalyzeBoss(boss, deck, character, currentHP, maxHP);
			results.Add(result);
		}

		return results;
	}

	private static List<BossTemplate> GetBossesForAct(int act)
	{
		// Try JSON-loaded data first
		if (_bossesByAct != null && _bossesByAct.TryGetValue(act.ToString(), out var loaded))
			return loaded;

		// Fallback to hardcoded defaults
		return act switch
		{
			1 => new List<BossTemplate>
			{
				new() { Name = "Act 1 보스 — 물량형",
					NeedsAoe = false, NeedsScaling = false, NeedsBlock = true,
					DangerThreshold = 0.4f,
					Tips = new() { "초반 보스는 기본 덱으로도 가능", "블록 카드가 충분하면 안전" } },
				new() { Name = "Act 1 보스 — 고딜형",
					NeedsAoe = false, NeedsScaling = false, NeedsBlock = true,
					NeedsFrontloadDamage = true, DangerThreshold = 0.45f,
					Tips = new() { "높은 단일 피해에 대비", "블록 + 선딜 모두 필요" } },
				new() { Name = "Act 1 보스 — 소환형",
					NeedsAoe = true, NeedsScaling = false, NeedsBlock = true,
					DangerThreshold = 0.4f,
					Tips = new() { "소환수를 빠르게 처리", "AOE 카드가 있으면 유리" } },
			},
			2 => new List<BossTemplate>
			{
				new() { Name = "Act 2 보스 — 스케일링형",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					DangerThreshold = 0.55f,
					Tips = new() { "AOE 딜이 중요", "스케일링 카드 1-2장 필수", "덱이 너무 두꺼우면 위험" } },
				new() { Name = "Act 2 보스 — 디버프형",
					NeedsAoe = false, NeedsScaling = true, NeedsBlock = true,
					NeedsFrontloadDamage = true, DangerThreshold = 0.55f,
					Tips = new() { "디버프 대비 필요", "빠르게 딜을 넣어야 함", "블록 카드가 충분하면 안전" } },
				new() { Name = "Act 2 보스 — 물량형",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					DangerThreshold = 0.6f,
					Tips = new() { "다수의 적 등장", "AOE + 스케일링 필수", "파워 카드 세팅이 중요" } },
			},
			3 => new List<BossTemplate>
			{
				new() { Name = "Act 3 보스 — 최종 스케일링",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					NeedsFrontloadDamage = true, DangerThreshold = 0.7f,
					Tips = new() { "강력한 스케일링 필수", "첫 턴 폭딜 가능해야", "상태 이상 대처 필요" } },
				new() { Name = "Act 3 보스 — 지구력전",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					DangerThreshold = 0.65f,
					Tips = new() { "장기전 대비 필요", "블록 + 스케일링 중심 덱이 유리", "소멸 카드로 덱 압축 권장" } },
				new() { Name = "Act 3 보스 — 패턴형",
					NeedsAoe = false, NeedsScaling = true, NeedsBlock = true,
					NeedsFrontloadDamage = true, DangerThreshold = 0.7f,
					Tips = new() { "패턴 파악이 중요", "선딜 + 블록 밸런스", "파워 카드 빨리 깔아야" } },
			},
			_ => new List<BossTemplate>()
		};
	}

	private static BossCheckResult AnalyzeBoss(BossTemplate boss, DeckAnalysis deck, string character, int hp, int maxHP)
	{
		var result = new BossCheckResult { BossName = boss.Name };
		float score = 50f;

		int deckSize = deck.TotalCards;
		int attacks = deck.AttackCount;
		int skills = deck.SkillCount;
		int powers = deck.PowerCount;
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 0.5f;

		// ─── Scaling check ───
		if (boss.NeedsScaling)
		{
			if (powers >= 2) { score += 15; result.Strengths.Add("파워 카드 충분"); }
			else if (powers >= 1) { score += 5; }
			else { score -= 15; result.Weaknesses.Add("스케일링 카드 부족"); }
		}

		// ─── AOE check (via archetype) ───
		if (boss.NeedsAoe)
		{
			bool hasAoe = deck.DetectedArchetypes?.Any(a =>
				a.Archetype.Id.Contains("aoe") || a.Archetype.Id.Contains("multi")) ?? false;
			if (hasAoe) { score += 10; result.Strengths.Add("AOE 딜 보유"); }
			else if (attacks >= 5) { score += 5; }
			else { score -= 10; result.Weaknesses.Add("AOE 부족"); }
		}

		// ─── Block check ───
		if (boss.NeedsBlock)
		{
			if (skills >= 4) { score += 10; result.Strengths.Add("블록 카드 충분"); }
			else if (skills >= 2) { score += 5; }
			else { score -= 10; result.Weaknesses.Add("블록 카드 부족"); }
		}

		// ─── Frontload damage check ───
		if (boss.NeedsFrontloadDamage)
		{
			if (attacks >= 5 && deckSize <= 25) { score += 10; result.Strengths.Add("초반 딜 가능"); }
			else { score -= 5; result.Weaknesses.Add("첫 턴 딜이 약할 수 있음"); }
		}

		// ─── Deck size penalty ───
		if (deckSize > 30) { score -= 10; result.Weaknesses.Add($"덱이 비대 ({deckSize}장)"); }
		else if (deckSize <= 20) { score += 5; result.Strengths.Add("덱이 얇고 효율적"); }

		// ─── HP check ───
		if (hpRatio < 0.3f) { score -= 15; result.Weaknesses.Add($"HP 위험 ({hp}/{maxHP})"); }
		else if (hpRatio >= 0.7f) { score += 5; result.Strengths.Add("HP 여유"); }

		// Clamp
		score = Math.Clamp(score, 0, 100);
		result.ReadinessScore = score;

		if (score >= 70) result.Verdict = "준비 완료";
		else if (score >= 45) result.Verdict = "주의";
		else result.Verdict = "위험";

		return result;
	}

	private class BossTemplate
	{
		public string Name { get; set; } = "";
		public bool NeedsAoe { get; set; }
		public bool NeedsScaling { get; set; }
		public bool NeedsBlock { get; set; }
		public bool NeedsFrontloadDamage { get; set; }
		public float DangerThreshold { get; set; }
		public List<string> Tips { get; set; } = new();
	}
}
