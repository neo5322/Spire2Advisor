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
	// Archetype bar colors — reference centralized theme tokens
	private static Color[] ArchColors => OverlayTheme.ArchColors;

	private void AddSectionHeader(string text)
	{
		// Separator before header (except first section)
		if (_content.GetChildCount() > 0)
		{
			HSeparator sep = new HSeparator();
			sep.AddThemeStyleboxOverride("separator", OverlayStyles.CreateSeparatorStyle());
			_content.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
		}

		// Header with left accent bar
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);

		// Accent bar (3px wide, matches section color)
		ColorRect accentBar = new ColorRect();
		accentBar.Color = ClrAccent;
		accentBar.CustomMinimumSize = new Vector2(3f, 0f);
		accentBar.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		headerRow.AddChild(accentBar, forceReadableName: false, Node.InternalMode.Disabled);

		Label label = new Label();
		label.Text = text;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", ClrAccent);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH1);
		label.AddThemeConstantOverride("outline_size", 3);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerRow.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);

		_content.AddChild(headerRow, forceReadableName: false, Node.InternalMode.Disabled);
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
		arrow.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		arrow.MouseFilter = Control.MouseFilterEnum.Ignore;
		headerRow.AddChild(arrow, forceReadableName: false, Node.InternalMode.Disabled);
		Label headerLabel = new Label();
		headerLabel.Text = text;
		ApplyFont(headerLabel, _fontBold);
		headerLabel.AddThemeColorOverride("font_color", ClrAccent);
		headerLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
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
			Label toggleLabel = arrow;
			headerRow.Connect("gui_input", Callable.From((InputEvent ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					// Brief flash feedback on toggle arrow
					if (GodotObject.IsInstanceValid(toggleLabel))
					{
						var flashTween = toggleLabel.CreateTween();
						flashTween?.TweenProperty(toggleLabel, "modulate", new Color(1.3f, 1.3f, 1.3f, 1f), 0.1f);
						flashTween?.TweenProperty(toggleLabel, "modulate", Colors.White, 0.15f);
					}
					ToggleSectionSetting(localKey, true);
					Rebuild();
				}
			}));
			return null;
		}
		// Section is expanded — add content container
		VBoxContainer sectionContent = new VBoxContainer();
		sectionContent.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
		_content.AddChild(sectionContent, forceReadableName: false, Node.InternalMode.Disabled);
		// Click to collapse
		string collapseKey = sectionKey;
		Label collapseToggleLabel = arrow;
		headerRow.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				// Brief flash feedback on toggle arrow
				if (GodotObject.IsInstanceValid(collapseToggleLabel))
				{
					var flashTween = collapseToggleLabel.CreateTween();
					flashTween?.TweenProperty(collapseToggleLabel, "modulate", new Color(1.3f, 1.3f, 1.3f, 1f), 0.1f);
					flashTween?.TweenProperty(collapseToggleLabel, "modulate", Colors.White, 0.15f);
				}
				ToggleSectionSetting(collapseKey, false);
				Rebuild();
			}
		}));
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
		hBoxContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
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
		// Card portrait thumbnail (rounded corners with border + inner shadow)
		Texture2D portrait = GetCardPortrait(card.Id, _currentCharacter);
		if (portrait != null)
		{
			PanelContainer thumbClip = new PanelContainer();
			thumbClip.ClipContents = true;
			thumbClip.CustomMinimumSize = new Vector2(OverlayTheme.ThumbnailSize, OverlayTheme.ThumbnailSize);
			thumbClip.AddThemeStyleboxOverride("panel", OverlayStyles.CreateThumbnailStyle(card.IsBestPick));
			TextureRect thumb = new TextureRect();
			thumb.Texture = portrait;
			thumb.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			thumb.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			thumb.CustomMinimumSize = new Vector2(OverlayTheme.ThumbnailSize, OverlayTheme.ThumbnailSize);
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
		vBoxContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		// Card name + sub-grade inline
		Label label = new Label();
		string text = card.Name ?? PrettifyId(card.Id);
		string upgradeTag = card.Upgraded ? " +" : "";
		label.Text = $"{text}{upgradeTag}";
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", card.IsBestPick ? ClrAccent : ClrCream);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		// Patch change badge
		string patchBadge = GetPatchChangeBadge("card", card.Id);
		if (patchBadge != null)
		{
			Label patchLbl = new Label();
			patchLbl.Text = patchBadge;
			ApplyFont(patchLbl, _fontBody);
			patchLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			patchLbl.AddThemeColorOverride("font_color", ClrExpensive);
			vBoxContainer.AddChild(patchLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Floor tier info
		if (_currentGameState != null && _currentCharacter != null)
		{
			string floorInfo = GetFloorTierInfo(card.Id, _currentCharacter, _currentGameState.ActNumber);
			if (floorInfo != null)
			{
				Label floorLbl = new Label();
				floorLbl.Text = floorInfo;
				ApplyFont(floorLbl, _fontBody);
				floorLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				floorLbl.AddThemeColorOverride("font_color", ClrAqua);
				vBoxContainer.AddChild(floorLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
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
		metaLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		metaLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(metaLbl, forceReadableName: false, Node.InternalMode.Disabled);
		// Shop price with gold icon (inline after meta)
		if (card.Price > 0 && _goldIcon != null)
		{
			HBoxContainer priceRow = new HBoxContainer();
			priceRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
			TextureRect goldTex = new TextureRect();
			goldTex.Texture = _goldIcon;
			goldTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			goldTex.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
			goldTex.CustomMinimumSize = new Vector2(OverlayTheme.GoldIconSize, OverlayTheme.GoldIconSize);
			priceRow.AddChild(goldTex, forceReadableName: false, Node.InternalMode.Disabled);
			Label priceLbl = new Label();
			priceLbl.Text = $"{card.Price}";
			ApplyFont(priceLbl, _fontBody);
			priceLbl.AddThemeColorOverride("font_color", ClrAccent);
			priceLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
			priceRow.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);
			vBoxContainer.AddChild(priceRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Expandable details on hover (hidden by default)
		if (_showTooltips)
		{
			VBoxContainer detailBox = new VBoxContainer();
			detailBox.Visible = false;
			detailBox.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
			AddTooltipLines(detailBox, card.SynergyReasons, card.AntiSynergyReasons, card.Notes, card.BaseScore, card.SynergyDelta, card.FloorAdjust, card.DeckSizeAdjust, card.UpgradeAdjust, card.FinalScore, card.ScoreSource);
			if (detailBox.GetChildCount() > 0)
			{
				vBoxContainer.AddChild(detailBox, forceReadableName: false, Node.InternalMode.Disabled);
				ConnectHoverSignals(panelContainer,
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = true; },
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = false; });
			}
		}
		// Score bar — thin visual indicator of card strength
		vBoxContainer.AddChild(CreateScoreBar(card.FinalScore, card.FinalGrade), forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddRelicEntry(ScoredRelic relic)
	{
		PanelContainer panelContainer = CreateEntryPanel(relic.IsBestPick, relic.FinalGrade);
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
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
		// Relic icon thumbnail (rounded corners with border + inner shadow)
		Texture2D relicIcon = GetRelicIcon(relic.Id);
		if (relicIcon != null)
		{
			PanelContainer relicClip = new PanelContainer();
			relicClip.ClipContents = true;
			relicClip.CustomMinimumSize = new Vector2(OverlayTheme.ThumbnailSize, OverlayTheme.ThumbnailSize);
			relicClip.AddThemeStyleboxOverride("panel", OverlayStyles.CreateThumbnailStyle(relic.IsBestPick));
			TextureRect thumb = new TextureRect();
			thumb.Texture = relicIcon;
			thumb.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			thumb.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
			thumb.CustomMinimumSize = new Vector2(OverlayTheme.ThumbnailSize, OverlayTheme.ThumbnailSize);
			relicClip.AddChild(thumb, forceReadableName: false, Node.InternalMode.Disabled);
			hBoxContainer.AddChild(relicClip, forceReadableName: false, Node.InternalMode.Disabled);
		}
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vBoxContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		// Relic name + sub-grade inline
		Label label = new Label();
		string text = relic.Name ?? PrettifyId(relic.Id);
		label.Text = text;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", relic.IsBestPick ? ClrAccent : ClrCream);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		// Patch change badge for relic
		string relicPatchBadge = GetPatchChangeBadge("relic", relic.Id);
		if (relicPatchBadge != null)
		{
			Label relicPatchLbl = new Label();
			relicPatchLbl.Text = relicPatchBadge;
			ApplyFont(relicPatchLbl, _fontBody);
			relicPatchLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			relicPatchLbl.AddThemeColorOverride("font_color", ClrExpensive);
			vBoxContainer.AddChild(relicPatchLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
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
		metaLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		metaLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(metaLbl, forceReadableName: false, Node.InternalMode.Disabled);
		// Archetype synergy tags (pill badges)
		var archTags = ExtractArchetypeTags(relic.SynergyReasons);
		if (archTags.Count > 0)
		{
			HBoxContainer tagRow = new HBoxContainer();
			tagRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
			int tagColorIdx = 0;
			foreach (string tag in archTags)
			{
				Color tagColor = ArchColors[tagColorIdx % ArchColors.Length];
				PanelContainer tagChip = new PanelContainer();
				tagChip.AddThemeStyleboxOverride("panel", OverlayStyles.CreateArchTagChipStyle(tagColor));
				Label tagLbl = new Label();
				tagLbl.Text = tag;
				ApplyFont(tagLbl, _fontBody);
				tagLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				tagLbl.AddThemeColorOverride("font_color", tagColor);
				tagLbl.MouseFilter = Control.MouseFilterEnum.Ignore;
				tagChip.AddChild(tagLbl, forceReadableName: false, Node.InternalMode.Disabled);
				tagRow.AddChild(tagChip, forceReadableName: false, Node.InternalMode.Disabled);
				tagColorIdx++;
			}
			vBoxContainer.AddChild(tagRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Price with gold icon
		if (relic.Price > 0)
		{
			if (_goldIcon != null)
			{
				HBoxContainer priceRow = new HBoxContainer();
				priceRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
				TextureRect goldTex = new TextureRect();
				goldTex.Texture = _goldIcon;
				goldTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				goldTex.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
				goldTex.CustomMinimumSize = new Vector2(OverlayTheme.GoldIconSize, OverlayTheme.GoldIconSize);
				priceRow.AddChild(goldTex, forceReadableName: false, Node.InternalMode.Disabled);
				Label priceLbl = new Label();
				priceLbl.Text = $"{relic.Price}";
				ApplyFont(priceLbl, _fontBody);
				priceLbl.AddThemeColorOverride("font_color", ClrAccent);
				priceLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
				priceRow.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);
				vBoxContainer.AddChild(priceRow, forceReadableName: false, Node.InternalMode.Disabled);
			}
			else
			{
				Label pLbl = new Label();
				pLbl.Text = $"{relic.Price}g";
				ApplyFont(pLbl, _fontBody);
				pLbl.AddThemeColorOverride("font_color", ClrAccent);
				pLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
				vBoxContainer.AddChild(pLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		// Expandable details on hover
		if (_showTooltips)
		{
			VBoxContainer detailBox = new VBoxContainer();
			detailBox.Visible = false;
			detailBox.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);
			AddTooltipLines(detailBox, relic.SynergyReasons, relic.AntiSynergyReasons, relic.Notes, relic.BaseScore, relic.SynergyDelta, relic.FloorAdjust, relic.DeckSizeAdjust, 0f, relic.FinalScore, relic.ScoreSource);
			if (detailBox.GetChildCount() > 0)
			{
				vBoxContainer.AddChild(detailBox, forceReadableName: false, Node.InternalMode.Disabled);
				ConnectHoverSignals(panelContainer,
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = true; },
					() => { if (GodotObject.IsInstanceValid(detailBox)) detailBox.Visible = false; });
			}
		}
		// Score bar — thin visual indicator of relic strength
		vBoxContainer.AddChild(CreateScoreBar(relic.FinalScore, relic.FinalGrade), forceReadableName: false, Node.InternalMode.Disabled);
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
			lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
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
				lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
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
				lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
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
			srcLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
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
		hBoxContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceXL);
		panelContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		hBoxContainer.AddChild(CreateSkipBadge(), forceReadableName: false, Node.InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hBoxContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label();
		label.Text = title;
		ApplyFont(label, _fontBold);
		label.AddThemeColorOverride("font_color", ClrSkip);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		vBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		Label label2 = new Label();
		label2.Text = reasoning;
		ApplyFont(label2, _fontBody);
		label2.AddThemeColorOverride("font_color", ClrSkipSub);
		label2.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		vBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
		_content.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private Control CreateScoreBar(float score, TierGrade grade)
	{
		float ratio = Mathf.Clamp(score / 5.0f, 0f, 1f);
		Color barColor = TierBadge.GetGodotColor(grade);

		HBoxContainer bar = new HBoxContainer();
		bar.CustomMinimumSize = new Vector2(0, OverlayTheme.ScoreBarHeight);
		bar.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bar.AddThemeConstantOverride("separation", 0);

		if (ratio > 0.01f)
		{
			Panel fill = new Panel();
			StyleBoxFlat fillStyle = new StyleBoxFlat();
			fillStyle.BgColor = new Color(barColor, OverlayTheme.OpScoreBarFill);
			fillStyle.CornerRadiusTopLeft = fillStyle.CornerRadiusBottomLeft = 2;
			if (ratio >= 0.99f)
				fillStyle.CornerRadiusTopRight = fillStyle.CornerRadiusBottomRight = 2;
			// Subtle top border glow for stronger scores
			if (ratio > 0.3f)
			{
				fillStyle.BorderWidthTop = 1;
				fillStyle.BorderColor = new Color(barColor, 0.3f);
			}
			fill.AddThemeStyleboxOverride("panel", fillStyle);
			fill.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			fill.SizeFlagsStretchRatio = ratio;
			fill.CustomMinimumSize = new Vector2(0, OverlayTheme.ScoreBarHeight);
			bar.AddChild(fill, forceReadableName: false, Node.InternalMode.Disabled);
		}

		if (ratio < 0.99f)
		{
			Panel empty = new Panel();
			StyleBoxFlat emptyStyle = new StyleBoxFlat();
			emptyStyle.BgColor = OverlayTheme.BgScoreBarEmpty;
			emptyStyle.CornerRadiusTopRight = emptyStyle.CornerRadiusBottomRight = 2;
			if (ratio <= 0.01f)
				emptyStyle.CornerRadiusTopLeft = emptyStyle.CornerRadiusBottomLeft = 2;
			empty.AddThemeStyleboxOverride("panel", emptyStyle);
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.SizeFlagsStretchRatio = 1.0f - ratio;
			empty.CustomMinimumSize = new Vector2(0, OverlayTheme.ScoreBarHeight);
			bar.AddChild(empty, forceReadableName: false, Node.InternalMode.Disabled);
		}

		return bar;
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
			() =>
			{
				if (!GodotObject.IsInstanceValid(panel)) return;
				panel.AddThemeStyleboxOverride("panel", hoverStyle);
				var t = panel.CreateTween();
				t?.TweenProperty(panel, "self_modulate", new Color(1.12f, 1.12f, 1.18f, 1f), 0.12f)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			},
			() =>
			{
				if (!GodotObject.IsInstanceValid(panel)) return;
				panel.AddThemeStyleboxOverride("panel", normalStyle);
				var t = panel.CreateTween();
				t?.TweenProperty(panel, "self_modulate", Colors.White, 0.15f)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			});
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
			CustomMinimumSize = new Vector2(OverlayTheme.BadgeSize, OverlayTheme.BadgeSize)
		};
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(subGrade.Length > 1 ? OverlayTheme.BadgeInnerWide : OverlayTheme.BadgeInnerDefault, OverlayTheme.BadgeInnerDefault)
		};
		panelContainer.AddThemeStyleboxOverride("panel", OverlayStyles.CreateBadgeStyle(grade));
		Label label = new Label();
		label.Text = subGrade;
		ApplyFont(label, _fontHeader);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_color", TierBadge.GetTextColor(grade));
		label.AddThemeFontSizeOverride("font_size", subGrade.Length > 1 ? OverlayTheme.FontBadgeSmall : OverlayTheme.FontBadgeLarge);
		label.AddThemeConstantOverride("outline_size", 0);
		panelContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		obj.AddChild(panelContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	private CenterContainer CreateSkipBadge()
	{
		CenterContainer obj = new CenterContainer
		{
			CustomMinimumSize = new Vector2(OverlayTheme.BadgeSize, OverlayTheme.BadgeSize)
		};
		PanelContainer panelContainer = new PanelContainer
		{
			CustomMinimumSize = new Vector2(OverlayTheme.BadgeInnerDefault, OverlayTheme.BadgeInnerDefault)
		};
		panelContainer.AddThemeStyleboxOverride("panel", OverlayStyles.CreateSkipBadgeStyle());
		Label label = new Label();
		label.Text = "\u2014";
		ApplyFont(label, _fontHeader);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeColorOverride("font_color", ClrSkip);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSkipBadge);
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
