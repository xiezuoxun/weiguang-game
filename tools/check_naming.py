#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
check_naming.py —— 控制清单 C2/C3 静态守护门（CI 层 0，无需 Unity）

对应控制清单：
  C2 命名唯一守护：全工程禁止别名（如 reveal_pct 不得写成 progress）；代码审查 + CI grep 守护。
  C3 阈值唯一：reveal_threshold=0.85 仅由 S4 写入 Commission，S1 读、S3 判定，无硬编码副本。

为什么不用 grep：
  裸 grep 会把注释与字符串里的 "0.85" 当成硬编码（实测 DataContract.cs 的行尾注释即误报）。
  本脚本先做一遍 C# 词法剥离（行注释 / 块注释 / 普通字符串 / 逐字字符串 / 字符字面量），
  再在"纯代码"上匹配，误报为 0；同时把字符串字面量单独收集，用于 EVT_ 字面量门。

三道门：
  G3-a  硬编码阈值：Assets/Scripts 下的代码（非注释非字符串）出现 0.85 → FAIL
  G3-b  命名别名   ：出现 BANNED_ALIASES 中任一别名标识符 → FAIL
  G3-c  事件名字面量：Core/EventBus.cs 之外出现 "EVT_..." 字符串字面量 → FAIL（必须引用 GameEvents 常量）
  附加   EventBus 名值一致：public const string EVT_X = "EVT_X"; 名与值必须相等 → 否则 FAIL

用法：
  python tools/check_naming.py [scripts_dir]     # 默认 Assets/Scripts
  python tools/check_naming.py --self-test       # 自测词法器与规则（不读工程）
退出码：0=PASS，1=FAIL，2=用法/路径错误
"""

import os
import re
import sys

# ── 允许豁免的文件（相对 scripts_dir，正斜杠）─────────────────────────────
#   EventBus.cs 是事件名常量的唯一定义处，字面量在此处合法。
EVENT_NAME_OWNER = "Core/EventBus.cs"

# ── C3：唯一阈值。数据侧写在 commissions.csv，代码侧一律读 Commission.reveal_threshold ──
THRESHOLD_PATTERN = re.compile(r"(?<![\w.])(?:0?\.85|0\.850+)[fFdDmM]?(?![\w.])")

# ── C2：禁止的别名标识符（左列别名 → 右列唯一命名）───────────────────────
BANNED_ALIASES = {
    "revealProgress": "reveal_pct / RevealPct()",
    "reveal_progress": "reveal_pct / RevealPct()",
    "dustProgress": "reveal_pct / RevealPct()",
    "dust_progress": "reveal_pct / RevealPct()",
    "dustPct": "reveal_pct / RevealPct()",
    "dust_pct": "reveal_pct / RevealPct()",
    "cleanPct": "reveal_pct / RevealPct()",
    "clean_pct": "reveal_pct / RevealPct()",
    "revealThreshold": "reveal_threshold",
    "threshold_reveal": "reveal_threshold",
    "fragCount": "fragment_count",
    "frag_count": "fragment_count",
    "fragmentNum": "fragment_count",
    "fragment_num": "fragment_count",
    "choiceNum": "choice_count",
    "choice_num": "choice_count",
    "endingCount": "ending_variants",
    "ending_count": "ending_variants",
    "endingNum": "ending_variants",
    "isMainPlot": "is_mainplot",
    "isMainplot": "is_mainplot",
    "is_main_plot": "is_mainplot",
    "main_plot": "is_mainplot / mainplot_progress",
    "mainPlotProgress": "mainplot_progress",
    "commissionPhase": "phase（字段名）/ CommissionPhase（类型名）",
    "saveNode": "save_node（字段名）/ SaveNode（类型名）",
}
_ALIAS_RE = re.compile(r"(?<![\w])(" + "|".join(map(re.escape, BANNED_ALIASES)) + r")(?![\w])")

_EVT_LITERAL_RE = re.compile(r"^EVT_[A-Z0-9_]*$")
_CONST_RE = re.compile(r'public\s+const\s+string\s+(EVT_\w+)\s*=\s*"([^"]*)"\s*;')


# ══════════════════════════════════════════════════════════════════════════
#  C# 词法剥离
# ══════════════════════════════════════════════════════════════════════════
def scan(src):
    """
    剥离注释与字符串。
    返回 (code_lines, string_literals)：
      code_lines      : list[str]，与源文件行一一对应；注释与字符串内容被替换为空格（保列号）
      string_literals : list[(line_no, text)]，1-based 行号 + 字面量内容（未反转义）
    """
    code = []            # 当前行的代码字符
    code_lines = []
    strings = []
    cur_str = None       # (start_line, [chars])
    line = 1
    i, n = 0, len(src)
    state = "N"          # N=normal L=line-comment B=block-comment S=string V=verbatim C=char

    def newline():
        nonlocal code
        code_lines.append("".join(code))
        code = []

    while i < n:
        ch = src[i]
        nxt = src[i + 1] if i + 1 < n else ""

        if ch == "\n":
            if state == "L":
                state = "N"
            # 块注释 / 逐字字符串可跨行；普通字符串按 C# 规则不可跨行（源码若跨行说明本就不合法，容错处理）
            if state == "S":
                state = "N"
                if cur_str:
                    strings.append((cur_str[0], "".join(cur_str[1])))
                    cur_str = None
            if state == "V" and cur_str:
                cur_str[1].append("\n")
            newline()
            line += 1
            i += 1
            continue

        if state == "N":
            if ch == "/" and nxt == "/":
                state = "L"; code.append("  "); i += 2; continue
            if ch == "/" and nxt == "*":
                state = "B"; code.append("  "); i += 2; continue
            if ch == "@" and nxt == '"':
                state = "V"; cur_str = (line, []); code.append("  "); i += 2; continue
            if ch == '"':
                state = "S"; cur_str = (line, []); code.append(" "); i += 1; continue
            if ch == "'":
                state = "C"; code.append(" "); i += 1; continue
            code.append(ch); i += 1; continue

        if state == "L":
            code.append(" "); i += 1; continue

        if state == "B":
            if ch == "*" and nxt == "/":
                state = "N"; code.append("  "); i += 2; continue
            code.append(" "); i += 1; continue

        if state == "S":
            if ch == "\\":                      # 转义：吞掉两个字符
                if cur_str: cur_str[1].append(src[i:i + 2])
                code.append("  "); i += 2; continue
            if ch == '"':
                state = "N"
                if cur_str:
                    strings.append((cur_str[0], "".join(cur_str[1])))
                    cur_str = None
                code.append(" "); i += 1; continue
            if cur_str: cur_str[1].append(ch)
            code.append(" "); i += 1; continue

        if state == "V":
            if ch == '"' and nxt == '"':        # 逐字字符串里的 "" 表示一个引号
                if cur_str: cur_str[1].append('"')
                code.append("  "); i += 2; continue
            if ch == '"':
                state = "N"
                if cur_str:
                    strings.append((cur_str[0], "".join(cur_str[1])))
                    cur_str = None
                code.append(" "); i += 1; continue
            if cur_str: cur_str[1].append(ch)
            code.append(" "); i += 1; continue

        if state == "C":
            if ch == "\\":
                code.append("  "); i += 2; continue
            if ch == "'":
                state = "N"
            code.append(" "); i += 1; continue

    newline()
    return code_lines, strings


# ══════════════════════════════════════════════════════════════════════════
#  规则
# ══════════════════════════════════════════════════════════════════════════
def check_source(rel_path, src):
    """对单个 .cs 源码执行三道门，返回 violation 字符串列表。"""
    out = []
    code_lines, strings = scan(src)
    rel = rel_path.replace("\\", "/")

    for no, text in enumerate(code_lines, start=1):
        if THRESHOLD_PATTERN.search(text):
            out.append(
                f"[C3] {rel}:{no} 硬编码阈值 0.85 —— 阈值唯一由 S4 写入 Commission.reveal_threshold，"
                f"代码侧必须读字段：{text.strip()[:80]}"
            )
        m = _ALIAS_RE.search(text)
        if m:
            alias = m.group(1)
            out.append(
                f"[C2] {rel}:{no} 命名别名 '{alias}' —— 唯一命名应为 '{BANNED_ALIASES[alias]}'"
            )

    if rel != EVENT_NAME_OWNER:
        for no, text in strings:
            if _EVT_LITERAL_RE.match(text):
                out.append(
                    f"[C2] {rel}:{no} 事件名字面量 \"{text}\" —— 必须引用 GameEvents.{text} 常量"
                )
    else:
        for m in _CONST_RE.finditer(src):
            name, value = m.group(1), m.group(2)
            if name != value:
                out.append(
                    f"[C2] {rel} 事件常量名值不一致：{name} = \"{value}\"（名与值必须逐字相等）"
                )

    return out


def run(scripts_dir):
    if not os.path.isdir(scripts_dir):
        print(f"FAIL: 目录不存在 {scripts_dir}")
        return 2

    violations, files = [], 0
    for root, _dirs, names in os.walk(scripts_dir):
        for name in sorted(names):
            if not name.endswith(".cs"):
                continue
            full = os.path.join(root, name)
            rel = os.path.relpath(full, scripts_dir)
            files += 1
            with open(full, "r", encoding="utf-8-sig") as f:
                violations += check_source(rel, f.read())

    print(f"[check_naming] 扫描 {files} 个 .cs（{scripts_dir}）")
    if violations:
        print(f"FAIL: {len(violations)} 处 C2/C3 违规\n")
        for v in violations:
            print("  " + v)
        print("\n参考：docs/architecture/控制清单.md C2（命名唯一）/ C3（阈值唯一）")
        return 1
    print("PASS: C2 命名唯一 / C3 阈值唯一 无违规")
    return 0


# ══════════════════════════════════════════════════════════════════════════
#  自测：词法器 + 三道门（正向应放过，负向应拦住）
# ══════════════════════════════════════════════════════════════════════════
def self_test():
    cases = []  # (说明, 文件相对路径, 源码, 期望违规数)

    # ── 正向：合法写法必须放过 ────────────────────────────────────────
    cases.append(("注释里的 0.85 不算硬编码", "Core/DataContract.cs",
                  'public class A { public float reveal_threshold; // 默认 0.85，S4 写入\n}', 0))
    cases.append(("块注释里的 0.85 与别名不算违规", "Core/A.cs",
                  '/* 阈值 0.85，旧名 revealThreshold 已废弃 */\npublic class A {}\n', 0))
    cases.append(("字符串里的 0.85 不算硬编码（提示文案）", "Core/A.cs",
                  'class A { void M(){ Log("需拂至 0.85"); } }', 0))
    cases.append(("读字段而非硬编码 —— 合法", "Core/A.cs",
                  'class A { bool Ok(Commission c, float pct){ return pct >= c.reveal_threshold; } }', 0))
    cases.append(("引用 GameEvents 常量 —— 合法", "Runtime/A.cs",
                  'class A { void M(){ Bus.Publish(GameEvents.EVT_PHASE_CHANGED, null); } }', 0))
    cases.append(("EventBus 自身定义字面量 —— 合法", EVENT_NAME_OWNER,
                  'class GameEvents { public const string EVT_SAVE_WRITTEN = "EVT_SAVE_WRITTEN"; }', 0))
    cases.append(("唯一命名 reveal_pct / fragment_count —— 合法", "Core/A.cs",
                  'class A { public float reveal_pct; public int fragment_count; public bool is_mainplot; '
                  'public int mainplot_progress; }', 0))
    cases.append(("逐字字符串里的别名不算违规", "Runtime/A.cs",
                  'class A { string s = @"legacy key: revealThreshold"; }', 0))
    cases.append(("0.8/0.9/0.855 等非阈值数不误报", "Core/A.cs",
                  'class A { float a = 0.8f, b = 0.9f, c = 0.855f, d = 1.85f, e = 10.85f; }', 0))
    cases.append(("含 0.85 的标识符不误报", "Core/A.cs",
                  'class A { const string K = "k"; int v0_85 = 1; }', 0))

    # ── 负向：违规必须拦住 ────────────────────────────────────────────
    cases.append(("C3 硬编码 0.85f", "Runtime/A.cs",
                  'class A { bool Ok(float p){ return p >= 0.85f; } }', 1))
    cases.append(("C3 硬编码 .85f（省略前导 0）", "Runtime/A.cs",
                  'class A { float t = .85f; }', 1))
    cases.append(("C3 硬编码 0.85（无后缀）", "Core/A.cs",
                  'class A { double t = 0.85; }', 1))
    cases.append(("C3 尾注释后仍拦住同行代码里的硬编码", "Core/A.cs",
                  'class A { float t = 0.85f; // 阈值\n}', 1))
    cases.append(("C2 别名 revealThreshold", "Core/A.cs",
                  'class A { public float revealThreshold; }', 1))
    cases.append(("C2 别名 fragCount", "Core/A.cs",
                  'class A { public int fragCount; }', 1))
    cases.append(("C2 别名 isMainPlot", "Core/A.cs",
                  'class A { public bool isMainPlot; }', 1))
    cases.append(("C2 别名 dustProgress", "Runtime/A.cs",
                  'class A { float dustProgress; }', 1))
    cases.append(("C2 别名 ending_count", "Core/A.cs",
                  'class A { int ending_count; }', 1))
    cases.append(("C2 EVT_ 字面量出现在业务代码", "Core/CommissionStateMachine.cs",
                  'class A { void M(){ Bus.Publish("EVT_CONTRACT_WARN", null); } }', 1))
    cases.append(("C2 EVT_ 字面量出现在 Runtime", "Runtime/A.cs",
                  'class A { void M(){ Bus.Publish("EVT_SAVE_FAILED", null); } }', 1))
    cases.append(("C2 EventBus 名值不一致", EVENT_NAME_OWNER,
                  'class GameEvents { public const string EVT_SAVE_WRITTEN = "EVT_SAVE_OK"; }', 1))
    cases.append(("同行两类违规各计一次（0.85 + 别名）", "Runtime/A.cs",
                  'class A { float revealThreshold = 0.85f; }', 2))
    cases.append(("未闭合块注释吞掉后续内容，不误报也不崩", "Core/A.cs",
                  'class A {} /* 0.85 revealThreshold', 0))

    print("[check_naming --self-test]")
    ok = 0
    for i, (desc, rel, src, expect) in enumerate(cases, start=1):
        got = check_source(rel, src)
        if len(got) == expect:
            ok += 1
            print(f"  ok  {i:2d}. {desc}")
        else:
            print(f"  FAIL {i:2d}. {desc} —— 期望 {expect} 处违规，实得 {len(got)}")
            for g in got:
                print(f"        · {g}")
    print(f"\n{'PASS' if ok == len(cases) else 'FAIL'}: {ok}/{len(cases)} 自测用例通过")
    return 0 if ok == len(cases) else 1


if __name__ == "__main__":
    args = [a for a in sys.argv[1:]]
    if "--self-test" in args:
        sys.exit(self_test())
    target = args[0] if args else os.path.join("Assets", "Scripts")
    sys.exit(run(target))
