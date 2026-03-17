using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace QuestceSpire.GameBridge;

/// <summary>
/// Reads real-time combat pile data via CombatManager.
/// Confirmed working API pattern from sts2decktracker.
/// </summary>
public static class CombatTracker
{
	public class CombatSnapshot
	{
		public List<CombatCardInfo> DrawPile { get; set; } = new();
		public List<CombatCardInfo> DiscardPile { get; set; } = new();
		public List<CombatCardInfo> Hand { get; set; } = new();
		public List<CombatCardInfo> ExhaustPile { get; set; } = new();
		public int DrawCount => DrawPile.Count;
		public int DiscardCount => DiscardPile.Count;
		public int HandCount => Hand.Count;
		public int ExhaustCount => ExhaustPile.Count;
		public int TotalCards => DrawCount + DiscardCount + HandCount + ExhaustCount;
		public DateTime Timestamp { get; set; } = DateTime.Now;
	}

	public class CombatCardInfo
	{
		public string Id { get; set; } = "";
		public string Name { get; set; } = "";
		public string Type { get; set; } = ""; // Attack/Skill/Power
		public int Cost { get; set; }
		public bool CostsX { get; set; }
		public bool IsUpgraded { get; set; }
		public string Enchantment { get; set; } = "";
		public string Rarity { get; set; } = "";
	}

	/// <summary>
	/// Returns true if a combat is currently in progress.
	/// </summary>
	public static bool IsInCombat()
	{
		try
		{
			return CombatManager.Instance != null && CombatManager.Instance.IsInProgress;
		}
		catch { return false; }
	}

	/// <summary>
	/// Takes a snapshot of all combat piles. Returns null if not in combat.
	/// </summary>
	public static CombatSnapshot TakeSnapshot()
	{
		try
		{
			if (!IsInCombat()) return null;

			var combatState = CombatManager.Instance.DebugOnlyGetState();
			if (combatState == null) return null;

			var player = combatState.Players[0];
			if (player?.PlayerCombatState == null) return null;

			var snapshot = new CombatSnapshot();

			var pcs = player.PlayerCombatState;

			if (pcs.DrawPile?.Cards != null)
				snapshot.DrawPile = ReadPile(pcs.DrawPile);

			if (pcs.DiscardPile?.Cards != null)
				snapshot.DiscardPile = ReadPile(pcs.DiscardPile);

			// Hand — try via reflection (PlayerCombatState.Hand may exist)
			try
			{
				var handProp = pcs.GetType().GetProperty("Hand",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				if (handProp?.GetValue(pcs) is CardPile handPile && handPile.Cards != null)
					snapshot.Hand = ReadPile(handPile);
			}
			catch { }

			// Exhaust pile — try via reflection
			try
			{
				var exhaustProp = pcs.GetType().GetProperty("ExhaustPile",
					System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				if (exhaustProp?.GetValue(pcs) is CardPile exhaustPile && exhaustPile.Cards != null)
					snapshot.ExhaustPile = ReadPile(exhaustPile);
			}
			catch { }

			return snapshot;
		}
		catch (Exception ex)
		{
			Plugin.Log($"CombatTracker.TakeSnapshot error: {ex.Message}");
			return null;
		}
	}

	/// <summary>
	/// Calculate probability of drawing specific cards next turn.
	/// Returns dict of cardName -> probability (0.0 to 1.0)
	/// </summary>
	public static Dictionary<string, float> CalculateDrawProbabilities(CombatSnapshot snapshot, int drawCount = 5)
	{
		var probs = new Dictionary<string, float>();
		if (snapshot == null || snapshot.DrawCount == 0) return probs;

		int pileSize = snapshot.DrawCount;
		int actualDraw = Math.Min(drawCount, pileSize);

		// Group cards in draw pile by name
		var groups = snapshot.DrawPile
			.GroupBy(c => c.Name + (c.IsUpgraded ? " +" : ""))
			.ToDictionary(g => g.Key, g => g.Count());

		foreach (var kvp in groups)
		{
			// Hypergeometric: P(at least 1) = 1 - C(N-K, n) / C(N, n)
			// where N = pile size, K = copies, n = draw count
			int copies = kvp.Value;
			float probNone = 1.0f;
			for (int i = 0; i < actualDraw; i++)
			{
				probNone *= (float)(pileSize - copies - i) / (pileSize - i);
				if (probNone <= 0) { probNone = 0; break; }
			}
			probs[kvp.Key] = 1.0f - probNone;
		}

		return probs.OrderByDescending(p => p.Value)
			.ToDictionary(p => p.Key, p => p.Value);
	}

	/// <summary>
	/// Get a summary of deck composition by type (Attack/Skill/Power).
	/// </summary>
	public static (int attacks, int skills, int powers, int other) GetTypeBreakdown(CombatSnapshot snapshot)
	{
		if (snapshot == null) return (0, 0, 0, 0);

		var all = snapshot.DrawPile.Concat(snapshot.DiscardPile)
			.Concat(snapshot.Hand).Concat(snapshot.ExhaustPile);

		int atk = 0, skl = 0, pwr = 0, oth = 0;
		foreach (var c in all)
		{
			switch (c.Type.ToLowerInvariant())
			{
				case "attack": atk++; break;
				case "skill": skl++; break;
				case "power": pwr++; break;
				default: oth++; break;
			}
		}
		return (atk, skl, pwr, oth);
	}

	private static List<CombatCardInfo> ReadPile(CardPile pile)
	{
		var result = new List<CombatCardInfo>();
		foreach (var card in pile.Cards)
		{
			result.Add(CardModelToCombatInfo(card));
		}
		return result;
	}

	private static CombatCardInfo CardModelToCombatInfo(CardModel card)
	{
		var info = new CombatCardInfo();
		try
		{
			info.Name = card.Title ?? "?";

			// Id
			try { info.Id = card.Id?.Entry ?? info.Name; }
			catch { info.Id = info.Name; }

			// Type
			try { info.Type = card.Type.ToString(); }
			catch { info.Type = "Unknown"; }

			// Cost
			try
			{
				if (card.EnergyCost != null)
				{
					info.CostsX = card.EnergyCost.CostsX;
					if (!info.CostsX)
						info.Cost = card.EnergyCost.GetWithModifiers(CostModifiers.All);
				}
			}
			catch { info.Cost = -1; }

			// Upgraded
			try { info.IsUpgraded = card.IsUpgraded; }
			catch { }

			// Enchantment
			try { info.Enchantment = card.Enchantment?.GetType().Name ?? ""; }
			catch { }

			// Rarity
			try { info.Rarity = card.Rarity.ToString(); }
			catch { }
		}
		catch { }
		return info;
	}
}
