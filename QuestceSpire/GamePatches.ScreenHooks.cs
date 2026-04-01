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
	public static void OnRelicRewardOpened(NChooseARelicSelection __result, IReadOnlyList<RelicModel> relics)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Relic reward screen detected — analyzing...");
			RecordHook("OnRelicRewardOpened");
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(relics);
			GameStateReader.SetLastMerchantInventory(null);
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				List<ScoredRelic> relics2 = Plugin.SynergyScorer.ScoreRelicOfferings(gameState.OfferedRelics, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
				Plugin.Coordinator?.ShowRelicAdvice(__result, relics2, deckAnalysis, gameState.Character);
				// Inject grade badges directly onto game relic nodes
				// Relic badge injection removed — overlay panel handles relic screens
				bool isBossRelic = gameState.OfferedRelics.Count > 0 && gameState.OfferedRelics.TrueForAll(r => string.Equals(r.Rarity, "Boss", StringComparison.OrdinalIgnoreCase));
				DecisionEventType relicEventType = isBossRelic ? DecisionEventType.BossRelic : DecisionEventType.RelicReward;
				Plugin.RunTracker?.RecordDecision(relicEventType, gameState.OfferedRelics.ConvertAll((RelicInfo r) => r.Id), null, gameState.DeckCards.ConvertAll((CardInfo c) => c.Id), gameState.CurrentRelics.ConvertAll((RelicInfo r) => r.Id), gameState.CurrentHP, gameState.MaxHP, gameState.Gold, gameState.ActNumber, gameState.Floor);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnRelicRewardOpened error: {value}");
		}
	}

	public static void OnShopOpened(NMerchantInventory __instance)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Shop screen detected — analyzing...");
			RecordHook("OnShopOpened");
			MerchantInventory inventory = __instance.Inventory;
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(inventory);
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				List<ScoredCard> cards = Plugin.SynergyScorer.ScoreOfferings(gameState.ShopCards, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
				List<ScoredRelic> relics = Plugin.SynergyScorer.ScoreRelicOfferings(gameState.ShopRelics, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
				Plugin.Coordinator?.ShowShopAdvice(__instance, cards, relics, deckAnalysis, gameState.Character);
				// Shop decisions not recorded — no purchase hook means chosenId is always null,
				// and mixed card+relic offered IDs corrupt card stats. Shop tracking deferred
				// until proper purchase event hooking is implemented.
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnShopOpened error: {value}");
		}
	}

	public static void OnRestSiteOpened(NRestSiteRoom __result)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Rest site detected — showing upgrade advice...");
			RecordHook("OnRestSiteOpened");
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				Plugin.Coordinator?.ShowRestSiteAdvice(__result, deckAnalysis, gameState.CurrentHP, gameState.MaxHP, gameState.ActNumber, gameState.Floor, gameState);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnRestSiteOpened error: {value}");
		}
	}

	public static void OnUpgradeScreenOpened(object __result, IReadOnlyList<CardModel> cards)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log($"Upgrade card selection detected — {cards?.Count ?? 0} cards offered...");
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState == null) return;

			string character = gameState.Character ?? "unknown";
			DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);

			Node upgradeNode = __result as Node;
			// Convert offered CardModels to CardInfo and score for upgrade delta
			if (cards != null && cards.Count > 0)
			{
				var offeredCards = new List<CardInfo>();
				foreach (var card in cards)
				{
					if (card != null)
						offeredCards.Add(GameStateReader.CardModelToInfo(card));
				}
				if (offeredCards.Count > 0)
				{
					var scored = Plugin.SynergyScorer.ScoreForUpgrade(offeredCards, deckAnalysis, character,
						gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
					Plugin.Coordinator?.ShowCardAdvice(upgradeNode, scored, deckAnalysis, character, "CARD UPGRADE");
					Plugin.BadgeManager?.CleanupAllBadges();
					return;
				}
			}

			// Fallback: show upgrade advice via card reward injector with empty cards
			Plugin.Coordinator?.ShowCardAdvice(upgradeNode, new List<ScoredCard>(), deckAnalysis, character, "CARD UPGRADE");
		}
		catch (Exception value)
		{
			Plugin.Log($"OnUpgradeScreenOpened error: {value}");
		}
	}

	public static void OnCombatSetup(NCombatRoom __result, object __0)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Combat room detected — showing deck status...");
			RecordHook("OnCombatSetup");
			List<string> enemyIds = null;
			try
			{
				// NCombatRoom.Create(ICombatRoomVisuals visuals, CombatRoomMode mode)
				// visuals is a CombatRoom with Encounter property → EncounterModel → Id.Entry
				string encounterId = null;
				if (__0 != null)
				{
					// __0 = ICombatRoomVisuals (CombatRoom). Get Encounter.Id.Entry
					var encounterProp = __0.GetType().GetProperty("Encounter",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					var encounterObj = encounterProp?.GetValue(__0);
					if (encounterObj != null)
					{
						var idProp = encounterObj.GetType().GetProperty("Id",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						var idObj = idProp?.GetValue(encounterObj);
						if (idObj != null)
						{
							var entryProp = idObj.GetType().GetProperty("Entry",
								BindingFlags.Instance | BindingFlags.Public);
							encounterId = entryProp?.GetValue(idObj)?.ToString();
						}
						Plugin.Log($"Encounter from visuals: {encounterId} (type={encounterObj.GetType().Name})");
						// Extract localized encounter name via Title or Name property
						try
						{
							var encTitleProp = encounterObj.GetType().GetProperty("Title",
								BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							string encLocalName = encTitleProp?.GetValue(encounterObj)?.ToString();
							if (string.IsNullOrEmpty(encLocalName))
							{
								var encNameProp = encounterObj.GetType().GetProperty("Name",
									BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								encLocalName = encNameProp?.GetValue(encounterObj)?.ToString();
							}
							if (encounterId != null && !string.IsNullOrEmpty(encLocalName))
							{
								GameStateReader.CacheLocalizedName("enemy", encounterId, encLocalName);
								Plugin.Log($"Encounter localized: {encounterId} → {encLocalName}");
							}
						}
						catch (Exception ex) { Plugin.Log($"Failed to extract encounter localized name: {ex.Message}"); }
						try
						{
							var eProps = encounterObj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							Plugin.Log($"EncounterObj type: {encounterObj.GetType().FullName}, props: {string.Join(", ", eProps.Select(p => $"{p.Name}:{p.PropertyType.Name}"))}");
						}
						catch (Exception ex) { Plugin.Log($"Failed to log encounter object properties: {ex.Message}"); }
					}
					// Also try to get enemy names from Enemies property
					if (encounterId == null)
					{
						var enemiesProp = __0.GetType().GetProperty("Enemies",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						if (enemiesProp != null)
						{
							var enemiesObj = enemiesProp.GetValue(__0);
							if (enemiesObj is System.Collections.IEnumerable enemies)
							{
								var tempIds = new List<string>();
								foreach (var enemy in enemies)
								{
									if (enemy == null) continue;
									var eidProp = enemy.GetType().GetProperty("Id",
										BindingFlags.Instance | BindingFlags.Public);
									var eidObj = eidProp?.GetValue(enemy);
									var eEntryProp = eidObj?.GetType().GetProperty("Entry",
										BindingFlags.Instance | BindingFlags.Public);
									var entry = eEntryProp?.GetValue(eidObj)?.ToString();
									if (entry != null) tempIds.Add(entry);
									// Cache localized enemy name
									try {
										var eTitleP = enemy.GetType().GetProperty("Title", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
										string eName = eTitleP?.GetValue(enemy)?.ToString();
										if (string.IsNullOrEmpty(eName)) { var eNP = enemy.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); eName = eNP?.GetValue(enemy)?.ToString(); }
										if (entry != null && !string.IsNullOrEmpty(eName)) GameStateReader.CacheLocalizedName("enemy", entry, eName);
									} catch (Exception ex) { Plugin.Log($"Failed to cache localized enemy name: {ex.Message}"); }
								}
								if (tempIds.Count > 0)
								{
									enemyIds = tempIds;
									Plugin.Log($"Enemy IDs from Enemies: {string.Join(", ", tempIds)}");
								}
							}
						}
					}
				}
				if (encounterId != null)
				{
					// Encounter Entry is Title Case with spaces (e.g., "Slimes Weak")
					// Convert to UPPER_SNAKE_CASE for lookup (e.g., "SLIMES_WEAK")
					string snakeId = encounterId.Replace(" ", "_").ToUpperInvariant();
					enemyIds = new List<string> { snakeId };
					// Also add variant-stripped version
					string baseId = System.Text.RegularExpressions.Regex.Replace(snakeId, @"_(WEAK|ELITE|STRONG|BOSS|HARD|EASY|NORMAL)$", "");
					if (baseId != snakeId)
						enemyIds.Add(baseId);
					// Also add the original Title Case for flexible matching
					enemyIds.Add(encounterId);
					Plugin.Log($"Encounter IDs: {string.Join(", ", enemyIds)}");
				}
				else if (enemyIds == null)
				{
					// Fallback: try RunManager current room
					try
					{
						var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
						if (rmType != null)
						{
							var instProp = rmType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
							var rm = instProp?.GetValue(null);
							var runStateProp = rm?.GetType().GetProperty("RunState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							var runState = runStateProp?.GetValue(rm);
							var curRoomProp = runState?.GetType().GetProperty("CurrentRoom", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
							var curRoom = curRoomProp?.GetValue(runState);
							if (curRoom != null)
							{
								var encProp = curRoom.GetType().GetProperty("Encounter",
									BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
								var enc = encProp?.GetValue(curRoom);
								var idP = enc?.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
								var idO = idP?.GetValue(enc);
								var entP = idO?.GetType().GetProperty("Entry", BindingFlags.Instance | BindingFlags.Public);
								var entry = entP?.GetValue(idO)?.ToString();
								if (entry != null)
								{
									string sId = entry.Replace(" ", "_").ToUpperInvariant();
									enemyIds = new List<string> { sId, entry };
									Plugin.Log($"Encounter from RunManager: {entry} → {sId}");
								}
							}
						}
					}
					catch (Exception ex2)
					{
						Plugin.Log($"RunManager fallback failed: {ex2.Message}");
					}
				}
				if (enemyIds == null || enemyIds.Count == 0)
					Plugin.Log("No encounter ID found for combat room.");
			}
			catch (Exception ex)
			{
				Plugin.Log($"Enemy ID extraction failed: {ex.Message}");
			}
			// Reset combat state for the new encounter
			string primaryEnemyId = enemyIds?.Count > 0 ? enemyIds[0] : "unknown";
			OnCombatStarted(primaryEnemyId);

			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				Plugin.Coordinator?.ShowCombatAdvice(__result, deckAnalysis, gameState.CurrentHP, gameState.MaxHP, gameState.ActNumber, gameState.Floor, gameState, enemyIds);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnCombatSetup error: {value}");
		}
	}

	public static void OnEventShowChoices(NEventRoom __result)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Event screen detected — showing context...");
			RecordHook("OnEventShowChoices");
			string eventId = null;
			string eventLocalName = null;
			try
			{
				// Try extracting event ID via reflection: NEventRoom → Event → Id → Entry
				var eventProp = __result.GetType().GetProperty("Event",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				var eventObj = eventProp?.GetValue(__result);
				if (eventObj != null)
				{
					var idProp = eventObj.GetType().GetProperty("Id",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					var idObj = idProp?.GetValue(eventObj);
					if (idObj != null)
					{
						var entryProp = idObj.GetType().GetProperty("Entry",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						eventId = entryProp?.GetValue(idObj)?.ToString();
					}
					// Extract localized name: Event.Title or Event.Name (LocString → ToString())
					var titleProp = eventObj.GetType().GetProperty("Title",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					eventLocalName = titleProp?.GetValue(eventObj)?.ToString();
					if (string.IsNullOrEmpty(eventLocalName))
					{
						var nameProp = eventObj.GetType().GetProperty("Name",
							BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						eventLocalName = nameProp?.GetValue(eventObj)?.ToString();
					}
				}
				if (eventId != null)
					Plugin.Log($"Event ID: {eventId}, LocalName: {eventLocalName ?? "(null)"}");
				{
					try
					{
						var props = eventObj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
						Plugin.Log($"EventObj type: {eventObj.GetType().FullName}, props: {string.Join(", ", props.Select(p => $"{p.Name}:{p.PropertyType.Name}"))}");
					}
					catch (Exception ex) { Plugin.Log($"Failed to log event object properties: {ex.Message}"); }
				}
			}
			catch (Exception ex)
			{
				Plugin.Log($"Event ID extraction failed: {ex.Message}");
			}
			// Cache localized name for overlay display
			if (eventId != null && eventLocalName != null)
				GameStateReader.CacheLocalizedName("event", eventId, eventLocalName);
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				Plugin.Coordinator?.ShowEventAdvice(__result, deckAnalysis, gameState.CurrentHP, gameState.MaxHP, gameState.Gold, gameState.ActNumber, gameState.Floor, eventId);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnEventShowChoices error: {value}");
		}
	}

	public static void OnMapScreenEntered(NMapScreen __result)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Map screen detected — generating path advice...");
			RecordHook("OnMapScreenEntered");
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				Plugin.Coordinator?.ShowMapAdvice(__result, deckAnalysis, gameState.CurrentHP, gameState.MaxHP, gameState.Gold, gameState.ActNumber, gameState.Floor);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnMapScreenEntered error: {value}");
		}
	}

	public static void OnCardRemovalOpened(NMerchantCardRemoval __instance)
	{
		try
		{
			EnsureOverlay();
			Plugin.Log("Card removal screen detected — analyzing deck for removal...");
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			GameState gameState = GameStateReader.ReadCurrentState();
			if (gameState != null)
			{
				DeckAnalysis deckAnalysis = Plugin.DeckAnalyzer.Analyze(gameState.Character, gameState.DeckCards, Plugin.TierEngine, gameState.CurrentRelics);
				Plugin.RunTracker?.RecordArchetypeSnapshot(gameState.Floor, deckAnalysis);
				List<ScoredCard> removalCandidates = Plugin.SynergyScorer.ScoreForRemoval(gameState.DeckCards, deckAnalysis, gameState.Character, gameState.ActNumber, gameState.Floor, Plugin.TierEngine, Plugin.AdaptiveScorer);
				Plugin.Coordinator?.ShowCardAdvice(__instance, removalCandidates, deckAnalysis, gameState.Character, "CARD REMOVAL");
				Plugin.RunTracker?.RecordDecision(DecisionEventType.CardRemove, gameState.DeckCards.ConvertAll((CardInfo c) => c.Id), null, gameState.DeckCards.ConvertAll((CardInfo c) => c.Id), gameState.CurrentRelics.ConvertAll((RelicInfo r) => r.Id), gameState.CurrentHP, gameState.MaxHP, gameState.Gold, gameState.ActNumber, gameState.Floor);
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnCardRemovalOpened error: {value}");
		}
	}

	public static void OnRelicSelected(object __0)
	{
		try
		{
			string text = null;
			if (__0 != null)
			{
				Type type = __0.GetType();
				PropertyInfo propertyInfo = type.GetProperty("Relic") ?? type.GetProperty("RelicModel") ?? type.GetProperty("Model");
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
			}
			Plugin.Log("Relic picked: " + (text ?? "(unknown)"));
			Plugin.RunTracker?.UpdateLastDecisionChoice(text);
			_isGenuineCardReward = false;
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			Plugin.Coordinator?.Clear();
			Plugin.BadgeManager?.CleanupAllBadges();
		}
		catch (Exception value2)
		{
			Plugin.Log($"OnRelicSelected error: {value2}");
		}
	}
}
