using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

public partial class OverlayManager
{
	// === Feature 1: Decision history ===

	public void ToggleHistory()
	{
		_showHistory = !_showHistory;
		_settings.ShowDecisionHistory = _showHistory;
		_settings.Save();
		Rebuild();
		Plugin.Log("Decision history " + (_showHistory ? "shown" : "hidden"));
	}

	private TierGrade LookupGrade(string id, string character)
	{
		var cardTier = Plugin.TierEngine?.GetCardTier(character, id);
		if (cardTier != null) return TierEngine.ParseGrade(cardTier.BaseTier);
		var relicTier = Plugin.TierEngine?.GetRelicTier(character, id);
		if (relicTier != null) return TierEngine.ParseGrade(relicTier.BaseTier);
		return TierGrade.C;
	}

	/// <summary>Convert UPPER_SNAKE_CASE game IDs to readable Title Case names.</summary>
	private static string PrettifyId(string id)
	{
		if (string.IsNullOrEmpty(id)) return id;
		// 1. Check runtime localized name cache (Korean etc.)
		string localized = GameStateReader.GetLocalizedName("card", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("relic", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("enemy", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		localized = GameStateReader.GetLocalizedName("event", id);
		if (!string.IsNullOrEmpty(localized)) return localized;
		// 2. Try tier data (English ID names)
		var cardTier = Plugin.TierEngine?.GetCardTier(null, id);
		if (cardTier != null && !string.IsNullOrEmpty(cardTier.Id)) return cardTier.Id;
		var relicTier = Plugin.TierEngine?.GetRelicTier(null, id);
		if (relicTier != null && !string.IsNullOrEmpty(relicTier.Id)) return relicTier.Id;
		// 3. Fallback: UPPER_SNAKE_CASE → Title Case
		return string.Join(" ", id.Split('_').Select(w =>
			w.Length > 0 ? char.ToUpper(w[0]) + w.Substring(1).ToLower() : w));
	}

	private void AddDecisionHistory()
	{
		var events = Plugin.RunTracker?.GetCurrentRunEvents();
		if (events == null || events.Count == 0) return;
		AddSectionHeader("결정 기록");

		// Wrap in a scroll container so long logs don't stretch the panel
		ScrollContainer scroll = new ScrollContainer();
		scroll.CustomMinimumSize = new Vector2(0, 0);
		// Cap height so it doesn't push the panel too tall
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill;
		float maxScrollHeight = 250f;
		var viewport = _panel?.GetViewportRect().Size ?? new Vector2(0, 800);
		if (viewport.Y > 0) maxScrollHeight = Math.Min(maxScrollHeight, viewport.Y * 0.3f);
		scroll.CustomMinimumSize = new Vector2(0, Math.Min(events.Count * 50f, maxScrollHeight));
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.MouseFilter = Control.MouseFilterEnum.Stop;

		VBoxContainer scrollContent = new VBoxContainer();
		scrollContent.AddThemeConstantOverride("separation", 6);
		scrollContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(scrollContent, forceReadableName: false, Node.InternalMode.Disabled);

		int count = Math.Min(events.Count, 15);
		for (int i = events.Count - 1; i >= events.Count - count; i--)
		{
			AddDecisionEntry(scrollContent, events[i]);
		}
		_content.AddChild(scroll, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddRecentDecisions(int maxCount)
	{
		var events = Plugin.RunTracker?.GetCurrentRunEvents();
		if (events == null || events.Count == 0) return;
		AddSectionHeader("최근 선택");
		AddRecentDecisionsTo(_content, maxCount);
	}

	private void AddRecentDecisionsTo(VBoxContainer target, int maxCount)
	{
		var events = Plugin.RunTracker?.GetCurrentRunEvents();
		if (events == null || events.Count == 0) return;
		int count = Math.Min(events.Count, maxCount);
		for (int i = events.Count - 1; i >= events.Count - count; i--)
		{
			AddDecisionEntry(target, events[i]);
		}
	}

	private void AddDecisionEntry(DecisionEvent evt)
	{
		AddDecisionEntry(_content, evt);
	}

	private void AddDecisionEntry(VBoxContainer target, DecisionEvent evt)
	{
		string character = _currentCharacter ?? _currentDeckAnalysis?.Character ?? "unknown";
		string typeIcon = evt.EventType switch
		{
			DecisionEventType.CardReward => "\u2694",
			DecisionEventType.RelicReward => "\u2B50",
			DecisionEventType.BossRelic => "\u2B50",
			DecisionEventType.Shop => "\uD83D\uDCB0",
			DecisionEventType.CardRemove => "\u2702",
			_ => "\u2022"
		};
		Color borderColor = evt.EventType switch
		{
			DecisionEventType.CardReward => ClrPositive,
			DecisionEventType.RelicReward => ClrAccent,
			DecisionEventType.BossRelic => ClrAccent,
			DecisionEventType.Shop => ClrExpensive,
			DecisionEventType.CardRemove => ClrAqua,
			_ => ClrSub
		};

		PanelContainer entryPanel = new PanelContainer();
		StyleBoxFlat entryStyle = new StyleBoxFlat();
		entryStyle.BgColor = new Color(0.04f, 0.06f, 0.1f, 0.5f);
		entryStyle.CornerRadiusTopRight = 6;
		entryStyle.CornerRadiusBottomRight = 6;
		entryStyle.BorderWidthLeft = 3;
		entryStyle.BorderColor = borderColor;
		entryStyle.ContentMarginLeft = 10f;
		entryStyle.ContentMarginRight = 8f;
		entryStyle.ContentMarginTop = 4f;
		entryStyle.ContentMarginBottom = 4f;
		entryPanel.AddThemeStyleboxOverride("panel", entryStyle);

		VBoxContainer vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 1);
		entryPanel.AddChild(vbox, forceReadableName: false, Node.InternalMode.Disabled);

		// Main line: chosen card/relic with grade
		string chosenName = evt.ChosenId != null ? PrettifyId(evt.ChosenId) : "스킵";
		TierGrade chosenGrade = evt.ChosenId != null ? LookupGrade(evt.ChosenId, character) : TierGrade.F;
		string gradeStr = evt.ChosenId != null ? $" [{chosenGrade}]" : "";
		Label mainLine = new Label();
		mainLine.Text = $"{typeIcon} F{evt.Floor}: {chosenName}{gradeStr}";
		ApplyFont(mainLine, _fontBold);
		mainLine.AddThemeColorOverride("font_color", evt.ChosenId != null ? ClrAccent : ClrSub);
		mainLine.AddThemeFontSizeOverride("font_size", 14);
		mainLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		vbox.AddChild(mainLine, forceReadableName: false, Node.InternalMode.Disabled);

		// Alternatives line
		var alternatives = evt.OfferedIds.Where(id => id != evt.ChosenId).Take(3).ToList();
		if (alternatives.Count > 0)
		{
			var altParts = alternatives.Select(id =>
			{
				TierGrade g = LookupGrade(id, character);
				return $"{id} [{g}]";
			});
			Label altLine = new Label();
			altLine.Text = $"  over {string.Join(", ", altParts)}";
			ApplyFont(altLine, _fontBody);
			altLine.AddThemeColorOverride("font_color", ClrSub);
			altLine.AddThemeFontSizeOverride("font_size", 17);
			altLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			vbox.AddChild(altLine, forceReadableName: false, Node.InternalMode.Disabled);
		}

		target.AddChild(entryPanel, forceReadableName: false, Node.InternalMode.Disabled);
	}

	// === Feature 2: Deck composition visualization ===

	private void ClearDeckViz()
	{
		if (_deckVizContainer == null || !GodotObject.IsInstanceValid(_deckVizContainer))
			return;
		var vizChildren = _deckVizContainer.GetChildren().ToArray();
		foreach (Node child in vizChildren)
		{
			if (child is Control ctrl)
				SafeDisconnectSignals(ctrl);
			_deckVizContainer.RemoveChild(child);
			child.QueueFree();
		}
		_deckVizContainer.Visible = false;
		_deckVizContainer.CustomMinimumSize = Vector2.Zero;
	}

	private void AddInlineDeckViz(DeckAnalysis analysis)
	{
		if (analysis == null || analysis.TotalCards == 0)
			return;
		AddSectionHeader("덱 구성");
		AddInlineDeckVizTo(_content, analysis);
	}

	private void AddInlineDeckVizTo(VBoxContainer target, DeckAnalysis analysis)
	{
		if (analysis == null || analysis.TotalCards == 0)
			return;
		// Inline archetype info — replicate colored breakdown
		Label deckHeader = new Label();
		deckHeader.Text = $"내 덱 ({analysis.TotalCards}장)";
		ApplyFont(deckHeader, _fontBold);
		deckHeader.AddThemeFontSizeOverride("font_size", 15);
		deckHeader.AddThemeColorOverride("font_color", ClrCream);
		target.AddChild(deckHeader, forceReadableName: false, Node.InternalMode.Disabled);
		if (analysis.DetectedArchetypes != null && analysis.DetectedArchetypes.Count > 0)
		{
			float totalStr = analysis.DetectedArchetypes.Sum(a => a.Strength);
			if (totalStr <= 0) totalStr = 1f;
			int cIdx = 0;
			foreach (var arch in analysis.DetectedArchetypes)
			{
				int pct = (int)(arch.Strength / totalStr * 100f);
				Color ac = ArchColors[cIdx % ArchColors.Length];
				HBoxContainer aRow = new HBoxContainer();
				aRow.AddThemeConstantOverride("separation", 6);
				ColorRect aBar = new ColorRect();
				aBar.Color = new Color(ac, 0.7f);
				aBar.CustomMinimumSize = new Vector2(Math.Max(pct * 0.6f, 3f), 10f);
				aRow.AddChild(aBar, forceReadableName: false, Node.InternalMode.Disabled);
				Label aLbl = new Label();
				aLbl.Text = $"{arch.Archetype.DisplayName}  {pct}%";
				ApplyFont(aLbl, _fontBold);
				aLbl.AddThemeColorOverride("font_color", ac);
				aLbl.AddThemeFontSizeOverride("font_size", 13);
				aRow.AddChild(aLbl, forceReadableName: false, Node.InternalMode.Disabled);
				target.AddChild(aRow, forceReadableName: false, Node.InternalMode.Disabled);
				cIdx++;
			}
		}
		else
		{
			Label noArch = new Label();
			noArch.Text = "아키타입 방향 불명확";
			ApplyFont(noArch, _fontBody);
			noArch.AddThemeColorOverride("font_color", ClrSub);
			noArch.AddThemeFontSizeOverride("font_size", 13);
			target.AddChild(noArch, forceReadableName: false, Node.InternalMode.Disabled);
		}
		AddInlineEnergyCurve(target, analysis);
		AddInlineTypeDistribution(target, analysis);
	}

	private void AddInlineEnergyCurve(VBoxContainer target, DeckAnalysis analysis)
	{
		if (analysis.EnergyCurve.Count == 0) return;
		Label curveHeader = new Label();
		curveHeader.Text = "에너지 비용";
		ApplyFont(curveHeader, _fontBody);
		curveHeader.AddThemeColorOverride("font_color", ClrSub);
		curveHeader.AddThemeFontSizeOverride("font_size", 14);
		target.AddChild(curveHeader, forceReadableName: false, Node.InternalMode.Disabled);

		HBoxContainer costRow = new HBoxContainer();
		costRow.AddThemeConstantOverride("separation", 2);
		for (int cost = 0; cost <= 5; cost++)
		{
			Label costLbl = new Label();
			costLbl.Text = cost == 5 ? "5+" : cost.ToString();
			ApplyFont(costLbl, _fontBody);
			costLbl.AddThemeColorOverride("font_color", ClrSub);
			costLbl.AddThemeFontSizeOverride("font_size", 14);
			costLbl.HorizontalAlignment = HorizontalAlignment.Center;
			costLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			costRow.AddChild(costLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		target.AddChild(costRow, forceReadableName: false, Node.InternalMode.Disabled);

		HBoxContainer curveRow = new HBoxContainer();
		curveRow.AddThemeConstantOverride("separation", 2);
		curveRow.CustomMinimumSize = new Vector2(0, 18f);
		int maxCount = analysis.EnergyCurve.Count > 0 ? analysis.EnergyCurve.Values.Max() : 1;
		Color[] costColors = {
			new Color(0.3f, 0.8f, 0.4f),
			new Color(0.4f, 0.8f, 0.9f),
			new Color(0.92f, 0.88f, 0.78f),
			new Color(0.9f, 0.75f, 0.3f),
			new Color(1f, 0.6f, 0.3f),
			new Color(0.9f, 0.3f, 0.3f)
		};
		for (int cost = 0; cost <= 5; cost++)
		{
			int count = analysis.EnergyCurve.TryGetValue(cost, out int c) ? c : 0;
			VBoxContainer col = new VBoxContainer();
			col.AddThemeConstantOverride("separation", 1);
			col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			float barHeight = maxCount > 0 ? (float)count / maxCount * 12f : 0f;
			ColorRect bar = new ColorRect();
			bar.Color = costColors[cost];
			bar.CustomMinimumSize = new Vector2(0, Math.Max(barHeight, 1f));
			col.AddChild(bar, forceReadableName: false, Node.InternalMode.Disabled);
			Label lbl = new Label();
			lbl.Text = count.ToString();
			ApplyFont(lbl, _fontBody);
			lbl.AddThemeColorOverride("font_color", ClrSub);
			lbl.AddThemeFontSizeOverride("font_size", 14);
			lbl.HorizontalAlignment = HorizontalAlignment.Center;
			col.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
			curveRow.AddChild(col, forceReadableName: false, Node.InternalMode.Disabled);
		}
		target.AddChild(curveRow, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddInlineTypeDistribution(VBoxContainer target, DeckAnalysis analysis)
	{
		int totalTyped = analysis.AttackCount + analysis.SkillCount + analysis.PowerCount;
		if (totalTyped == 0) return;
		HBoxContainer typeRow = new HBoxContainer();
		typeRow.AddThemeConstantOverride("separation", 0);
		typeRow.CustomMinimumSize = new Vector2(0, 8f);
		if (analysis.AttackCount > 0)
		{
			ColorRect atkBar = new ColorRect();
			atkBar.Color = new Color(0.9f, 0.35f, 0.3f);
			atkBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			atkBar.SizeFlagsStretchRatio = analysis.AttackCount;
			atkBar.CustomMinimumSize = new Vector2(0, 8f);
			typeRow.AddChild(atkBar, forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (analysis.SkillCount > 0)
		{
			ColorRect sklBar = new ColorRect();
			sklBar.Color = new Color(0.3f, 0.5f, 1f);
			sklBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			sklBar.SizeFlagsStretchRatio = analysis.SkillCount;
			sklBar.CustomMinimumSize = new Vector2(0, 8f);
			typeRow.AddChild(sklBar, forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (analysis.PowerCount > 0)
		{
			ColorRect pwrBar = new ColorRect();
			pwrBar.Color = new Color(0.3f, 0.8f, 0.4f);
			pwrBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			pwrBar.SizeFlagsStretchRatio = analysis.PowerCount;
			pwrBar.CustomMinimumSize = new Vector2(0, 8f);
			typeRow.AddChild(pwrBar, forceReadableName: false, Node.InternalMode.Disabled);
		}
		target.AddChild(typeRow, forceReadableName: false, Node.InternalMode.Disabled);
		HBoxContainer typeLblRow = new HBoxContainer();
		typeLblRow.AddThemeConstantOverride("separation", 8);
		typeLblRow.Alignment = BoxContainer.AlignmentMode.Center;
		if (analysis.AttackCount > 0)
		{
			Label atkLbl = new Label();
			atkLbl.Text = $"{analysis.AttackCount} 공격";
			ApplyFont(atkLbl, _fontBody);
			atkLbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
			atkLbl.AddThemeFontSizeOverride("font_size", 15);
			typeLblRow.AddChild(atkLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (analysis.SkillCount > 0)
		{
			Label sklLbl = new Label();
			sklLbl.Text = $"{analysis.SkillCount} 스킬";
			ApplyFont(sklLbl, _fontBody);
			sklLbl.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 1f));
			sklLbl.AddThemeFontSizeOverride("font_size", 15);
			typeLblRow.AddChild(sklLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (analysis.PowerCount > 0)
		{
			Label pwrLbl = new Label();
			pwrLbl.Text = $"{analysis.PowerCount} 파워";
			ApplyFont(pwrLbl, _fontBody);
			pwrLbl.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.4f));
			pwrLbl.AddThemeFontSizeOverride("font_size", 15);
			typeLblRow.AddChild(pwrLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		target.AddChild(typeLblRow, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void UpdateDeckViz(DeckAnalysis analysis)
	{
		if (_deckVizContainer == null || !GodotObject.IsInstanceValid(_deckVizContainer))
			return;
		// Clear previous
		var vizChildren = _deckVizContainer.GetChildren().ToArray();
		foreach (Node child in vizChildren)
		{
			if (child is Control ctrl)
				SafeDisconnectSignals(ctrl);
			_deckVizContainer.RemoveChild(child);
			child.QueueFree();
		}
		if (analysis == null || analysis.TotalCards == 0)
			return;

		// Energy curve row
		if (analysis.EnergyCurve.Count > 0)
		{
			// Header
			Label curveHeader = new Label();
			curveHeader.Text = "에너지 비용";
			ApplyFont(curveHeader, _fontBody);
			curveHeader.AddThemeColorOverride("font_color", ClrSub);
			curveHeader.AddThemeFontSizeOverride("font_size", 14);
			_deckVizContainer.AddChild(curveHeader, forceReadableName: false, Node.InternalMode.Disabled);

			// Cost number row above bars
			HBoxContainer costRow = new HBoxContainer();
			costRow.AddThemeConstantOverride("separation", 2);
			for (int cost = 0; cost <= 5; cost++)
			{
				Label costLbl = new Label();
				costLbl.Text = cost == 5 ? "5+" : cost.ToString();
				ApplyFont(costLbl, _fontBody);
				costLbl.AddThemeColorOverride("font_color", ClrSub);
				costLbl.AddThemeFontSizeOverride("font_size", 14);
				costLbl.HorizontalAlignment = HorizontalAlignment.Center;
				costLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				costRow.AddChild(costLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_deckVizContainer.AddChild(costRow, forceReadableName: false, Node.InternalMode.Disabled);

			HBoxContainer curveRow = new HBoxContainer();
			curveRow.AddThemeConstantOverride("separation", 2);
			curveRow.CustomMinimumSize = new Vector2(0, 18f);
			int maxCount = analysis.EnergyCurve.Count > 0 ? analysis.EnergyCurve.Values.Max() : 1;
			Color[] costColors = {
				new Color(0.3f, 0.8f, 0.4f),   // 0: green
				new Color(0.4f, 0.8f, 0.9f),   // 1: aqua
				new Color(0.92f, 0.88f, 0.78f), // 2: cream
				new Color(0.9f, 0.75f, 0.3f),   // 3: gold
				new Color(1f, 0.6f, 0.3f),       // 4: orange
				new Color(0.9f, 0.3f, 0.3f)      // 5+: red
			};
			for (int cost = 0; cost <= 5; cost++)
			{
				int count = analysis.EnergyCurve.TryGetValue(cost, out int c) ? c : 0;
				VBoxContainer col = new VBoxContainer();
				col.AddThemeConstantOverride("separation", 1);
				col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

				// Bar
				float barHeight = maxCount > 0 ? (float)count / maxCount * 12f : 0f;
				ColorRect bar = new ColorRect();
				bar.Color = costColors[cost];
				bar.CustomMinimumSize = new Vector2(0, Math.Max(barHeight, 1f));
				col.AddChild(bar, forceReadableName: false, Node.InternalMode.Disabled);

				// Count label below bar
				Label lbl = new Label();
				lbl.Text = count.ToString();
				ApplyFont(lbl, _fontBody);
				lbl.AddThemeColorOverride("font_color", ClrSub);
				lbl.AddThemeFontSizeOverride("font_size", 14);
				lbl.HorizontalAlignment = HorizontalAlignment.Center;
				col.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);

				curveRow.AddChild(col, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_deckVizContainer.AddChild(curveRow, forceReadableName: false, Node.InternalMode.Disabled);
		}

		// Type distribution row
		int totalTyped = analysis.AttackCount + analysis.SkillCount + analysis.PowerCount;
		if (totalTyped > 0)
		{
			HBoxContainer typeRow = new HBoxContainer();
			typeRow.AddThemeConstantOverride("separation", 0);
			typeRow.CustomMinimumSize = new Vector2(0, 8f);
			// Attack segment (red)
			if (analysis.AttackCount > 0)
			{
				ColorRect atkBar = new ColorRect();
				atkBar.Color = new Color(0.9f, 0.35f, 0.3f);
				atkBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				atkBar.SizeFlagsStretchRatio = analysis.AttackCount;
				atkBar.CustomMinimumSize = new Vector2(0, 8f);
				typeRow.AddChild(atkBar, forceReadableName: false, Node.InternalMode.Disabled);
			}
			// Skill segment (blue)
			if (analysis.SkillCount > 0)
			{
				ColorRect sklBar = new ColorRect();
				sklBar.Color = new Color(0.3f, 0.5f, 1f);
				sklBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				sklBar.SizeFlagsStretchRatio = analysis.SkillCount;
				sklBar.CustomMinimumSize = new Vector2(0, 8f);
				typeRow.AddChild(sklBar, forceReadableName: false, Node.InternalMode.Disabled);
			}
			// Power segment (green)
			if (analysis.PowerCount > 0)
			{
				ColorRect pwrBar = new ColorRect();
				pwrBar.Color = new Color(0.3f, 0.8f, 0.4f);
				pwrBar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				pwrBar.SizeFlagsStretchRatio = analysis.PowerCount;
				pwrBar.CustomMinimumSize = new Vector2(0, 8f);
				typeRow.AddChild(pwrBar, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_deckVizContainer.AddChild(typeRow, forceReadableName: false, Node.InternalMode.Disabled);

			// Type labels — color-matched
			HBoxContainer typeLblRow = new HBoxContainer();
			typeLblRow.AddThemeConstantOverride("separation", 8);
			typeLblRow.Alignment = BoxContainer.AlignmentMode.Center;
			if (analysis.AttackCount > 0)
			{
				Label atkLbl = new Label();
				atkLbl.Text = $"{analysis.AttackCount} 공격";
				ApplyFont(atkLbl, _fontBody);
				atkLbl.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
				atkLbl.AddThemeFontSizeOverride("font_size", 17);
				typeLblRow.AddChild(atkLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			if (analysis.SkillCount > 0)
			{
				Label sklLbl = new Label();
				sklLbl.Text = $"{analysis.SkillCount} 스킬";
				ApplyFont(sklLbl, _fontBody);
				sklLbl.AddThemeColorOverride("font_color", new Color(0.3f, 0.5f, 1f));
				sklLbl.AddThemeFontSizeOverride("font_size", 17);
				typeLblRow.AddChild(sklLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			if (analysis.PowerCount > 0)
			{
				Label pwrLbl = new Label();
				pwrLbl.Text = $"{analysis.PowerCount} 파워";
				ApplyFont(pwrLbl, _fontBody);
				pwrLbl.AddThemeColorOverride("font_color", new Color(0.3f, 0.8f, 0.4f));
				pwrLbl.AddThemeFontSizeOverride("font_size", 17);
				typeLblRow.AddChild(pwrLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			_deckVizContainer.AddChild(typeLblRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	// === A1: Win rate tracker ===

	private string _lastWinRateCharacter;

	private void UpdateWinRate()
	{
		if (_winRateLabel == null || !GodotObject.IsInstanceValid(_winRateLabel))
			return;
		if (string.IsNullOrEmpty(_currentCharacter) || Plugin.RunDatabase == null)
		{
			_winRateLabel.Visible = false;
			return;
		}
		// Only re-query if character changed
		if (_currentCharacter == _lastWinRateCharacter && _winRateLabel.Visible)
			return;
		_lastWinRateCharacter = _currentCharacter;
		try
		{
			var (wins, total) = Plugin.RunDatabase.GetCharacterWinRate(_currentCharacter);
			if (total == 0)
			{
				_winRateLabel.Visible = false;
				return;
			}
			float rate = (float)wins / total * 100f;
			string charName = char.ToUpper(_currentCharacter[0]) + _currentCharacter.Substring(1);
			_winRateLabel.Text = $"{charName}: {wins}W / {total} ({rate:F0}%)";
			_winRateLabel.AddThemeColorOverride("font_color", rate >= 50f ? ClrPositive : rate >= 30f ? ClrSub : ClrNegative);
			_winRateLabel.Visible = true;
		}
		catch
		{
			_winRateLabel.Visible = false;
		}
	}

	// === A2: Post-run controversial picks summary ===

	public void ShowRunSummary(RunOutcome outcome, int finalFloor, int finalAct)
	{
		try
		{
			var events = Plugin.RunTracker?.GetCurrentRunEvents();
			if (events == null || events.Count == 0)
			{
				Clear();
				return;
			}
			string character = Plugin.RunTracker?.CurrentCharacter ?? _currentCharacter ?? "unknown";
			string runId = Plugin.RunTracker?.CurrentRunId;
			_currentCards = null;
			_currentRelics = null;
			_currentDeckAnalysis = null;
			_currentScreen = outcome == RunOutcome.Win ? "RUN WON!" : "RUN LOST";
			_mapAdvice = null;
			_lastWinRateCharacter = null;
			if (!EnsureOverlay()) return;
			if (_screenLabel != null && GodotObject.IsInstanceValid(_screenLabel))
				_screenLabel.Text = _currentScreen;
			UpdateTitleSepColor();
			UpdateWinRate();
			if (_collapsed)
			{
				ResizePanelToContent();
				return;
			}
			DisconnectAllHoverSignals();
			var children = _content.GetChildren().ToArray();
			foreach (Node child in children)
			{
				if (child != null)
				{
					if (child is Control ctrl)
						SafeDisconnectSignals(ctrl);
					_content.RemoveChild(child);
					child.QueueFree();
				}
			}

			Color outcomeColor = outcome == RunOutcome.Win ? ClrPositive : ClrNegative;
			AddSectionHeader($"RUN SUMMARY \u2014 {outcome.ToString().ToUpper()} (Floor {finalFloor})");

			// Generate RunSummaryData from DB
			RunSummaryData summaryData = null;
			if (!string.IsNullOrEmpty(runId) && Plugin.RunDatabase != null)
			{
				try
				{
					var runSummary = new RunSummary(Plugin.RunDatabase);
					summaryData = runSummary.Generate(runId, character, outcome.ToString());
				}
				catch (Exception ex)
				{
					Plugin.Log($"RunSummary generation failed: {ex.Message}");
				}
			}

			// ─── Combat Efficiency ───
			if (summaryData != null && summaryData.TotalTurns > 0)
			{
				AddSectionHeader("전투 효율");
				float dpt = (float)summaryData.TotalDamageDealt / summaryData.TotalTurns;
				float dtpt = (float)summaryData.TotalDamageTaken / summaryData.TotalTurns;
				float bpt = (float)summaryData.TotalBlockGenerated / summaryData.TotalTurns;

				AddStatRow("총 딜량", $"{summaryData.TotalDamageDealt}", ClrPositive);
				AddStatRow("턴당 딜", $"{dpt:F1}", dpt >= 15 ? ClrPositive : ClrSub);
				AddStatRow("받은 피해", $"{summaryData.TotalDamageTaken}", dtpt > 10 ? ClrNegative : ClrSub);
				AddStatRow("생성 블록", $"{summaryData.TotalBlockGenerated}", bpt >= 8 ? ClrPositive : ClrSub);
				AddStatRow("총 턴 수", $"{summaryData.TotalTurns}", ClrSub);
			}

			// ─── Most/Least Played Cards ───
			if (summaryData?.MostPlayedCards?.Count > 0)
			{
				AddSectionHeader("가장 많이 사용한 카드");
				foreach (var card in summaryData.MostPlayedCards)
				{
					string name = PrettifyId(card.CardId);
					AddStatRow(name, $"{card.PlayCount}회", ClrAccent);
				}
			}
			if (summaryData?.LeastPlayedCards?.Count > 0)
			{
				AddSectionHeader("거의 사용하지 않은 카드");
				foreach (var card in summaryData.LeastPlayedCards)
				{
					string name = PrettifyId(card.CardId);
					AddStatRow(name, $"{card.PlayCount}회", ClrNegative);
				}
			}

			// ─── Decision Review (Community Comparison) ───
			if (summaryData?.Decisions?.Count > 0)
			{
				var reviewed = summaryData.Decisions.Where(d => d.Feedback != null).ToList();
				if (reviewed.Count > 0)
				{
					AddSectionHeader("커뮤니티 데이터 대비 결정 분석");
					int good = summaryData.GoodDecisions;
					int bad = summaryData.BadDecisions;
					AddStatRow("좋은 선택", $"{good}", ClrPositive);
					AddStatRow("아쉬운 선택", $"{bad}", bad > 0 ? ClrNegative : ClrSub);

					foreach (var d in reviewed.Where(d => !d.WasBetterChoice.GetValueOrDefault(true)).Take(5))
					{
						PanelContainer dPanel = new PanelContainer();
						StyleBoxFlat dStyle = new StyleBoxFlat();
						dStyle.BgColor = new Color(ClrNegative, 0.08f);
						dStyle.CornerRadiusTopRight = 6;
						dStyle.CornerRadiusBottomRight = 6;
						dStyle.BorderWidthLeft = 3;
						dStyle.BorderColor = ClrNegative;
						dStyle.ContentMarginLeft = 10f;
						dStyle.ContentMarginRight = 8f;
						dStyle.ContentMarginTop = 4f;
						dStyle.ContentMarginBottom = 4f;
						dPanel.AddThemeStyleboxOverride("panel", dStyle);

						Label dLbl = new Label();
						dLbl.Text = $"F{d.Floor} [{d.EventType}]: {d.Feedback}";
						ApplyFont(dLbl, _fontBody);
						dLbl.AddThemeColorOverride("font_color", ClrCream);
						dLbl.AddThemeFontSizeOverride("font_size", 13);
						dLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
						dPanel.AddChild(dLbl, forceReadableName: false, Node.InternalMode.Disabled);
						_content.AddChild(dPanel, forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
			}

			// ─── Controversial Picks (tier-based) ───
			var controversial = new List<(DecisionEvent evt, TierGrade chosenGrade, TierGrade bestGrade)>();
			int nullChoices = events.Count(e => e.ChosenId == null && e.OfferedIds?.Count > 0);
			bool choiceTrackingBroken = nullChoices > events.Count * 0.7f;
			foreach (var evt in events)
			{
				if (evt.OfferedIds == null || evt.OfferedIds.Count == 0) continue;
				TierGrade bestGrade = TierGrade.F;
				foreach (string id in evt.OfferedIds)
				{
					TierGrade g = LookupGrade(id, character);
					if (g > bestGrade) bestGrade = g;
				}
				if (evt.ChosenId == null)
				{
					if (!choiceTrackingBroken && bestGrade >= TierGrade.A)
						controversial.Add((evt, TierGrade.F, bestGrade));
				}
				else
				{
					TierGrade chosenGrade = LookupGrade(evt.ChosenId, character);
					int gap = (int)bestGrade - (int)chosenGrade;
					if (gap >= 2)
						controversial.Add((evt, chosenGrade, bestGrade));
				}
			}
			if (controversial.Count > 0)
			{
				AddSectionHeader("논란 선택");
				foreach (var (evt, chosenGrade, bestGrade) in controversial.Take(8))
				{
					string chosen = evt.ChosenId != null ? PrettifyId(evt.ChosenId) : "스킵";
					string bestId = evt.OfferedIds?.OrderByDescending(id => (int)LookupGrade(id, character)).FirstOrDefault();
					string bestName = bestId != null ? PrettifyId(bestId) : "?";
					int gap = (int)bestGrade - (int)chosenGrade;
					PanelContainer cPanel = new PanelContainer();
					StyleBoxFlat cStyle = new StyleBoxFlat();
					cStyle.BgColor = new Color(outcomeColor, 0.08f);
					cStyle.CornerRadiusTopRight = 6;
					cStyle.CornerRadiusBottomRight = 6;
					cStyle.BorderWidthLeft = 3;
					cStyle.BorderColor = outcomeColor;
					cStyle.ContentMarginLeft = 10f;
					cStyle.ContentMarginRight = 8f;
					cStyle.ContentMarginTop = 4f;
					cStyle.ContentMarginBottom = 4f;
					cPanel.AddThemeStyleboxOverride("panel", cStyle);
					Label cLbl = new Label();
					cLbl.Text = $"F{evt.Floor}: {chosen} [{chosenGrade}] \u2014 Best: {bestName} [{bestGrade}] ({gap} grades)";
					ApplyFont(cLbl, _fontBody);
					cLbl.AddThemeColorOverride("font_color", outcomeColor);
					cLbl.AddThemeFontSizeOverride("font_size", 13);
					cLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					cPanel.AddChild(cLbl, forceReadableName: false, Node.InternalMode.Disabled);
					_content.AddChild(cPanel, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
			else
			{
				Label noContr = new Label();
				noContr.Text = "논란 선택 없음 \u2014 좋은 판단!";
				ApplyFont(noContr, _fontBody);
				noContr.AddThemeColorOverride("font_color", ClrPositive);
				noContr.AddThemeFontSizeOverride("font_size", 14);
				_content.AddChild(noContr, forceReadableName: false, Node.InternalMode.Disabled);
			}

			// ─── Gold ───
			if (summaryData != null && summaryData.PeakGold > 0)
			{
				AddStatRow("최고 골드", $"{summaryData.PeakGold}G", ClrExpensive);
			}

			// ─── Decision Replay (v0.12.2) ───
			AddDecisionReplay(events, character);

			// ─── Global Stats Comparison (v0.14.3) ───
			AddGlobalStatsComparison(character);

			// Stats line
			Label statsLbl = new Label();
			statsLbl.Text = $"Decisions: {events.Count} | Controversial: {controversial.Count}";
			ApplyFont(statsLbl, _fontBold);
			statsLbl.AddThemeColorOverride("font_color", ClrSub);
			statsLbl.AddThemeFontSizeOverride("font_size", 14);
			_content.AddChild(statsLbl, forceReadableName: false, Node.InternalMode.Disabled);
			ResizePanelToContent();

			if (_content != null && GodotObject.IsInstanceValid(_content))
			{
				_content.Modulate = new Color(1, 1, 1, 0);
				_content.CreateTween()?.TweenProperty(_content, "modulate", Colors.White, 0.3f);
			}
			_previousScreen = _currentScreen;
		}
		catch (Exception ex)
		{
			Plugin.Log($"ShowRunSummary error: {ex}");
			Clear();
		}
	}

	private void AddStatRow(string label, string value, Color valueColor)
	{
		HBoxContainer row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		Label lbl = new Label();
		lbl.Text = label;
		ApplyFont(lbl, _fontBody);
		lbl.AddThemeColorOverride("font_color", ClrSub);
		lbl.AddThemeFontSizeOverride("font_size", 13);
		lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
		Label val = new Label();
		val.Text = value;
		ApplyFont(val, _fontBold);
		val.AddThemeColorOverride("font_color", valueColor);
		val.AddThemeFontSizeOverride("font_size", 13);
		row.AddChild(val, forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
	}

	/// <summary>
	/// v0.12.2: Decision replay — scrollable per-decision review with community win-rate.
	/// </summary>
	private void AddDecisionReplay(IReadOnlyList<DecisionEvent> events, string character)
	{
		if (events == null || events.Count == 0) return;
		var meaningful = events.Where(e => e.OfferedIds?.Count > 0).ToList();
		if (meaningful.Count == 0) return;

		AddSectionHeader("결정 리플레이");

		ScrollContainer scroll = new ScrollContainer();
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		scroll.MouseFilter = Control.MouseFilterEnum.Stop;
		float maxH = Math.Min(meaningful.Count * 60f, 300f);
		scroll.CustomMinimumSize = new Vector2(0, maxH);
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill;

		VBoxContainer scrollContent = new VBoxContainer();
		scrollContent.AddThemeConstantOverride("separation", 4);
		scrollContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(scrollContent, forceReadableName: false, Node.InternalMode.Disabled);

		foreach (var evt in meaningful)
		{
			PanelContainer entry = new PanelContainer();
			StyleBoxFlat style = new StyleBoxFlat();
			style.BgColor = new Color(0.04f, 0.06f, 0.1f, 0.5f);
			style.CornerRadiusTopRight = 6;
			style.CornerRadiusBottomRight = 6;
			style.BorderWidthLeft = 3;
			style.ContentMarginLeft = 10f;
			style.ContentMarginRight = 8f;
			style.ContentMarginTop = 3f;
			style.ContentMarginBottom = 3f;

			// Color border by decision quality
			Color borderClr = ClrSub;
			if (evt.ChosenId != null)
			{
				TierGrade chosen = LookupGrade(evt.ChosenId, character);
				TierGrade best = evt.OfferedIds.Max(id => LookupGrade(id, character));
				int gap = (int)best - (int)chosen;
				borderClr = gap == 0 ? ClrPositive : gap == 1 ? ClrAccent : ClrNegative;
			}
			style.BorderColor = borderClr;
			entry.AddThemeStyleboxOverride("panel", style);

			VBoxContainer vbox = new VBoxContainer();
			vbox.AddThemeConstantOverride("separation", 1);
			entry.AddChild(vbox, forceReadableName: false, Node.InternalMode.Disabled);

			// Line 1: Floor + event type + chosen
			string typeIcon = evt.EventType switch
			{
				DecisionEventType.CardReward => "\u2694",
				DecisionEventType.RelicReward or DecisionEventType.BossRelic => "\u2B50",
				DecisionEventType.Shop => "\uD83D\uDCB0",
				DecisionEventType.CardRemove => "\u2702",
				_ => "\u2022"
			};
			string chosenName = evt.ChosenId != null ? PrettifyId(evt.ChosenId) : "스킵";
			TierGrade chosenGrade = evt.ChosenId != null ? LookupGrade(evt.ChosenId, character) : TierGrade.F;

			Label mainLine = new Label();
			mainLine.Text = $"{typeIcon} F{evt.Floor}: {chosenName} [{chosenGrade}]";
			ApplyFont(mainLine, _fontBold);
			mainLine.AddThemeColorOverride("font_color", borderClr);
			mainLine.AddThemeFontSizeOverride("font_size", 13);
			mainLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			vbox.AddChild(mainLine, forceReadableName: false, Node.InternalMode.Disabled);

			// Line 2: All offered options with grades + community win-rate
			var parts = new List<string>();
			foreach (string id in evt.OfferedIds)
			{
				TierGrade g = LookupGrade(id, character);
				string name = PrettifyId(id);
				string marker = id == evt.ChosenId ? "\u25B6" : " ";
				// Try to get community win rate
				string wrStr = "";
				try
				{
					var stats = Plugin.RunDatabase?.GetCommunityCardStats(character, id);
					if (stats != null && stats.SampleSize >= 5)
						wrStr = $" {stats.WinRateWhenPicked:P0}";
				}
				catch { }
				parts.Add($"{marker}{name}[{g}]{wrStr}");
			}
			Label offeredLine = new Label();
			offeredLine.Text = string.Join("  ", parts);
			ApplyFont(offeredLine, _fontBody);
			offeredLine.AddThemeColorOverride("font_color", ClrSub);
			offeredLine.AddThemeFontSizeOverride("font_size", 11);
			offeredLine.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			vbox.AddChild(offeredLine, forceReadableName: false, Node.InternalMode.Disabled);

			scrollContent.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
		}

		_content.AddChild(scroll, forceReadableName: false, Node.InternalMode.Disabled);
	}

	/// <summary>
	/// v0.14.3: Global stats comparison — player performance vs leaderboard averages.
	/// </summary>
	private void AddGlobalStatsComparison(string character)
	{
		if (Plugin.SteamLeaderboardSync?.Cache == null) return;
		var cache = Plugin.SteamLeaderboardSync.Cache;
		if (cache.Leaderboards == null || cache.Leaderboards.Count == 0) return;
		if (Plugin.RunDatabase == null || string.IsNullOrEmpty(character)) return;

		AddSectionHeader("글로벌 통계 비교");

		try
		{
			// Player stats
			var (wins, total) = Plugin.RunDatabase.GetCharacterWinRate(character);
			if (total == 0) return;
			float playerWinRate = (float)wins / total;

			// Show player win rate
			string charName = char.ToUpper(character[0]) + character.Substring(1);
			AddStatRow($"{charName} 승률", $"{playerWinRate:P0} ({wins}/{total})",
				playerWinRate >= 0.5f ? ClrPositive : playerWinRate >= 0.3f ? ClrAccent : ClrNegative);

			// Leaderboard summary
			int totalPlayers = 0;
			foreach (var board in cache.Leaderboards)
			{
				if (board.EntryCount > totalPlayers)
					totalPlayers = board.EntryCount;
			}
			if (totalPlayers > 0)
			{
				AddStatRow("글로벌 참가자", $"{totalPlayers:N0}명", ClrSub);
			}

			// Show top leaderboards
			int shown = 0;
			foreach (var board in cache.Leaderboards)
			{
				if (shown >= 5) break;
				if (board.EntryCount < 10) continue;
				AddStatRow(board.Name, $"{board.EntryCount:N0}명", ClrSub);
				shown++;
			}

			// Pipeline status summary
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
					AddStatRow("파이프라인", $"{success}/{history.Count} 성공 ({totalMs}ms)",
						failed == 0 ? ClrPositive : ClrNegative);
				}
			}

			// Last updated
			if (!string.IsNullOrEmpty(cache.LastUpdated))
			{
				if (DateTime.TryParse(cache.LastUpdated, out var lastUpdate))
				{
					var age = DateTime.UtcNow - lastUpdate;
					string ageStr = age.TotalHours < 1 ? $"{age.TotalMinutes:F0}분 전" :
						age.TotalDays < 1 ? $"{age.TotalHours:F0}시간 전" :
						$"{age.TotalDays:F0}일 전";
					AddStatRow("마지막 업데이트", ageStr, ClrSub);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"AddGlobalStatsComparison error: {ex.Message}");
		}
	}
}
