// ChoiceTests.cs — Sprint 2 S3-1~S3-5 抉择分支叙事真实化单测（EditMode，纯 C# Core）。
// 覆盖：SelectOption 合法落定、非法 optionId fail-fast、ChoiceHitTester 容差边界、
//       ChoiceWordingGuard 字数约束、EndingTag 三态单层语义。
// 与 SmokeTests/DustRevealTests/AssemblyTests 互不冲突（独立文件、独立 fixture）；不改任何既有测试。
using System;
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Runtime;
using Weiguang.Tests;

namespace Weiguang.Tests
{
    [TestFixture]
    public class ChoiceTests
    {
        // ── 测试装置：可变的 SaveSnapshot（SessionRunner 经 Func<SaveSnapshot> _snap 读取）──
        sealed class Harness
        {
            public SaveSnapshot Snap;
            public readonly EventBus Bus = new EventBus();
            public readonly EventRecorder Rec;
            public readonly Commission C;
            public readonly SessionRunner Runner;

            public Harness(int choiceCount = 3, bool preloadNode = false)
            {
                C = Build.NewCommission("com_c", fragmentCount: 0, phase: CommissionPhase.Choosing);
                C.choice_count = choiceCount;
                Snap = Build.NewSnapshot(C, SaveNode.NodeChoose);
                if (preloadNode)
                {
                    var node = new ChoiceNode { node_id = "cn_com_c", commission_id = C.commission_id };
                    for (int i = 0; i < choiceCount; i++)
                        node.options.Add(new ChoiceOption
                        {
                            option_id = $"op{i}", wording = "（待填措辞）",
                            truth_level = 0.5f * i, ending_tag = (EndingTag)(i % 3), client_reaction = "…", sdt_autonomy_weight = 0.5f
                        });
                    Snap.choice_states.Add(node);
                }
                var fsm = new CommissionStateMachine(Bus);
                var save = new SaveEngine(new FakeStorage(), Build.DIR, Bus);
                Runner = new SessionRunner(Bus, fsm, save, () => Snap);
                Rec = new EventRecorder(Bus, GameEvents.EVT_CHOICE_MADE);
            }
        }

        // ── S3-1 StartChoose 加载：缺失 node 时按 choice_count 新建占位，不自动选 ──
        [Test]
        public void StartChoose_CreatesPlaceholder_WhenNodeMissing_AndDoesNotAutoSelect()
        {
            var h = new Harness(choiceCount: 3);
            h.Runner.StartChoose(h.C);
            var node = h.Snap.choice_states.Find(n => n.commission_id == h.C.commission_id);
            Assert.IsNotNull(node, "缺失 node 应新建占位 ChoiceNode");
            Assert.AreEqual(3, node.options.Count, "3 选项委托应建 3 个占位 option");
            Assert.AreEqual("（待填措辞）", node.options[0].wording, "占位 wording 应合规（≤26 字）");
            // StartChoose 内仍走默认"选第一个"路径（SelectOption）→ 实际已落定
            Assert.AreEqual(node.options[0].option_id, node.selected_option_id, "StartChoose 默认确定性选第一个（经 SelectOption）");
        }

        // ── S3-2 SelectOption 合法落定：写入 selected_option_id、_lastTag 正确、广播事件 ──
        [Test]
        public void SelectOption_ValidId_WritesSelectionAndTag_AndPublishesEvent()
        {
            var h = new Harness(preloadNode: true);
            h.Runner.SelectOption(h.C, "op2");
            var node = h.Snap.choice_states.Find(n => n.commission_id == h.C.commission_id);
            Assert.AreEqual("op2", node.selected_option_id, "应写入玩家所选的 option_id");
            Assert.AreEqual(EndingTag.Reframe, node.options[2].ending_tag, "op2 的 ending_tag 应为 Reframe（i%3）");
            Assert.AreEqual(1, h.Rec.Count(GameEvents.EVT_CHOICE_MADE), "应经 EventBus 广播恰好一次 EVT_CHOICE_MADE");
            Assert.AreEqual(EndingTag.Reframe, h.Rec.Last(GameEvents.EVT_CHOICE_MADE), "事件载荷应为所选 tag");
        }

        // ── S3-2 SelectOption 非法 optionId：fail-fast 抛 ContractViolationException ──
        [Test]
        public void SelectOption_InvalidId_ThrowsContractViolation()
        {
            var h = new Harness(preloadNode: true);
            var ex = Assert.Throws<ContractViolationException>(() => h.Runner.SelectOption(h.C, "op99"));
            Assert.IsNotNull(ex, "非法 optionId 必须抛 ContractViolationException（fail-fast）");
            var node = h.Snap.choice_states.Find(n => n.commission_id == h.C.commission_id);
            Assert.IsTrue(string.IsNullOrEmpty(node.selected_option_id), "非法点选不得写入 selected_option_id");
        }

        // ── S3-3 ChoiceHitTester：0.6× 半径内命中 ──
        [Test]
        public void HitTester_Hits_WithinSixtyPercentRadius()
        {
            // 偏离 0.6× 基准半径（在 1.3× 命中半径内）→ 命中
            Assert.IsTrue(ChoiceHitTester.HitAtDistance(0.6f, 1f), "偏离 0.6× 半径内应命中");
            Assert.IsTrue(ChoiceHitTester.HitAtDistance(0.0f, 1f), "正中心必命中");
        }

        // ── S3-3 ChoiceHitTester：恰好 1.3× 边界命中 ──
        [Test]
        public void HitTester_Hits_AtBoundaryRadius()
        {
            // 1.3× 边界（含等于）→ 命中
            Assert.IsTrue(ChoiceHitTester.HitAtDistance(1.3f, 1f), "恰好 1.3× 命中半径边界应命中");
        }

        // ── S3-3 ChoiceHitTester：>1.3× 不命中 ──
        [Test]
        public void HitTester_Misses_BeyondRadius()
        {
            // 偏离 >1.3× 基准半径 → 不命中
            Assert.IsFalse(ChoiceHitTester.HitAtDistance(1.4f, 1f), "偏离 1.4× 半径应不命中");
            Assert.IsFalse(ChoiceHitTester.HitAtDistance(2.0f, 1f), "偏离 2.0× 半径应不命中");
        }

        // ── S3-3 ChoiceHitTester：退化半径安全 ──
        [Test]
        public void HitTester_SafeOnZeroRadius()
            => Assert.IsFalse(ChoiceHitTester.Hit(0f, 0f, 0f, 0f, 0f), "基准半径=0 不得命中（无热区）");

        // ── S3-4 ChoiceWordingGuard：3 选项 26 字 PASS、27 字 FAIL ──
        [Test]
        public void WordingGuard_ThreeOptions_26Pass_27Fail()
        {
            // 26 个全角字
            var ok = new string('中', 26);
            Assert.DoesNotThrow(() => ChoiceWordingGuard.ValidateWording(ok, 3), "26 字全角（3 选项）应 PASS");
            // 27 个全角字
            var bad = new string('中', 27);
            Assert.Throws<ContractViolationException>(() => ChoiceWordingGuard.ValidateWording(bad, 3), "27 字全角（3 选项）应 FAIL");
        }

        // ── S3-4 ChoiceWordingGuard：2 选项 39 字 PASS、40 字 FAIL ──
        [Test]
        public void WordingGuard_TwoOptions_39Pass_40Fail()
        {
            var ok = new string('中', 39);
            Assert.DoesNotThrow(() => ChoiceWordingGuard.ValidateWording(ok, 2), "39 字全角（2 选项）应 PASS");
            var bad = new string('中', 40);
            Assert.Throws<ContractViolationException>(() => ChoiceWordingGuard.ValidateWording(bad, 2), "40 字全角（2 选项）应 FAIL");
        }

        // ── S3-4 ChoiceWordingGuard：含半角混合计数正确（半角字母数字=0.5）──
        [Test]
        public void WordingGuard_MixedHalfwidth_CountsHalf()
        {
            // "abc" = 1.5，"中文" = 2，合计 3.5（3 选项上限 26 → PASS）
            string mixed = "abc中文";
            Assert.AreEqual(3.5f, ChoiceWordingGuard.CountWording(mixed), 1e-6f, "半角 abc=1.5 + 全角中文=2 = 3.5");
            Assert.DoesNotThrow(() => ChoiceWordingGuard.ValidateWording(mixed, 3), "3.5 ≤ 26 应 PASS");

            // 构造恰好 = 26.5（应 FAIL）：25 全角 + 3 半角字母 = 25 + 1.5 = 26.5 > 26
            var borderline = new string('中', 25) + "abc";
            Assert.AreEqual(26.5f, ChoiceWordingGuard.CountWording(borderline), 1e-6f);
            Assert.Throws<ContractViolationException>(() => ChoiceWordingGuard.ValidateWording(borderline, 3), "26.5 > 26（3 选项）应 FAIL");
        }

        // ── S3-5 EndingTag 三态单层语义：选不同 option 得不同 tag，无多 tag 叠加 ──
        [Test]
        public void EndingTag_ThreeStates_SingleLayer_NoStack()
        {
            var h = new Harness(preloadNode: true);
            // op0→Truth, op1→Omit, op2→Reframe（i%3）
            h.Runner.SelectOption(h.C, "op0");
            Assert.AreEqual(EndingTag.Truth, h.Rec.Last(GameEvents.EVT_CHOICE_MADE), "op0 → Truth");
            h.Runner.SelectOption(h.C, "op1");
            Assert.AreEqual(EndingTag.Omit, h.Rec.Last(GameEvents.EVT_CHOICE_MADE), "op1 → Omit（切换为单层，无叠加）");
            h.Runner.SelectOption(h.C, "op2");
            Assert.AreEqual(EndingTag.Reframe, h.Rec.Last(GameEvents.EVT_CHOICE_MADE), "op2 → Reframe（单层，R3）");

            // 事件只记录最近一次 tag（无多 tag 叠加：每次 SelectOption 重写 _lastTag）
            Assert.AreEqual(3, h.Rec.Count(GameEvents.EVT_CHOICE_MADE), "三次点选各广播一次（单层 tag，非叠加）");
        }

        // ── 事件常量唯一性（S3 复用 EVT_CHOICE_MADE 不得破坏 C2）──
        [Test]
        public void ChoiceMade_EventConstant_IsSelfNamed()
            => Assert.AreEqual("EVT_CHOICE_MADE", GameEvents.EVT_CHOICE_MADE);
    }
}
