using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Rest site screen injector — shows rest/upgrade recommendation and upgrade priorities.
/// </summary>
public class RestSiteInjector : BaseScreenInjector
{
	public override string ScreenName => "REST SITE";

	private DeckAnalysis _deckAnalysis;
	private int _currentHP, _maxHP, _actNumber, _floor;
	private GameState _gameState;
	private List<(string icon, string text, Color color)> _advice;

	public RestSiteInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState)
	{
		_deckAnalysis = deckAnalysis;
		_currentHP = currentHP;
		_maxHP = maxHP;
		_actNumber = actNumber;
		_floor = floor;
		_gameState = gameState;
		_advice = GenerateRestSiteAdvice(deckAnalysis, currentHP, maxHP, actNumber, floor, gameState);

		Inject(gameNode);
		Rebuild();
	}

	protected override void BuildContent()
	{
		if (_advice == null || _advice.Count == 0) return;

		AddSectionHeader("휴식 조언");
		foreach (var (icon, text, color) in _advice)
		{
			if (icon == "##")
			{
				AddSubSectionHeader(text, color);
				continue;
			}
			AddAdviceTip(icon, text, color);
		}
	}

	private List<(string icon, string text, Color color)> GenerateRestSiteAdvice(DeckAnalysis deck, int hp, int maxHP, int act, int floor, GameState gameState)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;

		if (hpRatio < 0.5f)
		{
			advice.Add(("\u2B50", "회복 추천 — HP가 낮습니다", SharedResources.ClrNegative));
			if (hpRatio < 0.3f)
				advice.Add(("\u26a0", "HP critical — resting is almost always correct here", SharedResources.ClrNegative));
		}
		else if (hpRatio >= 0.75f)
		{
			advice.Add(("\u2B06", "업그레이드 추천 — HP 여유", SharedResources.ClrPositive));
			if (deck != null && deck.DetectedArchetypes.Count > 0)
				advice.Add(("\u2694", $"{deck.DetectedArchetypes[0].Archetype.DisplayName} 핵심 카드 업그레이드", SharedResources.ClrAccent));
		}
		else
		{
			bool isBossSoon = (floor % 8) >= 6;
			if (isBossSoon)
				advice.Add(("\u2764", "보스 임박 — 안전을 위해 회복하세요", SharedResources.ClrExpensive));
			else
				advice.Add(("\u2B06", "업그레이드를 고려하세요 — HP 충분", SharedResources.ClrAqua));
		}

		// Upgrade priority list when upgrade is recommended (HP >= 50%)
		if (hpRatio >= 0.5f && gameState != null && deck != null)
		{
			string character = gameState.Character ?? deck.Character ?? "unknown";
			var priorities = GetUpgradePriorities(gameState, deck, character);
			if (priorities.Count > 0)
			{
				advice.Add(("\u2B06", "업그레이드 우선순위:", SharedResources.ClrAccent));
				advice.AddRange(priorities);
			}
		}

		return advice;
	}

	private List<(string icon, string text, Color color)> GetUpgradePriorities(GameState gs, DeckAnalysis deck, string character)
	{
		var upgradeable = new List<CardInfo>();
		foreach (var card in gs.DeckCards)
		{
			if (card.Upgraded) continue;
			if (card.Type == "Status" || card.Type == "Curse") continue;
			upgradeable.Add(card);
		}
		if (upgradeable.Count == 0) return new List<(string, string, Color)>();

		var scored = Plugin.SynergyScorer.ScoreForUpgrade(upgradeable, deck, character,
			gs.ActNumber, gs.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);

		return scored
			.Take(3)
			.Select(c =>
			{
				string cardName = c.Name ?? c.Id;
				string subGrade = TierEngine.ScoreToSubGrade(c.FinalScore);

				string reason;
				var upgradeData = Plugin.RunDatabase?.GetUpgradeValue(c.Id, character);
				if (upgradeData != null && upgradeData.SampleSize >= 3 && upgradeData.UpgradeWinDelta > 0.02f)
					reason = $" — upgrade win +{upgradeData.UpgradeWinDelta:P0} ({upgradeData.SampleSize} runs)";
				else if (c.UpgradeDelta >= 0.6f)
					reason = $" — big upgrade (+{c.UpgradeDelta:F1})";
				else if (c.SynergyDelta > 0.4f)
					reason = " — 핵심 시너지";
				else if (c.FinalGrade >= TierGrade.A)
					reason = " — 높은 영향력";
				else if (c.FinalGrade >= TierGrade.B)
					reason = " — 안정적 선택";
				else
					reason = " — 기본 업그레이드";
				string text = $"\u2B06 {cardName} [{subGrade}]{reason}";
				Color color = c.FinalGrade >= TierGrade.A ? SharedResources.ClrPositive : c.FinalGrade >= TierGrade.B ? SharedResources.ClrAqua : SharedResources.ClrCream;
				return ((string)"\u2022", text, color);
			})
			.ToList();
	}
}
