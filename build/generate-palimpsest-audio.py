"""Original Palimpsest score and cues. No samples or third-party melodies. Standard library only."""
from array import array
from pathlib import Path
import json
import math
import random
import wave

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'src/Overload.Game/Assets/Audio'
DEST.mkdir(parents=True, exist_ok=True)
RATE = 22050
TAU = math.tau
RNG = random.Random(176)
MANIFEST = []


def freq(midi):
    return 440 * 2 ** ((midi - 69) / 12)


def tone(t, f):
    return math.sin(TAU * f * t) + .24 * math.sin(TAU * f * 2 * t) + .08 * math.sin(TAU * f * 3 * t)


def bell(t, f):
    return sum(math.sin(TAU * f * r * t) * math.exp(-t * (2 + k)) / (k + 1) for k, r in enumerate([1, 2.71, 4.1])) * .18


def save(name, seconds, sample, loop=False):
    length = round(seconds * RATE)
    data = array('h')
    peak = 0
    for i in range(length):
        t = i / RATE
        gain = min(1, i / 160, (length - 1 - i) / 160) if loop else min(1, i / 16, (length - 1 - i) / 60)
        v = max(-.85, min(.85, sample(t))) * gain
        peak = max(peak, abs(v))
        data.append(round(v * 32767))
    with wave.open(str(DEST / (name + '.wav')), 'wb') as out:
        out.setparams((1, 2, RATE, length, 'NONE', 'not compressed'))
        out.writeframes(data.tobytes())
    MANIFEST.append({'name': name, 'seconds': seconds, 'sampleRate': RATE, 'loop': loop, 'peakDbFS': round(20 * math.log10(max(peak, 1e-9)), 2)})


CHORDS = [50, 46, 53, 48, 50, 58, 53, 48, 50, 46, 53, 55, 50, 46, 48, 45]
ARPS = [0, 7, 12, 7, 3, 7, 10, 7]


def score(t):
    bar = int(t / 3) % 16
    root = CHORDS[bar]
    local = t % 3
    breath = math.sin(math.pi * local / 3) ** 2
    pad = sum(tone(t, freq(root + j)) * .014 for j in (0, 3, 7)) * breath
    bass = tone(t, freq(root - 12)) * .035 * breath
    a = t % .375
    note = root + 12 + ARPS[int(t / .375) % 8]
    pluck = tone(a, freq(note)) * .054 * (1 - math.exp(-a * 260)) * math.exp(-a * 10)
    # A slow original four-note bell phrase sits behind the plucked ostinato.
    age = t % 6
    melody = bell(age, freq([74, 72, 69, 65, 74, 77, 72, 69][int(t / 6) % 8])) * .3
    return pad + bass + pluck + melody


save('music', 48, score, True)
def combat_layer(t):
    beat = t % .75
    drum = math.sin(TAU * (58 * beat + 2.6 * (1 - math.exp(-beat * 35)))) * math.exp(-beat * 18) * .16
    offbeat = (t + .375) % .75
    tap = math.sin(TAU * 170 * offbeat) * math.exp(-offbeat * 65) * .035
    root = CHORDS[int(t / 3) % 16] - 12
    pulse = tone(beat, freq(root)) * math.exp(-beat * 7) * .025
    return drum + tap + pulse

save('combat', 48, combat_layer, True)
save('ambience', 16, lambda t: .022 * math.sin(TAU * 55 * t) + .009 * math.sin(TAU * 82.5 * t) + .008 * math.sin(TAU * 110 * t) * (.7 + .3 * math.sin(TAU * t / 16)), True)
save('strike', .24, lambda t: .21 * (RNG.random() * 2 - 1) * math.exp(-t * 28) + .12 * tone(t, 135) * math.exp(-t * 23))
save('hit', .28, lambda t: .27 * math.sin(TAU * (95 * t - 85 * t * t)) * math.exp(-t * 23) + .14 * (RNG.random() * 2 - 1) * math.exp(-t * 34))
save('cast', .5, lambda t: .13 * tone(t, 220 + t * 600) * math.sin(math.pi * min(1, t / .5)) * math.exp(-t * 3))
save('evade', .22, lambda t: .1 * (RNG.random() * 2 - 1) * math.sin(math.pi * t / .22) * math.exp(-t * 12))
save('defeat', .65, lambda t: .12 * tone(t, 90) * math.exp(-t * 8) + .1 * (RNG.random() * 2 - 1) * math.exp(-t * 16) + bell(t, 330) * .18)
save('heal', .9, lambda t: bell(t, 523.251) * .6 + (bell(max(0, t - .14), 659.255) * .4 if t > .14 else 0))
save('overload', .65, lambda t: bell(t, 392) * .45 + (bell(t - .09, 587.33) * .45 if t > .09 else 0))
save('reward', 1.5, lambda t: sum(bell(t - delay, freq(note)) * .42 for delay, note in [(0, 62), (.13, 65), (.27, 69), (.45, 74)] if t > delay))
save('warning', .38, lambda t: tone(t, 146.83) * math.exp(-t * 13) * .09 + bell(t, 293.66) * .24)
(DEST / 'manifest.json').write_text(json.dumps({'author': 'Original synthesis authored for Overload, 2026-10-08', 'source': 'build/generate-palimpsest-audio.py', 'assets': MANIFEST}, indent=2) + '\n', encoding='utf-8')
print('Generated', len(MANIFEST), 'original score/ambience/cue assets')
