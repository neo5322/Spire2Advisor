using System;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

/// <summary>
/// Settings menu UI — BuildSettingsMenu, toggle handlers, positioning.
/// </summary>
public partial class OverlayManager
{
	private void BuildSettingsMenu()
	{
		_settingsMenu = new PanelContainer();
		_settingsMenu.AnchorLeft = 1f;
		_settingsMenu.AnchorRight = 1f;
		_settingsMenu.AnchorTop = 0f;
		_settingsMenu.AnchorBottom = 0f;
		_settingsMenu.Visible = false;
		_settingsMenu.ZIndex = 102;
		_settingsMenu.MouseFilter = Control.MouseFilterEnum.Stop;

		StyleBoxFlat menuStyle = new StyleBoxFlat();
		menuStyle.BgColor = new Color(0.03f, 0.05f, 0.1f, 0.97f);
		menuStyle.BorderWidthTop = menuStyle.BorderWidthBottom = menuStyle.BorderWidthLeft = menuStyle.BorderWidthRight = 2;
		menuStyle.BorderColor = ClrBorder;
		menuStyle.CornerRadiusTopLeft = menuStyle.CornerRadiusTopRight = menuStyle.CornerRadiusBottomLeft = menuStyle.CornerRadiusBottomRight = 6;
		menuStyle.ContentMarginLeft = menuStyle.ContentMarginRight = 12;
		menuStyle.ContentMarginTop = menuStyle.ContentMarginBottom = 8;
		_settingsMenu.AddThemeStyleboxOverride("panel", menuStyle);

		VBoxContainer menuVBox = new VBoxContainer();
		menuVBox.AddThemeConstantOverride("separation", 4);
		_settingsMenu.AddChild(menuVBox, forceReadableName: false, Node.InternalMode.Disabled);

		// Header row with close button
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.MouseFilter = Control.MouseFilterEnum.Ignore;
		Label header = new Label();
		header.Text = "설정";
		ApplyFont(header, _fontBold);
		header.AddThemeFontSizeOverride("font_size", 14);
		header.AddThemeColorOverride("font_color", ClrHeader);
		header.MouseFilter = Control.MouseFilterEnum.Ignore;
		header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerRow.AddChild(header, forceReadableName: false, Node.InternalMode.Disabled);

		Label closeBtn = new Label();
		closeBtn.Text = "[X]";
		ApplyFont(closeBtn, _fontBold);
		closeBtn.AddThemeFontSizeOverride("font_size", 14);
		closeBtn.AddThemeColorOverride("font_color", ClrSub);
		closeBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.GuiInput += (InputEvent ev2) =>
		{
			if (ev2 is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
				_settingsMenu.Visible = false;
		};
		headerRow.AddChild(closeBtn, forceReadableName: false, Node.InternalMode.Disabled);
		menuVBox.AddChild(headerRow, forceReadableName: false, Node.InternalMode.Disabled);

		HSeparator sep = new HSeparator();
		sep.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.4f), Thickness = 1 });
		menuVBox.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);

		// Menu items
		AddSettingsToggle(menuVBox, "인게임 뱃지", _showInGameBadges, () => { ToggleInGameBadges(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "결정 기록", _showHistory, () => { ToggleHistory(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "덱 구성 표시", _showDeckBreakdown, () => { _showDeckBreakdown = !_showDeckBreakdown; _settings.ShowDeckBreakdown = _showDeckBreakdown; _settings.Save(); Rebuild(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "적 팁", _settings.ShowEnemyTips, () => { _settings.ShowEnemyTips = !_settings.ShowEnemyTips; _settings.Save(); RegenerateAdvice(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "이벤트 조언", _settings.ShowEventAdvice, () => { _settings.ShowEventAdvice = !_settings.ShowEventAdvice; _settings.Save(); RegenerateAdvice(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "지도 조언", _settings.ShowMapAdvice, () => { _settings.ShowMapAdvice = !_settings.ShowMapAdvice; _settings.Save(); RegenerateAdvice(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "전투 조언", _settings.ShowCombatAdvice, () => { _settings.ShowCombatAdvice = !_settings.ShowCombatAdvice; _settings.Save(); RegenerateAdvice(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "전투 파일 추적", _showCombatPiles, () => { _showCombatPiles = !_showCombatPiles; if (!_showCombatPiles && _combatPileContainer != null) { _combatPileContainer.GetParent()?.RemoveChild(_combatPileContainer); _combatPileContainer.QueueFree(); _combatPileContainer = null; } RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "클라우드 동기화", _settings.CloudSyncEnabled, () => { _settings.CloudSyncEnabled = !_settings.CloudSyncEnabled; _settings.Save(); RefreshSettingsMenu(); });
		AddSettingsToggle(menuVBox, "데이터 자동 업데이트", _settings.AutoUpdateData, () => { _settings.AutoUpdateData = !_settings.AutoUpdateData; _settings.Save(); RefreshSettingsMenu(); });

		// Opacity section
		HSeparator sep2 = new HSeparator();
		sep2.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.3f), Thickness = 1 });
		menuVBox.AddChild(sep2, forceReadableName: false, Node.InternalMode.Disabled);

		HBoxContainer opacityRow = new HBoxContainer();
		opacityRow.MouseFilter = Control.MouseFilterEnum.Ignore;
		Label opLabel = new Label();
		opLabel.Text = "투명도:";
		ApplyFont(opLabel, _fontBody);
		opLabel.AddThemeFontSizeOverride("font_size", 13);
		opLabel.AddThemeColorOverride("font_color", ClrCream);
		opLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		opLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		opacityRow.AddChild(opLabel, forceReadableName: false, Node.InternalMode.Disabled);

		foreach (float step in OpacitySteps)
		{
			Label stepBtn = new Label();
			int pct = (int)(step * 100);
			bool isActive = Math.Abs(_panelOpacity - step) < 0.01f;
			stepBtn.Text = $" {pct}% ";
			ApplyFont(stepBtn, _fontBold);
			stepBtn.AddThemeFontSizeOverride("font_size", 13);
			stepBtn.AddThemeColorOverride("font_color", isActive ? ClrHeader : ClrSub);
			stepBtn.MouseFilter = Control.MouseFilterEnum.Stop;
			stepBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			float capturedStep = step;
			stepBtn.GuiInput += (InputEvent ev2) =>
			{
				if (ev2 is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
				{
					_panelOpacity = capturedStep;
					_opacityIndex = Array.IndexOf(OpacitySteps, capturedStep);
					ApplyOpacity(_panelOpacity);
					_settings.PanelOpacity = _panelOpacity;
					_settings.Save();
					RefreshSettingsMenu();
				}
			};
			opacityRow.AddChild(stepBtn, forceReadableName: false, Node.InternalMode.Disabled);
		}
		menuVBox.AddChild(opacityRow, forceReadableName: false, Node.InternalMode.Disabled);

		// Community stats export/import
		HSeparator sepStats = new HSeparator();
		sepStats.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.3f), Thickness = 1 });
		menuVBox.AddChild(sepStats, forceReadableName: false, Node.InternalMode.Disabled);

		Label exportBtn = new Label();
		exportBtn.Text = "통계 내보내기";
		ApplyFont(exportBtn, _fontBody);
		exportBtn.AddThemeFontSizeOverride("font_size", 13);
		exportBtn.AddThemeColorOverride("font_color", ClrAqua);
		exportBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		exportBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		exportBtn.GuiInput += (InputEvent ev2) =>
		{
			if (ev2 is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			{
				try
				{
					var (path, cards, relics) = StatsExporter.ExportToFile(Plugin.RunDatabase, Plugin.PluginFolder);
					Plugin.Log($"Stats exported: {cards} cards, {relics} relics → {path}");
					exportBtn.Text = $"내보내기 완료! ({cards}카드, {relics}유물)";
					exportBtn.AddThemeColorOverride("font_color", ClrPositive);
				}
				catch (Exception ex)
				{
					Plugin.Log($"Export failed: {ex.Message}");
					exportBtn.Text = "내보내기 실패!";
					exportBtn.AddThemeColorOverride("font_color", ClrNegative);
				}
			}
		};
		menuVBox.AddChild(exportBtn, forceReadableName: false, Node.InternalMode.Disabled);

		Label importBtn = new Label();
		importBtn.Text = "통계 가져오기";
		ApplyFont(importBtn, _fontBody);
		importBtn.AddThemeFontSizeOverride("font_size", 13);
		importBtn.AddThemeColorOverride("font_color", ClrAqua);
		importBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		importBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		importBtn.GuiInput += (InputEvent ev2) =>
		{
			if (ev2 is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			{
				try
				{
					string importPath = StatsExporter.FindImportFile(Plugin.PluginFolder);
					if (importPath == null)
					{
						importBtn.Text = "questcespire_stats_export.json을 Downloads에 넣어주세요";
						importBtn.AddThemeColorOverride("font_color", ClrSub);
						return;
					}
					var (cards, relics) = StatsExporter.ImportFromFile(Plugin.RunDatabase, importPath);
					if (cards == -1)
					{
						importBtn.Text = "파일을 읽을 수 없습니다";
						importBtn.AddThemeColorOverride("font_color", ClrSub);
					}
					else
					{
						string fileName = System.IO.Path.GetFileName(importPath);
						Plugin.Log($"Stats imported from {fileName}: {cards} cards, {relics} relics");
						importBtn.Text = $"가져오기 완료: {fileName} ({cards}카드/{relics}유물)";
						importBtn.AddThemeColorOverride("font_color", ClrPositive);
					}
				}
				catch (Exception ex)
				{
					Plugin.Log($"Import failed: {ex.Message}");
					importBtn.Text = "가져오기 실패!";
					importBtn.AddThemeColorOverride("font_color", ClrNegative);
				}
			}
		};
		menuVBox.AddChild(importBtn, forceReadableName: false, Node.InternalMode.Disabled);

		// Hide overlay option
		HSeparator sep3 = new HSeparator();
		sep3.AddThemeStyleboxOverride("separator", new StyleBoxLine { Color = new Color(ClrBorder, 0.3f), Thickness = 1 });
		menuVBox.AddChild(sep3, forceReadableName: false, Node.InternalMode.Disabled);

		Label hideBtn = new Label();
		hideBtn.Text = "오버레이 접기";
		ApplyFont(hideBtn, _fontBody);
		hideBtn.AddThemeFontSizeOverride("font_size", 13);
		hideBtn.AddThemeColorOverride("font_color", ClrSub);
		hideBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		hideBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		hideBtn.GuiInput += (InputEvent ev2) =>
		{
			if (ev2 is InputEventMouseButton mb2 && mb2.Pressed && mb2.ButtonIndex == MouseButton.Left)
			{
				_settingsMenu.Visible = false;
				if (!_collapsed) ToggleCollapsed();
			}
		};
		menuVBox.AddChild(hideBtn, forceReadableName: false, Node.InternalMode.Disabled);

		_layer.AddChild(_settingsMenu, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddSettingsToggle(VBoxContainer parent, string label, bool currentValue, Action onToggle)
	{
		HBoxContainer row = new HBoxContainer();
		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		Label checkmark = new Label();
		checkmark.Text = currentValue ? "\u2611" : "\u2610";
		ApplyFont(checkmark, _fontBody);
		checkmark.AddThemeFontSizeOverride("font_size", 15);
		checkmark.AddThemeColorOverride("font_color", currentValue ? ClrPositive : ClrSub);
		checkmark.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(checkmark, forceReadableName: false, Node.InternalMode.Disabled);

		Label text = new Label();
		text.Text = $" {label}";
		ApplyFont(text, _fontBody);
		text.AddThemeFontSizeOverride("font_size", 13);
		text.AddThemeColorOverride("font_color", ClrCream);
		text.MouseFilter = Control.MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(text, forceReadableName: false, Node.InternalMode.Disabled);

		row.GuiInput += (InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				onToggle();
			}
		};

		parent.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void ToggleSettingsMenu()
	{
		if (_settingsMenu == null || !GodotObject.IsInstanceValid(_settingsMenu))
			return;
		_settingsMenu.Visible = !_settingsMenu.Visible;
		if (_settingsMenu.Visible)
		{
			PositionSettingsMenu();
		}
	}

	private void PositionSettingsMenu()
	{
		if (_settingsMenu == null || _panel == null) return;
		// Position below the gear button, aligned to right edge of panel
		float panelRight = _panel.OffsetRight;
		float panelTop = _panel.OffsetTop;
		_settingsMenu.OffsetLeft = panelRight - 220;
		_settingsMenu.OffsetRight = panelRight;
		_settingsMenu.OffsetTop = panelTop + 40;
		_settingsMenu.OffsetBottom = panelTop + 300;
		_settingsMenu.ResetSize();
	}

	private void RefreshSettingsMenu()
	{
		// Rebuild the menu to reflect new toggle states
		if (_settingsMenu != null && GodotObject.IsInstanceValid(_settingsMenu))
		{
			bool wasVisible = _settingsMenu.Visible;
			SafeDisconnectSignals(_settingsMenu, recursive: true);
			_settingsMenu.QueueFree();
			_settingsMenu = null;
			BuildSettingsMenu();
			if (wasVisible)
			{
				_settingsMenu.Visible = true;
				PositionSettingsMenu();
			}
		}
	}
}
