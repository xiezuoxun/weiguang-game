#!/usr/bin/env python3
"""gen_b1_textures.py — B1 拂尘微光 Shader 配套贴图程序化生成。

产出：
  1. DustNoise_512.png  — 512×512 无缝平铺灰阶噪点（Shader _DustTex）
  2. GlowRadial_256.png — 256×256 径向渐变光晕（Shader _GlowTex）

技术要求：
  - DustNoise: 灰阶 R=G=B，无缝平铺（边缘 wrap-around），值域 [0.2, 0.8]
    （不全黑/全白，避免遮罩完全透明或完全不透明）
  - GlowRadial: 中心暖白 → 边缘透明，RGB 略偏暖（R>G>B），alpha 径向衰减
"""
import math
import random
import os
from PIL import Image

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Art", "Textures")


def gen_dust_noise(size=512, seed=42):
    """无缝平铺灰阶噪点：Perlin-like 多层叠加 + 边缘 wrap。"""
    rng = random.Random(seed)
    px = Image.new("L", (size, size))  # 灰阶
    data = px.load()

    # 多层 value noise（低频→高频），每层 wrap-around 保证无缝
    for octave in range(5):
        freq = 2 ** octave  # 1, 2, 4, 8, 16
        amp = 0.5 ** octave  # 1, 0.5, 0.25, 0.125, 0.0625
        grid_size = freq * 4  # 采样网格
        grid = [[rng.random() for _ in range(grid_size)] for _ in range(grid_size)]

        for y in range(size):
            for x in range(size):
                # 归一化坐标 → wrap-around 到 grid
                fx = (x / size * grid_size) % grid_size
                fy = (y / size * grid_size) % grid_size
                ix0, iy0 = int(fx), int(fy)
                ix1, iy1 = (ix0 + 1) % grid_size, (iy0 + 1) % grid_size
                tx, ty = fx - ix0, fy - iy0
                # smoothstep 插值
                sx, sy = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
                v = (grid[iy0][ix0] * (1 - sx) + grid[iy0][ix1] * sx) * (1 - sy) + \
                    (grid[iy1][ix0] * (1 - sx) + grid[iy1][ix1] * sx) * sy
                if octave == 0:
                    data[x, y] = int(v * 255 * amp)
                else:
                    data[x, y] = min(255, data[x, y] + int(v * 255 * amp * 0.6))

    # 值域映射到 [0.2, 0.8] → [51, 204]
    for y in range(size):
        for x in range(size):
            v = data[x, y]
            v = int(51 + (v / 255.0) * 153)
            data[x, y] = v

    return px


def gen_glow_radial(size=256):
    """径向渐变光晕：中心暖白 → 边缘透明。"""
    img = Image.new("RGBA", (size, size))
    data = img.load()
    cx, cy = size / 2, size / 2
    max_r = size / 2

    for y in range(size):
        for x in range(size):
            dx, dy = x - cx, y - cy
            dist = math.sqrt(dx * dx + dy * dy)
            t = min(dist / max_r, 1.0)
            # 平滑衰减：1 - smoothstep(0.2, 1.0, t)
            if t < 0.2:
                alpha = 1.0
            else:
                u = (t - 0.2) / 0.8
                alpha = 1.0 - u * u * (3 - 2 * u)  # smoothstep 反向

            # 暖白色：R=255, G=237, B=199（对齐 Shader _GlowColor 默认 (1.0, 0.93, 0.78)）
            r, g, b = 255, 237, 199
            data[x, y] = (r, g, b, int(alpha * 255))

    return img


def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    noise = gen_dust_noise(512)
    noise_path = os.path.join(OUTPUT_DIR, "DustNoise_512.png")
    noise.save(noise_path)
    print(f"Generated: {noise_path} ({noise.size[0]}x{noise.size[1]})")

    glow = gen_glow_radial(256)
    glow_path = os.path.join(OUTPUT_DIR, "GlowRadial_256.png")
    glow.save(glow_path)
    print(f"Generated: {glow_path} ({glow.size[0]}x{glow.size[1]})")

    # 验证无缝性：左边缘 vs 右边缘像素差
    left = [noise.getpixel((0, y)) for y in range(512)]
    right = [noise.getpixel((511, y)) for y in range(512)]
    diff = sum(abs(l - r) for l, r in zip(left, right)) / 512
    print(f"Seamless check (L-R diff): {diff:.2f} (should be <5.0)")

    top = [noise.getpixel((x, 0)) for x in range(512)]
    bot = [noise.getpixel((x, 511)) for x in range(512)]
    diff2 = sum(abs(t - b) for t, b in zip(top, bot)) / 512
    print(f"Seamless check (T-B diff): {diff2:.2f} (should be <5.0)")


if __name__ == "__main__":
    main()
