# 《微光归处》Phase 6 收口裁定（Closure Decision）

> 裁定人：主理人 游承峰（Weiguang 工作室）
> 裁定日期：2026-08-27
> 裁定依据：沙箱侧交付物静态核查 + `git bundle verify` + `validate_contract.py` 实跑；**用户本机验收结果尚未回报**（对话中无 A/B/C 门绿/红数据）。

## 一、裁定结论

### ⛔ Phase 6 整体：NOT DONE（暂缓收口）

**原因**：唯一硬阻断项——二进制美术实体资产（B1/B2/B3 PNG + 音频 8 文件）仍在用户本机待产；真机串联（B1–B5）与真机打磨（C1–C4）尚未在本机验证。沙箱无图形界面、无法代产二进制、无法跑真机帧率，故**缺用户侧数据不能宣布 Done**。

### ✅ 沙箱侧工程：准予收口（移交用户侧执行）

沙箱可做的纯代码/文档产物 100% 落盘、静态核查通过、CI 层0 实跑 PASS、bundle 完整自包含。这部分可签字移交。

## 二、沙箱侧可裁定项（Confirmed）

| 域 | 交付物 | 裁定 |
|---|---|---|
| 6-A 文本层 | `art-spec.md` + `DustReveal.shader`（手写 HLSL）+ ArtBinding 转接层 ×6 + `phase6-asset-tasks.md` + `phase6-scene-assembly.md`（`b506c03`） | ✅ 代码层就绪 |
| 6-B 骨架 | `EventBus.Unsubscribe` + 4 桥 `OnUnbind` 精准退订 + `GameBootstrap.BindArtBridges`（`3702b06`） | ✅ 真实代码落盘，Core 零 UnityEngine 泄漏核对通过 |
| 6-C I1 | `validate_contract.py` 增 R4/R5/BOM；`run-ci.sh` G1 调用；CI 层0 实跑 PASS | ✅ |
| 6-C 首启 | `OnboardingUIRuntimeBridge.cs`（订阅 `EVT_FIRST_LAUNCH`，纯 C# stub 不引 Unity UI） | ✅ |
| 6-C 降级 | `RuntimeQualityFallbackTests.cs`（EditMode，`ForDevice` 三档数学）+ `SessionRunner.EffectiveDustCellCap` 已接 `Math.Min(_quality.maxDustCells,64)` | ✅ C# 层 |
| 6-C 运营 | `phase6-launch-assets.md`（商店图/视频/招募/字段对齐清单） | ✅ |
| CI 守护 | `contract-gate` PASS；`unity-tests` job 已激活（需本机跑全） | ✅ 层0；层1 待本机 |
| 同步 | `weiguang-6ab.bundle`（60K，动态基线 `origin/master..master`，含 9 commit，`git bundle verify` 通过） | ✅ |
| 收口门 | `phase6-closure-gate.md`（A1–A7/B1–B5/C1–C4 清单） | ✅ |

## 三、待本机验证项（Blockers，状态未知 → 未裁定）

| 门 | 项 | 依赖 |
|---|---|---|
| A1–A7 | 6-A 美术二进制实体（Shader 编译/亮度差/13 碎片/吸附/纸签/5 缺口/音频） | 用户本机 Unity 产 PNG/音频 |
| B1–B5 | 6-B 真机串联（挂场景无异常 / `[6-B] 已绑定 5 桥` / 手势语义 / onPause 零误差 / `ArtAcceptanceTests` 全过） | 本机 Play 模式 |
| C1 | 低端机降级真机帧率（≥30fps） | 真机 Profiler |
| C2 | 首启引导真实 Canvas | 本机 UI 接入 |
| C3 | 云端 CI `unity-tests` 双绿 | 本机 push 后 Actions 跑 |

## 四、解锁条件（宣布 Phase 6 Done 前必须全满足）

1. 用户回报 **A1–A7 全 PASS**（美术实体闭环）
2. 用户回报 **B1–B5 全 PASS**（真机串联闭环）
3. 用户回报 **C1–C3** 状态（C4 仅上线需要）
4. 沙箱据回报复核，无新增阻断

## 五、若本机验收发现问题

- **美术/表现类**：art-director 据 `art-spec.md` 修订，回沙箱不入 CI 核心逻辑
- **编译/逻辑类**：engineering-lead 修，重打包 bundle 交付
- **数据契约类**：`validate_contract.py` 增规则，CI 层0 守护

## 六、当前可签字部分

> **沙箱侧工程交付（6-A 排期 + 6-B 骨架 + 6-C 增强 + 收口门 + 离线同步）已收口，准予移交用户侧执行。**
> Phase 6 整体 Done 待用户侧 A/B/C 门验收回报后裁定。

---

### 下一步动作
- 用户：取 `weiguang-6ab.bundle` → 本机 `git pull` → Unity 产二进制资产 → 跑 EditMode+PlayMode → 对照 `phase6-closure-gate.md` 勾门 → 回报绿/红。
- 主理人：收到回报后据本裁定第四节做最终签收或开修复 Issue。
