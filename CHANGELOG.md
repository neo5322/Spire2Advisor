# Changelog

모든 **릴리즈된** 변경 사항을 기록합니다.

> 아직 공개 릴리즈가 없습니다. 아래는 개발 이력입니다.

## [Unreleased]

### 개발 이력

- 14개 데이터 파이프라인 코드 작성 (검증 필요)
- 런 회고/결정 리플레이 코드 작성 (검증 필요)
- OverlayManager 5개 partial class 분리
- GamePatches 3개 partial class 분리
- Core 스코어링 엔진 설정 외부화 (scoring_config.json)
- ICardScorer/IRelicScorer 인터페이스 추출
- Constructor injection 도입 (Core 클래스)
- 디자인 토큰 시스템 (OverlayTheme.cs, OverlayStyles.cs)
- SQLite 기반 런 히스토리 (RunDatabase)
- 오프라인 데이터 관리 (OfflineDataManager)
- 보안 강화 (경로 검증, API 응답 크기 제한)

### 초기 기능 (v0.1.0 코드 기반)

- 카드/유물 보상 분석 (시너지 점수 + 티어 등급)
- 전투 파일 추적 (드로우/버림/손패 실시간 표시)
- 보스 대비 진단
- 업그레이드 우선순위
- 적 정보 / 이벤트 조언 / 상점 분석
- 런 추적 및 승률 통계
