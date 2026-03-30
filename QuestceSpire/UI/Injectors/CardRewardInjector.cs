using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Card reward/upgrade/removal screen injector — shows scored card recommendations.
/// Handles: CARD REWARD, CARD UPGRADE, CARD REMOVAL, EVENT CARD OFFER screens.
/// </summary>
public class CardRewardInjector : BaseScreenInjector
{
	private string _screenName = "CARD REWARD";
	public override string ScreenName => _screenName;

	private List<ScoredCard> _cards;
	private DeckAnalysis _deckAnalysis;
	private string _character;

	public CardRewardInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredCard> cards, DeckAnalysis deckAnalysis, string character, string screenLabel = "CARD REWARD")
	{
		_cards = cards;
		_deckAnalysis = deckAnalysis;
		_character = character;
		_screenName = screenLabel;

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
			"CARD UPGRADE" => "업그레이드 추천",
			"CARD REMOVAL" => "제거 추천",
			"EVENT CARD OFFER" => "이벤트 카드 추천",
			_ => "카드 추천"
		};
		AddSectionHeader(headerText);

		bool isFirst = true;
		foreach (var card in _cards)
		{
			string subGrade = TierEngine.ScoreToSubGrade(card.FinalScore);
			Color gradeColor = GetGradeColor(card.FinalGrade);
			string name = card.Name ?? card.Id;
			string costStr = card.Cost >= 0 ? $"[{card.Cost}]" : "";
			bool isBest = isFirst && card.FinalGrade >= TierGrade.B;

			AddCardEntry(name, subGrade, gradeColor, costStr, isBest, card);
			isFirst = false;
		}

		// Skip recommendation for card rewards
		if (_screenName == "CARD REWARD" && _cards.Count > 0)
		{
			var best = _cards[0];
			if (best.FinalGrade <= TierGrade.C)
			{
				AddAdviceTip("\u26a0", "낮은 등급 — 스킵을 고려하세요", SharedResources.ClrSkip);
			}
		}

		// Deck summary
		if (Settings.ShowDeckBreakdown && _deckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			var summaryLbl = new Label();
			summaryLbl.Text = $"카드 {_deckAnalysis.TotalCards}장";
			if (_deckAnalysis.DetectedArchetypes?.Count > 0)
				summaryLbl.Text += $" — {_deckAnalysis.DetectedArchetypes[0].Archetype.DisplayName} ({_deckAnalysis.DetectedArchetypes[0].Strength:P0})";
			Res.ApplyFont(summaryLbl, Res.FontBody);
			summaryLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
			summaryLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
			Content.AddChild(summaryLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private void AddCardEntry(string name, string grade, Color gradeColor, string costStr, bool isBest, ScoredCard card)
	{
		var entry = new PanelContainer();
		entry.AddThemeStyleboxOverride("panel", isBest ? Res.SbBest : Res.SbEntry);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);

		// Main row: grade + cost + name
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);

		var gradeLbl = new Label();
		gradeLbl.Text = grade;
		Res.ApplyFont(gradeLbl, Res.FontBold);
		gradeLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		gradeLbl.AddThemeColorOverride("font_color", gradeColor);
		gradeLbl.CustomMinimumSize = new Vector2(40, 0);
		hbox.AddChild(gradeLbl, forceReadableName: false, Node.InternalMode.Disabled);

		if (!string.IsNullOrEmpty(costStr))
		{
			var costLbl = new Label();
			costLbl.Text = costStr;
			Res.ApplyFont(costLbl, Res.FontBody);
			costLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			costLbl.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			hbox.AddChild(costLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}

		var nameLbl = new Label();
		nameLbl.Text = name;
		Res.ApplyFont(nameLbl, Res.FontBody);
		nameLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		nameLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
		nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(nameLbl, forceReadableName: false, Node.InternalMode.Disabled);

		vbox.AddChild(hbox, forceReadableName: false, Node.InternalMode.Disabled);

		// Synergy/reason sub-line
		if (card.SynergyDelta > 0.3f || card.UpgradeDelta > 0.5f)
		{
			var reasonLbl = new Label();
			var reasons = new List<string>();
			if (card.SynergyDelta > 0.3f) reasons.Add($"시너지 +{card.SynergyDelta:F1}");
			if (card.UpgradeDelta > 0.5f) reasons.Add($"업그레이드 +{card.UpgradeDelta:F1}");
			reasonLbl.Text = string.Join("  ", reasons);
			Res.ApplyFont(reasonLbl, Res.FontBody);
			reasonLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			reasonLbl.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			vbox.AddChild(reasonLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}

		entry.AddChild(vbox, forceReadableName: false, Node.InternalMode.Disabled);
		Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static Color GetGradeColor(TierGrade grade) => grade switch
	{
		TierGrade.S => SharedResources.ClrAccent,
		TierGrade.A => SharedResources.ClrPositive,
		TierGrade.B => SharedResources.ClrAqua,
		TierGrade.C => SharedResources.ClrCream,
		TierGrade.D => SharedResources.ClrSub,
		_ => SharedResources.ClrSkip
	};
}
