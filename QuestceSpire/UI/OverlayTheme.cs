using Godot;
using QuestceSpire.Core;

namespace QuestceSpire.UI;

/// <summary>
/// Centralized design tokens for the overlay UI.
/// All colors, font sizes, spacing, and radii are defined here.
/// Palette tuned for STS2 in-game cohesion (dark parchment fantasy).
/// </summary>
public static class OverlayTheme
{
    // ── Colors: Backgrounds ──────────────────────────────────────
    // Dark blue-black with warm undertone to match STS2 UI panels

    public static readonly Color BgPanel = new(0.035f, 0.045f, 0.085f, 0.94f);
    public static readonly Color BgEntry = new(0.06f, 0.07f, 0.12f, 0.50f);
    public static readonly Color BgEntryHover = new(0.10f, 0.11f, 0.18f, 0.70f);
    public static readonly Color BgEntryBest = new(0.831f, 0.714f, 0.357f, 0.08f);
    public static readonly Color BgEntryBestHover = new(0.831f, 0.714f, 0.357f, 0.14f);
    public static readonly Color BgEntrySTierHover = new(0.831f, 0.714f, 0.357f, 0.18f);
    public static readonly Color BgChip = new(0.02f, 0.03f, 0.07f, 0.7f);
    public static readonly Color BgThumbnail = new(0.02f, 0.02f, 0.04f, 1f);
    public static readonly Color BgSkipBadge = new(0.15f, 0.12f, 0.22f);
    public static readonly Color BgScoreBarEmpty = new(0.08f, 0.08f, 0.12f, 0.3f);
    public static readonly Color BgHeroPick = new(0.12f, 0.10f, 0.05f, 0.85f);
    public static readonly Color BgSkipZone = new(0.04f, 0.04f, 0.06f, 0.40f);
    public static readonly Color BgHpBar = new(0.08f, 0.06f, 0.04f, 0.8f);

    // ── Colors: Borders & Chrome ─────────────────────────────────
    // Warm gold border evokes STS2 card/relic frames

    public static readonly Color Border = new(0.55f, 0.44f, 0.28f);
    public static readonly Color BorderLight = new(0.65f, 0.52f, 0.34f, 0.5f);
    public static readonly Color Outline = new(0.02f, 0.02f, 0.04f);
    public static readonly Color Shadow = new(0f, 0f, 0f, 0.6f);

    // ── Colors: Text ─────────────────────────────────────────────
    // Warm cream/parchment tones for readability against dark bg

    public static readonly Color TextHeader = new(0.92f, 0.78f, 0.35f);
    public static readonly Color TextAccent = new(0.831f, 0.714f, 0.357f);
    public static readonly Color TextBody = new(0.90f, 0.86f, 0.76f);
    public static readonly Color TextSub = new(0.52f, 0.49f, 0.38f);
    public static readonly Color TextNotes = new(0.68f, 0.64f, 0.56f);
    public static readonly Color TextSkip = new(0.42f, 0.40f, 0.36f);

    // ── Colors: Semantic ─────────────────────────────────────────

    public static readonly Color Positive = new(0.3f, 0.78f, 0.4f);
    public static readonly Color Negative = new(0.88f, 0.32f, 0.28f);
    public static readonly Color Warning = new(1f, 0.6f, 0.3f);
    public static readonly Color Info = new(0.50f, 0.78f, 0.90f);
    public static readonly Color Skip = new(0.50f, 0.22f, 0.80f);
    public static readonly Color SkipSub = new(0.55f, 0.55f, 0.72f);
    public static readonly Color Hover = new(0.1f, 0.12f, 0.2f, 0.8f);

    // ── Colors: Score Bar ────────────────────────────────────────

    public static readonly Color ScoreBarS = new(1f, 0.84f, 0f, 0.85f);
    public static readonly Color ScoreBarA = new(0.25f, 0.75f, 0.35f, 0.80f);
    public static readonly Color ScoreBarB = new(0.3f, 0.65f, 0.60f, 0.70f);
    public static readonly Color ScoreBarC = new(0.45f, 0.45f, 0.45f, 0.60f);
    public static readonly Color ScoreBarD = new(0.55f, 0.40f, 0.30f, 0.55f);
    public static readonly Color ScoreBarF = new(0.75f, 0.20f, 0.20f, 0.60f);
    public static readonly Color ScoreBarBg = new(0.10f, 0.10f, 0.14f, 0.50f);

    public static Color GetScoreBarColor(TierGrade grade) => grade switch
    {
        TierGrade.S => ScoreBarS,
        TierGrade.A => ScoreBarA,
        TierGrade.B => ScoreBarB,
        TierGrade.C => ScoreBarC,
        TierGrade.D => ScoreBarD,
        TierGrade.F => ScoreBarF,
        _ => ScoreBarC
    };

    // ── Colors: HP Bar ───────────────────────────────────────────

    public static readonly Color HpFull = new(0.30f, 0.75f, 0.35f);
    public static readonly Color HpMid = new(0.90f, 0.70f, 0.20f);
    public static readonly Color HpLow = new(0.85f, 0.25f, 0.20f);
    public static readonly Color HpBg = new(0.12f, 0.08f, 0.06f, 0.85f);

    public static Color GetHpColor(float ratio) =>
        ratio > 0.6f ? HpFull : ratio > 0.3f ? HpMid : HpLow;

    // ── Colors: Card Types (canonical — use these everywhere) ────

    public static readonly Color CardAttack = new(0.88f, 0.38f, 0.30f);
    public static readonly Color CardSkill = new(0.38f, 0.62f, 0.92f);
    public static readonly Color CardPower = new(0.92f, 0.82f, 0.32f);

    public static Color GetCardTypeColor(string type) => type?.ToLowerInvariant() switch
    {
        "attack" => CardAttack,
        "skill" => CardSkill,
        "power" => CardPower,
        _ => TextBody
    };

    public static string GetCardTypeIcon(string type) => type?.ToLowerInvariant() switch
    {
        "attack" => "\u2694",    // ⚔
        "skill" => "\u26E8",    // ⛨
        "power" => "\u2726",    // ✦
        _ => "\u25C6"           // ◆
    };

    // ── Colors: Tier Grades ──────────────────────────────────────

    public static Color GetTierColor(TierGrade grade) => grade switch
    {
        TierGrade.S => new Color(1f, 0.84f, 0f),
        TierGrade.A => new Color(0.25f, 0.78f, 0.30f),
        TierGrade.B => new Color(0.30f, 0.68f, 0.62f),
        TierGrade.C => new Color(0.55f, 0.55f, 0.55f),
        TierGrade.D => new Color(0.60f, 0.44f, 0.32f),
        TierGrade.F => new Color(0.85f, 0.22f, 0.22f),
        _ => new Color(0.55f, 0.55f, 0.55f),
    };

    public static Color GetTierTextColor(TierGrade grade) => grade switch
    {
        TierGrade.S or TierGrade.A => new Color(0.05f, 0.05f, 0.05f),
        _ => new Color(0.95f, 0.95f, 0.95f),
    };

    // ── Colors: Archetype bars ───────────────────────────────────

    public static readonly Color[] ArchColors =
    {
        new(0.4f, 0.8f, 0.95f),
        new(0.95f, 0.6f, 0.3f),
        new(0.7f, 0.5f, 0.95f),
        new(0.3f, 0.9f, 0.5f),
    };

    // ── Font Sizes ───────────────────────────────────────────────

    public const int FontTitle = 18;        // App title (smaller for compact)
    public const int FontH1 = 16;           // Section headers
    public const int FontH2 = 14;           // Collapsible headers, card names
    public const int FontBody = 13;         // Body text, tooltips
    public const int FontSmall = 12;        // Settings, secondary info
    public const int FontCaption = 11;      // Pile counts, debug
    public const int FontBadgeLarge = 20;   // Single-char grade badge
    public const int FontBadgeSmall = 17;   // Multi-char grade badge (A+, B-)
    public const int FontSkipBadge = 22;    // Skip em-dash

    // Expanded view uses slightly larger fonts
    public const int FontExpandedH1 = 18;
    public const int FontExpandedH2 = 16;
    public const int FontExpandedBody = 14;

    // ── Spacing (4px grid) ───────────────────────────────────────

    public const int SpaceXS = 2;
    public const int SpaceSM = 4;
    public const int SpaceMD = 6;           // Tighter default for compact
    public const int SpaceLG = 10;
    public const int SpaceXL = 14;
    public const int SpaceXXL = 16;         // Panel margins (was 20)

    // ── Border Radii ─────────────────────────────────────────────

    public const int RadiusSM = 4;
    public const int RadiusMD = 8;
    public const int RadiusLG = 12;
    public const int RadiusPanel = 14;

    // ── Opacity Tokens ───────────────────────────────────────────

    public const float OpBorderSubtle = 0.35f;
    public const float OpBorderAccent = 0.85f;
    public const float OpBorderNormal = 0.6f;
    public const float OpShadowNormal = 0.4f;
    public const float OpShadowAccent = 0.3f;
    public const float OpScoreBarFill = 0.7f;
    public const float OpChipBorder = 0.3f;
    public const float OpChipBg = 0.12f;

    // ── Panel Sizes ──────────────────────────────────────────────

    public const float PanelWidthCompact = 210f;
    public const float PanelWidthExpanded = 360f;
    public const float ThumbnailSize = 52f;
    public const float BadgeSize = 34f;
    public const float BadgeInnerDefault = 30f;
    public const float BadgeInnerWide = 38f;
    public const float GoldIconSize = 12f;
    public const float ScoreBarHeight = 5f;
    public const float HpBarHeight = 6f;
    public const float TypeBarHeight = 3f;
    public const float GradeBadgeWidth = 36f;
}
