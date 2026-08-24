// SmokeTests.cs — Sprint 1 烟雾测试门（EditMode，Unity Test Framework / NUnit）。
// 对应 sprint-01.md 质量门：
//   ① 空会话骨架 RECEIVED→ARCHIVED 连续 10 次无异常
//   ② fragment_count==0 跳过 ASSEMBLING
//   ③ 损坏快照回退、版本不兼容拒读、非法迁移拒绝
// 注：核心逻辑为纯 C#，用 InMemoryStorage 直接测；不需 PlayMode。
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    // 内存 IO 实现（测存档语义，不碰真磁盘）。
    // 实现已上收到 TestKit.FakeStorage（增加了 pattern 匹配、故障注入与调用录音），
    // 此处保留类名以免既有烟雾用例改写；新用例请直接用 FakeStorage。
    class InMemoryStorage : FakeStorage { }

    [TestFixture]
    public class SmokeTests
    {
        EventBus bus; SaveEngine save; CommissionStateMachine fsm;

        SaveSnapshot Snap(Commission c = null) => new SaveSnapshot
        {
            version = SaveEngine.SAVE_VERSION,
            active_commission = c,
            codex = new List<CodexEntry>()
        };

        [SetUp]
        public void Setup()
        {
            bus = new EventBus();
            var io = new InMemoryStorage();
            save = new SaveEngine(io, "/mem", bus)
            {
                Serialize = s => FakeJson(s),
                Deserialize = j => FromFakeJson(j)
            };
            fsm = new CommissionStateMachine(bus);
        }

        // ── ① 状态机全链路 ×10 ────────────────────────────────────
        [Test]
        public void FullPhaseChain_10Runs_NoException()
        {
            for (int run = 0; run < 10; run++)
            {
                var c = NewCommission("com_" + run, fragmentCount: 3);
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Received));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Examining));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Revealing));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Assembling));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Choosing));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Delivering));
                Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Archived));
                save.Save(Snap(c), force: true);
            }
        }

        [Test]
        public void FragmentCountZero_SkipsAssembling()
        {
            var c = NewCommission("com_skip", fragmentCount: 0);
            fsm.AdvancePhase(c, CommissionPhase.Received);
            fsm.AdvancePhase(c, CommissionPhase.Examining);
            fsm.AdvancePhase(c, CommissionPhase.Revealing);
            Assert.IsTrue(fsm.AdvancePhase(c, CommissionPhase.Choosing)); // 跳过合法
            Assert.AreEqual(CommissionPhase.Choosing, c.phase);
        }

        [Test]
        public void FragmentCountPositive_CannotSkipAssembling()
        {
            var c = NewCommission("com_noskip", fragmentCount: 2);
            fsm.AdvancePhase(c, CommissionPhase.Received);
            fsm.AdvancePhase(c, CommissionPhase.Examining);
            fsm.AdvancePhase(c, CommissionPhase.Revealing);
            Assert.IsFalse(fsm.AdvancePhase(c, CommissionPhase.Choosing)); // 拒绝
            Assert.AreEqual(CommissionPhase.Revealing, c.phase);
        }

        // ── ② 存档：原子写/节流/强制写 ────────────────────────────
        [Test]
        public void Throttle_SkipsWithin3s_ForceAlwaysWrites()
        {
            save.Save(Snap(), force: true);
            var count = ((InMemoryStorage)FilesOf(save)).Files.Count;
            save.Save(Snap()); // 节流内 → 合并，不新增文件
            Assert.AreEqual(count, FilesOf(save).Files.Count);
            save.Save(Snap(), force: true); // 强制 → 新文件
            Assert.AreEqual(count + 1, FilesOf(save).Files.Count);
        }

        [Test]
        public void CorruptSnapshot_FallsBackToEarlier()
        {
            save.Save(Snap(), force: true);
            var snap2 = Snap(NewCommission("com_x", 1)); snap2.last_node = SaveNode.NodeChoose;
            save.Save(snap2, force: true);
            var io = FilesOf(save);
            var latest = io.Files.Keys.OrderBy(k => k).Last();
            io.Files[latest] = "deadbeef\n{broken"; // 人为损坏最新档
            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded);
            Assert.IsNull(loaded.active_commission); // 回退到第 1 档
        }

        [Test]
        public void FutureVersion_Rejected()
        {
            var s = Snap(); s.version = 99;
            var io = FilesOf(save);
            // 直接手写一个未来版本档（绕过 Save 的版本覆盖）。
            // 修正：校验和必须正确，否则该档先被判"损坏"而走回退分支，
            //       LoadLatest 返回 null 但 LastError 仍是 null → 断言"更新"必失败（测的其实不是版本拒读）。
            var json = FakeJson(s);
            io.Files["/mem/save_0999.json"] = Fnv1a.Sum(json) + "\n" + json;
            Assert.IsNull(save.LoadLatest()); // 拒读不崩溃
            StringAssert.Contains("更新", save.LastError);
        }

        // ── ③ 契约守卫 ───────────────────────────────────────────
        [Test]
        public void ContractGuard_RejectsOutOfRange()
        {
            var c = NewCommission("com_bad", 7); // fragment_count>6
            c.choice_count = 1;                  // <2
            var errs = ContractGuard.Validate(c);
            Assert.GreaterOrEqual(errs.Count, 2);
        }

        // ── helpers ─────────────────────────────────────────────
        InMemoryStorage FilesOf(SaveEngine e)
            => (InMemoryStorage)typeof(SaveEngine).GetField("_io", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(e);

        Commission NewCommission(string id, int fragmentCount) => new Commission
        {
            commission_id = id, client_id = "cl_t", item_id = "it_t", chapter_index = 1,
            session_soft_budget_min = 7, reveal_threshold = 0.85f, fragment_count = fragmentCount,
            choice_count = 2, ending_variants = 2, is_mainplot = false
        };

        // 极简可逆序列化（测试用；Unity 层用 JsonUtility）
        string FakeJson(SaveSnapshot s) => $"v={s.version};node={s.last_node};active={(s.active_commission != null ? s.active_commission.commission_id + "@" + s.active_commission.phase : "null")};codex={s.codex.Count}";
        SaveSnapshot FromFakeJson(string j)
        {
            var parts = j.Split(';');
            var s = new SaveSnapshot { version = int.Parse(parts[0].Split('=')[1]) };
            Enum.TryParse(parts[1].Split('=')[1], out SaveNode n); s.last_node = n;
            var a = parts[2].Split(new[] { '=' }, 2)[1];
            if (a != "null") { var kv = a.Split('@'); Enum.TryParse(kv[1], out CommissionPhase p); s.active_commission = new Commission { commission_id = kv[0], phase = p }; }
            s.codex = new List<CodexEntry>();
            return s;
        }
    }
}
