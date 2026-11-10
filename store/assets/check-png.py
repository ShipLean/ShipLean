#!/usr/bin/env python3
"""Checks each exported PNG against its spec: exact width and height, and alpha only where the store wants it.
Reads the tab-separated list export.sh writes (id, w, h, file, alpha). Exits 1 on any mismatch."""
import struct, sys
from pathlib import Path

COLOR = {0: "gray", 2: "RGB", 3: "palette", 4: "gray+alpha", 6: "RGBA"}
bad = 0
for line in Path(sys.argv[1]).read_text().splitlines():
    id_, w, h, file, alpha = line.split("\t")
    p = Path("exports") / file
    data = p.read_bytes()[:33]
    width, height, _, ctype = struct.unpack(">IIBB", data[16:26])
    has_alpha = ctype in (4, 6)
    ok = (width, height) == (int(w), int(h)) and has_alpha == (alpha == "1")
    size = p.stat().st_size
    print(f"{'ok ' if ok else 'BAD'} {file}  {width}x{height} {COLOR.get(ctype, ctype)}  {size // 1024} KB")
    bad += not ok
sys.exit(1 if bad else 0)
