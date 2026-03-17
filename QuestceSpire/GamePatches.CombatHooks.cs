using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire;

/// <summary>
/// Harmony patches for combat events (turns, card plays, damage),
/// potion events, and shop purchases.
/// </summary>
public static partial class GamePatches
{
	// ─── Combat Turn Tracking (v0.10.1) ───

	private static int _currentTurnNumber;
	private static List<string> _turnCardsPlayed = new();
	private static int _turnDamageDealt;
	private static int _turnDamageTaken;
	private static int _turnBlockGenerated;
	private static string _currentCombatEnemyId;

	public static void OnTurnStart()
	{
		try
		{
			RecordHook("OnTurnStart");
			_currentTurnNumber++;
			_turnCardsPlayed = new List<string>();
			_turnDamageDealt = 0;
			_turnDamageTaken = 0;
			_turnBlockGenerated = 0;
		}
		catch (Exception ex) { Plugin.Log($"OnTurnStart error: {ex.Message}"); }
	}

	public static void OnTurnEnd()
	{
		try
		{
			RecordHook("OnTurnEnd");
			if (Plugin.CombatLogger == null) return;

			var gs = GameStateReader.ReadCurrentState();
			int playerHp = gs?.CurrentHP ?? 0;
			string runId = Plugin.RunTracker?.CurrentRunId;
			int floor = gs?.Floor ?? 0;

			Plugin.CombatLogger.RecordTurn(new CombatTurnRecord
			{
				RunId = runId ?? "unknown",
				Floor = floor,
				EnemyId = _currentCombatEnemyId,
				TurnNumber = _currentTurnNumber,
				CardsPlayed = new List<string>(_turnCardsPlayed),
				DamageDealt = _turnDamageDealt,
				DamageTaken = _turnDamageTaken,
				BlockGenerated = _turnBlockGenerated,
				PlayerHp = playerHp
			});
		}
		catch (Exception ex) { Plugin.Log($"OnTurnEnd error: {ex.Message}"); }
	}

	public static void OnCardPlayed(CardModel card)
	{
		try
		{
			RecordHook("OnCardPlayed");
			string cardId = card?.Definition?.Id ?? "unknown";
			_turnCardsPlayed.Add(cardId);
		}
		catch (Exception ex) { Plugin.Log($"OnCardPlayed error: {ex.Message}"); }
	}

	public static void OnDamageDealt(int amount)
	{
		try
		{
			_turnDamageDealt += Math.Max(0, amount);
		}
		catch { }
	}

	public static void OnDamageTaken(int amount)
	{
		try
		{
			_turnDamageTaken += Math.Max(0, amount);
		}
		catch { }
	}

	public static void OnBlockGenerated(int amount)
	{
		try
		{
			_turnBlockGenerated += Math.Max(0, amount);
		}
		catch { }
	}

	public static void OnCombatStarted(string enemyId)
	{
		_currentTurnNumber = 0;
		_currentCombatEnemyId = enemyId;
		_turnCardsPlayed = new List<string>();
	}

	// ─── Potion Tracking (v0.10.2) ───

	public static void OnPotionObtained(object potion)
	{
		try
		{
			RecordHook("OnPotionObtained");
			string potionId = ExtractPotionId(potion);
			if (potionId == null) return;

			var gs = GameStateReader.ReadCurrentState();
			Plugin.PotionTracker?.RecordEvent(new PotionEvent
			{
				RunId = Plugin.RunTracker?.CurrentRunId ?? "unknown",
				PotionId = potionId,
				EventType = "obtained",
				Floor = gs?.Floor ?? 0
			});
		}
		catch (Exception ex) { Plugin.Log($"OnPotionObtained error: {ex.Message}"); }
	}

	public static void OnPotionUsed(object potion)
	{
		try
		{
			RecordHook("OnPotionUsed");
			string potionId = ExtractPotionId(potion);
			if (potionId == null) return;

			var gs = GameStateReader.ReadCurrentState();
			Plugin.PotionTracker?.RecordEvent(new PotionEvent
			{
				RunId = Plugin.RunTracker?.CurrentRunId ?? "unknown",
				PotionId = potionId,
				EventType = "used",
				Floor = gs?.Floor ?? 0,
				EnemyId = _currentCombatEnemyId
			});
		}
		catch (Exception ex) { Plugin.Log($"OnPotionUsed error: {ex.Message}"); }
	}

	public static void OnPotionDiscarded(object potion)
	{
		try
		{
			RecordHook("OnPotionDiscarded");
			string potionId = ExtractPotionId(potion);
			if (potionId == null) return;

			var gs = GameStateReader.ReadCurrentState();
			Plugin.PotionTracker?.RecordEvent(new PotionEvent
			{
				RunId = Plugin.RunTracker?.CurrentRunId ?? "unknown",
				PotionId = potionId,
				EventType = "discarded",
				Floor = gs?.Floor ?? 0
			});
		}
		catch (Exception ex) { Plugin.Log($"OnPotionDiscarded error: {ex.Message}"); }
	}

	private static string ExtractPotionId(object potion)
	{
		if (potion == null) return null;
		try
		{
			// Try common property names via reflection
			var idProp = potion.GetType().GetProperty("Id") ?? potion.GetType().GetProperty("PotionId");
			if (idProp != null)
				return idProp.GetValue(potion)?.ToString();
			var defProp = potion.GetType().GetProperty("Definition");
			if (defProp != null)
			{
				var def = defProp.GetValue(potion);
				var defId = def?.GetType().GetProperty("Id");
				return defId?.GetValue(def)?.ToString();
			}
			return potion.ToString();
		}
		catch { return potion.ToString(); }
	}

	// ─── Shop Purchase Tracking (v0.10.3) ───

	public static void OnShopCardPurchased(CardModel card)
	{
		try
		{
			RecordHook("OnShopCardPurchased");
			string cardId = card?.Definition?.Id ?? "unknown";
			var gs = GameStateReader.ReadCurrentState();
			Plugin.RunTracker?.RecordDecision(
				DecisionEventType.ShopCard,
				new List<string> { cardId },
				cardId,
				gs?.DeckCards?.ConvertAll(c => c.Id),
				gs?.CurrentRelics?.ConvertAll(r => r.Id),
				gs?.CurrentHP ?? 0,
				gs?.MaxHP ?? 0,
				gs?.Gold ?? 0,
				gs?.ActNumber ?? 1,
				gs?.Floor ?? 0
			);
			Plugin.Log($"Shop purchase recorded: {cardId}");
		}
		catch (Exception ex) { Plugin.Log($"OnShopCardPurchased error: {ex.Message}"); }
	}

	public static void OnShopRelicPurchased(object relic)
	{
		try
		{
			RecordHook("OnShopRelicPurchased");
			string relicId = "unknown";
			try
			{
				var defProp = relic?.GetType().GetProperty("Definition");
				var def = defProp?.GetValue(relic);
				relicId = def?.GetType().GetProperty("Id")?.GetValue(def)?.ToString() ?? "unknown";
			}
			catch { }

			var gs = GameStateReader.ReadCurrentState();
			Plugin.RunTracker?.RecordDecision(
				DecisionEventType.ShopRelic,
				new List<string> { relicId },
				relicId,
				gs?.DeckCards?.ConvertAll(c => c.Id),
				gs?.CurrentRelics?.ConvertAll(r => r.Id),
				gs?.CurrentHP ?? 0,
				gs?.MaxHP ?? 0,
				gs?.Gold ?? 0,
				gs?.ActNumber ?? 1,
				gs?.Floor ?? 0
			);
			Plugin.Log($"Shop relic purchase recorded: {relicId}");
		}
		catch (Exception ex) { Plugin.Log($"OnShopRelicPurchased error: {ex.Message}"); }
	}

	/// <summary>
	/// Apply combat, potion, and shop purchase Harmony patches.
	/// Called from ApplyManualPatches.
	/// </summary>
	public static void ApplyCombatPatches(Harmony harmony)
	{
		// Combat turn patches — try to find turn management types via reflection
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Combat.CombatManager", "StartPlayerTurn", nameof(OnTurnStart));
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Combat.CombatManager", "EndPlayerTurn", nameof(OnTurnEnd));
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Combat.CombatManager", "PlayCard", nameof(OnCardPlayed));

		// Potion patches
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Entities.Players.PlayerModel", "AddPotion", nameof(OnPotionObtained));
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Entities.Players.PlayerModel", "UsePotion", nameof(OnPotionUsed));
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Entities.Players.PlayerModel", "DiscardPotion", nameof(OnPotionDiscarded));

		// Shop purchase patches
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory", "PurchaseCard", nameof(OnShopCardPurchased));
		TryPatchByName(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory", "PurchaseRelic", nameof(OnShopRelicPurchased));
	}

	private static void TryPatchByName(Harmony harmony, string typeName, string methodName, string postfixName)
	{
		try
		{
			var type = AccessTools.TypeByName(typeName);
			if (type == null)
			{
				Plugin.Log($"CombatHooks: type {typeName} not found — patch skipped.");
				return;
			}
			PatchMethod(harmony, type, methodName, postfixName);
		}
		catch (Exception ex)
		{
			Plugin.Log($"CombatHooks: failed to patch {typeName}.{methodName}: {ex.Message}");
		}
	}
}
