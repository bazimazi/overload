"""Validate actual source images, runtime copies, animation metadata and foot pivots. No image editing."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets/source/pixel-v2'
RUNTIME = ROOT / 'src/Overload.Game/Assets/Pixel'
frames = 0
images = 0
for path in sorted(RUNTIME.glob('*.png')):
    original = ROOT / 'assets/source/world-v1/landmarks.png' if path.name == 'world-landmarks.png' else SOURCE / path.name
    assert original.exists(), f'Original missing: {path.name}'
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    assert digest == hashlib.sha256(original.read_bytes()).hexdigest(), f'Artwork changed during import: {path.name}'
    with Image.open(path) as image:
        image.verify()
    metadata_path = path.with_suffix('.atlas.json')
    if metadata_path.exists():
        data = json.loads(metadata_path.read_text(encoding='utf-8'))
        with Image.open(path) as image:
            assert image.mode == 'RGBA' and image.getchannel('A').getextrema() == (0,255), f'Alpha missing: {path.name}'
            assert data['sourceSha256'] == digest, f'Stale atlas: {path.name}'
            assert len(data['frames']) == data['columns'] * data['rows']
            assert data['scale'] > 0
            assert data['walk'] and all(0 <= r < data['rows'] for r in data['walk'] + [data[k] for k in ('idle','windup','strike','hit')])
            for x,y,w,h,px,py in data['frames']:
                assert w > 0 and h > 0 and x >= 0 and y >= 0 and x+w <= image.width and y+h <= image.height
                assert 0 <= px <= w and 0 <= py <= h
                assert image.getchannel('A').crop((x,y,x+w,y+h)).getbbox(), f'Empty frame: {path.name}'
                frames += 1
    images += 1
print(f'PIXEL_ASSETS_OK {images} original/runtime pairs; {frames} frames; source hashes, alpha, pivots, rectangles and animations pass')
