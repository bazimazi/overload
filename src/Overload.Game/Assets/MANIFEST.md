# Overload asset provenance

## Pixel presentation, 8 October 2026

The current renderer uses twelve original/runtime PNG pairs in `assets/source/pixel-v2` and `Assets/Pixel`: six environments (Hearth, Court, Ash, Glass, Hollow, Crown), three Frames, one ordinary-enemy atlas, one regional-boss atlas and one prop atlas. These AI-generated bitmap assets use the built-in image_gen tool. Recorded prompts, including the new Hearth prompt, are in `assets/source/pixel-v2/generation.json`; four earlier sheets do not have a recorded prompt in that file. This is an honest provenance gap, not an assertion of hand-authored source art.

Source PNGs are retained byte-for-byte. `build/import-pixel-atlases.py` measures alpha silhouettes and foot pivots rather than assuming that generated grids are exact. `build/validate-pixel-assets.py` checks every source/runtime hash, all 328 frame rectangles, alpha, pivots and animation rows. Worlds render at 640×360 logical pixels with nearest filtering and integer output scaling; the interface renders at output resolution. Animation is tied to actual action phases with distance-driven walking, anticipation, follow-through, hit recoil and death fades. The atlases remain finite generated pose sets; final artist-directed animation is not claimed.

`build/generate-palimpsest-audio.py` creates the current score, ambience and combat cues from original synthesis. The percussion layer shares the 48-second musical loop and fades in during encounters; heavy tells have a rate-limited warning cue. Peak and loop metadata are in `Assets/Audio/manifest.json`. No downloaded samples or commercial music were used. Earlier SVG and sound sources are preserved below as historical provenance.

The six original slice SVG illustrations and seven WAV recordings in this directory are original project assets authored for Overload on 6 October 2026. No downloaded imagery, samples, melodies, fonts or third-party asset packs were used.

Editable source and reproducible generator: `build/generate-slice-assets.py` (Python standard library). SVGs use an authored limited palette and crisp edges; Godot imports them as textures. WAVs are mono 22,050 Hz signed 16-bit PCM synthesized from oscillators and seeded noise. The bell motif, ambience, attack/hit transients and UI/memory cues are representative slice assets, not final production audio mastering.

| Files | Purpose |
| --- | --- |
| warden, pursuer, brute, caster, bellkeeper `.svg` | Character silhouettes and material palettes |
| court-banner.svg | Court motif and environment decoration |
| music.wav / ambience.wav | Looping bell motif and court atmosphere |
| strike.wav / hit.wav | Attack and impact feedback |
| memory.wav / ui.wav / bell.wav | Memory acquisition, menu confirmation and boss arrival |

The procedural arena floor, warnings, memory markers and effect geometry remain code-authored in `WorldView` and `CombatEffects` so their presentation follows collision state. Godot's bundled fallback font is used; engine/component/font attribution and license texts are included in `GODOT-NOTICES.txt` in the review package. Its matching self-contained .NET runtime license and notices are included separately.

## A03 expansion artwork

The two additional Frame SVGs and 32 named regional enemy/boss SVGs are original editable repository assets authored on 6 October 2026. Six ordinary silhouette templates share four regional palettes; bosses use eight authored crest/body combinations. Their full file inventory and authorship are in `assets/source/manifest.json`. No external artwork or samples were used. Regional floor palettes and motifs are drawn by WorldView; wells, echo, marks and warnings follow gameplay geometry in the effect adapters. These are prototype silhouettes, not a completed final sprite-animation pipeline.
