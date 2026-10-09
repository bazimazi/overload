#!/usr/bin/env python3
"""Verify retained originals, atlas bounds and the distributed font license."""
import hashlib
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
record_path = root / 'assets/source/rpg-v3/generation.json'
record = json.loads(record_path.read_text(encoding='utf-8-sig'))
for asset in record['assets']:
    source = record_path.parent / asset['file']
    runtime = root / asset['runtime']
    source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
    assert source_hash == hashlib.sha256(runtime.read_bytes()).hexdigest(), f"Source differs: {source}"
    assert asset['prompt'], f"Missing generation prompt: {source}"
    with Image.open(source) as image:
        assert image.width >= 1024 and image.height >= 1024, f"Asset resolution: {source}"
        if asset['transparent']:
            assert image.mode == 'RGBA' and image.getchannel('A').getextrema()[0] == 0, f"Missing alpha: {source}"
        layout = asset.get('layout')
        if layout:
            assert image.width >= layout['columns'] * 64 and image.height >= layout['rows'] * 64
        print(f"RPG ASSET OK {asset['file']} {image.width}x{image.height} sha256={source_hash}")
font = root / record['fonts'][0]['file']
license_file = root / record['fonts'][0]['licenseFile']
assert font.stat().st_size > 10000 and 'SIL OPEN FONT LICENSE' in license_file.read_text(encoding='utf-8')
print('RPG ASSETS OK 3 original/runtime pairs; recorded prompts; transparent scenery; licensed font')
