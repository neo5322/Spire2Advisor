# Changelog

모든 주요 변경 사항을 기록합니다.

## [0.8.0] - 2026-03-17

### 추가
- Linux/macOS 빌드 스크립트 (`build.sh`, `release.sh`)
- CLAUDE.md 개발자 가이드 및 `.editorconfig`
- README 한/영 이중 언어 지원, OS별 설치 가이드, FAQ/트러블슈팅
- CHANGELOG.md 추가

### 변경
- `GamePatches` → 3개 partial class로 분리 (`GamePatches.cs`, `CardReward.cs`, `ScreenHooks.cs`)
- `RunDatabase` → partial class 분리
- `OverlayManager` → 5개 partial class로 분리 (Advice, Builder, Stats, Badges, Settings)
- Core 스코어링 엔진 설정 외부화 (`scoring_config.json`)
- `ICardScorer`/`IRelicScorer` 인터페이스 추출

### 수정
- UI 메모리 누수 및 시그널 수명주기 관리 개선
- JSON 역직렬화 null 안전성 강화
- 빈 컬렉션 / substring 범위 오류 수정
- GameBridge/Patches/Plugin 안정성 개선
- Tracking/DB 안정성 및 데이터 정확도 개선
- DB 트랜잭션 에러 시 Rollback 누락 수정 (SaveRun, CommunityStats)
- 버전 번호 `.csproj` 단일 소스로 통일 (Plugin.cs, release 스크립트 자동 동기화)
- CI 릴리즈 파이프라인 산출물 검증 강화
- 릴리즈 스크립트 `local.props` 존재 확인 및 필수 파일 검증 추가

## [0.1.0] - 초기 릴리즈

### 추가
- 카드/유물 보상 분석 (시너지 점수 + 티어 등급)
- 전투 파일 추적 (드로우/버림/손패 실시간 표시)
- 보스 대비 진단
- 업그레이드 우선순위
- 적 정보 / 이벤트 조언 / 상점 분석
- 런 추적 및 승률 통계
- SQLite 기반 런 히스토리
