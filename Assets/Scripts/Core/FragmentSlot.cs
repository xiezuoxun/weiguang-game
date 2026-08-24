// FragmentSlot.cs — S2-1 拼合解谜槽位模型（CR-002 T2 槽位落中带语义）。
// 纯 C#（Core），不依赖 UnityEngine；可被 EditMode 单测与未来任意运行时复用。
// 槽位锚点 anchor 为归一化屏坐标（0–1，原点左上），由构造/校验保证落于中带 [0.33,0.67]。
// 归属带判定（S2-1）：碎片需落入其 home_slot 的归属带——中带（Y∈[0.33,0.67]）+ X 接近锚点（|posX-anchor_x|≤0.15）——才锁定。
using System;

namespace Weiguang.Core
{
    [Serializable]
    public class FragmentSlot
    {
        // ── 归属带常量（与 CR-002 T2 一致；提取为常量避免散落字面量，便于单测与调参）──
        /// <summary>中带 Y 下界（屏高 33%）。</summary>
        public const float MID_BAND_MIN = 0.33f;
        /// <summary>中带 Y 上界（屏高 67%）。</summary>
        public const float MID_BAND_MAX = 0.67f;
        /// <summary>X 接近锚点的容差（|posX-anchor_x|≤此值视为命中槽位）。</summary>
        public const float X_TOLERANCE = 0.15f;

        public string slot_id;
        public float anchor_x;   // 归一化 [0,1]，由构造保证落中带范围外仍可设，但合法槽位应∈[0,1]
        public float anchor_y;   // 归一化 [0,1]，中带 [0.33,0.67] 由构造/校验保证
        public string accepts_fragment_id;
        public bool is_filled;

        public FragmentSlot() { }

        public FragmentSlot(string slot_id, float anchor_x, float anchor_y, string accepts_fragment_id)
        {
            this.slot_id = slot_id;
            this.anchor_x = anchor_x;
            this.anchor_y = anchor_y;
            this.accepts_fragment_id = accepts_fragment_id;
            this.is_filled = false;
        }

        /// <summary>S2-1 归属带判定：落点是否落入本槽位归属带。
        /// 中带判定（Y∈[MID_BAND_MIN,MID_BAND_MAX]）+ X 接近锚点（|posX-anchor_x|≤X_TOLERANCE）。
        /// 纯几何判定，不修改任何状态；命中与否由调用方决定锁定。</summary>
        public bool IsWithinBand(float posX, float posY)
        {
            bool inMidBand = posY >= MID_BAND_MIN && posY <= MID_BAND_MAX;
            bool nearAnchorX = Math.Abs(posX - anchor_x) <= X_TOLERANCE;
            return inMidBand && nearAnchorX;
        }
    }
}
