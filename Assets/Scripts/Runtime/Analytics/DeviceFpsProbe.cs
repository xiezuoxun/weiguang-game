// DeviceFpsProbe.cs — Phase 8-B 真机帧率补录探针（Runtime/MonoBehaviour）。
// 每秒基于 Time.deltaTime 累加实测 FPS，按采样窗口（默认 60s）汇总 avg/min fps，
// 经 IAnalyticsSink.Track("device_fps", {...}) 上报（含设备型号 SystemInfo.deviceModel、quality tier）。
// 仅供真机测量用；沙箱无 GPU，本探针的 FPS 数字不足为凭，必须在真机执行（见 production/phase8-device-fps.md）。
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core.Analytics;

namespace Weiguang.Runtime.Analytics
{
    /// <summary>真机帧率探针。挂到 GameBootstrap 同 GameObject，由 GameBootstrap 注入 sink + quality 并启停。</summary>
    public class DeviceFpsProbe : MonoBehaviour
    {
        /// <summary>埋点出口（由 GameBootstrap 注入，通常即与 AnalyticsTracker 同一 sink）。</summary>
        public IAnalyticsSink sink;

        /// <summary>运行时画质档（由 GameBootstrap 注入），用于上报 quality tier。</summary>
        public RuntimeQuality quality;

        /// <summary>采样窗口（秒）。到点汇总一次并上报，随后重置继续下一窗口。</summary>
        public float sampleWindowSec = 60f;

        /// <summary>是否在屏幕上用 OnGUI 显示实时 avg/min fps（便于真机手动读数）。</summary>
        public bool showOnScreen = true;

        float _elapsed;
        int _frames;
        float _minFps = float.MaxValue;
        float _lastAvg;
        float _lastMin;

        /// <summary>由 GameBootstrap 在生命周期内调用以启动一次采样。</summary>
        public void StartSampling() { ResetWindow(); enabled = true; }

        /// <summary>由 GameBootstrap 在生命周期结束时调用以停止并补报最后一次窗口。</summary>
        public void StopSampling()
        {
            enabled = false;
            if (_frames > 0) Report();
        }

        void ResetWindow() { _elapsed = 0f; _frames = 0; _minFps = float.MaxValue; }

        void Update()
        {
            if (Time.deltaTime <= 0f) return;
            float inst = 1f / Time.deltaTime;
            _frames++;
            _elapsed += Time.deltaTime;
            if (inst < _minFps) _minFps = inst;

            if (_elapsed >= sampleWindowSec)
            {
                Report();
                ResetWindow();
            }
        }

        void Report()
        {
            if (sink == null) return;
            float avg = _frames > 0 ? _frames / _elapsed : 0f;
            _lastAvg = avg;
            _lastMin = _minFps == float.MaxValue ? 0f : _minFps;
            sink.Track("device_fps", new Dictionary<string, object>
            {
                ["avg_fps"] = System.Math.Round(avg, 2),
                ["min_fps"] = System.Math.Round(_lastMin, 2),
                ["device_model"] = SystemInfo.deviceModel,
                ["quality_tier"] = QualityTierName(quality),
                ["sample_sec"] = sampleWindowSec,
                ["frames"] = _frames,
                ["unity_version"] = Application.unityVersion,
            });
        }

        /// <summary>把 RuntimeQuality 映射为一个稳定的 tier 名（供 analytics 分组：low/mid/high）。</summary>
        public static string QualityTierName(RuntimeQuality q)
        {
            if (q == null) return "unknown";
            if (!q.enableGlowShader && !q.enableChoiceShader) return "low";
            if (!q.enableGlowShader || !q.enableChoiceShader) return "mid";
            return "high";
        }

        void OnGUI()
        {
            if (!showOnScreen || _frames == 0) return;
            float live = _elapsed > 0f ? _frames / _elapsed : 0f;
            var txt = $"FPS live={live:F1} avg@win={_lastAvg:F1} min@win={_lastMin:F1} ({QualityTierName(quality)})";
            GUI.Label(new Rect(16, 16, 460, 24), txt);
        }
    }
}
