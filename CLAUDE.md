# Spire Advisor — Developer Guide

Slay the Spire 2 인게임 오버레이 모드. 실시간 카드/유물 추천, 덱 분석, 전투 파일 추적 제공.

## Tech Stack

- **Runtime**: .NET 9, Godot 4.5.1 Mono, Harmony (patching)
- **Language**: C# (latest, nullable enabled, implicit usings)
- **Dependencies**: Newtonsoft.Json, Microsoft.Data.Sqlite
- **Game DLLs**: 0Harmony.dll, GodotSharp.dll, sts2.dll (from game install)

## Project Layout

```
QuestceSpire/                 # Main C# project
├── Plugin.cs                 # Entry point — static service locator
├── GamePatches.cs              # Harmony patches — entry point + utilities
├── GamePatches.CardReward.cs   # Card reward screen hooks
├── GamePatches.CombatHooks.cs  # Combat damage/block/card play hooks
├── GamePatches.ScreenHooks.cs  # Screen navigation hooks (shop, map, rest, event)
├── Core/                     # Business logic (scoring, tiers, analysis)
│   ├── SynergyScorer.cs      # Card/relic synergy scoring (ICardScorer, IRelicScorer)
│   ├── TierEngine.cs         # Loads tier data from Data/
│   ├── DeckAnalyzer.cs       # Deck composition analysis
│   ├── AdaptiveScorer.cs     # Community data integration
│   ├── BossAdvisor.cs        # Boss matchup analysis
│   ├── CardPropertyScorer.cs # Card property evaluation
│   ├── EventAdvisor.cs       # Event choice recommendations
│   ├── EnemyAdvisor.cs       # Enemy threat assessment
│   ├── RunSummary.cs         # Post-run analysis engine (combat stats, card usage, community comparison)
│   ├── CharacterCardTiers.cs   # Tier file model (with patchVersion metadata)
│   ├── RelicTierFile.cs        # Relic tier file model
│   ├── RelicTierEntry.cs       # Relic tier entry
│   ├── CardTierEntry.cs        # Card tier entry
│   ├── TierGrade.cs            # Tier grade enum
│   ├── ArchetypeDefinitions.cs # Archetype loading & definitions
│   ├── PotionAdvisor.cs        # Potion usage recommendations
│   ├── OverlaySettings.cs      # Settings persistence (JSON, version-migrated)
│   └── ...data classes       # ScoredCard, ScoredRelic, ArchetypeMatch, etc.
├── GameBridge/               # Game API abstraction
│   ├── GameStateReader.cs    # Extracts game state (deck, hand, relics)
│   └── CombatTracker.cs      # Combat-specific state tracking
├── Tracking/                 # Persistence & statistics
│   ├── RunDatabase.cs        # SQLite run history core (262 lines)
│   ├── RunDatabase.Pipelines.cs  # Pipeline DB tables & queries (500 lines)
│   ├── RunTracker.cs         # Current run tracking
│   ├── LocalStatsComputer.cs # Win rate, card/relic usage stats
│   ├── CloudSync.cs          # Community statistics sync
│   ├── DataUpdater.cs        # Automatic data file updates (219 lines)
│   ├── GameDataImporter.cs   # Imports game history
│   ├── PipelineHttp.cs       # Shared HTTP client with rate-limit + retry
│   ├── IDataPipeline.cs      # Common pipeline interface with status tracking
│   ├── PipelineOrchestrator.cs  # Dependency-ordered pipeline runner
│   ├── RelicCardCrossRef.cs  # Relic-card co-occurrence win-rate pipeline + relic_card_cross DB table
│   ├── RuntimeCardExtractor.cs  # sts2.dll reflection for auto-detecting new cards/relics
│   ├── OfflineDataManager.cs # Disk cache, file verification, fallback loading, 30-day cleanup
│   ├── SpireCodexSync.cs     # Spire Codex API sync (cards/relics/potions)
│   ├── PatchNotesTracker.cs  # Steam patch note parsing → balance changes
│   ├── SteamLeaderboardSync.cs  # Global leaderboard stats cache
│   ├── CoPickSynergyComputer.cs # Card pair co-occurrence win-rate analysis
│   ├── FloorTierComputer.cs  # Per-act tier adjustment (Act 1 ≠ Act 3)
│   ├── UpgradeValueComputer.cs  # Card upgrade delta win-rate comparison
│   ├── RunHealthComputer.cs  # Run health score 0-100 vs winning benchmarks
│   ├── CombatLogger.cs       # Per-turn card/damage/block recording
│   ├── CardUsageTracker.cs   # Card play frequency → removal recommendations
│   ├── PotionTracker.cs      # Potion acquire/use/discard event tracking
│   ├── AutoTierGenerator.cs  # Multi-signal automated tier computation
│   └── MetaArchetypeComputer.cs # Top-3 meta archetypes + core cards
├── UI/                       # Overlay UI (Godot controls)
│   ├── OverlayManager.cs     # Core lifecycle, panel management, Rebuild (1,371 lines)
│   ├── OverlayManager.Advice.cs   # Per-screen advice generation (1,047 lines)
│   ├── OverlayManager.Builder.cs  # UI element construction (826 lines)
│   ├── OverlayManager.Stats.cs    # History, deck viz, win rate, run summary (1,170 lines)
│   ├── OverlayManager.Badges.cs   # In-game card/relic badges (463 lines)
│   ├── OverlayManager.Settings.cs # Settings menu (395 lines)
│   ├── OverlayTheme.cs         # Centralized design tokens (colors, fonts, spacing, radii)
│   ├── OverlayStyles.cs        # StyleBoxFlat factory methods + StyleLabel helper
│   └── OverlayInputHandler.cs  # Input event handling
├── Data/                     # JSON/TSV data files
│   ├── archetypes.json       # Character archetype definitions
│   ├── scoring_config.json   # Scoring weights
│   ├── CardTiers/*.json      # Per-character card tiers
│   ├── RelicTiers/*.json     # Per-character relic tiers
│   ├── CardProperties/*.tsv  # Card metadata
│   ├── EventAdvice/events.json
│   ├── EnemyTips/enemies.json
│   └── BossData/bosses.json
└── pack/                     # Godot project files for PCK export
```

## Build

**Requirements**: .NET 9 SDK, Godot 4.5.1 Mono, Slay the Spire 2 installed.

The project needs game DLLs (0Harmony, GodotSharp, sts2) from a local STS2 install. Paths are configured via `local.props`.

```bash
# Build (Windows only — PostBuild deploys to game mods folder + generates PCK)
cd QuestceSpire && dotnet build -c Release

# In CI/headless (no game DLLs) — build will fail on DLL references
```

**Note**: There are no tests. The project has no test framework or test directory. Validation is done by running the mod in-game.

## Architecture Notes

### Service Locator Pattern
`Plugin.cs` is the composition root. All services are static properties on `Plugin`:
- `Plugin.TierEngine`, `Plugin.SynergyScorer`, `Plugin.DeckAnalyzer`, etc.
- `Plugin.Overlay` (the UI manager)
- `Plugin.RunDatabase`, `Plugin.RunTracker`, `Plugin.LocalStats`
- `Plugin.PipelineOrchestrator` — runs all 14 data pipelines on background init

### Data Flow
1. `GamePatches` intercepts game events via Harmony
2. `GamePatches` calls `GameStateReader` to extract state
3. `GamePatches` notifies `Plugin.Overlay` and `Plugin.RunTracker`
4. `OverlayManager.Rebuild()` orchestrates UI update using Core scorers
5. `RunTracker` persists decisions to `RunDatabase`

### OverlayManager Partial Classes
The UI is split by concern into partial classes sharing the same field set:
- **Main (.cs)**: Lifecycle, panel management, signals, `Rebuild()` orchestration
- **Advice**: `ShowCardAdvice()`, `ShowRelicAdvice()`, `ShowCombatAdvice()`, etc.
- **Builder**: `AddCardEntry()`, `AddRelicEntry()`, `AddSectionHeader()`, etc.
- **Stats**: `AddDecisionHistory()`, `AddInlineDeckViz()`, `UpdateWinRate()`, etc.
- **Badges**: `InjectCardGrades()`, `AttachGradeBadge()`, cleanup
- **Settings**: `BuildSettingsMenu()`, toggle handlers

### Key Patterns
- **Scoring**: `ICardScorer`/`IRelicScorer` interfaces in Core
- **Harmony patching**: `[HarmonyPatch]` attributes + manual patches in `GamePatches`
- **UI**: Godot Control tree built programmatically (no .tscn scenes)
- **Settings**: `OverlaySettings` (JSON file in plugin folder)
- **Logging**: `Plugin.Log()` → `spire-advisor.log`

### Design Token System
`OverlayTheme.cs` centralizes all visual constants:
- **Colors**: 30+ named colors (backgrounds, text, semantic, card types, tiers)
- **Font Sizes**: 8-level hierarchy (Title→H1→H2→Body→Small→Caption→Badge)
- **Spacing**: 6-level 4px grid (XS=2, SM=4, MD=8, LG=12, XL=16, XXL=20)
- **Radii**: 4 levels (SM=6, MD=10, LG=16, Panel=18)

`OverlayStyles.cs` provides StyleBoxFlat factory methods:
- `CreatePanelStyle()`, `CreateEntryStyle()`, `CreateBestEntryStyle()`, etc.
- `CreateThumbnailStyle()`, `CreateBadgeStyle()`, `CreateDecisionEntryStyle()`
- `StyleLabel()` — one-shot font+size+color application

### Game Version Detection
`TierEngine.DetectVersions()` runs at startup and on reload:
- Extracts `patchVersion` from loaded CardTiers JSON metadata
- Detects game version from `sts2` assembly via reflection
- `IsTierDataStale` flag triggers warning banner in overlay

### Security Hardening
- `DataUpdater`: path traversal prevention, manifest size limits
- `PatchNotesTracker`: response size limits (1MB)
- `CloudSync`: privacy notice, upload semaphore

### Pipeline Phases
PipelineOrchestrator runs 14 pipelines in 4 phases:
- Phase 0: Infrastructure (DataUpdater, CloudSync)
- Phase 1: External data (SpireCodex, PatchNotes, RuntimeExtractor, Leaderboard) — parallel
- Phase 2: Local analysis (CoPick, FloorTier, Upgrade, Meta, RelicCross) — parallel
- Phase 3: Derived (AutoTierGenerator) — sequential

## Conventions

- **Language**: Korean in README/comments, English in code identifiers
- **Namespaces**: `QuestceSpire`, `QuestceSpire.Core`, `QuestceSpire.UI`, `QuestceSpire.GameBridge`, `QuestceSpire.Tracking`
- **Naming**: PascalCase for public members, _camelCase for private fields
- **No tests**: Changes must be verified by code review only
- **Partial classes**: Use `OverlayManager.<Concern>.cs` naming for new UI partials
- **Data files**: JSON for structured data, TSV for tabular card properties
- **Design tokens**: All colors/fonts/spacing in `OverlayTheme.cs`, styles in `OverlayStyles.cs`
- **Error handling**: No bare `catch { }` — all catches must log via `Plugin.Log()`
- **Thread safety**: Use `GameStateReader.Set*()` methods, not direct field assignment
- **Settings**: Version-migrated via `OverlaySettings.SettingsVersion` (currently 7)
- **Security**: Validate filenames from API responses before disk write

## Known Large Files

| File | Lines | Notes |
|------|-------|-------|
| `OverlayManager.cs` | 1,371 | Core lifecycle — further split possible |
| `OverlayManager.Stats.cs` | 1,170 | Statistics display + run summary UI |
| `OverlayManager.Advice.cs` | 1,047 | Per-screen advice methods |
| `OverlayManager.Builder.cs` | 826 | UI element builders |
| `SynergyScorer.cs` | 693 | Complex scoring logic |
| `RunDatabase.Pipelines.cs` | 586 | Pipeline DB tables (partial class of RunDatabase) |
| `GamePatches.ScreenHooks.cs` | 469 | Screen navigation hooks |
| `OverlayManager.Badges.cs` | 463 | In-game card/relic badges |
| `OverlayManager.Settings.cs` | 395 | Settings menu |
| `GamePatches.CombatHooks.cs` | 311 | Combat damage/block/card play hooks |
| `GamePatches.CardReward.cs` | 310 | Card reward screen hooks |
| `RunDatabase.cs` | 262 | Core SQL schema — pipeline tables split to RunDatabase.Pipelines.cs |
| `DataUpdater.cs` | 240 | Automatic data file updates |
| `GamePatches.cs` | 179 | Harmony patches — entry point + utilities |
