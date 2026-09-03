// IAnalyticsSink.cs — Phase 8-A 数据埋点：sink 抽象（纯 C#，无 UnityEngine 依赖）。
// 分层：Core 层。任何想接收埋点事件的出口（日志 / Unity Analytics / 自有后端）都实现此接口。
// 这样埋点核心（AnalyticsTracker）可完全脱离 Unity 在 EditMode 下编译与测试（ADR-005 分层约束）。
using System.Collections.Generic;

namespace Weiguang.Core.Analytics
{
    /// <summary>
    /// 埋点事件出口。Track 的 props 约定为可序列化扁平键值对：值类型限
    /// string / int / long / float / double / bool，或嵌套 IDictionary（用于聚合结构如抉择分布、漏斗）。
    /// 实现方必须吞掉内部异常——埋点失败绝不允许拖垮玩法主链路。
    /// </summary>
    public interface IAnalyticsSink
    {
        /// <summary>上报一条命名事件及属性。props 允许为 null（表示无附加属性）。</summary>
        void Track(string eventName, IDictionary<string, object> props);
    }
}
