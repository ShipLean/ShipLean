#!/usr/bin/env python3
"""Rewrites an 8-bit RGB PNG as RGBA (alpha 255) in place. Google Play wants its 512 icon as a 32-bit PNG,
and Chrome writes a fully opaque screenshot as 24-bit RGB. Standard library only."""
import struct, sys, zlib
from pathlib import Path


def chunks(data):
    i = 8
    while i < len(data):
        n, = struct.unpack(">I", data[i:i + 4])
        yield data[i + 4:i + 8], data[i + 8:i + 8 + n]
        i += 12 + n


def unfilter(raw, width, height, bpp):
    stride = width * bpp
    out, prev, i = [], bytearray(stride), 0
    for _ in range(height):
        f, row = raw[i], bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = row[x - bpp] if x >= bpp else 0
            b, c = prev[x], (prev[x - bpp] if x >= bpp else 0)
            if f == 1: row[x] = (row[x] + a) & 255
            elif f == 2: row[x] = (row[x] + b) & 255
            elif f == 3: row[x] = (row[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                row[x] = (row[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        out.append(row)
        prev = row
    return out


def chunk(kind, body):
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)


for name in sys.argv[1:]:
    p = Path(name)
    data = p.read_bytes()
    parts = list(chunks(data))
    ihdr = parts[0][1]
    width, height, depth, ctype = struct.unpack(">IIBB", ihdr[:10])
    if ctype == 6:
        continue
    if (depth, ctype) != (8, 2) or ihdr[12] != 0:
        sys.exit(f"{name}: only 8-bit non-interlaced RGB is handled")
    rows = unfilter(zlib.decompress(b"".join(b for k, b in parts if k == b"IDAT")), width, height, 3)
    raw = bytearray()
    for row in rows:
        raw.append(0)
        for x in range(0, len(row), 3):
            raw += row[x:x + 3] + b"\xff"
    head = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    p.write_bytes(data[:8] + chunk(b"IHDR", head) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))
