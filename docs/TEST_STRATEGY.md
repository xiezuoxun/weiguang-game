# 《微光归处》测试策略（Test Strategy）

> 负责人：程基岩（engineering-lead）　|　阶段：Phase 4 预制作 → Sprint 1
> 基准：`docs/architecture/主架构文档.md` §7、`docs/architecture/控制清单.md`（I1–I4 / C1–C6）、`design/gdd/06-自动存档系统.md` ⑧、`production/sprint-01.md` 质量门
> 适用：Unity 2022 LTS + URP 2D，Unity Test Framework（NUnit 3）

---

## 0. 一句话策略

**Core 是纯 C#，所以 90% 的风险都能用秒级 EditMode 单测挡住；Unity 只用来验"引擎里才存在的东西"（渲染、手势、真机 IO）。**
因此测试重心刻意压在金字塔底部：CI 里能在**没有 Unity 许可证**的机器上跑完契约门，Unity 测试作为第二道门。

---

## 1. 分层模型

```
        ┌───────────────────────────────────┐
  层 3  │ 烟雾门 Smoke（Sprint 结束）          │  SmokeTests.cs：全链路 ×10、断点恢复
        ├───────────────────────────────────┤  判定 Sprint 能否收口
  层 2  │ 集成 Integration（PlayMode/设备）    │  待补：onPause 真机 <500ms、快照 ≤200KB、
        ├───────────────────────────────────┤        手势→语义事件、URP 帧率
  层 1  │ 单元 Unit（EditMode，NUnit）         │  ContractGuard / SaveEngine / FSM / EventBus
        ├───────────────────────────────────┤  ← 主力，秒级，每次提交必跑
  层 0  │ 契约门 Contract Gate（Python）       │  validate_contract.py + 自测，无需 Unity
        └───────────────────────────────────┘
```

### 层 0 · 契约门（I1，硬阻塞）

| 项 | 内容 |
|---|---|
| 脚本 | `tools/validate_contract.py`（校验 `Assets/Data/*.csv`） |
| 自测 | `tools/test_validate_contract.py` —— 31 个用例（4 正向 / 27 负向），守护"校验器本身不被改坏" |
| 拦截项 | 必需列缺失 · 数值不可解析 · 布尔字面量非法 · 数值边界（C6）· 枚举非法 · 引用悬空 · MVP 量与主线存在性 · R3 二级分支列（C4） |
| 退出码 | 0=PASS，1=FAIL（CI 直接判） |
| 为什么它是层 0 | 内容数据一旦越界，Unity 导入期 `int.Parse`/`bool.Parse` 会直接抛异常崩在玩家机上；把它前置成"构建失败"是 ADR-004 + ZZ CONCERN-2 的原始诉求 |

> `is_mainplot=yes` 这类**非法布尔**在 C# 里无法表达（`bool` 没有非法值），只能在层 0 拦截 —— 这正是层 0 不可被单测替代的原因。

### 层 1 · 单元测试（EditMode / NUnit）

| 测试文件 | 被测 | 用例数 | 覆盖要点 |
|---|---|---|---|
| `ContractGuardTests.cs` | `ContractGuard` / `ContractGuardAssert` / `DustGrid` | 41 | 6 个数值字段的四象限边界（界内/界上/界外）、多重违规一次全报、抛异常语义、枚举值集冻结、reveal_pct 计算 |
| `SaveEngineTests.cs` | `SaveEngine` | 24 | 原子写 temp→rename、校验和、3s 节流与脏位、force 绕节流、损坏回退、篡改检测、版本拒读/迁移、写失败不丢档、依赖未注入 fail-fast |
| `CommissionStateMachineTests.cs` | `CommissionStateMachine` | 20 | 8×8 全迁移矩阵 ×2（fc>0 / fc=0）、终态、跳过规则、告警双通道、事件"发且只发一次"、phase→SaveNode 映射 |
| `EventBusTests.cs` | `EventBus` / `GameEvents` | 8 | 异常隔离、派发顺序、回调中改订阅表、事件名常量唯一性（C2） |
| `SmokeTests.cs`（既有） | 组合 | 7 | Sprint 1 烟雾门原始 7 例，保留不动（仅修 1 处测试自身缺陷） |
| `TestKit.cs` | — | — | 公共夹具：`FakeStorage`（故障注入 + 调用录音）、`Fnv1a`、`FakeJson`、`Build`、`EventRecorder` |

**总计 100 个用例（本轮新增 93）**，`[TestCase]` 参数化按实例计；全部 EditMode，无需 PlayMode / 真磁盘 / 真设备。

单测三条铁律：
1. **测试侧独立声明规格**。状态机的合法迁移表在测试里重写一份（`Spec`），不复用被测代码的表 —— 否则改坏实现测试会一起变绿。
2. **替身要像真的**。`FakeStorage.ListFiles` 模拟 `Directory.GetFiles(dir, "save_*.json")` 的前缀/后缀匹配，才能暴露 `.json.tmp` 被误收之类的真实缺陷。
3. **错误消息也是契约**。断言"错误指向了正确的字段名"，而不只是"报错了"。

### 层 2 · 集成测试（PlayMode / 设备，Sprint 1 内补）

当前**未实现**（无 Unity 环境，诚实登记为缺口）。已锁定 5 条必做项，全部对应 GDD 验收标准中"引擎里才能测"的部分：

| ID | 用例 | 对应验收 | 依赖 |
|---|---|---|---|
| INT-1 | `onPause` 强制写全链路 <500ms（真机中低端档） | S6 ⑧-2 | 真机 + `UnitySaveStorage` |
| INT-2 | 单快照体积 ≤200KB（`JsonUtility` 序列化 MVP 满档） | S6 ⑧-6 | Unity 序列化器 |
| INT-3 | REVEALING/ASSEMBLING/CHOOSING 切后台→回前台，子状态误差 0 | S6 ⑧-1 | PlayMode + `GameBootstrap` |
| INT-4 | 磁盘满/无权限 → 轻提示不崩溃 | S6 ⑧-7 | 权限受限目录 |
| INT-5 | 空会话骨架真机跑 10 次帧率不掉档 | 概念 §8 / P1 | 真机 profiling |

### 层 3 · 烟雾门（Sprint 收口）

`SmokeTests.cs` 的 7 例 + 层 1 全绿 + 层 0 PASS。判定见 §3。

---

## 2. 覆盖率目标

| 装配 | 行覆盖目标 | 分支覆盖目标 | 说明 |
|---|---|---|---|
| `Weiguang.Core` | **≥ 85%** | ≥ 75% | 唯一有硬门的装配；契约/存档/状态机是全系统地基 |
| └ `SaveEngine` | **100%** | ≥ 90% | 数据安全直连玩家进度，允许 0 未测分支例外须写 ADR |
| └ `CommissionStateMachine` | **100%** | 100% | 8×8 矩阵已穷举 |
| └ `ContractGuard(+Assert)` | **100%** | 100% | 边界即契约 |
| `Weiguang.Runtime` | 不设硬门（Sprint 1） | — | 当前全是 stub，Sprint 2 起随 EPIC 替换为真实实现时同步补测并升门至 ≥60% |
| 表现层 / UI / Shader | 0 | — | 走人工验收 + 美术走查，不做自动化 |

采集命令（Code Coverage package `com.unity.testtools.codecoverage`）：

```bash
Unity -batchmode -nographics -projectPath game -runTests -testPlatform EditMode \
  -enableCodeCoverage \
  -coverageOptions "generateAdditionalMetrics;generateHtmlReport;assemblyFilters:+Weiguang.Core" \
  -coverageResultsPath artifacts/coverage -logFile -
```

> 覆盖率是**下限守护**，不是目标本身。禁止为了凑数写"调一遍不断言"的测试；评审时优先看断言质量。

---

## 3. Sprint 1 质量门（PASS / FAIL 判定）

Sprint 1 收口须**同时**满足以下 6 条，任一 FAIL 则 Sprint 不得收口（需主理人显式放行并登记豁免）：

| # | 门 | 判定方式 | 当前状态 |
|---|---|---|---|
| G1 | 契约门 I1 PASS | `python tools/validate_contract.py Assets/Data` 退出 0 | ✅ 已通过（5 委托/4 客户/5 物件） |
| G2 | 契约门自测 PASS | `python tools/test_validate_contract.py` 退出 0 | ✅ 31/31 |
| G3 | 命名与阈值守护 PASS | CI grep 门（C2 别名 / C3 硬编码 0.85） | ✅ 无违规 |
| G4 | EditMode 单测全绿 | Unity `-runTests -testPlatform EditMode` 退出 0 | ⏳ 待 Unity 环境执行 |
| G5 | 烟雾门：空会话 RECEIVED→ARCHIVED ×10 无崩溃 | `SmokeTests.FullPhaseChain_10Runs_NoException` + `GameBootstrap` 手动跑 | ⏳ 待 Unity 环境执行 |
| G6 | 中断恢复误差 0（REVEALING/ASSEMBLING/CHOOSING） | INT-3（PlayMode）或手动验收记录 | ⏳ 待补层 2 |
| G7 | `Weiguang.Core` 行覆盖 ≥85% | Code Coverage 报告 | ⏳ 待 Unity 环境执行 |

**控制清单闭合关系**：G1+G2+G3 闭合 **I1**（CI 契约校验器上线）与 **C1/C2/C3/C6**（契约冻结与边界锁死）；G4+G5 闭合 **I3** 的校验部分；G4 中的 `SaveEngineTests` 闭合 **I4**（存档校验：原子写/损坏回退/版本拒读/节流）除真机耗时项外的全部条目。

---

## 4. S6 验收标准 → 测试映射（GDD 06 ⑧ 逐条）

| GDD 06 ⑧ 验收条目 | 覆盖测试 | 层 |
|---|---|---|
| 各节点切后台回前台子状态误差 0 | `RoundTrip_RestoresPhaseNodeAndFragmentCount` + INT-3 | 1 / 2 |
| `onPause` <500ms 强制写 | `ForcedWrite_CompletesWellUnder500ms`（逻辑下界）+ INT-1（真机） | 1 / 2 |
| 连续变更合并、1 次/3s 节流生效 | `Throttle_SecondWriteWithinWindow_IsMerged_NotFailed`、`Throttle_ManyRapidWrites_CollapseToOne`、`Throttle_AfterWindowElapsed_WritesAgainWithoutForce` | 1 |
| 人为损坏 → 回退上一档，不崩不丢 | `CorruptLatest_FallsBackToPreviousReadableSnapshot`、`TamperedPayload_WithStaleChecksum_IsRejected`、`BrokenJson_WithValidChecksum_IsAlsoTreatedAsCorrupt`、`AllSnapshotsCorrupt_ReturnsNull_WithoutThrowing` | 1 |
| 版本不兼容安全拒读并提示 | `FutureVersion_RejectedWithUpdateHint`、`MissingMigrator_FailsSafelyWithHint`、`OlderVersion_RunsMigrationChain_ThenLoads` | 1 |
| 单快照 ≤200KB | INT-2（**缺口**：需 Unity `JsonUtility`） | 2 |
| 写盘失败捕获并轻提示，不崩溃 | `WriteFailure_IsCaught_PreviousSnapshotStaysReadable`、`RenameFailure_LeavesTempFile_ButItIsNeverPickedUpAsSnapshot`、`SerializeNotInjected_SaveFailsFast` | 1 |

---

## 5. 命令速查

```bash
# 层 0：契约门（无需 Unity，秒级）——本地提交前必跑
python game/tools/validate_contract.py game/Assets/Data
python game/tools/test_validate_contract.py

# 一键跑全部可在本机执行的门（含命名守护，Unity 可选）
bash game/ci/run-ci.sh                    # macOS / Linux / Git Bash
powershell -File game/ci/run-ci.ps1       # Windows

# 层 1：EditMode 单测（需 Unity 2022 LTS）
Unity -batchmode -nographics -runTests -projectPath game \
      -testPlatform EditMode -testResults artifacts/editmode.xml -logFile -

# 跳过唯一的慢用例（3.1s 真实等待）
Unity ... -runTests -testPlatform EditMode -testCategory "!Slow"

# 层 2：PlayMode（待 INT-1~5 落地后启用）
Unity -batchmode -runTests -projectPath game -testPlatform PlayMode -testResults artifacts/playmode.xml
```

Unity `-runTests` 退出码：`0`=全部通过，`2`=有用例失败，`3`=测试运行失败（编译错误等）。CI 只判 `!=0` 即失败。
注意：`-runTests` 不得与 `-quit` 同用（Unity 会在测试跑完前退出）。

---

## 6. 装配（asmdef）与可测性设计

| 装配 | 引擎引用 | 引用 | 意图 |
|---|---|---|---|
| `Weiguang.Core` | **noEngineReferences: true** | — | 强制 Core 保持纯 C#：一旦有人 `using UnityEngine` 立即编译失败。这是"EditMode 能秒测一切"的前提 |
| `Weiguang.Runtime` | 是 | `Weiguang.Core` | 单向依赖，Unity 相关实现（`UnitySaveStorage`/`GameBootstrap`）只能在这层 |
| `Weiguang.Tests.EditMode` | 是（TestRunner） | `Weiguang.Core` + nunit | `includePlatforms: [Editor]` + `defineConstraints: [UNITY_INCLUDE_TESTS]` → 不进包体；**故意不引用 Runtime**，保证单测不被 MonoBehaviour 拖下水 |

> 补装配前，`Assets/Tests/EditMode/*.cs` 会被编进 `Assembly-CSharp`（无 nunit 引用 → 整个工程编译失败，且测试不会出现在 Test Runner 窗口）。三个 `.asmdef` 是让测试"能跑起来"的必要条件，不是可选优化。
> `.meta` 文件由 Unity 首次导入时生成，无需手写。

---

## 7. 已知风险与缺口（诚实登记）

| ID | 风险 | 影响 | 处置 |
|---|---|---|---|
| R-1 | `SaveEngine.LoadLatest` 的 `while (snap.version < SAVE_VERSION)` 依赖迁移器自增 `version`，忘记自增 → **死循环卡死 CI/游戏启动** | 高（但当前只有 v0→v1 一跳，未触发） | 未改 Core。已用 `Migrator_MustBumpVersion_ContractGuard` 固化契约；建议 `SAVE_VERSION` 升到 2 前加"迁移轮次上限"守护，走 ADR-002 补丁 |
| R-2 | `EventBus` 只有 `Subscribe`/`Clear`，无 `Unsubscribe` | 中：长生命周期订阅者（UI）无法退订 → 泄漏 + 幽灵回调 | 建议 Sprint 2 补 `Unsubscribe(evt, handler)` 与 `IDisposable` 订阅句柄；已在测试中固化"回调中改订阅表不崩"的现有语义 |
| R-3 | 无 Unity 环境，层 1/层 2 未实际执行 | 中：单测代码本身未经编译器验证 | CI 已配好；**首个有 Unity 的环境须先跑一次 G4**，编译期问题一次性清掉 |
| R-4 | `UnitySaveStorage.Move` 用 delete-then-move，非真原子 | 中：delete 与 move 之间断电会丢目标档 | 建议改用 `File.Replace`（Windows/移动端均支持备份语义）；已由"上一份快照可回退"缓解 |
| R-5 | `SimpleCsv` 不支持引号/转义，中文含逗号会串列 | 中：叙事文案进 CSV 后必然踩 | 内容量上来前换 `choices.csv` 解析器或改 JSON；层 0 已能拦"列数错位"导致的解析失败 |
| R-6 | 快照体积/真机 IO 无数据 | 中：S6 ⑧ 两条验收无法判定 | INT-1/INT-2，Sprint 1 内补 |

---

## 8. 新增功能的测试准入（Definition of Done 附加项）

任何 Story 标记完成前：
1. 新增/修改 Core 逻辑 → 必须有对应 EditMode 用例，且**先写测试再写实现**；
2. 新增契约字段 → 必须同步 `validate_contract.py` 的 `REQUIRED_COLS` + 边界判定 + 自测负向用例（C1 变更评审）；
3. 新增 `EVT_*` → 必须加进 `GameEvents` 常量表（`EventNameConstants_AreUniqueAndSelfNamed` 会挡住字面量漂移）；
4. 修 bug → 先补一条能复现该 bug 的失败用例，再修（本轮 3 处修复均已照此办理）；
5. 测试证据路径写进 Story 的"测试证据"栏：`game/Assets/Tests/EditMode/<文件>::<用例名>`。
