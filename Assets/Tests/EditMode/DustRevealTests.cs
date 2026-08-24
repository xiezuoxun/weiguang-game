// DustRevealTests.cs — Sprint 2 S1-1~S1-5 拂尘显影真实化单测（EditMode，纯 C# Core）。
// 覆盖：DustGrid 逐格精度、RevealThresholdTracker once-lock、边界钳制、EventBus 集成恰好 3 次 whisper。
// 与 SmokeTests 互不冲突（独立文件、独立 fixture）；不改任何既有测试。
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Tests;

namespace Weiguang.Tests
{
    [TestFixture]
    public class DustRevealTests
    {
        // ── S1-1 DustGrid 逐格 reveal 精度 ──────────────────────────
        [Test]
        public void DustGrid_RevealRatio_EqualsRevealedOverTotal()
        {
            var g = new DustGrid { width = 4, height = 3, revealed = new bool[12] };
            Assert.AreEqual(0f, g.RevealPct());

            // 拂开 5 格（非连续，验证精确除法）
            g.RevealCell(0, 0); g.RevealCell(3, 0); g.RevealCell(1, 1); g.RevealCell(2, 2); g.RevealCell(0, 2);
            Assert.AreEqual(5f / 12f, g.RevealPct(), 1e-6f, "RevealRatio 必须精确等于 拂格/总格");

            // 重复拂同一格不改变计数（幂等）
            g.RevealCell(0, 0);
            Assert.AreEqual(5f / 12f, g.RevealPct(), 1e-6f);

            // 越界坐标被忽略，不崩
            Assert.IsFalse(g.RevealCell(-1, 0));
            Assert.IsFalse(g.RevealCell(4, 0));
            Assert.IsFalse(g.RevealCell(0, 3));
            Assert.AreEqual(5f / 12f, g.RevealPct(), 1e-6f);

            // 拂满
            g.RevealAll();
            Assert.AreEqual(1f, g.RevealPct(), 1e-6f);
        }

        [Test]
        public void DustGrid_EmptyGrid_RevealPct_IsZero()
            => Assert.AreEqual(0f, new DustGrid { width = 0, height = 0, revealed = new bool[0] }.RevealPct());

        // ── S1-2 RevealThresholdTracker once-lock ──────────────────
        [Test]
        public void Tracker_OnceLock_FiresEachThresholdExactlyOnce()
        {
            var t = new RevealThresholdTracker();
            var fired = new List<float>();

            // 连续越过 25/50，未到 75
            t.Update(0.3f, fired.Add);
            t.Update(0.4f, fired.Add);
            t.Update(0.6f, fired.Add);
            CollectionAssert.AreEqual(new[] { 0.25f, 0.50f }, fired, "越 25/50 各触发一次，顺序升档");

            // 越过 75
            t.Update(0.8f, fired.Add);
            CollectionAssert.AreEqual(new[] { 0.25f, 0.50f, 0.75f }, fired, "0.8 触发 75");

            // 重复调用不得再次触发（once-lock）
            int before = fired.Count;
            t.Update(0.9f, fired.Add);
            Assert.AreEqual(before, fired.Count, "重复 Update(0.9) 不得再触发任何档");
            t.Update(0.75f, fired.Add);
            t.Update(1f, fired.Add);
            Assert.AreEqual(before, fired.Count, "已锁定后任意高值不再触发");
        }

        [Test]
        public void Tracker_OutOfOrder_FiresAllOnce()
        {
            // 直接到 1.0 应一次性触发三档（各一次），顺序 25/50/75
            var t = new RevealThresholdTracker();
            var fired = new List<float>();
            t.Update(1f, fired.Add);
            CollectionAssert.AreEqual(new[] { 0.25f, 0.50f, 0.75f }, fired);
        }

        // ── S1-2 边界：reveal_pct 钳制 [0,1] ───────────────────────
        [Test]
        public void Tracker_ClampsInput_ToUnitInterval()
        {
            var t = new RevealThresholdTracker();
            var fired = new List<float>();
            t.Update(-0.1f, fired.Add); // 夹到 0 → 不触发任何档
            Assert.IsEmpty(fired, "负值被夹到 0，不得触发 25");

            var t2 = new RevealThresholdTracker();
            var fired2 = new List<float>();
            t2.Update(1.2f, fired2.Add); // 夹到 1 → 触发全部三档
            CollectionAssert.AreEqual(new[] { 0.25f, 0.50f, 0.75f }, fired2, "超 1 值被夹到 1，触发全部");
        }

        // ── S1-3/S1-4 EventBus 集成：全流程恰好 3 次 whisper，无重复 ─
        [Test]
        public void RevealFlow_PublishesExactlyThreeWhispers_NoDuplicate()
        {
            var bus = new EventBus();
            var rec = new EventRecorder(bus, GameEvents.EVT_REVEAL_WHISPER, GameEvents.EVT_REVEAL_COMPLETE);

            // 复刻 S1-4 驱动逻辑（Core 层确定性模拟）：逐格 reveal → tracker → 发 whisper
            var grid = new DustGrid { width = 5, height = 4, revealed = new bool[20] };
            var tracker = new RevealThresholdTracker();
            for (int y = 0; y < grid.height; y++)
                for (int x = 0; x < grid.width; x++)
                {
                    grid.RevealCell(x, y);
                    tracker.Update(grid.RevealPct(), t =>
                    {
                        string key = RevealThresholdTracker.KeyOf(t);
                        bus.Publish(GameEvents.EVT_REVEAL_WHISPER, new RevealWhisperEvent(key, t));
                    });
                }

            // 恰好 3 次 whisper
            Assert.AreEqual(3, rec.Count(GameEvents.EVT_REVEAL_WHISPER), "浮纸签只应在 25/50/75 各发一次");
            var keys = rec.Payloads(GameEvents.EVT_REVEAL_WHISPER)
                           .Cast<RevealWhisperEvent>()
                           .Select(e => e.whisper_key)
                           .ToList();
            CollectionAssert.AreEquivalent(
                new[] { RevealWhisperEvent.WHISPER_25, RevealWhisperEvent.WHISPER_50, RevealWhisperEvent.WHISPER_75 },
                keys, "三档 key 各出现一次，无重复、无遗漏");

            // 文案允许为空（设计侧后续填），工程侧不依赖内容
            foreach (var e in rec.Payloads(GameEvents.EVT_REVEAL_WHISPER).Cast<RevealWhisperEvent>())
                Assert.IsNull(e.text, "本 PR 文案可为空，不得因空文案崩溃");

            // 拂满后发 REVEAL_COMPLETE（reveal_pct=1 ≥ threshold 0.85）
            bus.Publish(GameEvents.EVT_REVEAL_COMPLETE, grid.RevealPct());
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_REVEAL_COMPLETE));
        }

        // ── 事件名常量唯一性（S1-3 新增 EVT_REVEAL_WHISPER 不得破坏 C2）──
        [Test]
        public void RevealWhisper_EventConstant_IsSelfNamed()
            => Assert.AreEqual("EVT_REVEAL_WHISPER", GameEvents.EVT_REVEAL_WHISPER);
    }
}
