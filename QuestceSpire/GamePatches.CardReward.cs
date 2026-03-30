using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;
using QuestceSpire.UI;

namespace QuestceSpire;

public static partial class GamePatches
{
	// Track the screen instance that ShowScreen was called on, so _Ready/retry
	// don't inject badges on unrelated screens (discard pile, removal, etc.)
	private static WeakReference<NCardRewardSelectionScreen> _activeCardRewardScreen;

	// Whitelist flag: true only when a genuine card reward ShowScreen/RefreshOptions was detected
	// Prevents badge injection on screens that reuse NCardRewardSelectionScreen (events, upgrades, etc.)
	public static bool IsGenuineCardReward => _isGenuineCardReward;
	private static bool _isGenuineCardReward;

	// Dedup: track last recorded card reward to prevent ShowScreen+RefreshOptions double-recording
	private static string _lastCardRewardFingerprint;

	public static void OnCardRewardScreenReady(NCardRewardSelectionScreen __instance)
	{
		EnsureOverlay();
		// Only retry for screens that ShowScreen was called on (not discard pile, etc.)
		if (_activeCardRewardScreen == null || !_activeCardRewardScreen.TryGetTarget(out var active) || active != __instance)
			return;
		ScheduleRetry(() => TryShowCardRewardFromScreen(__instance), 5, 0.3);
	}

	public static void OnCardRewardOpened(NCardRewardSelectionScreen __result, IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> extraOptions)
	{
		try
		{
			// Always clean up existing badges first — prevents stale badges on pile viewers, etc.
			Plugin.BadgeManager?.CleanupAllBadges();
			// Draw/discard pile viewers use ShowScreen with 20+ cards — skip those
			// But multi-pick screens (e.g. "choose 2 from 9") have 6-15 cards — allow those
			if (options != null && options.Count > 15)
			{
				Plugin.Log($"ShowScreen with {options.Count} cards — likely pile viewer, skipping.");
				return;
			}
			if (options != null && options.Count > 5)
			{
				Plugin.Log($"ShowScreen with {options.Count} cards — multi-pick card selection detected.");
			}
			// Skip screens that have their own handlers and reuse NCardRewardSelectionScreen
			string curScreen = Plugin.Coordinator?.ActiveScreenName;
			if (curScreen == "CARD REMOVAL" || curScreen == "CARD UPGRADE" ||
			    curScreen == "MERCHANT SHOP" || curScreen == "EVENT CARD OFFER" ||
			    curScreen == "EVENT" || curScreen == "REST SITE")
			{
				Plugin.Log($"ShowScreen fired during {curScreen} — skipping card reward logic.");
				return;
			}
			EnsureOverlay();
			_activeCardRewardScreen = new WeakReference<NCardRewardSelectionScreen>(__result);
			_isGenuineCardReward = true;
			Plugin.Log("Card reward screen detected — analyzing...");
			RecordHook("OnCardRewardOpened");
			if (options != null)
				GameStateReader.SetLastCardOptions(options);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			if (!TryShowCardRewardFromScreen(__result))
			{
				Plugin.Log("Game state not ready for card reward, scheduling retry...");
				ScheduleRetry(() => TryShowCardRewardFromScreen(__result));
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnCardRewardOpened error: {value}");
		}
	}

	public static void OnCardRewardRefreshed(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> extraOptions)
	{
		try
		{
			if (options != null && options.Count > 15)
				return;
			string curScreen2 = Plugin.Coordinator?.ActiveScreenName;
			if (curScreen2 == "CARD REMOVAL" || curScreen2 == "CARD UPGRADE" ||
			    curScreen2 == "MERCHANT SHOP" || curScreen2 == "EVENT CARD OFFER" ||
			    curScreen2 == "EVENT" || curScreen2 == "REST SITE")
				return;
			EnsureOverlay();
			RecordHook("OnCardRewardRefreshed");
			_activeCardRewardScreen = new WeakReference<NCardRewardSelectionScreen>(__instance);
			_isGenuineCardReward = true;
			Plugin.Log("Card reward RefreshOptions detected — re-analyzing...");
			if (options != null)
				GameStateReader.SetLastCardOptions(options);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			if (!TryShowCardRewardFromScreen(__instance))
			{
				Plugin.Log("Game state not ready for card refresh, scheduling retry...");
				ScheduleRetry(() => TryShowCardRewardFromScreen(__instance));
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnCardRewardRefreshed error: {value}");
		}
	}

	private static bool TryShowCardRewardFromScreen(NCardRewardSelectionScreen screen)
	{
		// Guard: only proceed if this is still a genuine card reward context
		if (!_isGenuineCardReward)
		{
			Plugin.Log("TryShowCardRewardFromScreen skipped — not a genuine card reward");
			return true; // return true to stop retries
		}
		// Guard: verify the screen node is still valid and visible (retries may fire after screen changed)
		if (screen == null || !GodotObject.IsInstanceValid(screen) || !screen.IsVisibleInTree())
		{
			Plugin.Log("TryShowCardRewardFromScreen skipped — screen no longer valid/visible");
			return true;
		}
		// Try reading card options from the screen instance via reflection
		// This works even when Harmony parameter injection fails
		if (GameStateReader.GetLastCardOptions() == null || GameStateReader.GetLastCardOptions().Count == 0)
		{
			try
			{
				var cardsField = screen.GetType().GetField("_cards",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				var cardsProp = screen.GetType().GetProperty("Cards",
					BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				object cardsObj = cardsField?.GetValue(screen) ?? cardsProp?.GetValue(screen);
				if (cardsObj is IReadOnlyList<CardCreationResult> screenCards && screenCards.Count > 0)
				{
					Plugin.Log($"Read {screenCards.Count} cards from screen instance");
					GameStateReader.SetLastCardOptions(screenCards);
				}
				else
				{
					// Try getting card holders and extracting cards from children
					var holdersField = screen.GetType().GetField("_cardHolders",
						BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
					if (holdersField != null)
					{
						var holders = holdersField.GetValue(screen);
						if (holders is System.Collections.IList holderList && holderList.Count > 0)
						{
							var results = new List<CardCreationResult>();
							foreach (var holder in holderList)
							{
								var crProp = holder.GetType().GetProperty("CreationResult",
									BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								if (crProp?.GetValue(holder) is CardCreationResult cr)
									results.Add(cr);
							}
							if (results.Count > 0)
							{
								Plugin.Log($"Read {results.Count} cards from card holders");
								GameStateReader.SetLastCardOptions(results);
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log($"Failed to read cards from screen: {ex.Message}");
			}
		}

		GameState gameState = GameStateReader.ReadCurrentState();
		if (gameState == null) return false;
		if (gameState.ReflectionFailed)
		{
			Plugin.Log("Card reward: reflection failed to read game state");
			return false;
		}
		if (gameState.OfferedCards == null || gameState.OfferedCards.Count == 0)
		{
			Plugin.Log("Card reward: no offered cards in game state");
			return false;
		}

		DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
		Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
		List<ScoredCard> cards = Plugin.SynergyScorer.ScoreOfferings(gameState.OfferedCards, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
		Plugin.Coordinator?.ShowCardAdvice(screen, cards, deckAnalysis, gameState.Character);
		Plugin.BadgeManager?.InjectCardGrades(screen, cards);
		// Dedup: only record if this is a new offering (prevents ShowScreen+RefreshOptions double-recording)
		var offeredIds = gameState.OfferedCards.ConvertAll((CardInfo c) => c.Id);
		offeredIds.Sort();
		string fingerprint = $"{gameState.Floor}:{string.Join(",", offeredIds)}";
		if (fingerprint != _lastCardRewardFingerprint)
		{
			_lastCardRewardFingerprint = fingerprint;
			Plugin.RunTracker?.RecordDecision(DecisionEventType.CardReward, gameState.OfferedCards.ConvertAll((CardInfo c) => c.Id), null, gameState.DeckCards.ConvertAll((CardInfo c) => c.Id), gameState.CurrentRelics.ConvertAll((RelicInfo r) => r.Id), gameState.CurrentHP, gameState.MaxHP, gameState.Gold, gameState.ActNumber, gameState.Floor);
		}
		return true;
	}

	private static void ScheduleRetry(Func<bool> action, int retriesLeft = 3, double delay = 0.2)
	{
		try
		{
			var tree = (SceneTree)Engine.GetMainLoop();
			if (tree == null) return;
			// Capture the active screen reference so we can check validity before executing
			var screenRef = _activeCardRewardScreen;
			var timer = tree.CreateTimer(delay);
			timer.Timeout += () =>
			{
				try
				{
					// Check if the captured screen is still valid before executing
					if (screenRef != null && screenRef.TryGetTarget(out var targetNode))
					{
						if (!GodotObject.IsInstanceValid(targetNode))
						{
							Plugin.Log("ScheduleRetry skipped — captured screen node is no longer valid");
							return;
						}
					}
					if (!action() && retriesLeft > 1)
					{
						Plugin.Log($"Retry failed, {retriesLeft - 1} attempts remaining...");
						ScheduleRetry(action, retriesLeft - 1, delay);
					}
				}
				catch (Exception ex)
				{
					Plugin.Log($"Retry error: {ex.Message}");
				}
			};
		}
		catch (Exception ex)
		{
			Plugin.Log($"ScheduleRetry error: {ex.Message}");
		}
	}

	public static void OnCardSelected(object __0)
	{
		try
		{
			string text = null;
			if (__0 != null)
			{
				Type type = __0.GetType();
				PropertyInfo propertyInfo = type.GetProperty("Card") ?? type.GetProperty("CardModel");
				if (propertyInfo != null)
				{
					object value = propertyInfo.GetValue(__0);
					if (value != null)
					{
						object obj = value.GetType().GetProperty("Id")?.GetValue(value);
						if (obj != null)
						{
							text = obj.GetType().GetProperty("Entry")?.GetValue(obj)?.ToString();
						}
					}
				}
				if (text == null)
				{
					PropertyInfo property = type.GetProperty("CreationResult");
					if (property != null)
					{
						object value2 = property.GetValue(__0);
						object obj2 = (value2?.GetType().GetProperty("Card"))?.GetValue(value2);
						if (obj2 != null)
						{
							object obj3 = obj2.GetType().GetProperty("Id")?.GetValue(obj2);
							text = (obj3?.GetType().GetProperty("Entry"))?.GetValue(obj3)?.ToString();
						}
					}
				}
			}
			Plugin.Log("Card picked: " + (text ?? "(unknown)"));
			Plugin.RunTracker?.UpdateLastDecisionChoice(text);
			_isGenuineCardReward = false;
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			Plugin.Coordinator?.Clear();
			Plugin.BadgeManager?.CleanupAllBadges();
		}
		catch (Exception value3)
		{
			Plugin.Log($"OnCardSelected error: {value3}");
		}
	}
}
