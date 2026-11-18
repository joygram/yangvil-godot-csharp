#!/usr/bin/env python3
"""DV2Rebuild data/*.json -> TSV tables + schema (rpg2d 방식 무손실 반입).

usage: python -I dv2_to_tsv.py <src_data_dir> <out_dir>
out: <out_dir>/tables/dv2--<Name>.tsv, <out_dir>/schema/dv2--<Name>.json, <out_dir>/dv2-import-index.md
규칙: 값 변경 없음. 중첩은 자식 테이블로 분리(부모 `_row` 참조). 스칼라 리스트는 JSON 문자열 셀.
"""
import hashlib
import json
import os
import re
import sys

SCALAR = (str, int, float, bool, type(None))


def is_scalar(v):
    return isinstance(v, SCALAR)


def cell(v):
    if v is None:
        return ""
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, str):
        return v.replace("\t", " ").replace("\r", " ").replace("\n", "\\n")
    if isinstance(v, (int, float)):
        return json.dumps(v)
    return json.dumps(v, ensure_ascii=False, separators=(",", ":"))


def scalar_list(v):
    return isinstance(v, list) and all(is_scalar(x) for x in v)


def scalar_tree(v):
    """스칼라/스칼라 리스트만으로 이뤄진 값 (JSON 셀로 담아도 무손실)."""
    if is_scalar(v) or scalar_list(v):
        return True
    return False


class Importer:
    def __init__(self, base):
        self.base = base
        self.tables = {}  # name -> {"cols": [...], "rows": [...], "parent": str|None, "lists": set}

    def add_rows(self, name, rows, parent=None, lists=None):
        t = self.tables.setdefault(name, {"cols": [], "rows": [], "parent": parent, "lists": set()})
        for r in rows:
            for k in r:
                if k not in t["cols"]:
                    t["cols"].append(k)
        t["rows"].extend(rows)
        if lists:
            t["lists"] |= lists

    def record(self, tname, rec, parent_name, parent_row, key_col=None, key=None, idx=None):
        """레코드 dict 하나를 행 + 자식 테이블들로 분해. 반환: 행 번호."""
        vals = list(rec.values())
        same_shape = False
        if len(vals) >= 3 and all(isinstance(x, dict) for x in vals):
            shapes = {}
            for x in vals:
                shapes[frozenset(x)] = shapes.get(frozenset(x), 0) + 1
            same_shape = max(shapes.values()) >= 0.6 * len(vals)
            if all(re.fullmatch(r"[0-9_#a-f:.\-]+", str(k)) and any(c.isdigit() for c in str(k)) for k in rec):
                same_shape = True  # 키가 숫자 ID형이면 데이터 키로 취급
        if key_col is None and (len(vals) >= 10 and all(isinstance(x, dict) for x in vals) or same_shape):
            # ID 키 사전(키마다 같은 모양) → 키별 테이블이 아니라 키 열을 가진 한 테이블
            return self.node(tname, rec, parent_name, parent_row)
        row = {}
        if parent_name is not None:
            row["_parent_row"] = parent_row
        if key_col is not None:
            row[key_col] = key
        if idx is not None:
            row["_idx"] = idx
        lists = set()
        children = []
        for k, v in rec.items():
            if is_scalar(v):
                row[k] = v
            elif scalar_list(v):
                row[k] = v
                lists.add(k)
            elif tname.count("__") >= 2 or (key_col is not None and isinstance(v, dict)):
                row[k] = v  # 깊이 3 이상은 JSON 셀로 보존 (무손실)
                lists.add(k)
            else:
                children.append((k, v))
        t = self.tables.setdefault(tname, {"cols": [], "rows": [], "parent": parent_name, "lists": set()})
        my_row = len(t["rows"])
        row["_row"] = my_row
        self.add_rows(tname, [row], parent_name, lists)
        for k, v in children:
            self.node(f"{tname}__{safe(k)}", v, tname, my_row)
        return my_row

    def node(self, tname, v, parent_name, parent_row):
        if isinstance(v, dict):
            vals = list(v.values())
            if vals and all(isinstance(x, dict) for x in vals):
                for key, rec in v.items():
                    self.record(tname, rec, parent_name, parent_row, key_col="_key", key=key)
            else:
                # 혼합/스칼라 dict: 한 행으로. 자식은 분리
                self.record(tname, v, parent_name, parent_row)
        elif isinstance(v, list):
            for i, rec in enumerate(v):
                if isinstance(rec, dict):
                    self.record(tname, rec, parent_name, parent_row, idx=i)
                elif scalar_list(rec):
                    self.add_rows(tname, [{"_parent_row": parent_row, "_idx": i, "value": rec,
                                           "_row": len(self.tables.get(tname, {"rows": []})["rows"])}],
                                  parent_name, {"value"})
                else:
                    self.add_rows(tname, [{"_parent_row": parent_row, "_idx": i, "value": rec,
                                           "_row": len(self.tables.get(tname, {"rows": []})["rows"])}],
                                  parent_name)
        else:
            self.add_rows(tname, [{"_parent_row": parent_row, "value": v,
                                   "_row": len(self.tables.get(tname, {"rows": []})["rows"])}], parent_name)

    def run(self, name, data):
        top = f"{name}"
        if isinstance(data, list):
            self.node(top, data, None, None)
        else:
            self.record(top, data, None, None)


def safe(k):
    """파일명 금지 문자를 `_uXX`로 치환 (유일·복원 가능)."""
    return "".join(f"_u{ord(c):02x}" if c in '<>:"/\\|?*' or ord(c) < 32 or c == "_" and False else c
                   for c in str(k)) or "_empty"


def pascal(s):
    return "".join(p[:1].upper() + p[1:] for p in s.replace("-", "_").split("_") if p)


def main():
    src, out = sys.argv[1], sys.argv[2]
    tdir, sdir = os.path.join(out, "tables"), os.path.join(out, "schema")
    os.makedirs(tdir, exist_ok=True)
    os.makedirs(sdir, exist_ok=True)
    index = []
    for fn in sorted(os.listdir(src)):
        if not fn.endswith(".json"):
            continue
        path = os.path.join(src, fn)
        raw = open(path, "rb").read()
        data = json.loads(raw.decode("utf-8"))
        base = pascal(fn[:-5])
        imp = Importer(base)
        imp.run(base, data)
        for tname, t in imp.tables.items():
            cols = ["_row"] + [c for c in t["cols"] if c != "_row"]
            with open(os.path.join(tdir, f"dv2--{tname}.tsv"), "w", encoding="utf-8", newline="\n") as f:
                f.write("\t".join(cols) + "\n")
                for r in t["rows"]:
                    f.write("\t".join(cell(r.get(c)) for c in cols) + "\n")
            schema = {
                "table": tname,
                "domain": "dv2",
                "origin": {
                    "source_path": f"DV2Rebuild/data/{fn}",
                    "source_sha256": hashlib.sha256(raw).hexdigest(),
                    "source_row_count": len(t["rows"]),
                },
                "primary_key": ["_row"],
                "relations": ([{"column": "_parent_row", "table": t["parent"], "parent_column": "_row"}]
                              if t["parent"] else []),
                "lists": sorted(t["lists"]),
            }
            with open(os.path.join(sdir, f"dv2--{tname}.json"), "w", encoding="utf-8", newline="\n") as f:
                json.dump(schema, f, ensure_ascii=False, indent=2)
                f.write("\n")
            index.append((fn, tname, len(t["rows"]), len(cols), t["parent"] or "-"))
    with open(os.path.join(out, "dv2-import-index.md"), "w", encoding="utf-8", newline="\n") as f:
        f.write("# DV2 반입 테이블 색인\n\n> Generated by scripts/tools/dv2_to_tsv.py. Do not edit manually.\n\n")
        f.write(f"- 원본 파일: {len({i[0] for i in index})}\n- 테이블: {len(index)}\n\n")
        f.write("| 원본 | 테이블 | 행 | 열 | 부모 |\n|---|---|---:|---:|---|\n")
        for i in index:
            f.write(f"| {i[0]} | {i[1]} | {i[2]} | {i[3]} | {i[4]} |\n")
    print(f"files={len({i[0] for i in index})} tables={len(index)}")


if __name__ == "__main__":
    main()
