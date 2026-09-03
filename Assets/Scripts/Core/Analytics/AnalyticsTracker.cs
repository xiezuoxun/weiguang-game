// AnalyticsTracker.cs — Phase 8-A 数据埋点核心（纯 C#，无 UnityEngine 依赖，可 EditMode 测）。
// 订阅四动词 + 委托生命周期事件，聚合：四动词首见率、碎片吸附率、抉择分布、基础漏斗。
// 收到事件时调用 IAnalyticsSink.Track 上报（细粒度事件流 + 归档时吐聚合快照）。
// 退订通过 Unsubscribe 精准移除每个 handler，绝不 Clear 误清其他订阅者（沿用 EventBus 设计意图）。
using System;
using System.Collections.Generic;
using Weiguang.Core;

namespace Weiguang.Core.Analytics
{
    /// <summary>Phase 8-A 埋点聚合器。纯 C#，仅依赖 Core 层 EventBus + IAnalyticsSink。</summary>
    public class AnalyticsTracker
    {
        // ── 细粒度事件流事件名 ──────────────────────────────────────
        public const string E_NAME_REVEAL = "reveal_whisper";
        public const string E_NAME_REVEAL_DONE = "reveal_complete";
        public const string E_NAME_ASSEMBLE = "assemble_complete";
        public const string E_NAME_CHOICE = "choice_made";
        public const string E_NAME_ARCHIVED = "archived";
        public const string E_NAME_CODEX = "codex_unlocked";
        public const string E_NAME_FIRST_LAUNCH = "first_launch";
        public const string E_NAME_COMMISSION_START = "commission_start";
        public const string E_NAME_COMMISSION_DONE = "commission_done";
        // ── 聚合快照事件名（归档 / 委托完成时吐一次）──
        public const string E_NAME_METRICS = "analytics_metrics";

        readonly EventBus _bus;
        readonly IAnalyticsSink _sink;

        // ── 漏斗 / 首见（按"委托"scoped：每个委托计一次；cumulative 跨委托累加）──
        int _commissionStarts;            // 委托开始数（漏斗起点）
        int _commissionDones;             // 委托完成数（当前代码无 publisher，长期为 0，见文档说明）
        bool _comReveal, _comAssemble, _comChoose, _comArchive; // 当前委托内是否已触发该动词
        int _fsReveal, _fsAssemble, _fsChoose, _fsArchive;      // cumulative：到达过该阶段的委托数

        // ── 碎片吸附（来自 EVT_ASSEMBLE_COMPLETE 的 locked/total 累加）──
        long _fragLocked, _fragTotal;

        // ── 抉择分布（按 ending_tag）──
        readonly Dictionary<string, int> _choiceByTag = new Dictionary<string, int>();

        // handler 引用（持有以便精准退订）
        readonly EventBus.Handler _hRevealWhisper, _hRevealComplete, _hAssemble, _hChoice,
                                  _hArchived, _hCodex, _hFirstLaunch, _hCommStart, _hCommDone;

        public AnalyticsTracker(EventBus bus, IAnalyticsSink sink)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _hRevealWhisper  = OnRevealWhisper;
            _hRevealComplete = OnRevealComplete;
            _hAssemble       = OnAssembleComplete;
            _hChoice         = OnChoiceMade;
            _hArchived       = OnArchived;
            _hCodex          = OnCodexUnlocked;
            _hFirstLaunch    = OnFirstLaunch;
            _hCommStart      = OnCommissionStart;
            _hCommDone       = OnCommissionDone;
        }

        /// <summary>订阅全部埋点相关事件（由宿主在 bus 创建后、首个 Publish 之前调用）。</summary>
        public void Subscribe()
        {
            _bus.Subscribe(GameEvents.EVT_REVEAL_WHISPER, _hRevealWhisper);
            _bus.Subscribe(GameEvents.EVT_REVEAL_COMPLETE, _hRevealComplete);
            _bus.Subscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, _hAssemble);
            _bus.Subscribe(GameEvents.EVT_CHOICE_MADE, _hChoice);
            _bus.Subscribe(GameEvents.EVT_ARCHIVED, _hArchived);
            _bus.Subscribe(GameEvents.EVT_CODEX_UNLOCKED, _hCodex);
            _bus.Subscribe(GameEvents.EVT_FIRST_LAUNCH, _hFirstLaunch);
            _bus.Subscribe(GameEvents.EVT_COMMISSION_START, _hCommStart);
            _bus.Subscribe(GameEvents.EVT_COMMISSION_DONE, _hCommDone);
        }

        /// <summary>精准退订（不 Clear，避免误清其他订阅者）。宿主在 OnDestroy 调用。</summary>
        public void Unsubscribe()
        {
            _bus.Unsubscribe(GameEvents.EVT_REVEAL_WHISPER, _hRevealWhisper);
            _bus.Unsubscribe(GameEvents.EVT_REVEAL_COMPLETE, _hRevealComplete);
            _bus.Unsubscribe(GameEvents.EVT_ASSEMBLE_COMPLETE, _hAssemble);
            _bus.Unsubscribe(GameEvents.EVT_CHOICE_MADE, _hChoice);
            _bus.Unsubscribe(GameEvents.EVT_ARCHIVED, _hArchived);
            _bus.Unsubscribe(GameEvents.EVT_CODEX_UNLOCKED, _hCodex);
            _bus.Unsubscribe(GameEvents.EVT_FIRST_LAUNCH, _hFirstLaunch);
            _bus.Unsubscribe(GameEvents.EVT_COMMISSION_START, _hCommStart);
            _bus.Unsubscribe(GameEvents.EVT_COMMISSION_DONE, _hCommDone);
        }

        // ── 公共只读指标（供测试 / 上层查询 / GetMetrics 聚合）──
        public int CommissionStarts => _commissionStarts;
        public int CommissionDones => _commissionDones;
        public int FirstSeenReveal => _fsReveal;
        public int FirstSeenAssemble => _fsAssemble;
        public int FirstSeenChoose => _fsChoose;
        public int FirstSeenArchive => _fsArchive;
        public long FragmentLocked => _fragLocked;
        public long FragmentTotal => _fragTotal;
        public double AdsorbRate => _fragTotal > 0 ? (double)_fragLocked / _fragTotal : 0d;
        public IReadOnlyDictionary<string, int> ChoiceByEndingTag => _choiceByTag;

        // 首见率 = 到达过该阶段的委托数 / 委托开始数（单用户样本近似；跨用户真实率由后端按 user 聚合）。
        public double RevealFirstSeenRate => _commissionStarts > 0 ? (double)_fsReveal / _commissionStarts : 0d;
        public double AssembleFirstSeenRate => _commissionStarts > 0 ? (double)_fsAssemble / _commissionStarts : 0d;
        public double ChooseFirstSeenRate => _commissionStarts > 0 ? (double)_fsChoose / _commissionStarts : 0d;
        public double ArchiveFirstSeenRate => _commissionStarts > 0 ? (double)_fsArchive / _commissionStarts : 0d;

        /// <summary>聚合指标快照（嵌套 choice_distribution / funnel 为 Dictionary，供后端分组）。</summary>
        public IDictionary<string, object> GetMetrics()
        {
            return new Dictionary<string, object>
            {
                ["commission_starts"] = _commissionStarts,
                ["commission_dones"] = _commissionDones,
                ["reveal_first_seen"] = _fsReveal,
                ["assemble_first_seen"] = _fsAssemble,
                ["choose_first_seen"] = _fsChoose,
                ["archive_first_seen"] = _fsArchive,
                ["reveal_first_seen_rate"] = Math.Round(RevealFirstSeenRate, 4),
                ["assemble_first_seen_rate"] = Math.Round(AssembleFirstSeenRate, 4),
                ["choose_first_seen_rate"] = Math.Round(ChooseFirstSeenRate, 4),
                ["archive_first_seen_rate"] = Math.Round(ArchiveFirstSeenRate, 4),
                ["fragment_locked"] = _fragLocked,
                ["fragment_total"] = _fragTotal,
                ["fragment_adsorb_rate"] = Math.Round(AdsorbRate, 4),
                ["choice_distribution"] = new Dictionary<string, int>(_choiceByTag),
                ["funnel"] = new Dictionary<string, int>
                {
                    ["commission_start"] = _commissionStarts,
                    ["reveal"] = _fsReveal,
                    ["assemble"] = _fsAssemble,
                    ["choose"] = _fsChoose,
                    ["archive"] = _fsArchive,
                },
            };
        }

        // ── handlers ─────────────────────────────────────────────
        void OnCommissionStart(object payload)
        {
            _commissionStarts++;
            // 新委托：重置"本次委托内是否已触发动词"的口径，使首见按委托计数（而非按事件触发次数）。
            _comReveal = _comAssemble = _comChoose = _comArchive = false;

            string id = (payload as Commission)?.commission_id ?? "";
            _sink.Track(E_NAME_COMMISSION_START, new Dictionary<string, object> { ["commission_id"] = id });
        }

        void OnCommissionDone(object payload)
        {
            _commissionDones++;
            string id = (payload as Commission)?.commission_id ?? "";
            _sink.Track(E_NAME_COMMISSION_DONE, new Dictionary<string, object> { ["commission_id"] = id });
        }

        void OnRevealWhisper(object payload)
        {
            if (payload is RevealWhisperEvent ev)
            {
                _sink.Track(E_NAME_REVEAL, new Dictionary<string, object>
                {
                    ["whisper_key"] = ev.whisper_key,
                    ["reveal_pct"] = ev.reveal_pct,
                });
            }
            MarkReveal();
        }

        void OnRevealComplete(object payload)
        {
            float pct = payload is float f ? f : 0f;
            _sink.Track(E_NAME_REVEAL_DONE, new Dictionary<string, object> { ["reveal_pct"] = pct });
            MarkReveal();
        }

        void OnAssembleComplete(object payload)
        {
            int locked = 0, total = 0;
            if (payload is AssembleCompleteEvent ev)
            {
                locked = ev.locked_count;
                total = ev.total_count;
                _fragLocked += locked;
                _fragTotal += total;
            }
            _sink.Track(E_NAME_ASSEMBLE, new Dictionary<string, object>
            {
                ["locked"] = locked,
                ["total"] = total,
                ["adsorb_rate"] = total > 0 ? Math.Round((double)locked / total, 4) : 0d,
            });
            if (!_comAssemble) { _comAssemble = true; _fsAssemble++; }
        }

        void OnChoiceMade(object payload)
        {
            string tag = (payload is EndingTag t) ? t.ToString() : (payload?.ToString() ?? "unknown");
            if (_choiceByTag.ContainsKey(tag)) _choiceByTag[tag]++;
            else _choiceByTag[tag] = 1;
            _sink.Track(E_NAME_CHOICE, new Dictionary<string, object> { ["ending_tag"] = tag });
            if (!_comChoose) { _comChoose = true; _fsChoose++; }
        }

        void OnArchived(object payload)
        {
            if (payload is ArchivedEvent ev)
            {
                _sink.Track(E_NAME_ARCHIVED, new Dictionary<string, object>
                {
                    ["entry_id"] = ev.entry_id,
                    ["timeline_order"] = ev.timeline_order,
                    ["is_mainplot"] = ev.is_mainplot,
                });
            }
            if (!_comArchive) { _comArchive = true; _fsArchive++; }
            // 委托归档 = 漏斗终点：吐一次聚合快照（含首见率 / 吸附率 / 抉择分布 / 漏斗）。
            _sink.Track(E_NAME_METRICS, GetMetrics());
        }

        void OnCodexUnlocked(object payload)
        {
            if (payload is CodexUnlockedEvent ev)
                _sink.Track(E_NAME_CODEX, new Dictionary<string, object> { ["entry_id"] = ev.entry_id });
            // 注：首见口径以 EVT_ARCHIVED（归档动词）为准；codex_unlock 为同源体验层钩子，不重复计首见。
        }

        void OnFirstLaunch(object payload)
        {
            _sink.Track(E_NAME_FIRST_LAUNCH, new Dictionary<string, object>());
        }

        void MarkReveal()
        {
            if (!_comReveal) { _comReveal = true; _fsReveal++; }
        }
    }
}
