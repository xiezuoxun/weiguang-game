// CodexVisualBridge.cs — S5 图鉴归档表现桥（订阅 EVT_ARCHIVED / EVT_CODEX_UNLOCKED）
// 作者：林绘澄（art-director）｜Phase 6-A
// 职责：订阅 EVT_ARCHIVED 与 EVT_CODEX_UNLOCKED，点亮图鉴条目（解锁脉冲 / 翻开动画）。
//       不依赖归档收束的其他表现（事件语义独立，见 EventBus 注释）。美术表现留 TODO，无资产不崩。
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到 Codex UI 容器。条目 GameObject 命名为 entry_&lt;entry_id&gt;（cx_&lt;commission_id&gt;）。
    /// </summary>
    public class CodexVisualBridge : ArtBridgeBase
    {
        [Tooltip("解锁脉冲时长(ms)")]
        public float unlockPulseMs = 400f;

        readonly System.Collections.Generic.Dictionary<string, Transform> _entries =
            new System.Collections.Generic.Dictionary<string, Transform>();

        public void RegisterEntry(string entryId, Transform entry) { if (entryId != null) _entries[entryId] = entry; }

        protected override void OnBind()
        {
            Bus.Subscribe(GameEvents.EVT_ARCHIVED, OnArchived);
            Bus.Subscribe(GameEvents.EVT_CODEX_UNLOCKED, OnUnlocked);
        }

        protected override void OnUnbind() { }

        void OnArchived(object payload)
        {
            if (payload is ArchivedEvent ev)
                Debug.Log($"[ArtBinding/Codex] 归档完成 entry={ev.entry_id} mainplot={ev.is_mainplot}");
            // TODO(音频): 播 SFX-04 归档完成音（温暖收束，呼应微光主题）。
        }

        void OnUnlocked(object payload)
        {
            if (payload is CodexUnlockedEvent ev && _entries.TryGetValue(ev.entry_id, out var e))
            {
                // TODO(美术表现): 条目解锁脉冲 / 翻开动画（独立触发点，不依赖归档收束）。
                Debug.Log($"[ArtBinding/Codex] 解锁脉冲 entry={ev.entry_id}");
            }
        }
    }
}
