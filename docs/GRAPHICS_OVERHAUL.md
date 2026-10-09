# Graphics overhaul — 9 October 2026

Build: `graphics.2026-10-09`. Launch **Play Overload.cmd** in either the canonical repository or `C:\dev\games\overload`. The complete Windows runtime and SHA-256 evidence are recorded in `artifacts/LATEST-GRAPHICS.json`. The preceding combat runtime is preserved in `artifacts/before-graphics-2026-10-09/windows`.

## Visible changes

The world now renders at **1280×720**, with the same 640×360 world coverage. Camera zoom and mouse projection share the new scale, so aiming, travel distances, collision and attack reach retain their authored behavior. The higher resolution preserves character and scenery detail that was lost in the former enlarged 640×360 image. The interface still renders independently at the window resolution.

Outdoor floors use earth and moss materials blended in world coordinates. Broad natural variation breaks up repetition, mirrored samples hide texture boundaries, and mipmaps quiet fine details during movement. Interiors use weathered stone. Each region has its own palette; Glass has more vegetation and damp ground. Textured, feathered roads follow actual navigation routes and maintain their clear width.

Solid outdoor footprints contain trees and boulders; low grass and reeds remain passable. Interior footprints have raised masonry, pillars, ruins, crates and braziers. Hearth has a recognizable forge and supply depot. Mara, Iven and Sen have distinct civilian sprites instead of sharing player-class sprites. Existing collision geometry, quest locations and interact ranges remain authoritative.

Animated actors cast projected silhouette shadows, with contact shadows at their feet. Warm braziers and waypoint lights add local depth. Solid scenery eases to partial opacity when its canopy or roof would cover the character, then becomes opaque after the character moves clear. Hazard outlines, health bars, destination markers and map controls remain visible over the materials.

## Artwork and implementation

Six new images were created with the **built-in image_gen tool**: earth, moss, stone, a twelve-object scenery atlas, a three-person NPC atlas and a four-building settlement atlas. Original generated files were copied without raster editing. Sprite regions and foot pivots are metadata; settlement crops follow each actual connected object, including a chapel that crosses the nominal row boundary.

The full prompt set, dimensions, source paths, runtime paths and PNG digests are in [generation.json](../assets/source/material-v3/generation.json). Source artwork lives in `assets/source/material-v3`; imported artwork and the material shader live in `src/Overload.Game/Assets/Materials`. `build/validate-material-assets.py` checks exact source/runtime equality, dimensions, digests, true transparency and tight sprite regions. Earlier artwork remains available.

Ground materials use one retained surface. Routes and small scenery retain native drawing between camera cells; structures retain their geometry and are culled outside the view. Only actor presentation, player light and visible animated lights advance each frame. Decoration does not modify gameplay RNG, navigation or saves.

## Verification

`artifacts/LATEST-GRAPHICS.json` identifies the tested executable, PCK and game assembly, source files, regression logs, rendering sample and both mirrored launch folders. The visual fixture uses an isolated profile and protected Life to stage captures; it does not establish ordinary combat acceptance. The separate adventure and campaign input drivers use ordinary character stats and real combat.

The graphics review checks canopy opacity and restoration, clear road routes, native camera projection and mouse aiming at **960×540, 1280×720 and 1920×1080**. Its captures include Hearth, the frontier, every region's wilderness, dungeon and enforcer, plus local maps. The menu review covers both text sizes. Movement, combat, campaign progression and persistent inventory are checked separately on the exported runtime.

Reproduce the asset and export checks from the repository root:

```powershell
python build/validate-material-assets.py
./build/export.ps1 -SmokeTest
./build/graphics.ps1 -Rendered -Exported
./build/motion.ps1 -Rendered -Exported
./build/review-rpg.ps1 -Rendered -Exported
./build/adventure.ps1 -Rendered -Exported
./build/campaign.ps1 -Rendered -Exported
./build/world.ps1 -Performance -Exported
```

Performance evidence comes from a separate protected fixture with sixteen active enemies on a 6144×3840 frontier, the full HUD and 1280×720 output. It measures thirty seconds after ten seconds of warmup. Broader hardware and independent human/controller acceptance remain open.

Final results: **299 domain tests**, **216 Frame cases**, **10,000 generated maps with zero unreachable routes**, **28/60 graphics checks**, **38/39 movement checks**, **14/22 menu checks**, **36/36 open-world checks**, **53/62 identity checks**, **200/153 adventure checks**, and **408/396 normal-stat campaign checks** (headless/rendered). Eight boss fights appear in each campaign record. All final engine error logs are empty. The final Intel Arc 140T sixteen-enemy rendering sample passes at **p95 9.01 ms / p99 9.30 ms**, from 3,600 frames. The protected graphics review saves 25 captures; ordinary combat evidence lives separately in `artifacts/adventure` and `artifacts/campaign`.

## Reference and remaining work

Blizzard's [Diablo IV environment art update](https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022) informed the attention to materials, environmental construction and lighting. This comparison uses published design material; Diablo IV was not run alongside Overload. Camera scaling follows Godot's [Camera2D API](https://docs.godotengine.org/en/stable/classes/class_camera2d.html).

Overload remains an original 2D ARPG development build. Diablo IV's animation variety, authored environmental density, cinematic lighting, content volume and production quality remain substantial development work. These checks establish the listed behavior and local rendering results; independent player feedback is still needed to judge readability, enjoyment and pacing.

Fresh game captures: [Hearth and Mara](../artifacts/graphics/hearth-mara.png), [Glass wilderness](../artifacts/graphics/Glass-wilderness.png), [Ash dungeon](../artifacts/graphics/Ash-dungeon.png), [frontier](../artifacts/graphics/frontier.png).
