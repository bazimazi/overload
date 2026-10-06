# Overload

Overload is a Godot C# action RPG prototype about learning new ways to act and earning the right to rewrite one character rule. The A03 candidate has three Frames, 24 skills, nine shared signatures, Elsewhere and Red Covenant, four regional rosters, eight bosses and 48 room templates. New characters play a sixteen-checkpoint regional campaign ending with First Pattern. Earlier court saves remain resumable. Human playtest and release-quality gates remain open.

## Play the prototype

On this workspace, launch **`artifacts/windows/Overload.exe`**. Keep the entire `artifacts/windows` folder together: the executable needs its `.pck` and managed runtime directory. The export includes the .NET runtime and does not require the editor.

The review build uses Mobile/Vulkan rendering. To select Compatibility/OpenGL explicitly on other hardware, launch `Overload.exe --rendering-method gl_compatibility --rendering-driver opengl3`. Additional hardware validation remains open.

The current review candidate is identified by **`artifacts/LATEST-CANDIDATE.json`**, with a uniquely named `Overload-rc-*-windows.zip`, its SHA256, complete source snapshot and quality evidence. Follow the [release review sheet](docs/RELEASE_REVIEW.md). Earlier A03/P07/E05/S06 archives are preserved. Use the [S06 feedback sheet](docs/playtests/S06_FIRST_PLAYTEST.md) for the independent session. S06 and human release acceptance stay open until those sessions and fixes are recorded.

To build from source on Windows, install .NET SDK **10.0.401**, then run from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/setup.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/run.ps1
```

Setup downloads and checksum-verifies Godot **4.7.2 .NET** and matching templates into the local toolchain. Downloads total about 1.3 GB and are excluded from source control. `build/run.ps1 -Editor` opens the editor. The root `Overload.sln` builds all projects; `src/Overload.Game/Overload.Game.sln` is the editor/export entry point.

| Action | Keyboard / mouse | Controller |
| --- | --- | --- |
| Move / aim | WASD / mouse | Left / right stick |
| Frame basic attack | Hold left mouse | Hold RB |
| Three equipped active skills | Q / E / R | X / Y / B |
| Evade / flask | Space / F | A / LB |
| Pause | Escape | Start |
| Menu navigation / confirm / back | Arrows / Enter / Escape | D-pad / A / B |
| Diagnostics | F3 | Keyboard only |

Fresh ordinary installs start with a level-1 Standard Warden. Choose **Create separate Standard character** at Hearth to select Warden, Threadseer or Revenant and start at level 1. Their four-region campaign has sixteen checkpoints and eight bosses. **Compare accelerated training builds** creates one of three equal-budget builds per Frame in a separate sandbox save. Older characters can still begin or resume their eight-room court. Practice encounters grant no rewards. Room clears bank XP, gold, Alloy and gear atomically; retry restores Life, Focus and flasks.

Each Frame has a free basic and three equipped active skills from its eight-skill collection. Rank skills, choose techniques, learn twelve connected talent nodes, equip two inscriptions, compare/forge six gear slots, or respec at Hearth. Threadseer places delayed wells and looms; Revenant commands one echo and uses ranged pressure. Control/utility casts keep their authored behavior and cannot dispatch direct-damage patterns. Character XP and post-60 Resonance have no designed level cap.

Completing your campaign opens **Fracture board and attunement**. Seven-group expeditions use authored rooms at any unlocked tier, bank fixed XP budgets, and unlock the next tier on completion. Spend gold and Alloy on shared attunement grades up to your highest cleared tier. Every ten tiers offers a saved chapter route with targeted equipment; known routes remain available. Character's exact ledger complements abbreviated HUD counters. To inspect tiers directly in separate test profiles, run `artifacts/windows/Overload.exe -- --fracture-practice=10` (also try 9 and 11).

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

## Implementation scope

The current assignment extends through **R02** in [the implementation plan](docs/IMPLEMENTATION_PLAN.md). Quality/release implementation and evidence are recorded in [Q/release validation](docs/Q_RELEASE_VALIDATION.md). S06 playtesting, human release acceptance and the real player evidence needed to choose an expansion remain open. No expansion content or public release is claimed.

Use `docs/CORE_SYSTEMS.md` as the authority for Overload/Override edge cases, `docs/ENDLESS_PROGRESSION.md` for progression and scaling, `docs/GAME_DESIGN.md` for product scope, and `docs/IMPLEMENTATION_PLAN.md` for execution order. Record deliberate changes in a decision log when development begins.

A03 implementation decisions and verification limits: [validation report](docs/A03_VALIDATION.md), [status](docs/STATUS.md), [Frame/region/oath decision](docs/decisions/0008-frames-regions-covenant.md).
