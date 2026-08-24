// CodexArchiveTests.cs — Sprint 2 S5-1~S5-4 图鉴归档真实化单测（EditMode，纯 C# Core）。
// 覆盖：Archive 聚合 / 幂等 / timeline_order 递增 / 终态守卫 fail-fast / Ending 三态映射 / EventBus 集成。
// 与 SmokeTests/DustRevealTests/AssemblyTests/ChoiceTests 互不冲突（独立文件、独立 fixture）；不改任何既有测试。
using System;
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Tests;

namespace Weiguang.Tests
{
    [TestFixture]
    public class CodexArchiveTests
    {
        // ── 测试装置：可变的 SaveSnapshot + EventRecorder（订阅 EVT_ARCHIVED）──
        sealed class Harness
        {
            public SaveSnapshot Snap;
            public readonly EventBus Bus = new EventBus();
            public readonly EventRecorder Rec;
            public readonly SessionRunner Runner;

            public Harness()
            {
                Snap = Build.NewSnapshot();
                var fsm = new CommissionStateMachine(Bus);
                var save = new SaveEngine(new FakeStorage(), Build.DIR, Bus);
                Runner = new SessionRunner(Bus, fsm, save, () => Snap);
                Rec = new EventRecorder(Bus, GameEvents.EVT_ARCHIVED);
            }

            /// <summary>构造一个可直接归档的 commission（phase=Delivering，跳过状态机前置）。</summary>
            public static Commission Deliverable(string id, bool isMainplot = false)
            {
                var c = Build.NewCommission(id, fragmentCount: 0);
                c.is_mainplot = isMainplot;
                c.phase = CommissionPhase.Delivering; // 合法归档前置态
                return c;
            }

            /// <summary>构造一个处於 Choosing 的 commission（供完整链路驱动 Deliver 验证 Ending 映射）。</summary>
            public static Commission Choosing(string id)
            {
                var c = Build.NewCommission(id, fragmentCount: 0, phase: CommissionPhase.Choosing);
                c.choice_count = 2;
                return c;
            }
        }

        /// <summary>在 Snap 上预载最小抉择点（仅 op0，ending_tag 强制为 tag），供 SelectOption→Deliver 链路。</summary>
        static void AttachChoice(Harness h, Commission c, EndingTag tag)
        {
            var node = new ChoiceNode { node_id = "cn_" + c.commission_id, commission_id = c.commission_id };
            node.options.Add(new ChoiceOption
            {
                option_id = "op0", wording = "（待填措辞）",
                ending_tag = tag, client_reaction = "…", sdt_autonomy_weight = 0.5f
            });
            h.Snap.choice_states.Add(node);
        }

        // ── S5-2 Archive 一次：codex.Count==1 / timeline_order==0 / is_mainplot 一致 / entry_id 格式 ──
        [Test]
        public void Archive_Once_PopulatesCodex_WithZeroOrder_AndCorrectFields()
        {
            var h = new Harness();
            var c = Harness.Deliverable("com_1", isMainplot: true);
            h.Runner.Archive(c);

            Assert.AreEqual(1, h.Snap.codex.Count, "归档一次后 codex 应恰 1 条");
            var e = h.Snap.codex[0];
            Assert.AreEqual(0, e.timeline_order, "首条 timeline_order 应为 0");
            Assert.IsTrue(e.is_mainplot, "is_mainplot 应与 commission 一致");
            Assert.AreEqual("cx_com_1", e.entry_id, "entry_id 格式应为 cx_<commission_id>");
            Assert.AreEqual("com_1", e.commission_id, "commission_id 应透传");
            Assert.AreEqual("it_t", e.item_id, "item_id 应来自 commission");
            Assert.AreEqual("cl_t", e.client_id, "client_id 应来自 commission");
            Assert.AreEqual("（待填引语）", e.quote, "quote 应为合规占位（design 后续填）");
            Assert.AreEqual(CommissionPhase.Archived, c.phase, "归档后 commission 应转 Archived 终态（Step 已迁移）");
        }

        // ── S5-3 幂等：同一 commission 调 Archive 两次，codex.Count 仍为 1（不重复）──
        [Test]
        public void Archive_Idempotent_WhenCalledTwice_ForSameCommission()
        {
            var h = new Harness();
            var c = Harness.Deliverable("com_1");
            h.Runner.Archive(c); // 第一次：正常归档
            h.Runner.Archive(c); // 第二次：c.phase 已是 Archived → 幂等跳过
            Assert.AreEqual(1, h.Snap.codex.Count, "同一 commission 二次归档应幂等，codex 仍 1 条");
            Assert.AreEqual(1, h.Rec.Count(GameEvents.EVT_ARCHIVED), "仅广播一次 EVT_ARCHIVED（第二次幂等不发）");
        }

        // ── S5-2 timeline_order 递增：归档两个不同 commission，第二条 timeline_order==1 ──
        [Test]
        public void Archive_TwoDifferentCommissions_SecondTimelineOrderIsOne()
        {
            var h = new Harness();
            var c1 = Harness.Deliverable("com_a");
            var c2 = Harness.Deliverable("com_b");
            h.Runner.Archive(c1);
            h.Runner.Archive(c2);

            Assert.AreEqual(2, h.Snap.codex.Count, "两个不同 commission 应各 1 条，共 2 条");
            Assert.AreEqual(0, h.Snap.codex[0].timeline_order, "首条 timeline_order==0");
            Assert.AreEqual(1, h.Snap.codex[1].timeline_order, "第二条 timeline_order==1（递增）");
            Assert.AreEqual("cx_com_a", h.Snap.codex[0].entry_id);
            Assert.AreEqual("cx_com_b", h.Snap.codex[1].entry_id);
        }

        // ── S5-3 终态守卫：phase 非 Delivering（此处 Choosing，既非 Delivering 也非 Archived）调 Archive → 抛 ──
        [Test]
        public void Archive_GuardsAgainstOutOfOrder_WhenPhaseNotDelivering_Throws()
        {
            var h = new Harness();
            var c = Build.NewCommission("com_x", fragmentCount: 0, phase: CommissionPhase.Choosing);
            var ex = Assert.Throws<ContractViolationException>(() => h.Runner.Archive(c));
            Assert.IsNotNull(ex, "乱序归档（非 Delivering 且非 Archived）必须 fail-fast 抛 ContractViolationException");
            Assert.AreEqual(0, h.Snap.codex.Count, "乱序归档不得写入 codex");
        }

        // ── S5-1 Ending 三态映射：完整链路（SelectOption→Deliver）后 LastEnding 与 tag 对应 ──
        [TestCase(EndingTag.Truth, "微光重燃", "释然")]
        [TestCase(EndingTag.Omit, "尘封未启", "怅惘")]
        [TestCase(EndingTag.Reframe, "另寻归处", "和解")]
        public void Deliver_BuildsEnding_MappedByTag(EndingTag tag, string expectedTitle, string expectedStage)
        {
            var h = new Harness();
            var c = Harness.Choosing("com_e");
            AttachChoice(h, c, tag);

            h.Runner.SelectOption(c, "op0"); // → EVT_CHOICE_MADE → OnChoiceMade → Deliver(tag)

            Assert.IsNotNull(h.Runner.LastEnding, "Deliver 后应构建 Ending");
            Assert.AreEqual("ed_com_e", h.Runner.LastEnding.ending_id, "ending_id 格式 ed_<commission_id>");
            Assert.AreEqual(expectedTitle, h.Runner.LastEnding.title, $"{tag} 标题应映射为 {expectedTitle}");
            Assert.AreEqual(expectedStage, h.Runner.LastEnding.emotion_arc_stage, $"{tag} 情绪弧应映射为 {expectedStage}");
            Assert.AreEqual(tag, h.Snap.codex[0].ending_tag, "CodexEntry.ending_tag 应与 tag 一致");
        }

        // ── S5-2 EventBus 集成：订阅 EVT_ARCHIVED，Archive 后恰好 1 次且载荷 entry_id 正确 ──
        [Test]
        public void Archive_PublishesArchivedEvent_Once_WithCorrectPayload()
        {
            var h = new Harness();
            var c = Harness.Deliverable("com_evt");
            h.Runner.Archive(c);

            Assert.AreEqual(1, h.Rec.Count(GameEvents.EVT_ARCHIVED), "应经 EventBus 广播恰好一次 EVT_ARCHIVED");
            var payload = h.Rec.Last(GameEvents.EVT_ARCHIVED) as ArchivedEvent;
            Assert.IsNotNull(payload, "EVT_ARCHIVED 载荷应为 ArchivedEvent");
            Assert.AreEqual("cx_com_evt", payload.entry_id, "载荷 entry_id 应正确");
            Assert.AreEqual(0, payload.timeline_order, "载荷 timeline_order 应为 0");
            Assert.IsFalse(payload.is_mainplot, "载荷 is_mainplot 应与 commission 一致（此处 false）");
        }

        // ── 事件常量唯一性（S5 新增 EVT_ARCHIVED 不得破坏 C2 命名唯一）──
        [Test]
        public void Archived_EventConstant_IsSelfNamed()
            => Assert.AreEqual("EVT_ARCHIVED", GameEvents.EVT_ARCHIVED);
    }
}
