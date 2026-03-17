using System.Collections.Generic;
using System.Linq;
using QuestceSpire.GameBridge;

namespace QuestceSpire.Core;

/// <summary>
/// Analyzes deck readiness for known bosses per act.
/// STS2 bosses (Early Access): Act 1, Act 2, Act 3 + Heart.
/// </summary>
public static class BossAdvisor
{
	public class BossCheckResult
	{
		public string BossName { get; set; } = "";
		public string Verdict { get; set; } = ""; // 준비 완료 / 주의 / 위험
		public List<string> Strengths { get; set; } = new();
		public List<string> Weaknesses { get; set; } = new();
		public float ReadinessScore { get; set; } // 0-100
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
		return act switch
		{
			1 => new List<BossTemplate>
			{
				new() { Name = "Act 1 보스", 
					NeedsAoe = false, NeedsScaling = false, NeedsBlock = true,
					DangerThreshold = 0.4f,
					Tips = new() { "초반 보스는 기본 덱으로도 가능", "블록 카드가 충분하면 안전" } },
			},
			2 => new List<BossTemplate>
			{
				new() { Name = "Act 2 보스",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					DangerThreshold = 0.55f,
					Tips = new() { "AOE 딜이 중요", "스케일링 카드 1-2장 필수", "덱이 너무 두꺼우면 위험" } },
			},
			3 => new List<BossTemplate>
			{
				new() { Name = "Act 3 보스",
					NeedsAoe = true, NeedsScaling = true, NeedsBlock = true,
					NeedsFrontloadDamage = true,
					DangerThreshold = 0.7f,
					Tips = new() { "강력한 스케일링 필수", "첫 턴 폭딜 가능해야", "상태 이상 대처 필요" } },
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
		score = System.Math.Clamp(score, 0, 100);
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
