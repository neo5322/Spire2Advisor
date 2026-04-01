using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

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

		// Build completion from community data
		BuildCompletionSection(_deckAnalysis);

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

		// Deck breakdown (expanded only)
		if (IsExpanded && Settings.ShowDeckBreakdown && _deckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			AddDeckSummary(_deckAnalysis);
		}

		// HP bar at bottom
		if (_maxHP > 0)
		{
			Content.AddChild(CreateHpBar(_currentHP, _maxHP), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private void BuildCompletionSection(DeckAnalysis analysis)
	{
		try
		{
			if (analysis == null || Plugin.CommunityData == null || !Plugin.CommunityData.IsLoaded)
				return;

			// Get deck Korean names for community lookup
			var deckKoreanNames = new List<string>();
			var gs = GameStateReader.ReadCurrentState();
			if (gs?.DeckCards != null)
			{
				foreach (var card in gs.DeckCards)
				{
					var koreanName = GameStateReader.GetLocalizedName("card", card.Id);
					if (koreanName != null) deckKoreanNames.Add(koreanName);
				}
			}

			if (deckKoreanNames.Count == 0) return;

			var completion = Plugin.CommunityData.GetBuildCompletion(
				analysis.Character, null, deckKoreanNames);
			if (completion == null) return;
			float pct = completion.Percentage;
			int level = completion.Level;
			if (pct <= 0) return;

			AddSectionHeader("빌드 진행");

			// Build name + percentage
			string archName = completion.ArchetypeName ?? "빌드";
			var headerLbl = new Label();
			headerLbl.Text = $"{archName} {pct:F0}%";
			OverlayStyles.StyleLabel(headerLbl, Res.FontBold, OverlayTheme.FontH2,
				level >= 4 ? OverlayTheme.Positive : OverlayTheme.TextAccent);
			Content.AddChild(headerLbl, forceReadableName: false, Node.InternalMode.Disabled);

			// Progress bar
			var barBg = new PanelContainer();
			var barBgStyle = new StyleBoxFlat();
			barBgStyle.BgColor = OverlayTheme.BgScoreBarEmpty;
			OverlayStyles.SetAllCornerRadius(barBgStyle, 3);
			barBg.AddThemeStyleboxOverride("panel", barBgStyle);
			barBg.CustomMinimumSize = new Vector2(0, 8);

			var barFill = new ColorRect();
			float fillRatio = Math.Clamp(pct / 100f, 0f, 1f);
			Color barColor = level switch
			{
				1 => OverlayTheme.ScoreBarC,
				2 => OverlayTheme.ScoreBarB,
				3 => OverlayTheme.ScoreBarA,
				4 => OverlayTheme.ScoreBarS,
				5 => new Color(1f, 0.84f, 0f),
				_ => OverlayTheme.ScoreBarC
			};
			barFill.Color = barColor;
			barFill.CustomMinimumSize = new Vector2(CurrentPanelWidth * fillRatio * 0.75f, 8);
			barBg.AddChild(barFill, forceReadableName: false, Node.InternalMode.Disabled);
			Content.AddChild(barBg, forceReadableName: false, Node.InternalMode.Disabled);

			// Missing must cards — from DeckAnalysis (populated by scoring agent)
			var missingMust = analysis.MissingMustCards;
			if (missingMust?.Count > 0)
			{
				var mustLbl = new Label();
				mustLbl.Text = $"필수: {string.Join(", ", missingMust)}";
				OverlayStyles.StyleLabel(mustLbl, Res.FontBody, OverlayTheme.FontCaption, OverlayTheme.Negative);
				mustLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				Content.AddChild(mustLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}

			// Missing rec cards — from DeckAnalysis (populated by scoring agent)
			var missingRec = analysis.MissingRecCards;
			if (missingRec?.Count > 0)
			{
				var recLbl = new Label();
				recLbl.Text = $"권장: {string.Join(", ", missingRec)}";
				OverlayStyles.StyleLabel(recLbl, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrSub);
				recLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				Content.AddChild(recLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"MapInjector.BuildCompletionSection error: {ex.Message}");
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
		if (analysis.AttackCount + analysis.SkillCount + analysis.PowerCount > 0)
		{
			var typeLbl = new Label();
			var parts = new List<string>();
			if (analysis.AttackCount > 0) parts.Add($"Attack: {analysis.AttackCount}");
			if (analysis.SkillCount > 0) parts.Add($"Skill: {analysis.SkillCount}");
			if (analysis.PowerCount > 0) parts.Add($"Power: {analysis.PowerCount}");
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
