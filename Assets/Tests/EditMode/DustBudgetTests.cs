// DustBudgetTests.cs — Phase 7-C1 拂尘表现层分辨率封顶单测（EditMode，纯 C# Runtime）。
// 覆盖：DustBudget.CapGrid 等比封顶数学 + SessionRunner.VisualDustResolution 经 EffectiveDustCellCap 接入。
// 不碰真机/Shader；真机帧率验收见 game/production/phase6-device-fallback.md。
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Runtime;

namespace Weiguang.Tests
{
    [TestFixture]
    public class DustBudgetTests
    {
        // ── CapGrid：原网格已满足上限 → 原样返回 ──
        [Test]
        public void CapGrid_BelowCap_Unchanged()
        {
            var (w, h) = DustBudget.CapGrid(64, 8, 8);
            Assert.AreEqual((8, 8), (w, h), "8×8=64 已满足上限，应原样");
        }

        // ── CapGrid：maxCells<=0 视为不约束 ──
        [Test]
        public void CapGrid_NoCap_ReturnsOriginal()
        {
            var (w, h) = DustBudget.CapGrid(0, 12, 12);
            Assert.AreEqual((12, 12), (w, h));
            var (w2, h2) = DustBudget.CapGrid(-5, 12, 12);
            Assert.AreEqual((12, 12), (w2, h2));
        }

        // ── CapGrid：非法尺寸 → (1,1) ──
        [Test]
        public void CapGrid_InvalidSize_ReturnsOne()
        {
            Assert.AreEqual((1, 1), DustBudget.CapGrid(36, 0, 5));
            Assert.AreEqual((1, 1), DustBudget.CapGrid(36, 5, -1));
        }

        // ── CapGrid：12×12=144 降 36 → 6×6 ──
        [Test]
        public void CapGrid_Square_DownscalesToFit()
        {
            var (w, h) = DustBudget.CapGrid(36, 12, 12);
            Assert.AreEqual(36, w * h, "总格应恰好 36");
            Assert.AreEqual((6, 6), (w, h));
        }

        // ── CapGrid：8×8 全开档 64 → 原样；降档 36 → 6×6 ──
        [Test]
        public void CapGrid_FullVsLowTier()
        {
            Assert.AreEqual((8, 8), DustBudget.CapGrid(64, 8, 8));
            Assert.AreEqual((6, 6), DustBudget.CapGrid(36, 8, 8), "8×8=64 降到 ≤36 取 6×6");
        }

        // ── CapGrid：非正方 10×20=200 降 64 → 总格 ≤64，保长宽比 ──
        [Test]
        public void CapGrid_NonSquare_StaysWithinCap()
        {
            var (w, h) = DustBudget.CapGrid(64, 10, 20);
            Assert.LessOrEqual(w * h, 64, "总格必须 ≤ 上限");
            Assert.GreaterOrEqual(w, 1);
            Assert.GreaterOrEqual(h, 1);
            // 长宽比近似保留（10:20=1:2），6×11=66>64 → 钳回 6×10 或 5×11
            Assert.AreEqual(2.0, (double)w / h, 0.2, "应保持约 1:2 长宽比");
        }

        // ── SessionRunner.VisualDustResolution 经 EffectiveDustCellCap 接入 ──
        [Test]
        public void VisualDustResolution_RespectsQualityCap()
        {
            var bus = new EventBus();
            var fsm = new CommissionStateMachine(bus);
            var save = new SaveEngine(new FakeStorage(), Build.DIR, bus)
            {
                Serialize = FakeJson.Write,
                Deserialize = FakeJson.Read
            };

            // 降级档：maxDustCells=36 → 8×8 CSV 应封到 6×6
            var lowQ = new RuntimeQuality { maxDustCells = 36 };
            var runnerLow = new SessionRunner(bus, fsm, save, () => new SaveSnapshot(), lowQ);
            Assert.AreEqual((6, 6), runnerLow.VisualDustResolution(8, 8), "降级档应封到 6×6");

            // 全开档：默认 64 → 8×8 原样
            var runnerFull = new SessionRunner(bus, fsm, save, () => new SaveSnapshot());
            Assert.AreEqual((8, 8), runnerFull.VisualDustResolution(8, 8), "全开档应原样 8×8");
        }

        // ── 硬上限 64：即便 quality.maxDustCells 大于 64 也不破封顶 ──
        [Test]
        public void VisualDustResolution_NeverExceeds64()
        {
            var bus = new EventBus();
            var fsm = new CommissionStateMachine(bus);
            var save = new SaveEngine(new FakeStorage(), Build.DIR, bus)
            {
                Serialize = FakeJson.Write,
                Deserialize = FakeJson.Read
            };
            var q = new RuntimeQuality { maxDustCells = 999 };
            var runner = new SessionRunner(bus, fsm, save, () => new SaveSnapshot(), q);
            var (w, h) = runner.VisualDustResolution(8, 8);
            Assert.AreEqual((8, 8), (w, h), "8×8 已 ≤64，封顶不变");
            var (w2, h2) = runner.VisualDustResolution(10, 10);
            Assert.LessOrEqual(w2 * h2, 64, "即便 quality 给 999，封顶硬上限 64");
        }
    }
}
