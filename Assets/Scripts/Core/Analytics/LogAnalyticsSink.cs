// LogAnalyticsSink.cs — Phase 8-A 默认 sink：纯 C#，把 Track 打到 Console/Debug（EditMode 可测、无 Unity 依赖）。
// 真实设备可换成 UnityAnalyticsSink（Runtime 层 MonoBehaviour）。实现须吞异常——埋点绝不能拖垮玩法主链路。
using System;
using System.Collections.Generic;

namespace Weiguang.Core.Analytics
{
    /// <summary>默认埋点出口：输出到 Console（CI/Editor 可读、可断言）。
    /// Output 可注入（默认 Console.WriteLine），便于 Unity 层桥接 Debug.Log 或测试重定向。</summary>
    public class LogAnalyticsSink : IAnalyticsSink
    {
        /// <summary>输出委托（默认 Console.WriteLine）。宿主可替换为 Debug.Log 桥接或测试录音器。</summary>
        public Action<string> Output = Console.WriteLine;

        public void Track(string eventName, IDictionary<string, object> props)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("[Analytics] ").Append(eventName);
                if (props != null)
                {
                    foreach (var kv in props)
                        sb.Append(" | ").Append(kv.Key).Append('=').Append(kv.Value);
                }
                Output?.Invoke(sb.ToString());
            }
            catch (Exception e)
            {
                // 埋点失败不得影响玩法：吞掉并记录，绝不上抛。
                try { Output?.Invoke("[Analytics] sink error: " + e.Message); } catch { }
            }
        }
    }
}
