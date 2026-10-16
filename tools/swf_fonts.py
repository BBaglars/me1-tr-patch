#!/usr/bin/env python3
"""List DefineFont2/3 fonts of SWF/GFX files (as dumped by `Me1Tlk swf`) and check Turkish glyph coverage.
Usage: swf_fonts.py <dir-or-swf> ..."""
import glob, os, struct, sys, zlib

NEED = "ĞğİıŞşÇçÖöÜü"


def body(data):
    sig = data[:3]
    if sig in (b"CWS", b"CFX"):
        return data[8:] if False else data[:8] + zlib.decompress(data[8:])
    if sig in (b"FWS", b"GFX"):
        return data
    raise ValueError("not a swf")


def tags(d):
    p = 8
    nbits = d[p] >> 3
    p += (5 + nbits * 4 + 7) // 8 + 4  # rect + framerate(2) + framecount(2)
    while p + 2 <= len(d):
        h = struct.unpack_from("<H", d, p)[0]
        p += 2
        code, ln = h >> 6, h & 0x3F
        if ln == 0x3F:
            ln = struct.unpack_from("<I", d, p)[0]
            p += 4
        yield code, d[p:p + ln]
        p += ln


def fonts(path):
    out = []
    d = body(open(path, "rb").read())
    for code, t in tags(d):
        if code not in (48, 75):
            continue
        fid = struct.unpack_from("<H", t, 0)[0]
        flags = t[2]
        wide_offsets, wide_codes = bool(flags & 8), bool(flags & 4) or code == 75
        nlen = t[4]
        name = t[5:5 + nlen].decode("latin-1", "replace")
        p = 5 + nlen
        n = struct.unpack_from("<H", t, p)[0]
        p += 2
        osz = 4 if wide_offsets else 2
        p += (n + 1) * osz if code in (48, 75) else 0
        # code table sits after the glyph shapes; its offset is stored right after the offset table
        glyph_off = struct.unpack_from("<I" if wide_offsets else "<H", t, p - osz)[0]
        # offsets are relative to the start of the offset table
        tab_start = p - (n + 1) * osz
        cpos = tab_start + glyph_off
        fmt = "<H" if wide_codes else "<B"
        sz = 2 if wide_codes else 1
        chars = {struct.unpack_from(fmt, t, cpos + i * sz)[0] for i in range(n)}
        out.append((fid, name, n, chars))
    return out


if __name__ == "__main__":
    paths = []
    for a in sys.argv[1:]:
        paths += glob.glob(os.path.join(a, "**", "*.swf"), recursive=True) if os.path.isdir(a) else [a]
    seen = {}
    for p in paths:
        try:
            fs = fonts(p)
        except Exception as e:  # noqa
            print("ERR", p, e)
            continue
        for fid, name, n, chars in fs:
            miss = [c for c in NEED if ord(c) not in chars]
            key = (name, n)
            if key in seen:
                continue
            seen[key] = p
            print(f"{os.path.basename(p)}: font '{name}' glyphs={n} missing={''.join(miss) or 'none'}")
