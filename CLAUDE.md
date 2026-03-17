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
├── GamePatches.cs            # Harmony patches for game hooks (897 lines)
├── Core/                     # Business logic (scoring, tiers, analysis)
│   ├── SynergyScorer.cs      # Card/relic synergy scoring (ICardScorer, IRelicScorer)
│   ├── TierEngine.cs         # Loads tier data from Data/
│   ├── DeckAnalyzer.cs       # Deck composition analysis
│   ├── AdaptiveScorer.cs     # Community data integration
│   ├── BossAdvisor.cs        # Boss matchup analysis
│   ├── CardPropertyScorer.cs # Card property evaluation
│   ├── EventAdvisor.cs       # Event choice recommendations
│   ├── EnemyAdvisor.cs       # Enemy threat assessment
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
│   ├── PipelineOrchestrator.cs  # Dependency-ordered pipeline runner
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
│   ├── OverlayManager.cs     # Core lifecycle, panel management, Rebuild (1,331 lines)
│   ├── OverlayManager.Advice.cs   # Per-screen advice generation (909 lines)
│   ├── OverlayManager.Builder.cs  # UI element construction (739 lines)
│   ├── OverlayManager.Stats.cs    # History, deck viz, win rate (721 lines)
│   ├── OverlayManager.Badges.cs   # In-game card/relic badges (463 lines)
│   └── OverlayManager.Settings.cs # Settings menu (300 lines)
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

## Conventions

- **Language**: Korean in README/comments, English in code identifiers
- **Namespaces**: `QuestceSpire`, `QuestceSpire.Core`, `QuestceSpire.UI`, `QuestceSpire.GameBridge`, `QuestceSpire.Tracking`
- **Naming**: PascalCase for public members, _camelCase for private fields
- **No tests**: Changes must be verified by code review only
- **Partial classes**: Use `OverlayManager.<Concern>.cs` naming for new UI partials
- **Data files**: JSON for structured data, TSV for tabular card properties

## Known Large Files

| File | Lines | Notes |
|------|-------|-------|
| `OverlayManager.cs` | 1,331 | Core lifecycle — further split possible |
| `OverlayManager.Advice.cs` | 909 | Per-screen advice methods |
| `GamePatches.cs` | 897 | 20+ Harmony patches — candidate for splitting |
| `OverlayManager.Builder.cs` | 739 | UI element builders |
| `OverlayManager.Stats.cs` | 721 | Statistics display |
| `SynergyScorer.cs` | 621 | Complex scoring logic |
| `RunDatabase.Pipelines.cs` | 500 | Pipeline DB tables (partial class of RunDatabase) |
| `RunDatabase.cs` | 262 | Core SQL schema — pipeline tables split to RunDatabase.Pipelines.cs |
