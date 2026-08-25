// RevealVisualBridge.cs — B1 拂尘显影表现桥（订阅 EVT_REVEAL_WHISPER）
// 作者：林绘澄（art-director）｜Phase 6-A
// 职责：订阅 EVT_REVEAL_WHISPER / EVT_REVEAL_THRESHOLD_CROSSED，驱动 DustReveal.shader 的
//       _Pulse 补间（120–200ms 0→1→0）与 _Threshold 写入，制造阈值顿挫感。
//       美术表现（Material 赋值）留 TODO；无材质时仅记日志，不崩。
using UnityEngine;
using Weiguang.Core;

namespace Weiguang.Runtime.ArtBinding
{
    /// <summary>
    /// 挂载到 DustGrid 容器 GameObject；由 GameBootstrap.Bind 注入 EventBus，OnDestroy 退订。
    /// _pulseDurationMs 默认 160ms（验收区间 120–200ms）。
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class RevealVisualBridge : ArtBridgeBase
    {
        [Tooltip("脉冲时长(ms)，验收区间 120–200")]
        public float pulseDurationMs = 160f;

        Material _mat;          // DustReveal 材质实例（TODO：编辑器挂或运行时实例化）
        float _pulseStart = -1f;

        protected override void OnBind()
        {
            var r = GetComponent<Renderer>();
            _mat = r != null ? r.material : null;
            Bus.Subscribe(GameEvents.EVT_REVEAL_WHISPER, OnWhisper);
            Bus.Subscribe(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED, OnCrossed);
        }

        protected override void OnUnbind()
        {
            Bus.Unsubscribe(GameEvents.EVT_REVEAL_WHISPER, OnWhisper);
            Bus.Unsubscribe(GameEvents.EVT_REVEAL_THRESHOLD_CROSSED, OnCrossed);
        }

        void OnWhisper(object payload)
        {
            if (payload is RevealWhisperEvent ev)
                TriggerPulse(ThresholdOf(ev.whisper_key));
        }

        void OnCrossed(object payload)
        {
            if (payload is RevealThresholdCrossedEvent ev)
                TriggerPulse(ev.level);
        }

        void TriggerPulse(float threshold)
        {
            if (_mat != null)
            {
                _mat.SetFloat("_Threshold", threshold); // 对齐 RevealThresholdTracker.T25/50/T75
                _mat.SetFloat("_Pulse", 0f);
            }
            _pulseStart = Time.unscaledTime;
            // TODO(美术表现): 触发浮纸签（whispers.csv 文案）浮现 UI，由 UI 层订阅 EVT_REVEAL_WHISPER。
            Debug.Log($"[ArtBinding/Reveal] 脉冲触发 @threshold={threshold}");
        }

        void Update()
        {
            if (_pulseStart < 0f || _mat == null) return;
            float t = (Time.unscaledTime - _pulseStart) * 1000f / pulseDurationMs;
            if (t >= 1f) { _mat.SetFloat("_Pulse", 0f); _pulseStart = -1f; return; }
            float v = t < 0.5f ? (t * 2f) : (1f - (t - 0.5f) * 2f); // 0→1→0 三角补间
            _mat.SetFloat("_Pulse", v);
        }

        static float ThresholdOf(string key) =>
            key == RevealWhisperEvent.WHISPER_75 ? RevealThresholdTracker.T75 :
            key == RevealWhisperEvent.WHISPER_50 ? RevealThresholdTracker.T50 :
            RevealThresholdTracker.T25;
    }
}
