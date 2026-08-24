// ChoiceWordingGuard.cs — S3-4 wording 字数硬约束（CR-002 T3 ②）。
// 纯 C# / 不依赖 UnityEngine（Core 层约束）。
// 约束：全角字 ≤26（当选项数=3 时）/ ≤39（当选项数=2 时）；构造/校验时越界即抛（fail-fast）。
// 计数规则（简单、可确定、与文案工具对齐）：
//   全角字（中文/全角标点/全角符号）计 1；半角字母数字与半角标点计 0.5。
//   用 System.Globalization.CharUnicodeInfo 判定"是否全角宽度"；
//   简化规则：非半角字母数字/非半角空白即按全角计 1，否则计 0.5。
using System;
using System.Globalization;
using Weiguang.Core; // ContractViolationException

namespace Weiguang.Core
{
    public static class ChoiceWordingGuard
    {
        public const int MAX_WORDING_3_OPTIONS = 26; // 选项数=3 时全角字上限
        public const int MAX_WORDING_2_OPTIONS = 39; // 选项数=2 时全角字上限

        /// <summary>按选项数返回 wording 全角字上限（CR-002 T3 ②）。</summary>
        public static int MaxWording(int optionCount)
            => optionCount <= 2 ? MAX_WORDING_2_OPTIONS : MAX_WORDING_3_OPTIONS;

        /// <summary>全角字计数：全角字=1，半角字母数字=0.5（其余半角符号按 0.5）。</summary>
        public static float CountWording(string wording)
        {
            if (string.IsNullOrEmpty(wording)) return 0f;
            float total = 0f;
            foreach (char ch in wording)
            {
                // 半角字母/数字 = 0.5；其余（中文、全角标点、全角符号、半角标点）按 1（简单规则：半角字母数字以外均为 1）
                bool isHalfwidthLetterOrDigit =
                    (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
                total += isHalfwidthLetterOrDigit ? 0.5f : 1f;
            }
            return total;
        }

        /// <summary>
        /// 校验 wording 是否超字数（CR-002 T3 ②）。越界抛 ContractViolationException（fail-fast）。
        /// </summary>
        /// <param name="wording">措辞文本。</param>
        /// <param name="optionCount">该抉择点的选项数（2 或 3）。</param>
        public static void ValidateWording(string wording, int optionCount)
        {
            int limit = MaxWording(optionCount);
            float used = CountWording(wording);
            if (used > limit)
            {
                throw new ContractViolationException(new[]
                {
                    $"wording 超字数：used={used} > limit={limit}（optionCount={optionCount}）「{wording}」"
                });
            }
        }
    }
}
