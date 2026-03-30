using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Combat screen injector — shows enemy tips, deck strategy, run health,
/// draw pile tracking, and draw probabilities.
/// </summary>
public class CombatInjector : BaseScreenInjector
{
	public override string ScreenName => "COMBAT";

	private DeckAnalysis _deckAnalysis;
	private int _currentHP, _maxHP, _actNumber, _floor;
	private GameState _gameState;
	private List<string> _enemyIds;
	private List<(string icon, string text, Color color)> _combatAdvice;
	private List<(string icon, string text, Color color)> _enemyDetailsTips;
	private bool _showEnemyDetails = true;
	private CombatTracker.CombatSnapshot _lastCombatSnapshot;

	public CombatInjector(OverlaySettings settings) : base(settings) { }

	public void Show(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState, List<string> enemyIds)
	{
		_deckAnalysis = deckAnalysis;
		_currentHP = currentHP;
		_maxHP = maxHP;
		_actNumber = actNumber;
		_floor = floor;
		_gameState = gameState;
		_enemyIds = enemyIds;

		_combatAdvice = new List<(string, string, Color)>();
		_enemyDetailsTips = null;

		// Enemy-specific tips
		if (Settings.ShowEnemyTips && enemyIds != null && Plugin.EnemyAdvisor != null)
		{
			var tips = Plugin.EnemyAdvisor.GetTips(enemyIds);
			if (tips != null && tips.Count > 0)
			{
				_enemyDetailsTips = new List<(string, string, Color)>();
				foreach (var enemy in tips)
				{
					Color dangerColor = enemy.DangerLevel switch
					{
						"extreme" => SharedResources.ClrNegative,
						"high" => SharedResources.ClrExpensive,
						"medium" => SharedResources.ClrAccent,
						_ => SharedResources.ClrSub
					};
					string dangerIcon = enemy.DangerLevel switch
					{
						"extreme" => "\u2620",
						"high" => "\u26a0",
						"medium" => "\u25c6",
						_ => "\u25cb"
					};
					string localName = GameStateReader.GetLocalizedName("enemy", enemy.EnemyId) ?? enemy.EnemyName;
					string dangerStr = TranslateDangerLevel(enemy.DangerLevel);
					_enemyDetailsTips.Add((dangerIcon, $"{localName} [{dangerStr}]", dangerColor));
					if (enemy.Tips != null)
						foreach (var tip in enemy.Tips)
							_enemyDetailsTips.Add(("\u2022", tip, SharedResources.ClrCream));
				}
			}
		}

		// Run health in combat
		if (Plugin.RunHealthComputer != null)
		{
			float archStr = deckAnalysis?.DetectedArchetypes?.Count > 0 ? deckAnalysis.DetectedArchetypes[0].Strength : 0f;
			int bossReady = 50;
			int health = Plugin.RunHealthComputer.CalculateHealth(currentHP, maxHP, 0, deckAnalysis?.TotalCards ?? 0, floor, archStr, bossReady);
			string healthIcon = health >= 70 ? "\u2705" : health >= 45 ? "\u26a0" : "\u274c";
			Color healthColor = health >= 70 ? SharedResources.ClrPositive : health >= 45 ? SharedResources.ClrExpensive : SharedResources.ClrNegative;
			_combatAdvice.Add((healthIcon, $"런 건강도: {health}/100", healthColor));
		}

		// Deck strategy advice
		if (Settings.ShowCombatAdvice)
		{
			var deckAdvice = GenerateCombatAdvice(deckAnalysis, currentHP, maxHP, actNumber, floor);
			if (deckAdvice.Count > 0)
			{
				_combatAdvice.Add(("##", "덱 전략", SharedResources.ClrAccent));
				_combatAdvice.AddRange(deckAdvice);
			}
		}

		Inject(gameNode);
		Rebuild();
	}

	/// <summary>Update combat pile snapshot for live draw/discard tracking.</summary>
	public void UpdatePiles(CombatTracker.CombatSnapshot snapshot)
	{
		_lastCombatSnapshot = snapshot;
		// Only rebuild pile section, not entire panel
		// For now, full rebuild
		if (IsValid()) Rebuild();
	}

	protected override void BuildContent()
	{
		// Combat advice tips
		if (_combatAdvice != null && _combatAdvice.Count > 0)
		{
			foreach (var (icon, text, color) in _combatAdvice)
			{
				if (icon == "##")
				{
					AddSubSectionHeader(text, color);
					continue;
				}
				AddAdviceTip(icon, text, color);
			}
		}

		// Enemy details section
		if (_enemyDetailsTips != null && _enemyDetailsTips.Count > 0)
		{
			AddSectionHeader("적 상세 정보");
			foreach (var (icon, text, color) in _enemyDetailsTips)
			{
				AddAdviceTip(icon, text, color);
			}
		}

		// Combat pile tracking
		if (_lastCombatSnapshot != null)
			BuildCombatPileSection();
	}

	private void BuildCombatPileSection()
	{
		var snap = _lastCombatSnapshot;

		AddSectionHeader("전투 파일");

		// Pile counts
		var headerBox = new HBoxContainer();
		headerBox.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);

		AddPileCountLabel(headerBox, $"\u2660 드로우: {snap.DrawCount}", SharedResources.ClrAqua);
		AddPileCountLabel(headerBox, $"\u2663 버림: {snap.DiscardCount}", SharedResources.ClrSub);
		AddPileCountLabel(headerBox, $"\u2665 손패: {snap.HandCount}", SharedResources.ClrPositive);
		if (snap.ExhaustCount > 0)
			AddPileCountLabel(headerBox, $"\u2716 소멸: {snap.ExhaustCount}", SharedResources.ClrNegative);

		Content.AddChild(headerBox, forceReadableName: false, Node.InternalMode.Disabled);

		// Draw pile card list (grouped)
		if (snap.DrawPile.Count > 0)
		{
			var drawHeader = new Label();
			drawHeader.Text = $"── 드로우 파일 ({snap.DrawCount}장) ──";
			Res.ApplyFont(drawHeader, Res.FontBold);
			drawHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			drawHeader.AddThemeColorOverride("font_color", SharedResources.ClrAqua);
			Content.AddChild(drawHeader, forceReadableName: false, Node.InternalMode.Disabled);

			var grouped = snap.DrawPile
				.GroupBy(c => c.Name + (c.IsUpgraded ? " +" : ""))
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key);

			foreach (var group in grouped)
			{
				var card = group.First();
				string costStr = card.CostsX ? "X" : card.Cost >= 0 ? card.Cost.ToString() : "?";
				string countStr = group.Count() > 1 ? $" x{group.Count()}" : "";
				Color typeColor = OverlayTheme.GetCardTypeColor(card.Type);

				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

				var costLabel = new Label();
				costLabel.Text = $"[{costStr}]";
				Res.ApplyFont(costLabel, Res.FontBody);
				costLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				costLabel.AddThemeColorOverride("font_color", SharedResources.ClrSub);
				costLabel.CustomMinimumSize = new Vector2(28, 0);
				row.AddChild(costLabel, forceReadableName: false, Node.InternalMode.Disabled);

				var nameLabel = new Label();
				nameLabel.Text = group.Key + countStr;
				Res.ApplyFont(nameLabel, Res.FontBody);
				nameLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				nameLabel.AddThemeColorOverride("font_color", typeColor);
				nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(nameLabel, forceReadableName: false, Node.InternalMode.Disabled);

				Content.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		// Draw probabilities
		if (snap.DrawPile.Count > 0)
		{
			var probHeader = new Label();
			probHeader.Text = "── 다음 턴 확률 ──";
			Res.ApplyFont(probHeader, Res.FontBold);
			probHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			probHeader.AddThemeColorOverride("font_color", SharedResources.ClrAccent);
			Content.AddChild(probHeader, forceReadableName: false, Node.InternalMode.Disabled);

			var probs = CombatTracker.CalculateDrawProbabilities(snap, 5);
			int shown = 0;
			foreach (var kvp in probs)
			{
				if (shown >= 8) break;
				int pct = (int)(kvp.Value * 100);
				if (pct <= 0) continue;

				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

				var pctLabel = new Label();
				pctLabel.Text = $"{pct}%";
				Res.ApplyFont(pctLabel, Res.FontBold);
				pctLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				pctLabel.AddThemeColorOverride("font_color", pct >= 80 ? SharedResources.ClrPositive : pct >= 50 ? SharedResources.ClrAccent : SharedResources.ClrSub);
				pctLabel.CustomMinimumSize = new Vector2(36, 0);
				pctLabel.HorizontalAlignment = HorizontalAlignment.Right;
				row.AddChild(pctLabel, forceReadableName: false, Node.InternalMode.Disabled);

				var cardLabel = new Label();
				cardLabel.Text = kvp.Key;
				Res.ApplyFont(cardLabel, Res.FontBody);
				cardLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				cardLabel.AddThemeColorOverride("font_color", SharedResources.ClrCream);
				row.AddChild(cardLabel, forceReadableName: false, Node.InternalMode.Disabled);

				Content.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
				shown++;
			}
		}

		// Discard pile
		if (snap.DiscardPile.Count > 0)
		{
			var discardHeader = new Label();
			discardHeader.Text = $"── 버린 카드 ({snap.DiscardCount}장) ──";
			Res.ApplyFont(discardHeader, Res.FontBold);
			discardHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			discardHeader.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			Content.AddChild(discardHeader, forceReadableName: false, Node.InternalMode.Disabled);

			var grouped = snap.DiscardPile
				.GroupBy(c => c.Name + (c.IsUpgraded ? " +" : ""))
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key);

			foreach (var group in grouped)
			{
				string countStr = group.Count() > 1 ? $" x{group.Count()}" : "";
				var lbl = new Label();
				lbl.Text = $"  {group.Key}{countStr}";
				Res.ApplyFont(lbl, Res.FontBody);
				lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				lbl.AddThemeColorOverride("font_color", new Color(SharedResources.ClrSub, 0.8f));
				Content.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
	}

	private void AddPileCountLabel(HBoxContainer parent, string text, Color color)
	{
		var lbl = new Label();
		lbl.Text = text;
		Res.ApplyFont(lbl, Res.FontBold);
		lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
		lbl.AddThemeColorOverride("font_color", color);
		parent.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private List<(string icon, string text, Color color)> GenerateCombatAdvice(DeckAnalysis deck, int hp, int maxHP, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		int deckSize = deck?.TotalCards ?? 0;

		if (deckSize <= 12)
			advice.Add(("\u2714", "얇은 덱 — 드로우 일관성 우수", SharedResources.ClrPositive));
		else if (deckSize >= 30)
			advice.Add(("\u26a0", "덱이 큼 — 핵심 카드 드로우가 느릴 수 있음", SharedResources.ClrExpensive));

		if (hpRatio < 0.3f)
			advice.Add(("\u26a0", "HP 낮음 — 수비적 플레이, 블록 우선", SharedResources.ClrNegative));

		return advice;
	}

	private static string TranslateDangerLevel(string level)
	{
		return level?.ToLowerInvariant() switch
		{
			"low" => "낮음",
			"medium" => "보통",
			"high" => "높음",
			"extreme" => "극도",
			_ => level?.ToUpperInvariant() ?? "?"
		};
	}
}
