# Connected world implementation

Implemented 8 October 2026 following the [Diablo and Path of Exile study](WORLD_MAP_RESEARCH.md) and [world design](WORLD_MAP_DESIGN.md). This is the implementation record for W01–W06. The feature is playable; independent player acceptance and the commercial art/feel bar remain open.

New Standard characters explore Hearth and four connected regions through seventeen authored zones. Ash introduces the campaign; Glass and Hollow can follow in either order; Crown requires both resolutions. Eight existing regional bosses now occupy named places in that geography. Completing Crown presents two convergent endings and opens continuing Fractures.

## Playing

Select **Explore the world** at Hearth. Walk to the west road and press **G** to cross into Cinderroad. The upper gantry and lower service road reconnect. Clear the lower worker court and interact with Sootwake Refuge to restore a waypoint, survivors, and the direct return service gate. Defeat Bellkeeper in Bellhouse, restore both defended devices in Cooling Works, and reach Forgeheart in Kilnheart. Ash's resolution opens the aqueduct and Archive service road.

**Tab** toggles the discovered local map; **M** opens the regional map. Controller equivalents are **Back/View**, **D-pad Right**, and **D-pad Up** for interaction. **B** returns from map menus. Bindings appear in Controls and use the existing conflict checks. The local overlay continues simulation; the regional menu pauses it. Geography and gate reasons remain available without hovering or relying only on color.

Rest at an activated waypoint or reclaimed refuge. Nearby danger blocks rest and fast travel. Ordinary zone transitions carry Life, Focus, and flasks; safe rest restores them. Zone exit clears temporary memories, reservations, projectiles, and effects. Death or leaving exploration resumes the saved safe checkpoint and retains committed rewards and world changes. Fast travel reaches activated safe waypoints only.

Mara, Iven, and Sen explain the Pattern's erasures through optional short exchanges. Reclaimed refuges acquire survivors and an ally; restored regions change their conduit lighting, ambience, travel access, and objective text. Players can complete every required path with any Frame and ordinary Traverse.

## Delivery

| Work | Implemented behavior |
| --- | --- |
| W01 | Shared `LevelGeometry` bounds, terrain footprints and anchors; bounds-aware navigation and Elsewhere connectivity; physical collision/projectile authority; clamped following pixel camera and correct mouse/window transforms. Legacy courts retain their geometry adapter. |
| W02 | Materialized active/safe zone snapshots, persistent discovered/visited places and fog, actual exits, activated waypoints, regional/local map views, safe resume and schema-6 migration. |
| W03 | Walkable Hearth, multi-screen Cinderroad with a real reconnecting fork, Sootwake reclamation/shortcut, Cooling Works' defended devices, Bellhouse/Bellkeeper, and Forgeheart's regional consequence. |
| W04 | `fracture.v4` / `maps.v1`: materialized physical graphs, objective receipts, full-width SplitMix64 seeds, bounded rooms/ports/loops, optional caches, graph completion and exact wallet/frontier/qualification authority. Older Fractures keep their seven-group adapter. |
| W05 | Modular quiet floor crops, regional masonry/terrain drawing, four original landmark sprites, refuge states, recurring NPC exchanges, objective guidance, synthesized regional ambience, map/controller/text-scale integration and asset provenance. |
| W06 | Glass's causeways/observatory/galleries/basin, Hollow's approach/index/unwritten wing/vault, Crown's road/bastion/bridge/chamber, complete story gates and endings, new-character production activation, preserved legacy campaigns and endgame qualification. |

The regional kits combine existing original floor/actor assets, geometry-driven masonry and an added landmark atlas; this is not a large artist-authored tile library. The campaign supplies authored routes and persistent places, not a seamless streamed continent. No multiplayer, mounts, seasons, trading, new currencies, classes, or talent trees were added.

## Authority and persistence

`WorldContent` contains typed, versioned authored definitions, not a loose decoration-only graph. Exits, encounters, interaction anchors, blockers and reward manifests are defined together. `WorldRules` validates quest/travel prerequisites and commits rewards, resolved phases, discoveries and milestones in the same character aggregate. The game adapter validates proximity and live defenders before requesting those changes.

Schema 6 adds the optional world aggregate and optional Fracture map variant. Migration preserves old eight-room and sixteen-checkpoint journeys without inferring new exploration. A completed legacy campaign can explicitly enter the geography with its new first-clear rewards suppressed. Existing endgame access, balances, proofs, mastery, oath ownership and equipment remain intact. Unknown future world/generator versions preserve save files under the existing recovery policy.

The active and safe campaign geometry snapshots are persisted; Fractures freeze geometry, graph edges, objective identities, content/generator versions and a digest. Reload does not regenerate an active map from a changed RNG implementation. Durable encounter/site claims prevent duplicate rewards after alternate routes, death, retries, or transaction-receipt pruning. Fog reveals immediately in memory and is flushed periodically and at travel/menu boundaries. Temporary combat state is not serialized.

The world adapter activates nearby packs, limits live actors to twelve, and retires distant unclaimed packs. Reentering an unclaimed pocket restores its defenders. Claimed pockets remain cleared. Director schedules and navigation radius caches are bounded; retired actor schedules are removed. A larger tier changes numbers, not pack counts or warning speed.

## Economy

The four regional main manifests preserve the sixteen-checkpoint base totals: **43,200 XP, 4,600 gold, 128 Alloy, sixteen item grants**. Ash's main manifest is **6,000 XP, 550 gold, 32 Alloy, four items**. Main-road encounters can be bypassed; their unclaimed rewards are forfeited rather than duplicated by another route. Each region has a separate optional manifest of **900 XP, 120 gold, eight Alloy and one item**. Existing inscription bonuses and full-bag conversion remain separate.

Skill milestones occur once per regional resolution. Campaign exploration grants no Seals, breadth proofs, mastery shortcuts or free oath ownership. Crown resolution supplies campaign completion, preserving the remaining earned ritual requirements.

New Fractures allocate **80% of the existing XP budget to required objectives and 20% to two optional guarded caches**, with exact remainder allocation. Unclaimed cache XP is forfeited on completion. Gold, Alloy, targeted loot, frontier/chapter advancement, six eligible Seals, breadth bonuses, mastery and three-leg chain policies retain their existing authority. Longer journeys can change reward per minute; real session duration and oath grind require playtest measurement rather than an automated-clear estimate.

## Verification and artifacts

Run `build/test.ps1 -Engine` for domain, content, generation and all six engine suites. `dotnet run --project src/Overload.Tools -- world-report artifacts/world` emits the 10,000-seed geometry/pacing report. `build/world.ps1 -Review` runs the rendered world walkthrough; `-Performance` and `-Soak` run isolated quality fixtures. `build/export.ps1 -SmokeTest` creates the self-contained Windows build and verifies its existing adapters; the exported executable also accepts `--world-smoke`.

Evidence is written under `artifacts/world`: seed/summary reports, engine logs, rendered captures, display cases, performance and endurance JSON. The latest build manifest is `artifacts/LATEST-WORLD.json`. Earlier experience/candidate manifests remain historical evidence.

Executed on the development PC: **228 domain/content tests**, all six standalone integration suites, a rendered three-Frame campaign and three-activity walkthrough, **71 world captures** including six map display/text-size cases, and **13 validated PNG pairs / 332 frames**. A **10,000-seed** physical flood report found **zero unreachable maps** and 238 distinct topologies; shortest required routes ranged from 1,600 to 3,776 pixels. The shortest/longest reported seeds were also rendered, with eighteen raster assertions checking that every room appears in the local map.

The isolated 1280×720 Compatibility sample on an Intel Arc 140T measured **9.17 ms p95 / 9.44 ms p99** across 3,599 frames after ten seconds of warmup. A **600-second accelerated simulation** kept twelve live actors and roughly 30 KB saves; it completed in 18.84 wall seconds. These are local fixture measurements, not a real-time endurance session or evidence for other hardware. Exact suite counts, runtime hashes and report paths are recorded in the build manifest.

Traversal fixtures walk actual collision bodies through both Ash routes and the full campaign for all Frames. Direct fixture defeats test reward and quest receipts; they do not prove human combat feasibility or enjoyment. Synthetic keyboard/controller events test map opening, pause, Back and focus. Physical-controller feel, route/task comprehension, landmark recognition, optional-route appeal, campaign session lengths, audio mastering, and additional hardware remain independent acceptance work.
