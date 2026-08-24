#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
check_test_layout.py —— 测试装配不变量门（CI 层 0，无需 Unity）

守护"假绿灯"：Unity 对 asmdef 的处理是静默的，以下情况都不会让任何命令返回非零——
  · asmdef JSON 语法错误            → 整个装配不编译，Test Runner 一个用例都不出现
  · 测试装配丢 nunit / TestRunner 引用 → 同上
  · precompiledReferences 有 nunit 但 overrideReferences=false → 该列表被忽略，等于没引用
  · includePlatforms 被清空          → 测试被打进运行时包
  · Core 的 noEngineReferences 被关   → Core 悄悄依赖 UnityEngine，纯 C# 秒测前提破产
  · 测试文件被误删 / 用例被删空       → 用例数静默下降
所以必须显式断言。

用法：
  python tools/check_test_layout.py [project_dir]   # 默认当前目录（game/）
  python tools/check_test_layout.py --self-test     # 自测 asmdef 规则（不读工程）
退出码：0=PASS，1=FAIL，2=用法/路径错误
"""

import json
import os
import re
import sys

CORE = "Assets/Scripts/Core"
RUNTIME = "Assets/Scripts/Runtime"
TESTS = "Assets/Tests/EditMode"

# 必须存在的文件（相对 project_dir）
REQUIRED_FILES = [
    f"{CORE}/DataContract.cs",
    f"{CORE}/ContractGuardAssert.cs",
    f"{CORE}/SaveEngine.cs",
    f"{CORE}/CommissionStateMachine.cs",
    f"{CORE}/EventBus.cs",
    f"{CORE}/Weiguang.Core.asmdef",
    f"{RUNTIME}/Weiguang.Runtime.asmdef",
    f"{TESTS}/Weiguang.Tests.EditMode.asmdef",
    f"{TESTS}/TestKit.cs",
    f"{TESTS}/ContractGuardTests.cs",
    f"{TESTS}/SaveEngineTests.cs",
    f"{TESTS}/CommissionStateMachineTests.cs",
    f"{TESTS}/EventBusTests.cs",
    f"{TESTS}/SmokeTests.cs",
    "Assets/Data/commissions.csv",
    "Assets/Data/clients.csv",
    "Assets/Data/items.csv",
    "tools/validate_contract.py",
    "tools/test_validate_contract.py",
    "tools/check_naming.py",
    "docs/TEST_STRATEGY.md",
]

# 必须在 Core 里能找到的符号（按符号而非文件路径断言 —— 允许重构合并文件）
REQUIRED_SYMBOLS = [
    "class ContractGuard",
    "class ContractGuardAssert",
    "class ContractViolationException",
    "class SaveEngine",
    "interface ISaveStorage",
    "class CommissionStateMachine",
    "enum CommissionPhase",
    "enum SaveNode",
    "class EventBus",
    "class GameEvents",
]

# 每个测试文件至少应有的用例数下限（防"测试被悄悄删空"）
MIN_TESTS = {
    f"{TESTS}/ContractGuardTests.cs": 20,
    f"{TESTS}/SaveEngineTests.cs": 15,
    f"{TESTS}/CommissionStateMachineTests.cs": 12,
    f"{TESTS}/EventBusTests.cs": 6,
    f"{TESTS}/SmokeTests.cs": 5,
}


# ══════════════════════════════════════════════════════════════════════════
#  纯函数规则（可自测）
# ══════════════════════════════════════════════════════════════════════════
def check_core_asmdef(data):
    """Core 装配必须是纯 C#、无外部引用。"""
    out = []
    if data.get("name") != "Weiguang.Core":
        out.append(f"[asmdef] Core 装配名应为 Weiguang.Core，实为 {data.get('name')!r}")
    if data.get("noEngineReferences") is not True:
        out.append(
            "[asmdef] Weiguang.Core.noEngineReferences 必须为 true —— "
            "一旦为 false，Core 可引用 UnityEngine，纯 C# 秒级单测的前提即破"
        )
    if data.get("references"):
        out.append(f"[asmdef] Weiguang.Core 不得引用其他装配，实为 {data.get('references')}")
    return out


def check_runtime_asmdef(data):
    out = []
    if data.get("name") != "Weiguang.Runtime":
        out.append(f"[asmdef] Runtime 装配名应为 Weiguang.Runtime，实为 {data.get('name')!r}")
    if "Weiguang.Core" not in (data.get("references") or []):
        out.append("[asmdef] Weiguang.Runtime 必须引用 Weiguang.Core")
    return out


def check_tests_asmdef(data):
    """
    测试装配：Editor-only + 引用 Core + nunit + TestRunner + UNITY_INCLUDE_TESTS。
    nunit 在 Unity 里的标准位置是 precompiledReferences（需配 overrideReferences=true），
    也接受直接写进 references，两处任一命中即通过。
    """
    out = []
    refs = [str(r) for r in (data.get("references") or [])]
    pre = [str(r) for r in (data.get("precompiledReferences") or [])]

    if data.get("name") != "Weiguang.Tests.EditMode":
        out.append(f"[asmdef] 测试装配名应为 Weiguang.Tests.EditMode，实为 {data.get('name')!r}")
    if "Weiguang.Core" not in refs:
        out.append("[asmdef] 测试装配必须引用 Weiguang.Core，否则一个用例都编不过")

    has_nunit_pre = any("nunit" in r.lower() for r in pre)
    has_nunit_ref = any("nunit" in r.lower() for r in refs)
    if not (has_nunit_pre or has_nunit_ref):
        out.append(
            "[asmdef] 测试装配必须引用 nunit.framework.dll（references 或 precompiledReferences）"
            " —— 缺失时装配静默不编译，Test Runner 空列表而 CI 仍绿"
        )
    if has_nunit_pre and data.get("overrideReferences") is not True:
        out.append(
            "[asmdef] precompiledReferences 里有 nunit 但 overrideReferences 不为 true —— "
            "该列表会被 Unity 忽略，等于没引用"
        )

    for need in ("UnityEngine.TestRunner", "UnityEditor.TestRunner"):
        if need not in refs:
            out.append(f"[asmdef] 测试装配必须引用 {need}（EditMode 用例的发现与执行依赖它）")

    if data.get("includePlatforms") != ["Editor"]:
        out.append(
            f'[asmdef] 测试装配 includePlatforms 必须为 ["Editor"]，实为 {data.get("includePlatforms")}'
        )
    if "UNITY_INCLUDE_TESTS" not in (data.get("defineConstraints") or []):
        out.append("[asmdef] 测试装配 defineConstraints 必须含 UNITY_INCLUDE_TESTS")
    return out


def count_tests_in(text):
    """统计 [Test] / [TestCase] 属性数量（粗略但足以做下限守护；跳过注释行）。"""
    n = 0
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("//"):
            continue
        if s.startswith("[Test]") or s.startswith("[Test,") or s.startswith("[TestCase"):
            n += 1
    return n


# ══════════════════════════════════════════════════════════════════════════
#  文件系统扫描
# ══════════════════════════════════════════════════════════════════════════
def load_json_lenient(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        raw = f.read()
    try:
        return json.loads(raw), None
    except json.JSONDecodeError as e:
        return None, f"JSON 语法错误（第 {e.lineno} 行第 {e.colno} 列）：{e.msg}"


def run(root):
    if not os.path.isdir(root):
        print(f"FAIL: 目录不存在 {root}")
        return 2

    fails, notes = [], []

    def p(rel):
        return os.path.join(root, rel.replace("/", os.sep))

    # ── 1. 必备文件 ────────────────────────────────────────────────
    for rel in REQUIRED_FILES:
        if not os.path.isfile(p(rel)):
            fails.append(f"[缺文件] {rel} 不存在")

    # ── 2. Core 关键符号（按符号断言，容许文件重组）────────────────
    core_dir = p(CORE)
    core_src = ""
    if os.path.isdir(core_dir):
        for name in sorted(os.listdir(core_dir)):
            if name.endswith(".cs"):
                with open(os.path.join(core_dir, name), "r", encoding="utf-8-sig") as f:
                    core_src += f.read() + "\n"
    for sym in REQUIRED_SYMBOLS:
        kind, ident = sym.split(" ", 1)
        if not re.search(rf"\b{kind}\s+{re.escape(ident)}\b", core_src):
            fails.append(f"[缺符号] Core 中找不到 {sym}（被测对象缺失，测试将编译失败）")

    # ── 3~5. 三个 asmdef ───────────────────────────────────────────
    for rel, fn in (
        (f"{CORE}/Weiguang.Core.asmdef", check_core_asmdef),
        (f"{RUNTIME}/Weiguang.Runtime.asmdef", check_runtime_asmdef),
        (f"{TESTS}/Weiguang.Tests.EditMode.asmdef", check_tests_asmdef),
    ):
        f = p(rel)
        if not os.path.isfile(f):
            continue
        data, err = load_json_lenient(f)
        if err:
            fails.append(f"[asmdef] {os.path.basename(rel)} {err}")
        else:
            fails += fn(data)

    # 设计提示：层 1 只测纯 C# Core
    t = p(f"{TESTS}/Weiguang.Tests.EditMode.asmdef")
    if os.path.isfile(t):
        data, err = load_json_lenient(t)
        if not err and "Weiguang.Runtime" in (data.get("references") or []):
            notes.append(
                "测试装配引用了 Weiguang.Runtime —— 设计上层 1 只测纯 C# Core，"
                "如为新增 PlayMode 集成测试请另建装配"
            )

    # ── 6. 用例数下限 ──────────────────────────────────────────────
    total, per_file = 0, []
    for rel, lo in MIN_TESTS.items():
        f = p(rel)
        if not os.path.isfile(f):
            continue
        with open(f, "r", encoding="utf-8-sig") as fh:
            n = count_tests_in(fh.read())
        total += n
        per_file.append((os.path.basename(rel), n, lo))
        if n < lo:
            fails.append(f"[用例数] {rel} 仅 {n} 个用例，低于下限 {lo}（是否被误删？）")

    print(f"[check_test_layout] 工程根 {os.path.abspath(root)}")
    for name, n, lo in per_file:
        print(f"    {name:<34} {n:>3} 例（下限 {lo}）")
    print(f"[check_test_layout] EditMode 用例合计 {total} 个")
    for x in notes:
        print(f"  警告: {x}")
    if fails:
        print(f"\nFAIL: {len(fails)} 项装配/布局不变量被破坏\n")
        for x in fails:
            print("  " + x)
        return 1
    print("PASS: 测试装配不变量完好（Core 纯 C# / 测试 Editor-only / 符号齐备 / 用例数达标）")
    return 0


# ══════════════════════════════════════════════════════════════════════════
#  自测：谁来守护守护者
# ══════════════════════════════════════════════════════════════════════════
def self_test():
    GOOD_CORE = {"name": "Weiguang.Core", "references": [], "noEngineReferences": True}
    GOOD_RT = {"name": "Weiguang.Runtime", "references": ["Weiguang.Core"]}
    GOOD_TESTS = {
        "name": "Weiguang.Tests.EditMode",
        "references": ["Weiguang.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
        "includePlatforms": ["Editor"],
        "overrideReferences": True,
        "precompiledReferences": ["nunit.framework.dll"],
        "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    }

    def mut(base, **kw):
        d = dict(base)
        d.update(kw)
        return d

    cases = [
        ("Core 合规基线", check_core_asmdef, GOOD_CORE, 0),
        ("Core noEngineReferences=false 必须拦住", check_core_asmdef,
         mut(GOOD_CORE, noEngineReferences=False), 1),
        ("Core noEngineReferences 缺失必须拦住", check_core_asmdef,
         {"name": "Weiguang.Core", "references": []}, 1),
        ("Core 私自引用装配必须拦住", check_core_asmdef,
         mut(GOOD_CORE, references=["Unity.TextMeshPro"]), 1),
        ("Core 改名必须拦住", check_core_asmdef, mut(GOOD_CORE, name="Core"), 1),

        ("Runtime 合规基线", check_runtime_asmdef, GOOD_RT, 0),
        ("Runtime 丢 Core 引用必须拦住", check_runtime_asmdef, mut(GOOD_RT, references=[]), 1),

        ("测试 合规基线（nunit 在 precompiledReferences）", check_tests_asmdef, GOOD_TESTS, 0),
        ("测试 nunit 写在 references 也放过", check_tests_asmdef,
         mut(GOOD_TESTS,
             references=["Weiguang.Core", "UnityEngine.TestRunner", "UnityEditor.TestRunner",
                         "nunit.framework.dll"],
             precompiledReferences=[], overrideReferences=False), 0),
        ("测试 nunit 全丢必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, precompiledReferences=[]), 1),
        ("测试 有 nunit 但 overrideReferences=false 必须拦住（列表被忽略）", check_tests_asmdef,
         mut(GOOD_TESTS, overrideReferences=False), 1),
        ("测试 丢 UnityEngine.TestRunner 必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, references=["Weiguang.Core", "UnityEditor.TestRunner"]), 1),
        ("测试 丢 Core 引用必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, references=["UnityEngine.TestRunner", "UnityEditor.TestRunner"]), 1),
        ("测试 includePlatforms 被清空必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, includePlatforms=[]), 1),
        ("测试 includePlatforms 混入运行时平台必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, includePlatforms=["Editor", "Android"]), 1),
        ("测试 丢 UNITY_INCLUDE_TESTS 必须拦住", check_tests_asmdef,
         mut(GOOD_TESTS, defineConstraints=[]), 1),
        ("测试 同时丢 nunit 与两个 TestRunner —— 三处违规", check_tests_asmdef,
         mut(GOOD_TESTS, references=["Weiguang.Core"], precompiledReferences=[]), 3),
    ]

    print("[check_test_layout --self-test]")
    ok = 0
    for i, (desc, fn, data, expect) in enumerate(cases, start=1):
        got = fn(data)
        if len(got) == expect:
            ok += 1
            print(f"  ok  {i:2d}. {desc}")
        else:
            print(f"  FAIL {i:2d}. {desc} —— 期望 {expect} 处违规，实得 {len(got)}")
            for g in got:
                print(f"        · {g}")

    # 用例计数器自测
    counter_cases = [
        ("[Test] 与 [TestCase] 都计数", "[Test]\npublic void A(){}\n[TestCase(1)]\n[TestCase(2)]\npublic void B(int i){}\n", 3),
        ("注释掉的 [Test] 不计数", "// [Test]\npublic void A(){}\n", 0),
        ("[Test, Category(\"Slow\")] 计数", '[Test, Category("Slow")]\npublic void A(){}\n', 1),
        ("空文件计 0", "", 0),
    ]
    base = len(cases)
    for j, (desc, text, expect) in enumerate(counter_cases, start=1):
        got = count_tests_in(text)
        if got == expect:
            ok += 1
            print(f"  ok  {base + j:2d}. 用例计数：{desc}")
        else:
            print(f"  FAIL {base + j:2d}. 用例计数：{desc} —— 期望 {expect}，实得 {got}")

    total = len(cases) + len(counter_cases)
    print(f"\n{'PASS' if ok == total else 'FAIL'}: {ok}/{total} 自测用例通过")
    return 0 if ok == total else 1


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--self-test" in args:
        sys.exit(self_test())
    sys.exit(run(args[0] if args else "."))
