using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

public partial class OverlayManager
{
	private CanvasLayer _layer;

	private PanelContainer _panel;

	private VBoxContainer _content;

	private Label _compactToggle;

	// _archetypeLabel removed — deck info lives in DECK BREAKDOWN section

	private Label _screenLabel;

	// Settings menu
	private PanelContainer _settingsMenu;
	private Button _gearButton;



	private bool _visible = true;

	private bool _showTooltips = true;

	private bool _showInGameBadges = true;

	private List<ScoredCard> _currentCards;

	private List<ScoredRelic> _currentRelics;

	private DeckAnalysis _currentDeckAnalysis;

	private string _currentScreen = "IDLE";
	public string CurrentScreen => _currentScreen;
	private int _badgeEpoch = 0; // Incremented on every screen change; stale deferred calls check this

	// In-game badges: rendered on our own CanvasLayer to avoid polluting the game's scene tree.
	// Each badge tracks the game node it should overlay so we can update position.
	private readonly List<(PanelContainer badge, WeakReference<Control> target)> _inGameBadges = new();
	// The screen node that was active when badges were injected — used to detect screen changes
	private WeakReference<Node> _badgeScreenNode;
	// The expected NGridCardHolder count when badges were injected
	private int _badgeExpectedHolderCount;

	private List<(string icon, string text, Color color)> _mapAdvice;

	private string _currentCharacter;

	// Drag state
	private bool _dragging;
	private Vector2 _dragOffset;

	// Feature 3: Opacity control
	private float _panelOpacity = 1.0f;
	private static readonly float[] OpacitySteps = { 1.0f, 0.75f, 0.50f };
	private int _opacityIndex;

	// Feature 4: Collapsible mode
	private bool _collapsed;
	// _archChipPanel removed — deck info lives in DECK BREAKDOWN section
	private VBoxContainer _deckVizContainer;

	// Feature 1: Decision history
	private bool _showHistory;

	// Section toggles (persisted)
	private bool _showDeckBreakdown = true;
	// _showDrawProb removed — feature wasn't useful

	// Staleness tracking: clear overlay when game screen changes without a patch firing
	private ulong _lastUpdateTick;

	// Feature 5: Draw probability
	private GameState _currentGameState;

	// Feature 6: Relic tenure
	private int _currentFloor;

	// Stored context for advice re-generation on toggle
	private string _currentEventId;
	private List<string> _currentEnemyIds;

	// Debug overlay (F10)
	private bool _showDebug;

	// V1: Color-coded title separator
	private HSeparator _titleSep;

	// V2: Animated transitions
	private string _previousScreen = "";

	// V4: Card art hover preview
	private PanelContainer _hoverPreview;
	private TextureRect _hoverPreviewTex;

	// A1: Win rate tracker
	private Label _winRateLabel;

	// _minimizeBtn removed — collapse to title bar instead

	// Layout stabilization counter (re-runs FitPanelHeight for first N process ticks)
	private int _layoutTicksRemaining;

	// Shop refresh: track item count and IDs to detect purchases
	private int _shopItemCount;
	private HashSet<string> _shopCardIds = new HashSet<string>();
	private HashSet<string> _shopRelicIds = new HashSet<string>();

	// _archChipVBox removed — deck info lives in DECK BREAKDOWN section

	// STS2 color palette (matched from game scenes/DLL)
	private static readonly Color ClrBg = new Color(0.034f, 0.057f, 0.11f, 0.97f);

	private static readonly Color ClrBorder = new Color(0.624f, 0.490f, 0.322f);

	private static readonly Color ClrHeader = new Color(0.92f, 0.78f, 0.35f);

	private static readonly Color ClrAccent = new Color(0.831f, 0.714f, 0.357f);

	private static readonly Color ClrSub = new Color(0.580f, 0.545f, 0.404f);

	private static readonly Color ClrPositive = new Color(0.3f, 0.8f, 0.4f);

	private static readonly Color ClrNegative = new Color(0.9f, 0.35f, 0.3f);

	private static readonly Color ClrNotes = new Color(0.72f, 0.68f, 0.6f);

	private static readonly Color ClrSkip = new Color(0.557f, 0.212f, 0.882f);

	private static readonly Color ClrExpensive = new Color(1f, 0.6f, 0.3f);

	private static readonly Color ClrHover = new Color(0.1f, 0.12f, 0.2f, 0.8f);

	private static readonly Color ClrSkipSub = new Color(0.6f, 0.6f, 0.8f);

	private static readonly Color ClrAqua = new Color(0.529f, 0.808f, 0.922f);

	private static readonly Color ClrOutline = new Color(0.02f, 0.02f, 0.04f);

	private static readonly Color ClrCream = new Color(0.92f, 0.88f, 0.78f);

	// Game fonts (loaded from res://fonts/)
	private Font _fontBody;
	private Font _fontBold;
	private Font _fontHeader;

	// Game art
	private Texture2D _goldIcon;
	private readonly Dictionary<string, Texture2D> _cardPortraitCache = new Dictionary<string, Texture2D>();
	private readonly Dictionary<string, Texture2D> _relicIconCache = new Dictionary<string, Texture2D>();

	private StyleBoxFlat _sbPanel;

	private StyleBoxFlat _sbEntry;

	private StyleBoxFlat _sbBest;

	private StyleBoxFlat _sbChip;

	private OverlaySettings _settings;
	public OverlaySettings Settings => _settings;

	// Combat pile tracking
	private CombatTracker.CombatSnapshot _lastCombatSnapshot;
	private int _lastCombatHash;
	private bool _showCombatPiles = true;

	// Hover signal tracking — centralized connect/disconnect to prevent signal memory leaks
	private readonly List<Control> _connectedHoverNodes = new();

	private StyleBoxFlat _sbHover;

	private StyleBoxFlat _sbHoverBest;

	private StyleBoxFlat _sbSTier;

	private StyleBoxFlat _sbSTierHover;

	public OverlayManager()
	{
		_settings = OverlaySettings.Load();
		_visible = true; // Always start visible — hide is session-only
		_showTooltips = _settings.ShowTooltips;
		_showInGameBadges = _settings.ShowInGameBadges;
		_panelOpacity = _settings.PanelOpacity;
		_opacityIndex = Array.IndexOf(OpacitySteps, _panelOpacity);
		if (_opacityIndex < 0) _opacityIndex = 0;
		_collapsed = _settings.Collapsed;
		_showDeckBreakdown = _settings.ShowDeckBreakdown;
		_showHistory = false; // Always start off — user can enable in settings
		LoadGameFonts();
		LoadGameIcons();
		InitializeStyles();
		ApplyOpacity(_panelOpacity);
		BuildOverlay();
	}

	private void LoadGameFonts()
	{
		try
		{
			_fontBody = ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
			_fontBold = ResourceLoader.Load<Font>("res://fonts/kreon_bold.ttf");
			_fontHeader = ResourceLoader.Load<Font>("res://fonts/spectral_bold.ttf");
			Plugin.Log($"Game fonts loaded: body={_fontBody != null} bold={_fontBold != null} header={_fontHeader != null}");
		}
		catch (System.Exception ex)
		{
			Plugin.Log("Could not load game fonts, using defaults: " + ex.Message);
		}
	}

	private void LoadGameIcons()
	{
		try
		{
			_goldIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/gold_icon.png");
			Plugin.Log($"Game icons loaded: gold={_goldIcon != null}");
		}
		catch (System.Exception ex)
		{
			Plugin.Log("Could not load game icons: " + ex.Message);
		}
	}

	private static readonly string[] PortraitFallbackFolders = new[]
	{
		"colorless", "neutral", "shared", "common", ""
	};

	private Texture2D GetCardPortrait(string cardId, string character)
	{
		string key = $"{character}/{cardId}";
		if (_cardPortraitCache.TryGetValue(key, out var cached)) return cached;
		try
		{
			string fileName = cardId.Replace(" ", "_").Replace("'", "").Replace("-", "_").ToLowerInvariant();
			string charFolder = character?.ToLowerInvariant() ?? "ironclad";
			// Primary: character folder under packed
			string[] basePaths = new[]
			{
				"res://images/packed/card_portraits",
				"res://images/card_portraits"
			};
			Texture2D tex = null;
			foreach (string basePath in basePaths)
			{
				// Try character folder first
				tex = ResourceLoader.Load<Texture2D>($"{basePath}/{charFolder}/{fileName}.png");
				if (tex != null) break;
				// Try fallback folders
				foreach (string fallback in PortraitFallbackFolders)
				{
					string path = string.IsNullOrEmpty(fallback)
						? $"{basePath}/{fileName}.png"
						: $"{basePath}/{fallback}/{fileName}.png";
					tex = ResourceLoader.Load<Texture2D>(path);
					if (tex != null) break;
				}
				if (tex != null) break;
			}
			if (tex == null)
				Plugin.Log($"No portrait found for card '{cardId}' (file: {fileName}, char: {charFolder})");
			_cardPortraitCache[key] = tex;
			return tex;
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetCardPortrait error for '{cardId}': {ex.Message}");
			_cardPortraitCache[key] = null;
			return null;
		}
	}

	private Texture2D GetRelicIcon(string relicId)
	{
		if (_relicIconCache.TryGetValue(relicId, out var cached)) return cached;
		try
		{
			string fileName = relicId.Replace(" ", "_").Replace("'", "").Replace("-", "_").ToLowerInvariant();
			string path = $"res://images/relics/{fileName}.png";
			var tex = ResourceLoader.Load<Texture2D>(path);
			_relicIconCache[relicId] = tex;
			return tex;
		}
		catch (Exception ex)
		{
			Plugin.Log($"GetRelicIcon error for '{relicId}': {ex.Message}");
			_relicIconCache[relicId] = null;
			return null;
		}
	}

	private void ApplyFont(Label label, Font font)
	{
		if (font != null)
			label.AddThemeFontOverride("font", font);
	}

	private bool IsOverlayValid()
	{
		if (_layer != null && GodotObject.IsInstanceValid(_layer) && _panel != null && GodotObject.IsInstanceValid(_panel) && _content != null)
		{
			return GodotObject.IsInstanceValid(_content);
		}
		return false;
	}

	private static readonly string[] TrackedSignals = { "mouse_entered", "mouse_exited", "gui_input", "pressed" };

	private void SafeDisconnectSignals(Control node, bool recursive = false)
	{
		foreach (var signalName in TrackedSignals)
		{
			foreach (var conn in node.GetSignalConnectionList(signalName))
			{
				var callable = (Callable)conn["callable"];
				node.Disconnect(signalName, callable);
			}
		}
		if (recursive)
		{
			foreach (var child in node.GetChildren())
			{
				if (child is Control childCtrl && GodotObject.IsInstanceValid(childCtrl))
					SafeDisconnectSignals(childCtrl, true);
			}
		}
	}

	private void ConnectHoverSignals(Control node, Action onEnter, Action onExit)
	{
		node.Connect("mouse_entered", Callable.From(onEnter));
		node.Connect("mouse_exited", Callable.From(onExit));
		if (!_connectedHoverNodes.Contains(node))
			_connectedHoverNodes.Add(node);
	}

	private void DisconnectAllHoverSignals()
	{
		foreach (var node in _connectedHoverNodes)
		{
			if (node != null && GodotObject.IsInstanceValid(node))
			{
				SafeDisconnectSignals(node);
			}
		}
		_connectedHoverNodes.Clear();
	}

	private bool EnsureOverlay()
	{
		if (IsOverlayValid())
		{
			return true;
		}
		// Remove old nodes from scene tree before rebuilding
		try
		{
			DisconnectAllHoverSignals();
			if (_layer != null && GodotObject.IsInstanceValid(_layer))
			{
				_layer.GetParent()?.RemoveChild(_layer);
				_layer.QueueFree();
			}
			if (_hoverPreview != null && GodotObject.IsInstanceValid(_hoverPreview))
			{
				SafeDisconnectSignals(_hoverPreview);
				_hoverPreview.GetParent()?.RemoveChild(_hoverPreview);
				_hoverPreview.QueueFree();
			}
		}
		catch (Exception ex) { Plugin.Log($"EnsureOverlay cleanup error: {ex.Message}"); }
		_layer = null;
		_panel = null;
		_content = null;
		_screenLabel = null;
		_deckVizContainer = null;
		_titleSep = null;
		_winRateLabel = null;
		_hoverPreview = null;
		_hoverPreviewTex = null;
		InitializeStyles();
		BuildOverlay();
		return IsOverlayValid();
	}

	private void InitializeStyles()
	{
		_sbPanel = new StyleBoxFlat();
		_sbPanel.BgColor = ClrBg;
		_sbPanel.BorderWidthTop = 3;
		_sbPanel.BorderWidthLeft = 3;
		_sbPanel.BorderWidthRight = 1;
		_sbPanel.BorderWidthBottom = 1;
		_sbPanel.BorderColor = ClrBorder;
		_sbPanel.CornerRadiusTopLeft = 0;
		_sbPanel.CornerRadiusTopRight = 18;
		_sbPanel.CornerRadiusBottomLeft = 18;
		_sbPanel.CornerRadiusBottomRight = 0;
		StyleBoxFlat sbPanel3 = _sbPanel;
		float contentMarginLeft = (_sbPanel.ContentMarginRight = 20f);
		sbPanel3.ContentMarginLeft = contentMarginLeft;
		StyleBoxFlat sbPanel4 = _sbPanel;
		contentMarginLeft = (_sbPanel.ContentMarginBottom = 20f);
		sbPanel4.ContentMarginTop = contentMarginLeft;
		_sbPanel.ShadowSize = 12;
		_sbPanel.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
		_sbEntry = new StyleBoxFlat();
		_sbEntry.BgColor = new Color(0.06f, 0.08f, 0.14f, 0.6f);
		_sbEntry.CornerRadiusTopLeft = 8;
		_sbEntry.CornerRadiusTopRight = 8;
		_sbEntry.CornerRadiusBottomLeft = 8;
		_sbEntry.CornerRadiusBottomRight = 8;
		StyleBoxFlat sbEntry3 = _sbEntry;
		contentMarginLeft = (_sbEntry.ContentMarginRight = 14f);
		sbEntry3.ContentMarginLeft = contentMarginLeft;
		StyleBoxFlat sbEntry4 = _sbEntry;
		contentMarginLeft = (_sbEntry.ContentMarginBottom = 8f);
		sbEntry4.ContentMarginTop = contentMarginLeft;
		_sbHover = _sbEntry.Duplicate() as StyleBoxFlat;
		if (_sbHover != null)
		{
			_sbHover.BgColor = ClrHover;
			_sbHover.BorderWidthLeft = 3;
			_sbHover.BorderColor = ClrSub;
		}
		_sbBest = _sbEntry.Duplicate() as StyleBoxFlat;
		if (_sbBest != null)
		{
			_sbBest.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.1f);
			_sbBest.BorderWidthLeft = 4;
			_sbBest.BorderColor = ClrAccent;
		}
		_sbHoverBest = _sbBest?.Duplicate() as StyleBoxFlat;
		if (_sbHoverBest != null)
		{
			_sbHoverBest.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.15f);
		}
		_sbSTier = _sbBest?.Duplicate() as StyleBoxFlat;
		if (_sbSTier != null)
		{
			_sbSTier.BorderWidthLeft = 5;
			_sbSTier.ShadowSize = 10;
			_sbSTier.ShadowColor = new Color(ClrAccent, 0.6f);
		}
		_sbSTierHover = _sbSTier?.Duplicate() as StyleBoxFlat;
		if (_sbSTierHover != null)
		{
			_sbSTierHover.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.18f);
		}
		_sbChip = new StyleBoxFlat();
		_sbChip.BgColor = new Color(0.02f, 0.03f, 0.07f, 0.7f);
		StyleBoxFlat sbChip = _sbChip;
		int chipRadius = (_sbChip.CornerRadiusTopRight = 16);
		sbChip.CornerRadiusTopLeft = chipRadius;
		StyleBoxFlat sbChip2 = _sbChip;
		chipRadius = (_sbChip.CornerRadiusBottomRight = 16);
		sbChip2.CornerRadiusBottomLeft = chipRadius;
		StyleBoxFlat sbChip3 = _sbChip;
		contentMarginLeft = (_sbChip.ContentMarginRight = 12f);
		sbChip3.ContentMarginLeft = contentMarginLeft;
		StyleBoxFlat sbChip4 = _sbChip;
		contentMarginLeft = (_sbChip.ContentMarginBottom = 4f);
		sbChip4.ContentMarginTop = contentMarginLeft;
		_sbChip.BorderWidthBottom = 1;
		_sbChip.BorderWidthTop = 1;
		_sbChip.BorderColor = new Color(ClrAccent, 0.3f);
	}

	private void BuildOverlay()
	{
		SceneTree sceneTree = Engine.GetMainLoop() as SceneTree;
		if (sceneTree?.Root == null)
		{
			Plugin.Log("SceneTree not ready — overlay deferred.");
			return;
		}
		_layer = new CanvasLayer();
		_layer.Layer = 100;
		_panel = new PanelContainer();
		_panel.AnchorLeft = 1f;
		_panel.AnchorRight = 1f;
		_panel.AnchorTop = 0f;
		_panel.AnchorBottom = 0f;
		_panel.OffsetLeft = _settings.OffsetLeft;
		_panel.OffsetRight = _settings.OffsetRight;
		_panel.OffsetTop = _settings.OffsetTop;
		_panel.OffsetBottom = _settings.OffsetTop + 40;
		_panel.GrowVertical = Control.GrowDirection.End;
		_panel.AddThemeStyleboxOverride("panel", _sbPanel);
		_panel.MouseFilter = Control.MouseFilterEnum.Stop;
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);

		// Title bar area (draggable) — Pass so child buttons still receive clicks
		VBoxContainer titleBar = new VBoxContainer();
		titleBar.MouseFilter = Control.MouseFilterEnum.Pass;
		titleBar.MouseDefaultCursorShape = Control.CursorShape.Drag;
		titleBar.GuiInput += (InputEvent ev) => OnTitleBarInput(ev);
		vBoxContainer.AddChild(titleBar, forceReadableName: false, Node.InternalMode.Disabled);

		HBoxContainer titleRow = new HBoxContainer();
		titleRow.MouseFilter = Control.MouseFilterEnum.Pass;
		titleBar.AddChild(titleRow, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label();
		label.Text = "QU'EST-CE SPIRE?";
		ApplyFont(label, _fontBold);
		label.AddThemeFontSizeOverride("font_size", 28);
		label.AddThemeColorOverride("font_color", ClrHeader);
		label.AddThemeConstantOverride("outline_size", 4);
		label.AddThemeColorOverride("font_outline_color", ClrOutline);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		// (Settings button added at bottom of content in Rebuild)
		// Compact/expand toggle
		_compactToggle = new Label();
		_compactToggle.Text = "\u25B2";
		ApplyFont(_compactToggle, _fontBold);
		_compactToggle.AddThemeFontSizeOverride("font_size", 18);
		_compactToggle.AddThemeColorOverride("font_color", ClrSub);
		_compactToggle.MouseFilter = Control.MouseFilterEnum.Stop;
		_compactToggle.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_compactToggle.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				ToggleCollapsed();
		};
		titleRow.AddChild(_compactToggle, forceReadableName: false, Node.InternalMode.Disabled);

		// Decorative separator under title (V1: color-coded)
		_titleSep = new HSeparator();
		_titleSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.6f), Thickness = 2 });
		_titleSep.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_titleSep, forceReadableName: false, Node.InternalMode.Disabled);

		_screenLabel = new Label();
		_screenLabel.Text = "대기 중... (드래그로 이동)";
		ApplyFont(_screenLabel, _fontBold);
		_screenLabel.AddThemeFontSizeOverride("font_size", 14);
		_screenLabel.AddThemeColorOverride("font_color", ClrSub);
		_screenLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_screenLabel, forceReadableName: false, Node.InternalMode.Disabled);

		// A1: Win rate label
		_winRateLabel = new Label();
		_winRateLabel.Text = "";
		_winRateLabel.Visible = false;
		ApplyFont(_winRateLabel, _fontBody);
		_winRateLabel.AddThemeFontSizeOverride("font_size", 17);
		_winRateLabel.AddThemeColorOverride("font_color", ClrSub);
		_winRateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_winRateLabel, forceReadableName: false, Node.InternalMode.Disabled);

		// Archetype chip panel removed — deck info lives in DECK BREAKDOWN collapsible section
		// Deck composition visualization container (Feature 2)
		_deckVizContainer = new VBoxContainer();
		_deckVizContainer.AddThemeConstantOverride("separation", 4);
		_deckVizContainer.Visible = false; // hidden — deck info lives in DECK BREAKDOWN section
		vBoxContainer.AddChild(_deckVizContainer, forceReadableName: false, Node.InternalMode.Disabled);
		// Content container — no scroll, panel auto-expands to fit
		_content = new VBoxContainer();
		_content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_content.AddThemeConstantOverride("separation", 6);
		vBoxContainer.AddChild(_content, forceReadableName: false, Node.InternalMode.Disabled);
		_layer.AddChild(_panel, forceReadableName: false, Node.InternalMode.Disabled);
		OverlayInputHandler node = new OverlayInputHandler(this);
		_layer.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		BuildSettingsMenu();
		_panel.Visible = _visible;
		// Apply collapsed state
		if (_collapsed)
		{
			_content.Visible = false;
			_deckVizContainer.Visible = false;
		}
		// V4: Card art hover preview
		_hoverPreview = new PanelContainer();
		_hoverPreview.Visible = false;
		_hoverPreview.ZIndex = 101;
		_hoverPreview.ClipContents = true;
		_hoverPreview.MouseFilter = Control.MouseFilterEnum.Ignore;
		StyleBoxFlat hpStyle = new StyleBoxFlat();
		hpStyle.BgColor = ClrBg;
		hpStyle.BorderWidthTop = 2;
		hpStyle.BorderWidthBottom = 2;
		hpStyle.BorderWidthLeft = 2;
		hpStyle.BorderWidthRight = 2;
		hpStyle.BorderColor = ClrBorder;
		hpStyle.CornerRadiusTopLeft = 8;
		hpStyle.CornerRadiusTopRight = 8;
		hpStyle.CornerRadiusBottomLeft = 8;
		hpStyle.CornerRadiusBottomRight = 8;
		hpStyle.ShadowSize = 8;
		hpStyle.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
		hpStyle.ContentMarginTop = 4f;
		hpStyle.ContentMarginBottom = 4f;
		hpStyle.ContentMarginLeft = 4f;
		hpStyle.ContentMarginRight = 4f;
		_hoverPreview.AddThemeStyleboxOverride("panel", hpStyle);
		_hoverPreviewTex = new TextureRect();
		_hoverPreviewTex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_hoverPreviewTex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		_hoverPreviewTex.CustomMinimumSize = new Vector2(200f, 200f);
		_hoverPreview.AddChild(_hoverPreviewTex, forceReadableName: false, Node.InternalMode.Disabled);
		_layer.AddChild(_hoverPreview, forceReadableName: false, Node.InternalMode.Disabled);

		_layoutTicksRemaining = 5;

		sceneTree.Root.CallDeferred("add_child", _layer);
		Plugin.Log("Overlay built and attached to scene tree.");
	}

	private bool IsClickOnControl(Vector2 globalPos, Control control)
	{
		if (control == null || !GodotObject.IsInstanceValid(control)) return false;
		return control.GetGlobalRect().HasPoint(globalPos);
	}

	private void OnTitleBarInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				// Compact toggle now handles its own click via GuiInput
				if (mb.DoubleClick)
				{
					ToggleCollapsed();
					return;
				}
				if (mb.Pressed)
				{
					_dragging = true;
					_dragOffset = mb.GlobalPosition - _panel.GlobalPosition;
				}
				else
				{
					_dragging = false;
					SavePosition();
				}
			}
		}
		else if (ev is InputEventMouseMotion mm && _dragging)
		{
			Vector2 newPos = mm.GlobalPosition - _dragOffset;
			float panelW = _panel.OffsetRight - _panel.OffsetLeft;
			float panelH = _panel.OffsetBottom - _panel.OffsetTop;
			// Convert from anchor-relative offsets to absolute position
			// Panel is anchored to top-right (AnchorLeft=1, AnchorRight=1)
			Vector2 viewportSize = _panel.GetViewportRect().Size;
			_panel.OffsetLeft = newPos.X - viewportSize.X;
			_panel.OffsetRight = newPos.X - viewportSize.X + panelW;
			_panel.OffsetTop = newPos.Y;
			_panel.OffsetBottom = newPos.Y + panelH;
		}
	}

	private void MarkUpdated()
	{
		_lastUpdateTick = Time.GetTicksMsec();
	}

	/// <summary>
	/// Called periodically (~1s) by OverlayInputHandler._Process.
	/// Detects when game screen has changed without a patch firing and clears stale data.
	/// </summary>
	public void CheckForStaleScreen()
	{
		if (_lastUpdateTick == 0) return;
		// Badge lifecycle: badges live on our CanvasLayer, not in the game tree.
		// Clear them when screen changes away from card reward.
		// Update positions when still on card reward (targets may have moved).
		try
		{
			if (_inGameBadges.Count > 0)
			{
				if (_currentScreen != "CARD REWARD" || !GamePatches.IsGenuineCardReward)
				{
					ClearInGameBadges();
				}
				else
				{
					// Still on card reward — update positions and prune dead targets
					UpdateInGameBadgePositions();
				}
			}
		}
		catch (Exception ex) { Plugin.Log($"CheckForStaleScreen badge lifecycle error: {ex.Message}"); }
		// Shop live-refresh: re-read shop state every tick to catch purchases
		if (_currentScreen == "MERCHANT SHOP")
		{
			try { RefreshShopIfChanged(); } catch (Exception ex) { Plugin.Log($"RefreshShopIfChanged error: {ex.Message}"); }
		}
		// Event card offering: detect when an event offers cards (not handled by ShowScreen patch)
		if (_currentScreen == "EVENT" && _currentCards == null)
		{
			try { CheckForEventCardOffering(); } catch (Exception ex) { Plugin.Log($"CheckForEventCardOffering error: {ex.Message}"); }
		}
		ulong elapsed = Time.GetTicksMsec() - _lastUpdateTick;
		// If advice is showing for 8+ seconds, check if the game screen is still valid
		if (elapsed > 8000 && _currentScreen != null)
		{
			try
			{
				SceneTree tree = Engine.GetMainLoop() as SceneTree;
				if (tree?.Root == null) return;
				bool hasCardScreen = HasNodeOfType(tree.Root, "NCardRewardSelectionScreen", 4);
				bool hasRelicScreen = HasNodeOfType(tree.Root, "NChooseARelicSelection", 4);
				bool hasShopScreen = HasNodeOfType(tree.Root, "NMerchantInventory", 4);
				bool hasRestSite = HasNodeOfType(tree.Root, "NRestSiteRoom", 4);
				bool hasCombat = HasNodeOfType(tree.Root, "NCombatRoom", 4);
				bool hasEvent = HasNodeOfType(tree.Root, "NEventRoom", 4);
				bool isEventCardOffer = _currentScreen == "EVENT CARD OFFER";
				bool isCardAdvice = _currentScreen == "CARD REWARD" || _currentScreen == "CARD REMOVAL" || _currentScreen == "CARD UPGRADE" || isEventCardOffer;
				bool isRelicAdvice = _currentScreen == "RELIC REWARD";
				bool isShopAdvice = _currentScreen == "MERCHANT SHOP";
				bool isRestAdvice = _currentScreen == "REST SITE";
				bool isCombatAdvice = _currentScreen == "COMBAT";
				bool isEventAdvice = _currentScreen == "EVENT";

				bool screenGone = false;
				if (isCardAdvice && !hasCardScreen) screenGone = true;
				if (isRelicAdvice && !hasRelicScreen) screenGone = true;
				if (isShopAdvice && !hasShopScreen) screenGone = true;
				if (isRestAdvice && !hasRestSite) screenGone = true;
				if (isCombatAdvice && !hasCombat) screenGone = true;
				if (isEventAdvice && !hasEvent) screenGone = true;

				if (screenGone)
				{
					Plugin.Log($"Stale screen detected: {_currentScreen} no longer active — clearing overlay");
					Clear();
				}
			}
			catch (Exception ex) { Plugin.Log($"CheckForStaleScreen staleness check error: {ex.Message}"); }
		}
	}

	/// <summary>
	/// Called every 0.5s from OverlayInputHandler._Process.
	/// Reads combat piles via CombatManager and updates overlay if changed.
	/// </summary>
	public void UpdateCombatPiles()
	{
		try
		{
			if (!_showCombatPiles) return;
			if (_currentScreen != "COMBAT" && _currentScreen != "MAP / COMBAT") return;
			if (!CombatTracker.IsInCombat()) return;

			var snapshot = CombatTracker.TakeSnapshot();
			if (snapshot == null) return;

			// Quick hash to avoid unnecessary rebuilds
			int hash = snapshot.DrawCount * 10000 + snapshot.DiscardCount * 100 + snapshot.HandCount;
			if (hash == _lastCombatHash && _lastCombatSnapshot != null) return;

			_lastCombatHash = hash;
			_lastCombatSnapshot = snapshot;
			RebuildCombatPileSection();
		}
		catch (Exception ex)
		{
			Plugin.Log($"UpdateCombatPiles error: {ex.Message}");
		}
	}

	private void SavePosition()
	{
		if (_panel != null && GodotObject.IsInstanceValid(_panel) && _settings != null)
		{
			_settings.OffsetLeft = _panel.OffsetLeft;
			_settings.OffsetRight = _panel.OffsetRight;
			_settings.OffsetTop = _panel.OffsetTop;
			// Don't save OffsetBottom — auto-calculated from content height
			_settings.Save();
		}
	}


	public void Clear()
	{
		_currentCards = null;
		_currentRelics = null;
		_currentDeckAnalysis = null;
		_mapAdvice = null;
		_lastCombatSnapshot = null;
		_lastCombatHash = 0;
		if (_combatPileContainer != null && GodotObject.IsInstanceValid(_combatPileContainer))
		{
			SafeDisconnectSignals(_combatPileContainer);
			_combatPileContainer.GetParent()?.RemoveChild(_combatPileContainer);
			_combatPileContainer.QueueFree();
			_combatPileContainer = null;
		}
		_currentScreen = "MAP / COMBAT";
		MarkUpdated();
		if (_hoverPreview != null && GodotObject.IsInstanceValid(_hoverPreview))
			_hoverPreview.Visible = false;
		Rebuild();
	}


	public void ToggleVisible()
	{
		if (EnsureOverlay())
		{
			_visible = !_visible;
			_panel.Visible = _visible;
			if (!_visible && _settingsMenu != null)
				_settingsMenu.Visible = false;
			Plugin.Log("Overlay " + (_visible ? "shown" : "hidden"));
		}
	}

	public void ToggleInGameBadges()
	{
		_showInGameBadges = !_showInGameBadges;
		_settings.ShowInGameBadges = _showInGameBadges;
		_settings.Save();
		// Always clean up existing badges first
		ClearInGameBadges();
		// Re-inject if turning on and a badge-supporting screen is active
		if (_showInGameBadges && _currentScreen == "CARD REWARD" && _currentCards != null)
		{
			try
			{
				SceneTree tree = Engine.GetMainLoop() as SceneTree;
				Node screenNode = tree?.Root != null ? FindNodeOfType(tree.Root, "NCardRewardSelectionScreen", 4) : null;
				if (screenNode != null)
					InjectCardGrades(screenNode, _currentCards, force: true);
			}
			catch (Exception ex)
			{
				Plugin.Log($"ToggleInGameBadges reinject error: {ex.Message}");
			}
		}
		Rebuild();
		Plugin.Log("In-game badges " + (_showInGameBadges ? "shown" : "hidden"));
	}

	/// <summary>
	/// Called from _Input — handles settings menu close on Escape/click-outside.
	/// </summary>
	public void HandleSettingsClose(InputEvent ev)
	{
		if (_settingsMenu != null && _settingsMenu.Visible)
		{
			if (ev is InputEventKey { Pressed: not false } escKey && escKey.Keycode == Key.Escape)
			{
				_settingsMenu.Visible = false;
				return;
			}
			if (ev is InputEventMouseButton { Pressed: true } click)
			{
				Rect2 menuRect = _settingsMenu.GetGlobalRect();
				if (!menuRect.HasPoint(click.GlobalPosition))
				{
					_settingsMenu.Visible = false;
				}
			}
		}
	}

	/// <summary>
	/// Called from _UnhandledKeyInput — hotkeys that the game didn't consume.
	/// </summary>
	public void HandleInput(InputEvent ev)
	{
		if (ev is InputEventKey { Pressed: not false, Echo: false } inputEventKey)
		{
			Key key = inputEventKey.Keycode;
			Key pkey = inputEventKey.PhysicalKeycode;
			if (key == Key.F7 || pkey == Key.F7)
				ToggleVisible();
			else if (inputEventKey.AltPressed && (key == Key.H || pkey == Key.H))
				ToggleVisible();
			// Backtick/tilde as no-modifier fallback (STS2 doesn't use it)
			else if (!inputEventKey.AltPressed && !inputEventKey.CtrlPressed && !inputEventKey.ShiftPressed
				&& (key == Key.Quoteleft || pkey == Key.Quoteleft))
				ToggleVisible();
		}
	}

	public void ToggleDebugOverlay()
	{
		_showDebug = !_showDebug;
		Plugin.Log($"Debug overlay: {(_showDebug ? "ON" : "OFF")}");
		Rebuild();
	}

	private void Rebuild()
	{
		if (!EnsureOverlay())
		{
			return;
		}
		bool screenChanged = _currentScreen != _previousScreen;
		// Invalidate any pending deferred badge calls on screen change
		if (screenChanged)
			_badgeEpoch++;
		// Clean up in-game badges whenever we're not on a genuine card reward screen
		if (_currentScreen != "CARD REWARD" || !GamePatches.IsGenuineCardReward)
		{
			ClearInGameBadges();
		}
		if (_screenLabel != null && GodotObject.IsInstanceValid(_screenLabel))
		{
			if (_collapsed)
				_screenLabel.Text = GetCollapsedSummary();
			else
			{
				// Show a helpful context label instead of raw screen name
				string screenText = _currentScreen switch
				{
					"MAP" => "지도 — 경로를 선택하세요",
					"REST SITE" => "휴식 — 회복 or 업그레이드?",
					"CARD UPGRADE" => "카드 업그레이드 — 현명하게 고르세요",
					"CARD REMOVAL" => "카드 제거 — 덱을 정리하세요",
					"MERCHANT SHOP" => "상점 — 신중하게 둘러보세요",
					"EVENT CARD OFFER" => "이벤트 — 카드 제공",
					"COMBAT" => "전투",
					"EVENT" => "이벤트",
					"IDLE" => "대기 중... (드래그로 이동)",
					_ => _currentScreen
				};
				_screenLabel.Text = screenText;
			}
		}
		// V1: Color-coded title separator
		UpdateTitleSepColor();
		// A1: Win rate
		UpdateWinRate();
		// Hide top-level chip + viz — deck info now always in collapsible DECK BREAKDOWN section
		// Archetype chip removed — deck info in DECK BREAKDOWN section
		ClearDeckViz();
		// Collapsed guard: only update labels, skip full content rebuild
		if (_collapsed)
		{
			ResizePanelToContent();
			_previousScreen = _currentScreen;
			return;
		}
		DisconnectAllHoverSignals();
		var children = _content.GetChildren().ToArray();
		foreach (Node child in children)
		{
			if (child != null)
			{
				if (child is Control ctrl)
					SafeDisconnectSignals(ctrl, recursive: true);
				_content.RemoveChild(child);
				child.QueueFree();
			}
		}
		// Update notification banner
		if (Plugin.LatestVersion != null)
		{
			Label updateLbl = new Label();
			updateLbl.Text = $"\u26a0 Update Available: v{Plugin.LatestVersion}";
			ApplyFont(updateLbl, _fontBold);
			updateLbl.AddThemeFontSizeOverride("font_size", 14);
			updateLbl.AddThemeColorOverride("font_color", ClrExpensive);
			_content.AddChild(updateLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		bool hasCards = _currentCards != null && _currentCards.Count > 0;
		bool hasRelics = _currentRelics != null && _currentRelics.Count > 0;
		bool isRemoval = _currentScreen == "CARD REMOVAL";
		bool isUpgrade = _currentScreen == "CARD UPGRADE";
		bool isEventOffer = _currentScreen == "EVENT CARD OFFER";
		if (hasCards)
		{
			bool isShop = _currentScreen == "MERCHANT SHOP";
			AddSectionHeader(isRemoval ? "제거 추천 카드" : isShop ? "상점 추천 카드" : isUpgrade ? "업그레이드 추천" : isEventOffer ? "이벤트 카드" : "카드 분석");
			// Shop: show top 3 by score to keep panel compact
			// Upgrade: show top 3 upgrade targets
			// Non-shop: preserve game order so overlay matches on-screen badge positions
			var cardsToShow = (isShop || isUpgrade)
				? _currentCards.OrderByDescending(c => c.FinalScore).Take(3).ToList()
				: _currentCards.ToList();
			foreach (ScoredCard currentCard in cardsToShow)
			{
				AddCardEntry(currentCard);
			}
			int skippedCards = _currentCards.Count - cardsToShow.Count;
			if ((isShop || isUpgrade) && skippedCards > 0)
			{
				Label skipLbl = new Label();
				skipLbl.Text = $"  + {skippedCards}장 낮은 등급 카드";
				ApplyFont(skipLbl, _fontBody);
				skipLbl.AddThemeColorOverride("font_color", ClrSub);
				skipLbl.AddThemeFontSizeOverride("font_size", 13);
				_content.AddChild(skipLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			// Skip recommendation — tighter thresholds aligned with community consensus
			// Guides: ideal deck 20-25 cards, skip aggressively, every card dilutes draw pool
			if (!isRemoval && !isShop && !isUpgrade && !isEventOffer)
			{
				int deckSize = _currentDeckAnalysis?.TotalCards ?? 20;
				bool hasFocusedDeck = _currentDeckAnalysis?.DetectedArchetypes?.Count > 0 &&
					_currentDeckAnalysis.DetectedArchetypes[0].Strength > 0.4f;
				// Tighter skip thresholds: 20-25 is ideal, past 25 is bloated
				float skipThreshold = deckSize <= 15 ? 2.5f
					: deckSize <= 20 ? 2.2f
					: deckSize <= 25 ? (hasFocusedDeck ? 2.8f : 2.5f)
					: 3.0f; // 25+ cards: only take A-tier or better
				bool allWeak = _currentCards.All(c => c.FinalScore < skipThreshold);
				bool bestIsLow = _currentCards.Count > 0 && _currentCards.Max(c => c.FinalScore) < 2.5f;
				if (allWeak)
				{
					string skipMsg = deckSize <= 18
						? "덱 개선 효과 없음 — 스킵 추천"
						: deckSize <= 25
							? (hasFocusedDeck
								? "아키타입에 맞는 카드가 없습니다 — 스킵을 고려하세요."
								: "모든 카드가 약합니다 — 덱 집중을 위해 스킵하세요.")
							: $"카드 {deckSize}장 — 덱이 비대합니다. S/A 티어만 가져가세요.";
					AddSkipEntry("\u26A0 스킵 고려", skipMsg);
				}
				else if (bestIsLow && deckSize >= 20)
				{
					AddSkipEntry("\u26A0 덱 비대", $"카드 {deckSize}장 — 고효율 카드만 추가하세요.");
				}
			}
		}
		if (hasRelics)
		{
			bool isShop = _currentScreen == "MERCHANT SHOP";
			AddSectionHeader(isShop ? "상점 추천 유물" : "유물 분석");
			var relicsToShow = isShop
				? _currentRelics.OrderByDescending(r => r.FinalScore).Take(3).ToList()
				: _currentRelics.ToList();
			foreach (ScoredRelic currentRelic in relicsToShow)
			{
				AddRelicEntry(currentRelic);
			}
			int skippedRelics = _currentRelics.Count - relicsToShow.Count;
			if (isShop && skippedRelics > 0)
			{
				Label skipRLbl = new Label();
				skipRLbl.Text = $"  + {skippedRelics}개 낮은 등급 유물";
				ApplyFont(skipRLbl, _fontBody);
				skipRLbl.AddThemeColorOverride("font_color", ClrSub);
				skipRLbl.AddThemeFontSizeOverride("font_size", 13);
				_content.AddChild(skipRLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		if (!hasCards && !hasRelics && _mapAdvice != null && _mapAdvice.Count > 0)
		{
			string adviceHeader = _currentScreen switch
			{
				"COMBAT" => "전투 팁",
				"EVENT" => "이벤트 조언",
				"MAP" => "경로 조언",
				"REST SITE" => "휴식",
				_ => "조언"
			};
			AddSectionHeader(adviceHeader);
			foreach (var (icon, text, color) in _mapAdvice)
			{
				// Sub-section header marker
				if (icon == "##")
				{
					HSeparator subSep = new HSeparator();
					subSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.3f), Thickness = 1 });
					_content.AddChild(subSep, forceReadableName: false, Node.InternalMode.Disabled);
					Label subHeader = new Label();
					subHeader.Text = text;
					ApplyFont(subHeader, _fontBold);
					subHeader.AddThemeColorOverride("font_color", color);
					subHeader.AddThemeFontSizeOverride("font_size", 15);
					_content.AddChild(subHeader, forceReadableName: false, Node.InternalMode.Disabled);
					continue;
				}
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
				advLbl.AddThemeFontSizeOverride("font_size", 17);
				advLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				advPanel.AddChild(advLbl, forceReadableName: false, Node.InternalMode.Disabled);
				_content.AddChild(advPanel, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		// (no placeholder when no cards/relics — deck breakdown below is sufficient)
		// YOUR STATS — show on MAP/IDLE screens when enough local data exists
		string statsCharacter = _currentCharacter ?? Plugin.RunTracker?.CurrentCharacter;
		if ((_currentScreen == "MAP" || _currentScreen == "IDLE" || _currentScreen == "MAP / COMBAT") && statsCharacter != null)
		{
			try
			{
				var stats = Plugin.RunDatabase?.GetStatsComparison(statsCharacter);
				if (stats.HasValue && stats.Value.localRuns >= 3)
				{
					var (localWR, localN, commWR, commN) = stats.Value;
					AddSectionHeader("내 통계");
					float delta = localWR - commWR;
					Color deltaColor = delta >= 0 ? ClrPositive : ClrNegative;
					string deltaStr = delta >= 0 ? $"+{delta:F1}%" : $"{delta:F1}%";

					Label statsLbl = new Label();
					statsLbl.Text = $"승률: {localWR:F1}% ({localN}회)";
					ApplyFont(statsLbl, _fontBody);
					statsLbl.AddThemeFontSizeOverride("font_size", 15);
					statsLbl.AddThemeColorOverride("font_color", ClrCream);
					_content.AddChild(statsLbl, forceReadableName: false, Node.InternalMode.Disabled);

					if (commN > 0)
					{
						Label commLbl = new Label();
						commLbl.Text = $"커뮤니티: {commWR:F1}% (차이: {deltaStr})";
						ApplyFont(commLbl, _fontBody);
						commLbl.AddThemeFontSizeOverride("font_size", 14);
						commLbl.AddThemeColorOverride("font_color", deltaColor);
						_content.AddChild(commLbl, forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Log($"Stats comparison error: {ex.Message}");
			}
		}
		// Deck breakdown — only shown when enabled in settings
		if (_showDeckBreakdown && _currentDeckAnalysis != null)
		{
			AddSectionHeader("덱 구성");
			AddInlineDeckVizTo(_content, _currentDeckAnalysis);
		}
		// Decision history — only shown when explicitly enabled in settings
		if (_showHistory)
		{
			AddDecisionHistory();
		}
		// Debug overlay (F10)
		if (_showDebug)
		{
			AddSectionHeader("DEBUG (F10)");
			// Hook status
			string[] hookNames = { "OnCardRewardOpened", "OnRelicRewardOpened", "OnShopOpened",
				"OnMapScreenEntered", "OnEventShowChoices", "OnCombatSetup", "OnRestSiteOpened",
				"OnRunLaunched", "OnRunEnded" };
			foreach (var hookName in hookNames)
			{
				string timeStr = GamePatches.HookLastFired.TryGetValue(hookName, out var dt)
					? dt.ToString("HH:mm:ss")
					: "never";
				Label hookLbl = new Label();
				hookLbl.Text = $"  {hookName}: {timeStr}";
				ApplyFont(hookLbl, _fontBody);
				hookLbl.AddThemeFontSizeOverride("font_size", 12);
				hookLbl.AddThemeColorOverride("font_color", timeStr == "never" ? ClrSub : ClrCream);
				_content.AddChild(hookLbl, forceReadableName: false, Node.InternalMode.Disabled);
			}
			// State info
			Label stateLbl = new Label();
			int deckSize = _currentDeckAnalysis?.TotalCards ?? 0;
			stateLbl.Text = $"  Screen: {_currentScreen}  Char: {_currentCharacter ?? "?"}  Floor: {_currentFloor}  Deck: {deckSize}  v{Plugin.ModVersion}";
			ApplyFont(stateLbl, _fontBody);
			stateLbl.AddThemeFontSizeOverride("font_size", 12);
			stateLbl.AddThemeColorOverride("font_color", ClrAqua);
			_content.AddChild(stateLbl, forceReadableName: false, Node.InternalMode.Disabled);
		}
		// Settings button at bottom
		{
			_gearButton = new Button();
			_gearButton.Text = "\u2699 설정";
			_gearButton.Flat = true;
			if (_fontBody != null) _gearButton.AddThemeFontOverride("font", _fontBody);
			_gearButton.AddThemeFontSizeOverride("font_size", 13);
			_gearButton.AddThemeColorOverride("font_color", new Color(ClrSub, 0.7f));
			_gearButton.AddThemeColorOverride("font_hover_color", ClrHeader);
			_gearButton.MouseFilter = Control.MouseFilterEnum.Stop;
			_gearButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			_gearButton.Pressed += () => ToggleSettingsMenu();
			_content.AddChild(_gearButton, forceReadableName: false, Node.InternalMode.Disabled);
		}
		ResizePanelToContent();
		// V2: Fade-in on screen change
		if (screenChanged && _content != null && GodotObject.IsInstanceValid(_content))
		{
			_content.Modulate = new Color(1, 1, 1, 0);
			_content.CreateTween()?.TweenProperty(_content, "modulate", Colors.White, 0.2f);
		}
		_previousScreen = _currentScreen;
	}

	private void ResizePanelToContent()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
			return;
		// Defer to next frame so Godot computes layout first
		Callable.From(FitPanelHeight).CallDeferred();
	}

	private void FitPanelHeight()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
			return;
		// Reset to let Godot recalculate from children
		_panel.CustomMinimumSize = Vector2.Zero;
		_panel.ResetSize();
		// After ResetSize, the panel collapses to minimum. Read its actual size.
		// Use a second deferred call so Godot has a frame to re-layout.
		Callable.From(FitPanelHeightFinalize).CallDeferred();
	}

	private void FitPanelHeightFinalize()
	{
		if (_panel == null || !GodotObject.IsInstanceValid(_panel))
			return;
		// Use the tighter of actual size vs combined minimum to avoid excess space
		float sizeY = _panel.Size.Y;
		float minY = _panel.GetCombinedMinimumSize().Y;
		float height = (minY > 40f) ? Math.Min(sizeY, minY) : sizeY;
		height = Math.Max(height, 40f);
		var viewport = _panel.GetViewportRect().Size;
		if (viewport.Y > 0) height = Math.Min(height, viewport.Y - _panel.OffsetTop - 10f);
		_panel.OffsetBottom = _panel.OffsetTop + height;
	}

	/// <summary>
	/// Called from OverlayInputHandler._Process for the first few ticks to let layout stabilize.
	/// </summary>
	public void StabilizeLayout()
	{
		if (_layoutTicksRemaining <= 0) return;
		_layoutTicksRemaining--;
		FitPanelHeight();
	}

	// === Feature 3: Opacity control ===

	public void CycleOpacity()
	{
		_opacityIndex = (_opacityIndex + 1) % OpacitySteps.Length;
		_panelOpacity = OpacitySteps[_opacityIndex];
		ApplyOpacity(_panelOpacity);
		_settings.PanelOpacity = _panelOpacity;
		_settings.Save();
		Plugin.Log($"Opacity set to {(int)(_panelOpacity * 100)}%");
	}

	private void ApplyOpacity(float opacity)
	{
		if (_sbPanel != null) _sbPanel.BgColor = new Color(ClrBg.R, ClrBg.G, ClrBg.B, 0.97f * opacity);
		if (_sbEntry != null) _sbEntry.BgColor = new Color(0.06f, 0.08f, 0.14f, 0.6f * opacity);
		if (_sbBest != null) _sbBest.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.1f * opacity);
		if (_sbHover != null) _sbHover.BgColor = new Color(ClrHover.R, ClrHover.G, ClrHover.B, 0.8f * opacity);
		if (_sbHoverBest != null) _sbHoverBest.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.15f * opacity);
		if (_sbSTier != null) _sbSTier.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.1f * opacity);
		if (_sbSTierHover != null) _sbSTierHover.BgColor = new Color(0.831f, 0.714f, 0.357f, 0.18f * opacity);
		if (_sbChip != null) _sbChip.BgColor = new Color(0.02f, 0.03f, 0.07f, 0.7f * opacity);
	}

	// === Feature 4: Collapsible mode ===

	public void ToggleCollapsed()
	{
		_collapsed = !_collapsed;
		_settings.Collapsed = _collapsed;
		_settings.Save();
		if (_content != null && GodotObject.IsInstanceValid(_content))
			_content.Visible = !_collapsed;
		// _deckVizContainer stays hidden — deck info in DECK BREAKDOWN
		if (_titleSep != null && GodotObject.IsInstanceValid(_titleSep))
			_titleSep.Visible = !_collapsed;
		if (_screenLabel != null && GodotObject.IsInstanceValid(_screenLabel))
			_screenLabel.Visible = true; // Always visible — shows collapsed summary when collapsed
		if (_winRateLabel != null && GodotObject.IsInstanceValid(_winRateLabel))
			_winRateLabel.Visible = !_collapsed;
		// Update compact toggle arrow
		if (_compactToggle != null && GodotObject.IsInstanceValid(_compactToggle))
			_compactToggle.Text = _collapsed ? "\u25BC" : "\u25B2";
		// When expanding, rebuild content since Rebuild() skips content when collapsed
		if (!_collapsed)
			Rebuild();
		else
		{
			// Update the screen label to show collapsed summary
			if (_screenLabel != null && GodotObject.IsInstanceValid(_screenLabel))
				_screenLabel.Text = GetCollapsedSummary();
			ResizePanelToContent();
		}
		Plugin.Log("Overlay " + (_collapsed ? "collapsed" : "expanded"));
	}

	private string GetCollapsedSummary()
	{
		string gradeStr = "";
		if (_currentCards != null && _currentCards.Count > 0)
		{
			var best = _currentCards.FirstOrDefault(c => c.IsBestPick) ?? _currentCards[0];
			gradeStr = TierEngine.ScoreToSubGrade(best.FinalScore);
		}
		else if (_currentRelics != null && _currentRelics.Count > 0)
		{
			var best = _currentRelics.FirstOrDefault(r => r.IsBestPick) ?? _currentRelics[0];
			gradeStr = TierEngine.ScoreToSubGrade(best.FinalScore);
		}
		string screen = _currentScreen ?? "IDLE";
		return gradeStr.Length > 0 ? $"{gradeStr}  {screen}" : screen;
	}

}
