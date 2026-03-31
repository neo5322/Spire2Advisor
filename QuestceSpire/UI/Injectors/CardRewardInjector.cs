using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Card reward/upgrade/removal screen injector — shows scored card recommendations.
/// Handles: CARD REWARD, CARD UPGRADE, CARD REMOVAL, EVENT CARD OFFER screens.
/// Compact: grade badge + type icon + name per line, skip greyed out.
/// Expanded: score bars, synergy details, upgrade deltas.
/// </summary>
public class CardRewardInjector : BaseScreenInjector
{
	private string _screenName = "CARD REWARD";
	public override string ScreenName => _screenName;

	private List<ScoredCard> _cards;
	private DeckAnalysis _deckAnalysis;
	private string _character;
	private int _currentHP, _maxHP;

	public CardRewardInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredCard> cards, DeckAnalysis deckAnalysis, string character,
		string screenLabel = "CARD REWARD", int currentHP = 0, int maxHP = 0)
	{
		_cards = cards;
		_deckAnalysis = deckAnalysis;
		_character = character;
		_screenName = screenLabel;
		_currentHP = currentHP;
		_maxHP = maxHP;

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_cards == null || _cards.Count == 0)
		{
			AddAdviceTip("\u2714", "카드 정보 없음", SharedResources.ClrSub);
			return;
		}

		string headerText = _screenName switch
		{
			"CARD UPGRADE" => "업그레이드",
			"CARD REMOVAL" => "제거",
			"EVENT CARD OFFER" => "이벤트 카드",
			_ => "카드 추천"
		};
		AddSectionHeader(headerText);

		bool isFirst = true;
		foreach (var card in _cards)
		{
			bool isBest = isFirst && card.FinalGrade >= TierGrade.B;
			var entry = CreateCompactCardEntry(card, isBest);

			// Upgrade delta info (expanded, for upgrade screens)
			if (IsExpanded && card.UpgradeDelta > 0.5f)
			{
				AddUpgradeDeltaToEntry(entry, card.UpgradeDelta);
			}

			Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
			isFirst = false;
		}

		// Skip recommendation
		if (_screenName == "CARD REWARD" && _cards.Count > 0 && _cards[0].FinalGrade <= TierGrade.C)
		{
			Content.AddChild(CreateSkipEntry("낮은 등급 — 스킵 고려"), forceReadableName: false, Node.InternalMode.Disabled);
		}

		// Deck summary (expanded only)
		if (IsExpanded && Settings.ShowDeckBreakdown && _deckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			var summaryLbl = new Label();
			summaryLbl.Text = $"카드 {_deckAnalysis.TotalCards}장";
			if (_deckAnalysis.DetectedArchetypes?.Count > 0)
				summaryLbl.Text += $" — {_deckAnalysis.DetectedArchetypes[0].Archetype.DisplayName} ({_deckAnalysis.DetectedArchetypes[0].Strength:P0})";
			OverlayStyles.StyleLabel(summaryLbl, Res.FontBody, OverlayTheme.FontBody, SharedResources.ClrCream);
			Content.AddChild(summaryLbl, forceReadableName: false, Node.InternalMode.Disabled);

			// Type distribution
			if (_deckAnalysis.AttackCount + _deckAnalysis.SkillCount + _deckAnalysis.PowerCount > 0)
			{
				var typeRow = new HBoxContainer();
				typeRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);

				AddTypeChip(typeRow, "\u2694", "Attack", _deckAnalysis.AttackCount, OverlayTheme.CardAttack);
				AddTypeChip(typeRow, "\u26E8", "Skill", _deckAnalysis.SkillCount, OverlayTheme.CardSkill);
				AddTypeChip(typeRow, "\u2726", "Power", _deckAnalysis.PowerCount, OverlayTheme.CardPower);

				Content.AddChild(typeRow, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		// HP bar at bottom
		if (_maxHP > 0)
		{
			Content.AddChild(CreateHpBar(_currentHP, _maxHP), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private void AddUpgradeDeltaToEntry(PanelContainer entry, float delta)
	{
		try
		{
			var vbox = entry.GetChild(0);
			if (vbox is VBoxContainer vb)
			{
				var deltaLbl = new Label();
				deltaLbl.Text = $"\u2B06 업그레이드 +{delta:F1}";
				OverlayStyles.StyleLabel(deltaLbl, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrPositive);
				vb.AddChild(deltaLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"CardRewardInjector.AddUpgradeDeltaToEntry error: {ex.Message}");
		}
	}

	private void AddTypeChip(HBoxContainer parent, string icon, string label, int count, Color color)
	{
		if (count <= 0) return;
		var chip = new Label();
		chip.Text = $"{icon} {count}";
		OverlayStyles.StyleLabel(chip, Res.FontBody, OverlayTheme.FontCaption, color);
		parent.AddChild(chip, forceReadableName: false, Node.InternalMode.Disabled);
	}
}
