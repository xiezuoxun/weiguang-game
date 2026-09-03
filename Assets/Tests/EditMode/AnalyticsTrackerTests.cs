// AnalyticsTrackerTests.cs — Phase 8-A 埋点核心单测（EditMode，纯 C# 无 Unity 依赖）。
// 验证：① 四动词首见按"委托"口径正确累计；② 碎片吸附率 locked/total；
//       ③ 抉择分布 by ending_tag；④ 退订后不再响应；⑤ 归档吐聚合快照（漏斗 + 首见率 + 吸附率 + 分布）。
// 核心 AnalyticsTracker / LogAnalyticsSink 在 Weiguang.Core（noEngineReferences），本测试随 EditMode asmdef 编译，
// 不引用任何 UnityEngine，故可在无 Unity 宿主的 CI EditMode 下通过。
using System.Collections.Generic;
using NUnit.Framework;
using Weiguang.Core;
using Weiguang.Core.Analytics;

namespace Weiguang.Tests
{
    [TestFixture]
    public class AnalyticsTrackerTests
    {
        // 测试用 fake sink：记录收到的所有 Track 调用，供断言"事件流 + 聚合快照"。
        class FakeSink : IAnalyticsSink
        {
            public readonly List<KeyValuePair<string, IDictionary<string, object>>> Calls
                = new List<KeyValuePair<string, IDictionary<string, object>>>();

            public void Track(string eventName, IDictionary<string, object> props)
                => Calls.Add(new KeyValuePair<string, IDictionary<string, object>>(eventName, props));

            public int Count(string name) => Calls.FindAll(x => x.Key == name).Count;
        }

        EventBus bus;
        FakeSink sink;
        AnalyticsTracker tracker;

        [SetUp]
        public void Setup()
        {
            bus = new EventBus();
            sink = new FakeSink();
            tracker = new AnalyticsTracker(bus, sink);
            tracker.Subscribe();
        }

        [Test]
        public void FourVerbFirstSeen_AcrossTwoCommissions()
        {
            // 委托 1：走完四动词
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c1" });
            bus.Publish(GameEvents.EVT_REVEAL_WHISPER, new RevealWhisperEvent(RevealWhisperEvent.WHISPER_25, 0.25f));
            bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(3, 5));
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Truth);
            bus.Publish(GameEvents.EVT_ARCHIVED, new ArchivedEvent("cx_c1", 0, true));

            Assert.AreEqual(1, tracker.CommissionStarts);
            Assert.AreEqual(1, tracker.FirstSeenReveal);
            Assert.AreEqual(1, tracker.FirstSeenAssemble);
            Assert.AreEqual(1, tracker.FirstSeenChoose);
            Assert.AreEqual(1, tracker.FirstSeenArchive);

            // 委托 2：只抉择、不揭示（验证首见按"委托"口径，不跨委托重复累计）
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c2" });
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Reframe);
            bus.Publish(GameEvents.EVT_ARCHIVED, new ArchivedEvent("cx_c2", 1, false));

            Assert.AreEqual(2, tracker.CommissionStarts);
            Assert.AreEqual(1, tracker.FirstSeenReveal, "reveal 仅 c1 触发，c2 未揭示 → 首见仍为 1");
            Assert.AreEqual(1, tracker.FirstSeenAssemble, "assemble 仅 c1 触发");
            Assert.AreEqual(2, tracker.FirstSeenChoose, "choose 两委托都触发");
            Assert.AreEqual(2, tracker.FirstSeenArchive);

            // 首见率口径
            Assert.AreEqual(0.5d, tracker.RevealFirstSeenRate, 1e-9);
            Assert.AreEqual(1.0d, tracker.ChooseFirstSeenRate, 1e-9);
        }

        [Test]
        public void FragmentAdsorbRate_LockedOverTotal()
        {
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c1" });
            bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(3, 5));
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c2" });
            bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(4, 4));

            Assert.AreEqual(7L, tracker.FragmentLocked);   // 3 + 4
            Assert.AreEqual(9L, tracker.FragmentTotal);     // 5 + 4
            Assert.AreEqual(7.0 / 9.0, tracker.AdsorbRate, 1e-9);
        }

        [Test]
        public void ChoiceDistribution_ByEndingTag()
        {
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c1" });
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Truth);
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Truth);
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Omit);
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Reframe);

            Assert.AreEqual(2, tracker.ChoiceByEndingTag["Truth"]);
            Assert.AreEqual(1, tracker.ChoiceByEndingTag["Omit"]);
            Assert.AreEqual(1, tracker.ChoiceByEndingTag["Reframe"]);
            Assert.AreEqual(3, tracker.ChoiceByEndingTag.Count);
        }

        [Test]
        public void Unsubscribe_StopsResponding()
        {
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c1" });
            Assert.AreEqual(1, tracker.CommissionStarts);
            int sinkCallsBefore = sink.Calls.Count;

            tracker.Unsubscribe();
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c2" });
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Truth);

            Assert.AreEqual(1, tracker.CommissionStarts, "退订后不应再累加");
            Assert.AreEqual(sinkCallsBefore, sink.Calls.Count, "退订后不应再 Track");
        }

        [Test]
        public void Archive_EmitsMetricsSnapshot_WithFunnelAndRates()
        {
            bus.Publish(GameEvents.EVT_COMMISSION_START, new Commission { commission_id = "c1" });
            bus.Publish(GameEvents.EVT_REVEAL_WHISPER, new RevealWhisperEvent(RevealWhisperEvent.WHISPER_25, 0.25f));
            bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE, new AssembleCompleteEvent(2, 2));
            bus.Publish(GameEvents.EVT_CHOICE_MADE, (object)EndingTag.Truth);
            bus.Publish(GameEvents.EVT_ARCHIVED, new ArchivedEvent("cx_c1", 0, true));

            // 聚合快照 analytics_metrics 应至少发一次（归档终点吐一次）
            Assert.GreaterOrEqual(sink.Count(AnalyticsTracker.E_NAME_METRICS), 1);
            var snap = sink.Calls.Find(x => x.Key == AnalyticsTracker.E_NAME_METRICS).Value;

            Assert.AreEqual(1, (int)snap["commission_starts"]);
            Assert.AreEqual(1, (int)snap["reveal_first_seen"]);
            Assert.AreEqual(1.0d, (double)snap["reveal_first_seen_rate"], 1e-9);
            Assert.AreEqual(2L, (long)snap["fragment_locked"]);
            Assert.AreEqual(2L, (long)snap["fragment_total"]);
            Assert.IsTrue(snap.ContainsKey("choice_distribution"));
            Assert.IsTrue(snap.ContainsKey("funnel"));
        }
    }
}
