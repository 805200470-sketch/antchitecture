"""frames_to_video.py - turn a folder of numbered JPEG/PNG frames into an H.264 MP4 with Blender's video sequencer.

Run through Blender (no ffmpeg install needed):
  Blender -b --factory-startup -P Tools/frames_to_video.py -- <frames dir> <out.mp4> [fps] [quality]
quality: HIGH (large files), MEDIUM (default, good for sharing) or LOW.
Frames are taken in name order (f00000.jpg, f00001.jpg, ...). Colours pass through unchanged (Standard view).
"""
import glob
import os
import sys

import bpy

argv = sys.argv[sys.argv.index('--') + 1:]
src, out = argv[0], argv[1]
fps = int(argv[2]) if len(argv) > 2 else 30
quality = argv[3] if len(argv) > 3 else 'MEDIUM'
files = sorted(glob.glob(os.path.join(src, 'f*.jpg')) + glob.glob(os.path.join(src, 'f*.png')))
if not files:
    sys.exit(f'frames_to_video: no frames in {src}')

scene = bpy.context.scene
img = bpy.data.images.load(files[0])
w, h = img.size
scene.render.resolution_x, scene.render.resolution_y = w, h
scene.render.resolution_percentage = 100
scene.render.fps = fps
scene.frame_start, scene.frame_end = 1, len(files)

# no colour transform: the frames are already display-ready sRGB
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'None'
scene.view_settings.exposure = 0
scene.view_settings.gamma = 1

se = scene.sequence_editor_create()
strips = getattr(se, 'strips', None)
if strips is None:
    strips = se.sequences
strip = strips.new_image(name='frames', filepath=files[0], channel=1, frame_start=1)
for f in files[1:]:
    strip.elements.append(os.path.basename(f))
if hasattr(strip, 'colorspace_settings'):
    strip.colorspace_settings.name = 'sRGB'

settings = scene.render.image_settings
if hasattr(settings, 'media_type'):
    settings.media_type = 'VIDEO'
settings.file_format = 'FFMPEG'
ff = scene.render.ffmpeg
ff.format = 'MPEG4'
ff.codec = 'H264'
ff.constant_rate_factor = quality
ff.ffmpeg_preset = 'GOOD'
ff.gopsize = fps
ff.audio_codec = 'NONE'
scene.render.filepath = out
scene.render.use_file_extension = False
bpy.ops.render.render(animation=True)
print(f'frames_to_video: wrote {out} ({len(files)} frames, {w}x{h}, {fps} fps)')
