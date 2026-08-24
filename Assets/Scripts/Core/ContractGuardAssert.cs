// ContractGuardAssert.cs — 契约守卫的 fail-fast 包装（护栏 §5 / 控制清单 C1、C6）。
// 为什么单独一个文件：ContractGuard.Validate 返回错误清单（用于导入期"全量报错"，不中断遍历），
// 但运行时激活路径需要**硬拒绝**语义（choice_count 越界 → 拒绝激活，S4 ④）。
// 本文件只做包装，不改动 ContractGuard 任何既有判定逻辑。
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    /// <summary>契约违规异常：消息含全部违规项（每行一条），便于日志与测试断言。</summary>
    public class ContractViolationException : Exception
    {
        public readonly IReadOnlyList<string> Violations;

        public ContractViolationException(IList<string> violations)
            : base("契约违规（" + violations.Count + " 项）：\n  - " + string.Join("\n  - ", ToArray(violations)))
        {
            Violations = new List<string>(violations);
        }

        static string[] ToArray(IList<string> src)
        {
            var a = new string[src.Count];
            for (int i = 0; i < src.Count; i++) a[i] = src[i];
            return a;
        }
    }

    public static class ContractGuardAssert
    {
        /// <summary>校验 Commission，越界即抛（运行时激活门：宁崩在开发期，不静默截断进玩家存档）。</summary>
        public static Commission ThrowIfInvalid(Commission c)
        {
            var errs = ContractGuard.Validate(c);
            if (errs.Count > 0) throw new ContractViolationException(errs);
            return c;
        }

        /// <summary>校验 Client，越界即抛。</summary>
        public static Client ThrowIfInvalid(Client cl)
        {
            var errs = ContractGuard.Validate(cl);
            if (errs.Count > 0) throw new ContractViolationException(errs);
            return cl;
        }

        /// <summary>只判定是否合规（不抛），供 UI/导入期"跳过该条并告警"的软路径使用。</summary>
        public static bool IsValid(Commission c) => ContractGuard.Validate(c).Count == 0;
        public static bool IsValid(Client cl) => ContractGuard.Validate(cl).Count == 0;
    }
}
