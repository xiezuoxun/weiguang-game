#!/usr/bin/env python3
"""gen_b3_choice_tabs.py — B3 抉择纸签双态贴图程序化生成。

产出：
  1. choice_tab_idle.png     — 480×200 透明 PNG，宣纸毛边，未选中态
  2. choice_tab_selected.png — 480×200 透明 PNG，浮起+描边加粗+微光，选中态

色弱友好三重区分：亮度差 + 描边粗细差 + 形状差异（选中态有微光外圈）
"""
import os
import random
from PIL import Image, ImageDraw, ImageFilter

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Choices")
W, H = 480, 200


def gen_paper_edge(width, height, jitter=3, seed=42):
    """生成宣纸毛边不规则路径（四边形带抖动）。"""
    rng = random.Random(seed)
    margin = 20
    pts = []
    # 上边
    steps = 24
    for i in range(steps + 1):
        x = margin + (width - 2 * margin) * i / steps
        y = margin + rng.uniform(-jitter, jitter)
        pts.append((x, y))
    # 右边
    for i in range(1, 6):
        x = width - margin + rng.uniform(-jitter, jitter)
        y = margin + (height - 2 * margin) * i / 5
        pts.append((x, y))
    # 下边
    for i in range(steps + 1):
        x = width - margin - (width - 2 * margin) * i / steps
        y = height - margin + rng.uniform(-jitter, jitter)
        pts.append((x, y))
    # 左边
    for i in range(1, 6):
        x = margin + rng.uniform(-jitter, jitter)
        y = height - margin - (height - 2 * margin) * i / 5
        pts.append((x, y))
    return pts


def gen_choice_tab(selected=False):
    """生成单张纸签贴图。"""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # 宣纸底色：未选中偏暗，选中偏亮（亮度区分）
    if selected:
        paper_color = (245, 235, 210, 235)    # 亮米黄
        edge_color = (180, 130, 60, 255)       # 深棕描边
        edge_width = 4                           # 粗描边
        glow_color = (255, 220, 150, 80)        # 微光外圈
    else:
        paper_color = (200, 188, 165, 200)     # 暗米黄
        edge_color = (120, 100, 75, 180)        # 淡棕描边
        edge_width = 2                           # 细描边
        glow_color = None

    # 选中态：先画微光外圈（形状区分）
    if glow_color:
        glow_margin = 12
        glow_pts = gen_paper_edge(W, H, jitter=5, seed=99)
        # 外扩光圈
        glow_expanded = [(x + (1 if x > W//2 else -1) * 4, y + (1 if y > H//2 else -1) * 4) for x, y in glow_pts]
        draw.polygon(glow_expanded, fill=glow_color)
        # 模糊光圈
        img = img.filter(ImageFilter.GaussianBlur(radius=6))
        draw = ImageDraw.Draw(img)

    # 纸签本体
    pts = gen_paper_edge(W, H, jitter=3, seed=42 if not selected else 43)
    draw.polygon(pts, fill=paper_color, outline=edge_color, width=edge_width)

    # 安全区指示线（预留文字区域 ≤12 中文字）
    safe_margin = 30
    safe_color = (0, 0, 0, 30) if not selected else (180, 130, 60, 50)
    draw.rectangle([safe_margin, safe_margin + 10, W - safe_margin, H - safe_margin - 10],
                    outline=safe_color, width=1)

    # 选中态额外标记：右上角小圆点（形状区分辅助）
    if selected:
        cx, cy = W - 40, 40
        draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8],
                      fill=(255, 200, 100, 220), outline=(180, 130, 60, 255), width=2)

    return img


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    idle = gen_choice_tab(selected=False)
    idle_path = os.path.join(OUTPUT_DIR, "choice_tab_idle.png")
    idle.save(idle_path)
    print(f"Idle: {idle_path} ({idle.size[0]}x{idle.size[1]})")

    selected = gen_choice_tab(selected=True)
    sel_path = os.path.join(OUTPUT_DIR, "choice_tab_selected.png")
    selected.save(sel_path)
    print(f"Selected: {sel_path} ({selected.size[0]}x{selected.size[1]})")

    print("\nDone: 2 choice tab textures generated.")


if __name__ == "__main__":
    main()
