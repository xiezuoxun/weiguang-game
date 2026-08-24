#!/usr/bin/env python3
# test_validate_contract.py — 契约校验器（I1）自测：证明校验器本身"正向放行 + 负向拦截"。
# 这是 CI 里唯一**无需 Unity 即可执行**的自动化测试，用于守护 I1 不退化（校验器被改坏时立刻红）。
# 用法：python tools/test_validate_contract.py
# 退出码：0=全部用例通过，1=有用例未达预期
import copy, csv, os, subprocess, sys, tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
VALIDATOR = os.path.join(HERE, "validate_contract.py")

# ── 合规基线（3 委托 / 3 客户 / 3 物件，含 1 条主线）──────────────────
BASE = {
    "clients.csv": (
        ["client_id", "display_name", "relationship_level", "visit_count", "mainplot_progress"],
        [
            {"client_id": "cl_a", "display_name": "甲", "relationship_level": "1", "visit_count": "1", "mainplot_progress": "0"},
            {"client_id": "cl_b", "display_name": "乙", "relationship_level": "0", "visit_count": "0", "mainplot_progress": "0"},
            {"client_id": "cl_c", "display_name": "丙", "relationship_level": "5", "visit_count": "3", "mainplot_progress": "1"},
        ],
    ),
    "items.csv": (
        ["item_id", "display_name", "item_type", "material", "client_id", "grid_w", "grid_h", "is_mainplot"],
        [
            {"item_id": "it_a", "display_name": "怀表", "item_type": "Clock", "material": "Glass", "client_id": "cl_a", "grid_w": "8", "grid_h": "8", "is_mainplot": "false"},
            {"item_id": "it_b", "display_name": "照片", "item_type": "Paper", "material": "Paper", "client_id": "cl_b", "grid_w": "10", "grid_h": "10", "is_mainplot": "false"},
            {"item_id": "it_c", "display_name": "信", "item_type": "Letter", "material": "Paper", "client_id": "cl_c", "grid_w": "10", "grid_h": "12", "is_mainplot": "true"},
        ],
    ),
    "commissions.csv": (
        ["commission_id", "client_id", "item_id", "chapter_index", "session_soft_budget_min",
         "is_daily", "reveal_threshold", "fragment_count", "choice_count", "ending_variants", "is_mainplot"],
        [
            {"commission_id": "com_001", "client_id": "cl_a", "item_id": "it_a", "chapter_index": "1", "session_soft_budget_min": "7", "is_daily": "false", "reveal_threshold": "0.85", "fragment_count": "3", "choice_count": "2", "ending_variants": "2", "is_mainplot": "false"},
            {"commission_id": "com_002", "client_id": "cl_b", "item_id": "it_b", "chapter_index": "2", "session_soft_budget_min": "8", "is_daily": "false", "reveal_threshold": "0.85", "fragment_count": "0", "choice_count": "3", "ending_variants": "2", "is_mainplot": "false"},
            {"commission_id": "com_003", "client_id": "cl_c", "item_id": "it_c", "chapter_index": "3", "session_soft_budget_min": "9", "is_daily": "false", "reveal_threshold": "0.85", "fragment_count": "6", "choice_count": "2", "ending_variants": "3", "is_mainplot": "true"},
        ],
    ),
}


def write_fixture(data, extra=None):
    d = tempfile.mkdtemp(prefix="i1_fixture_")
    for name, (header, rows) in data.items():
        with open(os.path.join(d, name), "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=header, extrasaction="ignore")
            w.writeheader()
            for r in rows:
                w.writerow(r)
    for name, text in (extra or {}).items():
        with open(os.path.join(d, name), "w", newline="", encoding="utf-8") as f:
            f.write(text)
    return d


def run_validator(data_dir):
    p = subprocess.run([sys.executable, VALIDATOR, data_dir], capture_output=True, text=True)
    return p.returncode, (p.stdout or "") + (p.stderr or "")


# ── 变异器（每个用例改一处，其余保持合规）───────────────────────────
def mut(file, row, field, value):
    def f(d):
        d[file][1][row][field] = value
    return f


def drop_col(file, field):
    def f(d):
        header, rows = d[file]
        d[file] = ([c for c in header if c != field], rows)
    return f


def drop_row(file, row):
    def f(d):
        d[file][1].pop(row)
    return f


def dup_row(file, row):
    def f(d):
        d[file][1].append(copy.deepcopy(d[file][1][row]))
    return f


CASES = [
    # (用例名, 变异器, 期望退出码, 期望输出关键字)
    ("baseline 合规放行", None, 0, "PASS"),
    ("边界合法值放行（fc=0/6, cc=3, budget=5/10, level=5）",
     lambda d: [mut("commissions.csv", 0, "session_soft_budget_min", "5")(d),
                mut("commissions.csv", 1, "session_soft_budget_min", "10")(d)], 0, "PASS"),
    ("reveal_threshold 非默认值仅告警不拦截", mut("commissions.csv", 0, "reveal_threshold", "0.5"), 0, "warn"),

    ("fragment_count=7 越界", mut("commissions.csv", 0, "fragment_count", "7"), 1, "fragment_count"),
    ("fragment_count=-1 越界", mut("commissions.csv", 0, "fragment_count", "-1"), 1, "fragment_count"),
    ("choice_count=1 越界", mut("commissions.csv", 0, "choice_count", "1"), 1, "拒绝激活"),
    ("choice_count=4 越界", mut("commissions.csv", 0, "choice_count", "4"), 1, "choice_count"),
    ("ending_variants=1 越界", mut("commissions.csv", 0, "ending_variants", "1"), 1, "ending_variants"),
    ("reveal_threshold=1.5 越界", mut("commissions.csv", 0, "reveal_threshold", "1.5"), 1, "reveal_threshold"),
    ("reveal_threshold=-0.1 越界", mut("commissions.csv", 0, "reveal_threshold", "-0.1"), 1, "reveal_threshold"),
    ("session_soft_budget_min=12 越界", mut("commissions.csv", 0, "session_soft_budget_min", "12"), 1, "session_soft_budget_min"),
    ("session_soft_budget_min=4 越界", mut("commissions.csv", 0, "session_soft_budget_min", "4"), 1, "session_soft_budget_min"),
    ("relationship_level=6 越界", mut("clients.csv", 0, "relationship_level", "6"), 1, "relationship_level"),
    ("chapter_index=0 非法", mut("commissions.csv", 0, "chapter_index", "0"), 1, "chapter_index"),

    ("item_type 枚举非法", mut("items.csv", 0, "item_type", "Foo"), 1, "item_type"),
    ("material 枚举非法", mut("items.csv", 0, "material", "Metal"), 1, "material"),
    ("grid_w=0 非法", mut("items.csv", 0, "grid_w", "0"), 1, "grid_w"),

    ("is_mainplot=yes 非法布尔（C# bool.Parse 会崩）", mut("commissions.csv", 0, "is_mainplot", "yes"), 1, "非法布尔"),
    ("is_daily=1 非法布尔", mut("commissions.csv", 0, "is_daily", "1"), 1, "非法布尔"),
    ("items.is_mainplot=Y 非法布尔", mut("items.csv", 0, "is_mainplot", "Y"), 1, "非法布尔"),

    ("fragment_count 非数值", mut("commissions.csv", 0, "fragment_count", "three"), 1, "不是合法整数"),
    ("session_soft_budget_min 非数值", mut("commissions.csv", 0, "session_soft_budget_min", "七分钟"), 1, "不是合法数值"),

    ("缺失必需列 fragment_count", drop_col("commissions.csv", "fragment_count"), 1, "缺少必需列"),
    ("缺失必需列 grid_h", drop_col("items.csv", "grid_h"), 1, "缺少必需列"),

    ("client_id 引用悬空", mut("commissions.csv", 0, "client_id", "cl_ghost"), 1, "引用不存在"),
    ("item_id 引用悬空", mut("commissions.csv", 0, "item_id", "it_ghost"), 1, "引用不存在"),
    ("commission_id 重复", dup_row("commissions.csv", 0), 1, "重复"),

    ("委托数 2 < MVP 下限 3", lambda d: [drop_row("commissions.csv", 0)(d)], 1, "MVP 委托数"),
    ("无主线委托", mut("commissions.csv", 2, "is_mainplot", "false"), 1, "主线委托"),
]

EXTRA_CASES = [
    ("R3 二级分支列 parent_option_id 拦截（C4）",
     {"choices.csv": "node_id,option_id,wording,truth_level,ending_tag,parent_option_id\ncn_1,op0,措辞,0.5,Truth,op_x\n"},
     1, "R3 单层锁死"),
    ("choices.csv 单层合规放行",
     {"choices.csv": "node_id,option_id,wording,truth_level,ending_tag\ncn_1,op0,措辞,0.5,Truth\n"},
     0, "PASS"),
]


def main():
    if not os.path.isfile(VALIDATOR):
        print(f"[ERROR] 找不到校验器: {VALIDATOR}")
        return 1

    passed, failed = 0, []
    print("== I1 契约校验器自测 ==")
    for name, mutator, want_code, want_text in CASES:
        data = copy.deepcopy(BASE)
        if mutator:
            mutator(data)
        code, out = run_validator(write_fixture(data))
        ok = (code == want_code) and (want_text in out)
        if ok:
            passed += 1
            print(f"  [ok]   {name}")
        else:
            failed.append(name)
            print(f"  [FAIL] {name} → exit={code}（期望 {want_code}）关键字'{want_text}' {'命中' if want_text in out else '未命中'}")

    for name, extra, want_code, want_text in EXTRA_CASES:
        code, out = run_validator(write_fixture(copy.deepcopy(BASE), extra))
        ok = (code == want_code) and (want_text in out)
        if ok:
            passed += 1
            print(f"  [ok]   {name}")
        else:
            failed.append(name)
            print(f"  [FAIL] {name} → exit={code}（期望 {want_code}）")

    total = len(CASES) + len(EXTRA_CASES)
    print("== 结果 ==")
    if failed:
        print(f"FAIL：{len(failed)}/{total} 用例未达预期 → {failed}")
        return 1
    print(f"PASS：{passed}/{total} 用例通过（正向 4 / 负向 {total - 4}）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
