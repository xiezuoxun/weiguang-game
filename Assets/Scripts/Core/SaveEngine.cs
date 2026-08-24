// SaveEngine.cs — EPIC-01 自动存档底座（S6 GDD 全八节映射，ADR-002 快照式存档）。
// 纯 C# 核心：IO 经 ISaveStorage 抽象，Unity 层注入实现（persistentDataPath）。
// 机制：临时文件+rename 原子写 ｜ ≤1次/3s 节流 ｜ force 立即写（onPause）｜
//       FNV-1a 校验和 ｜ 版本迁移链 ｜ 损坏回退上一可用档。
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    public interface ISaveStorage
    {
        void WriteAllText(string path, string text);
        string ReadAllText(string path);
        bool Exists(string path);
        void Move(string from, string to);   // 原子 rename（同卷）
        void Delete(string path);
        string[] ListFiles(string dir, string pattern);
    }

    public class SaveEngine
    {
        public const int SAVE_VERSION = 1;
        const int THROTTLE_MS = 3000;
        readonly ISaveStorage _io;
        readonly string _dir;
        readonly EventBus _bus;
        long _lastWriteMs;

        // 快照序列化由宿主注入（Unity 层用 JsonUtility；测试层用任意 JSON）
        public Func<SaveSnapshot, string> Serialize;
        public Func<string, SaveSnapshot> Deserialize;
        // 版本迁移链：oldVersion → 迁移器
        readonly Dictionary<int, Func<SaveSnapshot, SaveSnapshot>> _migrations =
            new Dictionary<int, Func<SaveSnapshot, SaveSnapshot>>();

        public bool HasPendingDirtyWrite { get; private set; } // 节流跳过时置位，下个节点补写
        public string LastError { get; private set; }

        public SaveEngine(ISaveStorage io, string dir, EventBus bus)
        {
            _io = io; _dir = dir; _bus = bus;
        }

        public void RegisterMigration(int fromVersion, Func<SaveSnapshot, SaveSnapshot> m)
            => _migrations[fromVersion] = m;

        /// <summary>存档入口。force=true 时无视节流（onPause 强制写，S6 ⑤）。</summary>
        public bool Save(SaveSnapshot snap, bool force = false)
        {
            if (Serialize == null) { Fail("Serialize 未注入"); return false; }
            long now = NowMs();
            if (!force && now - _lastWriteMs < THROTTLE_MS)
            { HasPendingDirtyWrite = true; return true; } // 节流：合并写，不视为失败

            try
            {
                snap.version = SAVE_VERSION;
                snap.saved_at_iso = DateTime.UtcNow.ToString("o");
                string json = Serialize(snap);
                // 文件名单调递增：同一毫秒内的两次强制写（如 ARCHIVED 收口 + onPause）会生成同名文件，
                // rename 覆盖掉上一份快照 → 破坏 S6⑥-2「损坏则回退上一可用档」的兜底。+1ms 去重。
                if (now <= _lastWriteMs) now = _lastWriteMs + 1;
                string name = FileName(now);
                string tmp = name + ".tmp";
                // 校验和行：首行 <fnv64>\n + JSON（读档时先验）
                _io.WriteAllText(System.IO.Path.Combine(_dir, tmp), Checksum(json) + "\n" + json);
                _io.Move(System.IO.Path.Combine(_dir, tmp), System.IO.Path.Combine(_dir, name));
                _lastWriteMs = now; HasPendingDirtyWrite = false;
                _bus.Publish(GameEvents.EVT_SAVE_WRITTEN, name);
                return true;
            }
            catch (Exception e) { Fail(e.Message); return false; }
        }

        /// <summary>读最新可用快照。损坏→回退更早档；全部损坏→null（新建，不丢全部已归档内容则由调用方合并处理）。</summary>
        public SaveSnapshot LoadLatest()
        {
            if (Deserialize == null) { Fail("Deserialize 未注入"); return null; }
            var files = _io.ListFiles(_dir, "save_*.json");
            Array.Sort(files, StringComparer.Ordinal); // 时间戳命名，倒序取最新
            for (int i = files.Length - 1; i >= 0; i--)
            {
                var snap = TryRead(files[i]);
                if (snap != null)
                {
                    if (snap.version > SAVE_VERSION) { Fail($"存档版本 {snap.version} 高于当前 {SAVE_VERSION}，请更新"); return null; }
                    while (snap.version < SAVE_VERSION)
                    {
                        if (!_migrations.TryGetValue(snap.version, out var m)) { Fail($"无 v{snap.version}→迁移器"); return null; }
                        snap = m(snap);
                    }
                    return snap;
                }
                // 该档损坏 → 继续回退更早（S6 ⑥-2）
            }
            return null; // 无可用档 → 新开
        }

        SaveSnapshot TryRead(string path)
        {
            try
            {
                string raw = _io.ReadAllText(path);
                int nl = raw.IndexOf('\n');
                if (nl < 0) return null;
                string sum = raw.Substring(0, nl).Trim();
                string json = raw.Substring(nl + 1);
                if (Checksum(json) != sum) return null; // 校验失败 = 损坏
                return Deserialize(json);
            }
            catch { return null; }
        }

        // FNV-1a 64 —— 纯托管实现，无平台依赖
        static string Checksum(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16");
        }

        static string FileName(long ms) => $"save_{ms:D19}.json";
        static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        void Fail(string msg)
        {
            LastError = msg;
            _bus.Publish(GameEvents.EVT_SAVE_FAILED, msg);
        }
    }
}
