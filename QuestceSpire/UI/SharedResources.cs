using System;
using System.Collections.Generic;
using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI;

/// <summary>
/// Shared UI resources (fonts, icons, caches, styles) used by all screen injectors.
/// Singleton — initialized once, shared across all injectors.
/// </summary>
public class SharedResources
{
	public static SharedResources Instance { get; private set; }

	// Fonts
	public Font FontBody { get; private set; }
	public Font FontBold { get; private set; }
	public Font FontHeader { get; private set; }

	// Icons
	public Texture2D GoldIcon { get; private set; }

	// Caches
	private readonly Dictionary<string, Texture2D> _cardPortraitCache = new();
	private readonly Dictionary<string, Texture2D> _relicIconCache = new();

	// Style boxes
	public StyleBoxFlat SbPanel { get; private set; }
	public StyleBoxFlat SbEntry { get; private set; }
	public StyleBoxFlat SbHover { get; private set; }
	public StyleBoxFlat SbBest { get; private set; }
	public StyleBoxFlat SbHoverBest { get; private set; }
	public StyleBoxFlat SbSTier { get; private set; }
	public StyleBoxFlat SbSTierHover { get; private set; }
	public StyleBoxFlat SbChip { get; private set; }

	// Color aliases — delegate to OverlayTheme
	public static readonly Color ClrBg = OverlayTheme.BgPanel;
	public static readonly Color ClrBorder = OverlayTheme.Border;
	public static readonly Color ClrHeader = OverlayTheme.TextHeader;
	public static readonly Color ClrAccent = OverlayTheme.TextAccent;
	public static readonly Color ClrSub = OverlayTheme.TextSub;
	public static readonly Color ClrPositive = OverlayTheme.Positive;
	public static readonly Color ClrNegative = OverlayTheme.Negative;
	public static readonly Color ClrNotes = OverlayTheme.TextNotes;
	public static readonly Color ClrSkip = OverlayTheme.Skip;
	public static readonly Color ClrExpensive = OverlayTheme.Warning;
	public static readonly Color ClrHover = OverlayTheme.Hover;
	public static readonly Color ClrSkipSub = OverlayTheme.SkipSub;
	public static readonly Color ClrAqua = OverlayTheme.Info;
	public static readonly Color ClrOutline = OverlayTheme.Outline;
	public static readonly Color ClrCream = OverlayTheme.TextBody;
	public static readonly Color ClrBody = OverlayTheme.TextBody;
	public static readonly Color ClrInfo = OverlayTheme.Info;

	private static readonly string[] PortraitFallbackFolders = new[]
	{
		"colorless", "neutral", "shared", "common", ""
	};

	public static SharedResources Initialize()
	{
		Instance ??= new SharedResources();
		return Instance;
	}

	private SharedResources()
	{
		LoadFonts();
		LoadIcons();
		InitializeStyles();
	}

	private void LoadFonts()
	{
		try
		{
			FontBody = ResourceLoader.Load<Font>("res://fonts/kreon_regular.ttf");
			FontBold = ResourceLoader.Load<Font>("res://fonts/kreon_bold.ttf");
			FontHeader = ResourceLoader.Load<Font>("res://fonts/spectral_bold.ttf");
			Plugin.Log($"SharedResources fonts loaded: body={FontBody != null} bold={FontBold != null} header={FontHeader != null}");
		}
		catch (Exception ex)
		{
			Plugin.Log("SharedResources: Could not load game fonts: " + ex.Message);
		}
	}

	private void LoadIcons()
	{
		try
		{
			GoldIcon = ResourceLoader.Load<Texture2D>("res://images/packed/sprite_fonts/gold_icon.png");
			Plugin.Log($"SharedResources icons loaded: gold={GoldIcon != null}");
		}
		catch (Exception ex)
		{
			Plugin.Log("SharedResources: Could not load game icons: " + ex.Message);
		}
	}

	public void InitializeStyles()
	{
		SbPanel = OverlayStyles.CreatePanelStyle();
		SbEntry = OverlayStyles.CreateEntryStyle();
		SbHover = OverlayStyles.CreateEntryHoverStyle();
		SbBest = OverlayStyles.CreateBestEntryStyle();
		SbHoverBest = OverlayStyles.CreateBestEntryHoverStyle();
		SbSTier = OverlayStyles.CreateSTierStyle();
		SbSTierHover = OverlayStyles.CreateSTierHoverStyle();
		SbChip = OverlayStyles.CreateChipStyle();
	}

	public void ApplyOpacity(float opacity)
	{
		if (SbPanel != null) SbPanel.BgColor = new Color(ClrBg.R, ClrBg.G, ClrBg.B, 0.97f * opacity);
		if (SbEntry != null) SbEntry.BgColor = new Color(OverlayTheme.BgEntry, OverlayTheme.BgEntry.A * opacity);
		if (SbBest != null) SbBest.BgColor = new Color(OverlayTheme.BgEntryBest, OverlayTheme.BgEntryBest.A * opacity);
		if (SbHover != null) SbHover.BgColor = new Color(ClrHover.R, ClrHover.G, ClrHover.B, 0.8f * opacity);
		if (SbHoverBest != null) SbHoverBest.BgColor = new Color(OverlayTheme.BgEntryBestHover, OverlayTheme.BgEntryBestHover.A * opacity);
		if (SbSTier != null) SbSTier.BgColor = new Color(OverlayTheme.BgEntryBest, OverlayTheme.BgEntryBest.A * opacity);
		if (SbSTierHover != null) SbSTierHover.BgColor = new Color(OverlayTheme.BgEntrySTierHover, OverlayTheme.BgEntrySTierHover.A * opacity);
		if (SbChip != null) SbChip.BgColor = new Color(OverlayTheme.BgChip, OverlayTheme.BgChip.A * opacity);
	}

	// Font helpers
	public void ApplyFont(Label label, Font font)
	{
		if (font != null)
			label.AddThemeFontOverride("font", font);
	}

	public void StyleLabel(Label label, Font font, int fontSize, Color color)
		=> OverlayStyles.StyleLabel(label, font, fontSize, color);

	// Card portrait loading
	public Texture2D GetCardPortrait(string cardId, string character)
	{
		string key = $"{character}/{cardId}";
		if (_cardPortraitCache.TryGetValue(key, out var cached)) return cached;
		try
		{
			string fileName = cardId.Replace(" ", "_").Replace("'", "").Replace("-", "_").ToLowerInvariant();
			string charFolder = character?.ToLowerInvariant() ?? "ironclad";
			string[] basePaths = new[]
			{
				"res://images/packed/card_portraits",
				"res://images/card_portraits"
			};
			Texture2D tex = null;
			foreach (string basePath in basePaths)
			{
				tex = ResourceLoader.Load<Texture2D>($"{basePath}/{charFolder}/{fileName}.png");
				if (tex != null) break;
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

	public Texture2D GetRelicIcon(string relicId)
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

	public static Color GetScreenColor(string screen)
	{
		return screen switch
		{
			"CARD REWARD" or "CARD UPGRADE" or "CARD REMOVAL" or "EVENT CARD OFFER" => OverlayTheme.TextAccent,
			"RELIC REWARD" => OverlayTheme.Positive,
			"MERCHANT SHOP" => OverlayTheme.Warning,
			"COMBAT" => OverlayTheme.Negative,
			"EVENT" => OverlayTheme.Info,
			"MAP" => OverlayTheme.TextSub,
			"REST SITE" => OverlayTheme.Positive,
			"RUN WON!" => OverlayTheme.Positive,
			"RUN LOST" => OverlayTheme.Negative,
			_ => OverlayTheme.Border
		};
	}
}
