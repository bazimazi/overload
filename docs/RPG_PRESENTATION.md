# ARPG interface and presentation overhaul

Delivered 9 October 2026. The review covered the implementation plan, HUD, Hearth, equipment, character progression, skill loadout, talents, bindings, oaths, Atlas, regional and tactical maps, settings, input, combat feedback, and world rendering. The central mismatch was that most player decisions were long text-button lists, while the connected world used conspicuous flat geometric scenery.

The resulting interface follows familiar action-RPG conventions: dark bronze frames, serif display typography, painted skill and equipment art, Life and Focus orbs, an icon action bar with cooldown clocks, equipment sockets and a bag grid, side-by-side gear comparison, native focusable talent paths, and a painted world atlas. The game retains its existing original Overload/Override rules and pixel world.

| Area | Implemented behavior |
| --- | --- |
| Shared UI | Bronze ornamentation, parchment-colored text, licensed Cinzel headings, tooltip theme, hover transitions, panel entrances, and a visible back control |
| Combat HUD | Larger glass-style resource orbs with eased liquid fill, painted active-skill artwork, radial cooldown masks, XP strip, memory timers, target plate and floor ring |
| Arsenal | Six clickable equipped sockets around a Frame figure; 36 real inventory slots; slot filtering; item tooltips; comparison panels; equip, lock, quality, affix and salvage actions |
| Character | Frame portrait and attributes beside build destinations; exact Resonance allocation and all existing progression counters remain available |
| Skills | Painted training cards, five rank diamonds, legal training/technique availability, and an explicit skill picker for each active socket |
| Talents | Three connected chains of four icon nodes; learned, available and locked states reflect actual prerequisite and budget rules |
| Bindings / oaths / Codex | Illustrated memory sockets and choices, oath cards, and equipped inscription cards |
| Atlas | Original painted geography; all seventeen campaign zones correspond to selectable discovered markers and actual graph edges; existing safe-waypoint travel remains authoritative |
| Fractures | Clickable regional atlas, activity cards and compact tier controls; exact unbounded tier entry remains available |
| Settings | Native keyboard/controller-focusable volume sliders; existing display, flash, contrast, text and guidance options |
| Field inspection | **C** opens character inspection; **I** opens equipped gear. Combat clocks stop while inspecting; build changes remain at Hearth |
| Mouse movement | **Right click** requests a path through the existing navigation and collision body. Keyboard/stick movement cancels it; blocked movement stops automatically |
| World | Textured roads, regional architecture sprites, raised structures sorted at their feet, painted waypoint/refuge props, contextual landmark nameplates, decorative props, restrained mist/dust, and stronger defeat/target/movement feedback |

Art is original generated material, produced with the built-in imagegen tool. Source PNGs, exact prompts, hashes and atlas layout notes are retained in [the generation record](../assets/source/rpg-v3/generation.json). Cinzel is distributed with its SIL Open Font License; the export script copies that license beside the Windows game.

Run the Windows build at `artifacts/windows/Overload.exe` with its entire folder. Source validation is reproducible with:

    ./build/review-rpg.ps1 -Rendered
    ./build/export.ps1 -SmokeTest
    ./build/review-rpg.ps1 -Exported -Rendered

The new review uses isolated profiles and exercises real talent and skill transactions, disk reload, pointer movement and cancellation, and inspection pause behavior. Rendered review captures 57 screens, including 960×540, 1280×720 and 1920×1080 at 100% and 125% text, plus all four regional landscapes. Images and logs are under `artifacts/rpg/` and `artifacts/rpg-*.log`. The current local [screenshot gallery](../artifacts/rpg/index.html) and [build evidence](../artifacts/LATEST-RPG.json) make the result reviewable.

Executed validation: 230 domain tests; all seven exported engine suites (126 combat/input, 89 endless, 156 production, 327 expansion, 19 experience, 863 world, and 52 Frame/Overload/Override identity checks); and 10 new ARPG interaction checks in both headless and rendered modes. The source world suite also passed 852 checks. Rendered layouts were inspected and iterated; automated checks do not establish human playtest acceptance or parity with the art, content, animation and combat depth of a large commercial ARPG.

The updated rendering also passed the local 1280×720 Compatibility fixture on an Intel Arc 140T: **9.01 ms p95 / 9.32 ms p99**, twelve live regional enemies, and 3,599 frame samples after ten seconds of warmup. This measures one scrolling-world fixture on this machine. Reproduce with `./build/world.ps1 -Exported -Performance -Seconds 30`; metrics are in `artifacts/world/performance.json`.
