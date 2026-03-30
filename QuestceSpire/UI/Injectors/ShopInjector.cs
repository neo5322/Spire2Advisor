using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Shop screen injector — shows scored card and relic recommendations for the merchant.
/// </summary>
public class ShopInjector : BaseScreenInjector
{
	public override string ScreenName => "MERCHANT SHOP";

	private List<ScoredCard> _cards;
	private List<ScoredRelic> _relics;
	private DeckAnalysis _deckAnalysis;
	private string _character;

	public ShopInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredCard> cards, List<ScoredRelic> relics, DeckAnalysis deckAnalysis, string character)
	{
		_cards = cards;
		_relics = relics;
		_deckAnalysis = deckAnalysis;
		_character = character;

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		// Cards
		if (_cards != null && _cards.Count > 0)
		{
			AddSectionHeader("카드 추천");
			foreach (var card in _cards)
			{
				string subGrade = TierEngine.ScoreToSubGrade(card.FinalScore);
				Color gradeColor = GetGradeColor(card.FinalGrade);
				string name = card.Name ?? card.Id;
				string costStr = card.Cost >= 0 ? $"[{card.Cost}]" : "";
				AddCardEntry(name, subGrade, gradeColor, costStr, card.FinalGrade >= TierGrade.A);
			}
		}

		// Relics
		if (_relics != null && _relics.Count > 0)
		{
			AddSectionHeader("유물 추천");
			foreach (var relic in _relics)
			{
				string subGrade = TierEngine.ScoreToSubGrade(relic.FinalScore);
				Color gradeColor = GetGradeColor(relic.FinalGrade);
				string name = relic.Name ?? relic.Id;
				AddRelicEntry(name, subGrade, gradeColor, relic.FinalGrade >= TierGrade.A);
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

	private void AddCardEntry(string name, string grade, Color gradeColor, string costStr, bool isBest)
	{
		var entry = new PanelContainer();
		entry.AddThemeStyleboxOverride("panel", isBest ? Res.SbBest : Res.SbEntry);

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

		entry.AddChild(hbox, forceReadableName: false, Node.InternalMode.Disabled);
		Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddRelicEntry(string name, string grade, Color gradeColor, bool isBest)
	{
		var entry = new PanelContainer();
		entry.AddThemeStyleboxOverride("panel", isBest ? Res.SbBest : Res.SbEntry);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);

		var gradeLbl = new Label();
		gradeLbl.Text = grade;
		Res.ApplyFont(gradeLbl, Res.FontBold);
		gradeLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		gradeLbl.AddThemeColorOverride("font_color", gradeColor);
		gradeLbl.CustomMinimumSize = new Vector2(40, 0);
		hbox.AddChild(gradeLbl, forceReadableName: false, Node.InternalMode.Disabled);

		var nameLbl = new Label();
		nameLbl.Text = name;
		Res.ApplyFont(nameLbl, Res.FontBody);
		nameLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		nameLbl.AddThemeColorOverride("font_color", SharedResources.ClrCream);
		nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(nameLbl, forceReadableName: false, Node.InternalMode.Disabled);

		entry.AddChild(hbox, forceReadableName: false, Node.InternalMode.Disabled);
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
