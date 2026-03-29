using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

/// <summary>Advice rendering — per-screen advice generation and display.</summary>
public partial class OverlayManager
{
	private bool _showEnemyDetails = true;  // default expanded
	private VBoxContainer _combatPileContainer;
	private VBoxContainer _enemyDetailsContainer;
	private List<(string icon, string text, Color color)> _enemyDetailsTips;

	private void RebuildCombatPileSection()
	{
		if (_content == null || _lastCombatSnapshot == null) return;

		// Remove old combat pile section if exists
		if (_combatPileContainer != null && GodotObject.IsInstanceValid(_combatPileContainer))
		{
			SafeDisconnectSignals(_combatPileContainer);
			_combatPileContainer.GetParent()?.RemoveChild(_combatPileContainer);
			_combatPileContainer.QueueFree();
		}

		_combatPileContainer = new VBoxContainer();
		_combatPileContainer.AddThemeConstantOverride("separation", OverlayTheme.SpaceXS);

		var snap = _lastCombatSnapshot;

		// ─── Header: pile counts bar ───
		var headerBox = new HBoxContainer();
		headerBox.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
		_combatPileContainer.AddChild(headerBox);

		AddPileCountLabel(headerBox, $"\u2660 드로우: {snap.DrawCount}", ClrAqua);
		AddPileCountLabel(headerBox, $"\u2663 버림: {snap.DiscardCount}", ClrSub);
		AddPileCountLabel(headerBox, $"\u2665 손패: {snap.HandCount}", ClrPositive);
		if (snap.ExhaustCount > 0)
			AddPileCountLabel(headerBox, $"\u2716 소멸: {snap.ExhaustCount}", ClrNegative);

		// ─── Draw pile card list (grouped) ───
		if (snap.DrawPile.Count > 0)
		{
			var drawHeader = new Label();
			drawHeader.Text = $"── 드로우 파일 ({snap.DrawCount}장) ──";
			ApplyFont(drawHeader, _fontBold);
			drawHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			drawHeader.AddThemeColorOverride("font_color", ClrAqua);
			_combatPileContainer.AddChild(drawHeader);

			var grouped = snap.DrawPile
				.GroupBy(c => c.Name + (c.IsUpgraded ? " +" : ""))
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key);

			foreach (var group in grouped)
			{
				var card = group.First();
				string costStr = card.CostsX ? "X" : card.Cost >= 0 ? card.Cost.ToString() : "?";
				string countStr = group.Count() > 1 ? $" x{group.Count()}" : "";
				Color typeColor = GetCardTypeColor(card.Type);

				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

				var costLabel = new Label();
				costLabel.Text = $"[{costStr}]";
				ApplyFont(costLabel, _fontBody);
				costLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				costLabel.AddThemeColorOverride("font_color", ClrSub);
				costLabel.CustomMinimumSize = new Vector2(28, 0);
				row.AddChild(costLabel);

				var nameLabel = new Label();
				nameLabel.Text = group.Key + countStr;
				ApplyFont(nameLabel, _fontBody);
				nameLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				nameLabel.AddThemeColorOverride("font_color", typeColor);
				nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				row.AddChild(nameLabel);

				_combatPileContainer.AddChild(row);
			}
		}

		// ─── Draw probabilities (next 5 cards) ───
		if (snap.DrawPile.Count > 0)
		{
			var probHeader = new Label();
			probHeader.Text = "── 다음 턴 확률 ──";
			ApplyFont(probHeader, _fontBold);
			probHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			probHeader.AddThemeColorOverride("font_color", ClrAccent);
			_combatPileContainer.AddChild(probHeader);

			var probs = CombatTracker.CalculateDrawProbabilities(snap, 5);
			int shown = 0;
			foreach (var kvp in probs)
			{
				if (shown >= 8) break; // Top 8
				int pct = (int)(kvp.Value * 100);
				if (pct <= 0) continue;

				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

				var pctLabel = new Label();
				pctLabel.Text = $"{pct}%";
				ApplyFont(pctLabel, _fontBold);
				pctLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				pctLabel.AddThemeColorOverride("font_color", pct >= 80 ? ClrPositive : pct >= 50 ? ClrAccent : ClrSub);
				pctLabel.CustomMinimumSize = new Vector2(36, 0);
				pctLabel.HorizontalAlignment = HorizontalAlignment.Right;
				row.AddChild(pctLabel);

				var cardLabel = new Label();
				cardLabel.Text = kvp.Key;
				ApplyFont(cardLabel, _fontBody);
				cardLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				cardLabel.AddThemeColorOverride("font_color", ClrCream);
				row.AddChild(cardLabel);

				_combatPileContainer.AddChild(row);
				shown++;
			}
		}

		// ─── Discard pile (grouped, compact) ───
		if (snap.DiscardPile.Count > 0)
		{
			var discardHeader = new Label();
			discardHeader.Text = $"── 버린 카드 ({snap.DiscardCount}장) ──";
			ApplyFont(discardHeader, _fontBold);
			discardHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			discardHeader.AddThemeColorOverride("font_color", ClrSub);
			_combatPileContainer.AddChild(discardHeader);

			var grouped = snap.DiscardPile
				.GroupBy(c => c.Name + (c.IsUpgraded ? " +" : ""))
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key);

			foreach (var group in grouped)
			{
				string countStr = group.Count() > 1 ? $" x{group.Count()}" : "";
				var lbl = new Label();
				lbl.Text = $"  {group.Key}{countStr}";
				ApplyFont(lbl, _fontBody);
				lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
				lbl.AddThemeColorOverride("font_color", new Color(ClrSub, 0.8f));
				_combatPileContainer.AddChild(lbl);
			}
		}

		// Insert into _content (after existing advice sections)
		_content.AddChild(_combatPileContainer);
	}

	private void RebuildEnemyDetailsSection()
	{
		if (_content == null || _enemyDetailsTips == null || _enemyDetailsTips.Count == 0) return;

		// Remove old enemy details section if exists
		if (_enemyDetailsContainer != null && GodotObject.IsInstanceValid(_enemyDetailsContainer))
		{
			SafeDisconnectSignals(_enemyDetailsContainer);
			_enemyDetailsContainer.GetParent()?.RemoveChild(_enemyDetailsContainer);
			_enemyDetailsContainer.QueueFree();
		}

		var enemySection = AddCollapsibleSection("적 상세 정보", "combatEnemyDetails", ref _showEnemyDetails);
		if (enemySection != null)
		{
			_enemyDetailsContainer = enemySection;
			foreach (var (icon, text, color) in _enemyDetailsTips)
			{
				PanelContainer advPanel = new PanelContainer();
				StyleBoxFlat advStyle = new StyleBoxFlat();
				advStyle.BgColor = new Color(0.05f, 0.07f, 0.12f, 0.5f);
				advStyle.CornerRadiusTopRight = 8;
				advStyle.CornerRadiusBottomRight = 8;
				advStyle.BorderWidthLeft = 3;
				advStyle.BorderColor = new Color(color, 0.6f);
				advStyle.ContentMarginLeft = 12f;
				advStyle.ContentMarginRight = 10f;
				advStyle.ContentMarginTop = 6f;
				advStyle.ContentMarginBottom = 6f;
				advPanel.AddThemeStyleboxOverride("panel", advStyle);
				Label advLbl = new Label();
				advLbl.Text = $"{icon}  {text}";
				ApplyFont(advLbl, _fontBody);
				advLbl.AddThemeColorOverride("font_color", color);
				advLbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
				advLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				advPanel.AddChild(advLbl, forceReadableName: false, Node.InternalMode.Disabled);
				enemySection.AddChild(advPanel, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		else
		{
			_enemyDetailsContainer = null;
		}
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

	private Color GetCardTypeColor(string type) => OverlayTheme.GetCardTypeColor(type);

	private void AddPileCountLabel(HBoxContainer parent, string text, Color color)
	{
		var lbl = new Label();
		lbl.Text = text;
		ApplyFont(lbl, _fontBold);
		lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
		lbl.AddThemeColorOverride("font_color", color);
		parent.AddChild(lbl);
	}

	private void CheckForEventCardOffering()
	{
		// First check if _lastCardOptions was already set (ShowScreen fired)
		GameState gameState = GameStateReader.ReadCurrentState();
		if (gameState == null) return;
		if (gameState.OfferedCards != null && gameState.OfferedCards.Count > 0)
		{
			Plugin.Log($"Event card offering detected (from state): {gameState.OfferedCards.Count} cards");
			DeckAnalysis da = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
			List<ScoredCard> scored = Plugin.SynergyScorer.ScoreOfferings(gameState.OfferedCards, da, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
			ShowCardAdvice(scored, da, gameState.Character, "EVENT CARD OFFER");
			// No in-game badges for events — can't distinguish reward from upgrade/transform
			return;
		}
		// ShowScreen may not have fired — try to find card screen node and extract cards via reflection
		SceneTree sceneTree = Engine.GetMainLoop() as SceneTree;
		if (sceneTree?.Root == null) return;
		Node cardScreen = FindNodeOfType(sceneTree.Root, "NCardRewardSelectionScreen", 4);
		if (cardScreen == null) return;
		// Try to extract cards from the screen
		try
		{
			var offeredCards = ExtractCardsFromScreen(cardScreen);
			if (offeredCards == null || offeredCards.Count == 0) return;
			Plugin.Log($"Event card offering detected (from screen node): {offeredCards.Count} cards");
			DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
			List<ScoredCard> cards = Plugin.SynergyScorer.ScoreOfferings(offeredCards, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
			ShowCardAdvice(cards, deckAnalysis, gameState.Character, "EVENT CARD OFFER");
		}
		catch (Exception ex)
		{
			Plugin.Log($"CheckForEventCardOffering reflection error: {ex.Message}");
		}
	}

	private static List<CardInfo> ExtractCardsFromScreen(Node screen)
	{
		var cardsField = screen.GetType().GetField("_cards",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
		var cardsProp = screen.GetType().GetProperty("Cards",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
		object cardsObj = cardsField?.GetValue(screen) ?? cardsProp?.GetValue(screen);
		if (cardsObj is IReadOnlyList<CardCreationResult> screenCards && screenCards.Count > 0)
		{
			var result = new List<CardInfo>();
			foreach (var cr in screenCards)
			{
				if (cr.Card != null)
					result.Add(GameStateReader.CardModelToInfo(cr.Card));
			}
			return result;
		}
		// Try card holders
		var holdersField = screen.GetType().GetField("_cardHolders",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
		if (holdersField != null)
		{
			var holders = holdersField.GetValue(screen);
			if (holders is System.Collections.IList holderList && holderList.Count > 0 && holderList.Count <= 5)
			{
				var result = new List<CardInfo>();
				foreach (var holder in holderList)
				{
					var crProp = holder.GetType().GetProperty("CreationResult",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (crProp?.GetValue(holder) is CardCreationResult cr && cr.Card != null)
						result.Add(GameStateReader.CardModelToInfo(cr.Card));
				}
				return result;
			}
		}
		return null;
	}

	private void RefreshShopIfChanged()
	{
		GameState gameState = GameStateReader.ReadCurrentState();
		if (gameState == null) return;
		int currentCount = (gameState.ShopCards?.Count ?? 0) + (gameState.ShopRelics?.Count ?? 0);
		if (currentCount == _shopItemCount) return;
		// Item count changed — a purchase happened
		Plugin.Log($"Shop inventory changed ({_shopItemCount} → {currentCount}), refreshing...");

		// Detect what was purchased by diffing current vs previous item IDs
		var currentCardIds = new HashSet<string>(gameState.ShopCards?.Select(c => c.Id) ?? Enumerable.Empty<string>());
		var currentRelicIds = new HashSet<string>(gameState.ShopRelics?.Select(r => r.Id) ?? Enumerable.Empty<string>());
		var purchasedCards = _shopCardIds.Except(currentCardIds).ToList();
		var purchasedRelics = _shopRelicIds.Except(currentRelicIds).ToList();

		// Record shop purchases as decisions
		if (Plugin.RunTracker != null && (purchasedCards.Count > 0 || purchasedRelics.Count > 0))
		{
			var deckIds = gameState.DeckCards?.ConvertAll(c => c.Id) ?? new List<string>();
			var relicIds = gameState.CurrentRelics?.ConvertAll(r => r.Id) ?? new List<string>();
			foreach (string cardId in purchasedCards)
			{
				var offeredIds = _shopCardIds.ToList();
				Plugin.RunTracker.RecordDecision(
					DecisionEventType.ShopCard, offeredIds, cardId,
					deckIds, relicIds,
					gameState.CurrentHP, gameState.MaxHP, gameState.Gold,
					gameState.ActNumber, gameState.Floor);
				Plugin.Log($"Shop card purchase tracked: {cardId}");
			}
			foreach (string relicId in purchasedRelics)
			{
				var offeredIds = _shopRelicIds.ToList();
				Plugin.RunTracker.RecordDecision(
					DecisionEventType.ShopRelic, offeredIds, relicId,
					deckIds, relicIds,
					gameState.CurrentHP, gameState.MaxHP, gameState.Gold,
					gameState.ActNumber, gameState.Floor);
				Plugin.Log($"Shop relic purchase tracked: {relicId}");
			}
		}

		DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
		List<ScoredCard> cards = Plugin.SynergyScorer.ScoreOfferings(gameState.ShopCards, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
		List<ScoredRelic> relics = Plugin.SynergyScorer.ScoreRelicOfferings(gameState.ShopRelics, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
		ShowShopAdvice(cards, relics, deckAnalysis, gameState.Character);
	}

	private static bool IsInsideMerchant(Node node)
	{
		Node current = node;
		while (current != null)
		{
			string typeName = current.GetType().Name;
			if (typeName.Contains("Merchant") || typeName.Contains("merchant"))
				return true;
			current = current.GetParent();
		}
		return false;
	}

	private static Node FindNodeOfType(Node root, string typeName, int maxDepth)
	{
		if (maxDepth <= 0 || root == null) return null;
		if (root.GetType().Name == typeName) return root;
		foreach (Node child in root.GetChildren())
		{
			Node found = FindNodeOfType(child, typeName, maxDepth - 1);
			if (found != null) return found;
		}
		return null;
	}



	private static bool HasNodeOfType(Node root, string typeName, int maxDepth)
	{
		if (maxDepth <= 0 || root == null) return false;
		if (root.GetType().Name == typeName) return true;
		foreach (Node child in root.GetChildren())
		{
			if (HasNodeOfType(child, typeName, maxDepth - 1)) return true;
		}
		return false;
	}

	public void ShowCardAdvice(List<ScoredCard> cards, DeckAnalysis deckAnalysis = null, string character = null, string screenLabel = "CARD REWARD")
	{
		_currentCards = cards;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = character;
		_currentScreen = screenLabel;
		_mapAdvice = null;
		// Debug: log card names to verify localization
		if (cards != null && cards.Count > 0)
		MarkUpdated();
		Rebuild();
	}

	public void SetScreenLabel(string screen)
	{
		_currentScreen = screen;
		Rebuild();
	}

	public void ShowRelicAdvice(List<ScoredRelic> relics, DeckAnalysis deckAnalysis = null, string character = null)
	{
		_currentRelics = relics;
		_currentCards = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = character;
		_currentScreen = "RELIC REWARD";
		_mapAdvice = null;
		MarkUpdated();
		Rebuild();
	}

	public void ShowCardRemovalAdvice(List<ScoredCard> removalCandidates, DeckAnalysis deckAnalysis = null, string character = null)
	{
		_currentCards = removalCandidates?.Take(5).ToList();
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = character;
		_currentScreen = "CARD REMOVAL";
		_mapAdvice = null;
		MarkUpdated();
		Rebuild();
	}

	public void ShowRestSiteAdvice(DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState = null)
	{
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = deckAnalysis?.Character ?? _currentCharacter;
		_currentScreen = "REST SITE";
		_currentFloor = floor;
		_currentGameState = gameState;
		_mapAdvice = GenerateRestSiteAdvice(deckAnalysis, currentHP, maxHP, actNumber, floor, gameState);
		MarkUpdated();
		Rebuild();
	}

	public void ShowUpgradeAdvice(DeckAnalysis deckAnalysis, GameState gameState, string character)
	{
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = character;
		_currentScreen = "CARD UPGRADE";
		_currentGameState = gameState;
		_mapAdvice = null;
		MarkUpdated();
		Rebuild();
	}

	public void ShowCombatAdvice(DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState = null, List<string> enemyIds = null)
	{
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = deckAnalysis?.Character ?? _currentCharacter;
		_currentScreen = "COMBAT";
		_currentFloor = floor;
		_currentGameState = gameState;
		_currentEnemyIds = enemyIds;
		_currentEventId = null;
		_mapAdvice = new List<(string, string, Color)>();

		// Enemy-specific tips (stored separately for collapsible rendering)
		_enemyDetailsTips = null;
		if (_settings.ShowEnemyTips && enemyIds != null && Plugin.EnemyAdvisor != null)
		{
			var tips = Plugin.EnemyAdvisor.GetTips(enemyIds);
			if (tips != null && tips.Count > 0)
			{
				_enemyDetailsTips = new List<(string, string, Color)>();
				foreach (var enemy in tips)
				{
					Color dangerColor = enemy.DangerLevel switch
					{
						"extreme" => ClrNegative,
						"high" => ClrExpensive,
						"medium" => ClrAccent,
						_ => ClrSub
					};
					string dangerIcon = enemy.DangerLevel switch
					{
						"extreme" => "\u2620",
						"high" => "\u26a0",
						"medium" => "\u25c6",
						_ => "\u25cb"
					};
					_enemyDetailsTips.Add((dangerIcon, $"{GameStateReader.GetLocalizedName("enemy", enemy.EnemyId) ?? enemy.EnemyName} [{TranslateDangerLevel(enemy.DangerLevel)}]", dangerColor));
					if (enemy.Tips != null)
					{
						foreach (var tip in enemy.Tips)
						{
							_enemyDetailsTips.Add(("\u2022", tip, ClrCream));
						}
					}
				}
			}
		}

		// Run health in combat (compact)
		if (Plugin.RunHealthComputer != null)
		{
			float archStr = deckAnalysis?.DetectedArchetypes?.Count > 0 ? deckAnalysis.DetectedArchetypes[0].Strength : 0f;
			int bossReady = 50;
			int health = Plugin.RunHealthComputer.CalculateHealth(currentHP, maxHP, 0, deckAnalysis?.TotalCards ?? 0, floor, archStr, bossReady);
			string healthIcon = health >= 70 ? "\u2705" : health >= 45 ? "\u26a0" : "\u274c";
			Color healthColor = health >= 70 ? ClrPositive : health >= 45 ? ClrExpensive : ClrNegative;
			_mapAdvice.Add((healthIcon, $"런 건강도: {health}/100", healthColor));
		}

		// Generic combat advice (appended)
		if (_settings.ShowCombatAdvice)
		{
			var combatAdvice = GenerateCombatAdvice(deckAnalysis, currentHP, maxHP, actNumber, floor);
			if (combatAdvice.Count > 0)
			{
				_mapAdvice.Add(("##", "덱 전략", ClrAccent));
				_mapAdvice.AddRange(combatAdvice);
			}
		}
		MarkUpdated();
		Rebuild();
		RebuildEnemyDetailsSection();
	}

	public void ShowEventAdvice(DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor, string eventId = null)
	{
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = deckAnalysis?.Character ?? _currentCharacter;
		_currentScreen = "EVENT";
		_currentFloor = floor;
		_currentGameState = new GameState { CurrentHP = currentHP, MaxHP = maxHP, Gold = gold, ActNumber = actNumber, Floor = floor };
		_currentEventId = eventId;
		_currentEnemyIds = null;
		_mapAdvice = new List<(string, string, Color)>();

		// Event-specific advice (prepended before generic)
		if (_settings.ShowEventAdvice && eventId != null && Plugin.EventAdvisor != null)
		{
			var entry = Plugin.EventAdvisor.GetAdvice(eventId);
			if (entry != null)
			{
				int deckSize = deckAnalysis?.TotalCards ?? 0;
				var choices = Plugin.EventAdvisor.EvaluateChoices(entry, currentHP, maxHP, gold, deckSize, actNumber);
				if (choices != null && choices.Count > 0)
				{
					_mapAdvice.Add(("##", $"이벤트: {GameStateReader.GetLocalizedName("event", entry.EventId) ?? entry.EventName}", ClrAccent));
					foreach (var (label, rating, notes) in choices)
					{
						string icon = rating switch
						{
							"good" => "\u2714",
							"bad" => "\u2716",
							_ => "\u25c6"
						};
						Color color = rating switch
						{
							"good" => ClrPositive,
							"bad" => ClrNegative,
							_ => ClrExpensive
						};
						string text = string.IsNullOrEmpty(notes) ? label : $"{label} — {notes}";
						_mapAdvice.Add((icon, text, color));
					}
				}
			}
		}

		// Generic event advice (appended)
		if (_settings.ShowEventAdvice)
		{
			var eventAdvice = GenerateEventAdvice(deckAnalysis, currentHP, maxHP, gold, actNumber, floor);
			if (eventAdvice.Count > 0)
			{
				_mapAdvice.Add(("##", "일반 팁", ClrAccent));
				_mapAdvice.AddRange(eventAdvice);
			}
		}
		MarkUpdated();
		Rebuild();
	}

	public void ShowMapAdvice(DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor)
	{
		_currentFloor = floor;
		_currentGameState = new GameState { CurrentHP = currentHP, MaxHP = maxHP, Gold = gold, ActNumber = actNumber, Floor = floor };
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = deckAnalysis?.Character ?? _currentCharacter;
		_currentScreen = "MAP";
		_currentEventId = null;
		_currentEnemyIds = null;
		_mapAdvice = _settings.ShowMapAdvice
			? GenerateMapAdvice(deckAnalysis, currentHP, maxHP, gold, actNumber, floor)
			: new List<(string, string, Color)>();
		MarkUpdated();
		Rebuild();
	}

	/// <summary>
	/// Re-generates _mapAdvice from stored context after a settings toggle change.
	/// </summary>
	private void RegenerateAdvice()
	{
		var da = _currentDeckAnalysis;
		if (da == null) { Rebuild(); return; }
		var gs = _currentGameState;
		int hp = gs?.CurrentHP ?? 0;
		int maxHP = gs?.MaxHP ?? 1;
		int gold = gs?.Gold ?? 0;
		int act = gs?.ActNumber ?? 1;
		int floor = _currentFloor;

		switch (_currentScreen)
		{
			case "COMBAT":
				_mapAdvice = new List<(string, string, Color)>();
				_enemyDetailsTips = null;
				if (_settings.ShowEnemyTips && _currentEnemyIds != null && Plugin.EnemyAdvisor != null)
				{
					var tips = Plugin.EnemyAdvisor.GetTips(_currentEnemyIds);
					if (tips != null && tips.Count > 0)
					{
						_enemyDetailsTips = new List<(string, string, Color)>();
						foreach (var enemy in tips)
						{
							Color dangerColor = enemy.DangerLevel switch
							{
								"extreme" => ClrNegative, "high" => ClrExpensive,
								"medium" => ClrAccent, _ => ClrSub
							};
							string dangerIcon = enemy.DangerLevel switch
							{
								"extreme" => "\u2620", "high" => "\u26a0",
								"medium" => "\u25c6", _ => "\u25cb"
							};
							_enemyDetailsTips.Add((dangerIcon, $"{GameStateReader.GetLocalizedName("enemy", enemy.EnemyId) ?? enemy.EnemyName} [{TranslateDangerLevel(enemy.DangerLevel)}]", dangerColor));
							if (enemy.Tips != null)
								foreach (var tip in enemy.Tips)
									_enemyDetailsTips.Add(("\u2022", tip, ClrCream));
						}
					}
				}
				if (_settings.ShowCombatAdvice)
				{
					var combatAdvice = GenerateCombatAdvice(da, hp, maxHP, act, floor);
					if (combatAdvice.Count > 0)
					{
						_mapAdvice.Add(("##", "덱 전략", ClrAccent));
						_mapAdvice.AddRange(combatAdvice);
					}
				}
				break;

			case "EVENT":
				_mapAdvice = new List<(string, string, Color)>();
				if (_settings.ShowEventAdvice && _currentEventId != null && Plugin.EventAdvisor != null)
				{
					var entry = Plugin.EventAdvisor.GetAdvice(_currentEventId);
					if (entry != null)
					{
						int deckSize = da?.TotalCards ?? 0;
						var choices = Plugin.EventAdvisor.EvaluateChoices(entry, hp, maxHP, gold, deckSize, act);
						if (choices != null && choices.Count > 0)
						{
							_mapAdvice.Add(("##", $"이벤트: {GameStateReader.GetLocalizedName("event", entry.EventId) ?? entry.EventName}", ClrAccent));
							foreach (var (label, rating, notes) in choices)
							{
								string icon = rating switch { "good" => "\u2714", "bad" => "\u2716", _ => "\u25c6" };
								Color color = rating switch { "good" => ClrPositive, "bad" => ClrNegative, _ => ClrExpensive };
								string text = string.IsNullOrEmpty(notes) ? label : $"{label} — {notes}";
								_mapAdvice.Add((icon, text, color));
							}
						}
					}
				}
				if (_settings.ShowEventAdvice)
				{
					var eventAdvice = GenerateEventAdvice(da, hp, maxHP, gold, act, floor);
					if (eventAdvice.Count > 0)
					{
						_mapAdvice.Add(("##", "일반 팁", ClrAccent));
						_mapAdvice.AddRange(eventAdvice);
					}
				}
				break;

			case "MAP":
				_mapAdvice = _settings.ShowMapAdvice
					? GenerateMapAdvice(da, hp, maxHP, gold, act, floor)
					: new List<(string, string, Color)>();
				break;

			case "REST SITE":
				_mapAdvice = GenerateRestSiteAdvice(da, hp, maxHP, act, floor, gs);
				break;

			default:
				Rebuild();
				return;
		}
		Rebuild();
		if (_currentScreen == "COMBAT")
			RebuildEnemyDetailsSection();
	}

	public void ShowShopAdvice(List<ScoredCard> cards, List<ScoredRelic> relics, DeckAnalysis deckAnalysis = null, string character = null)
	{
		_currentCards = cards;
		_currentRelics = relics;
		_currentDeckAnalysis = deckAnalysis;
		_currentCharacter = character;
		_shopItemCount = (cards?.Count ?? 0) + (relics?.Count ?? 0);
		_shopCardIds = new HashSet<string>(cards?.Select(c => c.Id) ?? Enumerable.Empty<string>());
		_shopRelicIds = new HashSet<string>(relics?.Select(r => r.Id) ?? Enumerable.Empty<string>());
		_currentScreen = "MERCHANT SHOP";
		_mapAdvice = null;
		MarkUpdated();
		Rebuild();
	}

	private List<(string icon, string text, Color color)> GenerateRestSiteAdvice(DeckAnalysis deck, int hp, int maxHP, int act, int floor, GameState gameState = null)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;

		if (hpRatio < 0.5f)
		{
			advice.Add(("\u2B50", "회복 추천 — HP가 낮습니다", ClrNegative));
			if (hpRatio < 0.3f)
			{
				advice.Add(("\u26a0", "HP critical — resting is almost always correct here", ClrNegative));
			}
		}
		else if (hpRatio >= 0.75f)
		{
			advice.Add(("\u2B06", "업그레이드 추천 — HP 여유", ClrPositive));
			if (deck != null && deck.DetectedArchetypes.Count > 0)
			{
				advice.Add(("\u2694", $"{deck.DetectedArchetypes[0].Archetype.DisplayName} 핵심 카드 업그레이드", ClrAccent));
			}
		}
		else
		{
			// 50-75% HP: context-dependent
			bool isBossSoon = (floor % 8) >= 6; // rough heuristic
			if (isBossSoon)
			{
				advice.Add(("\u2764", "보스 임박 — 안전을 위해 회복하세요", ClrExpensive));
			}
			else
			{
				advice.Add(("\u2B06", "업그레이드를 고려하세요 — HP 충분", ClrAqua));
			}
		}

		// Upgrade priority list when upgrade is recommended (HP >= 50%)
		if (hpRatio >= 0.5f && gameState != null && deck != null)
		{
			string character = gameState.Character ?? deck.Character ?? "unknown";
			var priorities = GetUpgradePriorities(gameState, deck, character);
			if (priorities.Count > 0)
			{
				advice.Add(("\u2B06", "업그레이드 우선순위:", ClrAccent));
				advice.AddRange(priorities);
			}
		}

		return advice;
	}

	private List<(string icon, string text, Color color)> GetUpgradePriorities(GameState gs, DeckAnalysis deck, string character)
	{
		// Filter to non-upgraded, upgradeable cards
		var upgradeable = new List<CardInfo>();
		foreach (var card in gs.DeckCards)
		{
			if (card.Upgraded) continue;
			if (card.Type == "Status" || card.Type == "Curse") continue;
			upgradeable.Add(card);
		}
		if (upgradeable.Count == 0) return new List<(string, string, Color)>();

		// Score upgrade delta — how much value each card gains from upgrading
		var scored = Plugin.SynergyScorer.ScoreForUpgrade(upgradeable, deck, character,
			gs.ActNumber, gs.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);

		// Build display from top 3 by upgrade delta
		return scored
			.Take(3)
			.Select(c =>
			{
				string cardName = c.Name ?? PrettifyId(c.Id);
				string subGrade = TierEngine.ScoreToSubGrade(c.FinalScore);

				// Check DB upgrade value data for data-driven reason
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
				Color color = c.FinalGrade >= TierGrade.A ? ClrPositive : c.FinalGrade >= TierGrade.B ? ClrAqua : ClrCream;
				return ((string)"\u2022", text, color);
			})
			.ToList();
	}

	private List<(string icon, string text, Color color)> GenerateCombatAdvice(DeckAnalysis deck, int hp, int maxHP, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		int deckSize = deck?.TotalCards ?? 0;

		// Deck size combat tips
		if (deckSize <= 12)
		{
			advice.Add(("\u2714", "얇은 덱 — 드로우 일관성 우수", ClrPositive));
		}
		else if (deckSize >= 30)
		{
			advice.Add(("\u26a0", "덱이 큼 — 핵심 카드 드로우가 느릴 수 있음", ClrExpensive));
		}

		// HP warning
		if (hpRatio < 0.3f)
		{
			advice.Add(("\u26a0", "HP 낮음 — 수비적 플레이, 블록 우선", ClrNegative));
		}

		return advice;
	}

	private List<(string icon, string text, Color color)> GenerateEventAdvice(DeckAnalysis deck, int hp, int maxHP, int gold, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		int deckSize = deck?.TotalCards ?? 0;
		bool isDefined = deck != null && !deck.IsUndefined;

		// Event-specific guidance
		if (hpRatio < 0.35f)
		{
			advice.Add(("\u26a0", "HP 위험 — HP 소모 선택지 회피", ClrNegative));
		}
		if (gold < 50)
		{
			advice.Add(("\u26a0", "골드 부족 — 골드 소모 선택지 회피", ClrExpensive));
		}
		if (deckSize >= 25)
		{
			advice.Add(("\u2714", "덱이 큼 — 카드 제거가 가치 있음", ClrAqua));
		}
		if (deckSize <= 15 && isDefined)
		{
			advice.Add(("\u2714", "덱이 얇음 — 카드 추가 신중하게", ClrAqua));
		}
		if (!isDefined)
		{
			advice.Add(("\u2714", "덱 방향 불명확 — 카드 보상으로 방향 잡기", ClrCream));
		}

		return advice;
	}

	private List<(string icon, string text, Color color)> GenerateMapAdvice(DeckAnalysis deck, int hp, int maxHP, int gold, int act, int floor)
	{
		var advice = new List<(string, string, Color)>();
		float hpRatio = maxHP > 0 ? (float)hp / maxHP : 1f;
		bool isDefined = deck != null && !deck.IsUndefined;
		int deckSize = deck?.TotalCards ?? 0;

		// HP-based priorities
		if (hpRatio < 0.4f)
		{
			advice.Add(("\u2764", $"HP 위험 ({hp}/{maxHP}) — 휴식을 우선하세요", ClrNegative));
			advice.Add(("\u26a0", "엘리트를 피하세요 (다른 길이 없다면 제외)", ClrExpensive));
		}
		else if (hpRatio < 0.65f)
		{
			advice.Add(("\u2764", $"HP 보통 ({hp}/{maxHP}) — 휴식이 중요", ClrExpensive));
		}

		// Deck composition priorities
		if (!isDefined && floor <= 6)
		{
			advice.Add(("\u2694", "초반 — 전투와 이벤트로 덱 빌딩", ClrPositive));
		}
		else if (isDefined && deckSize >= 25)
		{
			advice.Add(("\u2702", $"덱 비대 ({deckSize}장) — 상점에서 카드 제거", ClrAqua));
		}
		else if (!isDefined && floor > 6)
		{
			advice.Add(("\u2694", "덱 방향 불명확 — 카드 보상으로 방향 잡기", ClrExpensive));
		}

		// Gold-based
		if (gold >= 300)
		{
			advice.Add(("\u2B50", $"골드: {gold} — 상점 가치 높음", ClrAccent));
		}
		else if (gold >= 150 && deckSize >= 20)
		{
			advice.Add(("\u2B50", $"골드: {gold} — 카드 제거를 위한 상점 이용 고려", ClrSub));
		}

		// Act-based
		if (act >= 2 && hpRatio > 0.7f && isDefined && deckSize < 25)
		{
			advice.Add(("\u2694", "덱 집중 + HP 여유 — 엘리트에서 유물 획득", ClrPositive));
		}

		// Treasure/question mark
		if (floor <= 4)
		{
			advice.Add(("\u2753", "초반 층 — ?방 가치 높음", ClrAqua));
		}

		// ─── Boss readiness diagnosis ───
		string character = deck?.Character ?? "unknown";
		var bossResults = BossAdvisor.Diagnose(deck, act, character, hp, maxHP);
		foreach (var boss in bossResults)
		{
			string icon = boss.ReadinessScore >= 70 ? "\u2705" : boss.ReadinessScore >= 45 ? "\u26a0" : "\u274c";
			Color color = boss.ReadinessScore >= 70 ? ClrPositive : boss.ReadinessScore >= 45 ? ClrExpensive : ClrNegative;
			advice.Add(("##", $"보스 대비 진단", ClrAccent));
			advice.Add((icon, $"{boss.BossName}: {boss.Verdict} ({boss.ReadinessScore:F0}점)", color));
			foreach (var s in boss.Strengths)
				advice.Add(("\u2714", s, ClrPositive));
			foreach (var w in boss.Weaknesses)
				advice.Add(("\u26a0", w, ClrNegative));
		}

		// ─── Meta Archetype Panel ───
		try
		{
			string metaPath = System.IO.Path.Combine(Plugin.PluginFolder, "Data", "meta_archetypes.json");
			if (System.IO.File.Exists(metaPath))
			{
				var metaJson = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<MetaArchetypeEntry>>>(
					System.IO.File.ReadAllText(metaPath));
				string charKey = character?.ToLowerInvariant() ?? deck?.Character?.ToLowerInvariant() ?? "";
				if (metaJson != null && metaJson.TryGetValue(charKey, out var archetypes) && archetypes.Count > 0)
				{
					advice.Add(("##", "메타 아키타입 Top 3", ClrAccent));
					int shown = 0;
					foreach (var arch in archetypes)
					{
						if (shown >= 3) break;
						string coreStr = arch.CoreCards != null && arch.CoreCards.Count > 0
							? string.Join(", ", arch.CoreCards.Take(3))
							: "";
						advice.Add(("\u2B50", $"{arch.Archetype}: {arch.WinRate:P0} 승률 ({arch.SampleSize}게임)", ClrPositive));
						if (coreStr.Length > 0)
							advice.Add(("\u2022", $"핵심: {coreStr}", ClrAqua));
						shown++;
					}
				}
			}
		}
		catch { }

		// ─── Run Health Gauge ───
		if (Plugin.RunHealthComputer != null)
		{
			float archStr = deck?.DetectedArchetypes?.Count > 0 ? deck.DetectedArchetypes[0].Strength : 0f;
			int bossReady = 50;
			var bossHealthResults = BossAdvisor.Diagnose(deck, act, character, hp, maxHP);
			if (bossHealthResults.Count > 0)
				bossReady = (int)bossHealthResults[0].ReadinessScore;
			int health = Plugin.RunHealthComputer.CalculateHealth(hp, maxHP, gold, deck?.TotalCards ?? 0, floor, archStr, bossReady);
			string healthIcon = health >= 70 ? "\u2705" : health >= 45 ? "\u26a0" : "\u274c";
			Color healthColor = health >= 70 ? ClrPositive : health >= 45 ? ClrExpensive : ClrNegative;
			advice.Insert(0, ("##", "런 건강도", ClrAccent));
			advice.Insert(1, (healthIcon, $"건강도: {health}/100", healthColor));
		}

		if (advice.Count == 0)
		{
			advice.Add(("\u2714", "균형 잡힌 상태 — 덱 강점을 살리세요", ClrCream));
		}

		return advice;
	}

	/// <summary>
	/// Get patch change badge text for a card or relic.
	/// Returns null if no recent changes.
	/// </summary>
	private string GetPatchChangeBadge(string entityType, string entityId)
	{
		try
		{
			var changes = Plugin.RunDatabase?.GetRecentPatchChanges(entityType, entityId, 3);
			if (changes == null || changes.Count == 0) return null;
			var latest = changes[0];
			if (latest.OldValue != null && latest.NewValue != null)
				return $"\u26a1 {latest.Property}: {latest.OldValue}\u2192{latest.NewValue}";
			return $"\u26a1 최근 변경: {latest.Property}";
		}
		catch { return null; }
	}

	/// <summary>
	/// Get floor/act-specific tier info for display.
	/// Returns null if no data available.
	/// </summary>
	private string GetFloorTierInfo(string cardId, string character, int act)
	{
		try
		{
			var stat = Plugin.RunDatabase?.GetFloorCardStat(cardId, character, act);
			if (stat == null || stat.SampleSize < 3) return null;
			return $"Act {act} 승률 {stat.WinRate:P0} ({stat.SampleSize}게임)";
		}
		catch { return null; }
	}
}
