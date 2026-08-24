// AssemblyTests.cs — Sprint 2 S2-1~S2-3 拼合解谜真实化单测（EditMode，纯 C# Core）。
// 覆盖：FragmentSlot 中带判定、X 容差、TryPlaceFragment 锁定语义、AssemblyBoard.AllLocked、边界（空盘）。
// 与 SmokeTests/DustRevealTests 互不冲突（独立文件、独立 fixture）；不改任何既有测试。
using NUnit.Framework;
using Weiguang.Core;

namespace Weiguang.Tests
{
    [TestFixture]
    public class AssemblyTests
    {
        // ── S2-1 FragmentSlot 中带判定：anchor_y=0.5 时 ──────────────
        [Test]
        public void Slot_MidBandHit_WhenPosYInBand()
        {
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "frag0");
            // posY=0.40 在中带 [0.33,0.67] 且 X 容差内 → 命中
            Assert.IsTrue(slot.IsWithinBand(0.5f, 0.40f), "posY=0.40 在中带内应命中");
        }

        [Test]
        public void Slot_NotHit_WhenPosYInUpperBand()
        {
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "frag0");
            // posY=0.20 在上带（<0.33）→ 不命中
            Assert.IsFalse(slot.IsWithinBand(0.5f, 0.20f), "posY=0.20 在上带不应命中");
        }

        [Test]
        public void Slot_NotHit_WhenPosYInLowerBand()
        {
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "frag0");
            // posY=0.80 在下带（>0.67）→ 不命中
            Assert.IsFalse(slot.IsWithinBand(0.5f, 0.80f), "posY=0.80 在下带不应命中");
        }

        // ── S2-1 X 容差：|posX-anchor_x|≤0.15 命中，>0.15 不命中 ────
        [Test]
        public void Slot_Hit_AtXThresholdBoundary()
        {
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "frag0");
            Assert.IsTrue(slot.IsWithinBand(0.65f, 0.50f), "|0.65-0.5|=0.15 边界内应命中");
            Assert.IsTrue(slot.IsWithinBand(0.35f, 0.50f), "|0.35-0.5|=0.15 边界内应命中");
        }

        [Test]
        public void Slot_NotHit_WhenXBeyondTolerance()
        {
            var slot = new FragmentSlot("slot0", 0.5f, 0.5f, "frag0");
            // |0.66-0.5|=0.16 > 0.15 → 不命中（即使 Y 在中带）
            Assert.IsFalse(slot.IsWithinBand(0.66f, 0.50f), "X 超容差不应命中（Y 仍在中带）");
        }

        // ── S2-1 TryPlaceFragment 命中后锁定；未命中不锁定 ──────────
        [Test]
        public void Board_PlaceHit_LocksFragmentAndFillsSlot()
        {
            var board = new AssemblyBoard();
            board.slots.Add(new FragmentSlot("slot0", 0.5f, 0.5f, "frag0"));
            var f = new Fragment { fragment_id = "frag0", item_id = "it", home_slot_id = "slot0" };
            board.fragments.Add(f);

            bool ok = board.TryPlaceFragment(f, 0.5f, 0.50f); // 落锚点：命中
            Assert.IsTrue(ok, "落中带且 X 容差内应返回 true");
            Assert.IsTrue(f.is_locked, "命中后 fragment.is_locked 必须为 true");
            Assert.IsTrue(board.slots[0].is_filled, "命中后 slot.is_filled 必须为 true");
        }

        [Test]
        public void Board_PlaceMiss_DoesNotLock()
        {
            var board = new AssemblyBoard();
            board.slots.Add(new FragmentSlot("slot0", 0.5f, 0.5f, "frag0"));
            var f = new Fragment { fragment_id = "frag0", item_id = "it", home_slot_id = "slot0" };
            board.fragments.Add(f);

            bool ok = board.TryPlaceFragment(f, 0.5f, 0.85f); // 下带：不命中
            Assert.IsFalse(ok, "落点不在归属带应返回 false");
            Assert.IsFalse(f.is_locked, "未命中不得锁定 fragment");
            Assert.IsFalse(board.slots[0].is_filled, "未命中不得填槽");
        }

        [Test]
        public void Board_PlaceMiss_WhenXBeyondTolerance_DoesNotLock()
        {
            var board = new AssemblyBoard();
            board.slots.Add(new FragmentSlot("slot0", 0.5f, 0.5f, "frag0"));
            var f = new Fragment { fragment_id = "frag0", item_id = "it", home_slot_id = "slot0" };
            board.fragments.Add(f);

            bool ok = board.TryPlaceFragment(f, 0.9f, 0.50f); // X 超容差：不命中
            Assert.IsFalse(ok, "X 超容差应返回 false");
            Assert.IsFalse(f.is_locked, "X 超容差不得锁定");
        }

        // ── S2-3 AllLocked ─────────────────────────────────────────
        [Test]
        public void Board_AllLocked_True_AfterAllThreePlaced()
        {
            var board = MakeBoard(3);
            // 依次命中 3 片（每片落到各自锚点）
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(board.TryPlaceFragment(board.fragments[i], board.slots[i].anchor_x, board.slots[i].anchor_y),
                    $"第 {i} 片应命中");
            Assert.IsTrue(board.AllLocked(), "3 片全锁后 AllLocked 应为 true");
        }

        [Test]
        public void Board_AllLocked_False_WhenOneMissing()
        {
            var board = MakeBoard(3);
            // 只命中前 2 片，漏第 3 片
            Assert.IsTrue(board.TryPlaceFragment(board.fragments[0], board.slots[0].anchor_x, board.slots[0].anchor_y));
            Assert.IsTrue(board.TryPlaceFragment(board.fragments[1], board.slots[1].anchor_x, board.slots[1].anchor_y));
            // 第 3 片故意不 place
            Assert.IsFalse(board.AllLocked(), "漏 1 片时 AllLocked 应为 false");
        }

        // ── S2-4 边界：fragment_count==0（调用方已跳过 ASSEMBLING）──
        // AssemblyBoard 空时 AllLocked 视为 true（无碎片可锁的构造守卫）。
        [Test]
        public void Board_AllLocked_True_WhenEmpty()
        {
            var board = new AssemblyBoard();
            Assert.IsTrue(board.AllLocked(), "空盘无碎片可锁，AllLocked 应视为 true（fragment_count==0 守卫语义）");
        }

        [Test]
        public void Board_PlaceOnEmptyBoard_ReturnsFalse()
        {
            var board = new AssemblyBoard();
            var f = new Fragment { fragment_id = "frag0" };
            Assert.IsFalse(board.TryPlaceFragment(f, 0.5f, 0.5f), "无归属槽位时 place 应返回 false（不锁定）");
            Assert.IsFalse(f.is_locked, "空盘不得锁定任何碎片");
        }

        // ── 事件常量自洽（S2-3 新增 EVT_ASSEMBLE_COMPLETE 不得破坏 C2）──
        [Test]
        public void AssembleComplete_EventConstant_IsSelfNamed()
            => Assert.AreEqual("EVT_ASSEMBLE_COMPLETE", GameEvents.EVT_ASSEMBLE_COMPLETE);

        // ── 辅助：构造 n 片碎片 + n 个 anchor 在 (0.5,0.5) 的槽位 ──
        static AssemblyBoard MakeBoard(int n)
        {
            var board = new AssemblyBoard();
            for (int i = 0; i < n; i++)
            {
                string fid = $"frag{i}";
                board.slots.Add(new FragmentSlot($"slot{i}", 0.5f, 0.5f, fid));
                board.fragments.Add(new Fragment { fragment_id = fid, item_id = "it", home_slot_id = $"slot{i}" });
            }
            return board;
        }
    }
}
