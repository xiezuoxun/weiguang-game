# Phase 6-C 真机中低端设备帧率验收手册

> 范围：低端机/移动端降级（glow/dust/codex/choice 四开关）的**真机帧率**验收。
> 沙箱限制：本工程沙箱**无 Unity 运行时、无 GPU、无真机**，以下仅手册；必须由**用户本机（Unity Editor）或真机**跑。
> 配套代码：降级开关 `RuntimeQuality.ForDevice` + `SessionRunner.EffectiveDustCellCap`（见 `Assets/Scripts/Runtime/RuntimeQuality.cs` / `SessionRunner.cs`）；
> 单测 `Assets/Tests/EditMode/RuntimeQualityFallbackTests.cs` 已覆盖 `ForDevice` 数学与 `EffectiveDustCellCap` 接入（不依赖真机）。

## 1. 目标档位

| 档位 | 示例设备 | 内存 | 预期降级 |
| --- | --- | --- | --- |
| Android 中端 | 如 6GB 机型 | ≥2048 MB | glow ON / dust 64 / choice ON（基本全开） |
| Android 低端 | 如 3GB 机型 | ~3072 MB | 注意：3GB 仍 ≥2048，按当前 `ForDevice` 仍全开；1–2GB 机才触发 glow off / dust 36 |
| iOS 旧机型 | 如 iPhone 8 | — | 走 `Application.isMobilePlatform` + `SystemInfo.systemMemorySize` 推断 |

> 说明：当前 `ForDevice` 阈值（glow/dust 以 2048MB 为界、choice 以 1536MB 为界）偏保守；**3GB 设备不会被降级**。
> 若需对 3–4GB 中端机也降 glow，请在 `RuntimeQuality.ForDevice` 上调阈值（属设计决策，不在本 PR）。

## 2. 验收阈值

- **降级开启**（glow/dust/codex/choice 依档位关闭）→ 目标 **≥30 fps**。
- **全开**（高端机）→ 目标 **≥60 fps**。
- 帧时间中位数/99 分位需在「走一条完整委托（reveal→assemble→choose→archive）」的稳定段统计，剔除首帧/场景加载尖峰。

## 3. 实测步骤

1. **Build 真机**：`File ▸ Build Settings ▸ Android/iOS ▸ Build and Run`（或先 Build 再装机）。
2. **开 Profiler**：`Window ▸ Analysis ▸ Profiler`，连真机（或 `Build` 时勾 `Development Build` + `Autoconnect Profiler`）。
3. **走一条委托**：进入游戏 → 首启引导 → 走完 reveal/assemble/choose/archive 全流程。
4. **取帧时间**：
   - 用 `UnityEngine.Profiling.Recorder` 在代码里采样 `Application.targetFrameRate` 对应帧时间：
     ```csharp
     using UnityEngine.Profiling;
     var frameRec = Recorder.Get("Main Thread"); // 或 "Render Thread"
     // 每帧 frameRec.sampleBlockCount / 累加 SampleBlock 的 time 取中位数/99 分位
     ```
   - 或在 Profiler 帧时间轴导出 CSV，离线算中位数/99 分位。
5. **对照三开关**：分别在（a）默认 `ForDevice` 自动档、（b）手动 `quality.enableGlowShader=false; quality.maxDustCells=36; quality.enableChoiceShader=false;` 下各跑一遍，
   记录两组帧时间差，确认降级档较全开档帧时间下降（或帧率上升），即降级生效。

## 4. 明确边界

- **沙箱不可验**：帧率/Shader/真机依赖 GPU 与设备，CI 沙箱由 `run-ci.sh` SKIP Unity 层属已知非阻断。
- **需用户本机/真机验**：本手册第 3 步的 Build+Profiler 必须由用户在真机或本地 Unity 完成；工程仅保证降级**配置开关**正确（见单测），不保证具体帧率数值。
- **单测已覆盖（无需真机）**：`RuntimeQuality.ForDevice` 三档位数学、`SessionRunner.EffectiveDustCellCap` 是否随 `quality.maxDustCells` 封顶。
