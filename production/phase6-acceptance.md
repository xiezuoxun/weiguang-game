# 《微光归处》Phase 6 美术/表现验收手册（步骤③准备）

> 适用：你本机 Unity 内验证 B1/B2/B3 Shader 与表现（沙箱无 Unity，无法代验）。
> 配套：自动化门 `Assets/Tests/PlayMode/ArtAcceptanceTests.cs`（见 §4）。
> 创建：2026-08-25

---

## 1. 验收总表（必须全过才能算 6-A/6-B 闭环）

| 编号 | 验收项 | 标准 | 触发点 | 验收手段 |
|---|---|---|---|---|
| A1 | B1 Shader 编译 | URP 2D 下无粉色材质报错 | 打开挂 DustReveal 的场景 | Console 无 Shader error |
| A2 | B1 阈值脉冲可见 | `_RevealProgress` 跨 0.25/0.50/0.75 时亮度差 ≥15% | `EVT_REVEAL_WHISPER`/`EVT_REVEAL_THRESHOLD_CROSSED` | 逐帧 screenshot 对比 / §4 测试 |
| A3 | B1 尘遮罩消退 | `_RevealProgress` 0→1 尘遮罩平滑淡出、微光渐显 | `SessionRunner.StartReveal` | 肉眼 / 录屏 |
| A4 | B2 碎片拼合 | 13 碎片对齐锚点（见 art-spec §0.2），吸附回弹 | `EVT_ASSEMBLE_COMPLETE` | 肉眼 |
| A5 | B3 纸签选中 | 未选中/选中双态对比清晰（亮度+描边双重区分，色弱友好），高亮 ≤250ms | `EVT_CHOICE_MADE` 前 | 肉眼 / §4 测试 |
| A6 | 资产齐全 | `ArtAssetLoader` 加载的 Sprite 全非空 | 场景启动 Bind 时 | §4 `AssetPresence` 测试 |
| A7 | 中断恢复 | onPause 强写 <500ms，回前台零误差 | 切后台/来电 | `SimulateOnPause` + 读档对比 |

---

## 2. B1 阈值脉冲人工核验方法（A2）
1. 在 `RevealVisualBridge` 挂的 Renderer 上挂 DustReveal 材质。
2. Play 模式，用手势/调试调用驱动 `_RevealProgress` 从 0 走到 1。
3. 在 `_RevealProgress` = 0.24 / 0.26、0.49 / 0.51、0.74 / 0.76 各截一帧。
4. 用取色器量物件中心区亮度，跨阈值两帧差值应 ≥15%（脉冲由 `_Pulse` 0→1→0 三角补间制造）。
5. 若差值不足：调 `RevealVisualBridge.pulseDurationMs`（默认 160ms，验收区间 120–200）或 DustReveal `_GlowStrength`。

---

## 3. 手动验收清单（逐条打勾回报沙箱）
- [ ] A1 Shader 编译无粉红
- [ ] A2 三档阈值亮度差 ≥15%（附截图或 §4 测试日志）
- [ ] A3 尘遮罩消退顺滑
- [ ] A4 碎片吸附回弹、13 片归位无错位
- [ ] A5 纸签选中高亮 ≤250ms、色弱友好
- [ ] A6 `AssetPresence` 测试全过（二进制资产已产齐）
- [ ] A7 中断恢复零误差

---

## 4. 自动化门：`ArtAcceptanceTests.cs`（PlayMode）

沙箱已写入 `Assets/Tests/PlayMode/ArtAcceptanceTests.cs` + `Weiguang.Tests.PlayMode.asmdef`（引用 Core + Runtime + TestRunner）。
本机打开工程后：Test Runner → **PlayMode** → Run All。

测试覆盖：
- `B1_ShaderPulse_DrivenByBridge`：用 `Shader.Find("Weiguang/DustReveal")` 建材质 → 挂 `RevealVisualBridge` → 注入测试 EventBus → 发 `EVT_REVEAL_WHISPER` → 断言 `_Pulse` 在 `Update` 后 >0（桥驱动 Shader 脉冲的链路打通）。
- `B2_B3_BridgesBindWithoutException`：实例化 4 桥、Bind、发对应事件，断言无异常（订阅/退订生命周期干净）。
- `EventBus_Unsubscribe_RemovesHandler`：验证 6-B 新增的 `Unsubscribe` 精准退订（Core 层纯逻辑，PlayMode 亦可跑）。
- `AssetPresence_FragmentsAndSlots`：断言 `Resources.Load<Sprite>("Fragments/fr_001")` 等 **非空**——此测试是"二进制资产是否产齐"的硬门，缺资产时 FAIL（即步骤①的完成信号）。

> 注意：A6 资产测试在你**未生产二进制**前会 FAIL（预期）。生产完资产后须全过，方算步骤①完成、可回沙箱做收口评审。

---

## 5. 回报格式（贴给沙箱主理人）
```
Phase 6 本机验收：
- EditMode 测试：PASS（X 例）
- PlayMode 测试：PASS（ArtAcceptanceTests Y 例，其中 AssetPresence 全过）
- A1~A7：全过 / 以下问题：[描述]
- 截图/录屏：已附
```
沙箱据此把 Phase 6-A/6-B 标记闭环，推进 6-C（I1 增强 / 首启引导 / 低端机降级）。
