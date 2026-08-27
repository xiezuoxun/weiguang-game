// DustBudget.cs — 打磨 Phase 7-C1：拂尘网格"表现层"分辨率封顶（纯 C#，可 EditMode 测）。
// 职责：把 CSV 拂尘网格 (w,h) 按设备降级上限 maxCells 等比缩到 ≤ maxCells 总格，
//       保证低端机/移动端减少采样密度、稳住帧率；逻辑层 reveal 仍按 CSV 全格（reveal_pct 阈值不受影响）。
// 不依赖 UnityEngine（保持 Runtime 层可被 EditMode 测试引用）。
using System;

namespace Weiguang.Runtime
{
    /// <summary>拂尘网格表现层分辨率预算：按设备档位的 maxDustCells 封顶采样密度。</summary>
    public static class DustBudget
    {
        /// <summary>
        /// 把 CSV 拂尘网格 (w,h) 按表现层采样上限 <paramref name="maxCells"/> 等比缩放，
        /// 返回封顶后的整数分辨率 (resW, resH)。
        /// 规则：
        ///   1. maxCells &lt;= 0 视为不约束，原样返回；
        ///   2. w/h 任一 &lt;= 0 视为非法，返回 (1,1)；
        ///   3. 原总格 ≤ maxCells 原样返回（已满足上限）；
        ///   4. 否则按 sqrt(maxCells/total) 等比缩，舍入后若仍略超上限则逐维 -1 钳回（保持最小 1×1）。
        /// 总格数必 ≤ maxCells，长宽比近似保留。
        /// </summary>
        public static (int resW, int resH) CapGrid(int maxCells, int w, int h)
        {
            if (maxCells <= 0) return (w, h);
            if (w <= 0 || h <= 0) return (1, 1);
            int total = w * h;
            if (total <= maxCells) return (w, h);

            double scale = Math.Sqrt((double)maxCells / total);
            int resW = Math.Max(1, (int)Math.Round(w * scale));
            int resH = Math.Max(1, (int)Math.Round(h * scale));

            // 舍入可能使 resW*resH 略超 maxCells，逐维递减钳回（优先缩较大边，减少形变）
            while (resW * resH > maxCells && (resW > 1 || resH > 1))
            {
                if (resW >= resH) resW--; else resH--;
            }
            return (resW, resH);
        }

        /// <summary>封顶后的总格数（供上层断言/日志）。</summary>
        public static int CappedTotal(int maxCells, int w, int h)
        {
            var (rw, rh) = CapGrid(maxCells, w, h);
            return rw * rh;
        }
    }
}
