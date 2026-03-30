using System;
using System.IO;
using Newtonsoft.Json;

namespace QuestceSpire.Core;

public class OverlaySettings
{
	public float OffsetLeft { get; set; } = -380f;
	public float OffsetRight { get; set; } = -30f;
	public float OffsetTop { get; set; } = 30f;
	public float OffsetBottom { get; set; } = 700f;
	public bool Visible { get; set; } = true;
	public bool ShowTooltips { get; set; } = true;
	public bool ShowInGameBadges { get; set; } = true;
	public float PanelOpacity { get; set; } = 1.0f;
	public bool Collapsed { get; set; } = false;
	public bool ShowDeckBreakdown { get; set; } = true;
	public bool ShowDecisionHistory { get; set; } = false;
	public bool ShowDrawProbability { get; set; } = true;
	public bool ShowEnemyTips { get; set; } = true;
	public bool ShowEventAdvice { get; set; } = true;
	public bool ShowMapAdvice { get; set; } = true;
	public bool ShowCombatAdvice { get; set; } = true;
	public bool CloudSyncEnabled { get; set; } = false;
	public bool AutoUpdateData { get; set; } = true;

	// v0.14.1: Pipeline & feature toggles
	public bool ShowPotionAdvice { get; set; } = true;
	public bool ShowRunHealth { get; set; } = true;
	public bool ShowBossReadiness { get; set; } = true;
	public bool ShowMetaArchetypes { get; set; } = true;
	public bool EnablePipelineSync { get; set; } = true;
	public bool ShowPatchChanges { get; set; } = true;
	public bool ShowFloorTierInfo { get; set; } = true;
	public bool ShowCoPickSynergy { get; set; } = true;
	public bool ShowRunSummary { get; set; } = true;

	// v0.16: Welcome & debug
	[JsonProperty("hasSeenWelcome")]
	public bool HasSeenWelcome { get; set; }

	[JsonProperty("debugLogging")]
	public bool DebugLogging { get; set; }

	[JsonProperty("hasSeenCloudNotice")]
	public bool HasSeenCloudNotice { get; set; }

	// v0.15: Auto-fade — panel fades to idle opacity when mouse leaves
	public bool AutoFadeEnabled { get; set; } = true;
	public float IdleOpacity { get; set; } = 0.35f;
	public float IdleDelaySeconds { get; set; } = 1.5f;

	// Bump this when defaults change to force migration on old saved files
	public int SettingsVersion { get; set; } = 0;
	private const int CurrentVersion = 7;

	private static string GetSettingsPath()
	{
		return Path.Combine(Plugin.PluginFolder, "overlay_settings.json");
	}

	public static OverlaySettings Load()
	{
		try
		{
			string path = GetSettingsPath();
			if (File.Exists(path))
			{
				string json = File.ReadAllText(path);
				var settings = JsonConvert.DeserializeObject<OverlaySettings>(json);
				if (settings != null)
				{
					if (settings.SettingsVersion < CurrentVersion)
					{
						Plugin.Log($"Migrating settings from v{settings.SettingsVersion} to v{CurrentVersion}");
						// v0-1 → v2: ShowDecisionHistory forced off
						if (settings.SettingsVersion < 2)
							settings.ShowDecisionHistory = false;
						// v2 → v3: CloudSyncEnabled defaults to true (no action needed)
						// v3 → v4: CloudSyncEnabled disabled (no server deployed yet)
						if (settings.SettingsVersion < 4)
							settings.CloudSyncEnabled = false;
						// v4 → v5: AutoUpdateData enabled by default
						if (settings.SettingsVersion < 5)
							settings.AutoUpdateData = true;
						// v5 → v6: New pipeline/feature toggles
						if (settings.SettingsVersion < 6)
						{
							settings.ShowPotionAdvice = true;
							settings.ShowRunHealth = true;
							settings.ShowBossReadiness = true;
							settings.ShowMetaArchetypes = true;
							settings.EnablePipelineSync = true;
							settings.ShowPatchChanges = true;
							settings.ShowFloorTierInfo = true;
							settings.ShowCoPickSynergy = true;
							settings.ShowRunSummary = true;
						}
						// v6 → v7: Auto-fade settings
						if (settings.SettingsVersion < 7)
						{
							settings.AutoFadeEnabled = true;
							settings.IdleOpacity = 0.35f;
							settings.IdleDelaySeconds = 1.5f;
						}
						settings.SettingsVersion = CurrentVersion;
						settings.Save();
					}
					Plugin.Log("Overlay settings loaded.");
					return settings;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log("Failed to load overlay settings: " + ex.Message);
		}
		var defaults = new OverlaySettings { SettingsVersion = CurrentVersion };
		defaults.Save();
		return defaults;
	}

	public void Save()
	{
		try
		{
			string path = GetSettingsPath();
			string json = JsonConvert.SerializeObject(this, Formatting.Indented);
			File.WriteAllText(path, json);
		}
		catch (Exception ex)
		{
			Plugin.Log("Failed to save overlay settings: " + ex.Message);
		}
	}
}
