#!/usr/bin/env python3
"""fix_texture_meta.py — 批量将 Resources/ 下的 PNG .meta 修改为 Sprite 导入类型。

textureType: 0 (Default) → 8 (Sprite)
spriteMode: 0 (None) → 1 (Single)
alphaIsTransparency: 0 → 1
enableMipMap: 1 → 0

DustNoise_512.png 和 GlowRadial_256.png 保持 Texture2D（Shader 贴图，非 Sprite）。
"""
import os
import re

RES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources")

# These are shader textures, NOT sprites — skip them
SKIP = {"DustNoise_512.png", "GlowRadial_256.png"}

count = 0
for root, dirs, files in os.walk(RES_DIR):
    for f in files:
        if not f.endswith(".png") or f in SKIP:
            continue
        meta_path = os.path.join(root, f + ".meta")
        if not os.path.exists(meta_path):
            continue
        with open(meta_path, "r", encoding="utf-8") as fh:
            content = fh.read()
        changed = False
        # textureType: 0 → 8
        if re.search(r'textureType:\s*0\b', content):
            content = re.sub(r'textureType:\s*0\b', 'textureType: 8', content)
            changed = True
        # spriteMode: 0 → 1
        if re.search(r'spriteMode:\s*0\b', content):
            content = re.sub(r'spriteMode:\s*0\b', 'spriteMode: 1', content)
            changed = True
        # alphaIsTransparency: 0 → 1
        if re.search(r'alphaIsTransparency:\s*0\b', content):
            content = re.sub(r'alphaIsTransparency:\s*0\b', 'alphaIsTransparency: 1', content)
            changed = True
        # enableMipMap: 1 → 0 (sprites don't need mipmaps)
        if re.search(r'enableMipMap:\s*1\b', content):
            content = re.sub(r'enableMipMap:\s*1\b', 'enableMipMap: 0', content)
            changed = True
        if changed:
            with open(meta_path, "w", encoding="utf-8") as fh:
                fh.write(content)
            count += 1
            print(f"Fixed: {os.path.relpath(meta_path, RES_DIR)}")

print(f"\nTotal: {count} .meta files fixed to Sprite import type.")
