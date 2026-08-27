#!/usr/bin/env python3
"""fix_npot_meta.py — Set nPOTScale to 0 (None) for all Sprite PNGs.

Unity can't generate Sprites from NPOT (Non-Power-Of-Two) textures when
nPOTScale is 1 (scales to nearest POT). Setting to 0 (None) fixes this.
"""
import os
import re

RES_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources")
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
        if re.search(r'nPOTScale:\s*1\b', content):
            content = re.sub(r'nPOTScale:\s*1\b', 'nPOTScale: 0', content)
            with open(meta_path, "w", encoding="utf-8") as fh:
                fh.write(content)
            count += 1
            print(f"Fixed NPOT: {os.path.relpath(meta_path, RES_DIR)}")

print(f"\nTotal: {count} files fixed.")
