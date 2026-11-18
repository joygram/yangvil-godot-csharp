# 냥빌 — Godot C# 판 (yangvil-godot-csharp)

털실 바구니에서 태어난 고양이를 키우고, 파티를 꾸려 골목길 모험을 떠나는 육성 수집 RPG.
Unity 프로젝트 YangVil 을 [godot-enhanced](https://github.com/joygram/godot-enhanced) C# (.NET 11) 로 옮긴 판.

## 두 판
- Unity 판: [joygram/YangVil](https://github.com/joygram/YangVil) — Unity 6000.6.8f1
- Unity 판 플레이: https://yangvil.deuklab.workers.dev
- **Godot C# 판 (이 저장소)**: [joygram/yangvil-godot-csharp](https://github.com/joygram/yangvil-godot-csharp) — godot-enhanced 4.7.2 (.NET 11)
- Godot C# 판 플레이: 배포 예정

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

## .NET 11 최신판
- `net11.0` + 최신 C# 그대로 사용 (Unity = 자체 Mono/IL2CPP 런타임에 묶임)
- 표준 `System.Text.Json`·`async/await`·LINQ 제약 없음
- 웹 내보내기 = godot-enhanced C# 웹 지원 (원래 Godot 4 는 C# 웹 불가)
- 웹·안드로이드 기본 트리밍 (godot-enhanced 0.1.5)

## 성능 (측정 예정)
| 항목 | Unity WebGL | Godot C# 웹 |
|---|---|---|
| 내려받기 크기 | 측정 예정 | 측정 예정 |
| 첫 화면까지 | 측정 예정 | 측정 예정 |
| 힙 메모리 | 측정 예정 | 측정 예정 |
| 빌드 시간 | 측정 예정 | 측정 예정 |

- 같은 게임·같은 에셋, 같은 조건 측정값만 기입

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
