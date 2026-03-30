using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Map screen injector — injects path advice into NMapScreen.
/// Shows: run health, HP advice, deck composition advice, gold advice,
/// boss readiness, meta archetypes.
/// </summary>
public class MapInjector : BaseScreenInjector
{
	public override string ScreenName => "MAP";

	private DeckAnalysis _deckAnalysis;
	private int _currentHP, _maxHP, _gold, _actNumber, _floor;
	private List<(string icon, string text, Color color)> _advice;

	public MapInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor)
	{
		_deckAnalysis = deckAnalysis;
		_currentHP = currentHP;
		_maxHP = maxHP;
		_gold = gold;
		_actNumber = actNumber;
		_floor = floor;

		_advice = Settings.ShowMapAdvice
			? GenerateMapAdvice(deckAnalysis, currentHP, maxHP, gold, actNumber, floor)
			: new List<(string, string, Color)>();

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_advice == null || _advice.Count == 0) return;

		AddSectionHeader("경로 조언");
		foreach (var (icon, text, color) in _advice)
		{
			if (icon == "##")
			{
				AddSubSectionHeader(text, color);
				continue;
			}
			AddAdviceTip(icon, text, color);
		}

		// Stats comparison
		string statsCharacter = _deckAnalysis?.Character;
		if (statsCharacter != null)
		{
			try
			{
				var stats = Plugin.RunDatabase?.GetStatsComparison(statsCharacter);
				if (stats.HasValue && stats.Value.localRuns >= 3)
				{
					var (localWR, localN, commWR, commN) = stats.Value;
					AddSectionHeader("내 통계");
					float delta = localWR - commWR;
					Color deltaColor = delta >= 0 ? SharedResources.ClrPositive : SharedResources.ClrNegative;
					string deltaStr = delta >= 0 ? $"+{delta:F1}%" : $"{delta:F1}%";

					var statsLbl = new Label();
					statsLbl.Text = $"승률: {localWR:F1}% ({localN}회)";
					Res.ApplyFont(statsLbl, Res.FontBody);
					statsLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
					statsLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
					Content.AddChild(statsLbl, forceReadableName: false, Node.InternalMode.Disabled);

					if (commN > 0)
					{
						var commLbl = new Label();
						commLbl.Text = $"커뮤니티: {commWR:F1}% (차이: {deltaStr})";
						Res.ApplyFont(commLbl, Res.FontBody);
						commLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
						commLbl.AddThemeColorOverride("font_color", deltaColor);
						Content.AddChild(commLbl, forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
			}
			catch (Exception ex) { Plugin.Log($"MapInjector stats error: {ex.Message}"); }
		}

		// Deck breakdown
		if (Settings.ShowDeckBreakdown && _deckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			// Simplified inline deck viz
			AddDeckSummary(_deckAnalysis);
		}
	}

	private void AddDeckSummary(DeckAnalysis analysis)
	{
		var summaryLbl = new Label();
		summaryLbl.Text = $"카드 {analysis.TotalCards}장";
		if (analysis.DetectedArchetypes?.Count > 0)
			summaryLbl.Text += $" — {analysis.DetectedArchetypes[0].Archetype.DisplayName} ({analysis.DetectedArchetypes[0].Strength:P0})";
		Res.ApplyFont(summaryLbl, Res.FontBody);
		summaryLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		summaryLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
		Content.AddChild(summaryLbl, forceReadableName: false, Node.InternalMode.Disabled);

		// Type distribution
		if (analysis.TypeCounts != null && analysis.TypeCounts.Count > 0)
		{
			var typeLbl = new Label();
			var parts = analysis.TypeCounts
				.Where(kv => kv.Value > 0)
				.OrderByDescending(kv => kv.Value)
				.Select(kv => $"{kv.Key}: {kv.Value}");
			typeLbl.Text = string.Join("  ", parts);
			Res.ApplyFont(typeLbl, Res.FontBody);
			typeLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			typeLbl.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			Content.AddChild(typeLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private List<(string icon, string text, Color color)> GenerateMapAdvice(DeckAnalysis deck, int hp, int maxHP, int gold, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		bool isDefined = deck != null && !deck.IsUndefined;
		int deckSize = deck?.TotalCards ?? 0;

		// HP-based priorities
		if (hpRatio < 0.4f)
		{
			advice.Add(("\u2764", $"HP 위험 ({hp}/{maxHP}) — 휴식을 우선하세요", SharedResources.ClrNegative));
			advice.Add(("\u26a0", "엘리트를 피하세요 (다른 길이 없다면 제외)", SharedResources.ClrExpensive));
		}
		else if (hpRatio < 0.65f)
		{
			advice.Add(("\u2764", $"HP 보통 ({hp}/{maxHP}) — 휴식이 중요", SharedResources.ClrExpensive));
		}

		// Deck composition priorities
		if (!isDefined && floor <= 6)
			advice.Add(("\u2694", "초반 — 전투와 이벤트로 덱 빌딩", SharedResources.ClrPositive));
		else if (isDefined && deckSize >= 25)
			advice.Add(("\u2702", $"덱 비대 ({deckSize}장) — 상점에서 카드 제거", SharedResources.ClrAqua));
		else if (!isDefined && floor > 6)
			advice.Add(("\u2694", "덱 방향 불명확 — 카드 보상으로 방향 잡기", SharedResources.ClrExpensive));

		// Gold-based
		if (gold >= 300)
			advice.Add(("\u2B50", $"골드: {gold} — 상점 가치 높음", SharedResources.ClrAccent));
		else if (gold >= 150 && deckSize >= 20)
			advice.Add(("\u2B50", $"골드: {gold} — 카드 제거를 위한 상점 이용 고려", SharedResources.ClrSub));

		// Act-based
		if (act >= 2 && hpRatio > 0.7f && isDefined && deckSize < 25)
			advice.Add(("\u2694", "덱 집중 + HP 여유 — 엘리트에서 유물 획득", SharedResources.ClrPositive));

		// Treasure/question mark
		if (floor <= 4)
			advice.Add(("\u2753", "초반 층 — ?방 가치 높음", SharedResources.ClrAqua));

		// Boss readiness
		string character = deck?.Character ?? "unknown";
		var bossResults = BossAdvisor.Diagnose(deck, act, character, hp, maxHP);
		foreach (var boss in bossResults)
		{
			string icon = boss.ReadinessScore >= 70 ? "\u2705" : boss.ReadinessScore >= 45 ? "\u26a0" : "\u274c";
			Color color = boss.ReadinessScore >= 70 ? SharedResources.ClrPositive : boss.ReadinessScore >= 45 ? SharedResources.ClrExpensive : SharedResources.ClrNegative;
			advice.Add(("##", "보스 대비 진단", SharedResources.ClrAccent));
			advice.Add((icon, $"{boss.BossName}: {boss.Verdict} ({boss.ReadinessScore:F0}점)", color));
			foreach (var s in boss.Strengths)
				advice.Add(("\u2714", s, SharedResources.ClrPositive));
			foreach (var w in boss.Weaknesses)
				advice.Add(("\u26a0", w, SharedResources.ClrNegative));
		}

		// Meta Archetype Panel
		try
		{
			string metaPath = System.IO.Path.Combine(Plugin.PluginFolder, "Data", "meta_archetypes.json");
			if (System.IO.File.Exists(metaPath))
			{
				var metaJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<MetaArchetypeEntry>>>(
					System.IO.File.ReadAllText(metaPath));
				string charKey = character?.ToLowerInvariant() ?? "";
				if (metaJson != null && metaJson.TryGetValue(charKey, out var archetypes) && archetypes.Count > 0)
				{
					advice.Add(("##", "메타 아키타입 Top 3", SharedResources.ClrAccent));
					int shown = 0;
					foreach (var arch in archetypes)
					{
						if (shown >= 3) break;
						string coreStr = arch.CoreCards != null && arch.CoreCards.Count > 0
							? string.Join(", ", arch.CoreCards.Take(3)) : "";
						advice.Add(("\u2B50", $"{arch.Archetype}: {arch.WinRate:P0} 승률 ({arch.SampleSize}게임)", SharedResources.ClrPositive));
						if (coreStr.Length > 0)
							advice.Add(("\u2022", $"핵심: {coreStr}", SharedResources.ClrAqua));
						shown++;
					}
				}
			}
		}
		catch (Exception ex) { Plugin.Log($"MapInjector: meta archetype error: {ex.Message}"); }

		// Run Health Gauge
		if (Plugin.RunHealthComputer != null)
		{
			float archStr = deck?.DetectedArchetypes?.Count > 0 ? deck.DetectedArchetypes[0].Strength : 0f;
			int bossReady = 50;
			if (bossResults.Count > 0)
				bossReady = (int)bossResults[0].ReadinessScore;
			int health = Plugin.RunHealthComputer.CalculateHealth(hp, maxHP, gold, deck?.TotalCards ?? 0, floor, archStr, bossReady);
			string healthIcon = health >= 70 ? "\u2705" : health >= 45 ? "\u26a0" : "\u274c";
			Color healthColor = health >= 70 ? SharedResources.ClrPositive : health >= 45 ? SharedResources.ClrExpensive : SharedResources.ClrNegative;
			advice.Insert(0, ("##", "런 건강도", SharedResources.ClrAccent));
			advice.Insert(1, (healthIcon, $"건강도: {health}/100", healthColor));
		}

		if (advice.Count == 0)
			advice.Add(("\u2714", "균형 잡힌 상태 — 덱 강점을 살리세요", SharedResources.ClrCream));

		return advice;
	}
}
