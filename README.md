# STS2Overlay

[한국어](#한국어) | [English](#english)

Slay the Spire 2 인게임 오버레이 모드 — 실시간 카드/유물 추천, 덱 분석, 전투 추적, 런 코칭을 제공합니다.

In-game overlay mod for Slay the Spire 2 — real-time card/relic recommendations, deck analysis, combat tracking, and run coaching.

---

## 한국어

### 주요 기능

#### 실시간 추천
- **카드 보상 분석** — 덱/유물/아키타입 기반 시너지 점수, S~F 티어 등급, 커뮤니티 승률 데이터
- **유물 보상 분석** — 덱 시너지 + 유물-카드 교차 승률 기반 추천
- **업그레이드 우선순위** — 정적 티어 + DB 업그레이드 승률 델타 기반 순위
- **상점 분석** — 가성비 분석, 제거 추천 (카드 사용률 기반)
- **포션 조언** — 티어 평가, 보스전 아끼기 vs 지금 사용 추천

#### 전투 지원
- **전투 파일 추적** — 드로우/버림/손패 실시간 표시, 다음 턴 확률 계산
- **적 정보** — 위험도 + 전투 팁
- **전투 턴 로깅** — 카드 사용, 딜/블록/피해 기록 (combat_turns DB)
- **포션 사용 추적** — 획득/사용/버림 이벤트 기록

#### 전략 분석
- **보스 대비 진단** — Codex 몬스터 데이터 + 과거 전투 기록 기반 분석
- **이벤트 조언** — 선택지별 평가
- **런 건강도 게이지** — HP/골드/덱/층수 기반 0~100 점수
- **메타 아키타입** — 현재 메타 Top 3 아키타입 + 핵심 카드
- **층별 티어** — Act별 카드 승률 차이 표시

#### 런 회고
- **런 요약** — 전투 효율, 카드 사용 빈도, 커뮤니티 대비 결정 분석
- **결정 리플레이** — 모든 선택을 스크롤하며 복기, 커뮤니티 승률 비교
- **논란 선택 하이라이트** — 최적 대비 2등급 이상 낮은 선택 표시
- **글로벌 통계 비교** — Steam 리더보드 대비 내 성적

#### 데이터 파이프라인
14개 백그라운드 파이프라인이 자동으로 데이터를 수집하고 분석합니다:

| 파이프라인 | 역할 |
|-----------|------|
| SpireCodexSync | Spire Codex API 카드/유물/포션/몬스터 동기화 |
| PatchNotesTracker | Steam 패치 노트 파싱 → 밸런스 변경 추적 |
| SteamLeaderboardSync | 글로벌 리더보드 통계 캐시 |
| CoPickSynergyComputer | 카드 쌍 동시 등장 승률 분석 |
| FloorTierComputer | Act별 카드 티어 조정 |
| UpgradeValueComputer | 카드 업그레이드 승률 델타 |
| RunHealthComputer | 런 건강도 점수 산출 |
| CombatLogger | 턴별 카드/딜/블록 기록 |
| CardUsageTracker | 카드 사용 빈도 → 제거 추천 |
| PotionTracker | 포션 획득/사용/버림 추적 |
| AutoTierGenerator | 다중 신호 기반 자동 티어 생성 |
| MetaArchetypeComputer | Top 3 메타 아키타입 산출 |
| RelicCardCrossRef | 유물-카드 교차 승률 분석 |
| RuntimeCardExtractor | sts2.dll 리플렉션으로 신규 카드/유물 자동 감지 |

#### UI & 품질 개선
- 🎨 디자인 토큰 시스템으로 일관된 UI 테마
- ⚠️ 게임 버전과 티어 데이터 버전 불일치 시 자동 경고
- 🔄 신규 카드 자동 감지 및 기본 티어 자동 할당
- 🛡️ API 응답 검증 및 경로 보안 강화
- 🔒 클라우드 동기화 시 전송 데이터 안내 (프라이버시)
- 📊 SQLite 쿼리 성능 최적화 (인덱스)
- 🎬 카드 추천 진입 애니메이션 (stagger fade)
- 📂 설정 메뉴 4그룹 분류 (표시/조언/데이터/고급)
- 🔍 디버그 로깅 모드 및 로그 파일 열기

#### 설정
- 20개 이상의 기능별 ON/OFF 토글 (4개 그룹: 표시/조언/데이터/고급)
- 패널 투명도, 위치 조정
- 클라우드 동기화, 자동 업데이트, 파이프라인 개별 제어
- 프라이버시 안내: 전송 데이터 내역 투명하게 표시
- 오프라인 모드: 번들 데이터 + 디스크 캐시로 네트워크 없이도 동작

---

### 설치

#### 다운로드

[Releases](../../releases) 페이지에서 최신 버전을 다운로드하세요.

#### 설치 경로

압축 해제 후 `SpireAdvisor/` 폴더를 게임의 `mods/` 디렉토리에 복사합니다:

| OS | 경로 |
|----|------|
| **Windows** | `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\` |
| **Linux** | `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/` |
| **macOS** | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods/` |

> Steam 라이브러리 폴더를 변경한 경우: Steam → 게임 우클릭 → 관리 → 로컬 파일 보기

설치 후 구조:
```
Slay the Spire 2/mods/SpireAdvisor/
├── SpireAdvisor.dll
├── SpireAdvisor.pck
├── Data/
└── (기타 DLL)
```

#### 설치 확인

게임 실행 시 왼쪽 상단에 오버레이 패널이 나타납니다. `spire-advisor.log` 파일이 모드 폴더에 생성되면 정상입니다.

---

### 사용법

- 왼쪽 상단 오버레이 패널 자동 표시
- 드래그로 위치 이동
- ▲ 버튼으로 접기/펼치기
- ⚙ 버튼으로 설정 메뉴 열기
- 카드/유물/상점/이벤트/전투/맵 화면에서 자동 분석

---

### FAQ

**오버레이가 안 보여요**
→ `mods/SpireAdvisor/` 폴더에 `.dll`과 `.pck` 파일이 모두 있는지 확인

**게임 업데이트 후 작동 안 해요**
→ STS2 업데이트로 API가 변경되면 모드가 깨질 수 있습니다. 최신 릴리즈를 확인하세요

**성능 이슈**
→ ⚙ 설정에서 불필요한 기능을 끄면 개선됩니다

**로그 파일 위치**
→ `mods/SpireAdvisor/spire-advisor.log`

---

## English

### Key Features

#### Real-time Recommendations
- **Card Reward Analysis** — Synergy scoring based on deck/relics/archetype, S–F tier grades, community win-rate data
- **Relic Reward Analysis** — Deck synergy + relic-card cross-reference win-rate recommendations
- **Upgrade Priority** — Static tier delta + DB upgrade win-rate delta ranking
- **Shop Analysis** — Value-for-gold analysis, removal recommendations (based on card play frequency)
- **Potion Advice** — Tier ratings, save-for-boss vs use-now recommendations

#### Combat Support
- **Combat Pile Tracker** — Real-time draw/discard/hand display, next turn probability
- **Enemy Intel** — Threat levels + combat tips
- **Combat Turn Logging** — Card plays, damage/block/HP recorded per turn
- **Potion Tracking** — Acquire/use/discard event recording

#### Strategic Analysis
- **Boss Matchup Diagnosis** — Codex monster data + past combat history analysis
- **Event Advice** — Per-choice evaluation
- **Run Health Gauge** — HP/gold/deck/floor-based 0–100 score
- **Meta Archetypes** — Current meta top 3 archetypes + core cards
- **Floor-based Tiers** — Per-act card win-rate differences

#### Post-Run Review
- **Run Summary** — Combat efficiency, card play frequency, community comparison
- **Decision Replay** — Scroll through every pick with community win-rate data
- **Controversial Pick Highlights** — Flags choices 2+ grades below optimal
- **Global Stats Comparison** — Your performance vs Steam leaderboards

#### Data Pipelines
14 background pipelines automatically collect and analyze data:

| Pipeline | Purpose |
|----------|---------|
| SpireCodexSync | Spire Codex API card/relic/potion/monster sync |
| PatchNotesTracker | Steam patch note parsing → balance change tracking |
| SteamLeaderboardSync | Global leaderboard stats cache |
| CoPickSynergyComputer | Card pair co-occurrence win-rate analysis |
| FloorTierComputer | Per-act card tier adjustment |
| UpgradeValueComputer | Card upgrade win-rate delta |
| RunHealthComputer | Run health score computation |
| CombatLogger | Per-turn card/damage/block recording |
| CardUsageTracker | Card play frequency → removal recommendations |
| PotionTracker | Potion acquire/use/discard tracking |
| AutoTierGenerator | Multi-signal automated tier computation |
| MetaArchetypeComputer | Top 3 meta archetype computation |
| RelicCardCrossRef | Relic-card co-occurrence win-rate analysis |
| RuntimeCardExtractor | sts2.dll reflection for new card/relic detection |

#### UI & Quality Improvements
- 🎨 Design token system for consistent UI theming
- ⚠️ Automatic warning when game version and tier data version mismatch
- 🔄 Auto-detection of new cards with default tier assignment
- 🛡️ API response validation and path traversal prevention
- 🔒 Transparent privacy notice for cloud sync data
- 📊 SQLite query performance optimization (indexes)
- 🎬 Card recommendation entry animation (stagger fade)
- 📂 Settings menu organized into 4 groups (Display/Advice/Data/Advanced)
- 🔍 Debug logging mode and log file quick-open

#### Settings
- 20+ per-feature ON/OFF toggles (4 groups: Display/Advice/Data/Advanced)
- Panel opacity and position adjustment
- Cloud sync, auto-update, per-pipeline control
- Privacy notice: transparent display of transmitted data
- Offline mode: bundled data + disk cache for network-free operation

---

### Installation

#### Download

Get the latest release from the [Releases](../../releases) page.

#### Install Path

Extract and copy the `SpireAdvisor/` folder into the game's `mods/` directory:

| OS | Path |
|----|------|
| **Windows** | `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\` |
| **Linux** | `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/` |
| **macOS** | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods/` |

> Custom Steam library folder? Steam → Right-click game → Manage → Browse local files.

After installation:
```
Slay the Spire 2/mods/SpireAdvisor/
├── SpireAdvisor.dll
├── SpireAdvisor.pck
├── Data/
└── (other DLLs)
```

#### Verify

Launch the game. The overlay panel should appear at the top-left. A `spire-advisor.log` file in the mod folder confirms successful loading.

---

### Usage

- Overlay panel auto-displays at top-left
- Drag to reposition
- ▲ button to collapse/expand
- ⚙ button for settings menu
- Auto-analysis on card/relic/shop/event/combat/map screens

---

### FAQ

**Overlay not showing**
→ Check that `.dll` and `.pck` files both exist in `mods/SpireAdvisor/`

**Mod stopped working after game update**
→ Game API changes may break mod compatibility. Check for the latest release

**Performance issues**
→ Disable unused features in ⚙ Settings

**Log file location**
→ `mods/SpireAdvisor/spire-advisor.log`

---

## Tech Stack

- **Runtime**: .NET 9, Godot 4.5.1 Mono, Harmony
- **Language**: C# (latest, nullable enabled)
- **Data**: Newtonsoft.Json, Microsoft.Data.Sqlite (8 DB tables)
- **UI**: Godot Control tree (programmatic, no .tscn)
- **Design system**: OverlayTheme.cs (color/size tokens) + OverlayStyles.cs (reusable style builders)
- **Game integration**: Thread-safe game state access, game version detection with stale data warnings
- **Security**: Path traversal prevention, API response size limits, input validation
- **Pipelines**: 14-pipeline data processing with dependency-ordered execution (PipelineOrchestrator)

## Build (Developers)

Requirements: .NET 9 SDK, Godot 4.5.1 Mono, Slay the Spire 2

1. Copy `local.props.example` → `local.props`
2. Set paths:
   ```xml
   <STS2GamePath>your game path</STS2GamePath>
   <GodotExePath>your Godot path</GodotExePath>
   ```
3. Build: `cd QuestceSpire && dotnet build -c Release`

## Data Sources

- Card/relic tiers: Based on [sts2-advisor](https://github.com/ebadon16/sts2-advisor)
- Card names: Runtime localization from game (Korean, etc.)
- Community data: Spire Codex API, Steam leaderboards

## Compatibility

- Slay the Spire 2 v0.98+ (Early Access)

## License

MIT License
