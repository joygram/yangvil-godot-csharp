# 냥빌 — Godot C# 판 (yangvil-godot-csharp)

털실 바구니에서 태어난 고양이를 키우고, 파티를 꾸려 골목길 모험을 떠나는 육성 수집 RPG.
Unity 프로젝트 YangVil 을 [godot-enhanced](https://github.com/joygram/godot-enhanced) C# (.NET 11) 로 옮긴 판.

> **⚡ Built with [godot-enhanced](https://github.com/joygram/godot-enhanced)**
> - 이 게임의 웹 빌드 = godot-enhanced 산출물 (`dotnet-web-4.7.2-0.3.0`)
> - 원래 Godot 4 = C# 웹 내보내기 불가 → godot-enhanced 로 C# 그대로 웹 실행
> - ▶ 플레이: https://yangvil-godot-csharp.deuklab.workers.dev

## 두 판
- Unity 판: [joygram/YangVil](https://github.com/joygram/YangVil) — Unity 6000.6.8f1
- Unity 판 플레이: https://yangvil.deuklab.workers.dev
- **Godot C# 판 (이 저장소)**: [joygram/yangvil-godot-csharp](https://github.com/joygram/yangvil-godot-csharp) — godot-enhanced 4.7.2 (.NET 11)
- Godot C# 판 플레이: https://yangvil-godot-csharp.deuklab.workers.dev

## 핵심 메리트
### 1. Unity 마이그레이션이 쉽다
- 게임 규칙 C# = 그대로 이식 (702줄 중 9줄 교체)
- 그림·표 = 무수정 복사, 화면 배치·연출 수치 동일
- UI = 엔진 API 대응만, 코드량 −25%

### 2. 라이선스 부담이 없다
- 엔진 = Godot (MIT) + godot-enhanced 패치, 런타임 비용·로열티·좌석 요금 없음
- 이 게임 코드 = MIT
- 엔진 소스 전체 공개 → 직접 고치고 배포 가능

### 3. C# 웹 배포가 쉽다
- 원래 Godot 4 = C# 웹 내보내기 불가 → godot-enhanced 로 가능
- `npm run deploy:web` 한 줄: 최신 엔진 받기 → 웹 빌드 → 압축 → Cloudflare 배포
- 내려받기 크기 Unity 대비 −44% (12.2MB vs 21.8MB)
- .NET 11 최신 C#·표준 라이브러리 그대로

## 무엇이 같나
- 게임 = Unity 판과 동일: 인트로 → 마을(마당·부화소·상점·식구) → 모험 지도 → 전투 → 결과 → 마을
- 화면 배치·문구·색·연출 수치 = 그대로
- UI 에셋 = 그대로: 그림 28장, 표 26장 무수정 복사
- 게임 규칙 코드(`game/scripts/core`) = 그대로: 702줄 중 저장 직렬화 9줄만 교체

## 얼마나 손댔나
| 부분 | Unity 판 | Godot C# 판 | 바뀐 것 |
|---|---|---|---|
| 규칙 (Core 4파일) | 702줄 | 704줄 | `Game.cs` 저장 3곳 |
| 화면·연출 (UI) | 2,648줄 | 1,983줄 (−25%) | 엔진 API 만 대응 |
| 표·그림 | 26 + 28 | 26 + 28 | 없음 |

- UI 감소 = Unity 판의 Godot 흉내 층 제거 → Godot 기본 기능 직접 사용
- Node2→`Node2D`, Sprite2→`Sprite2D`, Paint→`_Draw`, Tw→`Tween`
- UiBoost 셰이더→`modulate`, UGUI 레이아웃→Container, TMP→`Label`
- 저장: `JsonUtility` → `System.Text.Json`

## Unity 판 웹 비교 (실측)
| 항목 | Unity WebGL | godot-enhanced C# |
|---|---|---|
| 전송 크기 | 21.8MB | **12.2MB (−44%)** |
| 내려받기 끝 | 1.4~2.0s | **0.7~1.2s** |
| 첫 화면 | **1.7~2.4s** | 3.6~3.9s |
| 받은 뒤 초기화 | **약 0.3s** | 약 2.7s |
| 메모리 (Chrome 합계) | **639MB** | 764MB |

- 조건: 같은 PC, headless Chrome(소프트웨어 WebGL), 캐시 없음, 새 프로필
- 첫 화면 = 로딩 덮개가 사라질 때까지
- 재현: `node scripts/web/compare.mjs 3`
- 알려진 이슈: 초기화(wasm 54MB 컴파일 + .NET 시작)가 Unity 보다 김
- AOT: 표 수치 = 표준 템플릿(인터프리터)
- AOT 적용 = 게임 AOT 출력을 `mono_wasm_aot_dir=` 로 링크한 전용 템플릿 필요 (godot-enhanced `patches/dotnet-web-export.md`)
- 전용 템플릿 AOT 측정 = 예정

## 구조
- `game/` = Godot 프로젝트 (`project.godot`, `YangVil.csproj`)
- `game/scripts/core/` = 규칙 (엔진 무관 C#)
- `game/scripts/ui/` = 화면·연출
- `game/data/tables/*.tsv` = 정본 표
- `game/assets/` = 그림·폰트
- 저장: `user://yangvil_save.json`

## 실행
- `npm start` → .NET 11 확인 → godot-enhanced 최신 C# 키트 받기 → 빌드 → 가져오기 → 실행
- 엔진 = [godot-enhanced](https://github.com/joygram/godot-enhanced/releases) 최신 `dotnet-web-*` 릴리스 자동
- 키트 위치 `~/.godot-enhanced/` (다른 저장소와 공유), NuGet 소스 `godot-enhanced` 자동 등록
- 버전 고정: `GODOT_ENHANCED_TAG=dotnet-web-4.7.2-0.3.0`, 직접 지정: `GODOT_BIN`
- 웹 내보내기: 프리셋 `Web` → `Builds/WebGL/index.html`

## 에셋 바꾸기
- 코드 수정 없음 → 그림 교체 또는 표(TSV) 한 줄 수정
- 그림: `game/assets/shared/...` 같은 이름·같은 칸 배치로 덮어쓰기
- 새 그림: `game/assets/` 아래 추가 → `sprite.tsv` 에 한 줄
- `sprite.tsv` 칸: `sprite_id path columns rows start count`
- 색: `game/scripts/ui/Kit.cs` 맨 위 색 목록
- 문구: `game/data/tables/text.tsv` 의 `ko` 칸
- 타격감 시간·거리: `game/data/tables/config.tsv` (`*_ms`, `*_px`, `*_pct`)

## 라이선스
- 코드: [MIT](LICENSE)
- 그림·폰트: [Credits.md](Credits.md) 각 출처 라이선스 (Galmuri = OFL)
