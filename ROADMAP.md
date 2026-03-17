# Spire Advisor — 로드맵

현재 상태: 14개 데이터 파이프라인 구현 완료, DB 테이블 7개 생성, PipelineOrchestrator 가동.
**문제: 파이프라인 데이터가 UI/스코어링에 대부분 미연결.** 데이터는 있지만 활용이 안 됨.

---

## v0.9 — 파이프라인 연결 (Pipeline Wiring)

파이프라인 데이터를 기존 스코어링 + UI에 연결. 새 기능 없이 기존 화면을 풍부하게 만든다.

### v0.9.1 — 스코어링 통합

| 작업 | 파일 | 내용 |
|------|------|------|
| Co-Pick 시너지 스코어링 | `SynergyScorer.cs` | `CoPickSynergyComputer.GetCoPickBonus()` 호출 → 카드 보상에 "함께 승률 높은 카드 있음: +0.3" 보너스 |
| 업그레이드 가치 표시 | `SynergyScorer.cs` | `UpgradeValue` DB 데이터 → 휴식지/업그레이드 화면에서 카드별 업그레이드 우선순위 점수 |
| 자동 티어 폴백 | `AdaptiveScorer.cs` | 수동 티어 없는 카드에 `auto_tiers/*.json` 데이터 사용 |

### v0.9.2 — UI 정보 표시

| 작업 | 파일 | 내용 |
|------|------|------|
| 패치 변경 뱃지 | `OverlayManager.Advice.cs` | 카드/유물 보상에 "최근 변경: damage 8→10" 표시 (patch_changes 테이블) |
| 층별 티어 표시 | `OverlayManager.Advice.cs` | "이 카드 Act 1 승률 62% / Act 3 승률 41%" 표시 |
| 메타 아키타입 패널 | `OverlayManager.Advice.cs` | 맵 화면에서 "메타 Top 3: 힘 62%, 배기 55%, 블록 51%" + 핵심 카드 |

### v0.9.3 — 런 건강도 게이지

| 작업 | 파일 | 내용 |
|------|------|------|
| ShowRunHealth() | `OverlayManager.Advice.cs` | 새 메서드: RunHealthComputer 결과를 게이지 바로 표시 |
| 맵 화면 통합 | `OverlayManager.cs` | ShowMapAdvice() 호출 시 런 건강도 항상 상단에 표시 |
| 전투 화면 통합 | `OverlayManager.cs` | ShowCombatAdvice() 에도 간략한 건강도 표시 |

---

## v0.10 — Harmony 패치 확장 (Game Hooks)

파이프라인은 있지만 게임 이벤트를 못 잡아서 데이터가 빈 것들을 해결.

### v0.10.1 — 전투 턴 추적

| 작업 | 파일 | 내용 |
|------|------|------|
| OnTurnStart/End 패치 | `GamePatches.cs` (또는 신규 `GamePatches.Combat.cs`) | CombatManager 턴 전환 메서드 Harmony 패치 |
| OnCardPlayed 패치 | 위와 동일 | 카드 플레이 이벤트 → CombatLogger.OnCardPlayed() 호출 |
| OnDamageDealt/Taken 패치 | 위와 동일 | 데미지 이벤트 → CombatLogger 호출 |
| CombatLogger 연동 | `CombatTracker.cs` | CombatLogger를 기존 CombatTracker에 연결 |

### v0.10.2 — 포션 추적

| 작업 | 파일 | 내용 |
|------|------|------|
| OnPotionObtained 패치 | `GamePatches.cs` | 포션 획득 → PotionTracker.RecordObtained() |
| OnPotionUsed 패치 | `GamePatches.cs` | 포션 사용 → PotionTracker.RecordUsed() |
| OnPotionDiscarded 패치 | `GamePatches.cs` | 포션 버림 → PotionTracker.RecordDiscarded() |
| GameStateReader 포션 읽기 | `GameStateReader.cs` | Player.Potions 리플렉션으로 포션 슬롯 추출 |

### v0.10.3 — 상점 구매 추적

| 작업 | 파일 | 내용 |
|------|------|------|
| OnShopPurchase 패치 | `GamePatches.cs` | 상점 구매 이벤트 → RunTracker.RecordDecision(Shop) |
| 상점 decisions 기록 | `RunTracker.cs` | 구매 결정을 decisions 테이블에 저장 |

---

## v0.11 — 새 어드바이저 모듈 (New Advisors)

파이프라인 데이터 + Harmony 패치 연동 완료 후, 새 코어 분석 모듈 추가.

### v0.11.1 — PotionAdvisor

| 작업 | 파일 | 내용 |
|------|------|------|
| PotionAdvisor.cs | `Core/PotionAdvisor.cs` (신규) | 포션 티어 + "보스전까지 아끼기" vs "지금 사용" 추천 |
| Codex 포션 데이터 활용 | 위와 동일 | SpireCodexSync의 codex_potions.json 로드 |
| UI: 전투 화면 포션 섹션 | `OverlayManager.Advice.cs` | ShowCombatAdvice()에 "추천: Fire Potion 지금 사용" 섹션 추가 |
| Plugin 등록 | `Plugin.cs` | Plugin.PotionAdvisor 서비스 등록 |

### v0.11.2 — 강화된 BossAdvisor

| 작업 | 파일 | 내용 |
|------|------|------|
| Codex 몬스터 데이터 통합 | `BossAdvisor.cs` | codex_monsters.json의 HP/공격력 수치를 사용 |
| 데이터 기반 진단 | `BossAdvisor.cs` | 하드코딩된 임계값 → combat_turns 데이터 기반 위험도 산출 |
| 보스별 상세 매치업 | `OverlayManager.Advice.cs` | "당신의 덱 vs Hexaghost: AOE 부족, 블록 충분" |

### v0.11.3 — 카드 사용률 기반 제거 추천

| 작업 | 파일 | 내용 |
|------|------|------|
| CardUsageTracker 재계산 | `CardUsageTracker.cs` | combat_turns 데이터 축적 후 자동 재계산 |
| 제거 화면 강화 | `OverlayManager.Advice.cs` | "Strike: 전투당 0.3회 사용 → 제거 1순위" 표시 |

---

## v0.12 — 런 회고 / 코칭 (Post-Game Analysis)

### v0.12.1 — 런 요약 패널

| 작업 | 파일 | 내용 |
|------|------|------|
| RunSummary 데이터 클래스 | `Core/RunSummary.cs` (신규) | 런 종료 시 전체 decisions + combat_turns 종합 분석 |
| ShowRunSummary() | `OverlayManager.Stats.cs` | 런 종료 후 "잘한 선택 / 나쁜 선택 / 사용률 낮은 카드" 분석 |
| 핵심 지표 | 위와 동일 | 총 데미지, 전투 효율, 골드 사용, 아키타입 진행도 그래프 |

### v0.12.2 — 의사결정 리플레이

| 작업 | 파일 | 내용 |
|------|------|------|
| 결정별 피드백 | `OverlayManager.Stats.cs` | "Floor 12: Strike 대신 Inflame 픽 → 커뮤니티 데이터상 더 나은 선택이었음" |
| 승률 시뮬레이션 | `Core/RunSummary.cs` | "이 카드를 픽하지 않았다면 승률 +5% 예상" (커뮤니티 데이터 기반) |

---

## v0.13 — 추가 파이프라인 (Remaining Pipelines)

### v0.13.1 — RelicCardCrossRef

| 작업 | 파일 | 내용 |
|------|------|------|
| RelicCardCrossRef.cs | `Tracking/RelicCardCrossRef.cs` (신규) | 유물-카드 시너지 교차 참조 (유물이 특정 카드 태그에 미치는 영향) |
| DB 테이블 | `RunDatabase.Pipelines.cs` | `relic_card_synergies` 테이블 추가 |
| 스코어링 통합 | `SynergyScorer.cs` | "Shuriken + multi_hit 카드 → +0.3" 유물 기반 시너지 보너스 |

### v0.13.2 — RuntimeCardExtractor

| 작업 | 파일 | 내용 |
|------|------|------|
| RuntimeCardExtractor.cs | `Tracking/RuntimeCardExtractor.cs` (신규) | sts2.dll 리플렉션으로 새 카드/유물 자동 감지 |
| 스텁 티어 생성 | 위와 동일 | 티어 데이터 없는 신규 카드에 C급 기본 등급 부여 |
| CardPropertyScorer 폴백 | `CardPropertyScorer.cs` | TSV에 없는 카드를 런타임 추출 데이터로 보완 |

### v0.13.3 — IDataPipeline 인터페이스

| 작업 | 파일 | 내용 |
|------|------|------|
| IDataPipeline 정의 | `Tracking/IDataPipeline.cs` (신규) | `Name`, `RunAsync()`, `Interval`, `Enabled` 프로퍼티 |
| 기존 파이프라인 마이그레이션 | 모든 파이프라인 | 공통 인터페이스 구현 |
| PipelineOrchestrator 개선 | `PipelineOrchestrator.cs` | 인터페이스 기반 동적 파이프라인 등록 + 주기적 재실행 |

---

## v0.14 — 폴리시 (Polish & Settings)

### v0.14.1 — 설정 UI 확장

| 작업 | 파일 | 내용 |
|------|------|------|
| 파이프라인 토글 | `OverlayManager.Settings.cs` | SpireCodexSync, PatchNotesTracker 등 개별 on/off |
| 포션 어드바이스 토글 | 위와 동일 | ShowPotionAdvice 설정 |
| 런 건강도 토글 | 위와 동일 | ShowRunHealth 설정 |
| OverlaySettings v6 | `OverlaySettings.cs` | 새 설정 필드 + 마이그레이션 로직 |

### v0.14.2 — 오프라인 경험

| 작업 | 파일 | 내용 |
|------|------|------|
| Codex 번들 데이터 | `Data/SpireCodex/` | 첫 실행용 오프라인 번들 (cards, relics, potions, monsters) |
| PipelineHttp 디스크 캐시 | `PipelineHttp.cs` | URL 해시 기반 디스크 캐시 + TTL 설정 |
| 그레이스풀 디그레이드 | 모든 파이프라인 | 네트워크 없을 때 마지막 캐시 사용 + 로그 경고 |

### v0.14.3 — 글로벌 통계 비교

| 작업 | 파일 | 내용 |
|------|------|------|
| 리더보드 표시 | `OverlayManager.Stats.cs` | "당신의 승률: 상위 12% (글로벌)" 표시 |
| 캐릭터별 비교 | 위와 동일 | 캐릭터별 글로벌 vs 로컬 승률 차트 |

---

## 의존성 다이어그램

```
v0.9 파이프라인 연결 ←── 즉시 시작 가능 (데이터 이미 있음)
  ├── v0.9.1 스코어링 통합
  ├── v0.9.2 UI 정보 표시
  └── v0.9.3 런 건강도 게이지

v0.10 Harmony 패치 확장 ←── v0.9와 병렬 가능
  ├── v0.10.1 전투 턴 추적 ──→ v0.11.3 카드 사용률 제거 추천
  ├── v0.10.2 포션 추적 ──→ v0.11.1 PotionAdvisor
  └── v0.10.3 상점 구매 추적

v0.11 새 어드바이저 ←── v0.10 완료 필요
  ├── v0.11.1 PotionAdvisor (← v0.10.2)
  ├── v0.11.2 강화된 BossAdvisor
  └── v0.11.3 카드 사용률 제거 (← v0.10.1)

v0.12 런 회고 ←── v0.10.1 + v0.11 일부 필요
  ├── v0.12.1 런 요약 패널
  └── v0.12.2 의사결정 리플레이

v0.13 추가 파이프라인 ←── 독립적, 언제든 가능
  ├── v0.13.1 RelicCardCrossRef
  ├── v0.13.2 RuntimeCardExtractor
  └── v0.13.3 IDataPipeline 인터페이스

v0.14 폴리시 ←── 모든 기능 구현 후
  ├── v0.14.1 설정 UI
  ├── v0.14.2 오프라인 경험
  └── v0.14.3 글로벌 통계
```

---

## 우선순위 요약

| 순위 | 버전 | 핵심 가치 | 난이도 |
|------|------|----------|--------|
| 1 | **v0.9.1** | 기존 데이터로 즉시 점수 개선 | 낮음 |
| 2 | **v0.9.3** | 런 건강도 — 가장 눈에 띄는 새 기능 | 낮음 |
| 3 | **v0.9.2** | 패치/메타 정보 — 유저 신뢰도 향상 | 낮음 |
| 4 | **v0.10.1** | 전투 데이터 수집 — 장기적으로 가장 가치 있는 데이터 | 중간 |
| 5 | **v0.10.2** | 포션 추적 — 현재 완전 누락된 기능 | 중간 |
| 6 | **v0.11.1** | 포션 어드바이저 — 새 카테고리의 추천 | 중간 |
| 7 | **v0.12.1** | 런 회고 — TFTactics 코칭 대응 | 높음 |
| 8 | **v0.13.1** | 유물-카드 교차 시너지 — 정밀 추천 | 중간 |
