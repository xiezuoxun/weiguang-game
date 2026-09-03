// UnityAnalyticsSink.cs — Phase 8-A 设备 sink：MonoBehaviour 实现 IAnalyticsSink，转发到 Unity Analytics。
// 仅在 Runtime 层（可引用 UnityEngine）。真实上报用 #if UNITY_ANALYTICS 守卫；未定义时退化为 Debug.Log，
// 保证 Runtime asmdef 在无 Analytics 包环境下也能编译、可本地验证（ADR-005 分层：Unity 依赖只在此处）。
using System;
using System.Collections.Generic;
using UnityEngine;
using Weiguang.Core.Analytics;

namespace Weiguang.Runtime.Analytics
{
    /// <summary>设备端埋点出口：把 Track 转发到 UnityEngine.Analytics。
    /// 未接入 Analytics 包（UNITY_ANALYTICS 未定义）时退化为 Debug.Log，保证可编译、可真机排查。</summary>
    public class UnityAnalyticsSink : MonoBehaviour, IAnalyticsSink
    {
        [Tooltip("未定义 UNITY_ANALYTICS 时是否仍输出 Debug.Log（便于真机排查）")]
        public bool logFallback = true;

        public void Track(string eventName, IDictionary<string, object> props)
        {
            // 嵌套结构（如抉择分布 / 漏斗）转为 JSON 字符串，保证上报给 Unity Analytics 的是扁平原始值。
            var flat = Flatten(props);
#if UNITY_ANALYTICS
            try { UnityEngine.Analytics.Analytics.CustomEvent(eventName, flat); }
            catch (Exception e) { if (logFallback) Debug.LogWarning($"[UnityAnalyticsSink] CustomEvent 失败: {e.Message}"); }
#else
            if (logFallback) Debug.Log($"[Analytics] {eventName} {ToJson(props)}");
#endif
        }

        // 把嵌套 IDictionary 拍平成"键=JSON 字符串"的扁平字典，满足 Unity Analytics 的原始值约束。
        static Dictionary<string, object> Flatten(IDictionary<string, object> props)
        {
            var outp = new Dictionary<string, object>();
            if (props == null) return outp;
            foreach (var kv in props)
            {
                outp[kv.Key] = (kv.Value is IDictionary<string, object> nested) ? (object)ToJson(nested) : kv.Value;
            }
            return outp;
        }

        // 极简 JSON 序列化（仅用于嵌套结构兜底输出，避免引入额外依赖）。
        static string ToJson(object obj)
        {
            if (obj is IDictionary<string, object> d)
            {
                var parts = new List<string>();
                foreach (var kv in d) parts.Add($"\"{kv.Key}\":{JsonScalar(kv.Value)}");
                return "{" + string.Join(",", parts) + "}";
            }
            return JsonScalar(obj);
        }

        static string JsonScalar(object v)
        {
            if (v == null) return "null";
            if (v is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (v is bool b) return b ? "true" : "false";
            return v.ToString();
        }
    }
}
