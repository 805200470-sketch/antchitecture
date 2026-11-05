#!/usr/bin/env python3
"""probe.py <video>... - each video's size, frame rate, frames and length (OpenCV's FFmpeg; no ffprobe here)."""
import sys

import cv2

for p in sys.argv[1:]:
    v = cv2.VideoCapture(p)
    w, h = int(v.get(cv2.CAP_PROP_FRAME_WIDTH)), int(v.get(cv2.CAP_PROP_FRAME_HEIGHT))
    fps, n = v.get(cv2.CAP_PROP_FPS), int(v.get(cv2.CAP_PROP_FRAME_COUNT))
    print(f'{p}\t{w}x{h}\t{fps:.2f} fps\t{n} frames\t{n / fps if fps else 0:.2f} s')
