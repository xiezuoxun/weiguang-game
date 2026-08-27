# 《微光归处》Phase 6 收口评审门（Closure Gate）

> 用途：用户本机在 Unity 内完成二进制资产生产 + 真机验收后，逐条勾选本门，全部 PASS 即 Phase 6 收口、可进入发布准备。
> 沙箱环境无法生成二进制资产、无法跑真机帧率，故本门**由用户在本机执行并回报**，主理人据回报做收口裁定。

## 0. 同步基线（先做）

| 项 | 命令 / 动作 | 预期 |
|---|---|---|
| 取最新代码 | `git pull origin master`（若 GitHub 已通）或 `git pull <path>/weiguang-6ab.bundle master` | 含 7 个 Phase 6 commit：`9b791b2` `009826a` `b506c03` `3702b06` `5b60339` `d668c52` `2eec5b9` |
| 确认基线绿 | Unity Test Runner → EditMode 全过（含 `RuntimeQualityFallbackTests` + `RobustnessTests` + `FeedbackHooksTests`） | 0 失败 |

## 1. 6-A 美术实体（硬阻断项，必须 100%）

| 编号 | 验收点 | 资产 | 命令 / 方法 |
|---|---|---|---|
| A1 | `DustReveal.shader` 在 URP 2D 编译无粉红报错 | B1 | 编辑器内挂材质看 Inspector |
| A2 | 跨 0.25 / 0.50 / 0.75 三阈值亮度差 ≥15% | B1 | 截图三档，取像素均值比对 |
| A3 | 5 槽位底图 + 13 碎片 Sprite 加载无 MissingReference | B2 | Play 模式走一条委托 |
| A4 | 碎片吸附回弹动效 ≤250ms 无卡顿 | B2 | Profiler 帧时间 |
| A5 | 纸签双态切换（未选中→选中）高亮 ≤250ms，色弱友好 | B3 | 人工 + 截帧 |
| A6 | 5 缺口资产到位：物件立绘×10 / 低语笺×1 / 客户符号×4 / 手势笔触×1 / 承托底×1 | 缺口 | 资源目录核对 |
| A7 | 音频 8 文件入位：SFX×5 + BGM×3，播放无截断 | 音频 | AudioSource 试播 |

## 2. 6-B 真机串联

| 编号 | 验收点 | 命令 / 方法 |
|---|---|---|
| B1 | 空场景挂 `GameBootstrap` + CSV TextAsset 槽，Play 模式走完一条委托无 MissingReferenceException | Play 模式 |
| B2 | `BindArtBridges` 绑定 4 视觉桥 + 首启引导桥（共 5），Console 打印 `[6-B] 已绑定 5 个 ArtBridge` | Console |
| B3 | 手势层：拂尘手势→`StartReveal`、拼合拖拽→`TryPlaceFragment`、抉择点选→`SelectOption` 语义映射正确 | 真机操作 |
| B4 | `onPause` 中断恢复零误差：切后台/来电恢复后快照一致（GDD S6② <500ms 强写） | 真机 + 对比存档 |
| B5 | `ArtAcceptanceTests`（PlayMode）全过——尤其「资产齐全硬门」：缺任一资产即 FAIL | Test Runner |

## 3. 6-C 打磨与运营

| 编号 | 验收点 | 命令 / 方法 |
|---|---|---|
| C1 | `RuntimeQuality` 三档位降级真实生效：低端机关 glow/dust/paper 后帧率 ≥30fps | `phase6-device-fallback.md` Profiler 流程 |
| C2 | 首启引导真实 Canvas 接 `OnboardingUIRuntimeBridge`，四动词首见引导按序展示 | 真机首启 |
| C3 | 云端 CI 双绿：`contract-gate`（R4/R5/BOM 已增强）+ `unity-tests` | GitHub Actions |
| C4 | 运营素材就绪（如需上线）：商店图 / 30s 视频 / 招募文案（见 `phase6-launch-assets.md`） | 资产目录 |

## 4. 收口条件（全部满足才可裁定 Phase 6 Done）

- [ ] 0 节同步完成，EditMode 0 失败
- [ ] A1–A7 全 PASS（6-A 实体闭环）
- [ ] B1–B5 全 PASS（6-B 真机闭环）
- [ ] C1–C3 全 PASS（C4 仅上线需要）
- [ ] 用户本机回报「绿」+ 关键截图/帧率数据

## 5. 收口后动作

1. 主理人据回报写 `phase6-retro.md`（复盘：哪些按计划、哪些踩坑、Phase 7 建议）
2. 若上线：走 `phase6-launch-assets.md` 运营素材交付 + 商店提审
3. 若继续迭代：进入 Phase 7（内容扩充 / 玩法深度 / 数据埋点）

> ⚠️ 沙箱侧 Phase 6 工程产物（A 排期 + B 骨架 + C 增强）均已落盘，本门是**用户侧执行清单**，非沙箱任务。
