// FeedbackHooksTests.cs — 打磨阶段体验层反馈钩子单测（EditMode，纯 C# Core）。
// 覆盖：① 拂尘阈值跨越事件（EVT_REVEAL_THRESHOLD_CROSSED + payload）；
//       ② 拼合吸附/回弹钩子（OnFragmentSnapped/OnFragmentRejected，不改核心判定）；
//       ③ 抉择高亮/选中事件载荷（ChoiceOptionEvent）、图鉴解锁事件载荷（CodexUnlockedEvent）；
//       ④ 首启引导常量（OnboardingHints）四动词文案齐备且非空；
//       ⑤ 现有 EVT_* 常量唯一性/自命名不被新增破坏。
// 不引用 Weiguang.Runtime（asmdef 未引用），故 SessionRunner 的发布调用由 Core 层事件契约 + 本文件间接保证。
using System.Collections.Generic;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class FeedbackHooksTests
    {
        // ── ① 拂尘阈值跨越事件 ──────────────────────────────────────
        [Test]
        public void ThresholdCrossed_FiresOncePerLevel_WithLevelAndProgress()
        {
            var t = new RevealThresholdTracker();
            var crosses = new List<RevealThresholdCrossedEvent>();

            // 三参重载：onCrossed 钩子捕获每次跨越
            t.Update(0.3f, _ => { }, e => crosses.Add(e));
            t.Update(0.6f, _ => { }, e => crosses.Add(e));
            t.Update(0.9f, _ => { }, e => crosses.Add(e));

            Assert.AreEqual(3, crosses.Count, "25/50/75 各跨一次");
            Assert.AreEqual(RevealThresholdTracker.T25, crosses[0].level, 1e-6f);
            Assert.AreEqual(RevealThresholdTracker.T50, crosses[1].level, 1e-6f);
            Assert.AreEqual(RevealThresholdTracker.T75, crosses[2].level, 1e-6f);
            // progress 为钳制后的当前值（此处即 0.3/0.6/0.9）
            Assert.AreEqual(0.3f, crosses[0].progress, 1e-6f);

            // once-lock：重复调用不再触发
            int before = crosses.Count;
            t.Update(1f, _ => { }, e => crosses.Add(e));
            Assert.AreEqual(before, crosses.Count, "已锁定后不得再发跨越事件");
        }

        [Test]
        public void ThresholdCrossed_EventConstant_IsSelfNamed_AndPublishedViaBus()
        {
            Assert.AreEqual("EVT_REVEAL_THRESHOLD_CROSSED", GameEvents.EVT_REVEAL_THRESHOLD_CROSSED);

            var bus = new EventBus();
            var rec = new EventRecorder(bus, GameEvents.EVT_REVEAL_THRESHOLD_CROSSED);
            var tracker = new RevealThresholdTracker();
            // 复刻 SessionRunner 调用形态：onCrossed 经 EventBus 广播
            tracker.Update(1f, _ => { },
                e => bus.Publish(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED, e));

            Assert.AreEqual(3, rec.Count(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED), "跨满触发 3 次");
            var levels = rec.Payloads(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED)
                             .ConvertAll(e => ((RevealThresholdCrossedEvent)e).level);
            CollectionAssert.AreEquivalent(new[] { 0.25f, 0.50f, 0.75f }, levels);
        }

        [Test]
        public void ThresholdCrossed_NullHook_BehavesLikeTwoArgOverload()
        {
            // 传 null 钩子时，行为必须与原两参方法一致（现有用例 DustRevealTests 依赖该语义）
            var t = new RevealThresholdTracker();
            var fired = new List<float>();
            t.Update(1f, fired.Add, null);
            CollectionAssert.AreEqual(new[] { 0.25f, 0.50f, 0.75f }, fired);
        }

        // ── ② 拼合吸附 / 回弹钩子 ───────────────────────────────────
        [Test]
        public void Assembly_SnapAndRejectHooks_FireWithoutChangingCoreJudgement()
        {
            var board = new AssemblyBoard();
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "f0");
            board.slots.Add(slot);
            var f = new Fragment { fragment_id = "f0", home_slot_id = "slot0" };
            board.fragments.Add(f);

            var snapped = new List<(Fragment, FragmentSlot)>();
            var rejected = new List<(Fragment, FragmentSlot)>();
            board.OnFragmentSnapped = (fr, sl) => snapped.Add((fr, sl));
            board.OnFragmentRejected = (fr, sl) => rejected.Add((fr, sl));

            // 命中归属带（中带中心）→ 吸附，不回弹
            bool ok = board.TryPlaceFragment(f, 0.5f, 0.5f);
            Assert.IsTrue(ok, "核心判定：锚点命中应锁定");
            Assert.IsTrue(f.is_locked && slot.is_filled, "核心判定未被钩子改变");
            Assert.AreEqual(1, snapped.Count, "吸附钩子应触发一次");
            Assert.AreEqual(0, rejected.Count, "命中不应触发回弹");

            // 已锁定的槽位再落 → 既不应吸附也不应回弹（原幂等守卫语义保持）
            var f2 = new Fragment { fragment_id = "f0", home_slot_id = "slot0" };
            board.fragments.Add(f2);
            snapped.Clear(); rejected.Clear();
            bool dup = board.TryPlaceFragment(f2, 0.5f, 0.5f);
            Assert.IsFalse(dup, "槽位已填：幂等，不重复锁");
            Assert.AreEqual(0, snapped.Count, "已填槽位不触发吸附钩子");
            Assert.AreEqual(0, rejected.Count, "已填槽位不触发回弹钩子（保持原守卫）");

            // 未落中带（Y 超出中带）→ 回弹，不锁
            var f3 = new Fragment { fragment_id = "fx", home_slot_id = "slotX" };
            var slotX = new FragmentSlot("slotX", 0.5f, 0.5f, "fx");
            board.slots.Add(slotX); board.fragments.Add(f3);
            snapped.Clear(); rejected.Clear();
            bool miss = board.TryPlaceFragment(f3, 0.5f, 0.95f); // Y=0.95 超出中带
            Assert.IsFalse(miss, "未落中带：不应锁定");
            Assert.IsFalse(f3.is_locked, "未落中带：碎片保持未锁");
            Assert.AreEqual(1, rejected.Count, "回弹钩子应触发一次");
            Assert.AreEqual(0, snapped.Count, "未命中不应触发吸附");
        }

        [Test]
        public void Assembly_SnapThreshold_DefaultsToSlotXTolerance()
            => Assert.AreEqual(FragmentSlot.X_TOLERANCE, new AssemblyBoard().SnapThreshold, 1e-6f);

        [Test]
        public void Assembly_NoHookSubscribed_BehavesLikeBaseline()
        {
            // 不挂任何钩子：表现必须与原版完全一致（不崩、核心判定正常）
            var board = new AssemblyBoard();
            board.slots.Add(new FragmentSlot("slot0", 0.5f, 0.5f, "f0"));
            var f = new Fragment { fragment_id = "f0", home_slot_id = "slot0" };
            board.fragments.Add(f);
            Assert.IsTrue(board.TryPlaceFragment(f, 0.5f, 0.5f));
            Assert.IsTrue(board.AllLocked());
        }

        // ── ③ 抉择高亮 / 选中事件载荷 ───────────────────────────────
        [Test]
        public void ChoiceOptionEvent_CarriesOptionIdAndType()
        {
            Assert.AreEqual("EVT_OPTION_HIGHLIGHTED", GameEvents.EVT_OPTION_HIGHLIGHTED);
            Assert.AreEqual("EVT_OPTION_SELECTED", GameEvents.EVT_OPTION_SELECTED);

            var h = new ChoiceOptionEvent("op2", ChoiceOptionEvent.TYPE_HIGHLIGHTED);
            var s = new ChoiceOptionEvent("op2", ChoiceOptionEvent.TYPE_SELECTED);
            Assert.AreEqual("op2", h.option_id);
            Assert.AreEqual(ChoiceOptionEvent.TYPE_HIGHLIGHTED, h.type);
            Assert.AreEqual(ChoiceOptionEvent.TYPE_SELECTED, s.type);
        }

        [Test]
        public void ChoiceOptionEvent_PublishedViaBus()
        {
            var bus = new EventBus();
            var rec = new EventRecorder(bus, GameEvents.EVT_OPTION_HIGHLIGHTED, GameEvents.EVT_OPTION_SELECTED);
            bus.Publish(GameEvents.EVT_OPTION_HIGHLIGHTED, new ChoiceOptionEvent("op0", ChoiceOptionEvent.TYPE_HIGHLIGHTED));
            bus.Publish(GameEvents.EVT_OPTION_SELECTED, new ChoiceOptionEvent("op0", ChoiceOptionEvent.TYPE_SELECTED));

            Assert.AreEqual(1, rec.Count(GameEvents.EVT_OPTION_HIGHLIGHTED));
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_OPTION_SELECTED));
            Assert.AreEqual("op0", ((ChoiceOptionEvent)rec.Last(GameEvents.EVT_OPTION_HIGHLIGHTED)).option_id);
        }

        // ── ④ 图鉴解锁事件载荷 ─────────────────────────────────────
        [Test]
        public void CodexUnlockedEvent_CarriesEntryId()
        {
            Assert.AreEqual("EVT_CODEX_UNLOCKED", GameEvents.EVT_CODEX_UNLOCKED);
            var bus = new EventBus();
            var rec = new EventRecorder(bus, GameEvents.EVT_CODEX_UNLOCKED);
            bus.Publish(GameEvents.EVT_CODEX_UNLOCKED, new CodexUnlockedEvent("cx_com_t"));

            Assert.AreEqual(1, rec.Count(GameEvents.EVT_CODEX_UNLOCKED));
            Assert.AreEqual("cx_com_t", ((CodexUnlockedEvent)rec.Last(GameEvents.EVT_CODEX_UNLOCKED)).entry_id);
        }

        // ── ⑤ 首启引导常量 ─────────────────────────────────────────
        [Test]
        public void OnboardingHints_AllFourVerbs_PresentAndNonEmpty()
        {
            foreach (var verb in new[] { "reveal", "assemble", "choose", "archive" })
            {
                var kv = OnboardingHints.Of(verb);
                Assert.IsNotEmpty(kv.Key, $"动词 {verb} 缺 title");
                Assert.IsNotEmpty(kv.Value, $"动词 {verb} 缺 hint");
                Assert.LessOrEqual(kv.Value.Length, 20, $"动词 {verb} 文案过长（应≤20 字，安全进包）");
            }
            // 未知 key 返回空串对（不崩）
            var none = OnboardingHints.Of("unknown");
            Assert.AreEqual(string.Empty, none.Key);
            Assert.AreEqual(string.Empty, none.Value);
        }

        // ── ⑥ 现有 EVT_* 常量唯一性/自命名不被破坏（沿用 EventBusTests 契约，独立守护）──
        [Test]
        public void EventNameConstants_AreUniqueAndSelfNamed_AfterFeedbackHooks()
        {
            var fields = typeof(GameEvents).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.GreaterOrEqual(fields.Length, 13, "新增 4 个体验层事件后应 ≥13");
            var seen = new HashSet<string>();
            foreach (var f in fields)
            {
                var value = (string)f.GetValue(null);
                Assert.AreEqual(f.Name, value, "常量名与字符串值必须一致（C2）");
                StringAssert.StartsWith("EVT_", value);
                Assert.IsTrue(seen.Add(value), "事件名重复：" + value);
            }
        }
    }
}
