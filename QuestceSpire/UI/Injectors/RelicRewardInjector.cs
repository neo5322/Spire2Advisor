using System;
using System.Collections.Generic;
using Godot;
using QuestceSpire.Core;

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

		bool isFirst = true;
		foreach (var relic in _relics)
		{
			bool isBest = isFirst && relic.FinalGrade >= TierGrade.B;
			var entry = CreateCompactRelicEntry(relic, isBest);
			Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
			isFirst = false;
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
}
