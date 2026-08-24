// OnboardingHints.cs — 打磨阶段新增：四核心动词的"首见引导文案"轻量常量（纯文本，无 UI 依赖）。
// 纯 C#（Core），不依赖 UnityEngine；仅暴露字符串供后续 Runtime（首启动引导层）消费，工程侧不接 UI。
// 文案为工程占位提示，design-strategist 后续可覆盖（见下方"待设计确认"缺口标注）。
using System.Collections.Generic;

namespace Weiguang.Core
{
    /// <summary>四核心动词首见引导文案：在玩家首次进入对应环节时由 Runtime 层选取展示（不接 UI，只供字符串）。
    /// 每个动词给"主提示 + 副提示"两行，便于上层排版；文案均≤20 字，安全进包。</summary>
    public static class OnboardingHints
    {
        // ── S1 拂尘（Reveal）：轻拂尘埃，唤回微光 ──
        public const string REVEAL_TITLE = "拂尘";
        public const string REVEAL_HINT = "轻拂尘埃，唤回微光";

        // ── S2 拼合（Assemble）：碎片归位，拼起旧忆 ──
        public const string ASSEMBLE_TITLE = "拼合";
        public const string ASSEMBLE_HINT = "将碎片拖回原位，拼起旧忆";

        // ── S3 抉择（Choose）：落子之前，先听心声 ──
        public const string CHOOSE_TITLE = "抉择";
        public const string CHOOSE_HINT = "停一停，听一听自己的心声";

        // ── S5 归档（Archive）：尘埃落定，微光归处 ──
        public const string ARCHIVE_TITLE = "归档";
        public const string ARCHIVE_HINT = "尘埃落定，微光终有归处";

        /// <summary>按动词 key（reveal/assemble/choose/archive）取 (title, hint) 二元组；未知 key 返回空串对。</summary>
        public static KeyValuePair<string, string> Of(string verb)
        {
            switch (verb)
            {
                case "reveal":   return new KeyValuePair<string, string>(REVEAL_TITLE, REVEAL_HINT);
                case "assemble": return new KeyValuePair<string, string>(ASSEMBLE_TITLE, ASSEMBLE_HINT);
                case "choose":   return new KeyValuePair<string, string>(CHOOSE_TITLE, CHOOSE_HINT);
                case "archive":  return new KeyValuePair<string, string>(ARCHIVE_TITLE, ARCHIVE_HINT);
                default:         return new KeyValuePair<string, string>(string.Empty, string.Empty);
            }
        }
    }
}
