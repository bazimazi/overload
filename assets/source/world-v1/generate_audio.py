"""Original deterministic ambience, independently generated from gameplay seeds."""
import math
import random
import struct
import wave
import hashlib
import json
from pathlib import Path

RATE = 22050
DURATION = 8
ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "src/Overload.Game/Assets/Audio"

for region, base, tint in [("ash", 55, .6), ("glass", 82.5, 1.3), ("hollow", 73.33, .9), ("crown", 41.25, .4)]:
    rng = random.Random("overload-world-v1-" + region)
    noise = 0
    samples = []
    for i in range(RATE * DURATION):
        t = i / RATE
        noise = noise * .97 + rng.uniform(-1, 1) * .03
        # Frequencies and envelopes repeat at the loop boundary; low noise masks the seam.
        drone = math.sin(math.tau * base * t) * .024 + math.sin(math.tau * base * 1.5 * t) * .013
        pulse = math.sin(math.tau * (2 if region == "ash" else 1) * t) ** 8
        air = noise * (.10 + .035 * math.sin(math.tau * t / DURATION))
        detail = math.sin(math.tau * (440 if region == "glass" else 330 if region == "hollow" else 165) * t) * pulse * .011 * tint
        value = drone + air + detail
        samples.append(struct.pack("<h", int(max(-.2, min(.2, value)) * 32767)))
    with wave.open(str(OUTPUT / ("world-" + region + ".wav")), "wb") as audio:
        audio.setnchannels(1)
        audio.setsampwidth(2)
        audio.setframerate(RATE)
        audio.writeframes(b"".join(samples))

# Mechanical atlas inspection; the generated PNG is never edited.
from PIL import Image
landmarks = ROOT / "src/Overload.Game/Assets/Pixel/world-landmarks.png"
with Image.open(landmarks) as image:
    width, height = image.size
metadata = dict(columns=2, rows=2, scale=1, idle=0, walk=[0], windup=0, strike=0, hit=0,
                sourceSha256=hashlib.sha256(landmarks.read_bytes()).hexdigest(),
                frames=[[x*width/2, y*height/2, width/2, height/2, width/4, height/2]
                        for y in range(2) for x in range(2)])
landmarks.with_suffix(".atlas.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
