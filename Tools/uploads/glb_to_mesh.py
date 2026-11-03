"""A Tripo GLB as the game reads a visitor's model (Uploads.cs): <out>.mesh and <out>_tex.<jpg|png>.

    python3 Tools/uploads/glb_to_mesh.py <model.glb> <out base path>

.mesh ("AGM1", little endian): vertex count, triangle count, then per vertex its position (3 floats), normal (3 floats)
and uv (2 floats), then 3 ints per triangle. In Unity's space (glTF's x turned round, so the triangles' order too), the
model's front still toward +z. The texture is the material's base colour image, at most 2048 px (Tripo's are 4096:
the game only reads its points' colours from it).
Only plain GLBs (no Draco or meshopt compression), as Tripo makes them.
"""
import io, json, struct, sys
import numpy as np
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
data = open(src, 'rb').read()
magic, version, length = struct.unpack('<III', data[:12])
assert magic == 0x46546C67, 'not a GLB'
pos, chunks = 12, {}
while pos < length:
    n, kind = struct.unpack('<II', data[pos:pos + 8])
    chunks[kind] = data[pos + 8:pos + 8 + n]
    pos += 8 + n
gltf, binary = json.loads(chunks[0x4E4F534A]), chunks[0x004E4942]
assert not gltf.get('extensionsUsed'), f"compressed GLB: {gltf.get('extensionsUsed')}"

SIZES = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}
TYPES = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}

def view(i):
    v = gltf['bufferViews'][i]
    return binary[v.get('byteOffset', 0):v.get('byteOffset', 0) + v['byteLength']], v.get('byteStride')

def accessor(i):
    a = gltf['accessors'][i]
    raw, stride = view(a['bufferView'])
    dtype, k = np.dtype(TYPES[a['componentType']]), SIZES[a['type']]
    off = a.get('byteOffset', 0)
    if stride and stride != dtype.itemsize * k:
        rows = np.frombuffer(raw, np.uint8, count=a['count'] * stride, offset=off).reshape(a['count'], stride)
        arr = rows[:, :dtype.itemsize * k].copy().view(dtype).reshape(a['count'], k)
    else:
        arr = np.frombuffer(raw, dtype, count=a['count'] * k, offset=off).reshape(a['count'], k)
    arr = arr.astype(np.float32 if a['componentType'] == 5126 else np.int64)
    if a.get('normalized'):
        arr = arr / float(np.iinfo(dtype).max)
    return arr

P, N, U, I = [], [], [], []
base = 0
for node in gltf['nodes']:
    if 'mesh' not in node:
        continue
    assert not any(k in node for k in ('matrix', 'rotation', 'scale', 'translation')), 'node transforms not handled'
    for prim in gltf['meshes'][node['mesh']]['primitives']:
        at = prim['attributes']
        p = accessor(at['POSITION'])
        P.append(p)
        N.append(accessor(at['NORMAL']) if 'NORMAL' in at else np.zeros_like(p))
        U.append(accessor(at['TEXCOORD_0']) if 'TEXCOORD_0' in at else np.zeros((len(p), 2), np.float32))
        idx = accessor(prim['indices']).reshape(-1) if 'indices' in prim else np.arange(len(p))
        I.append(idx.reshape(-1, 3) + base)
        base += len(p)
P, N, U, I = np.concatenate(P), np.concatenate(N), np.concatenate(U), np.concatenate(I)
P[:, 0] *= -1                                  # glTF is right-handed, Unity left-handed
N[:, 0] *= -1
I = I[:, [0, 2, 1]]                            # so each triangle turns the other way round
U[:, 1] = 1 - U[:, 1]                          # glTF's v runs down the image, Unity's up

with open(out + '.mesh', 'wb') as f:
    f.write(struct.pack('<4sii', b'AGM1', len(P), len(I)))
    f.write(np.hstack([P, N, U]).astype('<f4').tobytes())
    f.write(I.astype('<i4').tobytes())

material = gltf['materials'][prim.get('material', 0)]
tex = material['pbrMetallicRoughness']['baseColorTexture']['index']
image = gltf['images'][gltf['textures'][tex]['source']]
raw, _ = view(image['bufferView'])
ext = 'png' if image.get('mimeType') == 'image/png' else 'jpg'
picture = Image.open(io.BytesIO(raw))
if max(picture.size) > 2048:
    picture = picture.resize((2048, 2048 * picture.size[1] // picture.size[0]), Image.LANCZOS)
picture.save(f'{out}_tex.{ext}', **({'quality': 92} if ext == 'jpg' else {}))
lo, hi = P.min(0), P.max(0)
print(f'{len(P)} vertices, {len(I)} triangles, size {hi - lo}, texture {out}_tex.{ext}')
