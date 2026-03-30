using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI;

/// <summary>
/// Manages in-game grade badges injected directly onto card reward screen nodes.
/// Standalone replacement for OverlayManager.Badges.cs.
/// </summary>
public class BadgeManager
{
	private readonly List<(PanelContainer badge, WeakReference<Control> target)> _inGameBadges = new();
	private WeakReference<Node> _badgeScreenNode;
	private int _badgeExpectedHolderCount;
	private int _badgeEpoch;
	private bool _showBadges;

	private SharedResources Res => SharedResources.Instance;

	public BadgeManager(bool showBadges)
	{
		_showBadges = showBadges;
	}

	public void SetShowBadges(bool show)
	{
		_showBadges = show;
		if (!show) CleanupAllBadges();
	}

	public void InjectCardGrades(Node screenNode, List<ScoredCard> scoredCards, bool force = false)
	{
		if (!_showBadges || screenNode == null || !GodotObject.IsInstanceValid(screenNode) || scoredCards == null || scoredCards.Count == 0)
			return;
		if (!force && !GamePatches.IsGenuineCardReward)
		{
			Plugin.Log("InjectCardGrades skipped — not a genuine card reward");
			return;
		}
		if (IsInsideMerchant(screenNode))
			return;
		if (scoredCards.Count > 5)
			return;
		try
		{
			CleanupAllBadges();
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
		if (epoch != _badgeEpoch)
			return;
		if (!_showBadges || !GamePatches.IsGenuineCardReward)
			return;
		try
		{
			var cardHolders = FindCardHolderNodes(screenNode, scoredCards.Count);
			if (cardHolders == null || cardHolders.Count == 0)
			{
				Plugin.Log($"Could not find card holder nodes in {screenNode.GetType().Name} (expected {scoredCards.Count} cards)");
				return;
			}
			Plugin.Log($"Found {cardHolders.Count} card holders — injecting grade badges");
			_badgeScreenNode = new WeakReference<Node>(screenNode);
			_badgeExpectedHolderCount = cardHolders.Count;
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

	public void CleanupAllBadges()
	{
		_badgeEpoch++;
		foreach (var (badge, _) in _inGameBadges)
		{
			try
			{
				if (badge != null && GodotObject.IsInstanceValid(badge))
				{
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

	public void UpdateBadgePositions()
	{
		if (_inGameBadges.Count == 0) return;

		if (_badgeScreenNode != null)
		{
			if (!_badgeScreenNode.TryGetTarget(out var screenNode) ||
			    !GodotObject.IsInstanceValid(screenNode) ||
			    (screenNode is Control screenCtrl && !screenCtrl.IsVisibleInTree()))
			{
				CleanupAllBadges();
				return;
			}
			var allHolders = new List<Control>();
			Node searchRoot = (Node)screenNode.GetTree()?.Root ?? screenNode;
			FindNodesByTypeName(searchRoot, "NGridCardHolder", allHolders, 8);
			if (allHolders.Count > _badgeExpectedHolderCount + 1)
			{
				Plugin.Log($"Badge context changed: {allHolders.Count} NGridCardHolder nodes vs expected {_badgeExpectedHolderCount} — clearing badges");
				CleanupAllBadges();
				return;
			}
		}

		bool anyInvalid = false;
		foreach (var (badge, targetRef) in _inGameBadges)
		{
			if (badge == null || !GodotObject.IsInstanceValid(badge)) { anyInvalid = true; continue; }
			if (!targetRef.TryGetTarget(out var target) || !GodotObject.IsInstanceValid(target) || !target.IsVisibleInTree())
			{
				badge.Visible = false;
				anyInvalid = true;
				continue;
			}
			PositionBadgeInParent(badge, target);
		}
		if (anyInvalid)
		{
			for (int i = _inGameBadges.Count - 1; i >= 0; i--)
			{
				var (badge, targetRef) = _inGameBadges[i];
				bool badgeGone = badge == null || !GodotObject.IsInstanceValid(badge);
				bool targetGone = !targetRef.TryGetTarget(out var t) || !GodotObject.IsInstanceValid(t) || !t.IsVisibleInTree();
				if (badgeGone || targetGone)
				{
					if (!badgeGone) { badge.GetParent()?.RemoveChild(badge); badge.QueueFree(); }
					_inGameBadges.RemoveAt(i);
				}
			}
		}
	}

	private void AttachGradeBadge(Control targetNode, TierGrade grade, bool isBestPick, float score = -1f)
	{
		if (targetNode == null || !GodotObject.IsInstanceValid(targetNode))
			return;

		string subGrade = score >= 0f ? TierEngine.ScoreToSubGrade(score) : grade.ToString();
		Color badgeColor = TierBadge.GetGodotColor(grade);
		Color textColor = TierBadge.GetTextColor(grade);

		PanelContainer badge = new PanelContainer();
		badge.CustomMinimumSize = new Vector2(subGrade.Length > 1 ? 38f : 30f, 30f);

		StyleBoxFlat badgeStyle = new StyleBoxFlat();
		badgeStyle.BgColor = badgeColor;
		badgeStyle.CornerRadiusTopLeft = 0;
		badgeStyle.CornerRadiusTopRight = 10;
		badgeStyle.CornerRadiusBottomLeft = 10;
		badgeStyle.CornerRadiusBottomRight = 0;
		badgeStyle.BorderWidthTop = badgeStyle.BorderWidthBottom = badgeStyle.BorderWidthLeft = badgeStyle.BorderWidthRight = isBestPick ? 2 : 1;
		badgeStyle.BorderColor = isBestPick ? SharedResources.ClrAccent : badgeColor.Darkened(0.3f);
		badgeStyle.ShadowSize = isBestPick ? 12 : 4;
		badgeStyle.ShadowColor = isBestPick ? new Color(SharedResources.ClrAccent, 0.7f) : OverlayTheme.Shadow;
		badge.AddThemeStyleboxOverride("panel", badgeStyle);

		Label gradeLbl = new Label();
		gradeLbl.Text = subGrade;
		Res.ApplyFont(gradeLbl, Res.FontHeader);
		gradeLbl.AddThemeColorOverride("font_color", textColor);
		gradeLbl.AddThemeFontSizeOverride("font_size", subGrade.Length > 1 ? OverlayTheme.FontBadgeSmall : OverlayTheme.FontBadgeLarge);
		gradeLbl.HorizontalAlignment = HorizontalAlignment.Center;
		gradeLbl.VerticalAlignment = VerticalAlignment.Center;
		badge.AddChild(gradeLbl, forceReadableName: false, Node.InternalMode.Disabled);

		badge.ZIndex = 10;
		badge.MouseFilter = Control.MouseFilterEnum.Ignore;

		targetNode.AddChild(badge, forceReadableName: false, Node.InternalMode.Disabled);
		_inGameBadges.Add((badge, new WeakReference<Control>(targetNode)));
		Callable.From(() => PositionBadgeInParent(badge, targetNode)).CallDeferred();
	}

	private static void PositionBadgeInParent(PanelContainer badge, Control parent)
	{
		if (badge == null || !GodotObject.IsInstanceValid(badge) || parent == null || !GodotObject.IsInstanceValid(parent))
			return;
		float parentW = parent.Size.X;
		float parentH = parent.Size.Y;
		float badgeW = badge.GetCombinedMinimumSize().X;
		float badgeH = badge.GetCombinedMinimumSize().Y;
		badge.Position = new Vector2((parentW - badgeW) / 2f, parentH - badgeH - 4f);
	}

	private List<Control> FindCardHolderNodes(Node root, int expectedCount)
	{
		var gridHolders = new List<Control>();
		FindNodesByTypeName(root, "NGridCardHolder", gridHolders, 8);
		if (gridHolders.Count == expectedCount)
		{
			var hitboxes = new List<Control>();
			foreach (var holder in gridHolders)
			{
				bool found = false;
				foreach (Node child in holder.GetChildren())
				{
					if (child is Control ctrl && ctrl.GetType().Name.Contains("Hitbox"))
					{
						hitboxes.Add(ctrl);
						found = true;
						break;
					}
				}
				if (!found) hitboxes.Add(holder);
			}
			Plugin.Log($"Found {hitboxes.Count} card holders via NGridCardHolder hitboxes");
			return hitboxes;
		}

		var queue = new Queue<Node>();
		queue.Enqueue(root);

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
						return controlChildren;
					}
				}
			}
			if (GetDepth(current, root) < 8)
			{
				foreach (Node child in current.GetChildren())
					queue.Enqueue(child);
			}
		}
		return null;
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

	private static void LogNodeTree(Node node, string label, int depth, int maxDepth)
	{
		if (node == null || !GodotObject.IsInstanceValid(node) || depth > maxDepth) return;
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
