# Spire Advisor — Feature Backlog

TFT 헬퍼 (tactics.tools, MetaTFT, Mobalytics 등) 벤치마크 기반 기능 로드맵.

---

## 데이터 소스

기능 구현 전에 다양한 소스에서 데이터를 확보 + 파싱 + 정제해야 함.

### 즉시 통합 가능 (API/구조화 데이터)

| 소스 | URL | 형태 | 데이터 | 통합 방법 |
|------|-----|------|--------|----------|
| **Spire Codex** | [spire-codex.com](https://spire-codex.com/) | REST API (JSON) | 전체 카드/유물/포션/몬스터 데이터 (14개 언어) + 버전 간 diff | API 호출 → 카드 스탯/적 수치 자동 동기화 |
| **로컬 런 히스토리** | `saves/history/*.run` | JSON 파일 | 유저 자신의 전체 런 기록 (덱/유물/선택/층수) | GameDataImporter 확장 |
| **Steam 패치노트** | [store.steampowered.com/news/app/2868840](https://store.steampowered.com/news/app/2868840/) | RSS/JSON | 밸런스 패치 변경점 | 자동 파싱 → 트렌드 데이터 |
| **Steam 리더보드** | `steamcommunity.com/stats/2868840/leaderboards/?xml=1` | XML | 글로벌 클리어/승률 | 통계 보정용 |
| **자체 CloudSync** | `questcespire-api.questcespire.workers.dev/api` | REST API | 모드 유저 런 데이터 | 이미 구현됨 |

### 커뮤니티 데이터 (크롤링/파싱 필요)

| 소스 | URL | 데이터 | 비고 |
|------|-----|--------|------|
| **STS2Stats.com** | [sts2stats.com](https://sts2stats.com/) | 카드 픽률/승률 통계 | SpireLogs 후계자, API 미확인 |
| **Mobalytics** | [mobalytics.gg/slay-the-spire-2/tier-lists](https://mobalytics.gg/slay-the-spire-2/tier-lists) | 큐레이션 티어리스트 | 스크래핑 필요 |
| **sts2.wiki** | [sts2.wiki/cards/tier-list](https://sts2.wiki/cards/tier-list/) | 커뮤니티 투표 기반 티어 | 스크래핑 필요 |
| **wiki.gg** | [slaythespire.wiki.gg](https://slaythespire.wiki.gg/) | 카드/유물/적 상세 (832 문서) | 구조화 데이터 추출 |
| **Untapped.gg** | [sts2.untapped.gg](https://sts2.untapped.gg/en/cards) | 카드 데이터베이스 | MTG Arena 수준의 분석으로 확장 가능성 |
| **Reddit** | r/slaythespire | 메타 논의, 밸런스 의견 | JSON API 사용 가능 |

### 게임 내 추출 (DLL/파일)

| 소스 | 추출 방법 | 데이터 |
|------|----------|--------|
| **sts2.dll** | ILSpy/dnSpyEx 디컴파일 또는 런타임 리플렉션 | 카드 스탯 (데미지/블록/코스트), 적 HP/공격력, 보스 패턴 |
| **SlayTheSpire2.pck** | GDRE Tools 언팩 | 로컬라이즈 JSON, 이미지, 애니메이션 |
| **런타임 Harmony 패치** | GamePatches에서 캡처 | 실시간 전투 중 적 스탯, 인텐트 |

### 오픈소스 참고 프로젝트

| 프로젝트 | URL | 참고 가치 |
|----------|-----|----------|
| **spirescope** | [github.com/thequantumfalcon/spirescope](https://github.com/thequantumfalcon/spirescope) | 세이브 파서, 로그 테일러, Reddit/Steam 크롤러 |
| **spire-codex** | [github.com/ptrlrd/spire-codex](https://github.com/ptrlrd/spire-codex) | DLL 디컴파일 파이프라인 + 버전 diff 도구 |
| **sts2-advisor** | [github.com/ebadon16/sts2-advisor](https://github.com/ebadon16/sts2-advisor) | 커뮤니티 승률 수집 접근법 |

### 주시할 잠재 소스

| 소스 | 상태 |
|------|------|
| **Mega Crit 공식 데이터 덤프** | STS1은 380GB/7500만 런 공개. STS2는 1주일 만에 2500만+ 런 — 공개 시 최대 데이터 소스 |
| **Twitch 플러그인** | 공식 지원 예정 발표, STS1의 Slay the Relics 참고 |
| **STS2용 SpireLogs** | 아직 없음 — 선점 기회 |

---

## 데이터 정제 파이프라인 (TODO)

### Phase 1: 게임 데이터 자동 추출

1. **Spire Codex API 통합**
   - 카드/유물/몬스터 데이터 자동 동기화
   - 버전 diff로 패치 변경점 자동 감지
   - `DataUpdater`에서 Spire Codex를 primary source로 활용

2. **로컬 런 히스토리 완전 파싱**
   - spirescope의 `saves.py` 참고하여 `GameDataImporter` 확장
   - 바닐라 + 모디드 런 모두 수집
   - co-op 런 데이터도 포함

### Phase 2: 커뮤니티 데이터 집계

3. **다중 소스 크롤러**
   - STS2Stats.com / Mobalytics / wiki 티어리스트 크롤링
   - 여러 소스의 티어를 가중 평균하여 합산 티어 산출
   - API 서버(Cloudflare Workers)에서 스케줄링

4. **대규모 런 데이터 수집**
   - 자체 CloudSync 데이터 확장
   - Steam 리더보드 통합
   - "같이 픽된 카드 조합" 승률 집계 (`{cardA, cardB} → win_rate, sample_size`)

### Phase 3: 파생 데이터 생성

5. **자동 티어 산출**
   - 다중 소스 승률 + 픽률 + 시너지 데이터 → 자동 티어 계산
   - 수동 티어를 base로, 데이터로 보정
   - 목표: "패치 후 3일이면 새 티어 자동 반영"

6. **패치 트렌드 생성**
   - Spire Codex 버전 diff + Steam 패치노트 파싱
   - 패치 전후 승률 비교 → "이 카드 ↑12%" 트렌드

---

## 기능 백로그

### P0 — 데이터 기반 (데이터 파이프라인 완료 후)

- [ ] **카드 조합 시너지 세트** — "이 카드를 픽하면 함께 가져가면 좋은 카드 3장"
  - 데이터: 커뮤니티 런에서 co-pick 승률 집계
  - UI: 카드 보상 화면에서 "추천 조합" 섹션
  - TFT 대응: 아이템 조합표, 유닛 시너지

- [ ] **추천 빌드 경로 (Meta Comps)** — "현재 캐릭터 승률 Top 3 아키타입 + 핵심 카드"
  - 데이터: 아키타입별 승률 + 핵심 카드 목록
  - UI: 런 시작 시 / 맵 화면에서 "메타 빌드" 패널
  - TFT 대응: Meta Comps 페이지

- [ ] **패치 트렌드 표시** — "이 카드 승률 ↑12% (최근 패치)"
  - 데이터: Spire Codex diff + 패치별 커뮤니티 승률
  - UI: 카드/유물 보상에 ↑↓ 화살표
  - TFT 대응: tactics.tools 트렌드 추적

### P1 — 분석 기능

- [ ] **런 건강도 미터** — "이 런의 클리어 확률: 67%"
  - BossAdvisor + 덱 분석 + HP/골드 기반
  - 상단에 항상 표시되는 게이지 바
  - TFT 대응: MetaTFT 라운드별 승률 예측

- [ ] **액트별 체크리스트** — "Act 2 전 필요: 블록 4+, 스케일링 1+, AOE 1+"
  - BossAdvisor를 능동적으로 표시
  - 맵 화면에서 부족한 요소 강조
  - TFT 대응: Blitz.gg 디시전 트리 가이드

- [ ] **런 회고 / 코칭** — "런 종료 후 잘한 선택 / 나쁜 선택 분석"
  - RunDatabase decisions 기반
  - "이 카드는 덱에서 사용률 5%였음 — 스킵이 나았을 수 있음"
  - TFT 대응: TFTactics 포스트게임 코칭

### P2 — 고급 기능

- [ ] **보스 상세 매치업** — "당신의 덱 vs Hexaghost: AOE 부족, 블록 충분"
  - 보스별 Spire Codex 기반 실제 스탯/패턴 데이터
  - TFT 대응: MetaTFT 카운터/매치업 분석

- [ ] **골드/경제 효율 분석** — "다른 클리어 런 대비 상점 사용 빈도, 휴식 vs 업그레이드"
  - 성공 런 평균과 현재 런 비교
  - TFT 대응: MetaTFT 경제/레벨링 비교

- [ ] **카드 사용률 통계** — 전투에서 실제로 플레이된 횟수 추적
  - "Demon Form: 전투당 평균 0.3회 플레이 (드로우 확률 고려 시 적절)"
  - 덱 최적화 근거 제공

---

## 참고: TFT 헬퍼 벤치마크

| 툴 | 강점 | 차별점 |
|----|------|--------|
| **tactics.tools** | 통계 분석 깊이, 트렌드 추적 | 웹 전용, 오버레이 없음 |
| **MetaTFT** | 실시간 데이터 (분 단위), 승률 예측 | 200만+ 게임/일 분석 |
| **TFTactics.gg** | 초보 친화적, 포스트게임 코칭 | 큐레이션 기반 |
| **Mobalytics** | 큐레이션+데이터 균형, 커스텀 빌드 | 멀티게임 플랫폼 |
| **Blitz.gg** | 디시전 트리, 레벨링 계산기 | 분기 추천 |
