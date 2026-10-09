# Overload

**9 October graphics overhaul:** **Play Overload.cmd** launches `graphics.2026-10-09`. The world renders at 1280×720 with sharper characters, blended earth/moss/stone materials, feathered navigable paths, forests and ruins on solid terrain, projected actor shadows, and local lights. Hearth has a forge and depot; Mara, Iven and Sen have distinct civilian sprites. Roofs and canopies fade when they cover the player. [Changes, artwork prompts and verification](docs/GRAPHICS_OVERHAUL.md). Current runtime evidence: `artifacts/LATEST-GRAPHICS.json`.

**Previous combat rhythm and clarity pass:** Basic hits restore Focus; three hits empower the next damaging skill. Held mouse attacks continue through nearby enemies, held skill keys repeat, and red Life globes reward moving through cleared packs. A simpler opening menu and class selection lead directly into Hearth. [Gameplay and verification](docs/COMBAT_RHYTHM.md).

**Previous ARPG combat and playability pass:** Left click ground to move, enemies to approach and attack, and landmarks to interact; **Alt + LMB** attacks while standing your ground. Denser patrols, marked elite hazards, immediate defensive cancellation of basic attacks, faster exploration, and Reaving / Split Volley / Deathburst legendaries deepen combat. Equipment comparisons show damage, Life and armor changes immediately. [Controls, gameplay and full-campaign verification](docs/ARPG_GAMEPLAY.md).

**9 October adventure gameplay overhaul:** launch **Play Overload.cmd**. Start in Hearth, take Mara's first patrol quest, collect visible loot, compare and equip it with **I**, train with **K**, and follow the quest journal with **J**. **P** channels a town portal and returns to the same place. Repeatable Trailkeeper contracts open fresh frontier reaches and pay legendary gear with named powers. Story / Adventurer / Veteran difficulty is available in Hearth. [Gameplay and verification](docs/ADVENTURE_GAMEPLAY.md).

**9 October movement and animation upgrade:** short acceleration/turning ramps, prompt braking, precise click arrivals, **held right-mouse steering**, distance-driven walk cycles, eased combat poses, and a camera synchronized with interpolated sprites. Launch **Play Overload.cmd**. See [movement changes and verification](docs/MOVEMENT_ANIMATION.md).

**9 October open-world overhaul:** launch `artifacts/windows/Overload.exe`. World characters start inside safe Hearth Junction with an always-visible minimap and clickable map controls. **M** opens the world atlas; **Tab** opens a zoomable local map with pins and right-click path movement. Campaign landscapes are nine times larger; Hearth's southern trail enters connected **6144 × 3840** frontier reaches that continue east from level 1. Older world saves upgrade their geography automatically. See [open-world behavior and validation](docs/OPEN_WORLD_OVERHAUL.md).

The 9 October ARPG presentation overhaul adds painted skill/item art, an ornate orb HUD, equipment and progression panels, an interactive world atlas, regional architecture, and right-click path movement. **C / I** inspect your character/equipment during play. See [changes and validation](docs/RPG_PRESENTATION.md). Launch the updated Windows build at `artifacts/windows/Overload.exe`; keep the complete folder together.

Overload is a Godot C# action RPG prototype about learning new ways to act and earning the right to rewrite one character rule. New characters explore seventeen connected zones across Hearth and four regions, reclaim refuges, restore infrastructure, and face eight bosses before the First Pattern finale. Fractures add seeded physical branches and Hunt/Breach/Vault objectives. Three Frames, 24 skills, nine signatures and two earned oaths retain their combat rules. Earlier room-based saves remain resumable. Human playtest and release-quality gates remain open.

## Play the prototype

On this workspace, launch **`artifacts/windows/Overload.exe`**. Keep the entire `artifacts/windows` folder together: the executable needs its `.pck` and managed runtime directory. The export includes the .NET runtime and does not require the editor.

The current build uses Compatibility/OpenGL rendering. Additional hardware validation remains open.

The current build is identified by **`artifacts/LATEST-GRAPHICS.json`**. `LATEST-COMBAT.json` and `LATEST-ARPG.json` record the preceding combat passes. `LATEST-ADVENTURE.json` and `LATEST-MOVEMENT.json` record the previous passes. Double-click **`Play Overload.cmd`** in the repository root to launch it. [Open-world changes and evidence](docs/OPEN_WORLD_OVERHAUL.md) describe the starting zone, large campaign landscapes, continuing frontier and map navigation. `LATEST-OPEN-WORLD.json`, `LATEST-RPG.json`, `LATEST-IDENTITY.json`, `LATEST-WORLD.json`, `LATEST-EXPERIENCE.json` and `LATEST-CANDIDATE.json` record earlier passes. Human release acceptance remains open.

To build from source on Windows, install .NET SDK **10.0.401**, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/setup.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/run.ps1
```

Setup downloads and checksum-verifies Godot **4.7.2 .NET** and matching templates into the local toolchain. Downloads total about 1.3 GB and are excluded from source control. `build/run.ps1 -Editor` opens the editor. The root `Overload.sln` builds all projects; `src/Overload.Game/Overload.Game.sln` is the editor/export entry point.

| Action | Keyboard / mouse | Controller |
| --- | --- | --- |
| Move / aim | WASD / mouse | Left / right stick |
| Navigate / steer | Left click ground / hold mouse; right mouse also works | Left stick |
| Approach and attack | Click an enemy; hold to repeat | Left stick / RB |
| Attack while standing ground | Alt + left mouse | Hold RB |
| Three equipped active skills | Q / E / R | X / Y / B |
| Evade / flask | Space / F | A / LB |
| Preserve memories while acting | Hold Shift | Hold left trigger |
| Track a discovered landmark | T | D-pad Left |
| Pause | Escape | Start |
| Menu navigation / confirm / back | Arrows / Enter / Escape | D-pad / A / B |
| Interact / nearby loot | Click a landmark or loot label; G nearby | D-pad Up |
| Inventory / character | I / C | Pause menu |
| Skills / quest journal | K / J | Pause menu |
| Town portal / return | P | Pause menu |
| Local / world map | Tab / M | Back / D-pad Right |
| Diagnostics | F3 | Keyboard only |

Fresh installs start a level-1 Standard Warden in the connected campaign. Follow the gold marker to Mara, press **G** to accept the first quest, then take the west road. **Tab** opens the discovered local map; **M** opens regional travel. Controller: **D-pad Up** interacts, **Back/View** opens the local map, **D-pad Right** opens regional travel, **B** returns. Safe waypoints restore supplies and permit fast travel. Ash opens Glass and Hollow in either order; their resolutions open Crown. Older eight-room and sixteen-checkpoint journeys resume through their original adapters. See [World implementation](docs/WORLD_IMPLEMENTATION.md).

Each Frame has a free basic and three equipped active skills from its eight-skill collection. Rank skills, choose techniques, learn twelve connected talent nodes, equip two inscriptions, compare/forge six gear slots, or respec at Hearth. Threadseer places delayed wells and looms; Revenant commands one echo and uses ranged pressure. Control/utility casts keep their authored behavior and cannot dispatch direct-damage patterns. Character XP and post-60 Resonance have no designed level cap. Inventory, skills, talents and loadouts are editable in paused field menus; forging and salvage require Hearth.

Completing the campaign opens **Fracture Atlas and attunement**. New expeditions have real branching routes, required Hunt/Breach/Vault objectives and optional guarded caches at any unlocked tier. Required objectives allocate 80% of fixed XP; caches allocate 20%, forfeited if left behind. Completion retains exact gold, Alloy, targeted equipment and frontier/chapter rules. Older seven-group saves keep their original checkpoints.

The board offers Hunt, Breach and Vault in Ash, Glass, Hollow and Crown. Tier 10+ earns regional Seals and breadth proofs. A Standard character can earn Elsewhere through level 100+, campaign completion, four mastery quests, twelve proofs, 120 Seals per region and the normalized Trial of Contradiction. Use **Create separate Standard character** at Hearth for a level-1 start without synthetic currency. New Stillness/Rupture patterns come from mastery and the trial. Chains bank three regional legs, while Anomaly Hunts disclose a chosen boss mutation and Sovereign Echo rewards are cosmetic/Codex records.

After earning a first oath, complete Red Covenant's normalized reservoir challenge and pay 30 Seals from every region. Covenant reserves 0.4% maximum Life per base Focus point for four seconds, capped at 40%; expiry restores capacity without healing. Focus regeneration is inactive. Only one oath can be active. Optional Unfinished is cut after its design/QA spike.

The Field guide teaches controls, Frames, memories, patterns and oath tradeoffs. Settings include six audio channels, fullscreen, 100%/125% text, high contrast, reduced hit flashes and room guidance. Window focus loss or controller disconnect pauses combat. Menus scroll with the mouse wheel or follow controller/keyboard focus. Keyboard remapping persists and rejects key conflicts. Saves use checksums, atomic replacement and three backups; recovery can preserve damaged files and create a separate character.

While engaged, travel **3 meters within 2 seconds** to earn Momentum, or evade an actual hostile hit to earn Echo. Each memory lasts five seconds, refreshes rather than stacks, and has a two-second generation cooldown. Pause freezes these timers; death and encounter exit clear them. The HUD shows stored memories and expiry.

The arena equips three patterns. Spend Momentum on an Assault for **Pursuit** (advance up to 2 meters, then strike a narrow lane for 110% damage), or on evade for **Crossing** (travel up to 5 meters through enemies, with terrain still blocking). Spend Echo on an Assault for **Afterstrike**: two 60% strikes, 0.35 seconds apart, from the first strike's position and aim. Pursuit takes priority when both memories are ready; blocked or unaffordable patterns fall through to the next legal pattern or base action.

Pursuit/Crossing add 25 Strain and Afterstrike adds 30, up to 100. Strain decays at 12 per second after one second without a successful pattern. Each action pays once, and overloaded effects cannot generate new memories. The HUD predicts the next action through the shared resolver and shows fallback reasons; edit three bindings at Hearth.

The other patterns are **Convergence** (both memories, 45 Strain, a pull and 135% cone), **Shelter** (Echo, 30 Strain, a departure field intercepting two ordinary bolts) and **Reprieve** (Echo, 25 Strain, one flask for 25% healing plus a temporary 15% barrier). **Elsewhere** replaces Traverse with placement and return: placement has no evasion, Crossing is suspended, and Shelter can wrap the anchor phases. Practice it before using the explicitly sandboxed ritual.

```powershell
# Build, pure tests, content validation, and engine integration checks
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1 -Engine

# Produce the self-contained Windows build and run its integration checks
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest

# Quality reports, rendered matrix, accelerated soak and measured stress
dotnet run --project src/Overload.Tools -c Release --no-build -- quality-report
powershell -NoProfile -ExecutionPolicy Bypass -File build/quality.ps1 -Exported

# Archive a review candidate with source, hashes and evidence
powershell -NoProfile -ExecutionPolicy Bypass -File build/release.ps1 -SkipExport
```

See [implementation status and evidence](docs/STATUS.md) and [the foundation decision](docs/decisions/0001-arena-foundation.md). Human combat feel and physical-controller validation remain open; passing automated checks is not a playtest.

Prepared on 5 October 2026. All balance values, playtime estimates, content counts, and production estimates are initial proposals to validate through prototypes. Research describes the specific linked releases, not an exhaustive account of either reference game's current state.

## Reading order

1. [Game design](docs/GAME_DESIGN.md) — player experience, progression, items, world, art, accessibility, and release scope.
2. [Overload and Override specification](docs/CORE_SYSTEMS.md) — the defining mechanics, exact resolution rules, examples, acquisition, and abuse cases.
3. [Endless progression](docs/ENDLESS_PROGRESSION.md) — uncapped levels, XP, repeatable growth, equipment attunement, difficulty scaling, evolving encounters, and numerical safety.
4. [Implementation plan](docs/IMPLEMENTATION_PLAN.md) — architecture, milestones, agent tasks, tests, production estimates, and release gates.
5. [Research and decisions](docs/RESEARCH.md) — primary sources, their implications, alternatives, and unresolved assumptions.
6. [World and map research](docs/WORLD_MAP_RESEARCH.md) — Diablo II–IV and PoE 1–2 comparison, versioned sources, and audit of the current world.
7. [Connected world proposal](docs/WORLD_MAP_DESIGN.md) — regional geography, Foundry scenario, explorable maps, persistence, and W01–W06 delivery plan.

## Working decisions

| Area | Proposed baseline |
| --- | --- |
| Engine | Godot .NET, C# domain logic, Windows desktop first |
| Mode | Offline single-player; co-op requires a separate architecture decision before production |
| Presentation | 2D oblique top-down pixel art, readable combat and independently scaled UI |
| Progression | Skills, talents, Codex, equipment, Overload, and rare Override each have separate responsibilities |
| Levels and power | No maximum character level; every level after 60 grants an uncapped Resonance point |
| Difficulty | Fracture tiers increase indefinitely, with repeatable encounter chapters and no final tier |
| Overload | Equip character-wide signatures that spend combat memories to select alternate implementations of the same action |
| Override | Permanently earn one active replacement for a fundamental character contract through endgame breadth and mastery |
| First playable | One room, one hero, three skills, three Overloads, one boss, and a debug-accessible Override |
| First external slice | A polished 30–45 minute experience with a saved progression loop |
| Release direction | Premium RPG with an authored campaign and endless endgame; no live-service infrastructure in the baseline |

The owner confirmed Godot with C#, Windows first, and offline single-player on 5 October 2026. Co-op is a later, separately scoped decision. The implementation agent must check for subsequent decisions before scaffolding. The empty workspace contained no existing engine project or repository instructions when inspected.

The owner also requires uncapped levels, ongoing power progression, and uncapped difficulty. Level 60 is a foundation milestone, not a maximum; tier 10 is an early endgame milestone, not an endpoint. This requirement replaces the original finite progression proposal. Prototype playtime and authored content budgets do not cap the progression systems.

## Experience upgrade

The current playable pass adds pixel-art environments and Frames, a distinct Hearth, action-phase animation and effects, tactical enemy navigation, contextual Overload coaching, signature selection, adaptive audio and continuous campaign checkpoints. After a room is saved, walk into the exit or press **G / D-pad up** to continue; **Q / X** equips the reward and continues. Pause provides the reward comparison and a return to Hearth. These bindings can be remapped. See [experience changes and verification](docs/EXPERIENCE_UPGRADE.md).

Launch the self-contained Windows candidate at `artifacts/windows/Overload.exe`, keeping its entire folder together. Source builds use the pinned Godot .NET editor; set `OVERLOAD_GODOT` if the editor is outside `.tools`. Run `build/review-experience.ps1 -Exported -Rendered` for isolated navigation/checkpoint checks and screenshots of the candidate.

## Implementation scope

The current assignment extends through **R02** in [the implementation plan](docs/IMPLEMENTATION_PLAN.md). Quality/release implementation and evidence are recorded in [Q/release validation](docs/Q_RELEASE_VALIDATION.md). S06 playtesting, human release acceptance and the real player evidence needed to choose an expansion remain open. No expansion content or public release is claimed.

Use `docs/CORE_SYSTEMS.md` as the authority for Overload/Override edge cases, `docs/ENDLESS_PROGRESSION.md` for progression and scaling, `docs/GAME_DESIGN.md` for product scope, and `docs/IMPLEMENTATION_PLAN.md` for execution order. Record deliberate changes in a decision log when development begins.

A03 implementation decisions and verification limits: [validation report](docs/A03_VALIDATION.md), [status](docs/STATUS.md), [Frame/region/oath decision](docs/decisions/0008-frames-regions-covenant.md).
