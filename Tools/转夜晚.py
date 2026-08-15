# -*- coding: utf-8 -*-
"""夜晚化 v4：灰蓝冷夜——天空近黑，山丘灰蓝、对比增强轮廓清晰。"""
from PIL import Image, ImageDraw
import os, statistics

src = r"E:\UNITY GAME\文字奇幻rpg\Assets\Resources\界面\Hills Free (update 3.0).png"
bak = r"E:\UNITY GAME\文字奇幻rpg\Assets\Resources\界面\Hills Free (update 3.0).png.morning.bak.png"

img = Image.open(bak).convert("RGB")
w, h = img.size
gray = img.convert("L")
lum = gray.load()
px = img.load()
print(f"恢复原图: {w}x{h}")

# 逐列山脊 + 中值平滑
ridge = []
for x in range(w):
    r = h
    for y in range(h):
        if lum[x, y] < 95:
            r = y
            break
    ridge.append(r)
half = 7
for x in range(w):
    lo = max(0, x - half); hi = min(w, x + half + 1)
    ridge[x] = int(statistics.median(ridge[lo:hi]))

# 逐像素
for y in range(h):
    for x in range(w):
        L = lum[x, y] / 255.0
        if y < ridge[x]:
            # 天空：几乎黑的深灰蓝，底部微亮
            t = y / max(1, ridge[x])
            r = int(5 + (15 - 5) * t)
            g = int(7 + (19 - 7) * t)
            b = int(14 + (36 - 14) * t)
            px[x, y] = (r, g, b)
        else:
            # 山丘：灰蓝基色（上亮下暗）× 增强对比的亮度（轮廓清晰）
            t = min(1.0, (y - ridge[x]) / max(1, h - ridge[x]))
            base = (27 + (7 - 27) * t, 31 + (8 - 31) * t, 52 + (15 - 52) * t)
            factor = 0.35 + 0.8 * (L ** 1.15)   # 暗的更暗、亮的保留 → 轮廓清晰
            px[x, y] = (
                min(255, int(base[0] * factor)),
                min(255, int(base[1] * factor)),
                min(255, int(base[2] * factor)),
            )

# 山脊月光描边（加强，突出轮廓）
draw = ImageDraw.Draw(img, "RGBA")
for x in range(0, w, 2):
    y = ridge[x]
    if y < h - 1:
        draw.point((x, y), fill=(205, 215, 245, 120))
        if y + 1 < h:
            draw.point((x, y + 1), fill=(150, 160, 215, 60))

img.save(src, "PNG")
print("完成 v4：灰蓝冷夜，天空近黑，山丘轮廓清晰")
