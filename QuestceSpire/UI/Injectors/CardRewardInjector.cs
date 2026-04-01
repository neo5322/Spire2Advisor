using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

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

			// Community chips (build alignment tags)
			AddCommunityChips(entry, card);

			// Community tip
			AddCommunityTip(entry, card);

			// Combo detection
			AddComboInfo(entry, card);

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

	private void AddCommunityChips(PanelContainer entry, ScoredCard card)
	{
		try
		{
			if (card.Chips == null || card.Chips.Count == 0) return;
			var vbox = entry.GetChild(0) as VBoxContainer;
			if (vbox == null) return;

			var chipRow = new HBoxContainer();
			chipRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

			foreach (var (tag, label) in card.Chips)
			{
				Color chipColor = tag switch
				{
					"good" => OverlayTheme.Positive,
					"bad" => OverlayTheme.Negative,
					"mid" => OverlayTheme.Warning,
					_ => SharedResources.ClrSub
				};

				var chipPanel = new PanelContainer();
				var chipStyle = new StyleBoxFlat();
				chipStyle.BgColor = new Color(chipColor, 0.15f);
				OverlayStyles.SetAllCornerRadius(chipStyle, OverlayTheme.RadiusSM);
				chipStyle.ContentMarginLeft = chipStyle.ContentMarginRight = 6f;
				chipStyle.ContentMarginTop = chipStyle.ContentMarginBottom = 1f;
				OverlayStyles.SetAllBorderWidth(chipStyle, 1);
				chipStyle.BorderColor = new Color(chipColor, 0.3f);
				chipPanel.AddThemeStyleboxOverride("panel", chipStyle);

				var chipLbl = new Label();
				chipLbl.Text = label;
				OverlayStyles.StyleLabel(chipLbl, Res.FontBody, OverlayTheme.FontCaption, chipColor);
				chipPanel.AddChild(chipLbl, forceReadableName: false, Node.InternalMode.Disabled);
				chipRow.AddChild(chipPanel, forceReadableName: false, Node.InternalMode.Disabled);
			}

			vbox.AddChild(chipRow, forceReadableName: false, Node.InternalMode.Disabled);
		}
		catch (Exception ex)
		{
			Plugin.Log($"CardRewardInjector.AddCommunityChips error: {ex.Message}");
		}
	}

	private void AddCommunityTip(PanelContainer entry, ScoredCard card)
	{
		try
		{
			string tip = card.CommunityTip;
			if (string.IsNullOrEmpty(tip))
			{
				var koreanName = GameStateReader.GetLocalizedName("card", card.Id);
				if (koreanName != null)
					tip = Plugin.CommunityData?.GetTip(koreanName);
			}
			if (string.IsNullOrEmpty(tip)) return;

			var vbox = entry.GetChild(0) as VBoxContainer;
			if (vbox == null) return;

			var tipLbl = new Label();
			tipLbl.Text = tip;
			OverlayStyles.StyleLabel(tipLbl, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrNotes);
			tipLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			vbox.AddChild(tipLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		catch (Exception ex)
		{
			Plugin.Log($"CardRewardInjector.AddCommunityTip error: {ex.Message}");
		}
	}

	private void AddComboInfo(PanelContainer entry, ScoredCard card)
	{
		try
		{
			if (Plugin.CommunityData == null || !Plugin.CommunityData.IsLoaded) return;
			if (_deckAnalysis == null || string.IsNullOrEmpty(_character)) return;

			var koreanName = GameStateReader.GetLocalizedName("card", card.Id);
			if (koreanName == null) return;

			// Get deck Korean names
			var deckKoreanNames = new List<string>();
			var gs = GameStateReader.ReadCurrentState();
			if (gs?.DeckCards != null)
			{
				foreach (var c in gs.DeckCards)
				{
					var kn = GameStateReader.GetLocalizedName("card", c.Id);
					if (kn != null) deckKoreanNames.Add(kn);
				}
			}
			if (deckKoreanNames.Count == 0) return;

			var (full, _) = Plugin.CommunityData.GetMatchingCombos(_character, koreanName, deckKoreanNames);
			if (full == null || full.Count == 0) return;

			var vbox = entry.GetChild(0) as VBoxContainer;
			if (vbox == null) return;

			var combo = full[0];
			var comboLbl = new Label();
			comboLbl.Text = $"\u26A1 콤보: {combo.Name} — {combo.Why}";
			OverlayStyles.StyleLabel(comboLbl, Res.FontBody, OverlayTheme.FontCaption, OverlayTheme.Info);
			comboLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			vbox.AddChild(comboLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		catch (Exception ex)
		{
			Plugin.Log($"CardRewardInjector.AddComboInfo error: {ex.Message}");
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
