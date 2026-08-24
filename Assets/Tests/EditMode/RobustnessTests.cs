// RobustnessTests.cs — 打磨阶段工程健壮性单测（EditMode）。
// 覆盖：首启动引导事件、存档失败用户回调、RuntimeQuality 降级默认值、SessionRunner 降级配置接入。
// 不依赖 Unity 渲染，纯逻辑层验证（沿用现有 Weiguang.Tests.EditMode 风格）。
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Runtime;
using Weiguang.Tests; // 复用 TestKit.FakeStorage
using System.Collections.Generic;

namespace Weiguang.Tests.EditMode
{
    [TestFixture]
    public class RobustnessTests
    {
        // ── 首启动引导事件 ──
        [Test]
        public void FirstLaunch_Event_Carries_AllFourVerbHints()
        {
            var bus = new EventBus { LogError = m => Assert.Fail(m) };
            FirstLaunchEvent captured = null;
            bus.Subscribe(GameEvents.EVT_FIRST_LAUNCH, p => captured = p as FirstLaunchEvent);

            bus.Publish(GameEvents.EVT_FIRST_LAUNCH, new FirstLaunchEvent(
                OnboardingHints.Of("reveal"), OnboardingHints.Of("assemble"),
                OnboardingHints.Of("choose"), OnboardingHints.Of("archive")));

            Assert.IsNotNull(captured);
            Assert.AreEqual(OnboardingHints.REVEAL_HINT, captured.reveal.Value);
            Assert.AreEqual(OnboardingHints.ASSEMBLE_HINT, captured.assemble.Value);
            Assert.AreEqual(OnboardingHints.CHOOSE_HINT, captured.choose.Value);
            Assert.AreEqual(OnboardingHints.ARCHIVE_HINT, captured.archive.Value);
        }

        // ── 存档失败用户回调 ──
        [Test]
        public void SaveFailed_Callback_Invoked_WithMessage()
        {
            var bus = new EventBus { LogError = m => { } };
            string received = null;
            bus.Subscribe(GameEvents.EVT_SAVE_FAILED, p => received = p as string);

            bus.Publish(GameEvents.EVT_SAVE_FAILED, "disk full");
            Assert.AreEqual("disk full", received);
        }

        // ── RuntimeQuality 移动端降级默认值 ──
        [Test]
        public void RuntimeQuality_Mobile_LowMem_Downgrades_Glow()
        {
            var q = RuntimeQuality.ForDevice(isMobile: true, systemMemoryMB: 1024);
            Assert.IsFalse(q.enableGlowShader, "低端移动机应关微光 Shader");
            Assert.IsTrue(q.maxDustCells < 64, "低端机应降网格分辨率");
            Assert.IsFalse(q.enableChoiceShader, "1G 内存机应关纸签 Shader");
        }

        [Test]
        public void RuntimeQuality_Mobile_HighMem_FullQuality()
        {
            var q = RuntimeQuality.ForDevice(isMobile: true, systemMemoryMB: 4096);
            Assert.IsTrue(q.enableGlowShader);
            Assert.AreEqual(64, q.maxDustCells);
            Assert.IsTrue(q.enableChoiceShader);
        }

        [Test]
        public void RuntimeQuality_Desktop_Default_Full()
        {
            var q = RuntimeQuality.ForDevice(isMobile: false, systemMemoryMB: 8192);
            Assert.IsTrue(q.enableGlowShader);
            Assert.AreEqual(64, q.maxDustCells);
        }

        // ── SessionRunner 降级配置接入（默认全开，不破坏既有行为）──
        [Test]
        public void SessionRunner_Accepts_Quality_And_Exposes_DustCap()
        {
            var bus = new EventBus { LogError = m => { } };
            var fsm = new CommissionStateMachine(bus);
            var save = new SaveEngine(new FakeStorage(), "/tmp", bus);
            var snap = new SaveSnapshot { version = SaveEngine.SAVE_VERSION };
            var runner = new SessionRunner(bus, fsm, save, () => snap, new RuntimeQuality());

            Assert.AreEqual(64, runner.EffectiveDustCellCap);

            // 降级配置应限制上限
            var lowQ = new RuntimeQuality { maxDustCells = 36 };
            var runner2 = new SessionRunner(bus, fsm, save, () => snap, lowQ);
            Assert.AreEqual(36, runner2.EffectiveDustCellCap);
        }

        // ── SessionRunner 无 quality 参数时默认全开（向后兼容）──
        [Test]
        public void SessionRunner_NoQualityArg_Defaults_Full()
        {
            var bus = new EventBus { LogError = m => { } };
            var fsm = new CommissionStateMachine(bus);
            var save = new SaveEngine(new FakeStorage(), "/tmp", bus);
            var snap = new SaveSnapshot { version = SaveEngine.SAVE_VERSION };
            var runner = new SessionRunner(bus, fsm, save, () => snap); // 旧构造签名仍可用（向后兼容）
            Assert.AreEqual(64, runner.EffectiveDustCellCap);
        }
    }
}
