"""Read-only RGBA PNG audit. Writes JSON metadata only; never modifies pixels."""
from pathlib import Path
import json
import struct
import zlib
from collections import deque
import hashlib

def png_rgba(path):
    data = path.read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n"
    offset = 8
    compressed = bytearray()
    while offset < len(data):
        size = struct.unpack_from(">I", data, offset)[0]
        kind = data[offset + 4:offset + 8]
        chunk = data[offset + 8:offset + 8 + size]
        if kind == b"IHDR":
            width, height, depth, color, _, _, interlace = struct.unpack(">IIBBBBB", chunk)
            assert (depth, color, interlace) == (8, 6, 0), (path, depth, color, interlace)
        elif kind == b"IDAT":
            compressed.extend(chunk)
        offset += size + 12
    raw = zlib.decompress(compressed)
    stride = width * 4
    pixels = bytearray(width * height * 4)
    previous = bytearray(stride)
    for y in range(height):
        start = y * (stride + 1)
        mode = raw[start]
        row = bytearray(raw[start + 1:start + 1 + stride])
        for x in range(stride):
            a = row[x - 4] if x >= 4 else 0
            b = previous[x]
            c = previous[x - 4] if x >= 4 else 0
            if mode == 1:
                predict = a
            elif mode == 2:
                predict = b
            elif mode == 3:
                predict = (a + b) // 2
            elif mode == 4:
                p = a + b - c
                da, db, dc = abs(p - a), abs(p - b), abs(p - c)
                predict = a if da <= db and da <= dc else b if db <= dc else c
            else:
                assert mode == 0
                predict = 0
            row[x] = (row[x] + predict) & 255
        pixels[y * stride:(y + 1) * stride] = row
        previous = row
    return width, height, pixels


def components(width, height, alpha, threshold=16):
    seen = bytearray(len(alpha))
    result = []
    for seed, value in enumerate(alpha):
        if value < threshold or seen[seed]:
            continue
        seen[seed] = 1
        queue = deque([seed])
        x0 = x1 = seed % width
        y0 = y1 = seed // width
        count = 0
        while queue:
            pixel = queue.popleft()
            x, y = pixel % width, pixel // width
            x0, x1 = min(x0, x), max(x1, x)
            y0, y1 = min(y0, y), max(y1, y)
            count += 1
            for adjacent in (pixel - 1 if x else -1,
                             pixel + 1 if x + 1 < width else -1,
                             pixel - width if y else -1,
                             pixel + width if y + 1 < height else -1):
                if adjacent >= 0 and not seen[adjacent] and alpha[adjacent] >= threshold:
                    seen[adjacent] = 1
                    queue.append(adjacent)
        result.append({"x": x0, "y": y0, "width": x1 - x0 + 1,
                       "height": y1 - y0 + 1, "pixels": count})
    return result

