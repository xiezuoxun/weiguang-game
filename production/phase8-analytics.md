# Phase 8-A 数据埋点（Analytics）

> 模块：四动词首见率 / 碎片吸附率 / 抉择分布 / 基础漏斗。
> 分层约束：埋点**核心**全在 `Weiguang.Core`（纯 C#、`noEngineReferences`，EditMode 可编译可测）；只有真正调 `UnityEngine` 的 sink 放在 `Weiguang.Runtime`。

## 1. 文件清单

| 文件 | 层 | 作用 |
|------|----|------|
| `Assets/Scripts/Core/Analytics/IAnalyticsSink.cs` | Core | 埋点出口接口 `Track(string, IDictionary<string,object>)` |
| `Assets/Scripts/Core/Analytics/AnalyticsTracker.cs` | Core | 订阅事件 + 聚合指标 + 触发 sink（**无 UnityEngine**） |
| `Assets/Scripts/Core/Analytics/LogAnalyticsSink.cs` | Core | 默认 sink，输出到 Console（CI/Editor 可读、可断言） |
| `Assets/Scripts/Runtime/Analytics/UnityAnalyticsSink.cs` | Runtime | 设备 sink，`MonoBehaviour`，转发 `UnityEngine.Analytics`（`#if UNITY_ANALYTICS` 守卫） |
| `Assets/Scripts/Runtime/Analytics/DeviceFpsProbe.cs` | Runtime | 真机帧率探针（见 `phase8-device-fps.md`） |
| `Assets/Scripts/Runtime/GameBootstrap.cs` | Runtime | `WireAnalytics()` 接线 + `OnDestroy` 退订 |
| `Assets/Tests/EditMode/AnalyticsTrackerTests.cs` | Tests.EditMode | 核心单测（四动词首见 / 吸附率 / 抉择分布 / 退订 / 聚合快照） |

> ⚠️ **路径偏差说明**：任务书面路径为 `Assets/Scripts/Tests.EditMode/AnalyticsTrackerTests.cs`，但仓库现存唯一的 EditMode 测试程序集位于 `Assets/Tests/EditMode/Weiguang.Tests.EditMode.asmdef`（引用 Core+Runtime+NUnit）。若把测试放进 `Assets/Scripts/` 会被 `Weiguang.Core`/`Weiguang.Runtime` 程序集吞入，而这两个程序集**不引用 NUnit**，会导致整项目编译失败。故测试按工程实际放在 `Assets/Tests/EditMode/`，以保证 CI 的 EditMode 能真正编译运行。

## 2. 埋点事件表

### 2.1 细粒度事件流（每次触发都上报）
| eventName | 触发源 | props |
|-----------|--------|-------|
| `reveal_whisper` | EVT_REVEAL_WHISPER | `whisper_key`, `reveal_pct` |
| `reveal_complete` | EVT_REVEAL_COMPLETE | `reveal_pct` |
| `assemble_complete` | EVT_ASSEMBLE_COMPLETE | `locked`, `total`, `adsorb_rate` |
| `choice_made` | EVT_CHOICE_MADE | `ending_tag` (Truth/Omit/Reframe) |
| `archived` | EVT_ARCHIVED | `entry_id`, `timeline_order`, `is_mainplot` |
| `codex_unlocked` | EVT_CODEX_UNLOCKED | `entry_id` |
| `first_launch` | EVT_FIRST_LAUNCH | （无） |
| `commission_start` | EVT_COMMISSION_START | `commission_id` |
| `commission_done` | EVT_COMMISSION_DONE | `commission_id` |

### 2.2 聚合快照（归档终点吐一次）
| eventName | 触发源 | props |
|-----------|--------|-------|
| `analytics_metrics` | EVT_ARCHIVED（委托漏斗终点） | 见 §3 |

## 3. 指标定义

聚合快照 `analytics_metrics.props`：

- **四动词首见率**（按"委托"口径：每个委托首次触发某动词计 1 次，跨委托累加为 `_fsX`，除以委托开始数 `_commissionStarts`）
  - `reveal_first_seen` / `reveal_first_seen_rate`
  - `assemble_first_seen` / `assemble_first_seen_rate`
  - `choose_first_seen` / `choose_first_seen_rate`
  - `archive_first_seen` / `archive_first_seen_rate`
- **碎片吸附率**：`fragment_locked` / `fragment_total` 累加自每次 `assemble_complete` 的 `locked/total`；`fragment_adsorb_rate = locked/total`。
- **抉择分布**：`choice_distribution` = `{ ending_tag: count }`（如 `{"Truth":12,"Omit":5,"Reframe":8}`）。
- **基础漏斗**：`funnel = { commission_start, reveal, assemble, choose, archive }`，各环节计"到达过该阶段的委托数"。

> 单客户端样本下首见率为"到达阶段委托占比"的近似；跨用户真实率由后端按 `user_id` 聚合（本端只负责把原始计数上报）。

## 4. Sink 设计

```
IAnalyticsSink (Core)
 ├─ LogAnalyticsSink (Core)   // 默认，Console 输出，EditMode 可测
 └─ UnityAnalyticsSink (Runtime, MonoBehaviour)  // 设备转发 Unity Analytics
```
- `AnalyticsTracker` 只持有 `IAnalyticsSink`，不关心具体出口。
- `LogAnalyticsSink` 异常全吞，绝不回抛到玩法链路。
- `UnityAnalyticsSink` 仅在 `UNITY_ANDROID || UNITY_IOS` 下被 `GameBootstrap` 实例化；真实 `UnityEngine.Analytics.Analytics.CustomEvent` 调用再受 `#if UNITY_ANALYTICS` 守卫，未启用 Analytics 包时退化为 `Debug.Log`，**保证 Runtime 程序集永远可编译**。
- 嵌套结构（抉择分布 / 漏斗）在 `UnityAnalyticsSink` 内被拍平成「键=JSON 字符串」，满足 Unity Analytics 的原始值约束。

## 5. 设备上启用真实 Unity Analytics（启用配方）

> ⚠️ 代码目标 API 是**旧版** `UnityEngine.Analytics.Analytics.CustomEvent`（见 `UnityAnalyticsSink.cs:23`）。
> 这要求装**旧版 Analytics 包 `com.unity.analytics`**（暴露 `UnityEngine.Analytics` 命名空间）。
> 新版「Unity Analytics」(`com.unity.services.analytics`) 用的是 `Unity.Services.Analytics` 命名空间与不同 API，**不兼容本代码**；如坚持用新版，提变更单改 `UnityAnalyticsSink` 的实现。

### 前置依赖
- Unity 2022.3.20f1 工程，已装旧版 Analytics 包（`Window → Package Manager → 搜 Analytics`，或 `com.unity.analytics`）。
- Unity Dashboard 已创建并**关联本项目**，Analytics 服务已启用（legacy Analytics 需 Dashboard 激活后才上报）。

### 启用步骤（每设备包一次）
1. **装包**：Package Manager 安装 `com.unity.analytics`（旧版）。
2. **翻宏**：`Project Settings → Player → 目标平台 (Android/iOS) → Scripting Define Symbols`，在现有符号后追加 `;UNITY_ANALYTICS`（分号分隔）。
   - ⚠️ 翻 `UNITY_ANALYTICS` 宏**必须同时完成第 1 步装包**，否则 `UnityEngine.Analytics` 命名空间缺失，`UnityAnalyticsSink.cs:23` 编译报错。
3. **（如需）补 asmdef 引用**：若编译报 `The type or namespace name 'Analytics' does not exist in 'UnityEngine'`，在 `Weiguang.Runtime.asmdef` 的 `references` 加 Analytics 程序集（具体名用 Assembly Finder 查该包 asmdef，通常为 `UnityEngine.Analytics` 或 `Unity.Analytics`）。
4. **出包**：`GameBootstrap.WireAnalytics()` 已在 `UNITY_ANDROID || UNITY_IOS` 下自动 `AddComponent<UnityAnalyticsSink>()`，把 `_analyticsSink` 从 `LogAnalyticsSink` 换成 `UnityAnalyticsSink`，**无需改代码**。
5. **真机运行**：所有 `Track` 经 `Analytics.CustomEvent` 上报。

### 验证
- **Editor / CI（宏未定义）**：Console 出现 `[Analytics] <event> {...}`（Debug.Log 兜底），单测可断言 —— 证明链路通。
- **设备包（宏定义 + 装包）**：Unity Dashboard → Analytics → Custom Events 实时出现事件：
  `reveal_whisper` / `assemble_complete` / `choice_made` / `archived` / `analytics_metrics` / `device_fps`。
  看不到事件 → 查 Dashboard 项目关联、Analytics 是否启用、包是否装对（必须是旧版 `com.unity.analytics`）。

> 沙箱无 Unity，无法实际翻宏/装包/看 Dashboard —— 本配方为**你可以在本机一步到位执行**的清单；宏与包属编辑器侧操作，主程在沙箱只保证代码路径正确（`#if UNITY_ANALYTICS` 守卫 + 设备自动换 sink）。

## 6. 测试覆盖

`AnalyticsTrackerTests.cs`（EditMode，纯 C#）断言：
- 四动词首见按委托口径累计（委托 2 未揭示 → reveal 首见仍为 1）。
- 碎片吸附率 `locked/total` 跨委托累加正确。
- 抉择分布 `by ending_tag` 计数正确。
- `Unsubscribe()` 后不再响应、不再 `Track`。
- 归档吐 `analytics_metrics` 快照，含 `funnel` / 首见率 / 吸附率 / `choice_distribution`。

> 沙箱无 Unity，无法实际 RUN；代码遵循 Core 零 UnityEngine 依赖，可在 CI 的 EditMode 下编译并通过。

## 7. 已知项 / 后续

- `EVT_COMMISSION_DONE` 当前**无任何 publisher**（`GameEvents` 已声明，tracker 已订阅），故 `commission_dones` 长期为 0；漏斗终点以 `archive` 为准。待 EPIC 补齐委托收口广播后自动生效，无需改 tracker。
- 跨启动"累计"目前是进程内实例级累加；若需跨会话累计，后续可把 `GetMetrics()` 快照落存档（复用 `SaveEngine`），属增量工作，不在本 Phase。
