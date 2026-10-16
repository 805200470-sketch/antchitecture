#!/usr/bin/env python3
"""mp4_faststart.py - move an MP4's index (moov box) in front of the media data, like qt-faststart.

Blender writes the index at the end of the file. Players that stream or preview (browsers, phones,
chat apps, some viewers) then have to read the whole file before showing anything, which looks like
"the video will not open". Moving the index to the front fixes that without re-encoding.

  python3 Tools/mp4_faststart.py in.mp4 [out.mp4]      (no out: rewrite in place via a temp file)
"""
import os
import struct
import sys

CONTAINERS = {b'moov', b'trak', b'mdia', b'minf', b'stbl', b'edts', b'dinf', b'mvex', b'moof', b'traf'}


def boxes(buf, start, end):
    i = start
    while i + 8 <= end:
        size, typ = struct.unpack('>I4s', buf[i:i + 8])
        hdr = 8
        if size == 1:
            size = struct.unpack('>Q', buf[i + 8:i + 16])[0]
            hdr = 16
        elif size == 0:
            size = end - i
        if size < hdr:
            raise ValueError(f'bad box {typ!r} at {i}')
        yield typ, i, size, hdr
        i += size


def shift_offsets(buf, start, end, shift):
    """Add shift to every chunk offset (stco / co64) inside a moov box."""
    for typ, i, size, hdr in boxes(buf, start, end):
        body = i + hdr
        if typ in CONTAINERS:
            shift_offsets(buf, body, i + size, shift)
        elif typ == b'stco':
            n = struct.unpack('>I', buf[body + 4:body + 8])[0]
            for k in range(n):
                o = body + 8 + 4 * k
                v = struct.unpack('>I', buf[o:o + 4])[0] + shift
                if v > 0xFFFFFFFF:
                    raise ValueError('offset overflow: file too large for 32-bit chunk offsets')
                buf[o:o + 4] = struct.pack('>I', v)
        elif typ == b'co64':
            n = struct.unpack('>I', buf[body + 4:body + 8])[0]
            for k in range(n):
                o = body + 8 + 8 * k
                buf[o:o + 8] = struct.pack('>Q', struct.unpack('>Q', buf[o:o + 8])[0] + shift)


def faststart(src, dst):
    data = open(src, 'rb').read()
    top = list(boxes(data, 0, len(data)))
    moov = next((b for b in top if b[0] == b'moov'), None)
    mdat = next((b for b in top if b[0] == b'mdat'), None)
    if moov is None or mdat is None:
        raise ValueError('no moov or mdat box: not an MP4/MOV file?')
    if moov[1] < mdat[1]:
        if src != dst:
            open(dst, 'wb').write(data)
        return False
    moov_bytes = bytearray(data[moov[1]:moov[1] + moov[2]])
    shift_offsets(moov_bytes, moov[3], len(moov_bytes), len(moov_bytes))
    out = data[:mdat[1]] + bytes(moov_bytes) + data[mdat[1]:moov[1]] + data[moov[1] + moov[2]:]
    # write in place (same file, same identity): a chat card or shortcut pointing at the old file keeps working
    with open(dst, 'r+b' if os.path.exists(dst) else 'wb') as f:
        f.seek(0)
        f.write(out)
        f.truncate()
    return True


if __name__ == '__main__':
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else src
    moved = faststart(src, dst)
    print(f'mp4_faststart: {"moved the index to the front of" if moved else "index already at the front of"} {dst}')
