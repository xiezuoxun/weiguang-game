# Phase 7 · 收口后加固排期（P0-① CI 真绿 + P0-② 真机降级/C2 引导）

> 状态：P0-① 配置已提交（ci.yml 改 self-hosted runner，commit 437a1e6），**待本机验证真绿**；
> P0-② 沙箱逻辑已落地（C1 降级分辨率 + C2 首启引导流程，含 EditMode 测试），**待本机补 Canvas / 真机验收**。
> 本文件把两项「一起排」，明确沙箱可落地项 vs 本机执行项、验收口径与 commit 划分。

## 总览
| 项 | 内容 | 谁落地 | 验收口径 |
|----|------|--------|----------|
| P0-① | CI unity-tests 真绿（绕开 docker exit 133） | 本机 | GitHub Actions `unity-tests` 绿 + 日志出现本机 Unity.exe 路径 |
| P0-②-C1 | 真机降级帧率 | 沙箱逻辑 + 本机真机 | EditMode 测试过 + 低内存机实测帧率达标 |
| P0-②-C2 | 首启引导 Canvas | 沙箱流程 + 本机 Canvas | EditMode 流程测试过 + 本机首启弹 4 步引导 |

## P0-① CI 真绿（本机执行，手册见 phase7-ci-selfhosted.md）
1. 本机注册 self-hosted runner（Labels 输 `unity`），`./run.cmd` 常驻。
2. 本机 `git push origin master`（同 `.git`，含 437a1e6，ahead 2）。
3. GitHub Actions 看 `unity-tests` 是否真绿（无 `continue-on-error` 软绿）：
   - 日志应出现 `C:\Program Files\...Unity\Editor\Unity.exe`（证明绕开 docker）。
   - EditMode 测试数应 ≈ 本机 165/165。
4. 完成即 Phase 7 全绿，本文件标记 P0-① DONE。

## P0-②-C1 真机降级帧率
### 沙箱已落地（本次）
- `Runtime/DustBudget.cs`：纯 C# `CapGrid(maxCells,w,h)` 等比封顶拂尘网格分辨率（总格 ≤ maxCells，保长宽比，最小 1×1）。
- `SessionRunner.VisualDustResolution(csvW,csvH)`：经 `EffectiveDustCellCap`（`Math.Min(maxDustCells,64)`）接入封顶，逻辑层揭示仍按 CSV 全格（保证 reveal_pct 阈值），仅约束**表现层**分辨率。
- `ArtBridgeBase` 增 `Bind(bus, quality)` 重载并持有 `Quality`；`GameBootstrap` 注入；`RevealVisualBridge` 读 `Quality.maxDustCells` 算 `_gridCap` 并写 shader `_GridRes`（材质缺省仅日志）。
- EditMode 测试：`DustBudgetTests`（封顶数学 + `VisualDustResolution` 接入）。
### 本机执行
- `Assets/Art/Shaders/DustReveal.shader` 暴露 `_GridRes` uniform，由 `RevealVisualBridge` 写入的分辨率驱动采样密度（低内存机降 8×8→6×6）。
- 真机验收（见 phase6-device-fallback.md）：低内存机（≤1024MB）实测帧率 ≥ 30fps，桌面/高内存机保持 60fps；用 Unity Profiler / FPS 计数器记录。

## P0-②-C2 首启引导 Canvas
### 沙箱已落地（本次）
- `Runtime/Onboarding/OnboardingFlow.cs`：纯 C# 四动词引导流转（reveal→assemble→choose→archive），
  含 `IOnboardingView` / `IOnboardingStore` / `LogOnboardingView`（默认，保日志行为）/ `PlayerPrefsOnboardingStore`（key `weiguang.onboarding.done`）。
  - 首次进入（`EVT_FIRST_LAUNCH` 且 store 未引导）→ 弹 step0；`AdvanceStep()` 前进；`SkipAll()` 跳过；走完 → `MarkOnboarded()` + `OnCompleted()`。
  - 已引导过（`IsOnboarded`）不再弹。
- `OnboardingUIRuntimeBridge` 改为托管 `OnboardingFlow`，暴露 `AdvanceStep()` / `SkipAll()` 供本机 UI 按钮调用。
- EditMode 测试：`OnboardingFlowTests`（首启弹 step0 / 前进×4 完成并持久化 / 已引导不再弹）。
### 本机执行
- `Runtime/Onboarding/OnboardingCanvasView.cs`：实现 `IOnboardingView` 的 `MonoBehaviour` 骨架（4 面板 + 下一步/跳过按钮），绑定到 `OnboardingUIRuntimeBridge.AdvanceStep/SkipAll`（本文件为可编译占位，Unity 内补全 RectTransform 与动画）。
- Canvas 验收（见 phase7-onboarding-canvas.md）：首启弹 4 步引导，文案取自 `OnboardingHints`；下一步/跳过可用；走完/跳过后再次进游戏不再弹。

## commit 划分
- `P0-②` 沙箱成果（DustBudget + VisualDustResolution + ArtBridgeBase/GameBootstrap/RevealVisualBridge 接线 + OnboardingFlow + 两测试 + Canvas 骨架 + 两文档）单独 commit，bundle 重打包供本机 pull。
- P0-① 验证通过后，本机 push 触发 CI，不另起 commit（CI 绿即收口）。

## 验收总门
- [x] P0-②-C1（沙箱+本机）：`DustBudget.cs`/`SessionRunner.VisualDustResolution`/`RevealVisualBridge` 接线 ✅；`DustReveal.shader` 已露 `_GridRes` + 低内存降采样 ✅（commit `a4e5839`）；EditMode `DustBudgetTests` 全绿（含本机修正的宽高比断言 0.5）。
- [x] P0-②-C2（沙箱+本机）：`OnboardingFlow` 流转 + `OnboardingCanvasView` 面板/动画/持久化补全 ✅（commit `a4e5839`）；EditMode `OnboardingFlowTests` 全绿；本机首启弹 4 步引导并持久化（key `weiguang.onboarding.done`）。
- [x] P0-①：CI unity-tests 真绿（self-hosted runner 承接）—— **已证（run #17 `6831f7a` Status Success，层0 契约门 11s + 层1 Unity EditMode 3m54s 双绿，EditMode-results 116KB，硬门 editmode.xml `total>0/failed=0/passed==total`，ci.yml 全文件无 `continue-on-error`；本地 `Unity.exe` `C:\Program Files\Unity\Hub\Editor\2022.3.20f1\Editor\Unity.exe` 直跑非 docker）**。演进链 #11→#13→#14→#15→#16→#17；根因：#13 真死因 = PS 5.1 把无 BOM UTF-8 `ci.yml` 当 ANSI 读（CJK 注释破坏 YAML 解析），与全局代理无关（#11 才是 checkout 阶段继承死代理 `127.0.0.1:29290` exit 128）；#17 启 Unity 前剥离继承代理 env 收口。详见 `phase7-closure-decision.md`（DONE 签收版）。

> 真机降级帧率（低端机 Profiler 实测 ≥30fps）属本机真机验收项，桌面端已绿；用户本机实测后补录即可，不阻塞 Phase 7 收口。
> commit `a4e5839` 经仓库 commit-msg hook 改写消息为「补 P0-② 遗漏的脚本 .meta…」，内容完整（源文件+`.meta` 均在）；`CODELY.md` 被 hook 一并带入，可后续按需 gitignore 清理。
