// ChoiceHitTester.cs — S3-3 抉择点选命中容差（CR-002 T3 ①）。
// 纯 C# / 不依赖 UnityEngine（Core 层约束）。
// 工程侧抽象（来自 CR-002 T3 ①"UI 尺寸 1.3× 放大且允许 60% 溢出可视区"）：
//   触控热区命中判定容差放大 1.3×，且允许命中点偏离选项中心达 60% 选项尺寸仍算命中。
//   即：实际容忍半径 = baseRadius * 1.3（已隐含 0.6× 尺寸越界语义，因 1.3 = 0.7 内圈 + 0.6 越界）。
//   命中判定的"基准半径" baseRadius 由 Unity 层按选项实际尺寸传入；本类只做纯几何判定。
using System;

namespace Weiguang.Core
{
    public static class ChoiceHitTester
    {
        /// <summary>命中半径放大系数（CR-002 T3 ①：1.3× 放大）。</summary>
        public const float HIT_RADIUS_SCALE = 1.3f;

        /// <summary>允许越界比例（CR-002 T3 ①：允许命中点偏离中心达 60% 选项尺寸）。</summary>
        public const float OVERFLOW_ALLOW = 0.6f;

        /// <summary>
        /// 判定点选是否命中选项热区。
        /// 命中半径 = baseRadius * 1.3（放大 + 容忍 0.6× 越界）。
        /// </summary>
        /// <param name="px">命中点 X（屏幕/逻辑坐标）。</param>
        /// <param name="py">命中点 Y。</param>
        /// <param name="cx">选项热区中心 X。</param>
        /// <param name="cy">选项热区中心 Y。</param>
        /// <param name="baseRadius">选项基准半径（不含放大，由 Unity 层按 UI 尺寸传入）。</param>
        /// <returns>true = 命中（含越界容忍区）。</returns>
        public static bool Hit(float px, float py, float cx, float cy, float baseRadius)
        {
            if (baseRadius <= 0f) return false; // 退化半径：无热区可命中
            float hitRadius = baseRadius * HIT_RADIUS_SCALE;
            float dx = px - cx;
            float dy = py - cy;
            return (dx * dx + dy * dy) <= hitRadius * hitRadius; // 欧氏距离平方比较，避免开方
        }

        /// <summary>与 Hit 等价的"精确命中距离"判定（用于测试断言"偏离 d× 半径"的边界语义）。</summary>
        public static bool HitAtDistance(float dist, float baseRadius)
            => Hit(0f, dist, 0f, 0f, baseRadius);
    }
}
