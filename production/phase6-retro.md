# 《微光归处》Phase 6 复盘（Retro）

> 复盘日期：2026-08-27 18:41
> 复盘范围：Phase 6 全周期（A 排期 + B 骨架 + C 增强 + 收口裁定）
> 复盘人：主理人 游承峰 + 工作室全体（art-director / engineering-lead / design-strategist / quality-lead）

---

## 一、Phase 6 目标回顾

- **Phase 5**：验证「代码对不对」——核心循环逻辑（S1 拂尘 / S2 拼合 / S3 抉择 / S5 图鉴）+ 存档底座 + 内容产能 + CI 守护四闭环 ✅
- **Phase 6**：验证「游戏好不好」——从逻辑骨架进化到**可玩产品**

---

## 二、按时序关键节点

| 时间 | commit | 事件 |
|---|---|---|
| Phase 5 收口 | `35a8695` | ci.yml 加 master 触发，CI 双绿 |
| Phase 5 打磨 | `9b791b2` `009826a` | 反馈钩子 + 工程健壮性（18 例新测试） |
| Phase 6-A | `b506c03` | B1 `DustReveal.shader` + ArtBinding ×6 + 生产清单 + 场景装配说明 |
| Phase 6-B | `3702b06` | `EventBus.Unsubscribe` + 4 桥 `OnUnbind` 精准退订 + `GameBootstrap.BindArtBridges` 串联 |
| Phase 6 本机手册 | `5b60339` | `phase6-local-runbook.md` + `phase6-acceptance.md` + `ArtAcceptanceTests.cs` + **EditMode asmdef 缺 Runtime 引用修复** |
| Phase 6-C I1 | `d668c52` | `validate_contract.py` R4/R5/BOM 交叉校验 + `phase6-launch-assets.md` |
| Phase 6-C 首启/降级 | `2eec5b9` | `OnboardingUIRuntimeBridge` + `RuntimeQualityFallbackTests` + `phase6-device-fallback.md` |
| 收口门 | `be1d537` | `phase6-closure-gate.md`（A1–A7 / B1–B5 / C1–C4 清单） |
| 收口裁定 | `b8a905e` | `phase6-closure-decision.md`（升级为 DONE）+ `phase6-retro.md`（本文件） |

合计 **10 个 Phase 6 commit**，全部入库沙箱 git，工作树 clean。

---

## 三、踩过的坑（避坑指南）

### 1. 沙箱网络墙（贯穿 Phase 6）
- `github.com:443` 经本地代理 `127.0.0.1` 连不上，`git push` 必失败（`fatal: Could not connect to server`）
- GitHub connector 的 MCP 通道 `connected`，但只对 WebFetch/MCP 工具生效，git 命令行不通
- **对策**：统一用 `git bundle create <file> origin/master..master` 离线同步；本机 `git pull <bundle> master`
- **判断本机工程位置**：扫 C 盘用户目录 + D 盘所有 `.git/config`，本机唯一一份指向 `xiezuoxun/weiguang-game` 即 WorkBuddy workspace 这份——本机无需 bundle

### 2. EditMode asmdef 缺 Runtime 引用（关键 bug）
- `Weiguang.Tests.EditMode.asmdef` 原只引 `Weiguang.Core`
- 但 `RobustnessTests.cs` 用了 `RuntimeQuality` / `SessionRunner`（属 `Weiguang.Runtime`）
- 本机 EditMode 测试**编译失败**（之前云端 CI "绿"可能因 unity-tests 实际未跑全）
- **修复**：在 `Weiguang.Tests.EditMode.asmdef` 的 `references` 加 `"Weiguang.Runtime"`（已纳入 `5b60339`）

### 3. CSV BOM 误带
- `commissions.csv` / `items.csv` 带 UTF-8 BOM
- `validate_contract.py` 默认 `utf-8-sig` 读不阻断，但 2 条 warn
- **对策**：加 `check_bom()` 检测（warn 不阻断），内容侧存 UTF-8 无 BOM 为佳（已纳入 `d668c52`）

### 4. CI 层 1 docker exit 133
- `game-ci/unity-test-runner@v4` 在 GitHub 沙箱 Linux docker 跑不起来（exit 133）
- 用户用 `continue-on-error` 软绿（`b5dde76`），整 run Success
- 代码正确性以**本机 165/165** 为 ground truth
- **待修**：把 unity-test-runner 改用官方镜像或换 self-hosted runner（Phase 7 候选）

### 5. PNG .meta 缺字段（用户本机发现）
- Unity 自动生成的 NPOT 纹理若 `textureType/spriteMode/nPOTScale` 默认值，无法生成 Sprite
- 用户修：`textureType: 0→8`（Sprite）/ `spriteMode: 0→1`（Single）/ `nPOTScale: 1→0`（None）
- **对策**：写一个资产导入脚本批量设置，或在 `ArtAssetLoader.cs` 加 Resources.Load 失败的兜底日志（Phase 7 候选）

### 6. Bundle 基线漂移（自我修复）
- 早期 bundle 用固定基线 `35a8695..master`，漏了 `35a8695` 本身（git 实际 `ahead 8` 而非 7）
- **对策**：改动态基线 `origin/master..master`，确保自包含（已修复在 `5b60339` 重打包时）

---

## 四、按计划项 / 超计划项

### 按计划（按时序交付）
- ✅ 6-A 美术排期（art-spec.md + DustReveal.shader + ArtBinding ×6 + phase6-asset-tasks.md + phase6-scene-assembly.md）
- ✅ 6-B 真机串联骨架（EventBus.Unsubscribe + 4 桥 OnUnbind + GameBootstrap.BindArtBridges）
- ✅ 6-C I1 数据契约（R4/R5/BOM 入 CI）
- ✅ 6-C 首启引导（OnboardingUIRuntimeBridge）
- ✅ 6-C 低端机降级验证（ForDevice 数学 + EffectiveDustCellCap 接入 + 真机手册）

### 超计划（用户友好度 + 健壮性）
- ✅ 收口门文档（phase6-closure-gate.md）：让用户本机对照勾 A/B/C 清单
- ✅ 收口裁定文档（phase6-closure-decision.md）：升级版含本机回报 + CI 实查
- ✅ 复盘文档（本文件）
- ✅ 本机手册（phase6-local-runbook.md）：明确「本机即 workspace 无需 bundle」判断流程
- ✅ 验收手册（phase6-acceptance.md）：A1–A7 验收命令/截图方法
- ✅ PlayMode 验收测试（ArtAcceptanceTests.cs）：含「资产齐全硬门」自动校验
- ✅ EditMode asmdef 引用修复：解阻塞本机测试编译
- ✅ CSV BOM 检测入 CI（warn 不阻断）
- ✅ R4/R5 交叉校验入 CI（数据关系守护）

---

## 五、Phase 7 建议（按优先级）

### P0 必修（阻塞发布）
1. **真机补录 C1/C2**：低端机降级帧率 + 首启引导 Canvas 接入（本机桌面端已绿，真机待测）
2. **CI docker 修复**：让 unity-test-runner 在云端真实绿（消除 `continue-on-error` 软绿）

### P1 加分（提升产品力）
3. **数据埋点**：Unity Analytics 监测四动词首见率 / 碎片吸附率 / 抉择分布
4. **内容扩充**：7 张 CSV → 20 张（物件变体 / 客户变体 / 低语主题）

### P2 上线准备
5. **运营素材**：商店图 / 30s 视频 / 招募文案（phase6-launch-assets.md 清单已有）

### P3 可选
6. **资产导入自动化**：PNG .meta 批量设置脚本
7. **PlayMode 测试覆盖补强**：增加 onPause 中断恢复真机测试

---

## 六、关键数据

| 维度 | 数值 |
|---|---|
| Phase 6 commit 数 | 10 |
| 总代码行数（Phase 5+6 估算） | ~3500 行 C# + ~800 行 Shader + ~500 行 Python + ~1500 行 Markdown |
| 测试覆盖 | EditMode 165 例 + PlayMode 4 例 = **169 例** |
| CI 层 0 | 真实绿 ✅ |
| CI 层 1 | 软绿 ⚠️（docker exit 133 待修） |
| Bundle 大小 | 64K（含全部未推送 commit，verify 通过） |
| 美术资产到位 | B1 2 贴图 + B2 5 底图 + 13 碎片 + B3 2 纸签 + 缺口 17 项 + 音频 8 文件 |
| 真机降级帧率 | 未测（本机桌面端） |
| 云端 CI 直推可行性 | 沙箱无 GitHub 网络，需用户本机推 + 离线 bundle 备份 |

---

## 七、组织经验沉淀

### 沙箱侧角色分工（验证有效）
- **主理人（游承峰）**：调度 + 决断 + 文档对账 + 收口裁定
- **art-director（林绘澄）**：美术规格 + 转接层脚手架 + 生产清单
- **engineering-lead（程基岩）**：EventBus 生命周期 + GameBootstrap 串联 + 验收测试
- **design-strategist（秦墨）**：UX 规格 + 反馈钩子 + 首启引导文案
- **quality-lead（苏景明）**：测试矩阵 + 验证门 + 健壮性兜底

### 工作流模式
1. **Phase 启动**：主理人写 phaseN-plan.md（子阶段划分 + 验收门）
2. **执行**：派子代理独立交付，commit 落盘
3. **文档对账**：每阶段末尾主理人核验代码真实态，更新文档执行状态
4. **收口门**：写 phaseN-closure-gate.md（用户本机验收清单）
5. **收口裁定**：写 phaseN-closure-decision.md + phaseN-retro.md

### 同步机制（关键约定）
- **沙箱无 GitHub 网络**：git push 必失败，统一用 `git bundle create <file> origin/master..master`
- **本机工程位置**：用 `find` 扫 `*.git/config` 找 `xiezuoxun/weiguang-game` 引用；本机唯一一份即 WorkBuddy workspace 这份，无需 bundle
- **CI 双层守护**：层 0 契约门（Python validate_contract.py）+ 层 1 Unity EditMode 测试

---

## 八、致谢

- **用户侧一次性到位**：EditMode 165 + PlayMode 4 + A1–A6 全 PASS + B1–B3 美术资产到位 + GBK/PNG .meta 工程补丁，零返工
- **WebFetch 通畅**：让沙箱能实查 GitHub Actions CI 状态，弥补 git push 网络墙的限制
- **art-spec.md 提前就位**：让 Phase 6-A 一启动就能按规格生产，避免规格漂移
- **EditMode 165/165** 是 Phase 6 工程质量的硬证据，给 Phase 7 真机补录奠定基础

---

### 下一步
等用户拍板 Phase 7 候选方向，主理人按七阶段流水线继续推进。