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
        // S1 体验层钩子：跨越 0.25/0.50/0.75 任一段阈值时发出（与 whisper 同源但语义独立），
        // 供美术接"顿挫感"脉冲（Shader 脉冲/震感）的精确触发点；payload 带跨过的 level 与当前 progress。
        public const string EVT_REVEAL_THRESHOLD_CROSSED = "EVT_REVEAL_THRESHOLD_CROSSED";
        // S2 → S4
        public const string EVT_ASSEMBLE_COMPLETE = "EVT_ASSEMBLE_COMPLETE";
        // S3 → S4
        public const string EVT_CHOICE_MADE = "EVT_CHOICE_MADE";
        // S3 体验层钩子：抉择点中某选项被"悬停高亮"时发出，供美术接纸签高亮 Shader 的触发点；
        // 与 EVT_CHOICE_MADE 区分——高亮是预选中态（手指悬停/聚焦），选中是落定态。
        public const string EVT_OPTION_HIGHLIGHTED = "EVT_OPTION_HIGHLIGHTED";
        // S3 体验层钩子：抉择点中某选项被"正式选中"时发出（与 EVT_CHOICE_MADE 同源，tag 语义不同），
        // 供美术接纸签选中高亮 Shader 的触发点；payload 带 option_id，不含文案内容。
        public const string EVT_OPTION_SELECTED = "EVT_OPTION_SELECTED";
        // S5 归档（图鉴收口）→ 广播给 UI/上层做图鉴表现，不含文案内容（设计侧后续填）
        public const string EVT_ARCHIVED = "EVT_ARCHIVED";
        // S5 体验层钩子：图鉴条目"解锁"独立事件（与 EVT_ARCHIVED 同源但语义独立），
        // 供图鉴解锁动画有独立触发点（解锁脉冲/翻开动画），不依赖归档收束的其他表现。payload 带 entry_id。
        public const string EVT_CODEX_UNLOCKED = "EVT_CODEX_UNLOCKED";
        // S6
        public const string EVT_SAVE_WRITTEN = "EVT_SAVE_WRITTEN";
        public const string EVT_SAVE_FAILED = "EVT_SAVE_FAILED";
        // 首启动引导（打磨）：_snapshot 为空（首次进入）时由 GameBootstrap 广播，
        // 供 UI 层弹首见引导；payload 带四动词引导文案（OnboardingHints）。
        public const string EVT_FIRST_LAUNCH = "EVT_FIRST_LAUNCH";
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

    /// <summary>S1 体验层钩子载荷：拂尘跨越 0.25/0.50/0.75 任一段阈值时由 RevealThresholdTracker 经 EventBus 广播。
    /// level 为跨过的阈值档位（RevealThresholdTracker.T25/T50/T75 之一），progress 为跨越瞬间的当前 reveal 进度。
    /// 该事件与 EVT_REVEAL_WHISPER 同源但语义独立——whisper 偏文案、本事件偏手感脉冲，美术可只接本事件做顿挫感。</summary>
    public class RevealThresholdCrossedEvent
    {
        public readonly float level;     // 跨过的阈值档位（0.25/0.50/0.75）
        public readonly float progress;  // 跨越瞬间的当前 reveal 进度（钳制后）

        public RevealThresholdCrossedEvent(float level, float progress)
        {
            this.level = level;
            this.progress = progress;
        }
    }

    /// <summary>S3 体验层钩子载荷：抉择点中选项被"悬停高亮"/"正式选中"时由 SessionRunner 经 EventBus 广播。
    /// option_id 为被高亮/选中的选项 id；type 标识 HIGHHLIGHTED/SELECTED 两态，便于同一订阅者分流。
    /// 不含文案内容（design-strategist 后续供应纸签文案），工程侧只提供字段容器 + 触发钩子。</summary>
    public class ChoiceOptionEvent
    {
        public const string TYPE_HIGHLIGHTED = "highlighted";
        public const string TYPE_SELECTED = "selected";

        public readonly string option_id; // 被高亮/选中的选项 id
        public readonly string type;       // highlighted / selected

        public ChoiceOptionEvent(string option_id, string type)
        {
            this.option_id = option_id;
            this.type = type;
        }
    }

    /// <summary>S5 体验层钩子载荷：图鉴条目解锁时由 SessionRunner 经 EventBus 广播（与 EVT_ARCHIVED 同源但语义独立）。
        /// entry_id 为图鉴条目 id（cx_&lt;commission_id&gt;），供图鉴解锁动画定位条目；不含文案内容（设计侧后续填）。</summary>
        public class CodexUnlockedEvent
        {
            public readonly string entry_id; // 图鉴条目 id（cx_&lt;commission_id&gt;）

            public CodexUnlockedEvent(string entry_id)
            {
                this.entry_id = entry_id;
            }
        }

        /// <summary>首启动引导事件载荷（打磨）：首次进入（_snapshot 为空）时由 GameBootstrap 广播。
        /// 携带四核心动词的引导文案（title/hint 二元组，来自 OnboardingHints），供 UI 层展示首见引导；
        /// 不含任何 Unity 依赖，纯字符串容器。</summary>
        public class FirstLaunchEvent
        {
            public readonly KeyValuePair<string, string> reveal;
            public readonly KeyValuePair<string, string> assemble;
            public readonly KeyValuePair<string, string> choose;
            public readonly KeyValuePair<string, string> archive;

            public FirstLaunchEvent(
                KeyValuePair<string, string> reveal,
                KeyValuePair<string, string> assemble,
                KeyValuePair<string, string> choose,
                KeyValuePair<string, string> archive)
            {
                this.reveal = reveal; this.assemble = assemble; this.choose = choose; this.archive = archive;
            }
        }
    }
