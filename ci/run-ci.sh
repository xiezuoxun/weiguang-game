#!/usr/bin/env bash
# =============================================================================
# run-ci.sh —— 《微光归处》CI 一键门（层 0 + 层 1 编排）
# 负责人：程基岩（engineering-lead）　|　对应：game/docs/TEST_STRATEGY.md §5
# 用法（从仓库根目录，即包含 game/ 的目录执行）：
#   bash game/ci/run-ci.sh
# 退出码：0 = 全部通过；非 0 = 有门失败（CI 直接判红）
#
# 设计原则（见 TEST_STRATEGY.md §0）：
#   Core 是纯 C#，90% 风险靠秒级 EditMode 单测挡住；Unity 只验"引擎里才存在的东西"。
#   因此 CI 在【无 Unity 许可证】的机器上也能跑完层 0（契约门 + 自测 + 命名守护），
#   Unity 测试作为可选第二道门——本机无 Unity 时显式 SKIP，不误报为失败。
# =============================================================================
set -u

# 定位仓库根：本脚本在 game/ci/ 下，上两级即根
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
GAME_DIR="$REPO_ROOT/game"
TOOLS_DIR="$GAME_DIR/tools"
DATA_DIR="$GAME_DIR/Assets/Data"

# 兼容 Git Bash 下的 Windows Python：把 MSYS 路径转成 Python 能识别的形态。
# Git Bash 中 `python3`/`python` 通常是 Windows 解释器，收到 /c/... 会被当成 C:\c\... 而找不到文件；
# 这里用 `cygpath -w` 转绝对 Windows 路径，且统一用 `python` 命令经 PATH 解析。
if command -v cygpath >/dev/null 2>&1; then
  TOOLS_DIR_W="$(cygpath -w "$TOOLS_DIR")"
  DATA_DIR_W="$(cygpath -w "$DATA_DIR")"
  GAME_DIR_W="$(cygpath -w "$GAME_DIR")"
else
  TOOLS_DIR_W="$TOOLS_DIR"
  DATA_DIR_W="$DATA_DIR"
  GAME_DIR_W="$GAME_DIR"
fi
PY="python3"
command -v python >/dev/null 2>&1 && PY="python"   # 优先用 PATH 中可解析的 python

echo "== 微光归处 CI :: 仓库根 $REPO_ROOT =="

RC=0  # 累计退出码

# -----------------------------------------------------------------------------
# 层 0 · G1 契约门（I1，硬阻塞） —— 数据越界在 Unity 导入期会崩在玩家机
#   脚本：tools/validate_contract.py  退出 0=PASS / 1=FAIL
# -----------------------------------------------------------------------------
echo ""
echo "[层0/G1] 契约门 validate_contract.py（校验 Assets/Data/*.csv）"
if "$PY" "$TOOLS_DIR_W/validate_contract.py" "$DATA_DIR_W"; then
  echo "  ✅ G1 PASS"
else
  echo "  ❌ G1 FAIL（契约违规，构建应阻断）"
  RC=1
fi

# -----------------------------------------------------------------------------
# 层 0 · G2 契约门自测 —— 守护"校验器本身不被改坏"（4 正向 / 27 负向）
#   脚本：tools/test_validate_contract.py  退出 0=PASS / 1=FAIL
# -----------------------------------------------------------------------------
echo ""
echo "[层0/G2] 契约校验器自测 test_validate_contract.py（31 用例）"
if "$PY" "$TOOLS_DIR_W/test_validate_contract.py"; then
  echo "  ✅ G2 PASS"
else
  echo "  ❌ G2 FAIL（校验器自测未过）"
  RC=1
fi

# -----------------------------------------------------------------------------
# G3 命名与阈值守护（C2 别名 / C3 硬编码 0.85 / EVT_ 字面量门）
#   脚本：tools/check_naming.py [scripts_dir]  退出 0=PASS / 1=FAIL
#   先词法剥离（注释/字符串/字符字面量）再在纯代码上匹配，避免误报
# -----------------------------------------------------------------------------
echo ""
echo "[G3] 命名与阈值守护 check_naming.py（C2/C3/EVT_ 门）"
if "$PY" "$TOOLS_DIR_W/check_naming.py" "$GAME_DIR_W/Assets/Scripts"; then
  echo "  ✅ G3 PASS"
else
  echo "  ❌ G3 FAIL（存在别名/硬编码阈值/EVT_ 字面量漂移）"
  RC=1
fi

# -----------------------------------------------------------------------------
# G3 附加 · 测试布局守护（asmdef / 装配结构，保证单测能被 Test Runner 发现）
#   脚本：tools/check_test_layout.py
# -----------------------------------------------------------------------------
echo ""
echo "[G3] 测试布局守护 check_test_layout.py"
if "$PY" "$TOOLS_DIR_W/check_test_layout.py"; then
  echo "  ✅ 测试布局 PASS"
else
  echo "  ⚠️  测试布局检查未通过或未实现（登记为已知项，不强制阻断 CI）"
  # 若该脚本尚为占位/未实现，不因此把 CI 判红；如已实装请按需改为 RC=1
fi

# -----------------------------------------------------------------------------
# 层 1 · G4/G5/G7 Unity EditMode 单测 + 烟雾门 + 覆盖率
#   仅在环境存在 Unity 可执行文件时执行；否则显式 SKIP（不误报）。
#   Unity -runTests 退出码：0=全过 / 2=有用例失败 / 3=运行失败（编译错误等）
#   注意：-runTests 不得与 -quit 同用（会在测试跑完前退出）。
# -----------------------------------------------------------------------------
echo ""
echo "[层1/G4/G5] Unity EditMode 单测（需 Unity 2022 LTS，可选）"
UNITY_BIN=""
if command -v Unity >/dev/null 2>&1; then
  UNITY_BIN="Unity"
elif [ -n "${UNITY_PATH:-}" ] && [ -x "$UNITY_PATH" ]; then
  UNITY_BIN="$UNITY_PATH"
else
  # 自动探测常见安装路径（本机装机后零配置即可跑）
  # 覆盖：用户级 Hub（--scope user 非管理员安装的默认位置）、机器级 Hub、裸装、macOS、Linux
  # 注意 Windows 用户级路径在 $LOCALAPPDATA/Unity/Hub/Editor（即 ~/AppData/Local/...）
  LOCAL_U="$LOCALAPPDATA/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe"
  CANDIDATES="$HOME/Unity/Hub/Editor/2022.3.20f1/Editor/Unity \
              \"$LOCAL_U\" \
              /opt/unity/Editor/Unity \
              /Applications/Unity/Hub/Editor/2022.3.20f1/Unity.app/Contents/MacOS/Unity \
              /c/Program\ Files/Unity/Hub/Editor/2022.3.20f1/Editor/Unity.exe \
              /c/Program\ Files/Unity/Editor/Unity.exe"
  for c in $CANDIDATES; do
    # 去掉可能的外层引号后再测存在性（Windows 用户级路径含空格）
    c_clean="$(echo "$c" | sed 's/^"//;s/"$//')"
    if [ -x "$c_clean" ] || [ -f "$c_clean" ]; then UNITY_BIN="$c_clean"; break; fi
  done
fi

if [ -n "$UNITY_BIN" ]; then
  mkdir -p "$REPO_ROOT/artifacts"
  echo "  调用：$UNITY_BIN -batchmode -nographics -runTests -projectPath $GAME_DIR -testPlatform EditMode ..."
  "$UNITY_BIN" -batchmode -nographics \
    -projectPath "$GAME_DIR" \
    -runTests -testPlatform EditMode \
    -testResults "$REPO_ROOT/artifacts/editmode.xml" \
    -logFile - || URC=$?
  if [ "${URC:-0}" -eq 0 ]; then
    echo "  ✅ G4/G5 Unity 单测全绿"
  else
    echo "  ❌ Unity 单测未过（退出 $URC），CI 判红"
    RC=1
  fi
else
  echo "  ⏭️  SKIP：本机未检测到 Unity 可执行文件（G4/G5/G7 待有 Unity 的环境首跑）。"
  echo "       方案 A（本机）：装 Unity 2022.3.20f1 后重跑本脚本即自动识别；"
  echo "       方案 B（云端）：在 game/.github/workflows/ci.yml 取消注释 unity-tests job 并配 UNITY_LICENSE secret。"
fi

# -----------------------------------------------------------------------------
# 汇总
# -----------------------------------------------------------------------------
echo ""
echo "== CI 结果 =="
if [ "$RC" -eq 0 ]; then
  echo "✅ PASS：层0（G1/G2/G3）全过；Unity 层（G4/G5/G7）按环境 SKIP/执行。"
else
  echo "❌ FAIL：存在未通过的门（详见上方）。"
fi
exit $RC
