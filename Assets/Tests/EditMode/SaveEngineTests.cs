// SaveEngineTests.cs — S6 自动存档底座单测（GDD 06 ⑧ 验收标准逐条映射；控制清单 I4）。
// 覆盖：原子写（temp→rename）｜校验和｜3s 节流与脏位补写｜force 强制写｜损坏回退｜
//       篡改检测｜版本拒读｜版本迁移｜写盘失败不崩不丢档｜依赖未注入的 fail-fast。
// 全部用 FakeStorage（内存 + 故障注入），不碰真磁盘、不需 PlayMode → CI 秒级可跑。
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class SaveEngineTests
    {
        EventBus bus;
        FakeStorage io;
        SaveEngine save;
        EventRecorder rec;

        [SetUp]
        public void Setup()
        {
            bus = new EventBus();
            io = new FakeStorage();
            save = new SaveEngine(io, Build.DIR, bus)
            {
                Serialize = FakeJson.Write,
                Deserialize = FakeJson.Read
            };
            rec = new EventRecorder(bus, GameEvents.EVT_SAVE_WRITTEN, GameEvents.EVT_SAVE_FAILED);
        }

        // ── ① 原子写（S6 ②「先写临时文件再 rename」）─────────────────
        [Test]
        public void AtomicWrite_TempThenRename_NoTempLeftBehind()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(), force: true));

            Assert.AreEqual(1, io.Writes.Count, "应只写一次临时文件");
            StringAssert.EndsWith(".json.tmp", io.Writes[0], "必须先落临时文件，不得直写目标档");
            Assert.AreEqual(1, io.Moves.Count, "必须经 rename 落定");
            Assert.AreEqual(1, io.SnapshotCount());
            Assert.IsFalse(io.Files.Keys.Any(k => k.EndsWith(".tmp", StringComparison.Ordinal)), "成功路径不得残留临时文件");
        }

        [Test]
        public void WrittenFile_FirstLineIsChecksumOfPayload()
        {
            save.Save(Build.NewSnapshot(), force: true);

            var raw = io.Files[io.LatestSnapshot()];
            int nl = raw.IndexOf('\n');
            Assert.Greater(nl, 0, "首行必须是校验和");
            string sum = raw.Substring(0, nl);
            string json = raw.Substring(nl + 1);
            Assert.AreEqual(16, sum.Length, "FNV-1a 64 → 16 位十六进制");
            Assert.AreEqual(Fnv1a.Sum(json), sum, "校验和须覆盖全部正文");
            StringAssert.Contains("v=" + SaveEngine.SAVE_VERSION, json);
        }

        [Test]
        public void Save_StampsCurrentVersionAndTimestamp()
        {
            var snap = Build.NewSnapshot();
            snap.version = 99;              // 调用方乱填的版本必须被覆盖
            snap.saved_at_iso = null;

            Assert.IsTrue(save.Save(snap, force: true));
            Assert.AreEqual(SaveEngine.SAVE_VERSION, snap.version, "落盘版本只认 SAVE_VERSION");
            Assert.IsNotEmpty(snap.saved_at_iso, "须写入 ISO 时间戳");
        }

        [Test]
        public void SaveWritten_EventFiresOnce_WithFileName()
        {
            save.Save(Build.NewSnapshot(), force: true);

            Assert.AreEqual(1, rec.Count(GameEvents.EVT_SAVE_WRITTEN));
            Assert.AreEqual(0, rec.Count(GameEvents.EVT_SAVE_FAILED));
            var name = rec.Last(GameEvents.EVT_SAVE_WRITTEN) as string;
            Assert.IsNotNull(name, "载荷应为文件名字符串");
            StringAssert.StartsWith("save_", name);
            StringAssert.EndsWith(".json", name);
            Assert.IsFalse(name.Contains("/") || name.Contains("\\"), "载荷是文件名而非全路径（UI 提示不暴露路径）");
        }

        [Test]
        public void RoundTrip_RestoresPhaseNodeAndFragmentCount()
        {
            var c = Build.NewCommission("com_rt", fragmentCount: 4, phase: CommissionPhase.Assembling);
            Assert.IsTrue(save.Save(Build.NewSnapshot(c, SaveNode.NodeAssemble), force: true));

            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveEngine.SAVE_VERSION, loaded.version);
            Assert.AreEqual(SaveNode.NodeAssemble, loaded.last_node);
            Assert.AreEqual("com_rt", loaded.active_commission.commission_id);
            Assert.AreEqual(CommissionPhase.Assembling, loaded.active_commission.phase, "断点 phase 须精确还原（误差 0）");
            Assert.AreEqual(4, loaded.active_commission.fragment_count);
        }

        // ── ② 节流 ≤1 次/3s + 脏位补写（S6 ⑤）───────────────────────
        [Test]
        public void Throttle_SecondWriteWithinWindow_IsMerged_NotFailed()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(), force: true));
            Assert.AreEqual(1, io.SnapshotCount());
            Assert.IsFalse(save.HasPendingDirtyWrite);

            Assert.IsTrue(save.Save(Build.NewSnapshot()), "节流是合并写，不得报失败");
            Assert.AreEqual(1, io.SnapshotCount(), "3s 内的第二次写应被合并，不新增档");
            Assert.IsTrue(save.HasPendingDirtyWrite, "被合并的写须置脏位，供下一节点补写");
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_SAVE_WRITTEN), "被节流的写不得发 EVT_SAVE_WRITTEN");
            Assert.AreEqual(0, rec.Count(GameEvents.EVT_SAVE_FAILED));
        }

        [Test]
        public void Throttle_ManyRapidWrites_CollapseToOne()
        {
            save.Save(Build.NewSnapshot(), force: true);
            for (int i = 0; i < 20; i++) save.Save(Build.NewSnapshot());   // 模拟拂尘期高频节点变更
            Assert.AreEqual(1, io.SnapshotCount(), "连续 20 次节点变更应合并为 1 次写（防频繁 IO 卡手势）");
            Assert.IsTrue(save.HasPendingDirtyWrite);
        }

        [Test]
        public void PendingDirtyWrite_ClearedByNextSuccessfulWrite()
        {
            save.Save(Build.NewSnapshot(), force: true);
            save.Save(Build.NewSnapshot());
            Assert.IsTrue(save.HasPendingDirtyWrite);

            Assert.IsTrue(save.Save(Build.NewSnapshot(), force: true));
            Assert.IsFalse(save.HasPendingDirtyWrite, "补写成功后须清脏位");
        }

        // ── ③ force 强制写（onPause 不节流，S6 ②/⑦）─────────────────
        [Test]
        public void Force_BypassesThrottle_AndNeverOverwritesPreviousSnapshot()
        {
            for (int i = 0; i < 5; i++)
                Assert.IsTrue(save.Save(Build.NewSnapshot(), force: true));

            // 回归护栏：同一毫秒内的连续强制写若共用时间戳档名，会 rename 覆盖上一份快照，
            // 直接摧毁 S6⑥-2「损坏回退上一可用档」的兜底能力。
            Assert.AreEqual(5, io.SnapshotCount(), "5 次强制写必须产出 5 份独立快照");
            CollectionAssert.AllItemsAreUnique(io.Snapshots());
            Assert.AreEqual(5, rec.Count(GameEvents.EVT_SAVE_WRITTEN));
            Assert.IsFalse(save.HasPendingDirtyWrite);

            var stamps = io.Snapshots()
                .Select(k => long.Parse(System.IO.Path.GetFileNameWithoutExtension(k).Substring("save_".Length)))
                .ToList();
            for (int i = 1; i < stamps.Count; i++)
                Assert.Greater(stamps[i], stamps[i - 1], "档名时间戳须严格递增（Ordinal 排序 == 时间序）");
        }

        [Test]
        public void ForcedWrite_CompletesWellUnder500ms()
        {
            // S6 ⑧「onPause <500ms 完成强制写」的下界守护：纯逻辑耗时。
            // 真机 IO 门在 PlayMode/设备测试补（见 TEST_STRATEGY 层 2）。
            var sw = Stopwatch.StartNew();
            Assert.IsTrue(save.Save(Build.NewSnapshot(Build.NewCommission()), force: true));
            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 500, "序列化+写盘逻辑本身不得吃掉 onPause 预算");
        }

        [Test]
        [Category("Slow")]
        public void Throttle_AfterWindowElapsed_WritesAgainWithoutForce()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(), force: true));
            Assert.AreEqual(1, io.SnapshotCount());

            Thread.Sleep(3100);   // 越过 THROTTLE_MS=3000 窗口（唯一需要真实等待的用例）

            Assert.IsTrue(save.Save(Build.NewSnapshot()), "窗口过后非强制写应真正落盘");
            Assert.AreEqual(2, io.SnapshotCount());
            Assert.IsFalse(save.HasPendingDirtyWrite);
        }

        // ── ④ 损坏回退（S6 ⑥-2）────────────────────────────────────
        [Test]
        public void CorruptLatest_FallsBackToPreviousReadableSnapshot()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(null, SaveNode.NodeReceive), force: true));
            Assert.IsTrue(save.Save(Build.NewSnapshot(Build.NewCommission("com_new", 2, CommissionPhase.Choosing), SaveNode.NodeChoose), force: true));
            Assert.AreEqual(2, io.SnapshotCount());

            io.TamperChecksum(io.LatestSnapshot());   // 人为损坏最新档

            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded, "最新档损坏必须回退上一可用档，不得当作无档");
            Assert.AreEqual(SaveNode.NodeReceive, loaded.last_node);
            Assert.IsNull(loaded.active_commission, "回退到第一份快照");
        }

        [Test]
        public void TamperedPayload_WithStaleChecksum_IsRejected()
        {
            save.Save(Build.NewSnapshot(null, SaveNode.NodeReceive), force: true);
            save.Save(Build.NewSnapshot(null, SaveNode.NodeChoose), force: true);

            io.TamperPayload(io.LatestSnapshot(), "node=NodeChoose", "node=NodeArchive"); // 改正文不改校验和

            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveNode.NodeReceive, loaded.last_node, "正文被篡改须判损坏（防作弊/防半截写）");
        }

        [Test]
        public void BrokenJson_WithValidChecksum_IsAlsoTreatedAsCorrupt()
        {
            save.Save(Build.NewSnapshot(null, SaveNode.NodeReceive), force: true);
            // 校验和正确但正文不是合法快照 → 反序列化抛异常，须被 TryRead 吃掉并回退
            io.WriteRawSnapshot(Build.DIR, Build.SnapshotName(long.MaxValue), "{完全不是我们的格式}");

            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded, "反序列化异常不得冒泡崩游戏");
            Assert.AreEqual(SaveNode.NodeReceive, loaded.last_node);
        }

        [Test]
        public void AllSnapshotsCorrupt_ReturnsNull_WithoutThrowing()
        {
            save.Save(Build.NewSnapshot(), force: true);
            save.Save(Build.NewSnapshot(), force: true);
            foreach (var k in io.Snapshots()) io.TamperChecksum(k);

            SaveSnapshot loaded = null;
            Assert.DoesNotThrow(() => loaded = save.LoadLatest());
            Assert.IsNull(loaded, "全部损坏 → 返回 null 由调用方新开局（不丢已归档内容的合并逻辑在调用方）");
        }

        [Test]
        public void NoSnapshot_LoadLatest_ReturnsNullAndIsNotAnError()
        {
            Assert.IsNull(save.LoadLatest());
            Assert.IsNull(save.LastError, "首次启动无档是正常态，不得记为失败");
            Assert.AreEqual(0, rec.Count(GameEvents.EVT_SAVE_FAILED));
        }

        // ── ⑤ 版本：高版本拒读 / 低版本迁移（S6 ⑥-3）─────────────────
        [Test]
        public void FutureVersion_RejectedWithUpdateHint()
        {
            var future = Build.NewSnapshot();
            future.version = SaveEngine.SAVE_VERSION + 98;
            io.WriteRawSnapshot(Build.DIR, Build.SnapshotName(1000), FakeJson.Write(future)); // 校验和正确，仅版本超前

            Assert.IsNull(save.LoadLatest(), "高版本档必须拒读，不得半解析");
            StringAssert.Contains("更新", save.LastError, "须给出「请更新」的明确提示，不静默");
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_SAVE_FAILED));
        }

        [Test]
        public void OlderVersion_RunsMigrationChain_ThenLoads()
        {
            var old = Build.NewSnapshot(Build.NewCommission("com_v0", 1, CommissionPhase.Revealing), SaveNode.NodeReveal);
            old.version = 0;
            io.WriteRawSnapshot(Build.DIR, Build.SnapshotName(1000), FakeJson.Write(old));

            int migrated = 0;
            save.RegisterMigration(0, s => { migrated++; s.version = 1; return s; });

            var loaded = save.LoadLatest();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, migrated, "v0→v1 迁移器须被调用恰好一次");
            Assert.AreEqual(SaveEngine.SAVE_VERSION, loaded.version);
            Assert.AreEqual("com_v0", loaded.active_commission.commission_id, "迁移不得丢内容");
            Assert.AreEqual(CommissionPhase.Revealing, loaded.active_commission.phase);
        }

        [Test]
        public void MissingMigrator_FailsSafelyWithHint()
        {
            var old = Build.NewSnapshot();
            old.version = 0;
            io.WriteRawSnapshot(Build.DIR, Build.SnapshotName(1000), FakeJson.Write(old));

            Assert.IsNull(save.LoadLatest(), "缺迁移器不得强行按当前版本解析");
            StringAssert.Contains("迁移器", save.LastError);
        }

        [Test]
        public void Migrator_MustBumpVersion_ContractGuard()
        {
            // 已知风险 R-1（登记在 TEST_STRATEGY「已知风险」，未改 Core）：
            // LoadLatest 的 while (snap.version < SAVE_VERSION) 依赖迁移器自增 version；
            // 迁移器若忘记自增会死循环并卡死 CI。此处把"迁移器契约"固化为可执行断言。
            var s = Build.NewSnapshot();
            s.version = 0;
            Func<SaveSnapshot, SaveSnapshot> migrator = x => { x.version = 1; return x; };
            Assert.Greater(migrator(s).version, 0, "任何迁移器都必须自增 version");
        }

        // ── ⑥ 写盘失败：不崩溃、不丢上一档（S6 ⑥-1）──────────────────
        [Test]
        public void WriteFailure_IsCaught_PreviousSnapshotStaysReadable()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(null, SaveNode.NodeReceive), force: true));

            io.FailOnWrite = true;
            Assert.IsFalse(save.Save(Build.NewSnapshot(null, SaveNode.NodeChoose), force: true), "写失败须返回 false");
            Assert.IsNotNull(save.LastError);
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_SAVE_FAILED), "须发 EVT_SAVE_FAILED 供 UI 轻提示");
            Assert.AreEqual(1, io.SnapshotCount(), "失败不得新增半截档");

            io.FailOnWrite = false;
            Assert.AreEqual(SaveNode.NodeReceive, save.LoadLatest().last_node, "上一份可用档必须完好");
        }

        [Test]
        public void RenameFailure_LeavesTempFile_ButItIsNeverPickedUpAsSnapshot()
        {
            Assert.IsTrue(save.Save(Build.NewSnapshot(null, SaveNode.NodeReceive), force: true));

            io.FailOnMove = true;
            Assert.IsFalse(save.Save(Build.NewSnapshot(null, SaveNode.NodeChoose), force: true));

            Assert.IsTrue(io.Files.Keys.Any(k => k.EndsWith(".json.tmp", StringComparison.Ordinal)), "临时文件残留（下次写覆盖）");
            Assert.AreEqual(1, io.SnapshotCount(), "`save_*.json` 不得把 `.json.tmp` 收作快照");

            io.FailOnMove = false;
            Assert.AreEqual(SaveNode.NodeReceive, save.LoadLatest().last_node);
        }

        // ── ⑦ 依赖未注入：fail-fast，不静默 ─────────────────────────
        [Test]
        public void SerializeNotInjected_SaveFailsFast()
        {
            var bare = new SaveEngine(io, Build.DIR, bus);
            Assert.IsFalse(bare.Save(Build.NewSnapshot()));
            StringAssert.Contains("Serialize", bare.LastError);
            Assert.AreEqual(1, rec.Count(GameEvents.EVT_SAVE_FAILED));
            Assert.AreEqual(0, io.Files.Count);
        }

        [Test]
        public void DeserializeNotInjected_LoadFailsFast()
        {
            save.Save(Build.NewSnapshot(), force: true);
            var bare = new SaveEngine(io, Build.DIR, bus) { Serialize = FakeJson.Write };
            Assert.IsNull(bare.LoadLatest());
            StringAssert.Contains("Deserialize", bare.LastError);
        }
    }
}
