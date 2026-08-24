// RevealThresholdTracker.cs — S1-2 拂尘阈值回调（CR-001 §④）。
// once-lock：0.25 / 0.50 / 0.75 三档各只触发一次，触发后锁定不再重复。
// 纯 C#（Core），不依赖 UnityEngine；边界输入 pct 钳制到 [0,1]。
using System;

namespace Weiguang.Core
{
    public class RevealThresholdTracker
    {
        // 三档阈值（与 CR-001 §④ 一致）
        public const float T25 = 0.25f;
        public const float T50 = 0.50f;
        public const float T75 = 0.75f;

        bool _fired25, _fired50, _fired75;

        /// <summary>越阈且未触发时回调 onThreshold(reveal_pct)，并锁定该档（once-lock）。
        /// pct 越界（&lt;0 或 &gt;1）会被钳制到 [0,1]，不抛异常（护栏 §5：越界截断并告警，不静默）。</summary>
        /// <returns>本次新触发的档位数（0/1），便于调用方计数。</returns>
        public int Update(float pct, Action<float> onThreshold)
            => Update(pct, onThreshold, null);

        /// <summary>越阈且未触发时回调 onThreshold(reveal_pct)，并锁定该档（once-lock）。
        /// 另提供体验层钩子 onCrossed：跨越任一段阈值时回调 onCrossed(new RevealThresholdCrossedEvent(level, clamped))，
        /// 供美术接"顿挫感"脉冲（钩子为可选，传 null 即无钩子、行为与原方法完全一致）。
        /// onThreshold 与 onCrossed 触发次数一致（同一次跨越两者各回调一次），不重复、不遗漏。
        /// pct 越界（&lt;0 或 &gt;1）会被钳制到 [0,1]。</summary>
        /// <returns>本次新触发的档位数（0/1），便于调用方计数。</returns>
        public int Update(float pct, Action<float> onThreshold, Action<RevealThresholdCrossedEvent> onCrossed)
        {
            float clamped = pct < 0f ? 0f : (pct > 1f ? 1f : pct); // 边界钳制
            int fired = 0;

            if (clamped >= T25 && !_fired25) { _fired25 = true; onThreshold?.Invoke(T25); onCrossed?.Invoke(new RevealThresholdCrossedEvent(T25, clamped)); fired++; }
            if (clamped >= T50 && !_fired50) { _fired50 = true; onThreshold?.Invoke(T50); onCrossed?.Invoke(new RevealThresholdCrossedEvent(T50, clamped)); fired++; }
            if (clamped >= T75 && !_fired75) { _fired75 = true; onThreshold?.Invoke(T75); onCrossed?.Invoke(new RevealThresholdCrossedEvent(T75, clamped)); fired++; }

            return fired;
        }

        public bool Fired25 => _fired25;
        public bool Fired50 => _fired50;
        public bool Fired75 => _fired75;

        /// <summary>已触发档位对应的 whisper_key（按 25/50/75 顺序），供 SessionRunner 选文案 key。</summary>
        public static string KeyOf(float threshold)
        {
            if (threshold >= T75) return RevealWhisperEvent.WHISPER_75;
            if (threshold >= T50) return RevealWhisperEvent.WHISPER_50;
            if (threshold >= T25) return RevealWhisperEvent.WHISPER_25;
            return null;
        }
    }
}
