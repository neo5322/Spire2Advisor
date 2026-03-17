# Spire Advisor — Slay the Spire 2 Mod

[한국어](#기능) | [English](#features)

실시간 카드/유물 추천, 덱 분석, 전투 파일 추적을 제공하는 STS2 인게임 오버레이 모드입니다.

An in-game overlay mod for Slay the Spire 2 that provides real-time card/relic recommendations, deck analysis, and combat tracking.

---

## 기능

- **카드 보상 분석**: 현재 덱/유물/아키타입 기반 시너지 점수 + 티어 등급
- **유물 보상 분석**: 덱 시너지 기반 추천
- **전투 파일 추적**: 드로우/버림/손패 실시간 표시 + 다음 턴 확률 계산
- **보스 대비 진단**: 맵 화면에서 보스 대응력 분석
- **업그레이드 우선순위**: 휴식에서 업그레이드 순위 표시
- **적 정보**: 위험도 + 팁
- **이벤트 조언**: 선택지별 평가
- **상점 분석**: 가성비 분석
- **런 추적**: 선택 기록, 승률 통계

## Features

- **Card Reward Analysis**: Synergy scoring + tier grades based on current deck/relics/archetype
- **Relic Reward Analysis**: Deck synergy-based recommendations
- **Combat File Tracker**: Real-time draw/discard/hand display + next turn probability
- **Boss Matchup Diagnosis**: Boss readiness analysis on the map screen
- **Upgrade Priority**: Upgrade rankings at rest sites
- **Enemy Intel**: Threat level + tips
- **Event Advice**: Per-choice evaluation
- **Shop Analysis**: Value-for-gold analysis
- **Run Tracking**: Decision history, win rate statistics

---

## 설치 / Installation

### 다운로드 / Download

[Releases](../../releases) 페이지에서 최신 `SpireAdvisor-v*.zip`을 다운로드하세요.

Download the latest `SpireAdvisor-v*.zip` from the [Releases](../../releases) page.

### 설치 경로 / Install Path

압축을 해제하고 `SpireAdvisor/` 폴더를 게임의 `mods/` 디렉토리에 복사합니다.

Extract and copy the `SpireAdvisor/` folder into the game's `mods/` directory:

| OS | 경로 / Path |
|----|-------------|
| **Windows (Steam)** | `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\` |
| **Linux (Steam)** | `~/.steam/steam/steamapps/common/Slay the Spire 2/mods/` |
| **macOS (Steam)** | `~/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods/` |

> Steam 라이브러리 폴더를 변경한 경우, Steam → 게임 우클릭 → 관리 → 로컬 파일 보기로 경로를 확인하세요.
>
> If you changed your Steam library folder: Steam → Right-click game → Manage → Browse local files.

설치 후 폴더 구조:

```
Slay the Spire 2/mods/SpireAdvisor/
├── SpireAdvisor.dll
├── SpireAdvisor.pck
├── Data/
└── (기타 DLL / other DLLs)
```

### 설치 확인 / Verify

게임을 실행하면 왼쪽 상단에 Spire Advisor 오버레이가 나타납니다. `spire-advisor.log` 파일이 모드 폴더에 생성되면 정상 로딩된 것입니다.

Launch the game. The Spire Advisor overlay should appear at the top-left. A `spire-advisor.log` file will be created in the mod folder upon successful loading.

---

## 사용법 / Usage

- 왼쪽 상단 오버레이 패널 / Top-left overlay panel
- 드래그로 이동 / Drag to move
- ▲ 버튼으로 접기 / ▲ button to collapse
- ⚙ 설정에서 기능별 ON/OFF / ⚙ Settings for per-feature toggles
- 카드/유물/상점/이벤트/전투/맵에서 자동 분석 / Auto-analysis on card/relic/shop/event/combat/map screens

<!-- 스크린샷 추가 예정 / Screenshots coming soon -->

---

## FAQ / 문제 해결 / Troubleshooting

**Q: 오버레이가 안 보여요 / Overlay not showing**
- `mods/SpireAdvisor/` 폴더에 `SpireAdvisor.dll`과 `SpireAdvisor.pck`가 모두 있는지 확인하세요.
- Check that both `SpireAdvisor.dll` and `SpireAdvisor.pck` exist in `mods/SpireAdvisor/`.

**Q: 게임 업데이트 후 작동 안 해요 / Mod stopped working after game update**
- STS2 업데이트로 API가 변경되면 모드가 호환되지 않을 수 있습니다. 최신 모드 버전을 확인하세요.
- Game updates may break mod compatibility. Check for the latest mod release.

**Q: 성능 이슈 / Performance issues**
- ⚙ 설정에서 사용하지 않는 기능을 OFF하면 성능이 개선될 수 있습니다.
- Turn off unused features in ⚙ Settings to improve performance.

**Q: 로그 파일은 어디에? / Where are the logs?**
- `mods/SpireAdvisor/spire-advisor.log` 파일을 확인하세요.
- Check `mods/SpireAdvisor/spire-advisor.log`.

---

## 빌드 (개발자용) / Build (Developers)

### 요구사항 / Requirements
- .NET 9 SDK
- Godot 4.5.1 Mono
- Slay the Spire 2

### 절차 / Steps
1. `local.props.example` → `local.props` 복사 / Copy
2. 경로 수정 / Edit paths:
   ```xml
   <STS2GamePath>게임 경로 / game path</STS2GamePath>
   <GodotExePath>Godot 경로 / Godot path</GodotExePath>
   ```
3. 빌드 실행 / Run build:
   - **Windows**: `cd QuestceSpire && build.bat`
   - **Linux/macOS**: `cd QuestceSpire && ./build.sh`

---

## 데이터 출처 / Data Sources

- 카드/유물 티어: [sts2-advisor](https://github.com/ebadon16/sts2-advisor) 기반
- 카드 이름: 게임 런타임 로컬라이즈 (한국어 등)

## 호환성 / Compatibility

- STS2 v0.98+ (Early Access)

## 라이선스 / License

MIT License
