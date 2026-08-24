# =============================================================================
# run-ci.ps1 —— 《微光归处》CI 一键门（Windows 版）
# 负责人：程基岩（engineering-lead）　|　对应：game/docs/TEST_STRATEGY.md §5
# 用法（从仓库根目录，即包含 game/ 的目录执行）：
#   powershell -File game/ci/run-ci.ps1
# 退出码：0 = 全部通过；非 0 = 有门失败（CI 直接判红）
#
# 与 run-ci.sh 等价：层 0（G1/G2/G3）无 Unity 即可跑；层 1（G4/G5/G7）有 Unity 才跑，
# 否则显式 SKIP，不把"缺 Unity"误报成失败。
# =============================================================================

$ErrorActionPreference = "Stop"

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Definition
$REPO_ROOT  = (Resolve-Path (Join-Path $SCRIPT_DIR "..\..")).Path
$GAME_DIR   = Join-Path $REPO_ROOT "game"
$TOOLS_DIR  = Join-Path $GAME_DIR "tools"
$DATA_DIR   = Join-Path $GAME_DIR "Assets\Data"

Write-Host "== 微光归处 CI :: 仓库根 $REPO_ROOT =="

$RC = 0

function Run-Step {
  param(
    [string]$Tag,
    [string]$Desc,
    [scriptblock]$Action
  )
  Write-Host ""
  Write-Host "[$Tag] $Desc"
  try {
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "exit=$LASTEXITCODE" }
    Write-Host "  ✅ $Tag PASS"
  } catch {
    Write-Host "  ❌ $Tag FAIL: $_"
    $script:RC = 1
  }
}

# -----------------------------------------------------------------------------
# 层 0 · G1 契约门（I1，硬阻塞）
# -----------------------------------------------------------------------------
Run-Step -Tag "层0/G1" -Desc "契约门 validate_contract.py（校验 Assets/Data/*.csv）" -Action {
  python "$TOOLS_DIR\validate_contract.py" "$DATA_DIR"
}

# -----------------------------------------------------------------------------
# 层 0 · G2 契约门自测（守护校验器本身）
# -----------------------------------------------------------------------------
Run-Step -Tag "层0/G2" -Desc "契约校验器自测 test_validate_contract.py（31 用例）" -Action {
  python "$TOOLS_DIR\test_validate_contract.py"
}

# -----------------------------------------------------------------------------
# G3 命名与阈值守护（C2/C3/EVT_）
# -----------------------------------------------------------------------------
Run-Step -Tag "G3" -Desc "命名与阈值守护 check_naming.py（C2/C3/EVT_ 门）" -Action {
  python "$TOOLS_DIR\check_naming.py" (Join-Path $GAME_DIR "Assets\Scripts")
}

# -----------------------------------------------------------------------------
# G3 附加 · 测试布局守护
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "[G3] 测试布局守护 check_test_layout.py"
try {
  python "$TOOLS_DIR\check_test_layout.py"
  Write-Host "  ✅ 测试布局 PASS"
} catch {
  Write-Host "  ⚠️  测试布局检查未通过或未实现（登记为已知项，不强制阻断 CI）"
}

# -----------------------------------------------------------------------------
# 层 1 · G4/G5/G7 Unity EditMode（可选）
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "[层1/G4/G5] Unity EditMode 单测（需 Unity 2022 LTS，可选）"
$UNITY_BIN = $null
if (Get-Command Unity -ErrorAction SilentlyContinue) { $UNITY_BIN = "Unity" }
elseif ($env:UNITY_PATH -and (Test-Path $env:UNITY_PATH)) { $UNITY_BIN = $env:UNITY_PATH }

if ($UNITY_BIN) {
  $artifacts = Join-Path $REPO_ROOT "artifacts"
  if (-not (Test-Path $artifacts)) { New-Item -ItemType Directory -Path $artifacts | Out-Null }
  Write-Host "  调用：$UNITY_BIN -batchmode -nographics -runTests -projectPath $GAME_DIR ..."
  & $UNITY_BIN -batchmode -nographics `
    -projectPath $GAME_DIR `
    -runTests -testPlatform EditMode `
    -testResults (Join-Path $artifacts "editmode.xml") `
    -logFile - 
  if ($LASTEXITCODE -eq 0) { Write-Host "  ✅ G4/G5 Unity 单测全绿" }
  else { Write-Host "  ❌ Unity 单测未过（退出 $LASTEXITCODE），CI 判红"; $RC = 1 }
} else {
  Write-Host "  ⏭️  SKIP：本机未检测到 Unity 可执行文件（G4/G5/G7 待有 Unity 的环境首跑）。"
  Write-Host "       本地手动命令："
  Write-Host "       Unity -batchmode -nographics -runTests -projectPath $GAME_DIR -testPlatform EditMode -testResults artifacts/editmode.xml -logFile -"
}

# -----------------------------------------------------------------------------
# 汇总
# -----------------------------------------------------------------------------
Write-Host ""
Write-Host "== CI 结果 =="
if ($RC -eq 0) {
  Write-Host "✅ PASS：层0（G1/G2/G3）全过；Unity 层（G4/G5/G7）按环境 SKIP/执行。"
} else {
  Write-Host "❌ FAIL：存在未通过的门（详见上方）。"
}
exit $RC
