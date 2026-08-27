// OnboardingFlowTests.cs — Phase 7-C2 首启引导流转单测（EditMode，纯 C#，无 Canvas 依赖）。
// 覆盖：首启弹 step0 / 前进×4 完成并持久化 / 已引导过不再弹 / 跳过直接收尾。
// 用内存替身 FakeStore 取代 PlayerPrefs，避免 EditMode 触碰真机持久化。
using System.Collections.Generic;
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Runtime.Onboarding;

namespace Weiguang.Tests
{
    /// <summary>内存版 IOnboardingStore 替身（EditMode 不碰 PlayerPrefs）。</summary>
    public class FakeStore : IOnboardingStore
    {
        public bool Done;
        public bool IsOnboarded() => Done;
        public void MarkOnboarded() => Done = true;
    }

    /// <summary>记录型 IOnboardingView 替身（断言展示/完成调用）。</summary>
    public class SpyView : IOnboardingView
    {
        public readonly List<(int index, string title, string hint)> Shown = new List<(int, string, string)>();
        public int Completed;
        public void ShowStep(int index, int total, string title, string hint) => Shown.Add((index, title, hint));
        public void OnCompleted() => Completed++;
    }

    [TestFixture]
    public class OnboardingFlowTests
    {
        static FirstLaunchEvent SampleEvent() => new FirstLaunchEvent(
            OnboardingHints.Of("reveal"), OnboardingHints.Of("assemble"),
            OnboardingHints.Of("choose"), OnboardingHints.Of("archive"));

        // ── 首启：应弹第 0 步（拂尘），且仅弹一步 ──
        [Test]
        public void Start_ShowsFirstStep_Only()
        {
            var spy = new SpyView();
            var flow = new OnboardingFlow(spy, new FakeStore());
            flow.Start(SampleEvent());

            Assert.IsTrue(flow.Active, "首启后应处于引导中");
            Assert.AreEqual(1, spy.Shown.Count, "首启只应弹第一步");
            Assert.AreEqual(0, spy.Shown[0].index);
            Assert.AreEqual(OnboardingHints.REVEAL_TITLE, spy.Shown[0].title);
        }

        // ── 前进×4：第 4 次 AdvanceStep 走完 → OnCompleted + 持久化 ──
        [Test]
        public void AdvanceThroughFourSteps_CompletesAndPersists()
        {
            var spy = new SpyView();
            var store = new FakeStore();
            var flow = new OnboardingFlow(spy, store);
            flow.Start(SampleEvent());

            flow.AdvanceStep(); // → assemble (1)
            flow.AdvanceStep(); // → choose  (2)
            flow.AdvanceStep(); // → archive (3)
            Assert.AreEqual(4, spy.Shown.Count, "前应已弹 4 步");
            Assert.AreEqual(OnboardingHints.ARCHIVE_TITLE, spy.Shown[3].title);
            Assert.IsTrue(flow.Active, "第 4 步仍在进行中");

            flow.AdvanceStep(); // 走完 → Complete
            Assert.IsFalse(flow.Active, "走完应退出引导中");
            Assert.AreEqual(1, spy.Completed, "应触发 OnCompleted");
            Assert.IsTrue(store.IsOnboarded(), "应持久化：已引导");
        }

        // ── 已引导过：再次 Start 不弹 ──
        [Test]
        public void AlreadyOnboarded_StartDoesNotShow()
        {
            var spy = new SpyView();
            var store = new FakeStore { Done = true }; // 已引导
            var flow = new OnboardingFlow(spy, store);
            flow.Start(SampleEvent());

            Assert.IsFalse(flow.Active, "已引导不应进入引导中");
            Assert.AreEqual(0, spy.Shown.Count, "已引导不应弹任何步");
        }

        // ── 跳过：直接收尾并持久化 ──
        [Test]
        public void SkipAll_CompletesImmediately()
        {
            var spy = new SpyView();
            var store = new FakeStore();
            var flow = new OnboardingFlow(spy, store);
            flow.Start(SampleEvent());

            flow.SkipAll();
            Assert.IsFalse(flow.Active, "跳过应退出引导中");
            Assert.AreEqual(1, spy.Completed, "跳过应触发 OnCompleted");
            Assert.IsTrue(store.IsOnboarded(), "跳过应持久化");
            Assert.AreEqual(1, spy.Shown.Count, "跳过不应继续弹后续步");
        }

        // ── 走完后继续 AdvanceStep 无效（幂等）──
        [Test]
        public void AdvanceAfterComplete_IsNoOp()
        {
            var spy = new SpyView();
            var store = new FakeStore();
            var flow = new OnboardingFlow(spy, store);
            flow.Start(SampleEvent());
            // 直接走到完成
            for (int i = 0; i < 5; i++) flow.AdvanceStep();
            int completed = spy.Completed;
            int shown = spy.Shown.Count;

            flow.AdvanceStep(); // 应无操作
            Assert.AreEqual(completed, spy.Completed, "完成后 AdvanceStep 不应再触发 OnCompleted");
            Assert.AreEqual(shown, spy.Shown.Count, "完成后 AdvanceStep 不应再弹步");
        }
    }
}
