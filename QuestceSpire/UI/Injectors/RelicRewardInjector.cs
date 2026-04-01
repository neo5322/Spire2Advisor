using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Relic reward screen injector — shows scored relic recommendations.
/// Compact: grade badge + name. Expanded: score bars, synergy details.
/// </summary>
public class RelicRewardInjector : BaseScreenInjector
{
	public override string ScreenName => "RELIC REWARD";

	private List<ScoredRelic> _relics;
	private DeckAnalysis _deckAnalysis;
	private string _character;
	private int _currentHP, _maxHP;

	public RelicRewardInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredRelic> relics, DeckAnalysis deckAnalysis, string character,
		int currentHP = 0, int maxHP = 0)
	{
		_relics = relics;
		_deckAnalysis = deckAnalysis;
		_character = character;
		_currentHP = currentHP;
		_maxHP = maxHP;

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_relics == null || _relics.Count == 0)
		{
			AddAdviceTip("\u2714", "유물 정보 없음", SharedResources.ClrSub);
			return;
		}

		AddSectionHeader("유물 추천");

		// Get build relic list for the active archetype
		List<string> buildRelics = null;
		try
		{
			if (Plugin.CommunityData != null && Plugin.CommunityData.IsLoaded && _deckAnalysis?.CommunityArchetype != null)
			{
				var archetype = _deckAnalysis.CommunityArchetype;
				string archetypeId = archetype?.Id;
				if (!string.IsNullOrEmpty(archetypeId))
					buildRelics = Plugin.CommunityData.GetBuildRelics(archetypeId);
			}
		}
		catch (Exception ex) { Plugin.Log($"RelicRewardInjector: build relic lookup error: {ex.Message}"); }

		bool isFirst = true;
		foreach (var relic in _relics)
		{
			bool isBest = isFirst && relic.FinalGrade >= TierGrade.B;
			var entry = CreateCompactRelicEntry(relic, isBest);

			// Build relic chip
			AddBuildRelicChip(entry, relic, buildRelics);

			Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
			isFirst = false;
		}

		// Build completion context
		if (_deckAnalysis?.CommunityArchetype != null)
		{
			try
			{
				var archetype = _deckAnalysis.CommunityArchetype;
				string archName = archetype?.Name;
				if (!string.IsNullOrEmpty(archName))
				{
					var buildLbl = new Label();
					buildLbl.Text = $"\u2726 빌드: {archName}";
					OverlayStyles.StyleLabel(buildLbl, Res.FontBody, OverlayTheme.FontCaption, OverlayTheme.Info);
					Content.AddChild(buildLbl, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
			catch (Exception ex) { Plugin.Log($"RelicRewardInjector: archetype display error: {ex.Message}"); }
		}

		// Deck context (expanded only)
		if (IsExpanded && Settings.ShowDeckBreakdown && _deckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			var summaryLbl = new Label();
			summaryLbl.Text = $"카드 {_deckAnalysis.TotalCards}장";
			if (_deckAnalysis.DetectedArchetypes?.Count > 0)
				summaryLbl.Text += $" — {_deckAnalysis.DetectedArchetypes[0].Archetype.DisplayName} ({_deckAnalysis.DetectedArchetypes[0].Strength:P0})";
			OverlayStyles.StyleLabel(summaryLbl, Res.FontBody, OverlayTheme.FontBody, SharedResources.ClrCream);
			Content.AddChild(summaryLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}

		// HP bar at bottom
		if (_maxHP > 0)
		{
			Content.AddChild(CreateHpBar(_currentHP, _maxHP), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private void AddBuildRelicChip(PanelContainer entry, ScoredRelic relic, List<string> buildRelics)
	{
		try
		{
			if (buildRelics == null || buildRelics.Count == 0) return;

			// Check if the relic name or localized name matches any build relic description
			string relicName = relic.Name ?? relic.Id;
			var koreanName = GameStateReader.GetLocalizedName("relic", relic.Id);
			bool isBuildRelic = buildRelics.Any(br =>
				br.Contains(relicName, StringComparison.OrdinalIgnoreCase) ||
				(koreanName != null && br.Contains(koreanName, StringComparison.OrdinalIgnoreCase)));

			if (!isBuildRelic) return;

			var vbox = entry.GetChild(0) as VBoxContainer;
			if (vbox == null) return;

			// Build relic chip
			var chipRow = new HBoxContainer();
			chipRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

			var chipPanel = new PanelContainer();
			var chipStyle = new StyleBoxFlat();
			chipStyle.BgColor = new Color(OverlayTheme.Positive, 0.15f);
			OverlayStyles.SetAllCornerRadius(chipStyle, OverlayTheme.RadiusSM);
			chipStyle.ContentMarginLeft = chipStyle.ContentMarginRight = 6f;
			chipStyle.ContentMarginTop = chipStyle.ContentMarginBottom = 1f;
			OverlayStyles.SetAllBorderWidth(chipStyle, 1);
			chipStyle.BorderColor = new Color(OverlayTheme.Positive, 0.3f);
			chipPanel.AddThemeStyleboxOverride("panel", chipStyle);

			var chipLbl = new Label();
			chipLbl.Text = "빌드 유물";
			OverlayStyles.StyleLabel(chipLbl, Res.FontBody, OverlayTheme.FontCaption, OverlayTheme.Positive);
			chipPanel.AddChild(chipLbl, forceReadableName: false, Node.InternalMode.Disabled);
			chipRow.AddChild(chipPanel, forceReadableName: false, Node.InternalMode.Disabled);

			vbox.AddChild(chipRow, forceReadableName: false, Node.InternalMode.Disabled);

			// Show matching build relic description
			string matchingDesc = buildRelics.FirstOrDefault(br =>
				br.Contains(relicName, StringComparison.OrdinalIgnoreCase) ||
				(koreanName != null && br.Contains(koreanName, StringComparison.OrdinalIgnoreCase)));
			if (!string.IsNullOrEmpty(matchingDesc))
			{
				var descLbl = new Label();
				descLbl.Text = matchingDesc;
				OverlayStyles.StyleLabel(descLbl, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrNotes);
				descLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				vbox.AddChild(descLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"RelicRewardInjector.AddBuildRelicChip error: {ex.Message}");
		}
	}
}
