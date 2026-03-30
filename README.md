# Spire Advisor

[한국어](#한국어) | [English](#english)

Slay the Spire 2 인게임 오버레이 모드.

In-game overlay mod for Slay the Spire 2.

> **Status**: Pre-release (개발 중). 아직 공개 릴리즈가 없습니다. 빌드하려면 게임 DLL이 필요합니다.
>
> **Status**: Pre-release (in development). No public release yet. Game DLLs required to build.

---

## 한국어

### 핵심 기능 (코드 작성 완료, 미검증)

> 아래 기능들은 코드가 존재하지만, 실제 게임 환경에서의 검증이 완료되지 않았습니다.
> 게임 업데이트, 외부 API 변경, Harmony 패치 호환성 등으로 인해 일부 기능이 작동하지 않을 수 있습니다.

#### 실시간 추천
- **카드 보상 분석** — 덱/유물/아키타입 기반 시너지 점수, S~F 티어 등급
- **유물 보상 분석** — 덱 시너지 기반 추천
- **업그레이드 우선순위** — 정적 티어 기반 순위
- **상점 분석** — 가성비 분석, 제거 추천
- **포션 조언** — 티어 평가, 보스전 아끼기 vs 지금 사용 추천

#### 전투 지원
- **전투 파일 추적** — 드로우/버림/손패 실시간 표시
- **적 정보** — 위험도 + 전투 팁
- **전투 턴 로깅** — 카드 사용, 딜/블록/피해 기록

#### 전략 분석
- **보스 대비 진단** — 덱 구성 기반 분석
- **이벤트 조언** — 선택지별 평가
- **런 건강도 게이지** — HP/골드/덱/층수 기반 0~100 점수

#### 런 회고
- **런 요약** — 전투 효율, 카드 사용 빈도 분석
- **결정 리플레이** — 모든 선택을 스크롤하며 복기

### 실험적 기능 (외부 API 의존, 작동 미확인)

아래 기능들은 외부 서비스에 의존하며, 해당 서비스의 실제 가용성이 확인되지 않았습니다.

| 기능 | 의존 서비스 | 상태 |
|------|-----------|------|
| 커뮤니티 승률 데이터 | questcespire-api (자체 API) | 미확인 |
| Spire Codex 카드/유물 동기화 | Spire Codex API | 미확인 |
| Steam 리더보드 비교 | Steam Community API | 미확인 |
| 패치 노트 자동 파싱 | Steam Store API | 미확인 |
| 메타 아키타입 분석 | 위 데이터 종합 | 미확인 |
| 층별 티어 조정 | 로컬 데이터 분석 | 미확인 |
| 유물-카드 교차 승률 | 로컬 데이터 분석 | 미확인 |

---

### 설치

> 아직 공개 릴리즈가 없습니다. 직접 빌드해야 합니다.

빌드 방법은 아래 [개발자 빌드](#개발자-빌드) 섹션을 참고하세요.

설치 후 `SpireAdvisor/` 폴더를 게임의 `mods/` 디렉토리에 복사합니다:

| OS | 경로 |
|----|------|
| **Windows** | `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\` |
| **Linux** | `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/` |
| **macOS** | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods/` |

설치 후 구조:
```
Slay the Spire 2/mods/SpireAdvisor/
├── SpireAdvisor.dll
├── SpireAdvisor.pck
├── Data/
└── (기타 DLL)
```

### 사용법

- 왼쪽 상단 오버레이 패널 자동 표시
- 드래그로 위치 이동
- ▲ 버튼으로 접기/펼치기
- ⚙ 버튼으로 설정 메뉴 열기

### FAQ

**오버레이가 안 보여요**
→ `mods/SpireAdvisor/` 폴더에 `.dll`과 `.pck` 파일이 모두 있는지 확인

**게임 업데이트 후 작동 안 해요**
→ STS2 업데이트로 API가 변경되면 모드가 깨질 수 있습니다

**로그 파일 위치**
→ `mods/SpireAdvisor/spire-advisor.log`

---

## English

### Core Features (Code Complete, Unverified)

> The features below have code written but have not been verified in a live game environment.
> Some features may not work due to game updates, external API changes, or Harmony patch compatibility.

#### Real-time Recommendations
- **Card Reward Analysis** — Synergy scoring based on deck/relics/archetype, S-F tier grades
- **Relic Reward Analysis** — Deck synergy-based recommendations
- **Upgrade Priority** — Static tier-based ranking
- **Shop Analysis** — Value-for-gold analysis, removal recommendations
- **Potion Advice** — Tier ratings, save-for-boss vs use-now recommendations

#### Combat Support
- **Combat Pile Tracker** — Real-time draw/discard/hand display
- **Enemy Intel** — Threat levels + combat tips
- **Combat Turn Logging** — Card plays, damage/block/HP recorded per turn

#### Strategic Analysis
- **Boss Matchup Diagnosis** — Deck composition-based analysis
- **Event Advice** — Per-choice evaluation
- **Run Health Gauge** — HP/gold/deck/floor-based 0-100 score

#### Post-Run Review
- **Run Summary** — Combat efficiency, card play frequency analysis
- **Decision Replay** — Scroll through every pick to review

### Experimental Features (External API Dependent, Unverified)

These features depend on external services whose availability has not been confirmed.

| Feature | Dependency | Status |
|---------|-----------|--------|
| Community win-rate data | questcespire-api (self-hosted) | Unverified |
| Spire Codex card/relic sync | Spire Codex API | Unverified |
| Steam leaderboard comparison | Steam Community API | Unverified |
| Patch notes auto-parsing | Steam Store API | Unverified |
| Meta archetype analysis | Aggregated data | Unverified |
| Floor-based tier adjustment | Local data analysis | Unverified |
| Relic-card cross win-rate | Local data analysis | Unverified |

---

### Installation

> No public release is available yet. You must build from source.

See the [Developer Build](#developer-build) section below.

Copy the `SpireAdvisor/` folder into the game's `mods/` directory:

| OS | Path |
|----|------|
| **Windows** | `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\` |
| **Linux** | `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/` |
| **macOS** | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods/` |

After installation:
```
Slay the Spire 2/mods/SpireAdvisor/
├── SpireAdvisor.dll
├── SpireAdvisor.pck
├── Data/
└── (other DLLs)
```

### Usage

- Overlay panel auto-displays at top-left
- Drag to reposition
- ▲ button to collapse/expand
- ⚙ button for settings menu

### FAQ

**Overlay not showing**
→ Check that `.dll` and `.pck` files both exist in `mods/SpireAdvisor/`

**Mod stopped working after game update**
→ Game API changes may break mod compatibility

**Log file location**
→ `mods/SpireAdvisor/spire-advisor.log`

---

## Tech Stack

- **Runtime**: .NET 9, Godot 4.5.1 Mono, Harmony
- **Language**: C# (latest, nullable enabled)
- **Data**: Newtonsoft.Json, Microsoft.Data.Sqlite
- **UI**: Godot Control tree (programmatic, no .tscn)

## Developer Build

Requirements: .NET 9 SDK, Godot 4.5.1 Mono, Slay the Spire 2 installed.

Game DLLs (`0Harmony.dll`, `GodotSharp.dll`, `sts2.dll`) are required from a local STS2 install. CI builds will fail without them.

1. Copy `local.props.example` → `local.props`
2. Set paths:
   ```xml
   <STS2GamePath>your game path</STS2GamePath>
   <GodotExePath>your Godot path</GodotExePath>
   ```
3. Build: `cd QuestceSpire && dotnet build -c Release`

## Data Sources

- Card/relic tiers: Based on [sts2-advisor](https://github.com/ebadon16/sts2-advisor)
- Card names: Runtime localization from game

## Naming

This project uses multiple names in different contexts:
- **Spire Advisor** — User-facing mod name
- **QuestceSpire** — Code namespace (legacy)
- **STS2Overlay** — GitHub repository name (legacy)

## Compatibility

- Slay the Spire 2 (Early Access)

## License

MIT License
