"""Validate imported materials, true alpha, exact copies, and sprite regions without altering artwork."""
from pathlib import Path
from PIL import Image
import hashlib
import json

root = Path(__file__).resolve().parents[1]
source = root / "assets/source/material-v3"
runtime = root / "src/Overload.Game/Assets/Materials"
manifest = json.loads((source / "generation.json").read_text(encoding="utf-8-sig"))
counts = {"scenery": 12, "villagers": 3, "settlement": 4}
for asset in manifest["assets"]:
    name = asset["name"]
    data = (source / (name + ".png")).read_bytes()
    assert data == (runtime / (name + ".png")).read_bytes(), name + " runtime differs"
    assert hashlib.sha256(data).hexdigest() == asset["sha256"], name + " manifest digest differs"
    with Image.open(source / (name + ".png")) as im:
        assert list(im.size) == asset["pixels"] and im.mode == asset["mode"], name + " format differs"
        if name in counts:
            alpha = im.getchannel("A")
            assert alpha.getextrema() == (0, 255), name + " requires actual transparency"
            assert alpha.histogram()[0] > im.width * im.height * .15, name + " background isn't transparent"
            metadata = (source / (name + ".frames.json")).read_bytes()
            assert metadata == (runtime / (name + ".frames.json")).read_bytes(), name + " sprite regions differ"
            frames = json.loads(metadata)
            assert len(frames) == counts[name], name + " frame count differs"
            for x, y, w, h, px, py in frames:
                assert w > 0 and h > 0 and 0 <= x < x+w <= im.width and 0 <= y < y+h <= im.height
                assert 0 <= px <= w and 0 <= py <= h
                box = alpha.crop((x, y, x+w, y+h)).point(lambda a: 255 if a > 8 else 0).getbbox()
                assert box == (0, 0, w, h), name + " has a loose or empty sprite crop"
    print("MATERIAL PASS:", name)
print("OVERLOAD_MATERIAL_ASSETS_OK assets=" + str(len(manifest["assets"])))
