# Open-world overhaul

Implemented 9 October 2026. Launch `artifacts/windows/Overload.exe` with its complete runtime folder. The build prints `OVERLOAD_READY open-world.2026-10-09` at startup. The source workspace is `C:\dev\github\bazimazi\overload`.

## What the player sees

World characters enter the playable starting zone, Hearth Junction, immediately on launch. Hearth is charted and safe, with NPCs, a waypoint, a west road into the campaign, and a southern trail into the Endless Frontier. Existing legacy journeys have a prominent **Start open world · separate level-1 character** action; their saved characters remain available.

The minimap stays visible during exploration and follows the character. It shows discovered floor, walls, roads, nearby enemies, landmarks, facing, the selected route and its distance. **Local map** and **World map** buttons are beside it. **Tab** opens the local map; **M** toggles the world atlas. Maps stop the simulation. Escape or controller Back closes the atlas; controller B also closes the local map.

On the local map, use the wheel to zoom, middle drag to pan, left click to track a landmark or pin discovered walkable ground, and right click to resume and walk there using collision-aware navigation. Keyboard movement cancels automatic walking. **T / D-pad Left** cycles known landmarks. Hidden ground and caches remain undiscovered until explored. The atlas marks the current location with a green ring, identifies the starting zone, shows the connected campaign and frontier, and provides travel to activated safe waypoints.

## Geography and progression

The four campaign outdoor zones expand from **1920 × 1152** to **5760 × 3456** pixels: nine times the area, approximately 86 camera screens. Roads, collision footprints, entrances, return arrivals, encounters and refuges use the expanded coordinates. Each landscape has twelve additional optional defender packs. Existing campaign interiors, boss arenas, story gates and main reward budgets retain their roles.

The frontier opens from level 1 without finishing the campaign or using the endgame board. Each reach is **6144 × 3840** pixels, approximately 102 camera screens, with a broad main road, northern and southern loops, generated regional structures, 21 defender packs, an optional guardian, guarded caches, a camp and a Hearth return conduit. The east trail enters the next reach; the west trail returns to the previous one. Four regional biomes cycle and layouts depend on the campaign seed and reach. Danger increases by one tier every three reaches. Frontier guardians do not resolve campaign regions or award oath qualifications.

This is connected procedural wilderness with explicit transitions between regions. It is not a seamless infinite terrain simulation. The biome art, road templates, enemy families and landmark types recur; further authored density, encounter pacing and variety remain product work.

## Persistence and technical work

Leaving exploration or quitting saves the character's actual world position. Death still returns to the last safe checkpoint. Camps restore supplies and provide a world-map return destination. Geography upgrades old world snapshots, positions and fog without resetting character power, story claims or activated waypoints. Old Hearth snapshots gain the frontier road.

Frontier rewards and discovery persist per reach and are idempotent across revisits and reloads. The latest 32 surveys are retained to bound save growth. Earlier terrain remains reproducible and traversable, but its reward opportunities are retired and its detailed fog is discarded; this also forfeits optional loot left behind in those old reaches. The last activated camp remains available for travel. The 80-reach regression verifies bounded surveys and prevents retired reward duplication.

Large landscapes use a visibility graph around expanded collision rectangles, so pathfinding scales with structures rather than total floor area. Enemy packs activate near the character and retire outside exploration range. Scenery is deterministic and culled to the camera. The minimap and local map share fog-aware, clipped terrain and road rendering.

## Verification

Run:

```powershell
dotnet build Overload.sln -c Release -p:RestoreLockedMode=true
dotnet test tests/Overload.Domain.Tests -c Release --no-build
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/open-world.ps1 -Exported -Rendered
powershell -NoProfile -ExecutionPolicy Bypass -File build/world.ps1 -Exported -Performance -Seconds 30
```

The new suite uses isolated saves, real map input, real collision walks from Hearth through two frontier reaches, camp activation, backtracking, waypoint travel, disk reload, hidden-pin rejection, and post-campaign guidance. Rendered captures cover 960 × 540, 1280 × 720 and 1920 × 1080 at 100% and 125% text. `frontier-entire-terrain-fixture.png` deliberately reveals all terrain for review; ordinary exploration keeps fog.

Validation passed: **238 domain/content tests**, the existing exported engine suites, **36 headless and 36 rendered open-world checks**, **52 headless and 61 rendered identity checks**, and **10 RPG interface checks**. The final 1280 × 720 frontier sample with twelve active actors measured **p95 9.03 ms / p99 9.84 ms** over thirty seconds after ten seconds of warmup on the local Intel Arc 140T. An accelerated **600 simulated seconds** completed in approximately nineteen wall-clock seconds with bounded actors and saves. This is local hardware and simulation evidence.

Local previews: [starting zone and minimap](../artifacts/open-world/starting-zone-minimap.png), [world atlas](../artifacts/open-world/world-atlas-start.png), [frontier camp](../artifacts/open-world/frontier-camp-minimap.png), [local terrain map](../artifacts/open-world/frontier-entire-terrain-fixture.png).

Evidence is under `artifacts/open-world`, with build and runtime hashes in `artifacts/LATEST-OPEN-WORLD.json`. Automated fixture defeats establish traversal, persistence and interface behavior, not human combat quality or Diablo/PoE-scale content quality.
