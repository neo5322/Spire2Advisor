# Spire Advisor — Slay the Spire 2 모드

실시간 카드/유물 추천, 덱 분석, 전투 파일 추적을 제공하는 STS2 인게임 오버레이 모드입니다.

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

## 설치 (바이너리)

1. [Releases](../../releases)에서 `SpireAdvisor-v*.zip` 다운로드
2. 압축 해제 → `SpireAdvisor/` 폴더를 게임의 `mods/`에 복사:
   ```
   Slay the Spire 2/mods/SpireAdvisor/
     ├── SpireAdvisor.dll
     ├── SpireAdvisor.pck
     └── Data/
   ```
3. STS2 실행

## 빌드 (소스)

### 요구사항
- .NET 9 SDK
- Godot 4.5.1 Mono
- Slay the Spire 2

### 절차
1. `local.props.example` → `local.props` 복사
2. 경로 수정:
   ```xml
   <STS2GamePath>게임 경로</STS2GamePath>
   <GodotExePath>Godot 경로</GodotExePath>
   ```
3. `build.bat` 실행

## 사용법

- 왼쪽 상단 오버레이 패널
- 드래그로 이동, ▲로 접기
- ⚙ 설정에서 기능별 ON/OFF
- 카드/유물/상점/이벤트/전투/맵에서 자동 분석

## 데이터 출처

- 카드/유물 티어: [sts2-advisor](https://github.com/ebadon16/sts2-advisor) 기반
- 카드 이름: 게임 런타임 로컬라이즈 (한국어 등)

## 호환성

- STS2 v0.98+ (Early Access)

## 라이선스

MIT License
