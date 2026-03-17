using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;

namespace QuestceSpire.UI;

public partial class OverlayManager
{
	/// <summary>
	/// Inject grade badges directly onto the game's card reward screen nodes.
	/// Walks the scene tree from the screen node to find card-holder children.
	/// </summary>
	public void InjectCardGrades(Node screenNode, List<ScoredCard> scoredCards, bool force = false)
	{
		if (!_showInGameBadges || screenNode == null || !GodotObject.IsInstanceValid(screenNode) || scoredCards == null || scoredCards.Count == 0)
			return;
		// Only inject when GamePatches confirmed this is a genuine card reward (not reused screen)
		// force=true bypasses this check (used by toggle reinject where we know the context is valid)
		if (!force && !GamePatches.IsGenuineCardReward)
		{
			Plugin.Log("InjectCardGrades skipped — not a genuine card reward");
			return;
		}
		if (_currentScreen != "CARD REWARD" || IsInsideMerchant(screenNode))
			return;
		// Card rewards have 3-4 cards; draw/discard pile viewers have many more
		if (scoredCards.Count > 5)
			return;
		try
		{
			// Clean up ALL previous badges (they live on our layer, so this is clean)
			ClearInGameBadges();
			// Capture epoch so deferred call can detect stale invocations
			int epoch = _badgeEpoch;
			LogNodeTree(screenNode, "CardReward", 0, 5);
			Callable.From(() => InjectCardGradesDeferred(screenNode, scoredCards, epoch)).CallDeferred();
		}
		catch (Exception ex)
		{
			Plugin.Log($"InjectCardGrades error: {ex.Message}");
		}
	}

	private void InjectCardGradesDeferred(Node screenNode, List<ScoredCard> scoredCards, int epoch)
	{
		if (screenNode == null || !GodotObject.IsInstanceValid(screenNode))
			return;
		// Stale deferred call — screen changed since injection was queued
		if (epoch != _badgeEpoch)
			return;
		if (!_showInGameBadges || _currentScreen != "CARD REWARD" || !GamePatches.IsGenuineCardReward)
			return;
		// No need to scan game tree for overlay screens — badges are on our layer
		// and will be cleaned up by ClearInGameBadges when screen changes.
		try
		{
			// Strategy: Find all Control children that look like card holders
			// Card reward screens typically have a container with N children (one per card)
			// We look for containers whose child count matches our scored card count
			var cardHolders = FindCardHolderNodes(screenNode, scoredCards.Count);
			if (cardHolders == null || cardHolders.Count == 0)
			{
				Plugin.Log($"Could not find card holder nodes in {screenNode.GetType().Name} (expected {scoredCards.Count} cards)");
				return;
			}
			Plugin.Log($"Found {cardHolders.Count} card holders — injecting grade badges");
			// Store context for later validation
			_badgeScreenNode = new WeakReference<Node>(screenNode);
			_badgeExpectedHolderCount = cardHolders.Count;
			// Match holders to scored cards by index (same order as ShowScreen receives them)
			for (int i = 0; i < Math.Min(cardHolders.Count, scoredCards.Count); i++)
			{
				AttachGradeBadge(cardHolders[i], scoredCards[i].FinalGrade, scoredCards[i].IsBestPick, scoredCards[i].FinalScore);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"InjectCardGradesDeferred error: {ex.Message}");
		}
	}

	/// <summary>
	/// Inject grade badges onto shop screen nodes.
	/// Shop has multiple item groups, so we use a broader search.
	/// </summary>
	public void InjectShopGrades(Node shopNode, List<ScoredCard> scoredCards, List<ScoredRelic> scoredRelics)
	{
		// Shop badge injection disabled — positional matching is unreliable
		// (hits potions, card removal, nav buttons). Overlay panel shows grades instead.
	}

	private void InjectShopGradesDeferred(Node shopNode, List<(TierGrade grade, bool isBest)> allGrades)
	{
		if (shopNode == null || !GodotObject.IsInstanceValid(shopNode))
			return;
		try
		{
			// Shop items may be in multiple containers. Find all sizeable Control leaves.
			var shopItems = FindAllSizeableControls(shopNode, minW: 60, minH: 60, maxDepth: 8);
			// Log found controls for debugging
			foreach (var item in shopItems)
				Plugin.Log($"  Shop item: {item.Name} ({item.GetType().Name}) size={item.Size} children={item.GetChildCount()}");
			Plugin.Log($"Shop: found {shopItems.Count} sizeable controls, have {allGrades.Count} grades");
			// Only badge up to the number of grades we have
			int matched = Math.Min(shopItems.Count, allGrades.Count);
			for (int i = 0; i < matched; i++)
			{
				AttachGradeBadge(shopItems[i], allGrades[i].grade, allGrades[i].isBest);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log($"InjectShopGradesDeferred error: {ex.Message}");
		}
	}

	/// <summary>
	/// Find all sizeable, visible, leaf-like Control nodes (no sizeable Control children themselves).
	/// Used for shop screens where items may be in multiple groups.
	/// </summary>
	private List<Control> FindAllSizeableControls(Node root, float minW, float minH, int maxDepth)
	{
		var result = new List<Control>();
		var stack = new Stack<(Node node, int depth)>();
		stack.Push((root, 0));
		while (stack.Count > 0)
		{
			var (current, depth) = stack.Pop();
			if (depth > maxDepth) continue;
			if (current is Control ctrl && ctrl.Visible && ctrl.Size.X >= minW && ctrl.Size.Y >= minH)
			{
				// Check if this is a "leaf" (no large Control children) — likely an item
				bool hasLargeChild = false;
				foreach (Node child in ctrl.GetChildren())
				{
					if (child is Control cc && cc.Visible && cc.Size.X >= minW && cc.Size.Y >= minH)
					{
						hasLargeChild = true;
						break;
					}
				}
				if (!hasLargeChild && depth >= 2)
				{
					// Skip navigation buttons — check node name AND walk up ancestors
					if (IsButtonNode(ctrl))
						continue;
					result.Add(ctrl);
					continue; // Don't recurse further into this item
				}
			}
			foreach (Node child in current.GetChildren())
			{
				stack.Push((child, depth + 1));
			}
		}
		return result;
	}

	/// <summary>
	/// Check if a node is a button or part of a button (back, close, nav, etc.)
	/// Checks the node itself, its name, its type, and up to 3 ancestors.
	/// </summary>
	private static bool IsButtonNode(Control ctrl)
	{
		// Check the node and its ancestors (up to 3 levels)
		Node current = ctrl;
		for (int i = 0; i < 4 && current != null; i++)
		{
			if (current is Godot.BaseButton)
				return true;
			string name = current.Name.ToString().ToLowerInvariant();
			string typeName = current.GetType().Name.ToLowerInvariant();
			if (name.Contains("back") || name.Contains("close") || name.Contains("exit") ||
				name.Contains("return") || name.Contains("button") || name.Contains("btn") ||
				name.Contains("nav") || name.Contains("cancel") ||
				typeName.Contains("button"))
				return true;
			current = current.GetParent();
		}
		return false;
	}

	/// <summary>
	/// Finds Control nodes that are likely card/relic holders.
	/// Searches for a container whose direct Control children count matches expectedCount.
	/// </summary>
	private List<Control> FindCardHolderNodes(Node root, int expectedCount)
	{
		// Strategy 1: Find NGridCardHolder nodes by type name (card reward screen)
		// Tree structure: CardRow > NGridCardHolder > NCardHolderHitbox (300x422)
		var gridHolders = new List<Control>();
		FindNodesByTypeName(root, "NGridCardHolder", gridHolders, 8);
		if (gridHolders.Count == expectedCount)
		{
			// Use the hitbox child of each grid holder (it has the actual size)
			var hitboxes = new List<Control>();
			foreach (var holder in gridHolders)
			{
				foreach (Node child in holder.GetChildren())
				{
					if (child is Control ctrl && ctrl.GetType().Name.Contains("Hitbox"))
					{
						hitboxes.Add(ctrl);
						break;
					}
				}
				if (hitboxes.Count < gridHolders.IndexOf(holder) + 1)
					hitboxes.Add(holder); // fallback to holder itself
			}
			Plugin.Log($"Found {hitboxes.Count} card holders via NGridCardHolder hitboxes");
			return hitboxes;
		}

		// Strategy 2: Find by container child count (relic reward, etc.)
		var queue = new Queue<Node>();
		queue.Enqueue(root);
		List<Control> bestMatch = null;

		while (queue.Count > 0)
		{
			Node current = queue.Dequeue();
			if (current is Control container)
			{
				var controlChildren = new List<Control>();
				foreach (Node child in container.GetChildren())
				{
					if (child is Control ctrl && ctrl.Visible && ctrl.Size.X >= 50 && ctrl.Size.Y >= 50)
						controlChildren.Add(ctrl);
				}
				if (controlChildren.Count == expectedCount && expectedCount >= 2)
				{
					bool allSizeable = controlChildren.All(c => c.Size.X >= 80 && c.Size.Y >= 80);
					if (allSizeable)
					{
						Plugin.Log($"Found holder container: {current.GetType().Name} with {controlChildren.Count} children");
						bestMatch = controlChildren;
						break;
					}
				}
			}
			if (GetDepth(current, root) < 8)
			{
				foreach (Node child in current.GetChildren())
					queue.Enqueue(child);
			}
		}
		return bestMatch;
	}

	private static void FindNodesByTypeName(Node root, string typeName, List<Control> results, int maxDepth)
	{
		if (root == null || maxDepth <= 0) return;
		if (root is Control ctrl && root.GetType().Name == typeName)
			results.Add(ctrl);
		foreach (Node child in root.GetChildren())
			FindNodesByTypeName(child, typeName, results, maxDepth - 1);
	}

	private static int GetDepth(Node node, Node root)
	{
		int depth = 0;
		Node current = node;
		while (current != null && current != root && depth < 20)
		{
			current = current.GetParent();
			depth++;
		}
		return depth;
	}

	/// <summary>
	/// Creates a floating grade badge as a child of the target game node.
	/// Badges are tracked in _inGameBadges for reliable cleanup (no node groups needed).
	/// </summary>
	private void AttachGradeBadge(Control targetNode, TierGrade grade, bool isBestPick, float score = -1f)
	{
		if (targetNode == null || !GodotObject.IsInstanceValid(targetNode))
			return;

		string subGrade = score >= 0f ? TierEngine.ScoreToSubGrade(score) : grade.ToString();
		Color badgeColor = TierBadge.GetGodotColor(grade);
		Color textColor = TierBadge.GetTextColor(grade);

		// Create badge panel — matches overlay CreateBadge style
		PanelContainer badge = new PanelContainer();
		badge.CustomMinimumSize = new Vector2(subGrade.Length > 1 ? 38f : 30f, 30f);

		StyleBoxFlat badgeStyle = new StyleBoxFlat();
		badgeStyle.BgColor = badgeColor;
		badgeStyle.CornerRadiusTopLeft = 0;
		badgeStyle.CornerRadiusTopRight = 10;
		badgeStyle.CornerRadiusBottomLeft = 10;
		badgeStyle.CornerRadiusBottomRight = 0;
		badgeStyle.BorderWidthTop = isBestPick ? 2 : 1;
		badgeStyle.BorderWidthBottom = isBestPick ? 2 : 1;
		badgeStyle.BorderWidthLeft = isBestPick ? 2 : 1;
		badgeStyle.BorderWidthRight = isBestPick ? 2 : 1;
		badgeStyle.BorderColor = isBestPick ? ClrAccent : badgeColor.Darkened(0.3f);
		badgeStyle.ShadowSize = isBestPick ? 12 : 4;
		badgeStyle.ShadowColor = isBestPick ? new Color(ClrAccent, 0.7f) : new Color(0f, 0f, 0f, 0.6f);
		badge.AddThemeStyleboxOverride("panel", badgeStyle);

		Label gradeLbl = new Label();
		gradeLbl.Text = subGrade;
		ApplyFont(gradeLbl, _fontHeader);
		gradeLbl.AddThemeColorOverride("font_color", textColor);
		gradeLbl.AddThemeFontSizeOverride("font_size", subGrade.Length > 1 ? 17 : 20);
		gradeLbl.HorizontalAlignment = HorizontalAlignment.Center;
		gradeLbl.VerticalAlignment = VerticalAlignment.Center;
		badge.AddChild(gradeLbl, forceReadableName: false, Node.InternalMode.Disabled);

		badge.ZIndex = 10; // Above sibling game UI
		badge.MouseFilter = Control.MouseFilterEnum.Ignore;

		// Add as child of target node (inherits z-order, goes behind popups)
		targetNode.AddChild(badge, forceReadableName: false, Node.InternalMode.Disabled);
		_inGameBadges.Add((badge, new WeakReference<Control>(targetNode)));
		// Position after adding (deferred so size is known)
		Callable.From(() => PositionBadgeInParent(badge, targetNode)).CallDeferred();
	}

	/// <summary>
	/// Position a badge within its parent node (bottom-center, local coordinates).
	/// Badge is a child of the target, so we use parent size, not global coords.
	/// </summary>
	private static void PositionBadgeInParent(PanelContainer badge, Control parent)
	{
		if (badge == null || !GodotObject.IsInstanceValid(badge) ||
		    parent == null || !GodotObject.IsInstanceValid(parent))
			return;
		float parentW = parent.Size.X;
		float parentH = parent.Size.Y;
		float badgeW = badge.GetCombinedMinimumSize().X;
		float badgeH = badge.GetCombinedMinimumSize().Y;
		badge.Position = new Vector2((parentW - badgeW) / 2f, parentH - badgeH - 4f);
	}

	public void CleanupAllBadges()
	{
		ClearInGameBadges();
	}

	/// <summary>
	/// Remove all in-game badges (children of game card nodes).
	/// Tracked list makes cleanup simple — no game tree scanning needed.
	/// </summary>
	private void ClearInGameBadges()
	{
		foreach (var (badge, _) in _inGameBadges)
		{
			try
			{
				if (badge != null && GodotObject.IsInstanceValid(badge))
				{
					SafeDisconnectSignals(badge);
					badge.GetParent()?.RemoveChild(badge);
					badge.QueueFree();
				}
			}
			catch (Exception ex) { Plugin.Log($"ClearInGameBadges error: {ex.Message}"); }
		}
		_inGameBadges.Clear();
		_badgeScreenNode = null;
		_badgeExpectedHolderCount = 0;
	}

	/// <summary>
	/// Update badge positions to track their target game nodes.
	/// Also removes badges whose targets are no longer valid/visible,
	/// or if the screen context has changed (pile viewer opened, etc.).
	/// Called from CheckForStaleScreen on each tick.
	/// </summary>
	private void UpdateInGameBadgePositions()
	{
		if (_inGameBadges.Count == 0) return;

		// Context check: if the card reward screen node is gone/hidden, clear all badges
		if (_badgeScreenNode != null)
		{
			if (!_badgeScreenNode.TryGetTarget(out var screenNode) ||
			    !GodotObject.IsInstanceValid(screenNode) ||
			    (screenNode is Control screenCtrl && !screenCtrl.IsVisibleInTree()))
			{
				ClearInGameBadges();
				return;
			}
			// Context check: if more NGridCardHolder nodes appeared, a pile/overlay opened
			var allHolders = new List<Control>();
			Node searchRoot = (Node)screenNode.GetTree()?.Root;
			if (searchRoot == null)
			{
				Plugin.Log("OverlayManager: GetTree().Root is null, falling back to screenNode");
				searchRoot = screenNode;
			}
			FindNodesByTypeName(searchRoot, "NGridCardHolder", allHolders, 8);
			if (allHolders.Count > _badgeExpectedHolderCount + 1)
			{
				Plugin.Log($"Badge context changed: {allHolders.Count} NGridCardHolder nodes vs expected {_badgeExpectedHolderCount} — clearing badges");
				ClearInGameBadges();
				return;
			}
		}

		bool anyInvalid = false;
		foreach (var (badge, targetRef) in _inGameBadges)
		{
			if (badge == null || !GodotObject.IsInstanceValid(badge))
			{
				anyInvalid = true;
				continue;
			}
			if (!targetRef.TryGetTarget(out var target) ||
			    !GodotObject.IsInstanceValid(target) ||
			    !target.IsVisibleInTree())
			{
				// Target gone or hidden — remove this badge
				badge.Visible = false;
				anyInvalid = true;
				continue;
			}
			// Update position within parent (local coords)
			PositionBadgeInParent(badge, target);
		}
		// Clean up invalid entries
		if (anyInvalid)
		{
			for (int i = _inGameBadges.Count - 1; i >= 0; i--)
			{
				var (badge, targetRef) = _inGameBadges[i];
				bool badgeGone = badge == null || !GodotObject.IsInstanceValid(badge);
				bool targetGone = !targetRef.TryGetTarget(out var t) || !GodotObject.IsInstanceValid(t) || !t.IsVisibleInTree();
				if (badgeGone || targetGone)
				{
					if (!badgeGone)
					{
						badge.GetParent()?.RemoveChild(badge);
						badge.QueueFree();
					}
					_inGameBadges.RemoveAt(i);
				}
			}
		}
	}

	private static void LogNodeTree(Node node, string label, int depth, int maxDepth)
	{
		if (node == null || !GodotObject.IsInstanceValid(node) || depth > maxDepth)
			return;
		string indent = new string(' ', depth * 2);
		string sizeInfo = node is Control ctrl ? $" [{ctrl.Size.X:F0}x{ctrl.Size.Y:F0}]" : "";
		string visInfo = node is Control ctrl2 ? (ctrl2.Visible ? "" : " (hidden)") : "";
		Plugin.Log($"  {indent}{label}> {node.GetType().Name} \"{node.Name}\"{sizeInfo}{visInfo}");
		int i = 0;
		foreach (Node child in node.GetChildren())
		{
			LogNodeTree(child, $"[{i}]", depth + 1, maxDepth);
			i++;
		}
	}
}
