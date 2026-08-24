// EventBusTests.cs — ADR-005 事件总线单测（玩法系统零互调的地基，坏了会静默吞掉整条链路）。
// 重点保证三件事：① 一个 handler 抛异常不得连坐后续 handler；② 无订阅者 Publish 不崩；
//                ③ 回调中改订阅表（Clear）不得抛"集合被修改"。
using System.Collections.Generic;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class EventBusTests
    {
        EventBus bus;
        List<string> errors;

        [SetUp]
        public void Setup()
        {
            bus = new EventBus();
            errors = new List<string>();
            bus.LogError = m => errors.Add(m);
        }

        [Test]
        public void Publish_WithNoSubscribers_IsNoOp()
        {
            Assert.DoesNotThrow(() => bus.Publish(GameEvents.EVT_COMMISSION_START, "payload"));
            Assert.DoesNotThrow(() => bus.Publish("EVT_从未注册的事件"));
            Assert.IsEmpty(errors);
        }

        [Test]
        public void Publish_InvokesAllSubscribers_InRegistrationOrder()
        {
            var order = new List<int>();
            bus.Subscribe(GameEvents.EVT_REVEAL_COMPLETE, _ => order.Add(1));
            bus.Subscribe(GameEvents.EVT_REVEAL_COMPLETE, _ => order.Add(2));
            bus.Subscribe(GameEvents.EVT_REVEAL_COMPLETE, _ => order.Add(3));

            bus.Publish(GameEvents.EVT_REVEAL_COMPLETE, 0.9f);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, order);
        }

        [Test]
        public void Publish_PassesPayloadThrough_IncludingNull()
        {
            object got = "未收到";
            int calls = 0;
            bus.Subscribe(GameEvents.EVT_CHOICE_MADE, p => { got = p; calls++; });

            bus.Publish(GameEvents.EVT_CHOICE_MADE, EndingTag.Reframe);
            Assert.AreEqual(EndingTag.Reframe, got);

            bus.Publish(GameEvents.EVT_CHOICE_MADE);        // 默认 null 载荷
            Assert.IsNull(got);
            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Publish_IsolatesHandlerException_LaterHandlersStillRun()
        {
            bool tailRan = false;
            bus.Subscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, _ => { throw new System.InvalidOperationException("坏订阅者"); });
            bus.Subscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, _ => tailRan = true);

            Assert.DoesNotThrow(() => bus.Publish(GameEvents.EVT_ASSEMBLE_COMPLETE));
            Assert.IsTrue(tailRan, "前一个 handler 抛异常不得阻断后续订阅者（否则一处 bug 冻结整条循环）");
            Assert.AreEqual(1, errors.Count, "异常须上报宿主日志，不得静默");
            StringAssert.Contains("坏订阅者", errors[0]);
            StringAssert.Contains(GameEvents.EVT_ASSEMBLE_COMPLETE, errors[0], "日志须带事件名便于定位");
        }

        [Test]
        public void Publish_AllowsSubscriptionTableMutationDuringCallback()
        {
            int calls = 0;
            bus.Subscribe(GameEvents.EVT_PHASE_CHANGED, _ => { calls++; bus.Clear(); });
            bus.Subscribe(GameEvents.EVT_PHASE_CHANGED, _ => calls++);

            Assert.DoesNotThrow(() => bus.Publish(GameEvents.EVT_PHASE_CHANGED), "回调中改订阅表不得抛集合修改异常（内部已做快照）");
            Assert.AreEqual(2, calls, "本次派发按快照执行，两个 handler 都应跑到");

            bus.Publish(GameEvents.EVT_PHASE_CHANGED);
            Assert.AreEqual(2, calls, "Clear 后不应再派发");
        }

        [Test]
        public void EventsAreIsolatedByName()
        {
            int a = 0, b = 0;
            bus.Subscribe(GameEvents.EVT_SAVE_WRITTEN, _ => a++);
            bus.Subscribe(GameEvents.EVT_SAVE_FAILED, _ => b++);

            bus.Publish(GameEvents.EVT_SAVE_WRITTEN);
            Assert.AreEqual(1, a);
            Assert.AreEqual(0, b, "事件名必须严格隔离");
        }

        [Test]
        public void Clear_RemovesAllSubscriptions()
        {
            int calls = 0;
            bus.Subscribe(GameEvents.EVT_COMMISSION_DONE, _ => calls++);
            bus.Clear();
            bus.Publish(GameEvents.EVT_COMMISSION_DONE);
            Assert.AreEqual(0, calls);
        }

        // ── C2 命名唯一：事件名常量表不得漂移/重名 ─────────────────────
        [Test]
        public void EventNameConstants_AreUniqueAndSelfNamed()
        {
            var fields = typeof(GameEvents).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.GreaterOrEqual(fields.Length, 9, "GameEvents 常量数不应减少（事件是跨系统契约）");

            var seen = new HashSet<string>();
            foreach (var f in fields)
            {
                var value = (string)f.GetValue(null);
                Assert.AreEqual(f.Name, value, "常量名与字符串值必须一致，避免 grep 不到（C2）");
                StringAssert.StartsWith("EVT_", value, "事件名必须 EVT_ 前缀");
                Assert.IsTrue(seen.Add(value), "事件名重复：" + value);
            }
        }
    }
}
