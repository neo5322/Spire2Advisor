using System;
using System.Collections.Generic;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Relic reward screen injector — shows scored relic recommendations.
/// </summary>
public class RelicRewardInjector : BaseScreenInjector
{
	public override string ScreenName => "RELIC REWARD";

	private List<ScoredRelic> _relics;
	private DeckAnalysis _deckAnalysis;
	private string _character;

	public RelicRewardInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredRelic> relics, DeckAnalysis deckAnalysis, string character)
	{
		_relics = relics;
		_deckAnalysis = deckAnalysis;
		_character = character;

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
			string subGrade = TierEngine.ScoreToSubGrade(relic.FinalScore);
			Color gradeColor = GetGradeColor(relic.FinalGrade);
			string name = relic.Name ?? relic.Id;
			bool isBest = isFirst && relic.FinalGrade >= TierGrade.B;

			AddRelicEntry(name, subGrade, gradeColor, isBest, relic);
			isFirst = false;
		}

		// Deck context
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

	private void AddRelicEntry(string name, string grade, Color gradeColor, bool isBest, ScoredRelic relic)
	{
		var entry = new PanelContainer();
		entry.AddThemeStyleboxOverride("panel", isBest ? Res.SbBest : Res.SbEntry);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);

		// Main row
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

		vbox.AddChild(hbox, forceReadableName: false, Node.InternalMode.Disabled);

		// Synergy reason
		if (relic.SynergyDelta > 0.3f)
		{
			var reasonLbl = new Label();
			reasonLbl.Text = $"시너지 +{relic.SynergyDelta:F1}";
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
