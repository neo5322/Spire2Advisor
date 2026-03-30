using System;
using Godot;
using QuestceSpire.Core;
using QuestceSpire.Tracking;

namespace QuestceSpire.UI;

/// <summary>
/// Standalone settings menu panel — attaches to Coordinator utility layer.
/// Replaces OverlayManager.Settings.cs.
/// </summary>
public class SettingsPanel
{
	private PanelContainer _menu;
	private readonly OverlaySettings _settings;
	private readonly OverlayCoordinator _coordinator;

	private SharedResources Res => SharedResources.Instance;

	private static readonly float[] OpacitySteps = { 1.0f, 0.75f, 0.50f };

	public SettingsPanel(OverlaySettings settings, OverlayCoordinator coordinator)
	{
		_settings = settings;
		_coordinator = coordinator;
	}

	public bool IsVisible => _menu != null && GodotObject.IsInstanceValid(_menu) && _menu.Visible;

	public void Toggle()
	{
		if (_menu == null || !GodotObject.IsInstanceValid(_menu))
		{
			Build();
		}
		_menu.Visible = !_menu.Visible;
	}

	public void Hide()
	{
		if (_menu != null && GodotObject.IsInstanceValid(_menu))
			_menu.Visible = false;
	}

	public void Destroy()
	{
		if (_menu != null && GodotObject.IsInstanceValid(_menu))
		{
			_menu.GetParent()?.RemoveChild(_menu);
			_menu.QueueFree();
			_menu = null;
		}
	}

	private void Build()
	{
		if (_menu != null && GodotObject.IsInstanceValid(_menu))
		{
			_menu.GetParent()?.RemoveChild(_menu);
			_menu.QueueFree();
		}

		_menu = new PanelContainer();
		_menu.AnchorLeft = 1f;
		_menu.AnchorRight = 1f;
		_menu.AnchorTop = 0f;
		_menu.AnchorBottom = 0f;
		_menu.OffsetLeft = -250;
		_menu.OffsetRight = -30;
		_menu.OffsetTop = 40;
		_menu.ResetSize();
		_menu.Visible = false;
		_menu.ZIndex = 102;
		_menu.MouseFilter = Control.MouseFilterEnum.Stop;

		StyleBoxFlat menuStyle = new StyleBoxFlat();
		menuStyle.BgColor = OverlayTheme.BgPanel;
		OverlayStyles.SetAllBorderWidth(menuStyle, 2);
		menuStyle.BorderColor = OverlayTheme.Border;
		OverlayStyles.SetAllCornerRadius(menuStyle, OverlayTheme.RadiusSM);
		menuStyle.ContentMarginLeft = menuStyle.ContentMarginRight = OverlayTheme.SpaceLG;
		menuStyle.ContentMarginTop = menuStyle.ContentMarginBottom = OverlayTheme.SpaceMD;
		_menu.AddThemeStyleboxOverride("panel", menuStyle);

		VBoxContainer vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", OverlayTheme.SpaceSM);
		_menu.AddChild(vbox, forceReadableName: false, Node.InternalMode.Disabled);

		// Header row with close button
		HBoxContainer headerRow = new HBoxContainer();
		headerRow.MouseFilter = Control.MouseFilterEnum.Ignore;
		Label header = new Label();
		header.Text = "설정";
		Res.ApplyFont(header, Res.FontBold);
		header.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		header.AddThemeColorOverride("font_color", SharedResources.ClrHeader);
		header.MouseFilter = Control.MouseFilterEnum.Ignore;
		header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headerRow.AddChild(header, forceReadableName: false, Node.InternalMode.Disabled);

		Label closeBtn = new Label();
		closeBtn.Text = "[X]";
		Res.ApplyFont(closeBtn, Res.FontBold);
		closeBtn.AddThemeFontSizeOverride("font_size", OverlayTheme.FontBody);
		closeBtn.AddThemeColorOverride("font_color", SharedResources.ClrSub);
		closeBtn.MouseFilter = Control.MouseFilterEnum.Stop;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				_menu.Visible = false;
		}));
		headerRow.AddChild(closeBtn, forceReadableName: false, Node.InternalMode.Disabled);
		vbox.AddChild(headerRow, forceReadableName: false, Node.InternalMode.Disabled);

		AddSeparator(vbox);

		// ── 표시 (Display) ──
		AddGroupHeader(vbox, "표시");
		AddToggle(vbox, "인게임 뱃지", _settings.ShowInGameBadges, () => {
			_settings.ShowInGameBadges = !_settings.ShowInGameBadges;
			_settings.Save();
			Plugin.BadgeManager?.SetShowBadges(_settings.ShowInGameBadges);
			Refresh();
		});
		AddToggle(vbox, "결정 기록", _settings.ShowDecisionHistory, () => {
			_settings.ShowDecisionHistory = !_settings.ShowDecisionHistory;
			_settings.Save();
			RebuildActive();
			Refresh();
		});
		AddToggle(vbox, "덱 구성 표시", _settings.ShowDeckBreakdown, () => {
			_settings.ShowDeckBreakdown = !_settings.ShowDeckBreakdown;
			_settings.Save();
			RebuildActive();
			Refresh();
		});
		AddToggle(vbox, "자동 페이드", _settings.AutoFadeEnabled, () => {
			_settings.AutoFadeEnabled = !_settings.AutoFadeEnabled;
			_settings.Save();
			Refresh();
		});

		// ── 조언 (Advice) ──
		AddGroupHeader(vbox, "조언");
		AddToggle(vbox, "적 팁", _settings.ShowEnemyTips, () => { _settings.ShowEnemyTips = !_settings.ShowEnemyTips; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "이벤트 조언", _settings.ShowEventAdvice, () => { _settings.ShowEventAdvice = !_settings.ShowEventAdvice; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "지도 조언", _settings.ShowMapAdvice, () => { _settings.ShowMapAdvice = !_settings.ShowMapAdvice; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "전투 조언", _settings.ShowCombatAdvice, () => { _settings.ShowCombatAdvice = !_settings.ShowCombatAdvice; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "포션 조언", _settings.ShowPotionAdvice, () => { _settings.ShowPotionAdvice = !_settings.ShowPotionAdvice; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "보스 대비", _settings.ShowBossReadiness, () => { _settings.ShowBossReadiness = !_settings.ShowBossReadiness; _settings.Save(); RebuildActive(); Refresh(); });

		// ── 데이터 (Data) ──
		AddGroupHeader(vbox, "데이터");
		AddToggle(vbox, "클라우드 동기화", _settings.CloudSyncEnabled, () => {
			if (!_settings.CloudSyncEnabled && !_settings.HasSeenCloudNotice)
				_settings.HasSeenCloudNotice = true;
			_settings.CloudSyncEnabled = !_settings.CloudSyncEnabled;
			_settings.Save();
			Refresh();
		});
		if (_settings.CloudSyncEnabled)
		{
			Label privacyNote = new Label();
			privacyNote.Text = "  ℹ 전송 데이터: 런 결과, 카드 선택, 승률 (개인 식별 정보 없음)";
			Res.ApplyFont(privacyNote, Res.FontBody);
			privacyNote.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			privacyNote.AddThemeColorOverride("font_color", SharedResources.ClrSub);
			privacyNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			vbox.AddChild(privacyNote, forceReadableName: false, Node.InternalMode.Disabled);
		}
		AddToggle(vbox, "데이터 자동 업데이트", _settings.AutoUpdateData, () => { _settings.AutoUpdateData = !_settings.AutoUpdateData; _settings.Save(); Refresh(); });
		AddToggle(vbox, "파이프라인 동기화", _settings.EnablePipelineSync, () => { _settings.EnablePipelineSync = !_settings.EnablePipelineSync; _settings.Save(); Refresh(); });

		// ── 고급 (Advanced) ──
		AddGroupHeader(vbox, "고급");
		AddToggle(vbox, "패치 변경 표시", _settings.ShowPatchChanges, () => { _settings.ShowPatchChanges = !_settings.ShowPatchChanges; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "층별 티어 정보", _settings.ShowFloorTierInfo, () => { _settings.ShowFloorTierInfo = !_settings.ShowFloorTierInfo; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "카드 조합 시너지", _settings.ShowCoPickSynergy, () => { _settings.ShowCoPickSynergy = !_settings.ShowCoPickSynergy; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "메타 아키타입", _settings.ShowMetaArchetypes, () => { _settings.ShowMetaArchetypes = !_settings.ShowMetaArchetypes; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "런 건강도", _settings.ShowRunHealth, () => { _settings.ShowRunHealth = !_settings.ShowRunHealth; _settings.Save(); RebuildActive(); Refresh(); });
		AddToggle(vbox, "런 요약", _settings.ShowRunSummary, () => { _settings.ShowRunSummary = !_settings.ShowRunSummary; _settings.Save(); Refresh(); });
		AddToggle(vbox, "디버그 로깅", _settings.DebugLogging, () => { _settings.DebugLogging = !_settings.DebugLogging; _settings.Save(); Refresh(); });

		// Opacity
		AddSeparator(vbox);
		HBoxContainer opacityRow = new HBoxContainer();
		opacityRow.MouseFilter = Control.MouseFilterEnum.Ignore;
		Label opLabel = new Label();
		opLabel.Text = "투명도:";
		Res.ApplyFont(opLabel, Res.FontBody);
		opLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		opLabel.AddThemeColorOverride("font_color", SharedResources.ClrBody);
		opLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		opLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		opacityRow.AddChild(opLabel, forceReadableName: false, Node.InternalMode.Disabled);

		foreach (float step in OpacitySteps)
		{
			Label stepBtn = new Label();
			int pct = (int)(step * 100);
			bool isActive = Math.Abs(_settings.PanelOpacity - step) < 0.01f;
			stepBtn.Text = $" {pct}% ";
			Res.ApplyFont(stepBtn, Res.FontBold);
			stepBtn.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
			stepBtn.AddThemeColorOverride("font_color", isActive ? SharedResources.ClrHeader : SharedResources.ClrSub);
			stepBtn.MouseFilter = Control.MouseFilterEnum.Stop;
			stepBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			float capturedStep = step;
			stepBtn.Connect("gui_input", Callable.From((InputEvent ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					_settings.PanelOpacity = capturedStep;
					_settings.Save();
					Refresh();
				}
			}));
			opacityRow.AddChild(stepBtn, forceReadableName: false, Node.InternalMode.Disabled);
		}
		vbox.AddChild(opacityRow, forceReadableName: false, Node.InternalMode.Disabled);

		// Stats export/import
		AddSeparator(vbox);
		AddActionButton(vbox, "통계 내보내기", SharedResources.ClrInfo, (btn) => {
			try
			{
				var (path, cards, relics) = StatsExporter.ExportToFile(Plugin.RunDatabase, Plugin.PluginFolder);
				Plugin.Log($"Stats exported: {cards} cards, {relics} relics → {path}");
				btn.Text = $"내보내기 완료! ({cards}카드, {relics}유물)";
				btn.AddThemeColorOverride("font_color", SharedResources.ClrPositive);
			}
			catch (Exception ex)
			{
				Plugin.Log($"Export failed: {ex.Message}");
				btn.Text = "내보내기 실패!";
				btn.AddThemeColorOverride("font_color", SharedResources.ClrNegative);
			}
		});
		AddActionButton(vbox, "통계 가져오기", SharedResources.ClrInfo, (btn) => {
			try
			{
				string importPath = StatsExporter.FindImportFile(Plugin.PluginFolder);
				if (importPath == null)
				{
					btn.Text = "questcespire_stats_export.json을 Downloads에 넣어주세요";
					btn.AddThemeColorOverride("font_color", SharedResources.ClrSub);
					return;
				}
				var (cards, relics) = StatsExporter.ImportFromFile(Plugin.RunDatabase, importPath);
				if (cards == -1)
				{
					btn.Text = "파일을 읽을 수 없습니다";
					btn.AddThemeColorOverride("font_color", SharedResources.ClrSub);
				}
				else
				{
					string fileName = System.IO.Path.GetFileName(importPath);
					Plugin.Log($"Stats imported from {fileName}: {cards} cards, {relics} relics");
					btn.Text = $"가져오기 완료: {fileName} ({cards}카드/{relics}유물)";
					btn.AddThemeColorOverride("font_color", SharedResources.ClrPositive);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log($"Import failed: {ex.Message}");
				btn.Text = "가져오기 실패!";
				btn.AddThemeColorOverride("font_color", SharedResources.ClrNegative);
			}
		});
		AddActionButton(vbox, "로그 파일 열기", SharedResources.ClrInfo, (_) => {
			try
			{
				string logPath = System.IO.Path.Combine(Plugin.PluginFolder ?? "", "spire-advisor.log");
				if (System.IO.File.Exists(logPath))
					OS.ShellOpen(logPath);
			}
			catch (Exception ex) { Plugin.Log($"Failed to open log file: {ex.Message}"); }
		});

		// Hide overlay
		AddSeparator(vbox);
		AddActionButton(vbox, "오버레이 접기", SharedResources.ClrSub, (_) => {
			_menu.Visible = false;
			_coordinator.ToggleVisible();
		});

		// First-run welcome
		if (_settings.SettingsVersion <= 7 && !_settings.HasSeenWelcome)
		{
			_settings.HasSeenWelcome = true;
			_settings.Save();
			AddSeparator(vbox);
			Label welcomeLabel = new Label();
			welcomeLabel.Text = "🎴 Spire Advisor에 오신 것을 환영합니다!\n• 카드/유물 추천이 실시간 표시됩니다\n• ⚙ 버튼으로 설정을 조정하세요\n• 타이틀 바를 드래그하여 이동\n• F10: 디버그 정보";
			Res.ApplyFont(welcomeLabel, Res.FontBody);
			welcomeLabel.AddThemeFontSizeOverride("font_size", OverlayTheme.FontCaption);
			welcomeLabel.AddThemeColorOverride("font_color", SharedResources.ClrInfo);
			welcomeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			vbox.AddChild(welcomeLabel, forceReadableName: false, Node.InternalMode.Disabled);
		}

		// Add to utility layer
		var utilityLayer = _coordinator.UtilityLayer;
		utilityLayer?.AddChild(_menu, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void Refresh()
	{
		bool wasVisible = IsVisible;
		Destroy();
		Build();
		if (wasVisible)
			_menu.Visible = true;
	}

	private void RebuildActive()
	{
		_coordinator.RebuildActiveInjector();
	}

	private void AddSeparator(VBoxContainer parent)
	{
		HSeparator sep = new HSeparator();
		sep.AddThemeStyleboxOverride("separator", OverlayStyles.CreateSeparatorStyle());
		parent.AddChild(sep, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddGroupHeader(VBoxContainer parent, string text)
	{
		AddSeparator(parent);
		Label header = new Label();
		header.Text = text;
		Res.ApplyFont(header, Res.FontBold);
		header.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		header.AddThemeColorOverride("font_color", SharedResources.ClrAccent);
		parent.AddChild(header, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddToggle(VBoxContainer parent, string label, bool currentValue, Action onToggle)
	{
		HBoxContainer row = new HBoxContainer();
		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		Label checkmark = new Label();
		checkmark.Text = currentValue ? "\u2611" : "\u2610";
		Res.ApplyFont(checkmark, Res.FontBody);
		checkmark.AddThemeFontSizeOverride("font_size", OverlayTheme.FontH2);
		checkmark.AddThemeColorOverride("font_color", currentValue ? SharedResources.ClrPositive : SharedResources.ClrSub);
		checkmark.MouseFilter = Control.MouseFilterEnum.Ignore;
		row.AddChild(checkmark, forceReadableName: false, Node.InternalMode.Disabled);

		Label text = new Label();
		text.Text = $" {label}";
		Res.ApplyFont(text, Res.FontBody);
		text.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		text.AddThemeColorOverride("font_color", SharedResources.ClrBody);
		text.MouseFilter = Control.MouseFilterEnum.Ignore;
		text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		row.AddChild(text, forceReadableName: false, Node.InternalMode.Disabled);

		row.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onToggle();
		}));

		parent.AddChild(row, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private void AddActionButton(VBoxContainer parent, string text, Color color, Action<Label> onClick)
	{
		Label btn = new Label();
		btn.Text = text;
		Res.ApplyFont(btn, Res.FontBody);
		btn.AddThemeFontSizeOverride("font_size", OverlayTheme.FontSmall);
		btn.AddThemeColorOverride("font_color", color);
		btn.MouseFilter = Control.MouseFilterEnum.Stop;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.Connect("gui_input", Callable.From((InputEvent ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				onClick(btn);
		}));
		parent.AddChild(btn, forceReadableName: false, Node.InternalMode.Disabled);
	}
}
