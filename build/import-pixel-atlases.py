"""Inspect original generated PNGs and export foot-pivot metadata without modifying the artwork.

Requires Pillow (python -m pip install pillow). Source alpha, row boundaries and every
rectangle are validated. Source/runtime PNGs remain byte-identical. No guessed grid slicing.
"""
import argparse
import hashlib
import json
import shutil
from pathlib import Path
from statistics import median
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / 'assets/source/pixel-v2'
TARGET = ROOT / 'src/Overload.Game/Assets/Pixel'
LAYOUTS = {'warden': (8, 7), 'threadseer': (8, 8), 'revenant': (8, 8), 'enemies': (8, 8), 'bosses': (8, 8), 'props': (4, 4)}


def components(image):
    width, height = image.size
    alpha = bytearray(image.getchannel('A').tobytes())
    found = []
    for start in range(len(alpha)):
        if alpha[start] < 80:
            continue
        alpha[start] = 0
        stack = [start]
        xs, ys = [], []
        while stack:
            index = stack.pop()
            x, y = index % width, index // width
            xs.append(x); ys.append(y)
            for neighbor in (index - 1 if x else -1, index + 1 if x + 1 < width else -1, index - width, index + width):
                if 0 <= neighbor < len(alpha) and alpha[neighbor] >= 80:
                    alpha[neighbor] = 0
                    stack.append(neighbor)
        if len(xs) > 400 and max(ys) - min(ys) > 35:
            found.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1, len(xs)))
    return found


def inspect(name, columns, rows, verbose=False):
    with Image.open(SOURCE / f'{name}.png') as image:
        if image.mode != 'RGBA' or image.getchannel('A').getextrema() != (0, 255):
            raise ValueError(f'{name}: real transparent alpha required')
        groups = [[] for _ in range(columns)]
        for box in components(image):
            cx = (box[0] + box[2]) / 2
            column = min(columns - 1, int(cx / image.width * columns))
            groups[column].append(box)
        if verbose:
            print(name, image.size, [[(b[0], b[1], b[2], b[3]) for b in sorted(g, key=lambda b: b[1])] for g in groups])
        for column, group in enumerate(groups):
            # First Pattern carries a disconnected void blade. Join only fragments that
            # lie inside a body's vertical extent and overlap its horizontal bounds.
            if len(group) > rows:
                main = sorted(group, key=lambda b: b[4], reverse=True)[:rows]
                fragments = [b for b in group if b not in main]
                for fragment in fragments:
                    candidates = [b for b in main if fragment[1] >= b[1] and fragment[3] <= b[3]
                                  and fragment[3] - fragment[1] < (b[3] - b[1]) / 2
                                  and fragment[0] < b[2] + 4 and fragment[2] > b[0] - 4]
                    if len(candidates) != 1:
                        raise ValueError(f'{name}: unassigned disconnected component in column {column}')
                    body = candidates[0]
                    main[main.index(body)] = (min(body[0], fragment[0]), min(body[1], fragment[1]), max(body[2], fragment[2]), max(body[3], fragment[3]), body[4] + fragment[4])
                group[:] = main
            if len(group) != rows:
                raise ValueError(f'{name}: column {column} has {len(group)} full silhouettes, expected {rows}. Inspect source; never silently import a bad grid.')
            group.sort(key=lambda b: b[1])
        frames = []
        for row in range(rows):
            for col in range(columns):
                b = groups[col][row]
                # Pad component bounds to preserve disconnected spark/weapon-edge pixels.
                x, y = max(0, b[0] - 2), max(0, b[1] - 2)
                right, bottom = min(image.width, b[2] + 2), min(image.height, b[3] + 1)
                alpha = image.getchannel('A')
                feet = [fx for fy in range(max(b[1], b[3] - 6), b[3]) for fx in range(b[0], b[2]) if alpha.getpixel((fx, fy)) >= 80]
                if not feet:
                    raise ValueError(f'{name}: empty foot pivot in row {row}, column {col}')
                pivot_x = (min(feet) + max(feet)) / 2 - x
                frames.append([x, y, right - x, bottom - y, pivot_x, b[3] - y])
        height = median(b[3] - b[1] for b in (g[0] for g in groups))
        metadata = {'columns': columns, 'rows': rows, 'scale': round(42 / height, 6), 'idle': 0,
                    'walk': [1, 2, 3, 0] if rows == 7 else [1, 2, 3, 4],
                    'windup': rows - 3, 'strike': rows - 2, 'hit': rows - 1,
                    'frames': frames, 'sourceSha256': hashlib.sha256((SOURCE / f'{name}.png').read_bytes()).hexdigest(),
                    'pivot': 'center of opaque feet in bottom six source pixels', 'facings': ['E', 'SE', 'S', 'SW', 'W', 'NW', 'N', 'NE']}
        if name == 'enemies':
            metadata['scale'] = .30
            metadata['facings'] = ['E-contact', 'E-pass', 'S-contact', 'S-pass', 'W-contact', 'W-pass', 'N-contact', 'N-pass']
        if name == 'bosses':
            metadata['scale'] = .37
            metadata['facings'] = ['E-idle', 'E-windup', 'E-strike', 'W-idle', 'W-windup', 'W-strike', 'S-idle', 'N-idle']
        if name == 'props':
            metadata['walk'] = [0]
        TARGET.mkdir(parents=True, exist_ok=True)
        (TARGET / f'{name}.atlas.json').write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
        shutil.copyfile(SOURCE / f'{name}.png', TARGET / f'{name}.png')
        print(f'{name}: {rows * columns} validated frames, alpha/pivots/bounds pass')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--inspect', action='store_true')
    args = parser.parse_args()
    for name, (columns, rows) in LAYOUTS.items():
        inspect(name, columns, rows, args.inspect)
