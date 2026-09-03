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

## 5. 设备上启用 Unity Analytics

1. 安装并启用 Unity **Analytics** 服务（Services 窗口 / Dashboard），Player Settings 勾选 Analytics，使 `UNITY_ANALYTICS` 脚本宏定义生效。
2. 出 **Android / iOS** 包：`GameBootstrap.WireAnalytics()` 在 `UNITY_ANDROID || UNITY_IOS` 下自动 `AddComponent<UnityAnalyticsSink>()`，把 `_analyticsSink` 从 `LogAnalyticsSink` 换成 `UnityAnalyticsSink`。
3. 真机运行后，所有 `Track` 自动经 `Analytics.CustomEvent` 上报，可在 **Unity Dashboard → Analytics → Custom Events** 查看 `reveal_whisper` / `assemble_complete` / `choice_made` / `archived` / `analytics_metrics` / `device_fps` 等。
4. Editor / CI 默认走 `LogAnalyticsSink`，埋点日志打印到 Console，单测可断言（见 §6）。

> 若需在 Editor 也走 Unity Analytics，可临时把 `WireAnalytics` 中的平台判断放宽，或手动 `gameObject.AddComponent<UnityAnalyticsSink>()` 并赋给 `_analyticsSink`——但生产默认保持「设备才走 Unity Analytics」。

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
