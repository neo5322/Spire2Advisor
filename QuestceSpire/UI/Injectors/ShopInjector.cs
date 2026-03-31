using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Shop screen injector — shows scored card and relic recommendations for the merchant.
/// Compact view: grade badge + type icon + name per line.
/// Expanded view: adds score bars, synergy reasons, price tags.
/// </summary>
public class ShopInjector : BaseScreenInjector
{
	public override string ScreenName => "MERCHANT SHOP";

	private List<ScoredCard> _cards;
	private List<ScoredRelic> _relics;
	private DeckAnalysis _deckAnalysis;
	private string _character;
	private int _currentHP, _maxHP, _gold;

	public ShopInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, List<ScoredCard> cards, List<ScoredRelic> relics,
		DeckAnalysis deckAnalysis, string character, int currentHP = 0, int maxHP = 0, int gold = 0)
	{
		_cards = cards;
		_relics = relics;
		_deckAnalysis = deckAnalysis;
		_character = character;
		_currentHP = currentHP;
		_maxHP = maxHP;
		_gold = gold;

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		// Gold display
		if (_gold > 0)
		{
			var goldRow = new HBoxContainer();
			goldRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
			var goldIcon = new Label();
			goldIcon.Text = "\u2B50";
			OverlayStyles.StyleLabel(goldIcon, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrAccent);
			goldRow.AddChild(goldIcon, forceReadableName: false, Node.InternalMode.Disabled);
			var goldLbl = new Label();
			goldLbl.Text = $"{_gold}G";
			OverlayStyles.StyleLabel(goldLbl, Res.FontBold, CurrentFontBody, SharedResources.ClrAccent);
			goldRow.AddChild(goldLbl, forceReadableName: false, Node.InternalMode.Disabled);
			Content.AddChild(goldRow, forceReadableName: false, Node.InternalMode.Disabled);
		}

		// Cards
		if (_cards != null && _cards.Count > 0)
		{
			AddSectionHeader("카드");
			foreach (var card in _cards)
			{
				var entry = CreateCompactCardEntry(card, card.IsBestPick);
				// Add price overlay for shop cards
				if (card.Price > 0 && !IsExpanded)
				{
					// In compact mode, show price inline — find the hbox and append
					AddPriceToEntry(entry, card.Price, _gold >= card.Price);
				}
				else if (card.Price > 0 && IsExpanded)
				{
					AddExpandedPriceInfo(entry, card.Price, _gold >= card.Price);
				}
				Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		// Relics
		if (_relics != null && _relics.Count > 0)
		{
			AddSectionHeader("유물");
			foreach (var relic in _relics)
			{
				var entry = CreateCompactRelicEntry(relic, relic.IsBestPick);
				Content.AddChild(entry, forceReadableName: false, Node.InternalMode.Disabled);
			}
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
		}

		// HP bar at bottom
		if (_maxHP > 0)
		{
			Content.AddChild(CreateHpBar(_currentHP, _maxHP), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	private void AddPriceToEntry(PanelContainer entry, int price, bool canAfford)
	{
		// Navigate into the entry's vbox > hbox to append price label
		try
		{
			var vbox = entry.GetChild(0);
			if (vbox is VBoxContainer vb)
			{
				var hbox = vb.GetChild(0);
				if (hbox is HBoxContainer hb)
				{
					var priceLbl = new Label();
					priceLbl.Text = $"{price}g";
					OverlayStyles.StyleLabel(priceLbl, Res.FontBody, OverlayTheme.FontCaption,
						canAfford ? SharedResources.ClrAccent : SharedResources.ClrNegative);
					hb.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"ShopInjector.AddPriceToEntry error: {ex.Message}");
		}
	}

	private void AddExpandedPriceInfo(PanelContainer entry, int price, bool canAfford)
	{
		try
		{
			var vbox = entry.GetChild(0);
			if (vbox is VBoxContainer vb)
			{
				var priceRow = new HBoxContainer();
				priceRow.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

				var priceLbl = new Label();
				priceLbl.Text = $"\u2B50 {price}G";
				OverlayStyles.StyleLabel(priceLbl, Res.FontBold, OverlayTheme.FontCaption,
					canAfford ? SharedResources.ClrAccent : SharedResources.ClrNegative);
				priceRow.AddChild(priceLbl, forceReadableName: false, Node.InternalMode.Disabled);

				if (!canAfford)
				{
					var warnLbl = new Label();
					warnLbl.Text = "— 골드 부족";
					OverlayStyles.StyleLabel(warnLbl, Res.FontBody, OverlayTheme.FontCaption, SharedResources.ClrNegative);
					priceRow.AddChild(warnLbl, forceReadableName: false, Node.InternalMode.Disabled);
				}

				vb.AddChild(priceRow, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"ShopInjector.AddExpandedPriceInfo error: {ex.Message}");
		}
	}
}
