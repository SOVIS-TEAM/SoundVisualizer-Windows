# Windows 앱 아이콘을 크기마다 따로 그린다: 파란 둥근 네모 위에 흰 고리, 32px 이상이면 양옆 소리 물결.
# 작은 크기에서도 보이게 크기마다 선 굵기를 정하고, 8배로 그린 뒤 줄인다.
# 인자: 출력 폴더. 만드는 것: SoundVisualizer.ico(16~256), msix 그림 4개, 미리보기.
import io
import math
import struct
import sys
from pathlib import Path

from PIL import Image, ImageDraw

OUT = Path(sys.argv[1])
SS = 8  # 슈퍼샘플링 배수
TOP = (59, 130, 246)     # #3B82F6
BOTTOM = (29, 78, 216)   # #1D4ED8
WHITE = (255, 255, 255)


def tile_mask(size_px, inset, radius):
    m = Image.new("L", (size_px, size_px), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, size_px - 1 - inset, size_px - 1 - inset], radius=radius, fill=255)
    return m


def gradient(w, h):
    g = Image.new("RGBA", (w, h))
    px = g.load()
    for y in range(h):
        t = y / max(1, h - 1)
        c = tuple(round(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3))
        for x in range(w):
            px[x, y] = c + (255,)
    return g


def draw_mark(layer, cx, cy, unit, size, with_waves):
    """unit = 아이콘 한 변(그리는 배율 포함). 고리와 물결을 흰색으로 그린다."""
    d = ImageDraw.Draw(layer)
    # 작은 크기일수록 고리를 굵고 크게(16px: 지름 약 9px, 굵기 약 2px)
    if size <= 20:
        r, w = 0.29 * unit, 0.125 * unit
    elif size <= 32:
        r, w = 0.27 * unit, 0.095 * unit
    else:
        r, w = 0.25 * unit, 0.075 * unit
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=WHITE + (255,), width=max(1, round(w)))
    if with_waves:
        # 양옆으로 퍼지는 소리 물결 두 겹: 안쪽은 진하게, 바깥은 옅게
        for k, (rr, ww, alpha) in enumerate([(0.36, 0.055, 235), (0.445, 0.045, 140)]):
            R = rr * unit
            box = [cx - R, cy - R, cx + R, cy + R]
            span = 38 if k == 0 else 32
            d.arc(box, start=-span, end=span, fill=WHITE + (alpha,), width=max(1, round(ww * unit)))
            d.arc(box, start=180 - span, end=180 + span, fill=WHITE + (alpha,), width=max(1, round(ww * unit)))


def render_square(size, full_bleed=False):
    S = size * SS
    inset = 0 if full_bleed else round(0.04 * S)
    radius = round(0.2 * S)
    tile = gradient(S, S)
    mask = tile_mask(S, inset, radius) if not full_bleed else Image.new("L", (S, S), 255)
    base = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    base.paste(tile, (0, 0), mask)
    mark = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw_mark(mark, S / 2, S / 2, S, size, with_waves=size >= 32)
    base.alpha_composite(mark)
    return base.resize((size, size), Image.LANCZOS)


def render_wide(w, h):
    W, H = w * SS, h * SS
    img = gradient(W, H)
    mark = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    unit = H * 0.9
    draw_mark(mark, W / 2, H / 2, unit, 150, with_waves=True)
    img.alpha_composite(mark)
    return img.resize((w, h), Image.LANCZOS)


def png_bytes(img):
    b = io.BytesIO()
    img.save(b, "PNG", optimize=True)
    return b.getvalue()


def write_ico(path, images):
    entries = [(im.size[0], png_bytes(im)) for im in images]
    header = struct.pack("<HHH", 0, 1, len(entries))
    offset = 6 + 16 * len(entries)
    dir_bytes, data = b"", b""
    for size, blob in entries:
        dim = 0 if size >= 256 else size
        dir_bytes += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset + len(data))
        data += blob
    path.write_bytes(header + dir_bytes + data)


OUT.mkdir(parents=True, exist_ok=True)
sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
icons = [render_square(s) for s in sizes]
write_ico(OUT / "SoundVisualizer.ico", icons)
render_square(44).save(OUT / "Square44x44Logo.png")
render_square(50).save(OUT / "StoreLogo.png")
render_square(150).save(OUT / "Square150x150Logo.png")
render_wide(310, 150).save(OUT / "Wide310x150Logo.png")

# 미리보기: 밝은 바탕(탐색기)과 어두운 바탕(작업 표시줄)에 실제 크기로
prev = Image.new("RGBA", (520, 220), (255, 255, 255, 255))
dark = Image.new("RGBA", (520, 110), (32, 39, 51, 255))
x = 10
for im in icons[:8]:
    s = im.size[0]
    prev.alpha_composite(im, (x, 10)); dark.alpha_composite(im, (x, 10)); x += s + 12
prev.paste(dark, (0, 110))
prev.save(OUT / "preview_sizes.png")
icons[-1].save(OUT / "preview_256.png")
print("ok", [i.size for i in icons])
