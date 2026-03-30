using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI.Injectors;

/// <summary>
/// Abstract base class for per-screen UI injectors.
/// Each subclass builds its own advice panel and injects it into a game node.
/// The panel uses TopLevel=true to render at viewport coordinates.
/// When the game node is freed, the panel is automatically cleaned up.
/// </summary>
public abstract class BaseScreenInjector
{
	protected SharedResources Res => SharedResources.Instance;
	protected OverlaySettings Settings { get; }

	protected PanelContainer Panel { get; private set; }
	protected VBoxContainer Content { get; private set; }
	private Label _screenLabel;
	private HSeparator _titleSep;
	private Label _compactToggle;
	private Label _winRateLabel;

	// Signal cleanup
	private readonly List<Control> _connectedHoverNodes = new();
	private static readonly string[] TrackedSignals = { "mouse_entered", "mouse_exited", "gui_input", "pressed" };

	// State
	private bool _collapsed;
	private bool _visible = true;
	private int _layoutTicksRemaining;

	// Drag
	private bool _dragging;
	private Vector2 _dragOffset;

	// Auto-fade
	private float _fadeValue = 1.0f;
	private float _fadeTarget = 1.0f;
	private double _idleTimer;
	private bool _mouseInPanel;

	/// <summary>Screen identifier (e.g. "CARD REWARD", "COMBAT", "MAP")</summary>
	public abstract string ScreenName { get; }

	protected BaseScreenInjector(OverlaySettings settings)
	{
		Settings = settings;
		_collapsed = settings.Collapsed;
	}

	/// <summary>
	/// Build the panel structure and inject into the game node.
	/// </summary>
	public void Inject(Node gameNode)
	{
		if (Panel == null || !GodotObject.IsInstanceValid(Panel))
			BuildPanel();

		if (Panel == null) return;

		Node targetParent = (gameNode != null && GodotObject.IsInstanceValid(gameNode))
			? gameNode : null;
		if (targetParent == null) return;

		var currentParent = Panel.GetParent();
		if (currentParent == targetParent) return;

		if (currentParent != null && GodotObject.IsInstanceValid(currentParent))
			currentParent.RemoveChild(Panel);

		targetParent.CallDeferred("add_child", Panel);
		_layoutTicksRemaining = 5;
	}

	/// <summary>
	/// Detach panel from current parent and optionally attach to a fallback parent.
	/// </summary>
	public void Detach(Node fallbackParent = null)
	{
		if (Panel == null || !GodotObject.IsInstanceValid(Panel)) return;

		var currentParent = Panel.GetParent();
		if (currentParent != null && GodotObject.IsInstanceValid(currentParent))
			currentParent.RemoveChild(Panel);

		if (fallbackParent != null && GodotObject.IsInstanceValid(fallbackParent))
			fallbackParent.CallDeferred("add_child", Panel);
	}

	public bool IsValid()
		=> Panel != null && GodotObject.IsInstanceValid(Panel) && Content != null && GodotObject.IsInstanceValid(Content);

	/// <summary>Clear content and rebuild from subclass.</summary>
	public void Rebuild()
	{
		if (!IsValid()) return;

		UpdateScreenLabel();
		UpdateTitleSepColor();

		if (_collapsed)
		{
			ResizePanelToContent();
			return;
		}

		DisconnectAllHoverSignals();
		ClearContent();

		BuildContent();

		// Stagger fade-in for entries
		int entryIdx = 0;
		foreach (Node child in Content.GetChildren())
		{
			if (child is PanelContainer entry && GodotObject.IsInstanceValid(entry))
			{
				entry.Modulate = new Color(1, 1, 1, 0);
				var tw = entry.CreateTween();
				tw?.TweenProperty(entry, "modulate:a", 1f, 0.15f)
					.SetDelay(entryIdx * 0.04f)
					.SetEase(Tween.EaseType.Out);
				entryIdx++;
			}
		}

		ResizePanelToContent();
	}

	/// <summary>Override in subclass to populate Content with screen-specific UI.</summary>
	protected abstract void BuildContent();

	/// <summary>Destroy the panel and clean up.</summary>
	public void Destroy()
	{
		DisconnectAllHoverSignals();
		if (Panel != null && GodotObject.IsInstanceValid(Panel))
		{
			Panel.GetParent()?.RemoveChild(Panel);
			Panel.QueueFree();
		}
		Panel = null;
		Content = null;
	}

	// === Panel construction ===

	private void BuildPanel()
	{
		Panel = new PanelContainer();
		Panel.TopLevel = true;
		Panel.AnchorLeft = 1f;
		Panel.AnchorRight = 1f;
		Panel.AnchorTop = 0f;
		Panel.AnchorBottom = 0f;
		Panel.OffsetLeft = Settings.OffsetLeft;
		Panel.OffsetRight = Settings.OffsetRight;
		Panel.OffsetTop = Settings.OffsetTop;
		Panel.OffsetBottom = Settings.OffsetTop + 40;
		Panel.GrowVertical = Control.GrowDirection.End;
		Panel.AddThemeStyleboxOverride("panel", Res.SbPanel);
		Panel.MouseFilter = Control.MouseFilterEnum.Stop;

		Panel.Connect("mouse_entered", Callable.From(() =>
		{
			_mouseInPanel = true;
			_idleTimer = 0;
			_fadeTarget = 1.0f;
		}));
		Panel.Connect("mouse_exited", Callable.From(() =>
		{
			_mouseInPanel = false;
			_idleTimer = 0;
		}));

		var mainVBox = new VBoxContainer();
		mainVBox.AddThemeConstantOverride("separation", OverlayTheme.SpaceLG);
		Panel.AddChild(mainVBox, forceReadableName: false, Node.InternalMode.Disabled);

		// Title bar
		var titleBar = new VBoxContainer();
		titleBar.MouseFilter = Control.MouseFilterEnum.Pass;
		titleBar.MouseDefaultCursorShape = Control.CursorShape.Drag;
		titleBar.Connect("gui_input", Callable.From((InputEvent ev) => OnTitleBarInput(ev)));
		mainVBox.AddChild(titleBar, forceReadableName: false, Node.InternalMode.Disabled);

		var titleRow = new HBoxContainer();
		titleRow.MouseFilter = Control.MouseFilterEnum.Pass;
		titleBar.AddChild(titleRow, forceReadableName: false, Node.InternalMode.Disabled);

		var titleLabel = new Label();
		titleLabel.Text = "QU'EST-CE SPIRE?";
		Res.ApplyFont(titleLabel, Res.FontBold);
		titleLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontTitle);
		titleLabel.AddThemeColorOverride("font_color", SharedResources.ClrHeader);
		titleLabel.AddThemeConstantOverride("outline_size", 4);
		titleLabel.AddThemeColorOverride("font_outline_color", SharedResources.ClrOutline);
		titleLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		titleLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		titleLabel.AddThemeColorOverride("font_shadow_color", OverlayTheme.Shadow);
		titleLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(titleLabel, forceReadableName: false, Node.InternalMode.Disabled);

		// Version badge
		var vBadge = new PanelContainer();
		var vbStyle = new StyleBoxFlat();
		vbStyle.BgColor = new Color(SharedResources.ClrAccent, 0.15f);
		vbStyle.CornerRadiusTopLeft = vbStyle.CornerRadiusTopRight = vbStyle.CornerRadiusBottomLeft = vbStyle.CornerRadiusBottomRight = 10;
		vbStyle.ContentMarginLeft = vbStyle.ContentMarginRight = 8f;
		vbStyle.ContentMarginTop = vbStyle.ContentMarginBottom = 2f;
		vbStyle.BorderWidthTop = vbStyle.BorderWidthBottom = vbStyle.BorderWidthLeft = vbStyle.BorderWidthRight = 1;
		vbStyle.BorderColor = new Color(SharedResources.ClrAccent, 0.3f);
		vBadge.AddThemeStyleboxOverride("panel", vbStyle);
		vBadge.MouseFilter = Control.MouseFilterEnum.Ignore;
		var vLabel = new Label();
		vLabel.Text = $"v{Plugin.ModVersion}";
		Res.ApplyFont(vLabel, Res.FontBody);
		vLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
		vLabel.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		vLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vBadge.AddChild(vLabel, forceReadableName: false, Node.InternalMode.Disabled);
		titleRow.AddChild(vBadge, forceReadableName: false, Node.InternalMode.Disabled);

		// Settings gear button
		var gearBtn = new Label();
		gearBtn.Text = "\u2699";
		Res.ApplyFont(gearBtn, Res.FontBold);
		gearBtn.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		gearBtn.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		gearBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		gearBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		gearBtn.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				Plugin.Coordinator?.ToggleSettings();
		}));
		titleRow.AddChild(gearBtn, forceReadableName: false, Node.InternalMode.Disabled);

		// Collapse toggle
		_compactToggle = new Label();
		_compactToggle.Text = _collapsed ? "\u25BC" : "\u25B2";
		Res.ApplyFont(_compactToggle, Res.FontBold);
		_compactToggle.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH1);
		_compactToggle.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		_compactToggle.MouseFilter = Control.MouseFilterEnum.Stop;
		_compactToggle.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_compactToggle.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				ToggleCollapsed();
		}));
		titleRow.AddChild(_compactToggle, forceReadableName: false, Node.InternalMode.Disabled);

		// Separator
		_titleSep = new HSeparator();
		_titleSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(SharedResources.ClrBorder, 0.6f), Thickness = 2 });
		_titleSep.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_titleSep, forceReadableName: false, Node.InternalMode.Disabled);

		// Screen label
		_screenLabel = new Label();
		_screenLabel.Text = ScreenName;
		Res.ApplyFont(_screenLabel, Res.FontBold);
		_screenLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		_screenLabel.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		_screenLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_screenLabel, forceReadableName: false, Node.InternalMode.Disabled);

		// Win rate label
		_winRateLabel = new Label();
		_winRateLabel.Text = "";
		_winRateLabel.Visible = false;
		Res.ApplyFont(_winRateLabel, Res.FontBody);
		_winRateLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		_winRateLabel.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		_winRateLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		titleBar.AddChild(_winRateLabel, forceReadableName: false, Node.InternalMode.Disabled);

		// Content
		Content = new VBoxContainer();
		Content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		Content.AddThemeConstantOverride("separation", OverlayTheme.SpaceMD);
		mainVBox.AddChild(Content, forceReadableName: false, Node.InternalMode.Disabled);

		Panel.Visible = _visible;
		if (_collapsed) Content.Visible = false;

		_layoutTicksRemaining = 5;
	}

	// === Shared UI builder helpers ===

	protected void AddSectionHeader(string text)
	{
		if (Content.GetChildCount() > 0)
		{
			var sep = new HSeparator();
			sep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(SharedResources.ClrBorder, 0.3f), Thickness = 1 });
			Content.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
		}

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);

		var accent = new PanelContainer();
		var accentStyle = new StyleBoxFlat();
		accentStyle.BgColor = SharedResources.GetScreenColor(ScreenName);
		accentStyle.ContentMarginLeft = 3f;
		accentStyle.ContentMarginRight = 0f;
		accentStyle.ContentMarginTop = 0f;
		accentStyle.ContentMarginBottom = 0f;
		OverlayStyles.SetAllCornerRadius(accentStyle, 2);
		accent.AddThemeStyleboxOverride("panel", accentStyle);
		accent.CustomMinimumSize = new Vector2(3, 0);
		hbox.AddChild(accent, forceReadableName: false, Node.InternalMode.Disabled);

		var label = new Label();
		label.Text = text.ToUpperInvariant();
		Res.ApplyFont(label, Res.FontBold);
		label.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		label.AddThemeColorOverride("font_color", SharedResources.ClrHeader);
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);

		Content.AddChild(hbox, forceReadableName: false, Node.InternalMode.Disabled);
	}

	protected void AddAdviceTip(string icon, string text, Color color)
	{
		var advPanel = new PanelContainer();
		var advStyle = new StyleBoxFlat();
		advStyle.BgColor = new Color(OverlayTheme.BgEntry, 0.5f);
		advStyle.CornerRadiusTopRight = 8;
		advStyle.CornerRadiusBottomRight = 8;
		advStyle.BorderWidthLeft = 3;
		advStyle.BorderColor = new Color(color, 0.6f);
		advStyle.ContentMarginLeft = 12f;
		advStyle.ContentMarginRight = 10f;
		advStyle.ContentMarginTop = 6f;
		advStyle.ContentMarginBottom = 6f;
		advPanel.AddThemeStyleboxOverride("panel", advStyle);

		var lbl = new Label();
		lbl.Text = $"{icon}  {text}";
		Res.ApplyFont(lbl, Res.FontBody);
		lbl.AddThemeColorOverride("font_color", color);
		lbl.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		advPanel.AddChild(lbl, forceReadableName: false, Node.InternalMode.Disabled);
		Content.AddChild(advPanel, forceReadableName: false, Node.InternalMode.Disabled);
	}

	protected void AddSubSectionHeader(string text, Color color)
	{
		var subSep = new HSeparator();
		subSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(SharedResources.ClrBorder, 0.3f), Thickness = 1 });
		Content.AddChild(subSep, forceReadableName: false, Node.InternalMode.Disabled);

		var subHeader = new Label();
		subHeader.Text = text;
		Res.ApplyFont(subHeader, Res.FontBold);
		subHeader.AddThemeColorOverride("font_color", color);
		subHeader.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		Content.AddChild(subHeader, forceReadableName: false, Node.InternalMode.Disabled);
	}

	// === Lifecycle helpers ===

	private void ClearContent()
	{
		if (Content == null) return;
		var children = Content.GetChildren().ToArray();
		foreach (Node child in children)
		{
			if (child is Control ctrl)
				SafeDisconnectSignals(ctrl, recursive: true);
			Content.RemoveChild(child);
			child.QueueFree();
		}
	}

	protected void ConnectHoverSignals(Control node, Action onEnter, Action onExit)
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
				SafeDisconnectSignals(node);
		}
		_connectedHoverNodes.Clear();
	}

	private static void SafeDisconnectSignals(Control node, bool recursive = false)
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

	// === Drag ===

	private void OnTitleBarInput(InputEvent ev)
	{
		if (ev is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.DoubleClick) { ToggleCollapsed(); return; }
				if (mb.Pressed)
				{
					_dragging = true;
					_dragOffset = mb.GlobalPosition - Panel.GlobalPosition;
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
			float panelW = Panel.OffsetRight - Panel.OffsetLeft;
			Vector2 viewportSize = Panel.GetViewportRect().Size;
			Panel.OffsetLeft = newPos.X - viewportSize.X;
			Panel.OffsetRight = newPos.X - viewportSize.X + panelW;
			Panel.OffsetTop = newPos.Y;
			Panel.OffsetBottom = newPos.Y + (Panel.OffsetBottom - Panel.OffsetTop);
		}
	}

	private void SavePosition()
	{
		if (Panel != null && GodotObject.IsInstanceValid(Panel))
		{
			Settings.OffsetLeft = Panel.OffsetLeft;
			Settings.OffsetRight = Panel.OffsetRight;
			Settings.OffsetTop = Panel.OffsetTop;
			Settings.Save();
		}
	}

	// === Collapse ===

	public void ToggleCollapsed()
	{
		_collapsed = !_collapsed;
		Settings.Collapsed = _collapsed;
		Settings.Save();
		if (Content != null && GodotObject.IsInstanceValid(Content))
			Content.Visible = !_collapsed;
		if (_titleSep != null && GodotObject.IsInstanceValid(_titleSep))
			_titleSep.Visible = !_collapsed;
		if (_compactToggle != null && GodotObject.IsInstanceValid(_compactToggle))
			_compactToggle.Text = _collapsed ? "\u25BC" : "\u25B2";
		if (_winRateLabel != null && GodotObject.IsInstanceValid(_winRateLabel))
			_winRateLabel.Visible = !_collapsed;
		if (!_collapsed) Rebuild();
		else ResizePanelToContent();
	}

	// === Visibility ===

	public void SetVisible(bool visible)
	{
		_visible = visible;
		if (Panel != null && GodotObject.IsInstanceValid(Panel))
			Panel.Visible = visible;
	}

	// === Auto-fade ===

	public void ProcessAutoFade(double delta)
	{
		if (!Settings.AutoFadeEnabled || Panel == null || !GodotObject.IsInstanceValid(Panel))
			return;
		if (!_mouseInPanel)
		{
			_idleTimer += delta;
			if (_idleTimer >= Settings.IdleDelaySeconds)
				_fadeTarget = Settings.IdleOpacity;
		}
		float diff = _fadeTarget - _fadeValue;
		if (Math.Abs(diff) > 0.005f)
		{
			_fadeValue += diff * (float)(5.0 * delta);
			Panel.Modulate = new Color(1f, 1f, 1f, _fadeValue);
		}
		else if (Math.Abs(diff) > 0.001f)
		{
			_fadeValue = _fadeTarget;
			Panel.Modulate = new Color(1f, 1f, 1f, _fadeValue);
		}
	}

	// === Layout ===

	public void StabilizeLayout()
	{
		if (_layoutTicksRemaining <= 0) return;
		_layoutTicksRemaining--;
		FitPanelHeight();
	}

	private void ResizePanelToContent()
	{
		if (Panel == null || !GodotObject.IsInstanceValid(Panel)) return;
		Callable.From(FitPanelHeight).CallDeferred();
	}

	private void FitPanelHeight()
	{
		if (Panel == null || !GodotObject.IsInstanceValid(Panel)) return;
		Panel.CustomMinimumSize = Vector2.Zero;
		Panel.ResetSize();
		Callable.From(FitPanelHeightFinalize).CallDeferred();
	}

	private void FitPanelHeightFinalize()
	{
		if (Panel == null || !GodotObject.IsInstanceValid(Panel)) return;
		float sizeY = Panel.Size.Y;
		float minY = Panel.GetCombinedMinimumSize().Y;
		float height = (minY > 40f) ? Math.Min(sizeY, minY) : sizeY;
		height = Math.Max(height, 40f);
		var viewport = Panel.GetViewportRect().Size;
		if (viewport.Y > 0) height = Math.Min(height, viewport.Y - Panel.OffsetTop - 10f);
		Panel.OffsetBottom = Panel.OffsetTop + height;
	}

	// === Screen label ===

	private void UpdateScreenLabel()
	{
		if (_screenLabel == null || !GodotObject.IsInstanceValid(_screenLabel)) return;
		_screenLabel.Text = ScreenName switch
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
			_ => ScreenName
		};
	}

	private void UpdateTitleSepColor()
	{
		if (_titleSep == null || !GodotObject.IsInstanceValid(_titleSep)) return;
		Color c = SharedResources.GetScreenColor(ScreenName);
		_titleSep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(c, 0.6f), Thickness = 2 });
	}
}
