# 《微光归处》· Sprint 1 可运行骨架

> Phase 5 制作 · Sprint 1（EPIC-01 存档底座 + EPIC-02 委托编排容器）
> 主理人手写产出（算力受限期间）｜Unity 2022 LTS + URP 2D（ADR-001）

## 这是什么
一个**能跑通核心循环骨架**的 Unity 项目源码：纯 C# 域模型（契约实体/状态机/事件总线/存档引擎）+ Unity 薄适配层 + S1/S2/S3/S5 stub。启动后自动把一条委托从 RECEIVED 推到 ARCHIVED 并全程自动存档，模拟切后台可精确恢复。

## 目录结构
```
game/
  Assets/
    Scripts/
      Core/                       # 纯 C#（无 UnityEngine 依赖，可单测）
        DataContract.cs           # 共享数据契约实体（对齐 GDD 00 §2）+ ContractGuard
        EventBus.cs               # ADR-005 事件总线（EVT_*）
        CommissionStateMachine.cs # S4 八态状态机 + SaveNode 映射
        SaveEngine.cs             # S6：原子写/3s节流/强制写/校验和/迁移/损坏回退
      Runtime/                    # Unity 适配层
        UnitySaveStorage.cs       # ISaveStorage 实现 + 轻量 CSV
        GameBootstrap.cs          # 组装 + 空会话驱动 + OnApplicationPause
        SessionRunner.cs          # 编排 + S1/S2/S3/S5 stub
    Data/                         # ADR-004 数据驱动（外置 CSV）
      commissions.csv / clients.csv / items.csv
    Tests/EditMode/
      SmokeTests.cs               # Sprint 1 质量门测试
  tools/
    validate_contract.py          # CI 契约校验器（控制清单 I1）
```

## 如何使用

### 1. 打开 Unity 项目
1. Unity Hub → 新建项目（Unity 2022.3 LTS，2D 模板）
2. 把本目录 `Assets/` 内容复制进项目（或直接以本目录为项目根，再补 `ProjectSettings/`）
3. 打开 Package Manager 安装 **Test Framework**（com.unity.test-framework）
4. 建一个空场景，挂 `GameBootstrap`，把 `Assets/Data/` 下三个 CSV 拖到对应 TextAsset 槽
5. Play → Console 观察空会话骨架日志：
   ```
   [S1-stub] 拂尘完成 reveal_pct=1.00（threshold=0.85）
   [S2-stub] 拼合完成（3 片锁定）
   [S3-stub] 抉择落定 selected=op0 tag=Truth（单层，R3）
   [S4] 交付：微光重燃（Truth）
   [S5-stub] 归档 CodexEntry=ce_com_001_0｜图鉴共 1 条
   ```

### 2. 跑烟雾测试（质量门）
Window → General → Test Runner → EditMode → Run All：
- `FullPhaseChain_10Runs_NoException`：10 次全链路无异常
- `FragmentCountZero_SkipsAssembling` / `FragmentCountPositive_CannotSkipAssembling`
- `Throttle_SkipsWithin3s_ForceAlwaysWrites`：节流 + onPause 强制写
- `CorruptSnapshot_FallsBackToEarlier`：损坏回退
- `FutureVersion_Rejected`：版本不兼容拒读
- `ContractGuard_RejectsOutOfRange`：越界拒

### 3. 跑 CI 契约校验器（不依赖 Unity）
```bash
python game/tools/validate_contract.py
# PASS：5 委托 / 4 客户 / 5 物件，契约合规
```

## Sprint 2 替换计划
| stub | 替换为 | EPIC |
|---|---|---|
| StartReveal | 手势拂尘（dust_grid 逐格 + 辅助模式） | EPIC-03 |
| StartAssemble | 拖拽吸附拼合 | EPIC-04 |
| StartChoose | 玩家点选 ChoiceNode（纸签 UI） | EPIC-05 |
| Archive | 图鉴时间线 UI | EPIC-06 |

## 已知边界（诚实声明）
- Unity 工程文件（ProjectSettings/manifest.json）未含——需按上述步骤在 Unity Hub 里生成，源码全部就绪
- 快照序列化用 JsonUtility（Unity 层）；EditMode 测试用极简可逆序列化验证语义
- CSV 引擎为轻量实现（无引号转义），MVP 数据量下够用
