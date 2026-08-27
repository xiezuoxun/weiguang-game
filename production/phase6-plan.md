# 《微光归处》Phase 6 规划

> 阶段：Phase 6（打磨 / 美术落地 / 真机串联 / 运营准备）
> 主理人：游承峰　|　基准：Phase 5 收口状态 + `production/art-spec.md` + `docs/ux/UX规格.md` + `docs/TEST_STRATEGY.md`
> 创建：2026-08-24

---

## 0. 一句话定位

Phase 5 验证的是"代码对不对"（逻辑闭环 + 内容闭环 + CI 守护闭环）；
**Phase 6 要验证的是"游戏好不好"**——从「能跑的逻辑骨架」进化到「能玩的产品」。

---

## 1. Phase 5 收口状态（验收基线）

| 维度 | 状态 | 证据 |
|---|---|---|
| 核心循环逻辑 | ✅ 已真实化 | S1 拂尘 / S2 拼合 / S3 抉择 / S5 图鉴 四 EPIC 纯 C# 落地，本机 G4/G5/G7 全绿 |
| 存档底座 | ✅ 已闭环 | S4/S6 SaveEngine + UnitySaveStorage + GameBootstrap + 19 例 SaveEngineTests |
| 内容产能 | ✅ 已交付 | 7 张 CSV（commissions/clients/items/choices/endings/quotes/whispers），48 委托-客户-物件-碎片-抉择-结局-低语全部对齐 |
| 体验钩子 | ✅ 已落盘 | EventBus 18 事件 + 反馈钩子(11 例) + 工程健壮性(7 例)，待本机 pull 验收 |
| 美术资产 | ⚠️ 规格就绪，实体空缺 | `production/art-spec.md` 已写，但 `Assets/` 零 .shader/.prefab/.asset/音频 |
| 云端 CI | ✅ 守护就位 | contract-gate + unity-tests 双绿，UNITY_LICENSE 已配 |

**结论**：Phase 5 = **逻辑闭环 + 内容闭环 + CI 守护闭环**；
唯一结构性缺口是 **美术/音频实体资产（B1/B2/B3）** 与 **真机 UI 串联**（Runtime 编排层目前靠 stub 驱动，无真实场景/预制体）。

---

## 2. Phase 6 三子阶段

### Phase 6-A · 美术与表现落地（阻断项，优先级最高）
**owner**：art-director（据 `production/art-spec.md` 排期）＋ engineering-lead（Shader/预制体接入）

1. **B1 拂尘微光 Shader**（URP 2D）
   - `DustReveal.shader`：尘遮罩随 `reveal_pct` 溶解，接 `RevealThresholdTracker.reveal_pct`
   - `GlowPulse.shader`：阈值跨越顿挫感，接 `EVT_REVEAL_THRESHOLD_CROSSED`
2. **B2 拼合槽位草图 + 13 碎片精灵**：接 `FragmentSlot` / `AssemblyBoard` 中带常量（MID_BAND 0.33–0.67）
3. **B3 抉择纸签 + 音频清单**（8 文件：拂尘笔触 / 吸附咔嗒 / 解锁叮 / 低语浮笺…）
4. **5 项补齐缺口**（art-spec.md P1/P2）：图鉴归档视觉 / 低语浮笺载体 / 客户符号 / 拂尘手势笔触 / DustGrid 承托底
5. **验收门**：Unity 编辑器内场景挂 `GameBootstrap` + 真实美术，Play 模式走完一条委托无 MissingReferenceException

### Phase 6-B · 真机 UI 串联与手感（依赖 6-A）
**owner**：engineering-lead ＋ design-strategist（UX 规格已备 `docs/ux/UX规格.md`）

1. **场景装配**：空场景挂 `GameBootstrap`，CSV 拖入 TextAsset 槽，接 `EventBus` 18 事件到 UI 响应（目前全靠 Console 日志）
2. **手势层**：拂尘手势→`StartReveal` 语义映射（接 `RevealThresholdTracker`）；拼合拖拽→`AssemblyBoard.TryPlaceFragment`；抉择点选→`ChoiceHitTester.SelectOption`
3. **中断恢复真机验**：onPause <500ms 强写（GDD S6②），切后台/来电恢复零误差
4. **层 2 集成测试补全**：`docs/TEST_STRATEGY.md` 标记为"待补"的 PlayMode/设备项（onPause 真机、快照 ≤200KB、URP 帧率、手势→语义）
5. **验收门**：真机（Android/iOS 或桌面）跑通一条主线 + 一次中断恢复，无卡死/无数据丢失

### Phase 6-C · 打磨与运营准备（可并行）　✅ 沙箱可落地产物已全部交付（commit `d668c52` + `2eec5b9`）
> 说明：6-C 沙箱可做的"逻辑/校验/手册/桥接"部分已完成；需真机/二进制/外部工具的"实体验收"由用户侧执行（见 `phase6-local-runbook.md` / `phase6-device-fallback.md` / `phase6-launch-assets.md`）。
1. **I1 增强** ✅：`validate_contract.py` 加 **R4 碎片归属交叉校验**（fragments→items 引用 + 按 item 聚合碎片数 == `fragment_count`）+ **R5 结局抉择一致性**（ending_id 唯一 + commission 引用完整 + choices 经 commission_id+ending_tag 关联）+ **BOM 检测**（EF BB BF → warn 不阻断）。`run-ci.sh` G1 门已调用，CI 层0 真实守护。实跑 PASS。
2. **首启引导** ✅（桥接层）：`OnboardingUIRuntimeBridge.cs` 继承 `ArtBridgeBase`，订阅 `EVT_FIRST_LAUNCH`，`ShowOnboarding` 为纯 C# stub（Debug.Log 四动词引导，未引 Unity UI，无 Canvas 不崩）；由 `GameBootstrap.BindArtBridges` 自动绑定。**真实引导 Canvas/面板待美术+用户本机接**。
3. **低端机降级验证** ✅（逻辑+手册）：`RuntimeQualityFallbackTests.cs`（EditMode）固化 `ForDevice` 三档位降级数学；`SessionRunner.EffectiveDustCellCap` 已接 `Math.Min(_quality.maxDustCells,64)`。**真机帧率实测见 `phase6-device-fallback.md`，需用户本机/真机跑**。
4. **运营素材** ✅（清单）：`phase6-launch-assets.md` 列商店图(1024×500/512 圆角/16:9)+30s 视频脚本(四动词四镜)+测试招募模板+字段对齐。二进制素材需美术/用户侧产。

---

## 3. 推进节奏

| 周次 | 焦点 | 交付 | 门禁 |
|---|---|---|---|
| W1 | 6-A 美术资产 | B1/B2/B3 实体 + 音频，入 `Assets/` | 编辑器内无 MissingReference |
| W2 | 6-B 场景串联 | 真机 UI 跑通一条委托 | PlayMode 无异常 + onPause 绿 |
| W3 | 6-B 集成测试 + 6-C | 层 2 测试补全 + 首启引导 + 降级验证 | CI 双绿 + 真机抽检 |
| W4 | 收口 / 上线准备 | I1 增强 + 运营素材 | Phase 6 评审门 |

---

## 4. 执行状态总览（截至 2026-08-27，收口裁定见 phase6-closure-decision.md）

| 子阶段 | 沙箱可落地产物 | 用户侧待执行 | 状态 |
|---|---|---|---|
| 6-A 美术资产 | B1 `DustReveal.shader` + ArtBinding 转接层×6 + 生产清单 + 场景装配说明（`b506c03`） | B1/B2/B3 二进制 PNG + 音频 8 文件（`phase6-asset-tasks.md`） | ⚠️ 实体空缺，硬阻断 |
| 6-B 场景骨架 | EventBus.Unsubscribe + 4 桥 OnUnbind + GameBootstrap.BindArtBridges（`3702b06`） | 真机挂场景 + 手势层 + onPause 真机验 + 层2 测试补全 | 🟡 骨架就绪，待串联 |
| 6-C 打磨运营 | I1 增强 + 首启引导桥 + 降级验证 + 运营清单（`d668c52`+`2eec5b9`） | 真机帧率实测 + 引导 Canvas + 运营素材二进制 | ✅ 沙箱产物齐 |

**全部 9 个未推送 Phase 6 commit 已打包 `weiguang-6ab.bundle`（60K，动态基线 origin/master..master，含 35a8695 起）**，用户本机 `git pull weiguang-6ab.bundle master` 即同步。

> ⚠️ **沙箱网络限制**：本 `game/` 已连 `origin/master` 但**未 push**（沙箱无 GitHub 出网权限）。所有新 commit 经 bundle 离线交付。

---

## 5. 已知残留（从 Phase 5 继承，非阻塞）

- `check_test_layout.py` 路径根误判（cwd 扫 `Assets/...` vs 实际 `game/Assets/...`），属已知非阻断项
- CR-003 R1–R8 中 Ornament 枚举（R1 已闭环）、**交叉校验入 CI（R4/R5）已闭环**（见 6-C① `d668c52`）、cl03 签名色、15 句低语文案（已交付 whispers.csv）、四张 CSV（已扩展为七张）
- 首个 Unity 环境已跑 G4/G7 清编译期问题（本机全绿已证明路径 C 各 EPIC 无编译期问题）
