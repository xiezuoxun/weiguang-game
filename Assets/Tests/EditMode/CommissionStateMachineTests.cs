// CommissionStateMachineTests.cs — S4 核心循环状态机单测（GDD 04 §2；护栏 §5 非法迁移不静默）。
// 关键设计：测试文件**独立重写一份合法迁移表**（Spec），而不是复用被测代码的表。
// 若有人改动 CommissionStateMachine.Legal，8×8=64 格全矩阵会立刻红，逼迫改动走 GDD/评审。
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class CommissionStateMachineTests
    {
        EventBus bus;
        CommissionStateMachine fsm;
        EventRecorder rec;
        List<string> warns;

        /// <summary>GDD 04 §2 合法迁移表（测试侧独立声明的"规格真相"）。</summary>
        static readonly Dictionary<CommissionPhase, CommissionPhase[]> Spec =
            new Dictionary<CommissionPhase, CommissionPhase[]>
            {
                { CommissionPhase.Idle,       new[]{ CommissionPhase.Received } },
                { CommissionPhase.Received,   new[]{ CommissionPhase.Examining } },
                { CommissionPhase.Examining,  new[]{ CommissionPhase.Revealing } },
                { CommissionPhase.Revealing,  new[]{ CommissionPhase.Assembling, CommissionPhase.Choosing } },
                { CommissionPhase.Assembling, new[]{ CommissionPhase.Choosing } },
                { CommissionPhase.Choosing,   new[]{ CommissionPhase.Delivering } },
                { CommissionPhase.Delivering, new[]{ CommissionPhase.Archived } },
                { CommissionPhase.Archived,   new CommissionPhase[0] },
            };

        static IEnumerable<CommissionPhase> AllPhases
            => Enum.GetValues(typeof(CommissionPhase)).Cast<CommissionPhase>();

        [SetUp]
        public void Setup()
        {
            bus = new EventBus();
            fsm = new CommissionStateMachine(bus);
            rec = new EventRecorder(bus, GameEvents.EVT_PHASE_CHANGED, GameEvents.EVT_CONTRACT_WARN);
            warns = new List<string>();
            fsm.OnWarn += m => warns.Add(m);
        }

        // ── ① 全链路 ×10（Sprint 1 烟雾门）─────────────────────────
        [Test]
        public void FullChain_TenRuns_AllLegal_EmitsSevenEventsEach()
        {
            var order = new[]
            {
                CommissionPhase.Received, CommissionPhase.Examining, CommissionPhase.Revealing,
                CommissionPhase.Assembling, CommissionPhase.Choosing, CommissionPhase.Delivering,
                CommissionPhase.Archived
            };

            for (int run = 0; run < 10; run++)
            {
                rec.Clear();
                var c = Build.NewCommission("com_" + run, fragmentCount: 3);
                foreach (var to in order)
                    Assert.IsTrue(fsm.AdvancePhase(c, to), "第 " + run + " 轮 " + to + " 应合法");

                Assert.AreEqual(CommissionPhase.Archived, c.phase);
                Assert.AreEqual(7, rec.Count(GameEvents.EVT_PHASE_CHANGED), "每轮应发 7 次 EVT_PHASE_CHANGED");
                Assert.AreEqual(0, rec.Count(GameEvents.EVT_CONTRACT_WARN), "合法链路不得有告警");
            }
            Assert.IsEmpty(warns);
        }

        [Test]
        public void FullChain_SkipAssembling_WhenZeroFragments()
        {
            var c = Build.NewCommission("com_skip", fragmentCount: 0);
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Received));
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Examining));
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Revealing));
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Choosing), "fragment_count==0 允许跳过 ASSEMBLING（S4 ②-3）");
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Delivering));
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Archived));

            Assert.AreEqual(6, rec.Count(GameEvents.EVT_PHASE_CHANGED), "跳过 ASSEMBLING → 只 6 次迁移");
            Assert.IsEmpty(warns);
        }

        // ── ② 全矩阵 8×8：非法迁移一律拒绝且保持原态 ─────────────────
        [Test]
        public void TransitionMatrix_FragmentsPresent_OnlySpecPathsAllowed_SkipForbidden()
        {
            foreach (var from in AllPhases)
                foreach (var to in AllPhases)
                {
                    // fragment_count>0：Revealing→Choosing 属于"非法跳过"，必须拒绝
                    bool expect = Spec[from].Contains(to)
                                  && !(from == CommissionPhase.Revealing && to == CommissionPhase.Choosing);

                    var c = Build.NewCommission("com_m", fragmentCount: 3, phase: from);
                    bool ok = fsm.AdvancePhase(c, to);

                    Assert.AreEqual(expect, ok, from + "→" + to + " 判定错");
                    Assert.AreEqual(expect ? to : from, c.phase, from + "→" + to + " 被拒后必须保持原 phase");
                }
        }

        [Test]
        public void TransitionMatrix_ZeroFragments_RevealingToChoosingBecomesLegal()
        {
            foreach (var from in AllPhases)
                foreach (var to in AllPhases)
                {
                    // 特性刻画：fragment_count==0 时 Revealing→Assembling 当前实现仍放行（表内合法）。
                    // 是否应额外硬挡（无碎片不得进拼合）留待 S4 Story 决策，届时本用例须同步更新。
                    bool expect = Spec[from].Contains(to);

                    var c = Build.NewCommission("com_z", fragmentCount: 0, phase: from);
                    bool ok = fsm.AdvancePhase(c, to);

                    Assert.AreEqual(expect, ok, from + "→" + to + "（fc=0）判定错");
                    Assert.AreEqual(expect ? to : from, c.phase, from + "→" + to + " 被拒后必须保持原 phase");
                }
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        public void PositiveFragments_CannotSkipAssembling(int fragmentCount)
        {
            var c = Build.NewCommission("com_noskip", fragmentCount, CommissionPhase.Revealing);

            Assert.IsFalse(fsm.AdvancePhase(c, CommissionPhase.Choosing));
            Assert.AreEqual(CommissionPhase.Revealing, c.phase, "拒绝后不得半推进");
            Assert.AreEqual(1, warns.Count, "拒绝必须告警（不静默）");
            StringAssert.Contains("不得跳过", warns[0]);
            StringAssert.Contains("fragment_count=" + fragmentCount, warns[0], "告警须带实际数值便于定位数据");
            Assert.AreEqual(0, rec.Count(GameEvents.EVT_PHASE_CHANGED), "拒绝不得发 phase 变更事件");
        }

        // ── ③ 终态与空引用 ─────────────────────────────────────────
        [Test]
        public void Archived_IsTerminal_NoOutgoingEdges()
        {
            Assert.IsEmpty(Spec[CommissionPhase.Archived], "ARCHIVED 是终态");

            foreach (var to in AllPhases)
            {
                var c = Build.NewCommission("com_end", 3, CommissionPhase.Archived);
                Assert.IsFalse(fsm.AdvancePhase(c, to), "ARCHIVED→" + to + " 必须拒绝");
                Assert.AreEqual(CommissionPhase.Archived, c.phase);
            }
            Assert.AreEqual(8, warns.Count, "8 次尝试须全部被拒并逐条告警（不静默）");
            Assert.IsTrue(warns.All(w => w.Contains("Archived")), "告警须指明来源终态 Archived");
        }

        [Test]
        public void NullCommission_RejectedWithWarn_NotCrash()
        {
            Assert.IsFalse(fsm.AdvancePhase(null, CommissionPhase.Received));
            Assert.AreEqual(1, warns.Count);
            StringAssert.Contains("null", warns[0]);
            Assert.AreEqual(0, rec.Count(GameEvents.EVT_PHASE_CHANGED));
        }

        // ── ④ 事件语义：一次成功迁移 = 恰好一次事件，载荷是同一实例 ──────
        [Test]
        public void SuccessfulTransition_PublishesExactlyOneEvent_WithSameCommissionInstance()
        {
            var c = Build.NewCommission("com_evt", 3);

            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Received));
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_PHASE_CHANGED), "发且只发一次");
            Assert.AreSame(c, rec.Last(GameEvents.EVT_PHASE_CHANGED), "载荷须是同一 Commission 实例（订阅方直接读新 phase）");
            Assert.AreEqual(CommissionPhase.Received, ((Commission)rec.Last(GameEvents.EVT_PHASE_CHANGED)).phase,
                "事件发出时 phase 必须已经是新值（先改态后发事件）");

            Assert.IsFalse(fsm.AdvancePhase(c, CommissionPhase.Archived));
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_PHASE_CHANGED), "失败迁移不得追加事件");
        }

        [Test]
        public void IllegalTransition_PublishesContractWarnEvent_AndCSharpEvent()
        {
            var c = Build.NewCommission("com_warn", 3, CommissionPhase.Idle);

            Assert.IsFalse(fsm.AdvancePhase(c, CommissionPhase.Delivering));

            Assert.AreEqual(1, rec.Count(GameEvents.EVT_CONTRACT_WARN), "须经事件总线广播告警（C2：用 GameEvents 常量）");
            Assert.AreEqual(1, warns.Count, "同时须触发 OnWarn（Unity 层接 Debug.LogWarning）");
            StringAssert.Contains("非法迁移", warns[0]);
            StringAssert.Contains("com_warn", warns[0], "告警须带 commission_id");
            Assert.AreEqual(warns[0], rec.Last(GameEvents.EVT_CONTRACT_WARN), "两条通道消息一致");
        }

        // ── ⑤ phase → SaveNode 映射（S6 断点）────────────────────────
        [TestCase(CommissionPhase.Received, SaveNode.NodeReceive)]
        [TestCase(CommissionPhase.Examining, SaveNode.NodeExamine)]
        [TestCase(CommissionPhase.Revealing, SaveNode.NodeReveal)]
        [TestCase(CommissionPhase.Assembling, SaveNode.NodeAssemble)]
        [TestCase(CommissionPhase.Choosing, SaveNode.NodeChoose)]
        [TestCase(CommissionPhase.Delivering, SaveNode.NodeDeliver)]
        [TestCase(CommissionPhase.Archived, SaveNode.NodeArchive)]
        [TestCase(CommissionPhase.Idle, SaveNode.NodeReceive)]   // 兜底：Idle 落回接物断点
        public void NodeOf_MapsEveryPhase(CommissionPhase phase, SaveNode expected)
        {
            Assert.AreEqual(expected, CommissionStateMachine.NodeOf(phase));
        }

        [Test]
        public void NodeOf_CoversAllSevenSaveNodes()
        {
            var mapped = AllPhases.Select(CommissionStateMachine.NodeOf).Distinct().ToList();
            Assert.AreEqual(7, mapped.Count, "七个 SaveNode 必须都可达（Idle 与 Received 共用 NodeReceive）");
        }
    }
}
