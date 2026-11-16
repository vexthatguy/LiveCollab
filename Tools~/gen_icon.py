#!/usr/bin/env python3
"""Generate LiveCollab .ico (64x64, PNG-compressed) with pure stdlib.

Draws the LiveCollab brand: cyan orb + two white highlights, matching the
Unity toolbar icon.
"""
import struct
import zlib
import math
import os

S = 64
CENTER = (S / 2.0, S / 2.0)
RADIUS = 27.0
CYAN = (0.13, 0.77, 0.91)
WHITE = (1.0, 1.0, 1.0)


def px(x, y):
    dx = x - CENTER[0]
    dy = y - CENTER[1]
    d = math.sqrt(dx * dx + dy * dy)
    if d > RADIUS + 1.2:
        return (0, 0, 0, 0)
    # antialias edge
    a = 1.0
    if d > RADIUS - 1.2:
        a = max(0.0, min(1.0, (RADIUS + 1.2 - d) / 2.4))
    # base orb with simple shading (lighter top-left)
    nx, ny = dx / max(d, 0.001), dy / max(d, 0.001)
    shade = 0.78 + 0.22 * (0.5 - ny * 0.5 + nx * 0.25)
    r = CYAN[0] * shade
    g = CYAN[1] * shade
    b = CYAN[2] * shade
    # two white highlights
    for (hx, hy, hr) in ((0.38, 0.30, 4.5), (0.72, 0.70, 3.2)):
        hd = math.sqrt((x / S - hx) ** 2 + (y / S - hy) ** 2)
        if hd < hr / S:
            w = 1.0 - hd / (hr / S)
            r = r + (WHITE[0] - r) * w
            g = g + (WHITE[1] - g) * w
            b = b + (WHITE[2] - b) * w
    return (int(r * 255), int(g * 255), int(b * 255), int(a * 255))


def chunk(tag, data):
    return (struct.pack(">I", len(data)) + tag + data +
            struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))


def make_png(size, get_px):
    raw = bytearray()
    for y in range(size):
        raw.append(0)  # filter: none
        for x in range(size):
            r, g, b, a = get_px(x, y)
            raw += bytes((r, g, b, a))
    ihdr = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) +
            chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def make_ico(png):
    # ICONDIR
    header = struct.pack("<HHH", 0, 1, 1)
    # ICONDIRENTRY
    entry = struct.pack("<BBBBHHII", 64, 64, 0, 0, 1, 32, len(png), 22)
    return header + entry + png


def main():
    png = make_png(S, px)
    ico = make_ico(png)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "livecollab_icon.ico")
    with open(out, "wb") as f:
        f.write(ico)
    print("wrote", out, len(ico), "bytes")


if __name__ == "__main__":
    main()
