# 드빌 시스템 이식 기획 (rpg2d 방식)

- 작성일: 2026-11-11
- 성격: 설계 기획만. 구현·수익모델은 범위 밖
- 원본: DV2Rebuild `data/*.json` 60개 + `systems/` 38개 (Godot, 팬 재구현)
- 방식: rpg2d 이식 방식 — 원본 값 무손실 이식 → 정본 테이블화 → 그 위에서 냥빌화
- 정본 문서(`game-design.md`)와 어긋나면 이식 전에 정본부터 승인받아 고침

## 이식 순서
1. 원본 그대로 반입: 수치·ID·이름 무변경. 읽기 전용 (`_ref_data` 규칙)
   - 도구: `scripts/tools/dv2_to_tsv.py` → `data/source/tables/dv2--*.tsv` + `schema/` + `dv2-import-index.md`
   - 중첩 = 자식 테이블(`_parent_row`), 깊은 중첩·리스트 = JSON 셀
2. 테이블별 이식 현황 추적 (도구가 생성, 전수 이식이 목표)
3. 정본 테이블화: TSV + 스키마 등록. 코드는 테이블만 읽음
4. 원본 그대로 동작시킴 → 이름·수치·구조를 하나씩 교체 (일괄 치환 아님, 교체 단위마다 동작 유지)
   - 교체는 정본 테이블 위에서만 (코드 수정 없음)
   - 교체 현황 = 테이블별 원본 / 교체됨 추적 (rpg2d 현황 문서 방식)
5. 공개 전 게이트: 원작명 잔존 검사 통과 필수

## 원본 반입 범위
- DV2Rebuild `data/` 수치·구조·식별자 전부
- 이름: 장비·젬 등 ID 겸 이름은 원문 그대로 남아 있음
- 대사 = 원본에도 없음. 스토리는 구간·스테이지 구성과 밸런스만 존재
- 그 외 표시 문자열(설명·이름) = 저장소에 없음 → 원문 확보 안 함
- 없는 문자열 = ID 키로 두고 냥빌 이름·문구를 바로 입력

## 원칙 (rpg2d 규칙 준용)
- 게임 규칙·밸런스 값 = 정본 테이블에서만 읽음. 코드 상수·fixture 기본값 금지
- 테이블에 값이 없으면 상수를 지어내지 않고 멈춤
- 이름 정본 = 로케일 테이블 한 장. 카드·도감·퀘스트에 이름 사본 금지
- 데이터 추가 = TSV 칼럼·테이블 + 스키마 등록. 별도 JSON 정본 신설 금지
- Unity SO = 빌드 산출물 (정본 아님) → 현 `game-design.md`의 "SO가 데이터" 항목과 충돌, 정본 수정 필요

## 반입 대상 테이블 → 시스템
| 시스템 | 원본 data | 양빌 상태 |
|---|---|---|
| 성장 | level_curve, stat_table, party_stats | 부분 있음 (간식 EXP) |
| 진화 | promote, combine_egg | 없음 |
| 부화·가챠 | gacha_eggs, egg_fragments, upgrade_egg | 부분 있음 (부화) |
| 전투 | combat, skills, dragon_skills, team_buffs, incapacitation | 있음 (단순 규칙) |
| 장비·젬 | equipment, equip_effects, gems, accessories | 없음 |
| 각성 | awaken, skill_awaken, skill_scrolls | 없음 |
| 드랍·보상 | drops, monster_drops, box_loot, battle_missions | 부분 있음 |
| 스테이지·적 | stages, stages_wiki, monsters, story_monsters | 있음 (5+1) |
| 퀘스트·칭호 | titles, adventure_events, scenario*, story* | 없음 |
| 상점·아이템 | shop, items, item_effects, imp_shop, laboratory | 부분 있음 |
| 콜로세움·기타 | colosseum, card_game, kades | 없음 |

## 범위 = 전 시스템 완전 이식
- 위 표의 시스템 전부 이식. 선별·보류·축소 없음
- 없는 것 = 에셋(그림·음악·애니메이션·폰트)뿐
- 에셋 자리는 플레이스홀더 슬롯로 비워 둠 (기존 `GameConfig` 슬롯 방식)
- 순서는 의존 순서만 (성장 → 진화·부화 → 전투·장비·각성 → 드랍·퀘스트 → 콜로세움 등)
- 원본이 서버 의존인 부분(멀티·온라인)만 클라이언트 단독 대체 방식 별도 설계

## 이식 시 정할 것
- 정본 형식 = TSV+스키마 (결정). 중첩 구조(스킬 효과·드랍 목록 등)는 자식 테이블로 정규화
- `game-design.md`의 SO 정본 서술을 TSV 정본 + SO 산출물로 고침 (승인 후)
- 원본 속성 체계(9속성 이상) ↔ 양빌 6속성 매핑
- 원본 레벨 상한 50 ↔ 양빌 단계 성장(Lv10/20) 매핑
- 반입 테이블별 판정 현황 문서를 도구로 생성할지

## 다음 단계
- 반입 테이블 60개 전수 이식 목록 확정 (이식 판정만, 불필요·미이식 없음)
- 정본 형식 결정
- 수익모델은 이식 범위 확정 후 별도 기획
