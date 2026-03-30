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
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Runs;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;
using QuestceSpire.UI;

namespace QuestceSpire;

public static partial class GamePatches
{
	// Debug: track when each hook last fired
	public static System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> HookLastFired { get; } = new();

	private static void RecordHook(string hookName)
	{
		HookLastFired[hookName] = DateTime.Now;
		// Any non-card-reward hook clears the genuine card reward flag, active screen reference,
		// and dedup fingerprint so the next card reward is always recorded
		if (hookName != "OnCardRewardOpened" && hookName != "OnCardRewardRefreshed")
		{
			_isGenuineCardReward = false;
			_activeCardRewardScreen = null;
			_lastCardRewardFingerprint = null;
		}
	}

	private static void EnsureOverlay()
	{
		if (Plugin.Coordinator == null)
		{
			try
			{
				var settings = OverlaySettings.Load();
				Plugin.Coordinator = new OverlayCoordinator(settings);
				Plugin.Log("Coordinator created.");
			}
			catch (Exception value)
			{
				Plugin.Log($"Coordinator creation failed: {value}");
			}
		}

		if (Plugin.BadgeManager == null)
		{
			try
			{
				bool showBadges = Plugin.Coordinator?.Settings?.ShowInGameBadges ?? true;
				Plugin.BadgeManager = new BadgeManager(showBadges);
				Plugin.Log("BadgeManager created.");
			}
			catch (Exception value)
			{
				Plugin.Log($"BadgeManager creation failed: {value}");
			}
		}
	}

	public static void OnRunLaunched(RunManager __instance, RunState __result)
	{
		try
		{
			EnsureOverlay();
			RecordHook("OnRunLaunched");
			if (__result != null)
			{
				// Use LocalContext.GetMe() for multiplayer safety
				Player player = null;
				try { player = LocalContext.GetMe(__result); } catch (Exception ex) { Plugin.Log($"OnRunLaunched GetMe error: {ex.Message}"); }
				player ??= __result.Players?.FirstOrDefault();
				string text = "unknown";
				int ascensionLevel = __result.AscensionLevel;
				if (player?.Character?.Id != null)
				{
					text = player.Character.Id.Entry?.ToLowerInvariant() ?? "unknown";
				}
				Plugin.RunTracker?.StartRun(text, "", ascensionLevel);
				Plugin.Log($"Run launched: {text} A{ascensionLevel}");
			}
		}
		catch (Exception value)
		{
			Plugin.Log($"OnRunLaunched error: {value}");
		}
	}

	public static void OnRunEnded(RunManager __instance, bool __0)
	{
		try
		{
			RecordHook("OnRunEnded");
			GameStateReader.SetLastCardOptions(null);
			GameStateReader.SetLastRelicOptions(null);
			GameStateReader.SetLastMerchantInventory(null);
			GameState gameState = GameStateReader.ReadCurrentState();
			int num = gameState?.Floor ?? 0;
			int num2 = gameState?.ActNumber ?? 0;
			RunOutcome runOutcome = (!__0 ? RunOutcome.Loss : RunOutcome.Win);
			Plugin.Coordinator?.ShowRunSummary(runOutcome, num, num2);
			Plugin.RunTracker?.EndRun(runOutcome, num, num2);
			// ApplyCachedStats: recompute local + merge cached cloud data once (no double-counting)
			if (Plugin.CloudSync != null)
				Plugin.CloudSync.ApplyCachedStats();
			else
				Plugin.LocalStats?.RecomputeAll();
			if (Plugin.Coordinator?.Settings?.CloudSyncEnabled ?? false)
				Task.Run(() => Plugin.CloudSync?.UploadPendingRuns());
			Plugin.Log($"Run ended: {runOutcome} on floor {num} (act {num2})");
		}
		catch (Exception value)
		{
			Plugin.Log($"OnRunEnded error: {value}");
		}
	}

	public static void ApplyManualPatches(Harmony harmony)
	{
		PatchMethod(harmony, typeof(NCardRewardSelectionScreen), "SelectCard", nameof(OnCardSelected));
		PatchMethod(harmony, typeof(NChooseARelicSelection), "SelectHolder", nameof(OnRelicSelected));
		PatchMethod(harmony, typeof(NCardRewardSelectionScreen), "_Ready", nameof(OnCardRewardScreenReady));
		PatchMethod(harmony, typeof(NCardRewardSelectionScreen), "ShowScreen", nameof(OnCardRewardOpened));
		PatchMethod(harmony, typeof(NCardRewardSelectionScreen), "RefreshOptions", nameof(OnCardRewardRefreshed));
		PatchMethod(harmony, typeof(NChooseARelicSelection), "ShowScreen", nameof(OnRelicRewardOpened));
		PatchMethod(harmony, typeof(NMerchantInventory), "Open", nameof(OnShopOpened));
		// NMerchantCardRemoval.FillSlot fires on shop load, not user click — skip it
		// Card removal advice shown as part of shop screen instead
		PatchMethod(harmony, typeof(NMapScreen), "Open", nameof(OnMapScreenEntered));
		PatchMethod(harmony, typeof(NEventRoom), "Create", nameof(OnEventShowChoices));
		PatchMethod(harmony, typeof(NCombatRoom), "Create", nameof(OnCombatSetup));
		PatchMethod(harmony, typeof(NRestSiteRoom), "Create", nameof(OnRestSiteOpened));
		// Upgrade card selection screen — use Type.GetType since it may not be directly referenced
		try
		{
			var upgradeScreenType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen");
			if (upgradeScreenType != null)
				PatchMethod(harmony, upgradeScreenType, "ShowScreen", nameof(OnUpgradeScreenOpened));
			else
				Plugin.Log("WARN: NDeckUpgradeSelectScreen not found — upgrade advice unavailable");
		}
		catch (Exception ex) { Plugin.Log($"WARN: Upgrade screen patch failed: {ex.Message}"); }
		PatchMethod(harmony, typeof(RunManager), "Launch", nameof(OnRunLaunched));
		PatchMethod(harmony, typeof(RunManager), "OnEnded", nameof(OnRunEnded));

		// v0.10: Combat, potion, and shop purchase patches
		ApplyCombatPatches(harmony);
	}

	private static void PatchMethod(Harmony harmony, Type targetType, string methodName, string postfixName)
	{
		try
		{
			MethodInfo target = AccessTools.Method(targetType, methodName);
			if (target == null)
			{
				Plugin.Log($"WARN: Could not find {targetType.Name}.{methodName}");
				return;
			}
			MethodInfo postfix = typeof(GamePatches).GetMethod(postfixName, BindingFlags.Static | BindingFlags.Public);
			if (postfix == null)
			{
				Plugin.Log($"WARN: Could not find postfix {postfixName}");
				return;
			}
			harmony.Patch(target, postfix: new HarmonyMethod(postfix));
			Plugin.Log($"Patched {targetType.Name}.{methodName} (static={target.IsStatic})");
		}
		catch (Exception ex)
		{
			Plugin.Log($"WARN: Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
		}
	}

	public static void ForceNotModded(ref bool __result)
	{
		__result = false;
	}
}
