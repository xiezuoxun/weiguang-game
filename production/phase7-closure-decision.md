# 《微光归处》Phase 7 收口裁定（Closure Decision）— DONE 签收版

> 裁定人：主理人 游承峰（Weiguang 工作室）
> 初裁日期：2026-09-03（背景：CI 排障中，P0-① 待 self-hosted runner 真绿）
> 升级日期：2026-09-03（DONE 签收版）
> 升级依据：CI run #17（`6831f7a`）Status **Success**，层0 + 层1 双绿；沙箱 WebFetch 实查 `xiezuoxun/weiguang-game/actions/workflows/ci.yml` 与 run #17 详情，确认真绿三判据齐备（self-hosted 本地 Unity.exe + 硬门 editmode.xml + 无 `continue-on-error`）；用户本机回报 P0-② C1/C2 完成（commit `a4e5839`）。
> 终验日期：2026-09-03（run #18 @ `8ae960c` 合并提交，四重判据齐备 + Phase 8 同步验证）

---

## 一、裁定结论

### ✅ Phase 7 整体：DONE（2026-09-03 签收）

**沙箱侧 P0-② 逻辑 + 本机验收全闭环；P0-① CI 真绿（self-hosted runner 本地 Unity 非 docker，硬门验证）已证；run #18 (`8ae960c`) 终验，四重判据齐备，Phase 7 P0-① 终端坐实。**
真机降级帧率（低端机 Profiler 实测 ≥30fps）属本机真机验收项，桌面端已绿，留作**发布前补录项**，不阻塞 Phase 7 收口。

### ✅ 沙箱侧 P0-② 逻辑：准予收口（已交付）

C1 降级分辨率封顶 + C2 首启引导流程 + 两 EditMode 测试，全部入库 + 本机 EditMode 全绿。

---

## 二、沙箱侧可裁定项（Confirmed）

| 域 | 交付物 | commit | 裁定 |
|---|---|---|---|
| P0-②-C1 降级分辨率 | `Runtime/DustBudget.cs`（`CapGrid` 等比封顶）+ `SessionRunner.VisualDustResolution` + `ArtBridgeBase.Bind(bus,quality)` 重载 + `GameBootstrap` 注入 + `RevealVisualBridge` 写 `_GridRes` + `DustBudgetTests` | `a4e5839` | ✅ |
| P0-②-C2 首启引导 | `Runtime/Onboarding/OnboardingFlow.cs`（四动词流转 + `IOnboardingView`/`IOnboardingStore` + `PlayerPrefsOnboardingStore` key `weiguang.onboarding.done`）+ `OnboardingUIRuntimeBridge` 托管 + `OnboardingCanvasView.cs` 骨架 + `OnboardingFlowTests` | `a4e5839` | ✅ |
| P0-① CI 真绿 | `ci.yml` 改 `self-hosted,unity` + 本地 `Unity.exe` 直跑（弃 game-ci docker）+ 硬门 `editmode.xml` + 去 CJK 注释（ASCII-only）+ 启 Unity 前剥离继承代理 env | `6831f7a`（最终稳定态，演进 #11→#17）+ `1954347`（合并线，proxy 剥离 + Start-Process） | ✅ |

---

## 三、本机验收回报（Confirmed）

- **C1**：`DustBudget` 封顶数学 + `VisualDustResolution` 接入，EditMode `DustBudgetTests` 全绿（含本机修正的宽高比断言 0.5）。`DustReveal.shader` 已露 `_GridRes` uniform + 低内存降采样（8×8→6×6）。
- **C2**：`OnboardingFlow` 流转 + `OnboardingCanvasView` 面板/动画/持久化补全，EditMode `OnboardingFlowTests` 全绿；本机首启弹 4 步引导（reveal→assemble→choose→archive）并持久化（key `weiguang.onboarding.done`）。
- 真机降级帧率（低内存机 Profiler ≥30fps）留补录（桌面端已绿）。

---

## 四、云端 CI 实查（2026-09-03，WebFetch `actions/workflows/ci.yml` + run #17 详情）

Latest run **#17（`6831f7a`）Status: Success，Total 3m 59s**

| Job | 状态 | 时长 |
|---|---|---|
| 层0 契约门 (I1) | ✅ Success | 11s |
| 层1 Unity EditMode 测试 (G4/G5/G7) | ✅ Success | 3m 54s |

- Artifact：`EditMode-results` **116 KB**（即 `editmode.xml` 经硬门放行，证明 `total>0` 且 `failed=0` 且 `passed==total`）
- 3 warnings（均非致命）：Node.js 20 deprecation ×2（`actions/*@v4` 被强制跑在 Node 24）；良性 tar 产物保存错 `C:\Program ... tar.exe exit code 2`（Artifacts upload 偶发，不影响判定）

**真绿三判据（齐备）：**
1. **layer1 Success** ✅（非 docker：`runs-on: [self-hosted, unity]`，`Unity.exe = C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe` 本地直跑，复用 self-hosted `weiguang-pc` 本机已激活 license）
2. **日志见本机 Unity.exe 路径** ✅（`ci.yml` `Run EditMode Tests` 步写明本地 Unity 绝对路径，佐证绕开 docker）
3. **无 `continue-on-error` 软绿** ✅（`ci.yml` 全文件无 `continue-on-error`；硬门 `exit 5`：`editmode.xml` 的 `total≤0` / `failed>0` / `passed<total` 任一即硬失败）

### CI 演进链（root cause 标注）
| Run | commit | 时长 | 死因 / 修复 |
|---|---|---|---|
| #11 | `a4e5839` | 26m31s | layer1 git checkout/clean `exit 128`：runner 继承死代理 `127.0.0.1:29290`（端口未监听）→ `github.com:443` 不通、ambiguous argument HEAD；Unity 未跑 |
| #13 | `19f97b2` | 1m41s | 层0✅ + Checkout✅（`actions/checkout@v4` 走 REST API，不经 git 代理）；真死因 ≠ 代理：`Check UNITY_LICENSE secret` 步抛异常（**PS 5.1 把无 BOM UTF-8 `ci.yml` 当 ANSI 读，CJK 注释破坏 YAML 解析**）→ Run EditMode Tests skipped |
| #14 | `575b070` | 1m32s | license-check 步骤改 ASCII-only（去 CJK 注释），触发即生效 |
| #15 | `ab99cff` | 1m57s | `Run EditMode Tests` 改调本机 `Unity.exe`（弃 game-ci docker action —— self-hosted 未装 docker → `Unable to locate executable file: docker`） |
| #16 | `bcd27d1` | 1m37s | 硬门加固：解析 `editmode.xml`（`total>0` / `failed=0` / `passed==total`）才放行，否则 `exit 5` |
| #17 | `6831f7a` | 3m59s | 启 Unity 前剥离继承的死 `HTTPS_PROXY` 等代理 env（避免 `Unity.Licensing.Client` 因 .NET Core 读代理变量 token 更新失败 → Unity `exit 0` 未跑测试） |

至此 CI 真绿稳定。

## 四（续）· 最终真绿复核（run #18 @ 8ae960c，Phase 8 合并提交）

> 复核日期：2026-09-03　|　head `8ae960c`（合并提交：本地 phase8 内容线 `d95ac8d` + bundle CI 硬门线 `1954347`）
> 触发：用户本机 pull `weiguang-p8.bundle` → merge `8ae960c` → `git push origin master`（代理绕过模式），共交付 6 提交；本地 `Packages/manifest.json` 的 Codely 本地引用改动按约束未提交。

run **#18（`8ae960c`）Status: Success，job id 100512685304**

| 验证点 | 证据 |
|---|---|
| layer1 Success | run #18 conclusion=success；层1 全步骤绿（含 `Run EditMode Tests`），层0 契约门 I1 同样全绿 |
| 日志见本机 Unity | `unity-editmode.log`（`C:\actions-runner\_work\weiguang-game\weiguang-game\artifacts\unity-editmode.log`，11:35:52）含 `COMMAND LINE ARGUMENTS: -projectPath / -runTests`；`Licensing Client` 成功启动（`LicenseClient-lenovo`, PId 54020）—— **proxy 剥离 + Start-Process 修复的直接证据** |
| 无 continue-on-error | `ci.yml` 全文件 `continue-on-error` 出现次数 = 0 |
| 硬门产出 editmode.xml | `total=183 / passed=183 / failed=0 / skipped=0 / duration=3.35s`（`C:\actions-runner\_work\weiguang-game\weiguang-game\artifacts\editmode.xml`，11:35:51）。183 = 165 基线 + **18 个 Phase 8 新增测试**（数据埋点 `AnalyticsTrackerTests` + 帧率探针相关），全过 |

**四重判据齐备 → Phase 7 P0-① CI 真绿 终端坐实。**

**Phase 8 同步验证（本次 run 的新增价值）：**
- 183 含 Phase 8 落地的 `AnalyticsTrackerTests`（数据埋点单测）与帧率探针相关编译/测试 → 证明 Runtime 层（`UnityAnalyticsSink` / `DeviceFpsProbe` / `GameBootstrap` 接线）可编译、`AnalyticsTracker` 逻辑实跑且过。
- 即 `8ae960c` 这一绿，同时收口「Phase 7 CI 真绿」+「Phase 8 工程侧可编译可测」，无需额外 CI run。
- 真机帧率（低端机 ≥30fps）、Canvas 真机 UI、美术/音频终版仍属本机/真机验收项（见 `release/phase8-release-checklist.md` A1–A4），不阻塞本次 CI 收口。

---

## 五、Phase 7 整体 DONE 签收

| 门 | 项 | 备注 |
|---|---|---|
| P0-②-C1 降级分辨率 | ✅ | 沙箱逻辑 + 本机 EditMode 全绿；`DustReveal.shader` 露 `_GridRes` 降采样 |
| P0-②-C2 首启引导 | ✅ | 沙箱流程 + 本机 Canvas 4 步引导 + 持久化（`weiguang.onboarding.done`） |
| P0-① CI 真绿 | ✅ | run #17 双绿 + **终验 run #18 @ 8ae960c：183/183 全过（含 18 Phase 8 新增）**，硬门验证，无 `continue-on-error` |
| 真机降级帧率补录 | 🟡 | 低内存机 Profiler ≥30fps，发布前补录（桌面端已绿） |

**所有"非阻塞留作补录项"已明确标记，不影响 Phase 7 整体 DONE 签收。**

---

## 六、复盘

详见 `phase7-retro.md`（建议同期提交）。关键教训：

1. **self-hosted runner 的 PowerShell 5.1 把无 BOM UTF-8 `ci.yml` 当 ANSI 读，CJK 注释破坏 YAML 解析** → CI `run:` 步骤直接抛异常退出。修复 = ASCII-only `ci.yml`（或加 BOM / 改用 bash shell）。这是 #13 真死因，与全局代理无关。
2. **game-ci/unity-test-runner 是 docker action**，self-hosted Windows 未装 docker → `Unable to locate executable file: docker`。改直接调本机 `Unity.exe` 批处理跑 EditMode（与本机手动命令一致，165/165 即 ground truth）。
3. **死代理 env 会被 runner 继承并破坏 Unity 激活**（.NET Core 读 `HTTPS_PROXY` → `Unity.Licensing.Client` token 更新失败 → Unity `exit 0` 未跑测试）。启 Unity 前剥离。
4. **绝不 self-signoff "绿"**：必须 `layer1 Success` + 日志见本机 `Unity.exe` 路径 + 无 `continue-on-error` 三判据齐备；并以 `editmode.xml` 硬门（`total>0 / failed=0 / passed==total`）二次印证。run #18 进一步把判据扩为四重（加 editmode.xml 实物数字复核），并将验证范围从 Phase 7 延伸到 Phase 8 工程侧。

---

## 七、Phase 8 状态（已落地，CI #18 验证）

> Phase 8 四件套已于 2026-09-03 全部落地并经 CI #18（`8ae960c`）真绿验证（详见 `production/phase8-*.md` 与 `release/`）。

| # | 原候选 | 状态 | 落地产物 |
|---|--------|------|----------|
| 1 | 真机补录（高优） | 🟡 代码就绪，待真机实测 | `DeviceFpsProbe` + `phase8-device-fps.md`（低端机 ≥30fps 验收线待回填） |
| 2 | 内容扩充 7→20 CSV | ✅ | 新增 12 表，`validate_contract` PASS（exit 0），旧 8 表未动 |
| 3 | 数据埋点 | ✅ | `Core/Analytics` + `Runtime/Analytics` + `GameBootstrap` 接线 + `AnalyticsTrackerTests`（CI #18 实跑通过） |
| 4 | 发布准备 | ✅ 文档齐 | `release/` 六篇（TapTap/Steam/WeGame/30s trailer/测试招募/总检表） |

---

### 下一步动作
- **用户**：本机真机按 `phase8-device-fps.md` 实测低端机帧率并回填；或接真实 Unity Analytics（设备包开 `UNITY_ANALYTICS`）；或按 `release/phase8-release-checklist.md` 推进三平台提审。
- **主理人**：`phase7-retro.md` 复盘文档（建议同期提交，见第六节）可补；其余按用户拍板推进 Phase 8 真机/发布项。
