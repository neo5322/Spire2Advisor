using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Run summary screen injector — shows post-run analysis with combat stats,
/// card usage, decision review, controversial picks, and global stats.
/// </summary>
public class RunSummaryInjector : BaseScreenInjector
{
	private string _screenName = "RUN WON!";
	public override string ScreenName => _screenName;

	private RunOutcome _outcome;
	private int _finalFloor, _finalAct;
	private string _character;
	private IReadOnlyList<DecisionEvent> _events;
	private RunSummaryData _summaryData;

	// Collapsible section toggles
	private bool _showCombatStats;
	private bool _showCardUsage;
	private bool _showDecisionReview = true;
	private bool _showControversialPicks;
	private bool _showDecisionReplay;
	private bool _showGlobalComparison;

	public RunSummaryInjector(OverlaySettings settings) : base(settings) { }

	public void Show(RunOutcome outcome, int finalFloor, int finalAct)
	{
		_outcome = outcome;
		_finalFloor = finalFloor;
		_finalAct = finalAct;
		_screenName = outcome == RunOutcome.Win ? "RUN WON!" : "RUN LOST";

		_events = Plugin.RunTracker?.GetCurrentRunEvents();
		_character = Plugin.RunTracker?.CurrentCharacter ?? "unknown";
		string runId = Plugin.RunTracker?.CurrentRunId;

		_summaryData = null;
		if (!string.IsNullOrEmpty(runId) && Plugin.RunDatabase != null)
		{
			try
			{
				var runSummary = new RunSummary(Plugin.RunDatabase);
				_summaryData = runSummary.Generate(runId, _character, outcome.ToString());
			}
			catch (Exception ex) { Plugin.Log($"RunSummary generation failed: {ex.Message}"); }
		}

		// No game node for run summary — inject into coordinator utility layer
		var utilityLayer = Plugin.Coordinator?.UtilityLayer;
		if (utilityLayer != null)
			Inject(utilityLayer);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_events == null || _events.Count == 0) return;

		Color outcomeColor = _outcome == RunOutcome.Win ? SharedResources.ClrPositive : SharedResources.ClrNegative;
		AddSectionHeader($"RUN SUMMARY \u2014 {_outcome.ToString().ToUpper()} (Floor {_finalFloor})");

		// Combat Efficiency
		if (_summaryData != null && _summaryData.TotalTurns > 0)
		{
			var section = AddCollapsibleSection("전투 효율", "runCombatStats", ref _showCombatStats);
			if (section != null)
			{
				float dpt = (float)_summaryData.TotalDamageDealt / _summaryData.TotalTurns;
				float dtpt = (float)_summaryData.TotalDamageTaken / _summaryData.TotalTurns;
				float bpt = (float)_summaryData.TotalBlockGenerated / _summaryData.TotalTurns;

				AddStatRowTo(section, "총 딜량", $"{_summaryData.TotalDamageDealt}", SharedResources.ClrPositive);
				AddStatRowTo(section, "턴당 딜", $"{dpt:F1}", dpt >= 15 ? SharedResources.ClrPositive : SharedResources.ClrSub);
				AddStatRowTo(section, "받은 피해", $"{_summaryData.TotalDamageTaken}", dtpt > 10 ? SharedResources.ClrNegative : SharedResources.ClrSub);
				AddStatRowTo(section, "생성 블록", $"{_summaryData.TotalBlockGenerated}", bpt >= 8 ? SharedResources.ClrPositive : SharedResources.ClrSub);
				AddStatRowTo(section, "총 턴 수", $"{_summaryData.TotalTurns}", SharedResources.ClrSub);
			}
		}

		// Most/Least Played Cards
		if (_summaryData?.MostPlayedCards?.Count > 0 || _summaryData?.LeastPlayedCards?.Count > 0)
		{
			var section = AddCollapsibleSection("카드 사용 통계", "runCardUsage", ref _showCardUsage);
			if (section != null)
			{
				if (_summaryData?.MostPlayedCards?.Count > 0)
				{
					var header = new Label();
					header.Text = "가장 많이 사용한 카드";
					Res.ApplyFont(header, Res.FontBold);
					header.AddThemeColorOverride("font_color", SharedResources.ClrCream);
					header.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
					section.AddChild(header, forceReadableName: false, Node.InternalMode.Disabled);
					foreach (var card in _summaryData.MostPlayedCards)
						AddStatRowTo(section, PrettifyId(card.CardId), $"{card.PlayCount}회", SharedResources.ClrAccent);
				}
				if (_summaryData?.LeastPlayedCards?.Count > 0)
				{
					var header = new Label();
					header.Text = "거의 사용하지 않은 카드";
					Res.ApplyFont(header, Res.FontBold);
					header.AddThemeColorOverride("font_color", SharedResources.ClrCream);
					header.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
					section.AddChild(header, forceReadableName: false, Node.InternalMode.Disabled);
					foreach (var card in _summaryData.LeastPlayedCards)
						AddStatRowTo(section, PrettifyId(card.CardId), $"{card.PlayCount}회", SharedResources.ClrNegative);
				}
			}
		}

		// Decision Review (Community Comparison)
		if (_summaryData?.Decisions?.Count > 0)
		{
			var reviewed = _summaryData.Decisions.Where(d => d.Feedback != null).ToList();
			if (reviewed.Count > 0)
			{
				var section = AddCollapsibleSection("커뮤니티 데이터 대비 결정 분석", "runDecisionReview", ref _showDecisionReview);
				if (section != null)
				{
					AddStatRowTo(section, "좋은 선택", $"{_summaryData.GoodDecisions}", SharedResources.ClrPositive);
					AddStatRowTo(section, "아쉬운 선택", $"{_summaryData.BadDecisions}", _summaryData.BadDecisions > 0 ? SharedResources.ClrNegative : SharedResources.ClrSub);

					foreach (var d in reviewed.Where(d => !d.WasBetterChoice.GetValueOrDefault(true)).Take(5))
					{
						var dPanel = new PanelContainer();
						dPanel.AddThemeStyleboxOverride("panel", OverlayStyles.CreateDecisionEntryStyle(SharedResources.ClrNegative));
						var dLbl = new Label();
						dLbl.Text = $"F{d.Floor} [{d.EventType}]: {d.Feedback}";
						Res.ApplyFont(dLbl, Res.FontBody);
						dLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
						dLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
						dLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
						dPanel.AddChild(dLbl, forceReadableName: false, Node.InternalMode.Disabled);
						section.AddChild(dPanel, forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
			}
		}

		// Controversial Picks
		BuildControversialPicks(outcomeColor);

		// Gold
		if (_summaryData != null && _summaryData.PeakGold > 0)
			AddStatRow("최고 골드", $"{_summaryData.PeakGold}G", SharedResources.ClrExpensive);

		// Decision Replay
		{
			var section = AddCollapsibleSection("결정 리플레이", "runDecisionReplay", ref _showDecisionReplay);
			if (section != null)
				BuildDecisionReplay(section);
		}

		// Global Stats Comparison
		{
			var section = AddCollapsibleSection("글로벌 통계 비교", "runGlobalComparison", ref _showGlobalComparison);
			if (section != null)
				BuildGlobalComparison(section);
		}

		// Stats line
		int controversialCount = CountControversial();
		var statsLbl = new Label();
		statsLbl.Text = $"Decisions: {_events.Count} | Controversial: {controversialCount}";
		Res.ApplyFont(statsLbl, Res.FontBold);
		statsLbl.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		statsLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		Content.AddChild(statsLbl, forceReadableName: false, Node.InternalMode.Disabled);
	}

	// === Collapsible section ===

	private VBoxContainer AddCollapsibleSection(string text, string sectionKey, ref bool isExpanded)
	{
		if (Content.GetChildCount() > 0)
		{
			var sep = new HSeparator();
			sep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(SharedResources.ClrBorder, 0.4f), Thickness = 1 });
			Content.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
		}

		var headerRow = new HBoxContainer();
		headerRow.MouseFilter = Control.MouseFilterEnum.Stop;
		headerRow.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var arrow = new Label();
		arrow.Text = isExpanded ? "\u25BC " : "\u25B6 ";
		Res.ApplyFont(arrow, Res.FontBold);
		arrow.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		arrow.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		arrow.MouseFilter = Control.MouseFilterEnum.Ignore;
		headerRow.AddChild(arrow, forceReadableName: false, Node.InternalMode.Disabled);

		var headerLabel = new Label();
		headerLabel.Text = text;
		Res.ApplyFont(headerLabel, Res.FontBold);
		headerLabel.AddThemeColorOverride("font_color", SharedResources.ClrAccent);
		headerLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		headerLabel.AddThemeConstantOverride("outline_size", 3);
		headerLabel.AddThemeColorOverride("font_outline_color", SharedResources.ClrOutline);
		headerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		headerRow.AddChild(headerLabel, forceReadableName: false, Node.InternalMode.Disabled);

		Content.AddChild(headerRow, forceReadableName: false, Node.InternalMode.Disabled);

		string localKey = sectionKey;
		if (!isExpanded)
		{
			headerRow.Connect("gui_input", Callable.From((InputEvent ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					SetSectionState(localKey, true);
					Rebuild();
				}
			}));
			return null;
		}

		var sectionContent = new VBoxContainer();
		sectionContent.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
		Content.AddChild(sectionContent, forceReadableName: false, Node.InternalMode.Disabled);

		headerRow.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				SetSectionState(localKey, false);
				Rebuild();
			}
		}));
		return sectionContent;
	}

	private void SetSectionState(string key, bool value)
	{
		switch (key)
		{
			case "runCombatStats": _showCombatStats = value; break;
			case "runCardUsage": _showCardUsage = value; break;
			case "runDecisionReview": _showDecisionReview = value; break;
			case "runControversialPicks": _showControversialPicks = value; break;
			case "runDecisionReplay": _showDecisionReplay = value; break;
			case "runGlobalComparison": _showGlobalComparison = value; break;
		}
	}

	// === UI helpers ===

	private void AddStatRow(string label, string value, Color valueColor)
	{
		AddStatRowTo(Content, label, value, valueColor);
	}

	private void AddStatRowTo(VBoxContainer target, string label, string value, Color valueColor)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
		var lbl = new Label();
		lbl.Text = label;
		Res.ApplyFont(lbl, Res.FontBody);
		lbl.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
		var val = new Label();
		val.Text = value;
		Res.ApplyFont(val, Res.FontBold);
		val.AddThemeColorOverride("font_color", valueColor);
		val.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		row.AddChild(val, forceReadableName: false, Node.InternalMode.Disabled);
		target.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
	}

	// === Controversial picks ===

	private int CountControversial()
	{
		int count = 0;
		int nullChoices = _events.Count(e => e.ChosenId == null && e.OfferedIds?.Count > 0);
		bool choiceTrackingBroken = nullChoices > _events.Count * 0.7f;
		foreach (var evt in _events)
		{
			if (evt.OfferedIds == null || evt.OfferedIds.Count == 0) continue;
			TierGrade bestGrade = TierGrade.F;
			foreach (string id in evt.OfferedIds)
			{
				TierGrade g = LookupGrade(id);
				if (g > bestGrade) bestGrade = g;
			}
			if (evt.ChosenId == null) { if (!choiceTrackingBroken && bestGrade >= TierGrade.A) count++; }
			else { TierGrade chosenGrade = LookupGrade(evt.ChosenId); if ((int)bestGrade - (int)chosenGrade >= 2) count++; }
		}
		return count;
	}

	private void BuildControversialPicks(Color outcomeColor)
	{
		var controversial = new List<(DecisionEvent evt, TierGrade chosenGrade, TierGrade bestGrade)>();
		int nullChoices = _events.Count(e => e.ChosenId == null && e.OfferedIds?.Count > 0);
		bool choiceTrackingBroken = nullChoices > _events.Count * 0.7f;
		foreach (var evt in _events)
		{
			if (evt.OfferedIds == null || evt.OfferedIds.Count == 0) continue;
			TierGrade bestGrade = TierGrade.F;
			foreach (string id in evt.OfferedIds) { TierGrade g = LookupGrade(id); if (g > bestGrade) bestGrade = g; }
			if (evt.ChosenId == null) { if (!choiceTrackingBroken && bestGrade >= TierGrade.A) controversial.Add((evt, TierGrade.F, bestGrade)); }
			else { TierGrade chosenGrade = LookupGrade(evt.ChosenId); if ((int)bestGrade - (int)chosenGrade >= 2) controversial.Add((evt, chosenGrade, bestGrade)); }
		}

		var section = AddCollapsibleSection("논란 선택", "runControversialPicks", ref _showControversialPicks);
		if (section == null) return;

		if (controversial.Count > 0)
		{
			foreach (var (evt, chosenGrade, bestGrade) in controversial.Take(8))
			{
				string chosen = evt.ChosenId != null ? PrettifyId(evt.ChosenId) : "스킵";
				string bestId = evt.OfferedIds?.OrderByDescending(id => (int)LookupGrade(id)).FirstOrDefault();
				string bestName = bestId != null ? PrettifyId(bestId) : "?";
				int gap = (int)bestGrade - (int)chosenGrade;
				var cPanel = new PanelContainer();
				cPanel.AddThemeStyleboxOverride("panel", OverlayStyles.CreateDecisionEntryStyle(outcomeColor));
				var cLbl = new Label();
				cLbl.Text = $"F{evt.Floor}: {chosen} [{chosenGrade}] \u2014 Best: {bestName} [{bestGrade}] ({gap} grades)";
				Res.ApplyFont(cLbl, Res.FontBody);
				cLbl.AddThemeColorOverride("font_color", outcomeColor);
				cLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
				cLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				cPanel.AddChild(cLbl, forceReadableName: false, Node.InternalMode.Disabled);
				section.AddChild(cPanel, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		else
		{
			var noContr = new Label();
			noContr.Text = "논란 선택 없음 \u2014 좋은 판단!";
			Res.ApplyFont(noContr, Res.FontBody);
			noContr.AddThemeColorOverride("font_color", SharedResources.ClrPositive);
			noContr.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
			section.AddChild(noContr, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	// === Decision replay ===

	private void BuildDecisionReplay(VBoxContainer target)
	{
		var meaningful = _events.Where(e => e.OfferedIds?.Count > 0).ToList();
		if (meaningful.Count == 0) return;

		var scroll = new ScrollContainer();
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.MouseFilter = Control.MouseFilterEnum.Stop;
		float maxH = Math.Min(meaningful.Count * 60f, 300f);
		scroll.CustomMinimumSize = new Vector2(0, maxH);
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill;

		var scrollContent = new VBoxContainer();
		scrollContent.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
		scrollContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(scrollContent, forceReadableName: false, Node.InternalMode.Disabled);

		foreach (var evt in meaningful)
		{
			var entry = new PanelContainer();
			Color borderClr = SharedResources.ClrSub;
			if (evt.ChosenId != null)
			{
				TierGrade chosen = LookupGrade(evt.ChosenId);
				TierGrade best = evt.OfferedIds.Max(id => LookupGrade(id));
				int gap = (int)best - (int)chosen;
				borderClr = gap == 0 ? SharedResources.ClrPositive : gap == 1 ? SharedResources.ClrAccent : SharedResources.ClrNegative;
			}
			entry.AddThemeStyleboxOverride("panel", OverlayStyles.CreateDecisionEntryStyle(borderClr));

			var vbox = new VBoxContainer();
			vbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
			entry.AddChild(vbox, forceReadableName: false, Node.InternalMode.Disabled);

			string typeIcon = evt.EventType switch
			{
				DecisionEventType.CardReward => "\u2694",
				DecisionEventType.RelicReward or DecisionEventType.BossRelic => "\u2B50",
				DecisionEventType.Shop => "\uD83D\uDCB0",
				DecisionEventType.CardRemove => "\u2702",
				_ => "\u2022"
			};
			string chosenName = evt.ChosenId != null ? PrettifyId(evt.ChosenId) : "스킵";
			TierGrade chosenGrade = evt.ChosenId != null ? LookupGrade(evt.ChosenId) : TierGrade.F;

			var mainLine = new Label();
			mainLine.Text = $"{typeIcon} F{evt.Floor}: {chosenName} [{chosenGrade}]";
			Res.ApplyFont(mainLine, Res.FontBold);
			mainLine.AddThemeColorOverride("font_color", borderClr);
			mainLine.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
			mainLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			vbox.AddChild(mainLine, forceReadableName: false, Node.InternalMode.Disabled);

			var parts = new List<string>();
			foreach (string id in evt.OfferedIds)
			{
				TierGrade g = LookupGrade(id);
				string name = PrettifyId(id);
				string marker = id == evt.ChosenId ? "\u25B6" : " ";
				string wrStr = "";
				try
				{
					var stats = Plugin.RunDatabase?.GetCommunityCardStats(_character, id);
					if (stats != null && stats.SampleSize >= 5)
						wrStr = $" {stats.WinRateWhenPicked:P0}";
				}
				catch (Exception ex) { Plugin.Log($"Decision replay win rate lookup failed: {ex.Message}"); }
				parts.Add($"{marker}{name}[{g}]{wrStr}");
			}
			var offeredLine = new Label();
			offeredLine.Text = string.Join("  ", parts);
			Res.ApplyFont(offeredLine, Res.FontBody);
			offeredLine.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			offeredLine.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			offeredLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			vbox.AddChild(offeredLine, forceReadableName: false, Node.InternalMode.Disabled);

			scrollContent.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
		}

		target.AddChild(scroll, forceReadableName: false, Node.InternalMode.Disabled);
	}

	// === Global stats comparison ===

	private void BuildGlobalComparison(VBoxContainer target)
	{
		if (Plugin.SteamLeaderboardSync?.Cache == null) return;
		var cache = Plugin.SteamLeaderboardSync.Cache;
		if (cache.Leaderboards == null || cache.Leaderboards.Count == 0) return;
		if (Plugin.RunDatabase == null || string.IsNullOrEmpty(_character)) return;

		try
		{
			var (wins, total) = Plugin.RunDatabase.GetCharacterWinRate(_character);
			if (total == 0) return;
			float playerWinRate = (float)wins / total;
			string charName = char.ToUpper(_character[0]) + _character.Substring(1);
			AddStatRowTo(target, $"{charName} 승률", $"{playerWinRate:P0} ({wins}/{total})",
				playerWinRate >= 0.5f ? SharedResources.ClrPositive : playerWinRate >= 0.3f ? SharedResources.ClrAccent : SharedResources.ClrNegative);

			int totalPlayers = 0;
			foreach (var board in cache.Leaderboards)
				if (board.EntryCount > totalPlayers) totalPlayers = board.EntryCount;
			if (totalPlayers > 0)
				AddStatRowTo(target, "글로벌 참가자", $"{totalPlayers:N0}명", SharedResources.ClrSub);

			int shown = 0;
			foreach (var board in cache.Leaderboards)
			{
				if (shown >= 5) break;
				if (board.EntryCount < 10) continue;
				AddStatRowTo(target, board.Name, $"{board.EntryCount:N0}명", SharedResources.ClrSub);
				shown++;
			}

			var orchestrator = Plugin.PipelineOrchestrator;
			if (orchestrator != null)
			{
				var history = orchestrator.GetRunHistory();
				if (history.Count > 0)
				{
					int success = 0, failed = 0;
					long totalMs = 0;
					foreach (var info in history)
					{
						if (info.Status == PipelineStatus.Success) success++;
						else if (info.Status == PipelineStatus.Failed) failed++;
						totalMs += info.DurationMs;
					}
					AddStatRowTo(target, "파이프라인", $"{success}/{history.Count} 성공 ({totalMs}ms)",
						failed == 0 ? SharedResources.ClrPositive : SharedResources.ClrNegative);
				}
			}

			if (!string.IsNullOrEmpty(cache.LastUpdated))
			{
				if (DateTime.TryParse(cache.LastUpdated, out var lastUpdate))
				{
					var age = DateTime.UtcNow - lastUpdate;
					string ageStr = age.TotalHours < 1 ? $"{age.TotalMinutes:F0}분 전" :
						age.TotalDays < 1 ? $"{age.TotalHours:F0}시간 전" : $"{age.TotalDays:F0}일 전";
					AddStatRowTo(target, "마지막 업데이트", ageStr, SharedResources.ClrSub);
				}
			}
		}
		catch (Exception ex) { Plugin.Log($"BuildGlobalComparison error: {ex.Message}"); }
	}

	// === Utility ===

	private TierGrade LookupGrade(string id)
	{
		var cardTier = Plugin.TierEngine?.GetCardTier(_character, id);
		if (cardTier != null) return TierEngine.ParseGrade(cardTier.BaseTier);
		var relicTier = Plugin.TierEngine?.GetRelicTier(_character, id);
		if (relicTier != null) return TierEngine.ParseGrade(relicTier.BaseTier);
		return TierGrade.C;
	}

	private static string PrettifyId(string id)
	{
		if (string.IsNullOrEmpty(id)) return id;
		string localized = GameStateReader.GetLocalizedName("card", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("relic", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("enemy", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("event", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		var cardTier = Plugin.TierEngine?.GetCardTier(null, id);
		if (cardTier != null && !string.IsNullOrEmpty(cardTier.Id)) return cardTier.Id;
		var relicTier = Plugin.TierEngine?.GetRelicTier(null, id);
		if (relicTier != null && !string.IsNullOrEmpty(relicTier.Id)) return relicTier.Id;
		return string.Join(" ", id.Split('_').Select(w =>
			w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1).ToLower() : w));
	}
}
