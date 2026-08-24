// AssemblyBoard.cs — S2-1/S2-3 拼合盘：管理碎片与槽位，提供归属带落子与全锁判定。
// 纯 C#（Core），不依赖 UnityEngine；不持有事件总线（编排归 SessionRunner，经 EventBus 通信，ADR-005）。
// 职责边界：
//   - 管理 List<FragmentSlot> + List<Fragment>
//   - TryPlaceFragment：把碎片落到某归一化坐标，命中其 home_slot 归属带则锁定（is_locked=true + slot.is_filled=true）
//   - AllLocked：所有碎片 is_locked 时返回 true（空盘按"无碎片可锁"视为已完成，便于 fragment_count==0 守卫）
using System;
using System.Collections.Generic;

namespace Weiguang.Core
{
    public class AssemblyBoard
    {
        public readonly List<FragmentSlot> slots = new List<FragmentSlot>();
        public readonly List<Fragment> fragments = new List<Fragment>();

        /// <summary>按碎片 id 找其归属槽位（home_slot_id 与 slot.slot_id 对应）。</summary>
        FragmentSlot FindHomeSlot(string fragmentId)
        {
            foreach (var s in slots)
                if (s.accepts_fragment_id == fragmentId) return s;
            return null;
        }

        /// <summary>S2-1 落子：把碎片 f 置于归一化坐标 (posX,posY)。
        /// 仅当落点落入其 home_slot 归属带时锁定——fragment.is_locked=true 且 slot.is_filled=true，返回 true；
        /// 否则不锁定、不填槽，返回 false。</summary>
        public bool TryPlaceFragment(Fragment f, float posX, float posY)
        {
            if (f == null) return false;
            var slot = FindHomeSlot(f.fragment_id);
            if (slot == null) return false;                 // 无归属槽位：不锁定
            if (slot.is_filled) return false;               // 槽位已填：不可重复落（幂等守卫）
            if (!slot.IsWithinBand(posX, posY)) return false; // 未落中带：不锁定

            f.current_pos_x = posX;
            f.current_pos_y = posY;
            f.is_locked = true;
            slot.is_filled = true;
            return true;
        }

        /// <summary>S2-3 拼合完成判定：所有碎片 is_locked==true 时返回 true。
        /// 空盘（无碎片）视为已完成（true），用于 fragment_count==0 时不误判为未完成；
        /// 调用方对 fragment_count==0 已跳过 ASSEMBLING，此处为构造守卫的兜底语义。</summary>
        public bool AllLocked()
        {
            if (fragments.Count == 0) return true;
            foreach (var f in fragments)
                if (!f.is_locked) return false;
            return true;
        }
    }
}
