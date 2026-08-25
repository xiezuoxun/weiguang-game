// RuntimeQualityFallbackTests.cs — Phase 6-C 低端机降级验证（EditMode，纯 C# Core/Runtime，FakeStorage 内存替身）。
// 作者：程基岩（engineering-lead）｜Phase 6-C
// 覆盖：RuntimeQuality.ForDevice 三档位数学（降级核心）+ SessionRunner.EffectiveDustCellCap 接入 quality。
// 不碰真机/Shader；真机帧率验收见 game/production/phase6-device-fallback.md。
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Runtime;

namespace Weiguang.Tests
{
    [TestFixture]
    public class RuntimeQualityFallbackTests
    {
        // ── 移动端低内存：glow off / dust 降级 36 / choice off ──
        // 注：choice shader 阈值为 systemMemoryMB >= 1536。故取 1024（<1536）作"真低内存"样本，
        //     使三项断言（glow off、dust 36、choice off）同时成立；1536 本身 choice 仍为 ON（见下方边界文档测试）。
        [Test]
        public void ForDevice_MobileLowMem_DisablesGlow()
        {
            var q = RuntimeQuality.ForDevice(true, 1024);
            Assert.IsFalse(q.enableGlowShader, "低内存移动端应关闭微光 Shader");
            Assert.AreEqual(36, q.maxDustCells, "低内存移动端 dust 应降到 36");
            Assert.IsFalse(q.enableChoiceShader, "低内存移动端应关闭抉择 Shader");
        }

        // ── 移动端高内存：glow on / dust 64 ──
        [Test]
        public void ForDevice_MobileHighMem_KeepsGlow()
        {
            var q = RuntimeQuality.ForDevice(true, 4096);
            Assert.IsTrue(q.enableGlowShader, "高内存移动端应保留微光 Shader");
            Assert.AreEqual(64, q.maxDustCells, "高内存移动端 dust 应保留 64");
        }

        // ── 桌面端：全部默认（true / 64）──
        [Test]
        public void ForDevice_Desktop_Defaults()
        {
            var q = RuntimeQuality.ForDevice(false, 8192);
            Assert.IsTrue(q.enableGlowShader, "桌面端 glow 默认 true");
            Assert.AreEqual(64, q.maxDustCells, "桌面端 dust 默认 64");
            Assert.IsTrue(q.enableChoiceShader, "桌面端 choice 默认 true");
            Assert.IsTrue(q.enableCodexAnim, "桌面端 codex 动画默认 true");
        }

        // ── SessionRunner.EffectiveDustCellCap 接入 quality（降级生效）──
        [Test]
        public void EffectiveDustCellCap_RespectsQuality()
        {
            var bus = new EventBus();
            var fsm = new CommissionStateMachine(bus);
            var save = new SaveEngine(new FakeStorage(), Build.DIR, bus)
            {
                Serialize = FakeJson.Write,
                Deserialize = FakeJson.Read
            };

            // 降级档位：maxDustCells=36
            var quality = new RuntimeQuality { maxDustCells = 36 };
            var runner = new SessionRunner(bus, fsm, save, () => new SaveSnapshot(), quality);
            Assert.LessOrEqual(runner.EffectiveDustCellCap, 36, "降级档位下拂尘上限应 ≤36");
            Assert.AreEqual(36, runner.EffectiveDustCellCap, "maxDustCells=36 时应封顶为 36");

            // 全开档位：默认 quality 仍受 64 硬上限约束
            var runnerFull = new SessionRunner(bus, fsm, save, () => new SaveSnapshot());
            Assert.AreEqual(64, runnerFull.EffectiveDustCellCap, "全开档位应封顶 64");
        }

        // ── 默认 enableCodexAnim 为 true ──
        [Test]
        public void EnableCodexAnim_DefaultTrue()
        {
            Assert.IsTrue(new RuntimeQuality().enableCodexAnim, "enableCodexAnim 默认应为 true");
        }

        // ── 边界文档：choice shader 阈值 = mem >= 1536（含 1536 即 ON）──
        [Test]
        public void ForDevice_ChoiceShaderThreshold_Boundary()
        {
            Assert.IsTrue(RuntimeQuality.ForDevice(true, 1536).enableChoiceShader,
                "1536 为 choice 阈值下限（含），应 ON");
            Assert.IsFalse(RuntimeQuality.ForDevice(true, 1024).enableChoiceShader,
                "1024 < 1536 应 OFF");
        }
    }
}
