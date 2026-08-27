#!/usr/bin/env python3
"""gen_placeholder_assets.py — 补齐 A6 测试所需的 P1/P2 占位资产。

产出：
  Items/it_watch.png          — 512×512 物件立绘占位
  Clients/cl_shen.png         — 256×256 客户符号占位
  Backdrop/dust_table.png     — 1080×1920 承托底占位
  Whisper/whisper_note.png    — 600×240 低语纸签底占位
  Cursor/dust_brush.png       — 128×128 手势笔触占位
  Choices/choice_tab.png      — 复制 choice_tab_idle（A6 测试检查的 key）
  Audio/sfx_reveal.wav        — 简短 SFX 占位
  Audio/bgm_main.wav          — 短 BGM 占位
"""
import os
import struct
import wave
from PIL import Image, ImageDraw

GAME_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
RES_DIR = os.path.join(GAME_DIR, "Assets", "Resources")


def gen_item_portrait(name, size=512):
    """物件立绘占位：纯色底 + 物件轮廓 + 名称。"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([50, 50, size - 50, size - 50], fill=(200, 180, 150, 200), outline=(120, 100, 70, 255), width=3)
    try:
        from PIL import ImageFont
        font = ImageFont.load_default()
    except:
        font = None
    draw.text((size // 2 - 30, size // 2 - 10), name, fill=(80, 60, 40, 255), font=font)
    return img


def gen_client_symbol(name, size=256):
    """客户符号占位：剪影圆形 + 名称首字。"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse([20, 20, size - 20, size - 20], fill=(150, 140, 130, 180), outline=(100, 90, 80, 255), width=2)
    try:
        from PIL import ImageFont
        font = ImageFont.load_default()
    except:
        font = None
    draw.text((size // 2 - 5, size // 2 - 5), name[0] if name else "?", fill=(255, 255, 255, 220), font=font)
    return img


def gen_backdrop(size=(1080, 1920)):
    """DustGrid 承托底：中性绒布色 + 轻微噪点。"""
    import random
    rng = random.Random(7)
    w, h = size
    img = Image.new("RGBA", (w, h), (45, 42, 38, 255))
    px = img.load()
    for y in range(h):
        for x in range(w):
            n = rng.randint(-8, 8)
            px[x, y] = (45 + n, 42 + n, 38 + n, 255)
    return img


def gen_whisper_note(size=(600, 240)):
    """低语浮纸签底：宣纸色半透明 + 毛边。"""
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    margin = 15
    draw.rectangle([margin, margin, size[0] - margin, size[1] - margin],
                    fill=(230, 220, 195, 200), outline=(150, 130, 100, 180), width=2)
    # 预留文字安全区指示
    draw.rectangle([margin + 20, margin + 15, size[0] - margin - 20, size[1] - margin - 15],
                    outline=(0, 0, 0, 20), width=1)
    return img


def gen_cursor(size=128):
    """手势笔触占位：指尖微光圆。"""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx, cy = size // 2, size // 2
    # 外圈微光
    for r in range(40, 20, -2):
        alpha = int(60 * (1 - (r - 20) / 20))
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 230, 160, alpha))
    # 中心点
    draw.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=(255, 240, 200, 220))
    return img


def gen_wav(path, duration_sec, freq=440, volume=0.3, sample_rate=44100):
    """生成简单正弦波 WAV 占位音频。"""
    import math
    n_samples = int(duration_sec * sample_rate)
    with wave.open(path, 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)  # 16-bit
        w.setframerate(sample_rate)
        for i in range(n_samples):
            # 淡入淡出
            t = i / sample_rate
            env = min(1.0, t / 0.05) * min(1.0, (duration_sec - t) / 0.1)
            val = int(volume * env * 32767 * math.sin(2 * math.pi * freq * t))
            w.writeframes(struct.pack('<h', val))


def main():
    # Items
    items_dir = os.path.join(RES_DIR, "Items")
    os.makedirs(items_dir, exist_ok=True)
    gen_item_portrait("怀表").save(os.path.join(items_dir, "it_watch.png"))

    # Clients
    clients_dir = os.path.join(RES_DIR, "Clients")
    os.makedirs(clients_dir, exist_ok=True)
    gen_client_symbol("沈").save(os.path.join(clients_dir, "cl_shen.png"))

    # Backdrop
    bd_dir = os.path.join(RES_DIR, "Backdrop")
    os.makedirs(bd_dir, exist_ok=True)
    gen_backdrop().save(os.path.join(bd_dir, "dust_table.png"))

    # Whisper
    wh_dir = os.path.join(RES_DIR, "Whisper")
    os.makedirs(wh_dir, exist_ok=True)
    gen_whisper_note().save(os.path.join(wh_dir, "whisper_note.png"))

    # Cursor
    cur_dir = os.path.join(RES_DIR, "Cursor")
    os.makedirs(cur_dir, exist_ok=True)
    gen_cursor().save(os.path.join(cur_dir, "dust_brush.png"))

    # Choices/choice_tab (A6 test key without _idle suffix)
    choices_dir = os.path.join(RES_DIR, "Choices")
    import shutil
    src = os.path.join(choices_dir, "choice_tab_idle.png")
    dst = os.path.join(choices_dir, "choice_tab.png")
    if os.path.exists(src):
        shutil.copy2(src, dst)
        print(f"Copy: {dst}")

    # Audio
    audio_dir = os.path.join(RES_DIR, "Audio")
    os.makedirs(audio_dir, exist_ok=True)
    gen_wav(os.path.join(audio_dir, "sfx_reveal.wav"), duration_sec=0.5, freq=800, volume=0.25)
    gen_wav(os.path.join(audio_dir, "bgm_main.wav"), duration_sec=2.0, freq=220, volume=0.15)

    print("\nDone: all placeholder assets generated.")


if __name__ == "__main__":
    main()
