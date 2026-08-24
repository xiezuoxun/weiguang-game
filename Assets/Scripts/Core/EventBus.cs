// EventBus.cs — ADR-005 分层解耦与事件总线：玩法系统零互调，只经事件通信。
// EVT_* 事件名与 GDD ③接口表一一对应。
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    public static class GameEvents
    {
        // S4 广播
        public const string EVT_COMMISSION_START = "EVT_COMMISSION_START";
        public const string EVT_COMMISSION_DONE = "EVT_COMMISSION_DONE";
        public const string EVT_PHASE_CHANGED = "EVT_PHASE_CHANGED";
        // S1 → S4
        public const string EVT_REVEAL_COMPLETE = "EVT_REVEAL_COMPLETE";
        // S1 浮纸签：越 0.25/0.50/0.75 阈值时各发一次（once-lock），不依赖文案内容
        public const string EVT_REVEAL_WHISPER = "EVT_REVEAL_WHISPER";
        // S2 → S4
        public const string EVT_ASSEMBLE_COMPLETE = "EVT_ASSEMBLE_COMPLETE";
        // S3 → S4
        public const string EVT_CHOICE_MADE = "EVT_CHOICE_MADE";
        // S5 归档（图鉴收口）→ 广播给 UI/上层做图鉴表现，不含文案内容（设计侧后续填）
        public const string EVT_ARCHIVED = "EVT_ARCHIVED";
        // S6
        public const string EVT_SAVE_WRITTEN = "EVT_SAVE_WRITTEN";
        public const string EVT_SAVE_FAILED = "EVT_SAVE_FAILED";
        // 契约/状态机告警（护栏 §5：越界与非法迁移不静默）——C2 命名唯一：事件名只在此处声明，禁止散落字面量
        public const string EVT_CONTRACT_WARN = "EVT_CONTRACT_WARN";
    }

    public class EventBus
    {
        public delegate void Handler(object payload);

        readonly Dictionary<string, List<Handler>> _subs = new Dictionary<string, List<Handler>>();

        public void Subscribe(string evt, Handler h)
        {
            if (!_subs.TryGetValue(evt, out var list)) { list = new List<Handler>(); _subs[evt] = list; }
            list.Add(h);
        }

        public Action<string> LogError = _ => { }; // 由宿主注入（Unity 层接 Debug.LogError）

        public void Publish(string evt, object payload = null)
        {
            if (!_subs.TryGetValue(evt, out var list)) return;
            // 拷贝防回调中退订
            var snapshot = list.ToArray();
            foreach (var h in snapshot) { try { h(payload); } catch (Exception e) { LogError($"[EventBus] {evt} handler 异常: {e.Message}"); } }
        }

        public void Clear() => _subs.Clear();
    }

    /// <summary>S1-3 浮纸签事件载荷（CR-001）：越阈时由 SessionRunner 经 EventBus 广播。
    /// whisper_key 固定为 whisper_25/50/75；text 允许为空（design-strategist 后续供应），
    /// 工程侧只提供字段容器 + 触发回调钩子，不依赖文案内容。</summary>
    public class RevealWhisperEvent
    {
        public const string WHISPER_25 = "whisper_25";
        public const string WHISPER_50 = "whisper_50";
        public const string WHISPER_75 = "whisper_75";

        public readonly string whisper_key;  // 触发档位标识（25/50/75 之一）
        public readonly float reveal_pct;    // 触发时的揭示进度
        public readonly string text;         // 低语文案，可为空（设计侧后续填）

        public RevealWhisperEvent(string whisper_key, float reveal_pct, string text = null)
        {
            this.whisper_key = whisper_key;
            this.reveal_pct = reveal_pct;
            this.text = text;
        }
    }

    /// <summary>S2-3 拼合完成事件载荷：所有碎片锁定后由 SessionRunner 经 EventBus 广播。
    /// locked_count / total_count 用于上层（Unity 层）做进度表现与完成反馈，不含文案内容（设计侧后续填）。</summary>
    public class AssembleCompleteEvent
    {
        public readonly int locked_count;  // 已锁定碎片数
        public readonly int total_count;   // 碎片总数（= fragment_count）

        public AssembleCompleteEvent(int locked_count, int total_count)
        {
            this.locked_count = locked_count;
            this.total_count = total_count;
        }
    }

    /// <summary>S5-4 归档事件载荷：CodexEntry 落库后由 SessionRunner 经 EventBus 广播。
    /// 仅携带上层表现所需的最小字段（entry_id / timeline_order / is_mainplot），
    /// 不含文案内容（quote 等设计侧后续填）。</summary>
    public class ArchivedEvent
    {
        public readonly string entry_id;       // 图鉴条目 id（cx_&lt;commission_id&gt;）
        public readonly int timeline_order;    // 时间线序号（从 0 递增）
        public readonly bool is_mainplot;      // 是否主线条目

        public ArchivedEvent(string entry_id, int timeline_order, bool is_mainplot)
        {
            this.entry_id = entry_id;
            this.timeline_order = timeline_order;
            this.is_mainplot = is_mainplot;
        }
    }
}
