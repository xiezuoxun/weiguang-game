// TestKit.cs — EditMode 测试公共夹具（存储替身 / 校验和 / 可逆序列化 / 实体构造器 / 事件录音机）。
// 设计原则：
//   1. 被测代码零改动可测——Core 为纯 C#，IO 经 ISaveStorage 注入，故无需 PlayMode、无需真磁盘。
//   2. 替身要"像真的"——FakeStorage 的 ListFiles 模拟 Directory.GetFiles(dir, pattern) 的前缀/后缀匹配，
//      否则 `save_*.json` 是否会误收 `.json.tmp` 这类真实缺陷会被替身掩盖。
//   3. 可注入故障——FailOnWrite/FailOnMove 用来覆盖 S6⑥-1「写盘失败不崩溃、不丢上一档」。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Weiguang.Core;

namespace Weiguang.Tests
{
    /// <summary>内存存储替身 + 故障注入 + 调用录音（断言"临时文件→rename"真的发生了）。</summary>
    public class FakeStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        public readonly List<string> Writes = new List<string>();   // 写入过的路径（含 .tmp）
        public readonly List<string> Moves = new List<string>();    // "from -> to"
        public bool FailOnWrite;
        public bool FailOnMove;

        public void WriteAllText(string path, string text)
        {
            if (FailOnWrite) throw new System.IO.IOException("注入故障：磁盘写失败（模拟磁盘满/无权限）");
            Writes.Add(path);
            Files[path] = text;
        }

        public string ReadAllText(string path)
        {
            if (!Files.TryGetValue(path, out var t)) throw new System.IO.FileNotFoundException(path);
            return t;
        }

        public bool Exists(string path) => Files.ContainsKey(path);

        public void Move(string from, string to)
        {
            if (FailOnMove) throw new System.IO.IOException("注入故障：rename 失败");
            if (!Files.ContainsKey(from)) throw new System.IO.FileNotFoundException(from);
            Moves.Add(from + " -> " + to);
            Files[to] = Files[from];      // 同卷 rename：覆盖语义
            Files.Remove(from);
        }

        public void Delete(string path) => Files.Remove(path);

        /// <summary>模拟 Directory.GetFiles(dir, "save_*.json")：单层目录 + 前缀/后缀匹配，Ordinal 升序。</summary>
        public string[] ListFiles(string dir, string pattern)
        {
            int star = pattern.IndexOf('*');
            string pre = star < 0 ? pattern : pattern.Substring(0, star);
            string suf = star < 0 ? string.Empty : pattern.Substring(star + 1);
            return Files.Keys
                .Where(k => Norm(System.IO.Path.GetDirectoryName(k)) == Norm(dir))
                .Where(k => Match(System.IO.Path.GetFileName(k), pre, suf))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();
        }

        static bool Match(string name, string pre, string suf)
            => name.Length >= pre.Length + suf.Length
               && name.StartsWith(pre, StringComparison.Ordinal)
               && name.EndsWith(suf, StringComparison.Ordinal);

        static string Norm(string p) => (p ?? string.Empty).Replace('\\', '/').TrimEnd('/');

        // ── 测试便捷入口 ──────────────────────────────────────────
        public string[] Snapshots() => ListFiles(Build.DIR, "save_*.json");
        public int SnapshotCount() => Snapshots().Length;
        public string LatestSnapshot() => Snapshots().LastOrDefault();
        public string OldestSnapshot() => Snapshots().FirstOrDefault();

        /// <summary>绕过 SaveEngine 直接落一份"校验和正确"的原始档（用于构造旧版本/未来版本档）。</summary>
        public void WriteRawSnapshot(string dir, string fileName, string json)
            => Files[System.IO.Path.Combine(dir, fileName)] = Fnv1a.Sum(json) + "\n" + json;

        /// <summary>把某个档的正文改掉但保留旧校验和（模拟位翻转/半截写）。</summary>
        public void TamperPayload(string path, string find, string replace)
        {
            var raw = Files[path];
            int nl = raw.IndexOf('\n');
            Files[path] = raw.Substring(0, nl + 1) + raw.Substring(nl + 1).Replace(find, replace);
        }

        /// <summary>只改校验和行（模拟校验和损坏）。</summary>
        public void TamperChecksum(string path)
        {
            var raw = Files[path];
            int nl = raw.IndexOf('\n');
            Files[path] = "0000000000000000" + raw.Substring(nl);
        }
    }

    /// <summary>FNV-1a 64（必须与 SaveEngine.Checksum 位对位一致，否则手写档一律被判损坏）。</summary>
    public static class Fnv1a
    {
        public static string Sum(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16");
        }
    }

    /// <summary>测试用可逆序列化（Unity 层用 JsonUtility；此处只要"可逆 + 遇脏数据抛异常"）。</summary>
    public static class FakeJson
    {
        public static string Write(SaveSnapshot s)
        {
            string active = s.active_commission == null
                ? "null"
                : s.active_commission.commission_id + "@" + s.active_commission.phase + "@" + s.active_commission.fragment_count;
            return string.Join(";", new[]
            {
                "v=" + s.version.ToString(CultureInfo.InvariantCulture),
                "node=" + s.last_node,
                "active=" + active,
                "codex=" + (s.codex == null ? 0 : s.codex.Count).ToString(CultureInfo.InvariantCulture),
                "reveal=" + (s.reveal_states == null ? 0 : s.reveal_states.Count).ToString(CultureInfo.InvariantCulture),
                "frag=" + (s.fragment_states == null ? 0 : s.fragment_states.Count).ToString(CultureInfo.InvariantCulture),
                "at=" + (s.saved_at_iso ?? string.Empty)
            });
        }

        public static SaveSnapshot Read(string json)
        {
            var d = new Dictionary<string, string>();
            foreach (var kv in json.Split(';'))
            {
                int i = kv.IndexOf('=');
                if (i > 0) d[kv.Substring(0, i)] = kv.Substring(i + 1);
            }
            if (!d.ContainsKey("v")) throw new FormatException("快照缺少 version 字段：" + json);

            var s = new SaveSnapshot { version = int.Parse(d["v"], CultureInfo.InvariantCulture) };
            if (d.TryGetValue("node", out var node))
                s.last_node = (SaveNode)Enum.Parse(typeof(SaveNode), node);
            if (d.TryGetValue("at", out var at)) s.saved_at_iso = at;
            if (d.TryGetValue("active", out var a) && a != "null")
            {
                var p = a.Split('@');
                s.active_commission = new Commission
                {
                    commission_id = p[0],
                    phase = (CommissionPhase)Enum.Parse(typeof(CommissionPhase), p[1]),
                    fragment_count = int.Parse(p[2], CultureInfo.InvariantCulture)
                };
            }
            return s;
        }
    }

    /// <summary>契约实体构造器：默认值一律合规，测试只改"要越界的那一个字段"。</summary>
    public static class Build
    {
        public const string DIR = "/mem";

        public static Commission NewCommission(string id = "com_t", int fragmentCount = 3,
                                               CommissionPhase phase = CommissionPhase.Idle)
            => new Commission
            {
                commission_id = id,
                client_id = "cl_t",
                item_id = "it_t",
                chapter_index = 1,
                phase = phase,
                session_soft_budget_min = 7f,
                is_daily = false,
                reveal_threshold = 0.85f,
                fragment_count = fragmentCount,
                choice_count = 2,
                ending_variants = 2,
                is_mainplot = false
            };

        public static Client NewClient(string id = "cl_t", int level = 1)
            => new Client { client_id = id, display_name = "测试客户", relationship_level = level, visit_count = 0, mainplot_progress = 0 };

        public static SaveSnapshot NewSnapshot(Commission c = null, SaveNode node = SaveNode.NodeReceive)
            => new SaveSnapshot
            {
                version = SaveEngine.SAVE_VERSION,
                active_commission = c,
                last_node = node
            };

        /// <summary>19 位零填充档名（与 SaveEngine.FileName 的 D19 一致，保证 Ordinal 排序==时间序）。</summary>
        public static string SnapshotName(long ms) => "save_" + ms.ToString("D19", CultureInfo.InvariantCulture) + ".json";
    }

    /// <summary>事件录音机：断言"发且只发一次"、载荷正确。</summary>
    public class EventRecorder
    {
        readonly List<KeyValuePair<string, object>> _log = new List<KeyValuePair<string, object>>();

        public EventRecorder(EventBus bus, params string[] events)
        {
            foreach (var e in events)
            {
                var captured = e;
                bus.Subscribe(captured, p => _log.Add(new KeyValuePair<string, object>(captured, p)));
            }
        }

        public int Count(string evt) => _log.Count(x => x.Key == evt);
        public int Total => _log.Count;
        public object Last(string evt) => _log.Where(x => x.Key == evt).Select(x => x.Value).LastOrDefault();
        public List<object> Payloads(string evt) => _log.Where(x => x.Key == evt).Select(x => x.Value).ToList();
        public List<string> Names() => _log.Select(x => x.Key).ToList();
        public void Clear() => _log.Clear();
    }
}
