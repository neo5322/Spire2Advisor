using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

/// <summary>UI element builders — cards, relics, badges, sections, tooltips.</summary>
public partial class OverlayManager
{
	// Archetype bar colors — distinct per slot for easy visual parsing
	private static readonly Color[] ArchColors = {
		new Color(0.4f, 0.8f, 0.95f),  // cyan
		new Color(0.95f, 0.6f, 0.3f),  // orange
		new Color(0.7f, 0.5f, 0.95f),  // purple
		new Color(0.3f, 0.9f, 0.5f),   // green
	};

	private void AddSectionHeader(string text)
	{
		// Separator line before header (except first section)
		if (_content.GetChildCount() > 0)
		{
			HSeparator sep = new HSeparator();
			sep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.4f), Thickness = 1 });
			_content.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
		}
		Label label = new Label();
		label.Text = text;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", ClrAccent);
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		_content.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
	}

	/// <summary>
	/// Adds a collapsible section: clickable header with toggle arrow, returns content VBox.
	/// sectionKey is used for persisting collapsed state in settings.
	/// Returns null if section is collapsed (caller should skip adding children).
	/// </summary>
	private VBoxContainer AddCollapsibleSection(string text, string sectionKey, ref bool isExpanded)
	{
		// Separator
		if (_content.GetChildCount() > 0)
		{
			HSeparator sep = new HSeparator();
			sep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.4f), Thickness = 1 });
			_content.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Header row: clickable toggle
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.MouseFilter = Control.MouseFilterEnum.Stop;
		headerRow.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		Label arrow = new Label();
		arrow.Text = isExpanded ? "\u25BC " : "\u25B6 ";
		ApplyFont(arrow, _fontBold);
		arrow.AddThemeColorOverride("font_color", ClrSub);
		arrow.AddThemeFontSizeOverride("font_size", 14);
		arrow.MouseFilter = Control.MouseFilterEnum.Ignore;
		headerRow.AddChild(arrow, forceReadableName: false, Node.InternalMode.Disabled);
		Label headerLabel = new Label();
		headerLabel.Text = text;
		ApplyFont(headerLabel, _fontBold);
		headerLabel.AddThemeColorOverride("font_color", ClrAccent);
		headerLabel.AddThemeFontSizeOverride("font_size", 16);
		headerLabel.AddThemeConstantOverride("outline_size", 3);
		headerLabel.AddThemeColorOverride("font_outline_color", ClrOutline);
		headerLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		headerRow.AddChild(headerLabel, forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(headerRow, forceReadableName: false, Node.InternalMode.Disabled);
		if (!isExpanded)
		{
			// Capture for click handler
			bool localExpanded = isExpanded;
			string localKey = sectionKey;
			headerRow.GuiInput += (InputEvent ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					ToggleSectionSetting(localKey, true);
					Rebuild();
				}
			};
			return null;
		}
		// Section is expanded — add content container
		VBoxContainer sectionContent = new VBoxContainer();
		sectionContent.AddThemeConstantOverride("separation", 4);
		_content.AddChild(sectionContent, forceReadableName: false, Node.InternalMode.Disabled);
		// Click to collapse
		string collapseKey = sectionKey;
		headerRow.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				ToggleSectionSetting(collapseKey, false);
				Rebuild();
			}
		};
		return sectionContent;
	}

	private void ToggleSectionSetting(string key, bool value)
	{
		switch (key)
		{
			case "deck": _showDeckBreakdown = value; _settings.ShowDeckBreakdown = value; break;
			case "history": _showHistory = value; _settings.ShowDecisionHistory = value; break;
			// "draw" case removed — draw probability feature removed
		}
		_settings.Save();
	}

	private void AddCardEntry(ScoredCard card)
	{
		PanelContainer panelContainer = CreateEntryPanel(card.IsBestPick, card.FinalGrade);
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 8);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		CenterContainer badge = CreateBadge(card.FinalGrade, card.FinalScore);
		hBoxContainer.AddChild(badge, forceReadableName: false, Node.InternalMode.Disabled);
		// V2: Pulse animation on best pick badge
		if (card.IsBestPick)
		{
			CenterContainer pulseBadge = badge;
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(pulseBadge))
				{
					var tween = pulseBadge.CreateTween();
					if (tween != null)
					{
						tween.SetLoops(2);
						tween.TweenProperty(pulseBadge, "modulate:a", 0.6f, 0.2f);
						tween.TweenProperty(pulseBadge, "modulate:a", 1.0f, 0.2f);
					}
				}
			}).CallDeferred();
		}
		// Card portrait thumbnail (rounded corners with border)
		Texture2D portrait = GetCardPortrait(card.Id, _currentCharacter);
		if (portrait != null)
		{
			PanelContainer thumbClip = new PanelContainer();
			thumbClip.ClipContents = true;
			thumbClip.CustomMinimumSize = new Vector2(36f, 36f);
			StyleBoxFlat thumbStyle = new StyleBoxFlat();
			thumbStyle.BgColor = new Color(0, 0, 0, 0);
			thumbStyle.CornerRadiusTopLeft = 6;
			thumbStyle.CornerRadiusTopRight = 6;
			thumbStyle.CornerRadiusBottomLeft = 6;
			thumbStyle.CornerRadiusBottomRight = 6;
			thumbStyle.BorderWidthTop = 2;
			thumbStyle.BorderWidthBottom = 2;
			thumbStyle.BorderWidthLeft = 2;
			thumbStyle.BorderWidthRight = 2;
			thumbStyle.BorderColor = new Color(ClrBorder, 0.8f);
			thumbClip.AddThemeStyleboxOverride("panel", thumbStyle);
			TextureRect thumb = new TextureRect();
			thumb.Texture = portrait;
			thumb.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			thumb.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			thumb.CustomMinimumSize = new Vector2(36f, 36f);
			thumbClip.AddChild(thumb, forceReadableName: false, Node.InternalMode.Disabled);
			hBoxContainer.AddChild(thumbClip, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// V4: Card art hover preview
		if (_showTooltips && portrait != null)
		{
			Texture2D hoverTex = portrait;
			ConnectHoverSignals(panelContainer,
				() =>
				{
					if (_hoverPreview != null && GodotObject.IsInstanceValid(_hoverPreview) &&
					    _hoverPreviewTex != null && _panel != null && GodotObject.IsInstanceValid(_panel))
					{
						_hoverPreviewTex.Texture = hoverTex;
						_hoverPreview.Position = new Vector2(_panel.GlobalPosition.X - 218f,
							panelContainer.GlobalPosition.Y);
						_hoverPreview.Visible = true;
					}
				},
				() =>
				{
					if (_hoverPreview != null && GodotObject.IsInstanceValid(_hoverPreview))
						_hoverPreview.Visible = false;
				});
		}
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vBoxContainer.AddThemeConstantOverride("separation", 0);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		// Card name + sub-grade inline
		Label label = new Label();
		string text = card.Name ?? PrettifyId(card.Id);
		string upgradeTag = card.Upgraded ? " +" : "";
		label.Text = $"{text}{upgradeTag}";
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", card.IsBestPick ? ClrAccent : ClrCream);
		label.AddThemeFontSizeOverride("font_size", 17);
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		// Line 2: type • cost • one-line reason
		string typeLower = card.Type?.ToLowerInvariant() ?? "";
		string costStr = card.Cost == 0 ? "0 cost" : card.Cost == 1 ? "1 energy" : $"{card.Cost} energy";
		string priceStr = "";
		if (card.Price > 0)
		{
			priceStr = _goldIcon != null ? "" : $" \u2022 {card.Price}g";
		}
		string oneLiner = BuildOneLiner(card.SynergyReasons, card.AntiSynergyReasons, card.BaseTier, card.FinalGrade);
		string metaText = $"{typeLower} \u2022 {costStr}{priceStr}";
		if (oneLiner.Length > 0)
			metaText += $" \u2022 {oneLiner}";
		Label metaLbl = new Label();
		metaLbl.Text = metaText;
		ApplyFont(metaLbl, _fontBody);
		bool hasAnti = card.AntiSynergyReasons != null && card.AntiSynergyReasons.Count > 0;
		metaLbl.AddThemeColorOverride("font_color", hasAnti ? ClrNegative : card.Cost >= 3 ? ClrExpensive : ClrSub);
		metaLbl.AddThemeFontSizeOverride("font_size", 14);
		metaLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(metaLbl, forceReadableName: false, Node.InternalMode.Disabled);
		// Shop price with gold icon (inline after meta)
		if (card.Price > 0 && _goldIcon != null)
		{
			HBoxContainer priceRow = new HBoxContainer();
			priceRow.AddThemeConstantOverride("separation", 2);
			TextureRect goldTex = new TextureRect();
			goldTex.Texture = _goldIcon;
			goldTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			goldTex.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
			goldTex.CustomMinimumSize = new Vector2(12f, 12f);
			priceRow.AddChild(goldTex, forceReadableName: false, Node.InternalMode.Disabled);
			Label priceLbl = new Label();
			priceLbl.Text = $"{card.Price}";
			ApplyFont(priceLbl, _fontBody);
			priceLbl.AddThemeColorOverride("font_color", ClrAccent);
			priceLbl.AddThemeFontSizeOverride("font_size", 17);
			priceRow.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);
			vBoxContainer.AddChild(priceRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Expandable details on hover (hidden by default)
		if (_showTooltips)
		{
			VBoxContainer detailBox = new VBoxContainer();
			detailBox.Visible = false;
			detailBox.AddThemeConstantOverride("separation", 1);
			AddTooltipLines(detailBox, card.SynergyReasons, card.AntiSynergyReasons, card.Notes, card.BaseScore, card.SynergyDelta, card.FloorAdjust, card.DeckSizeAdjust, card.UpgradeAdjust, card.FinalScore, card.ScoreSource);
			if (detailBox.GetChildCount() > 0)
			{
				vBoxContainer.AddChild(detailBox, forceReadableName: false, Node.InternalMode.Disabled);
				ConnectHoverSignals(panelContainer,
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = true; },
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = false; });
			}
		}
		_content.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddRelicEntry(ScoredRelic relic)
	{
		PanelContainer panelContainer = CreateEntryPanel(relic.IsBestPick, relic.FinalGrade);
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 8);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		CenterContainer relicBadge = CreateBadge(relic.FinalGrade, relic.FinalScore);
		hBoxContainer.AddChild(relicBadge, forceReadableName: false, Node.InternalMode.Disabled);
		// V2: Pulse animation on best pick badge
		if (relic.IsBestPick)
		{
			CenterContainer pulseBadge = relicBadge;
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(pulseBadge))
				{
					var tween = pulseBadge.CreateTween();
					if (tween != null)
					{
						tween.SetLoops(2);
						tween.TweenProperty(pulseBadge, "modulate:a", 0.6f, 0.2f);
						tween.TweenProperty(pulseBadge, "modulate:a", 1.0f, 0.2f);
					}
				}
			}).CallDeferred();
		}
		// Relic icon thumbnail (rounded corners with border)
		Texture2D relicIcon = GetRelicIcon(relic.Id);
		if (relicIcon != null)
		{
			PanelContainer relicClip = new PanelContainer();
			relicClip.ClipContents = true;
			relicClip.CustomMinimumSize = new Vector2(36f, 36f);
			StyleBoxFlat relicThumbStyle = new StyleBoxFlat();
			relicThumbStyle.BgColor = new Color(0, 0, 0, 0);
			relicThumbStyle.CornerRadiusTopLeft = 6;
			relicThumbStyle.CornerRadiusTopRight = 6;
			relicThumbStyle.CornerRadiusBottomLeft = 6;
			relicThumbStyle.CornerRadiusBottomRight = 6;
			relicThumbStyle.BorderWidthTop = 2;
			relicThumbStyle.BorderWidthBottom = 2;
			relicThumbStyle.BorderWidthLeft = 2;
			relicThumbStyle.BorderWidthRight = 2;
			relicThumbStyle.BorderColor = new Color(ClrBorder, 0.8f);
			relicClip.AddThemeStyleboxOverride("panel", relicThumbStyle);
			TextureRect thumb = new TextureRect();
			thumb.Texture = relicIcon;
			thumb.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			thumb.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			thumb.CustomMinimumSize = new Vector2(36f, 36f);
			relicClip.AddChild(thumb, forceReadableName: false, Node.InternalMode.Disabled);
			hBoxContainer.AddChild(relicClip, forceReadableName: false, Node.InternalMode.Disabled);
		}
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vBoxContainer.AddThemeConstantOverride("separation", 0);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		// Relic name + sub-grade inline
		Label label = new Label();
		string text = relic.Name ?? PrettifyId(relic.Id);
		label.Text = text;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", relic.IsBestPick ? ClrAccent : ClrCream);
		label.AddThemeFontSizeOverride("font_size", 17);
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		// Meta: rarity • tenure • one-liner
		string rarityLower = relic.Rarity?.ToLowerInvariant() ?? "";
		string oneLiner = BuildOneLiner(relic.SynergyReasons, relic.AntiSynergyReasons, relic.BaseTier, relic.FinalGrade);
		string tenureStr = "";
		if (Plugin.RunTracker != null && _currentFloor > 0)
		{
			int tenure = Plugin.RunTracker.GetRelicTenure(relic.Id, _currentFloor);
			if (tenure > 0) tenureStr = $" \u2022 held {tenure} floors";
		}
		string metaText = rarityLower + tenureStr;
		if (oneLiner.Length > 0)
			metaText += $" \u2022 {oneLiner}";
		Label metaLbl = new Label();
		metaLbl.Text = metaText;
		ApplyFont(metaLbl, _fontBody);
		bool hasAnti = relic.AntiSynergyReasons != null && relic.AntiSynergyReasons.Count > 0;
		metaLbl.AddThemeColorOverride("font_color", hasAnti ? ClrNegative : ClrSub);
		metaLbl.AddThemeFontSizeOverride("font_size", 14);
		metaLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(metaLbl, forceReadableName: false, Node.InternalMode.Disabled);
		// Archetype synergy tags
		var archTags = ExtractArchetypeTags(relic.SynergyReasons);
		if (archTags.Count > 0)
		{
			HBoxContainer tagRow = new HBoxContainer();
			tagRow.AddThemeConstantOverride("separation", 4);
			foreach (string tag in archTags)
			{
				Label tagLbl = new Label();
				tagLbl.Text = $"[{tag}]";
				ApplyFont(tagLbl, _fontBody);
				tagLbl.AddThemeFontSizeOverride("font_size", 17);
				tagLbl.AddThemeColorOverride("font_color", ClrAccent);
				tagRow.AddChild(tagLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			vBoxContainer.AddChild(tagRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Price with gold icon
		if (relic.Price > 0)
		{
			if (_goldIcon != null)
			{
				HBoxContainer priceRow = new HBoxContainer();
				priceRow.AddThemeConstantOverride("separation", 2);
				TextureRect goldTex = new TextureRect();
				goldTex.Texture = _goldIcon;
				goldTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				goldTex.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
				goldTex.CustomMinimumSize = new Vector2(12f, 12f);
				priceRow.AddChild(goldTex, forceReadableName: false, Node.InternalMode.Disabled);
				Label priceLbl = new Label();
				priceLbl.Text = $"{relic.Price}";
				ApplyFont(priceLbl, _fontBody);
				priceLbl.AddThemeColorOverride("font_color", ClrAccent);
				priceLbl.AddThemeFontSizeOverride("font_size", 17);
				priceRow.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);
				vBoxContainer.AddChild(priceRow, forceReadableName: false, Node.InternalMode.Disabled);
			}
			else
			{
				Label pLbl = new Label();
				pLbl.Text = $"{relic.Price}g";
				ApplyFont(pLbl, _fontBody);
				pLbl.AddThemeColorOverride("font_color", ClrAccent);
				pLbl.AddThemeFontSizeOverride("font_size", 17);
				vBoxContainer.AddChild(pLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		// Expandable details on hover
		if (_showTooltips)
		{
			VBoxContainer detailBox = new VBoxContainer();
			detailBox.Visible = false;
			detailBox.AddThemeConstantOverride("separation", 1);
			AddTooltipLines(detailBox, relic.SynergyReasons, relic.AntiSynergyReasons, relic.Notes, relic.BaseScore, relic.SynergyDelta, relic.FloorAdjust, relic.DeckSizeAdjust, 0f, relic.FinalScore, relic.ScoreSource);
			if (detailBox.GetChildCount() > 0)
			{
				vBoxContainer.AddChild(detailBox, forceReadableName: false, Node.InternalMode.Disabled);
				ConnectHoverSignals(panelContainer,
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = true; },
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = false; });
			}
		}
		_content.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static List<string> ExtractArchetypeTags(List<string> synergyReasons)
	{
		var tags = new List<string>();
		if (synergyReasons == null) return tags;
		foreach (string reason in synergyReasons)
		{
			int idx = reason.IndexOf("synergy with ");
			if (idx >= 0)
			{
				string archName = reason.Substring(idx + 13).Trim();
				if (archName.Length > 0 && !tags.Contains(archName))
					tags.Add(archName);
			}
		}
		return tags;
	}

	private static string BuildOneLiner(List<string> synergies, List<string> antiSynergies, TierGrade baseTier, TierGrade finalGrade)
	{
		// Priority: show most impactful single reason in plain English
		if (antiSynergies != null && antiSynergies.Count > 0)
		{
			string anti = antiSynergies[0];
			if (anti.IndexOf("conflict") >= 0) return "덱과 충돌";
			if (anti.IndexOf("expensive") >= 0) return "현재 비용 과다";
			if (anti.IndexOf("redundant") >= 0) return "중복 선택";
			return "덱에 맞지 않음";
		}
		if (synergies != null && synergies.Count > 0)
		{
			string syn = synergies[0];
			if (syn.IndexOf("synergy with ") >= 0)
			{
				// Extract archetype name for specificity
				string arch = syn.Substring(syn.IndexOf("synergy with ") + 13).Trim();
				if (arch.Length > 0 && arch.Length <= 20) return $"synergizes with {arch}";
				return "덱과 잘 맞음";
			}
			if (syn.IndexOf("fills gap: ") >= 0) return "추가: " + syn.Substring(syn.IndexOf("fills gap: ") + 11).Trim();
			if (syn.IndexOf("scaling") >= 0) return "후반 스케일링 우수";
			if (syn.IndexOf("flexible") >= 0) return "다용도 선택";
			if (syn.IndexOf("defense") >= 0) return "방어 보완";
			if (syn.IndexOf("upgraded") >= 0) return "업그레이드됨";
			if (syn.IndexOf("aoe") >= 0 || syn.IndexOf("AoE") >= 0) return "좋은 광역기";
			if (syn.IndexOf("draw") >= 0) return "카드 드로우 추가";
			if (syn.IndexOf("energy") >= 0) return "에너지 효율적";
			return syn.Length > 25 ? syn.Substring(0, 25) + "..." : syn;
		}
		if (finalGrade != baseTier)
		{
			return finalGrade > baseTier ? "현재 강함" : "현재 약함";
		}
		return "";
	}

	private void AddTooltipLines(VBoxContainer parent, List<string> synergies, List<string> antiSynergies, string notes, float baseScore = 0f, float synergyDelta = 0f, float floorAdjust = 0f, float deckSizeAdjust = 0f, float upgradeAdjust = 0f, float finalScore = 0f, string scoreSource = "static")
	{
		bool hasContent = false;

		// Card description / notes first — what does this card do?
		if (!string.IsNullOrEmpty(notes) && !IsFillerNote(notes))
		{
			Label lbl = new Label();
			lbl.Text = notes;
			ApplyFont(lbl, _fontBody);
			lbl.AddThemeColorOverride("font_color", ClrNotes);
			lbl.AddThemeFontSizeOverride("font_size", 14);
			lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			parent.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
			hasContent = true;
		}

		// Synergy / anti-synergy reasons — why is it rated this way?
		if (synergies != null && synergies.Count > 0)
		{
			foreach (string reason in synergies.Take(3))
			{
				string clean = CleanSynergyText(reason);
				if (string.IsNullOrEmpty(clean)) continue;
				Label lbl = new Label();
				lbl.Text = "\u2714 " + clean;
				ApplyFont(lbl, _fontBody);
				lbl.AddThemeColorOverride("font_color", ClrPositive);
				lbl.AddThemeFontSizeOverride("font_size", 14);
				lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				parent.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
				hasContent = true;
			}
		}

		if (antiSynergies != null && antiSynergies.Count > 0)
		{
			foreach (string reason in antiSynergies.Take(2))
			{
				string clean = CleanSynergyText(reason);
				if (string.IsNullOrEmpty(clean)) continue;
				Label lbl = new Label();
				lbl.Text = "\u2718 " + clean;
				ApplyFont(lbl, _fontBody);
				lbl.AddThemeColorOverride("font_color", ClrNegative);
				lbl.AddThemeFontSizeOverride("font_size", 14);
				lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				parent.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
				hasContent = true;
			}
		}

		// Score source hint — only show if learned from play data
		if (scoreSource == "adaptive")
		{
			Label srcLbl = new Label();
			srcLbl.Text = "\u2139 내 플레이 데이터 기반 평가";
			ApplyFont(srcLbl, _fontBody);
			srcLbl.AddThemeColorOverride("font_color", ClrAqua);
			srcLbl.AddThemeFontSizeOverride("font_size", 12);
			parent.AddChild(srcLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	/// <summary>
	/// Strip numeric scoring jargon from synergy/anti-synergy reason strings.
	/// "+0.8 synergy with Minion / Summoner" → "synergy with Minion / Summoner"
	/// </summary>
	private static string CleanSynergyText(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		// Strip leading "+0.8 " or "-0.4 " numeric prefixes
		string cleaned = text;
		if (cleaned.Length > 2 && (cleaned[0] == '+' || cleaned[0] == '-') && char.IsDigit(cleaned[1]))
		{
			int spaceIdx = cleaned.IndexOf(' ');
			if (spaceIdx > 0 && spaceIdx < 8)
				cleaned = cleaned.Substring(spaceIdx + 1);
		}
		// Capitalize first letter
		if (cleaned.Length > 0)
			cleaned = char.ToUpper(cleaned[0]) + cleaned.Substring(1);
		return cleaned;
	}

	/// <summary>
	/// Filter out notes that just describe card type/rarity/cost (already shown in meta line).
	/// </summary>
	private static bool IsFillerNote(string notes)
	{
		string lower = notes.ToLowerInvariant();
		// Skip notes like "1-cost common strike variant", "Filler attack with Strike tag"
		if (lower.Contains("filler")) return true;
		if (lower.Contains("-cost") && lower.Contains("variant")) return true;
		if (lower.Contains("basic") && lower.Contains("strike")) return true;
		if (lower.Contains("basic") && lower.Contains("defend")) return true;
		if (lower.Contains("starter card")) return true;
		return false;
	}

	private void AddSkipEntry(string title, string reasoning)
	{
		PanelContainer panelContainer = CreateEntryPanel(isBest: false);
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 16);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(CreateSkipBadge(), forceReadableName: false, Node.InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label();
		label.Text = title;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", ClrSkip);
		label.AddThemeFontSizeOverride("font_size", 17);
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		Label label2 = new Label();
		label2.Text = reasoning;
		ApplyFont(label2, _fontBody);
		label2.AddThemeColorOverride("font_color", ClrSkipSub);
		label2.AddThemeFontSizeOverride("font_size", 17);
		label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private PanelContainer CreateEntryPanel(bool isBest, TierGrade grade = TierGrade.C)
	{
		PanelContainer panel = new PanelContainer();
		bool isSTier = grade == TierGrade.S && isBest;
		StyleBoxFlat normalStyle = isSTier ? (_sbSTier ?? _sbBest ?? _sbEntry) :
			(isBest ? _sbBest : _sbEntry) ?? _sbEntry;
		StyleBoxFlat hoverStyle = isSTier ? (_sbSTierHover ?? _sbHoverBest ?? _sbEntry) :
			(isBest ? _sbHoverBest : _sbHover) ?? _sbEntry;
		panel.AddThemeStyleboxOverride("panel", normalStyle);
		panel.MouseFilter = Control.MouseFilterEnum.Stop;
		ConnectHoverSignals(panel,
			() => { if (GodotObject.IsInstanceValid(panel)) panel.AddThemeStyleboxOverride("panel", hoverStyle); },
			() => { if (GodotObject.IsInstanceValid(panel)) panel.AddThemeStyleboxOverride("panel", normalStyle); });
		// S-tier pulsing golden glow
		if (isSTier)
		{
			PanelContainer glowPanel = panel;
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(glowPanel))
				{
					var tween = glowPanel.CreateTween();
					if (tween != null)
					{
						tween.SetLoops(0); // infinite
						tween.TweenProperty(glowPanel, "self_modulate:a", 0.75f, 0.6f)
							.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
						tween.TweenProperty(glowPanel, "self_modulate:a", 1.0f, 0.6f)
							.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
					}
				}
			}).CallDeferred();
		}
		return panel;
	}

	private CenterContainer CreateBadge(TierGrade grade, float score = -1f)
	{
		string subGrade = score >= 0f ? TierEngine.ScoreToSubGrade(score) : grade.ToString();
		CenterContainer obj = new CenterContainer
		{
			CustomMinimumSize = new Vector2(34f, 34f)
		};
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(subGrade.Length > 1 ? 38f : 30f, 30f)
		};
		Color badgeColor = TierBadge.GetGodotColor(grade);
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = badgeColor
		};
		styleBoxFlat.CornerRadiusTopLeft = 0;
		styleBoxFlat.CornerRadiusTopRight = 10;
		styleBoxFlat.CornerRadiusBottomLeft = 10;
		styleBoxFlat.CornerRadiusBottomRight = 0;
		styleBoxFlat.BorderWidthTop = 1;
		styleBoxFlat.BorderWidthBottom = 1;
		styleBoxFlat.BorderWidthLeft = 1;
		styleBoxFlat.BorderWidthRight = 1;
		styleBoxFlat.BorderColor = badgeColor.Darkened(0.3f);
		panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
		Label label = new Label();
		label.Text = subGrade;
		ApplyFont(label, _fontHeader);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_color", TierBadge.GetTextColor(grade));
		label.AddThemeFontSizeOverride("font_size", subGrade.Length > 1 ? 17 : 20);
		label.AddThemeConstantOverride("outline_size", 0);
		panelContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		obj.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	private CenterContainer CreateSkipBadge()
	{
		CenterContainer obj = new CenterContainer
		{
			CustomMinimumSize = new Vector2(34f, 34f)
		};
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(30f, 30f)
		};
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = new Color(0.2f, 0.15f, 0.3f)
		};
		styleBoxFlat.CornerRadiusTopLeft = 0;
		styleBoxFlat.CornerRadiusTopRight = 10;
		styleBoxFlat.CornerRadiusBottomLeft = 10;
		styleBoxFlat.CornerRadiusBottomRight = 0;
		styleBoxFlat.BorderWidthTop = 1;
		styleBoxFlat.BorderWidthBottom = 1;
		styleBoxFlat.BorderWidthLeft = 1;
		styleBoxFlat.BorderWidthRight = 1;
		styleBoxFlat.BorderColor = ClrSkip;
		panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
		Label label = new Label();
		label.Text = "\u2014";
		ApplyFont(label, _fontHeader);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_color", ClrSkip);
		label.AddThemeFontSizeOverride("font_size", 22);
		panelContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		obj.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	// === V1: Color-coded title separator ===

	private static Color GetScreenColor(string screen)
	{
		return screen switch
		{
			"CARD REWARD" or "CARD REMOVAL" or "CARD UPGRADE" => ClrAccent,
			"이벤트 카드 제공" => ClrSkip,
			"RELIC REWARD" => ClrPositive,
			"MERCHANT SHOP" => ClrExpensive,
			"COMBAT" => ClrNegative,
			"MAP" or "MAP / COMBAT" => ClrAqua,
			"EVENT" => ClrSkip,
			"REST SITE" => ClrPositive,
			_ when screen != null && screen.Contains("RUN") => ClrAccent,
			_ => ClrBorder,
		};
	}

	private void UpdateTitleSepColor()
	{
		if (_titleSep == null || !GodotObject.IsInstanceValid(_titleSep))
			return;
		Color sepColor = GetScreenColor(_currentScreen);
		_titleSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(sepColor, 0.8f), Thickness = 2 });
	}
}
