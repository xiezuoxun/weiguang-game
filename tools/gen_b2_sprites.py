#!/usr/bin/env python3
"""gen_b2_sprites.py — B2 拼合槽位底图 + 碎片 Sprite 程序化生成。

产出：
  5 底图（Slots/it_*_board.png）: 槽位引导底图，透明背景 + 槽位轮廓
  13 碎片（Fragments/fr_001~fr_013.png）: 256×256 透明碎片

锚点严格按 fragments.csv 归一化坐标（原点左上），归属带 Y∈[0.33,0.67]。
"""
import math
import os
import csv
from PIL import Image, ImageDraw, ImageFilter

GAME_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
OUTPUT_DIR = os.path.join(GAME_DIR, "Assets", "Resources")

# ── 物件元数据（来自 items.csv）──
ITEMS = {
    "it_watch":    {"display": "停摆的怀表",   "type": "Clock",    "material": "Glass", "grid": (8, 8),  "fc": 1, "board_size": 512},
    "it_photo":    {"display": "褪色的全家福", "type": "Paper",    "material": "Paper", "grid": (10, 10),"fc": 2, "board_size": 512},
    "it_letter":   {"display": "未寄出的信",   "type": "Letter",   "material": "Paper", "grid": (10, 12),"fc": 2, "board_size": 512},
    "it_ornament": {"display": "断裂的银簪",   "type": "Ornament", "material": "Wood",  "grid": (8, 8),  "fc": 4, "board_size": 768},
    "it_mirror":   {"display": "蒙尘的梳妆镜", "type": "Other",    "material": "Glass", "grid": (12, 10),"fc": 4, "board_size": 768},
}

# ── 碎片锚点（来自 fragments.csv）──
FRAGMENTS = [
    # fragment_id, item_id, slot_id, slot_index, anchor_x, anchor_y
    ("fr_001", "it_watch",    "slot0", 0, 0.50, 0.50),
    ("fr_002", "it_photo",    "slot0", 0, 0.33, 0.45),
    ("fr_003", "it_photo",    "slot1", 1, 0.67, 0.55),
    ("fr_004", "it_letter",   "slot0", 0, 0.33, 0.40),
    ("fr_005", "it_letter",   "slot1", 1, 0.67, 0.60),
    ("fr_006", "it_ornament", "slot0", 0, 0.20, 0.50),
    ("fr_007", "it_ornament", "slot1", 1, 0.40, 0.42),
    ("fr_008", "it_ornament", "slot2", 2, 0.60, 0.58),
    ("fr_009", "it_ornament", "slot3", 3, 0.80, 0.46),
    ("fr_010", "it_mirror",   "slot0", 0, 0.20, 0.45),
    ("fr_011", "it_mirror",   "slot1", 1, 0.40, 0.55),
    ("fr_012", "it_mirror",   "slot2", 2, 0.60, 0.48),
    ("fr_013", "it_mirror",   "slot3", 3, 0.80, 0.52),
]

# ── 材质配色（对齐 art-spec 四肌理）──
MATERIAL_COLORS = {
    "Glass":   (180, 210, 230),  # 玻璃：冷青蓝
    "Paper":   (210, 195, 165),  # 纸张：暖黄褐
    "Wood":    (165, 130,  90),  # 木头：棕褐
    "Dust":    (158, 148, 136),  # 积尘：灰褐
}

ITEM_TYPE_SHAPES = {
    "Clock":    "circle",
    "Paper":    "rect",
    "Letter":   "rect",
    "Ornament": "diamond",
    "Other":    "circle",
}


def gen_slot_board(item_id, item_meta, fragments):
    """生成槽位引导底图：透明背景 + 物件轮廓 + 槽位热区虚线框 + 锚点标记。"""
    size = item_meta["board_size"]
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    mat_color = MATERIAL_COLORS.get(item_meta["material"], (180, 180, 180))
    shape = ITEM_TYPE_SHAPES.get(item_meta["type"], "rect")

    # 物件外轮廓（半透明底色）
    margin = size // 10
    if shape == "circle":
        draw.ellipse([margin, margin, size - margin, size - margin],
                      fill=(*mat_color, 40), outline=(*mat_color, 120), width=3)
    elif shape == "diamond":
        cx = size // 2
        pts = [(cx, margin), (size - margin, cx), (cx, size - margin), (margin, cx)]
        draw.polygon(pts, fill=(*mat_color, 40), outline=(*mat_color, 120), width=3)
    else:
        draw.rectangle([margin, margin, size - margin, size - margin],
                        fill=(*mat_color, 40), outline=(*mat_color, 120), width=3)

    # 槽位热区：每个碎片的归属带位置（归一化→像素，原点左上）
    slot_radius = size // 12
    for fid, iid, sid, sidx, ax, ay in fragments:
        if iid != item_id:
            continue
        px = int(ax * size)
        py = int(ay * size)

        # 热区虚线圆（归属带视觉提示）
        draw.ellipse([px - slot_radius, py - slot_radius,
                       px + slot_radius, py + slot_radius],
                      outline=(*mat_color, 180), width=2)
        # 内圈实线（槽位中心）
        draw.ellipse([px - slot_radius // 3, py - slot_radius // 3,
                       px + slot_radius // 3, py + slot_radius // 3],
                      fill=(*mat_color, 100))

        # 锚点十字标记
        cl = slot_radius // 2
        draw.line([(px - cl, py), (px + cl, py)], fill=(*mat_color, 200), width=1)
        draw.line([(px, py - cl), (px, py + cl)], fill=(*mat_color, 200), width=1)

    # 物件名称（底部小字）
    try:
        from PIL import ImageFont
        font = ImageFont.load_default()
    except:
        font = None
    label = item_meta["display"]
    draw.text((size // 2 - len(label) * 6, size - 30), label,
              fill=(*mat_color, 150), font=font)

    return img


def gen_fragment_sprite(frag_id, item_id, anchor_x, anchor_y, item_meta):
    """生成单张碎片 Sprite：256×256 透明 PNG，带物件材质色和不规则边缘。"""
    size = 256
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    mat_color = MATERIAL_COLORS.get(item_meta["material"], (180, 180, 180))
    shape = ITEM_TYPE_SHAPES.get(item_meta["type"], "rect")

    # 碎片本体：不规则多边形（模拟破损边缘）
    margin = 40
    if shape == "circle":
        # 圆形碎片（怀表/镜面）
        draw.ellipse([margin, margin, size - margin, size - margin],
                      fill=(*mat_color, 200), outline=(*mat_color, 255), width=2)
    elif shape == "diamond":
        # 菱形碎片（银簪）
        cx, cy = size // 2, size // 2
        r = (size - 2 * margin) // 2
        pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy)]
        draw.polygon(pts, fill=(*mat_color, 200), outline=(*mat_color, 255), width=2)
    else:
        # 矩形碎片（照片/信件）— 带不规则边缘
        pts = [
            (margin + 5, margin), (size - margin - 3, margin + 4),
            (size - margin, size - margin - 5), (margin + 3, size - margin)
        ]
        draw.polygon(pts, fill=(*mat_color, 200), outline=(*mat_color, 255), width=2)

    # 碎片编号（中心小字）
    try:
        from PIL import ImageFont
        font = ImageFont.load_default()
    except:
        font = None
    label = frag_id[-3:]  # "001", "002" etc.
    draw.text((size // 2 - 10, size // 2 - 5), label,
              fill=(255, 255, 255, 200), font=font)

    # 轻微模糊边缘（模拟毛边/磨损）
    img = img.filter(ImageFilter.GaussianBlur(radius=0.5))

    return img


def main():
    slots_dir = os.path.join(OUTPUT_DIR, "Slots")
    frags_dir = os.path.join(OUTPUT_DIR, "Fragments")
    os.makedirs(slots_dir, exist_ok=True)
    os.makedirs(frags_dir, exist_ok=True)

    # 生成 5 底图
    for item_id, meta in ITEMS.items():
        item_frags = [f for f in FRAGMENTS if f[1] == item_id]
        board = gen_slot_board(item_id, meta, item_frags)
        path = os.path.join(slots_dir, f"{item_id}_board.png")
        board.save(path)
        print(f"Board: {path} ({board.size[0]}x{board.size[1]}, {len(item_frags)} slots)")

    # 生成 13 碎片
    for fid, iid, sid, sidx, ax, ay in FRAGMENTS:
        meta = ITEMS[iid]
        frag = gen_fragment_sprite(fid, iid, ax, ay, meta)
        path = os.path.join(frags_dir, f"{fid}.png")
        frag.save(path)
        print(f"Fragment: {path} ({frag.size[0]}x{frag.size[1]}, anchor=({ax},{ay}))")

    print(f"\nDone: 5 boards + 13 fragments generated.")


if __name__ == "__main__":
    main()
