# Phase 6-B 场景串联与预制体结构说明

> 作者：林绘澄（art-director）｜供 engineering-lead（程基岩）在 6-B 阶段使用
> 前提：Phase 6-A 已交付 B1 Shader + ArtBinding 脚手架（Runtime 层，可编译）。

## 1. 空场景（MainScene）挂载清单
| GameObject | 组件 | 来源 | 说明 |
|---|---|---|---|
| `GameBootstrap` | `GameBootstrap` (Mono) | 既有 Runtime | 持有 EventBus；Awake 末注入各 Bridge |
| `DustGrid` | `Renderer` + `RevealVisualBridge` | B1 Shader 挂此 | 材质 = `Weiguang/DustReveal`；需 `_MainTex`/`_DustTex`/`_GlowTex` |
| `AssemblyBoard` | `Renderer` + `AssembleVisualBridge` | B2 容器 | 碎片/槽位底图 Sprite 由 ArtAssetLoader 加载 |
| `ChoiceBoard` | `ChoiceVisualBridge` | B3 容器 | 纸签实例命名 `option_<option_id>` 并 `RegisterTab` |
| `CodexUI` | `CodexVisualBridge` | S5 容器 | 条目命名 `entry_<entry_id>` 并 `RegisterEntry` |
| `CSV_TextAssets` | 3×`TextAsset` 槽 | 既有 | commissions/clients/items（GameBootstrap 公有字段拖入） |

## 2. EventBus 注入接口约定（关键）
- Core `EventBus` 无静态单例（ADR-005）。`GameBootstrap._bus` 是其唯一所有者。
- 所有 `*VisualBridge` 继承 `ArtBridgeBase`，通过 `Bind(EventBus bus)` 注入。
- **engineering-lead 任务**：在 `GameBootstrap.Awake()` 实例化/查找上述 Bridge 后，统一调用 `bridge.Bind(_bus)`。
  ```csharp
  // 伪代码（6-B 植入 GameBootstrap.Awake 末尾）
  foreach (var b in FindObjectsOfType<ArtBridgeBase>()) b.Bind(_bus);
  ```
- **退订约定**：Core `EventBus` 当前仅 `Subscribe`/`Clear`，无 `Unsubscribe`。退订依赖 `Bus.Clear()`（切会话/场景时由 GameBootstrap 调用）全局清理；若需精确退订，6-B 应为 `EventBus` 增 `Unsubscribe`（不破坏 Core 无 UnityEngine 约束）。

## 3. 资产加载约定（ArtAssetLoader）
- 资源放 `Resources/` 下，路径前缀见 `phase6-asset-tasks.md` 命名节。
- 6-B 接 Addressables 时仅改 `ArtAssetLoader.LoadSprite` 实现（接口不变）。
- 缺失资源时 `LoadSprite` 返回 null + 警告日志，不抛异常（保护核心循环）。

## 4. Shader 挂载点（B1）
- `DustGrid.Renderer.material` 赋 `Weiguang/DustReveal` 实例。
- 属性驱动：`_RevealProgress`（SessionRunner 拂尘进度）、`_Threshold`+`_Pulse`（RevealVisualBridge 在 EVT_REVEAL_WHISPER/CROSSED 触发）。
- `_GlowColor` 默认暖白 (1.0,0.93,0.78)，可 HDR 溢出。

## 5. 分层与 asmdef（已静态核对）
- ArtBinding 脚本置于 `game/Assets/Scripts/Runtime/ArtBinding/`，归属 `Weiguang.Runtime` asmdef（已引用 `Weiguang.Core`）。
- Core 层（`Weiguang.Core.asmdef`, `noEngineReferences:true`）零 UnityEngine 泄漏；ArtBinding 的 UnityEngine 使用仅限 Runtime 层，合规。
- 无需新建 asmdef。

## 6. 本机验收（沙箱不可验）
- Shader 编译：Unity Editor 内确认 `Weiguang/DustReveal` 无粉色报错材质。
- 美术表现：B1 阈值脉冲、B2 吸附回弹、B3 选中高亮需在真机/编辑器手测（见各 md 验收命令）。
- CI：无 Unity 时 `bash game/ci/run-ci.sh` 仅跑层0（G1/G2/G3 PASS），Unity 层 SKIP（已知非阻断）。
