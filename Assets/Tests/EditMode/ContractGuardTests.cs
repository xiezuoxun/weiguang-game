// ContractGuardTests.cs — 契约边界单测（GDD 00 §2 契约 + §5 护栏；控制清单 C1/C6）。
// 覆盖矩阵：每个数值字段 × {下界内, 下界, 上界, 上界外} → 合规放行 / 越界暴露。
// 双 API 并测：
//   · ContractGuard.Validate(...)        → 返回错误清单（导入期"全量报错不中断"）
//   · ContractGuardAssert.ThrowIfInvalid → 抛 ContractViolationException（运行时激活门硬拒绝）
// 断言粒度到"错误消息含字段名"，防止将来改判定后错误指向错字段而测试仍然绿。
using System.Linq;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class ContractGuardTests
    {
        static bool Mentions(System.Collections.Generic.List<string> errs, string field)
            => errs.Any(e => e.Contains(field));

        // ── 合规基线 ─────────────────────────────────────────────
        [Test]
        public void ValidCommission_NoErrors()
        {
            var errs = ContractGuard.Validate(Build.NewCommission());
            Assert.AreEqual(0, errs.Count, "合规委托不应报错：" + string.Join(" | ", errs));
            Assert.IsTrue(ContractGuardAssert.IsValid(Build.NewCommission()));
            Assert.DoesNotThrow(() => ContractGuardAssert.ThrowIfInvalid(Build.NewCommission()));
        }

        [Test]
        public void NullCommission_ReportedNotCrashed()
        {
            var errs = ContractGuard.Validate((Commission)null);
            Assert.AreEqual(1, errs.Count);
            StringAssert.Contains("null", errs[0]);
            Assert.Throws<ContractViolationException>(() => ContractGuardAssert.ThrowIfInvalid((Commission)null));
        }

        // ── reveal_threshold ∈ [0,1]（C3：默认 0.85，S4 唯一写入方）────
        [TestCase(0f, true)]
        [TestCase(0.85f, true)]
        [TestCase(1f, true)]
        [TestCase(-0.01f, false)]
        [TestCase(1.01f, false)]
        [TestCase(2f, false)]
        public void RevealThreshold_Boundary(float value, bool expectValid)
        {
            var c = Build.NewCommission();
            c.reveal_threshold = value;
            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(expectValid, errs.Count == 0, "reveal_threshold=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid) Assert.IsTrue(Mentions(errs, "reveal_threshold"), "错误未指向 reveal_threshold");
        }

        // ── fragment_count ∈ [0,6]（C6；0 表示无拼合环节）──────────────
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(6, true)]
        [TestCase(-1, false)]
        [TestCase(7, false)]
        [TestCase(99, false)]
        public void FragmentCount_Boundary(int value, bool expectValid)
        {
            var c = Build.NewCommission(fragmentCount: value);
            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(expectValid, errs.Count == 0, "fragment_count=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid) Assert.IsTrue(Mentions(errs, "fragment_count"), "错误未指向 fragment_count");
        }

        // ── choice_count ∈ [2,3]（R3 单层：越界须拒绝激活）─────────────
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(4, false)]
        public void ChoiceCount_Boundary(int value, bool expectValid)
        {
            var c = Build.NewCommission();
            c.choice_count = value;
            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(expectValid, errs.Count == 0, "choice_count=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid)
            {
                Assert.IsTrue(Mentions(errs, "choice_count"), "错误未指向 choice_count");
                Assert.IsTrue(Mentions(errs, "拒绝激活"), "choice_count 越界必须给出拒绝激活语义（S4 ④）");
            }
        }

        // ── ending_variants ≥ 2 ─────────────────────────────────────
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(1, false)]
        [TestCase(0, false)]
        [TestCase(-5, false)]
        public void EndingVariants_Boundary(int value, bool expectValid)
        {
            var c = Build.NewCommission();
            c.ending_variants = value;
            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(expectValid, errs.Count == 0, "ending_variants=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid) Assert.IsTrue(Mentions(errs, "ending_variants"), "错误未指向 ending_variants");
        }

        // ── session_soft_budget_min ∈ [5,10]（概念 §8 单次 5–10min）────
        [TestCase(5f, true)]
        [TestCase(7.5f, true)]
        [TestCase(10f, true)]
        [TestCase(4.9f, false)]
        [TestCase(10.1f, false)]
        [TestCase(0f, false)]
        public void SessionSoftBudget_Boundary(float value, bool expectValid)
        {
            var c = Build.NewCommission();
            c.session_soft_budget_min = value;
            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(expectValid, errs.Count == 0, "session_soft_budget_min=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid) Assert.IsTrue(Mentions(errs, "session_soft_budget_min"), "错误未指向 session_soft_budget_min");
        }

        // ── relationship_level ∈ [0,5] ─────────────────────────────
        [TestCase(0, true)]
        [TestCase(5, true)]
        [TestCase(-1, false)]
        [TestCase(6, false)]
        public void RelationshipLevel_Boundary(int value, bool expectValid)
        {
            var cl = Build.NewClient(level: value);
            var errs = ContractGuard.Validate(cl);
            Assert.AreEqual(expectValid, errs.Count == 0, "relationship_level=" + value + " 判定错：" + string.Join(" | ", errs));
            if (!expectValid) Assert.IsTrue(Mentions(errs, "relationship_level"), "错误未指向 relationship_level");
        }

        [Test]
        public void NullClient_ReportedNotCrashed()
        {
            var errs = ContractGuard.Validate((Client)null);
            Assert.AreEqual(1, errs.Count);
            Assert.Throws<ContractViolationException>(() => ContractGuardAssert.ThrowIfInvalid((Client)null));
        }

        // ── 多重违规：一次全报（导入期要求"不中断遍历"）────────────────
        [Test]
        public void MultipleViolations_AllReportedInOnePass()
        {
            var c = Build.NewCommission(fragmentCount: 9);
            c.reveal_threshold = 1.5f;
            c.choice_count = 1;
            c.ending_variants = 0;
            c.session_soft_budget_min = 99f;

            var errs = ContractGuard.Validate(c);
            Assert.AreEqual(5, errs.Count, "5 处越界应一次全报，实际：" + string.Join(" | ", errs));
            foreach (var field in new[] { "fragment_count", "reveal_threshold", "choice_count", "ending_variants", "session_soft_budget_min" })
                Assert.IsTrue(Mentions(errs, field), "缺少字段报错：" + field);
            // 每条错误都带 commission_id，便于 CSV 定位
            Assert.IsTrue(errs.All(e => e.Contains("com_t")), "错误消息须带 commission_id");
        }

        // ── fail-fast 包装：异常消息须含全部违规项 ────────────────────
        [Test]
        public void ThrowIfInvalid_ExceptionCarriesAllViolations()
        {
            var c = Build.NewCommission(fragmentCount: 7);
            c.choice_count = 1;

            var ex = Assert.Throws<ContractViolationException>(() => ContractGuardAssert.ThrowIfInvalid(c));
            Assert.AreEqual(2, ex.Violations.Count);
            StringAssert.Contains("fragment_count", ex.Message);
            StringAssert.Contains("choice_count", ex.Message);
            Assert.IsFalse(ContractGuardAssert.IsValid(c));
        }

        [Test]
        public void ThrowIfInvalid_ReturnsSameInstance_WhenValid()
        {
            var c = Build.NewCommission();
            Assert.AreSame(c, ContractGuardAssert.ThrowIfInvalid(c), "合规时应原样返回，便于链式调用");
        }

        // ── 枚举契约：值集不得漂移（C1 契约冻结的静态守护）─────────────
        [Test]
        public void Enums_MatchFrozenContract()
        {
            CollectionAssert.AreEquivalent(
                new[] { "Paper", "Clock", "Letter", "Ornament", "Other" },
                System.Enum.GetNames(typeof(ItemType)), "ItemType 值集漂移（GDD 00 §2.1 已冻结）");
            CollectionAssert.AreEquivalent(
                new[] { "Wood", "Paper", "Glass", "Dust" },
                System.Enum.GetNames(typeof(Material)), "Material 值集漂移（对应美术圣经 §4 四肌理）");
            CollectionAssert.AreEquivalent(
                new[] { "Truth", "Omit", "Reframe" },
                System.Enum.GetNames(typeof(EndingTag)), "EndingTag 必须是 R3 单层三态");
            CollectionAssert.AreEquivalent(
                new[] { "Idle", "Received", "Examining", "Revealing", "Assembling", "Choosing", "Delivering", "Archived" },
                System.Enum.GetNames(typeof(CommissionPhase)), "CommissionPhase 八态漂移（S4 ②）");
            CollectionAssert.AreEquivalent(
                new[] { "NodeReceive", "NodeExamine", "NodeReveal", "NodeAssemble", "NodeChoose", "NodeDeliver", "NodeArchive" },
                System.Enum.GetNames(typeof(SaveNode)), "SaveNode 七断点漂移（S6 ②）");
        }

        // ── DustGrid：reveal_pct 计算（S1 判定阈值的输入）──────────────
        [Test]
        public void DustGrid_RevealPct_MatchesRevealedRatio()
        {
            var g = new DustGrid { width = 4, height = 5, revealed = new bool[20] };
            Assert.AreEqual(20, g.TotalCells());
            Assert.AreEqual(0f, g.RevealPct(), 1e-6f);

            for (int i = 0; i < 17; i++) g.revealed[i] = true;   // 17/20 = 0.85 恰好等于默认阈值
            Assert.AreEqual(17, g.RevealedCount());
            Assert.AreEqual(0.85f, g.RevealPct(), 1e-6f);
            Assert.IsTrue(g.RevealPct() >= Build.NewCommission().reveal_threshold, "恰好 0.85 应视为达阈（>=）");
        }

        [Test]
        public void DustGrid_EmptyOrNull_DoesNotDivideByZero()
        {
            Assert.AreEqual(0f, new DustGrid { width = 0, height = 0, revealed = null }.RevealPct(), 1e-6f);
            Assert.AreEqual(0, new DustGrid { width = 3, height = 3, revealed = null }.RevealedCount());
        }
    }
}
