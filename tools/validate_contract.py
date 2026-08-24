#!/usr/bin/env python3
# validate_contract.py — 控制清单 I1：CI 契约校验器（ADR-004 数据驱动内容）。
# 校验 Assets/Data/*.csv 是否符合 GDD 00 共享数据契约边界（护栏 §5：越界必须暴露，不静默截断）。
# 用法：python validate_contract.py [数据目录，默认 game/Assets/Data]
# 退出码：0=PASS，1=FAIL（可接入 CI）
#
# 覆盖（与 game/docs/TEST_STRATEGY.md「层 0 契约门」一一对应）：
#   1. 必需列缺失            → FAIL（防 Unity 导入期 KeyError 崩溃）
#   2. 数值不可解析          → FAIL（防 int/float.Parse 运行时抛异常）
#   3. 布尔字面量非法        → FAIL（is_daily/is_mainplot 只接受 true/false；C# bool.Parse 会崩）
#   4. 数值边界越界          → FAIL（C6：fragment_count/choice_count/budget/level/variants）
#   5. 枚举非法              → FAIL（item_type/material）
#   6. 引用完整性            → FAIL（commission→client/item，item→client）
#   7. MVP 量与主线存在性    → FAIL（S4 ④：3–5 委托，≥1 主线）
#   8. R3 单层锁死           → FAIL（choices.csv 若存在，禁止二级引用列，C4）
import csv, sys, os

FAIL = []

# 必需列（与 GameBootstrap.LoadData() 读取的字段严格一致；缺列会让 Unity 导入期 KeyError）
REQUIRED_COLS = {
    "commissions.csv": ["commission_id", "client_id", "item_id", "chapter_index",
                        "session_soft_budget_min", "is_daily", "reveal_threshold",
                        "fragment_count", "choice_count", "ending_variants", "is_mainplot"],
    "clients.csv": ["client_id", "display_name", "relationship_level", "visit_count", "mainplot_progress"],
    "items.csv": ["item_id", "display_name", "item_type", "material", "client_id",
                  "grid_w", "grid_h", "is_mainplot"],
}

# R3 单层锁死：choices.csv 出现任一列即视为二级分支引用（架构 §7 / S3⑥-4 / C4）
R3_FORBIDDEN_COLS = ["parent_option_id", "parent_node_id", "next_node_id", "child_node_id", "sub_options"]


def err(msg):
    FAIL.append(msg)
    print(f"  [FAIL] {msg}")


def load(path):
    with open(path, newline="", encoding="utf-8-sig") as f:  # utf-8-sig 剥 BOM
        return list(csv.DictReader(f))


def cols_of(path):
    with open(path, newline="", encoding="utf-8-sig") as f:
        return next(csv.reader(f), [])


def check_columns(name, path):
    """必需列体检。返回 True=列齐全（可继续逐行校验）。"""
    have = [c.strip() for c in cols_of(path)]
    missing = [c for c in REQUIRED_COLS[name] if c not in have]
    for c in missing:
        err(f"{name}: 缺少必需列 `{c}`（导入期会 KeyError）")
    return not missing


def num(cid, field, raw, kind=float):
    """安全解析数值：不可解析→FAIL 并返回 None（不抛栈，CI 输出可读）。"""
    try:
        return kind(str(raw).strip())
    except (TypeError, ValueError):
        err(f"{cid}: {field}={raw!r} 不是合法{'整数' if kind is int else '数值'}")
        return None


def boolean(cid, field, raw):
    """布尔字面量只接受 true/false（大小写不敏感）。C# bool.Parse 对 yes/1/Y 会抛异常。"""
    v = str(raw).strip().lower() if raw is not None else ""
    if v not in ("true", "false"):
        err(f"{cid}: {field}={raw!r} 非法布尔（须 true/false）")
        return None
    return v == "true"


def check_commissions(rows):
    ids = set()
    for r in rows:
        cid = (r.get("commission_id") or "").strip()
        if not cid:
            err("commissions: 存在空 commission_id 行"); continue
        if cid in ids:
            err(f"{cid}: commission_id 重复")
        ids.add(cid)

        thr = num(cid, "reveal_threshold", r["reveal_threshold"])
        if thr is not None:
            if not (0 <= thr <= 1): err(f"{cid}: reveal_threshold={thr} 越界 [0,1]")
            elif thr != 0.85: print(f"  [warn] {cid}: reveal_threshold={thr} != 默认 0.85（确认是否有意）")
        fc = num(cid, "fragment_count", r["fragment_count"], int)
        if fc is not None and not (0 <= fc <= 6): err(f"{cid}: fragment_count={fc} 越界 [0,6]")
        cc = num(cid, "choice_count", r["choice_count"], int)
        if cc is not None and not (2 <= cc <= 3): err(f"{cid}: choice_count={cc} 越界 [2,3] → 拒绝激活")
        ev = num(cid, "ending_variants", r["ending_variants"], int)
        if ev is not None and ev < 2: err(f"{cid}: ending_variants<2")
        sb = num(cid, "session_soft_budget_min", r["session_soft_budget_min"])
        if sb is not None and not (5 <= sb <= 10): err(f"{cid}: session_soft_budget_min={sb} 越界 [5,10]")
        ch = num(cid, "chapter_index", r["chapter_index"], int)
        if ch is not None and ch < 1: err(f"{cid}: chapter_index={ch} < 1")
        boolean(cid, "is_daily", r["is_daily"])
        boolean(cid, "is_mainplot", r["is_mainplot"])
    return ids


def check_clients(rows):
    ids = set()
    for r in rows:
        cid = (r.get("client_id") or "").strip()
        if not cid:
            err("clients: 存在空 client_id 行"); continue
        if cid in ids:
            err(f"{cid}: client_id 重复")
        ids.add(cid)
        rl = num(cid, "relationship_level", r["relationship_level"], int)
        if rl is not None and not (0 <= rl <= 5): err(f"{cid}: relationship_level={rl} 越界 [0,5]")
        vc = num(cid, "visit_count", r["visit_count"], int)
        if vc is not None and vc < 0: err(f"{cid}: visit_count={vc} < 0")
        mp = num(cid, "mainplot_progress", r["mainplot_progress"], int)
        if mp is not None and mp < 0: err(f"{cid}: mainplot_progress={mp} < 0")
    return ids


def check_items(rows):
    types = {"Paper", "Clock", "Letter", "Ornament", "Other"}
    mats = {"Wood", "Paper", "Glass", "Dust"}
    ids = set()
    for r in rows:
        iid = (r.get("item_id") or "").strip()
        if not iid:
            err("items: 存在空 item_id 行"); continue
        if iid in ids:
            err(f"{iid}: item_id 重复")
        ids.add(iid)
        if r["item_type"] not in types: err(f"{iid}: item_type={r['item_type']} 非法")
        if r["material"] not in mats: err(f"{iid}: material={r['material']} 非法（须 Wood/Paper/Glass/Dust）")
        # dust_grid 尺寸：DustGrid.revealed 长度 = grid_w*grid_h，须 ≥1（S1 网格）
        gw = num(iid, "grid_w", r["grid_w"], int)
        gh = num(iid, "grid_h", r["grid_h"], int)
        if gw is not None and gw < 1: err(f"{iid}: grid_w={gw} < 1")
        if gh is not None and gh < 1: err(f"{iid}: grid_h={gh} < 1")
        boolean(iid, "is_mainplot", r["is_mainplot"])
    return ids


def check_r3_single_layer(data_dir):
    """C4：choices.csv 若存在，禁止任何二级引用列（R3 分支单层锁死）。"""
    path = os.path.join(data_dir, "choices.csv")
    if not os.path.isfile(path):
        print("-- choices.csv 未提供（R3 守护待内容就绪后生效）--")
        return
    have = [c.strip() for c in cols_of(path)]
    print(f"-- choices.csv ({len(have)} 列) --")
    for c in R3_FORBIDDEN_COLS:
        if c in have:
            err(f"choices.csv: 出现二级分支列 `{c}` → R3 单层锁死违规（C4）")


def main():
    data_dir = sys.argv[1] if len(sys.argv) > 1 else "game/Assets/Data"
    print(f"== 契约校验器 I1 :: {data_dir} ==")
    if not os.path.isdir(data_dir):
        print(f"  [FAIL] 数据目录不存在: {data_dir}"); sys.exit(1)

    paths = {n: os.path.join(data_dir, n) for n in REQUIRED_COLS}
    for n, p in paths.items():
        if not os.path.isfile(p):
            print(f"  [FAIL] 缺少数据表: {n}"); sys.exit(1)

    # 先做列体检：缺列直接判 FAIL 并退出（后续逐行校验会 KeyError）
    ok = all(check_columns(n, p) for n, p in paths.items())
    if not ok:
        print("== 结果 ==")
        print(f"FAIL：{len(FAIL)} 处契约违规（列缺失，先补表头）"); sys.exit(1)

    com = load(paths["commissions.csv"])
    cli = load(paths["clients.csv"])
    itm = load(paths["items.csv"])

    print(f"-- commissions ({len(com)}) --"); com_ok = check_commissions(com)
    print(f"-- clients ({len(cli)}) --");     cli_ok = check_clients(cli)
    print(f"-- items ({len(itm)}) --");       itm_ok = check_items(itm)

    # 引用完整性
    for r in com:
        if r["client_id"] not in cli_ok: err(f"{r['commission_id']}: 引用不存在 client_id={r['client_id']}")
        if r["item_id"] not in itm_ok: err(f"{r['commission_id']}: 引用不存在 item_id={r['item_id']}")
    for r in itm:
        if r["client_id"] not in cli_ok: err(f"{r['item_id']}: 引用不存在 client_id={r['client_id']}")

    # R3 单层（C4）
    check_r3_single_layer(data_dir)

    # MVP 量：3–5 委托，含 ≥1 条主线（S4 ④）
    if not (3 <= len(com) <= 5): err(f"MVP 委托数 {len(com)} 不在 [3,5]")
    if not any((r["is_mainplot"] or "").strip().lower() == "true" for r in com): err("缺少 is_mainplot=true 的主线委托")

    print("== 结果 ==")
    if FAIL:
        print(f"FAIL：{len(FAIL)} 处契约违规"); sys.exit(1)
    print(f"PASS：{len(com)} 委托 / {len(cli)} 客户 / {len(itm)} 物件，契约合规"); sys.exit(0)


if __name__ == "__main__":
    main()
