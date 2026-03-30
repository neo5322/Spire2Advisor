using System;
using System.Collections.Generic;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.GameBridge;
using QuestceSpire.UI.Injectors;

namespace QuestceSpire.UI;

/// <summary>
/// Coordinates per-screen injectors, manages shared resources and global UI elements.
/// Replaces OverlayManager as the central entry point for overlay UI.
///
/// Migration strategy: screens are migrated one at a time from OverlayManager to injectors.
/// During migration, both OverlayCoordinator (new) and OverlayManager (legacy) coexist.
/// </summary>
public class OverlayCoordinator
{
	private readonly OverlaySettings _settings;
	private readonly Dictionary<string, BaseScreenInjector> _injectors = new();
	private BaseScreenInjector _activeInjector;
	private CanvasLayer _utilityLayer;
	private OverlayInputHandler _inputHandler;
	private bool _visible = true;

	public OverlayCoordinator(OverlaySettings settings)
	{
		_settings = settings;
		SharedResources.Initialize();
		BuildUtilityLayer();
		Plugin.Log("OverlayCoordinator initialized.");
	}

	private void BuildUtilityLayer()
	{
		SceneTree sceneTree = Engine.GetMainLoop() as SceneTree;
		if (sceneTree?.Root == null)
		{
			Plugin.Log("SceneTree not ready — coordinator utility layer deferred.");
			return;
		}
		_utilityLayer = new CanvasLayer();
		_utilityLayer.Layer = 100;

		_inputHandler = new OverlayInputHandler(this);
		_utilityLayer.AddChild(_inputHandler, forceReadableName: false, Node.InternalMode.Disabled);

		sceneTree.Root.CallDeferred("add_child", _utilityLayer);
	}

	/// <summary>
	/// Get or create an injector of the specified type.
	/// </summary>
	public T GetInjector<T>() where T : BaseScreenInjector
	{
		string key = typeof(T).Name;
		if (_injectors.TryGetValue(key, out var existing))
			return (T)existing;

		var injector = (T)Activator.CreateInstance(typeof(T), _settings);
		_injectors[key] = injector;
		return injector;
	}

	/// <summary>
	/// Activate an injector — deactivates any currently active injector first.
	/// </summary>
	public void Activate(BaseScreenInjector injector)
	{
		if (_activeInjector != null && _activeInjector != injector)
		{
			_activeInjector.Detach();
		}
		_activeInjector = injector;
		injector.SetVisible(_visible);
	}

	/// <summary>
	/// Deactivate current injector (on screen close / Clear).
	/// </summary>
	public void DeactivateCurrent()
	{
		if (_activeInjector != null)
		{
			_activeInjector.Detach();
			_activeInjector = null;
		}
	}

	// === Screen methods ===

	public void ShowMapAdvice(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor)
	{
		var injector = GetInjector<MapInjector>();
		Activate(injector);
		injector.Show(gameNode, deckAnalysis, currentHP, maxHP, gold, actNumber, floor);
	}

	public void ShowRestSiteAdvice(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState)
	{
		var injector = GetInjector<RestSiteInjector>();
		Activate(injector);
		injector.Show(gameNode, deckAnalysis, currentHP, maxHP, actNumber, floor, gameState);
	}

	public void ShowEventAdvice(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int gold, int actNumber, int floor, string eventId)
	{
		var injector = GetInjector<EventInjector>();
		Activate(injector);
		injector.Show(gameNode, deckAnalysis, currentHP, maxHP, gold, actNumber, floor, eventId);
	}

	public void ShowCombatAdvice(Node gameNode, DeckAnalysis deckAnalysis, int currentHP, int maxHP, int actNumber, int floor, GameState gameState, List<string> enemyIds)
	{
		var injector = GetInjector<CombatInjector>();
		Activate(injector);
		injector.Show(gameNode, deckAnalysis, currentHP, maxHP, actNumber, floor, gameState, enemyIds);
	}

	public void ShowCardAdvice(Node gameNode, List<ScoredCard> cards, DeckAnalysis deckAnalysis, string character, string screenLabel = "CARD REWARD")
	{
		var injector = GetInjector<CardRewardInjector>();
		Activate(injector);
		injector.Show(gameNode, cards, deckAnalysis, character, screenLabel);
	}

	public void ShowRelicAdvice(Node gameNode, List<ScoredRelic> relics, DeckAnalysis deckAnalysis, string character)
	{
		var injector = GetInjector<RelicRewardInjector>();
		Activate(injector);
		injector.Show(gameNode, relics, deckAnalysis, character);
	}

	public void ShowShopAdvice(Node gameNode, List<ScoredCard> cards, List<ScoredRelic> relics, DeckAnalysis deckAnalysis, string character)
	{
		var injector = GetInjector<ShopInjector>();
		Activate(injector);
		injector.Show(gameNode, cards, relics, deckAnalysis, character);
	}

	public void UpdateCombatPiles(CombatSnapshot snapshot)
	{
		if (_activeInjector is CombatInjector combat)
			combat.UpdatePiles(snapshot);
	}

	public void Clear()
	{
		DeactivateCurrent();
	}

	// === Global controls ===

	public void ToggleVisible()
	{
		_visible = !_visible;
		_activeInjector?.SetVisible(_visible);
		Plugin.Log("Coordinator: overlay " + (_visible ? "shown" : "hidden"));
	}

	/// <summary>Called every frame from OverlayInputHandler.</summary>
	public void ProcessFrame(double delta)
	{
		_activeInjector?.StabilizeLayout();
		_activeInjector?.ProcessAutoFade(delta);

		// Detect if active injector's panel was freed (game node destroyed)
		if (_activeInjector != null && !_activeInjector.IsValid())
		{
			Plugin.Log($"Coordinator: active injector panel freed (game node destroyed)");
			_activeInjector = null;
		}
	}

	public void HandleInput(InputEvent ev)
	{
		if (ev is InputEventKey { Pressed: not false, Echo: false } key)
		{
			Key k = key.Keycode;
			Key pk = key.PhysicalKeycode;
			if (k == Key.F7 || pk == Key.F7)
				ToggleVisible();
			else if (key.AltPressed && (k == Key.H || pk == Key.H))
				ToggleVisible();
			else if (!key.AltPressed && !key.CtrlPressed && !key.ShiftPressed
				&& (k == Key.Quoteleft || pk == Key.Quoteleft))
				ToggleVisible();
		}
	}

	public bool IsActive => _activeInjector != null && _activeInjector.IsValid();
	public CanvasLayer UtilityLayer => _utilityLayer;
}

/// <summary>
/// Input handler for OverlayCoordinator — handles global hotkeys and per-frame updates.
/// </summary>
internal class OverlayInputHandler : Node
{
	private readonly OverlayCoordinator _coordinator;
	private readonly OverlayManager _legacyOwner;

	/// <summary>Constructor for new coordinator mode.</summary>
	public OverlayInputHandler(OverlayCoordinator coordinator)
	{
		_coordinator = coordinator;
		ProcessMode = ProcessModeEnum.Always;
	}

	/// <summary>Constructor for legacy OverlayManager mode.</summary>
	public OverlayInputHandler(OverlayManager owner)
	{
		_legacyOwner = owner;
		ProcessMode = ProcessModeEnum.Always;
	}

	private double _checkTimer;
	private double _combatTimer;

	public override void _UnhandledKeyInput(InputEvent ev)
	{
		if (ev is InputEventKey { Pressed: not false, Echo: false } key)
		{
			if (key.Keycode == Key.F10 || key.PhysicalKeycode == Key.F10)
			{
				_legacyOwner?.ToggleDebugOverlay();
				return;
			}
		}
		if (_coordinator != null)
			_coordinator.HandleInput(ev);
		else
			_legacyOwner?.HandleInput(ev);
	}

	public override void _Input(InputEvent ev)
	{
		if (ev is InputEventMouseButton || (ev is InputEventKey key && key.Keycode == Key.Escape))
			_legacyOwner?.HandleSettingsClose(ev);
	}

	public override void _Process(double delta)
	{
		if (_coordinator != null)
		{
			_coordinator.ProcessFrame(delta);
			return;
		}

		// Legacy mode
		if (_legacyOwner == null) return;
		_legacyOwner.StabilizeLayout();
		_legacyOwner.ProcessAutoFade(delta);

		_checkTimer += delta;
		if (_checkTimer >= 1.0)
		{
			_checkTimer = 0;
			_legacyOwner.CheckForStaleScreen();
		}

		_combatTimer += delta;
		if (_combatTimer >= 0.5)
		{
			_combatTimer = 0;
			_legacyOwner.UpdateCombatPiles();
		}
	}
}
