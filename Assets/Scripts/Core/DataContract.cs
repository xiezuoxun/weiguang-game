// DataContract.cs — 共享数据契约（唯一真相源：design/gdd/00-系统拆解与依赖排序.md §2）
// 纯 C# 域模型：不依赖 UnityEngine，可被 EditMode 单测与未来任意运行时复用。
// 命名/字段/单位与契约一一对应，禁止别名（护栏 §5）。
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    // ── 枚举（契约 §2.1）──────────────────────────────────────────
    public enum ItemType { Paper, Clock, Letter, Ornament, Other }

    public enum Material { Wood, Paper, Glass, Dust } // 对应美术圣经 §4 四肌理

    public enum CommissionPhase
    {
        Idle, Received, Examining, Revealing, Assembling, Choosing, Delivering, Archived
    }

    public enum EndingTag { Truth, Omit, Reframe } // R3 单层措辞三态

    public enum SaveNode
    {
        NodeReceive, NodeExamine, NodeReveal, NodeAssemble, NodeChoose, NodeDeliver, NodeArchive
    }

    // ── 实体（契约 §2.2）──────────────────────────────────────────
    [Serializable]
    public class Commission
    {
        public string commission_id;
        public string client_id;
        public string item_id;
        public int chapter_index;
        public CommissionPhase phase;
        public float session_soft_budget_min;   // ∈[5,10]
        public bool is_daily;
        public float reveal_threshold;          // ∈[0,1]，默认 0.85，S4 唯一写入方
        public int fragment_count;              // ∈[0,6]
        public int choice_count;                // ∈[2,3]
        public int ending_variants;             // ≥2
        public bool is_mainplot;
    }

    [Serializable]
    public class DustGrid // MemoryItem.dust_grid:Grid<cell>
    {
        public int width;
        public int height;
        public bool[] revealed; // length = width*height
        public int RevealedCount() { int n = 0; if (revealed == null) return 0; foreach (var r in revealed) if (r) n++; return n; }
        public int TotalCells() { return width * height; }
        public float RevealPct() { int t = TotalCells(); return t == 0 ? 0f : (float)RevealedCount() / t; }

        /// <summary>S1-1 拂尘真实化：按 (x,y) 拂开一格。坐标越界或已拂开则忽略（幂等）。</summary>
        public bool RevealCell(int x, int y)
        {
            if (revealed == null) return false;
            if (x < 0 || y < 0 || x >= width || y >= height) return false; // 钳制越界，不静默崩溃
            int idx = y * width + x;
            if (idx < 0 || idx >= revealed.Length) return false;
            if (revealed[idx]) return false; // 已拂开的格重复拂无效（手势抖动安全）
            revealed[idx] = true;
            return true;
        }

        /// <summary>S1-1 辅助驱动：按网格扫描顺序逐格拂开（真实手势输入在 Unity 层后续接，本 PR 用确定性模拟）。</summary>
        public void RevealAll()
        {
            int t = TotalCells();
            for (int i = 0; i < t; i++) if (revealed != null && i < revealed.Length) revealed[i] = true;
        }
    }

    [Serializable]
    public class MemoryItem
    {
        public string item_id;
        public string display_name;
        public ItemType item_type;
        public Material material;
        public string client_id;
        public DustGrid dust_grid;
        public bool detail_unlocked; // 端详解锁，写入方 S1（C3 补丁），默认 false
        public bool is_mainplot;
    }

    [Serializable]
    public class RevealState
    {
        public string item_id;
        public int revealed_cells;
        public int total_cells;
        public float reveal_pct; // ∈[0,1]
    }

    [Serializable]
    public class Fragment
    {
        public string fragment_id;
        public string item_id;
        public string home_slot_id;
        public float current_pos_x;
        public float current_pos_y;
        public float rotation; // ∈[0,360)
        public bool is_locked;
    }

    [Serializable]
    public class ChoiceOption
    {
        public string option_id;
        public string wording;
        public float truth_level;          // ∈[0,1]
        public EndingTag ending_tag;
        public string client_reaction;
        public float sdt_autonomy_weight;  // ∈[0,1]
    }

    [Serializable]
    public class ChoiceNode
    {
        public string node_id;
        public string commission_id;
        public List<ChoiceOption> options = new List<ChoiceOption>();
        public string selected_option_id; // null = 未选
    }

    [Serializable]
    public class Ending
    {
        public string ending_id;
        public string commission_id;
        public string title;
        public string description;
        public string emotion_arc_stage;
    }

    [Serializable]
    public class CodexEntry
    {
        public string entry_id;
        public string commission_id;
        public string item_id;
        public string client_id;
        public long lit_timestamp; // epoch_ms
        public EndingTag ending_tag;
        public int timeline_order;
        public bool is_mainplot;
        public string quote;
    }

    [Serializable]
    public class Client
    {
        public string client_id;
        public string display_name;
        public int relationship_level; // ∈[0,5]
        public int visit_count;
        public int mainplot_progress; // ∈[0,N]
    }

    [Serializable]
    public class SaveSnapshot // S6 契约
    {
        public int version;
        public Commission active_commission; // null = 无活跃委托
        public List<RevealState> reveal_states = new List<RevealState>();
        public List<Fragment> fragment_states = new List<Fragment>();
        public List<ChoiceNode> choice_states = new List<ChoiceNode>();
        public List<CodexEntry> codex = new List<CodexEntry>();
        public List<Client> clients = new List<Client>();
        public List<MemoryItem> items = new List<MemoryItem>();
        public SaveNode last_node;
        public string saved_at_iso;
    }

    // ── 契约边界校验（护栏 §5：越界拒/截断并告警，不静默）────────
    public static class ContractGuard
    {
        public static List<string> Validate(Commission c)
        {
            var errs = new List<string>();
            if (c == null) { errs.Add("commission 为 null"); return errs; }
            if (c.session_soft_budget_min < 5f || c.session_soft_budget_min > 10f)
                errs.Add($"{c.commission_id}: session_soft_budget_min={c.session_soft_budget_min} 越界 [5,10]");
            if (c.reveal_threshold < 0f || c.reveal_threshold > 1f)
                errs.Add($"{c.commission_id}: reveal_threshold={c.reveal_threshold} 越界 [0,1]");
            if (c.fragment_count < 0 || c.fragment_count > 6)
                errs.Add($"{c.commission_id}: fragment_count={c.fragment_count} 越界 [0,6]");
            if (c.choice_count < 2 || c.choice_count > 3)
                errs.Add($"{c.commission_id}: choice_count={c.choice_count} 越界 [2,3]（拒绝激活）");
            if (c.ending_variants < 2)
                errs.Add($"{c.commission_id}: ending_variants={c.ending_variants} < 2");
            return errs;
        }

        public static List<string> Validate(Client cl)
        {
            var errs = new List<string>();
            if (cl == null) { errs.Add("client 为 null"); return errs; }
            if (cl.relationship_level < 0 || cl.relationship_level > 5)
                errs.Add($"{cl.client_id}: relationship_level={cl.relationship_level} 越界 [0,5]");
            return errs;
        }
    }
}
