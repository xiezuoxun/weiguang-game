// CommissionStateMachine.cs — S4 核心循环状态机（GDD 04 §2）。
// 合法迁移表 + 越界拒绝；fragment_count==0 允许 REVEALING→CHOOSING 跳过 ASSEMBLING。
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    public class CommissionStateMachine
    {
        readonly EventBus _bus;

        // 合法迁移表（值：目标 phase 集合）
        static readonly Dictionary<CommissionPhase, CommissionPhase[]> Legal =
            new Dictionary<CommissionPhase, CommissionPhase[]>
            {
                { CommissionPhase.Idle,       new[]{ CommissionPhase.Received } },
                { CommissionPhase.Received,   new[]{ CommissionPhase.Examining } },
                { CommissionPhase.Examining,  new[]{ CommissionPhase.Revealing } },
                // 特例：fragment_count==0 跳过 ASSEMBLING（S4 ②-3）
                { CommissionPhase.Revealing,  new[]{ CommissionPhase.Assembling, CommissionPhase.Choosing } },
                { CommissionPhase.Assembling, new[]{ CommissionPhase.Choosing } },
                { CommissionPhase.Choosing,   new[]{ CommissionPhase.Delivering } },
                { CommissionPhase.Delivering, new[]{ CommissionPhase.Archived } },
                { CommissionPhase.Archived,   new CommissionPhase[0] },
            };

        public CommissionStateMachine(EventBus bus) { _bus = bus; }

        /// <summary>AdvancePhase(id, from, to)。非法迁移返回 false 并告警（不静默，护栏 §5）。</summary>
        public bool AdvancePhase(Commission c, CommissionPhase to)
        {
            if (c == null) { Warn("commission 为 null"); return false; }
            var from = c.phase;
            if (!Legal.TryGetValue(from, out var targets)) { Warn($"{c.commission_id}: {from} 无出边"); return false; }
            if (Array.IndexOf(targets, to) < 0) { Warn($"{c.commission_id}: 非法迁移 {from}→{to}"); return false; }
            // 跳过校验：只有 fragment_count==0 才允许跳 ASSEMBLING
            if (from == CommissionPhase.Revealing && to == CommissionPhase.Choosing && c.fragment_count > 0)
            { Warn($"{c.commission_id}: fragment_count={c.fragment_count}>0，不得跳过 ASSEMBLING"); return false; }
            c.phase = to;
            _bus.Publish(GameEvents.EVT_PHASE_CHANGED, c);
            return true;
        }

        /// <summary>phase → SaveNode 映射（S6 断点，契约 §2.1）。</summary>
        public static SaveNode NodeOf(CommissionPhase p)
        {
            switch (p)
            {
                case CommissionPhase.Received:   return SaveNode.NodeReceive;
                case CommissionPhase.Examining:  return SaveNode.NodeExamine;
                case CommissionPhase.Revealing:  return SaveNode.NodeReveal;
                case CommissionPhase.Assembling: return SaveNode.NodeAssemble;
                case CommissionPhase.Choosing:   return SaveNode.NodeChoose;
                case CommissionPhase.Delivering: return SaveNode.NodeDeliver;
                case CommissionPhase.Archived:   return SaveNode.NodeArchive;
                default: return SaveNode.NodeReceive;
            }
        }

        void Warn(string msg)
        {
            _bus.Publish(GameEvents.EVT_CONTRACT_WARN, msg); // C2：常量化，不用字面量
            OnWarn?.Invoke(msg);
        }
        public event Action<string> OnWarn;
    }
}
