# Phase 8-B 真机帧率补录手册（Device FPS Probe）

> # ⛔ 本机真机验收项 —— 沙箱不可代测
> 本探针测量的是**真实 GPU/CPU 在真机上的帧率**。当前沙箱/CI 环境**没有 GPU、不跑 Unity 渲染循环**，
> 任何在沙箱里"跑"出来的 FPS 数字都**没有意义、不能作为验收依据**。
> 以下测量步骤**必须由用户在真机（实体 Android/iOS 设备）上执行**，主程无法代测。

## 1. 为什么要真机

- `DeviceFpsProbe` 用 `Time.deltaTime` 累加实测 FPS，依赖 Unity 的真实渲染帧循环。
- 沙箱无 GPU、无真实帧循环，`Time.deltaTime` 不可信；低端机的降级（`RuntimeQuality.ForDevice` 按 `systemMemorySize` 关 Shader / 降拂尘网格）也只有在真机上才真实生效。
- 因此帧率验收是**明确的本机真机项**，须由持有设备的同学执行并回填数据。

## 2. 实现落点

| 项 | 位置 |
|----|------|
| 探针组件 | `Assets/Scripts/Runtime/Analytics/DeviceFpsProbe.cs`（`MonoBehaviour`） |
| 接线/启停 | `GameBootstrap.WireAnalytics()` + `OnDestroy()` |
| 上报事件 | `device_fps`（经 `IAnalyticsSink.Track`） |

`DeviceFpsProbe` 每帧累加，按 `sampleWindowSec`（默认 **60s**）汇总一次：
- `avg_fps` 窗口平均帧率
- `min_fps` 窗口最低瞬时帧率
- `device_model` = `SystemInfo.deviceModel`（设备型号）
- `quality_tier` = `low`/`mid`/`high`（来自 `RuntimeQuality`：`enableGlowShader`/`enableChoiceShader` 全关=low，任一关=mid，全开=high）
- `sample_sec`、`frames`、`unity_version`

## 3. 真机测量步骤

1. **出包**：在 Unity 出 **Android (APK/AAB)** 或 **iOS (IPA)** 包。`GameBootstrap` 在 `UNITY_ANDROID || UNITY_IOS` 下会自动 `enableDeviceFpsProbe = true` 并 `AddComponent<DeviceFpsProbe>()`。
   - 若想强制开关，也可在 Inspector 勾选 `GameBootstrap.enableDeviceFpsProbe` 后再出包（默认 false，仅设备包经 `#if` 自动开）。
2. **选机**：
   - **低端机**：系统内存 **≤ 1024 MB**（验收对象，最易掉帧）。
   - **对照**：一台桌面/高内存机（≥ 2048 MB）验证 60fps 基线。
3. **运行**：在设备上打开游戏，进入主玩法（拂尘→拼合→抉择→归档 走一轮，让降级档位真实生效）。
4. **等待采样**：让 `DeviceFpsProbe` 至少跑满 **60 秒**（默认窗口）。
5. **读数**（任一方式）：
   - **屏上**：探针 `showOnScreen=true` 时左上角实时显示 `FPS live / avg@win / min@win (tier)`；
   - **日志**：`UnityAnalyticsSink` 在未定义 `UNITY_ANALYTICS` 时 `Debug.Log("[Analytics] device_fps {...}")`；连设备用 `adb logcat` / Xcode 控制台抓取；
   - **后端**：若已启用 Unity Analytics，事件 `device_fps` 会上报到 Dashboard → Custom Events。
6. **记录**：把 `avg_fps` / `min_fps` / `device_model` / `quality_tier` 回填到验收表（见 §5）。

## 4. 验收线

| 设备档 | 验收线 |
|--------|--------|
| 低端机（≤1024 MB，含上述降级） | **avg_fps ≥ 30**，且 `min_fps` 不应长时间 < 20 |
| 桌面 / 高内存机（≥2048 MB，全开） | **avg_fps ≥ 60**（或接近设备刷新率） |

未达标时排查方向：降低 `RuntimeQuality.maxDustCells`、关 `enableGlowShader`/`enableChoiceShader`、检查 `DustBudget.CapGrid` 实际分辨率、用 Unity Profiler 定位 CPU/GPU 热点。

## 5. 备选：Unity Profiler 直测

若不愿依赖探针上报，也可：
1. 设备 **USB 连电脑**，Unity Editor → **Window → Analysis → Profiler** → 选设备。
2. 录制 60s 玩法，读 **FPS 图**（CPU/GPU 双栏）的 avg / min。
3. 该数值与 `device_fps` 事件应一致；以 Profiler 为准亦可，但**仍需真机**。

## 6. 回填验收表（待你本机真机实测）

> 🟡 **状态：模板就绪，待真机实测回填**。沙箱无 GPU/设备，主程无法产出 FPS 数字 —— 请按 §3 在低端机跑满窗口后，把下表数字抄入（也可直接贴 `device_fps` 事件行 / `adb logcat` 抓取行）。

| 实测日期 | 执行人 | 包版本(commit) | 设备 | device_model | quality_tier | avg_fps | min_fps | 结论（≥30） | 备注 |
|----------|--------|----------------|------|--------------|--------------|---------|---------|-------------|------|
| （填） | （填） | （填） | 低端机 A（≤1024MB） | （填） | low | （填） | （填） | ≥30 ✅/❌ | |
| （填） | （填） | （填） | 桌面 B（≥2048MB） | （填） | high | （填） | （填） | ≥60 ✅/❌ | 基线对照 |

回填后把本表状态从 🟡 翻 ✅，并同步更新 `release/phase8-release-checklist.md` 的 A1 项。
若低端机 `avg_fps < 30`：先按 §4 降级（`RuntimeQuality.maxDustCells` / 关 `enableGlowShader`/`enableChoiceShader` / `DustBudget.CapGrid`），复测；仍不达标则标记 No-Go 阻塞项。

> 主程在沙箱只能保证：**代码按上述逻辑编写、Core 零 Unity 依赖可 EditMode 编译、`GameBootstrap` 生命周期正确启停探针**。
> **FPS 数字本身必须由真机测量**，本手册即为此交付。
