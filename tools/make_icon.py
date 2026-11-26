# 원래 로고(tools/icon/logo_256.png)로 앱 아이콘 파일 SoundVisualizer.ico 를 만든다(#52).
#
# 로고 디자인은 바꾸지 않는다. 원래 아이콘에는 256px 한 장만 있어서 Windows 가 작게 줄이면 가는 고리가 사라져
# 검은 네모만 남았다. 그래서 크기마다 그림을 넣는다:
#   - 256px: 로고 파일 그대로(바이트까지 같다).
#   - 48~128px: 로고를 고품질로 줄인다.
#   - 16~40px(작업 표시줄·탐색기·알림 영역): 로고의 밝은 선을 최대값 필터로 조금 넓힌 뒤 줄인다.
#     모양·색·바탕은 모두 로고의 픽셀에서 나온다. 줄인 뒤 고리가 1px 남짓이 되게 한다.
# MSIX 타일(packaging/msix/Assets)은 원래 파일을 그대로 쓰므로 이 스크립트가 만들지 않는다.
#
# 쓰는 법: python tools/make_icon.py  (저장소 루트에서)
import io
import struct
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
LOGO = ROOT / "tools" / "icon" / "logo_256.png"
OUT = ROOT / "SoundVisualizer" / "SoundVisualizer.ico"
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128]
WIDEN_UP_TO = 40


def frame(logo, size):
    if size > WIDEN_UP_TO:
        return logo.resize((size, size), Image.LANCZOS)
    k = int(round(256 / size * 0.7)) | 1
    k = max(3, min(k, 13))
    return logo.filter(ImageFilter.MaxFilter(k)).resize((size, size), Image.LANCZOS)


def png_bytes(img):
    b = io.BytesIO()
    img.save(b, "PNG", optimize=True)
    return b.getvalue()


def write_ico(path, entries):
    # entries: (크기, PNG 바이트). 256 은 디렉터리에 0 으로 적는다.
    header = struct.pack("<HHH", 0, 1, len(entries))
    offset = 6 + 16 * len(entries)
    directory, data = b"", b""
    for size, blob in entries:
        dim = 0 if size >= 256 else size
        directory += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset + len(data))
        data += blob
    path.write_bytes(header + directory + data)


logo_bytes = LOGO.read_bytes()
logo = Image.open(io.BytesIO(logo_bytes)).convert("RGBA")
assert logo.size == (256, 256), logo.size
entries = [(s, png_bytes(frame(logo, s))) for s in SIZES] + [(256, logo_bytes)]
write_ico(OUT, entries)
print("ok", OUT, [s for s, _ in entries])
