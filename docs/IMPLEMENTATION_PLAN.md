# Overload implementation plan

Build a small, measurable combat game first, prove the Overload and Override interaction, then expand progression and content. This plan is written for an AI coding agent working with a human product owner and, when available, an artist and playtesters. It defines intended work; no game code or executed gameplay tests are claimed by these documents.

The owner confirmed Windows-first offline single-player development using Godot and C#, with uncapped character levels, continuing power progression, and uncapped difficulty. Co-op is a future decision. Milestones are gated by evidence rather than elapsed time, and the agent should complete one coherent task at a time while keeping the project runnable. The formulas, storage rules, and acceptance cases in [endless progression](ENDLESS_PROGRESSION.md) are required architecture, not an optional expansion.

Implementation through A03 and quality/release preparation through R02 are recorded in [STATUS](STATUS.md), [A03 validation](A03_VALIDATION.md) and [quality/release validation](Q_RELEASE_VALIDATION.md). Unfinished is cut under A03's optional scope. Human acceptance, additional hardware, release signoff and evidence-based expansion selection remain open; automated work does not close those prerequisites.

## Technical baseline

Use Godot's .NET edition, C# for gameplay and tools, text scenes and resources, and the Compatibility renderer initially. The retrieved release archive lists 4.7.2 as a stable release. M0 should verify and pin the exact supported stable editor, matching export templates, and compatible .NET SDK instead of floating with `latest`. The versioned C# documentation still contains some older-version wording, so a compiled and exported desktop smoke project is the final compatibility check. [Godot release archive](https://godotengine.org/download/archive/), [Godot C# documentation](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)

Do not install or depend on the ordinary non-.NET editor for this C# project. Use a desktop build; the retrieved C# documentation lists a web-export limitation. Browser deployment is not part of the product baseline. [Godot C# platform support](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)

Record the editor version and download checksum, renderer, export-template version, .NET SDK version in `global.json`, target framework, locked package versions, and initial export preset. Keep versions unchanged through a milestone unless a specific blocker requires an upgrade. Validate API examples against that pinned version rather than assuming every online example matches it.

Use Git for code and text assets; Git LFS for large editable art and audio sources when remote collaboration begins. Keep exported builds, engine caches, local telemetry, and personal saves out of version control. Do not require Git LFS just to run the placeholder prototype. Track source-asset licenses and authorship in a manifest.

Prefer built-in engine functionality and plain C# libraries over early framework selection. No custom ECS, visual scripting language, embedded database, dependency-injection framework, or general mod runtime is required. Profile before introducing data-oriented rewrites.

## Proposed repository structure

This is the intended implementation layout. The project currently contains documentation only.

```text
Overload.sln
global.json
Directory.Build.props
src/
  Overload.Domain/           # Engine-independent state, rules, RNG, definitions
  Overload.Content/          # Import, schemas, validation, definition registry
  Overload.Game/             # Godot project, scenes, adapters, UI, presentation
    project.godot
    Scenes/
    Scripts/
    Content/
    Assets/Generated/
  Overload.Tools/            # Console validators and balance reports
tests/
  Overload.Domain.Tests/
  Overload.Content.Tests/
  Overload.Integration/      # Godot scenes and automated smoke entry point
assets/source/              # Editable art and audio; provenance alongside it
build/                      # Export presets and reproducible build scripts
docs/
  GAME_DESIGN.md
  CORE_SYSTEMS.md
  IMPLEMENTATION_PLAN.md
  RESEARCH.md
  decisions/                # Introduce when actual decisions are made
  playtests/                # Introduce when observations exist
```

Domain cannot reference Godot types. Use domain vectors and plain records at boundaries; adapters translate to engine vectors. Content depends on Domain; Game depends on both. Domain tests run without the editor or imported textures. Do not split each subsystem into its own assembly unless build or test boundaries justify it.

## Runtime responsibilities

| Module | Owns | Boundary |
| --- | --- | --- |
| Input router | Device mapping, intent buffering, aim, menu focus | Produces intents, never damage or reward events |
| Simulation coordinator | Fixed tick, ordered phases, authoritative state mutation | One authority even in offline mode |
| Action system | State machines, timing, costs, cancellation | Runs the plan chosen by the resolver |
| Behavior profile | Overload bindings, priorities, active contract implementations | Rebuilt only on legal loadout changes |
| Combat resolver | Hit eligibility, damage, barriers, stagger, death | Emits bounded typed results |
| World adapter | Movement, shape queries, navigation, line of sight | Supplies geometry evidence to rules |
| Encounter director | Enemy roles, attack scheduling, boss phases | Cannot grant unvalidated quest rewards |
| Progression service | Exact XP/level calculation, Resonance, unlocks, Codex, proofs | No maximum level; separate from combat components |
| Fracture service | Uncapped frontier, chapter routes, reward budgets, attunement permissions | Generates current content lazily; no array indexed by every possible tier |
| Inventory and economy | Instances, wallets, crafting, transactions | Owns all deductions and grants |
| Save service | Versioning, atomic snapshots, recovery, migrations | Never serializes arbitrary nodes or executable types |
| Presentation | Sprites, camera, sound, HUD, menus | Observes state and events without changing outcomes |

Use explicit composition: one ActorState and several small systems, with an actor ID in events. Do not write a 3,000-line PlayerController that owns inventory, UI, save files, enemy targeting, and quest rewards.

## Simulation and combat math

Run rule advancement at 60 fixed ticks per second, with render interpolation for presentation. Convert authored durations to ticks at content load using one rounding convention, and display seconds in UI. Keep memory expiry, cooldowns, damage schedules, and costs in fixed-tick logic. Movement and hit detection use Godot queries through the adapter.

An initial tick sequence is: expire timers; ingest buffered intents; preflight and commit player/AI actions in stable actor order; advance motion and action phases; collect collision evidence; resolve hits in stable event order; apply stagger and lethal handling; generate allowed memories; process progression transactions; publish presentation events. Expiration precedes selection. Events generated during hit resolution do not retroactively change an already selected action.

Pure rule tests can be deterministic with a seed. Do not claim cross-platform deterministic physics or deterministic multiplayer from a fixed timestep. For replay diagnostics, record input, content/build hashes, RNG seeds, and relevant world-query results; engine replays can still diverge and should report the first state mismatch.

Use independently seeded random streams for combat criticals, loot, room selection, and decoration. Changing a visual effect must not change loot. Preview calls never advance RNG. Growing combat values, XP, levels, grades, frontier values, and wallets use BigInteger-backed domain types and integer subunits under the endless progression specification. Small bounded timing/physics values keep appropriate ordinary numeric types. Round once at the documented arithmetic boundary; never cast unbounded progression values to floats for combat calculations.

Initial damage math:

```text
attackBudget = (skillBaseAtRank + weaponAttackPower * weaponCoefficient)
               * (1 + summedOrdinaryDamageBonus)
               * techniqueCoefficient
               * damageResonance
               * sharedAttunementMultiplier

patternBudget = attackBudget * selectedPatternCoefficient
hitBudget = patternBudget * authoredHitShare
criticalBudget = hitBudget * (isCritical ? criticalMultiplier : 1)
finalDamage = criticalBudget * (1 - applicableReduction)
```

If no pattern matches, selectedPatternCoefficient is one. AuthoredHitShare values sum to one; Afterstrike uses a 1.2 pattern coefficient and two 0.5 shares. Purely utility actions have no attack budget. Weapon Attack Power in this formula is the foundational value before shared attunement; applying grade to both weapon and final attack would incorrectly double it. Life scales through Resolve and attunement once. Apply a barrier to final damage before Life. Damage conversion, if added, occurs before selecting the defense and never allows two damage types to share the same portion twice.

For the first balancing model, physical reduction is `min(0.65, Armor / (Armor + K))`; supernatural reduction is `min(0.60, Resistance / (Resistance + K))`. Campaign encounters use authored K values. Fracture tier T uses `K = 120 * (T + 24) / 25`, with no maximum T. Equipped Armor and Resistance scale with shared attunement as specified in the endless progression document. Compare items against the selected tier instead of displaying one misleading permanent reduction number. Enemy Life and damage grow with `((T + 24) / 25)²`; no clamp to the last authored tier is permitted.

Initial caps: movement bonus 25%, skill haste 30%, critical chance 40%, critical multiplier 2.0. Haste uses `duration / (1 + haste)` with action-specific minimum phases; it never shortens enemy tells or the player's evasion window. Never let a negative cooldown, maximum Life, or Focus capacity enter runtime state. Numerical values are tuning starting points and must remain in data.

Do not allow talents, Codex, gear, skill ranks, and patterns each to introduce independent unrestricted multiplicative damage buckets. Track every multiplier in a debug damage breakdown, including why a hit missed or was immune.

## Content model and validation

Definitions include Frame, Skill, Technique, TalentNode, Inscription, Pattern, Override, Enemy, Attack, Encounter, Room, ItemBase, Affix, LootTable, Quest, Reward, ProgressionCurve, WorldRule, BossMutation, and ChapterRoute. Author finite reusable templates plus formulas, never a definition for every level or tier. Use stable namespaced IDs and versioned JSON for mechanical content. Use Godot scenes/resources for presentation and geometry. This keeps mechanical changes reviewable without turning room construction into JSON authoring.

The validator must catch duplicate and missing IDs, cycles in prerequisite graphs, invalid skill adapters, ambiguous signatures, unauthorized contract slots, unsafe parameter ranges, missing localization keys, invalid animation names, unsupported damage tags, broken reward references, and unreachable quest prerequisites. Definitions are loaded and validated before entering a scene.

Attack timelines contain windup, hit windows, motion policy, cancel windows, hitbox shape references, audio/VFX cues, and cooldown. Gameplay owns hit timing; animation events mirror it. Animation interpolation or a dropped frame must not produce extra hits.

Store equipment as an instance GUID plus base ID, band, quality, affix values, replaceable-affix slot, lock state, and optional Relic ID. Store item stats directly after generation; loading an item must not reroll it. Quest proof IDs and claimed rewards are sets with idempotent transactions.

Add a small in-game debug overlay and content inspector before making a general editor. The overlay needs active action, selected signature, token time remaining, Strain, contract slot, hitbox outlines, telegraphs, current target, and the last few events. An agent should be able to diagnose an incorrect action without guessing from particles.

## Save and transaction design

The first save format is versioned JSON plus a checksum for accidental corruption detection, not anti-cheat. Include save schema version, game/content version, character GUID, profile mode, total XP, derived level validation, allocated foundation points and Resonance, auto-allocation settings, shared attunement, highest cleared/unlocked tier, active chapter and offered routes, chain progress, learned IDs, bindings/priorities, unlocked and selected oaths, items, all wallets, quest/proof state, active reward receipts, settings, and progression RNG state where relevant. Unbounded integers serialize as decimal strings. Retain only the last 64 detailed run records and compact finished-run receipts at committed run-end checkpoints; do not let ordinary saves grow by one record per future level or tier.

Save at checkpoints, after inventory/crafting transactions, after reward claims, on returning to Hearth, and on orderly exit. Do not promise mid-combat continuation in the first release. Quitting an unfinished encounter resumes at its entry checkpoint with earned persistent rewards preserved and unclaimed encounter rewards absent. No memories, temporary barriers, anchor, active action, or reservation queue survive checkpoint reload.

Persist expedition identity, its completed objectives, and the Unfinished charge before allowing the afterlife state. Returning from a interrupted afterlife resumes the encounter checkpoint with that charge spent. Otherwise save/reload could repeatedly recover lethal damage. Resetting an expedition from Hearth intentionally discards unfinished objective progress and creates a new ID.

For mutations, clone the persistent aggregate, validate, apply a uniquely identified transaction, write a temporary snapshot in the same directory, flush, verify its parse/checksum, then replace the current file while retaining the previous valid snapshot. Implement and test the actual Windows replacement behavior. On interruption, choose the newest complete valid snapshot. Never delete both valid snapshots before replacement succeeds.

Keep three rotating backups. On load, validate ranges and content IDs, apply migrations on a copy, and retain the original. Unknown removed patterns should be unequipped with a message and their progression preserved or refunded according to a migration. Do not deserialize arbitrary type names. Test truncated files, invalid checksums, future versions, and one-way migration behavior.

Ordinary local play cannot prevent save editing or enforce rare ownership globally. Do not build invasive anti-cheat for an offline RPG. If future online progression must be trusted, its authority and persistence need a separate design.

## Procedural generation and AI

Start with authored rooms and deterministic encounter scripts. Only after the slice works, assemble rooms with typed entrance sockets and a graph grammar: start, two to four combat branches, optional reward branch, checkpoint, boss, exit. Each seed must have a reachable critical path, legal spawn and return points, no blocked entrances, and at least one feasible movement route around each mandatory hazard.

Validate generated layouts before showing them. Retry with a bounded count, then use a known-good authored fallback; never hang on seed generation. Store seed, generator version, and selected room IDs for reproducing bugs. Run connectivity tests over at least 10,000 generated layouts, then visually inspect a representative sample. Connectivity alone does not prove fun or fairness.

Enemy AI uses small explicit state machines: Idle, Approach, Reposition, Windup, Attack, Recover, Stagger, Dead. Separation steering prevents piles; navigation updates are staggered across frames. Use attack reservations to cap simultaneous heavy threats. Do not give every ordinary enemy a complex behavior tree before simple roles are enjoyable.

Bosses use authored phase state machines with clear transition invulnerability rules and a deterministic test mode. Telegraph geometry comes from the same shape data as the damage query. Record mismatches automatically when an attack hits beyond its communicated area.

Chapter generation adds a bounded rule-compatibility pass before room population. Author six world rules and four boss mutations for the first full endgame, permit at most two rules and one mutation simultaneously, and keep the ordinary threat director budgets. Tier scales power without shortening warnings or increasing entity counts indefinitely. Persist offered chapter routes before showing them, and validate that an active route remains loadable under its saved content version.

## Presentation and asset pipeline

Use a low-resolution world SubViewport and an output-resolution UI layer. Camera follows continuous simulation positions but snaps the rendered world transform consistently to the pixel grid. Test diagonal motion and camera pans for shimmer. Keep foot pivots, sorting rules, collision footprints, and shadow origins consistent.

Art export should be scriptable and repeatable: source file to atlas PNG plus animation metadata to import validation. Retain source files. Avoid modifying atlas coordinates manually in many scene files; stable animation names bind content to frames. Validate missing frames and off-canvas pivots before packaging.

Produce one finished hero animation set, enemy, and room as an art benchmark before commissioning all regions. Human review is required for silhouette, animation readability, stylistic consistency, and source rights. AI can draft and iterate assets, but a generated sheet is not automatically a usable eight-direction animation set.

Audio routes through Music, Ambience, Player, Enemy, UI, and Master buses. Cap concurrent repeated effects and prioritize attack warnings. Pause, mute, ducking, and device changes need explicit behavior. Store loudness and loop metadata in the source manifest and audition transitions in the exported game.

All localization keys and input glyphs are resolved at display time. The UI should never bake English text or keyboard bindings into combat textures. Screenshots need to cover 1280×720, 1920×1080, 2560×1440, a 16:10 window, enlarged UI, and controller focus states.

## Milestones and gates

| Milestone | Dependency | Deliverable | Evidence required to advance |
| --- | --- | --- | --- |
| M0 Foundation | Confirmed platform choices | Pinned editor/SDK, solution, numeric domain types, empty arena, input abstraction, reproducible export | Clean checkout builds, exact progression values round-trip, and exported Windows app launches without the editor |
| M1 Combat arena | M0 | Movement, three Warden skills, evade, flask, three enemy roles, Bellkeeper prototype | Ten-minute playable loop; readable tells; correct death/retry; basic controller coverage |
| M2 Overload proof | M1 | Memories, three initial patterns, dispatcher, Strain, prediction HUD, diagnostics | All relevant OL acceptance tests pass; players deliberately choose a pattern |
| M3 Override proof | M2 | Elsewhere, compatibility, debug grant, preview chamber, atomic unlock transaction | Anchor state cases and one-time spending verified; new behavior is valuable and its loss is understood |
| M4 External slice | M3 | Hub, eight rooms, six skills, six patterns, small talent/Codex/item loop, uncapped XP service, save/load, representative art/audio | Independent players finish 30–45 minutes and understand the core distinction; synthetic profiles cross level 1,000 without losing progress |
| M5 Endless endgame foundation | M4 approved through evidence | Resonance, attunement, unlimited tier formulas, generator, chapter choices, wallets, tier-10 proofs, real Override qualification | EP01–EP11 pass; playable runs span tiers 9–11; economy model and accelerated qualification agree |
| M5B Extended endgame loop | M5 | Anomaly Hunts, three-leg chains, initial rule/mutation library, personal build records, one Sovereign Echo encounter | EP12 passes; players have distinct farming, pushing, and mastery goals; no duplicate rewards or mandatory oath requirement |
| M6 Content alpha | M5B | Four regions, three Frames, 24 skills, 18 patterns, planned bosses/items; later oaths if they pass scope review | Campaign and representative endgame tiers completable; every successful frontier clear unlocks another tier |
| M7 Beta | M6 | Balance, accessibility, art/audio polish, save migrations, device coverage, optimization | Performance and usability gates pass on nominated hardware; clean install and update verified |
| M8 Release candidate | M7 | Signed-off export, store assets, crash guidance, support process, recovery procedure | Human release review, final regression, asset manifest complete, reproducible archived build |
| M9 Save-compatible expansion | Released baseline and observed demand | Additional authored rooms, rules, boss mutations, and optional oath choices | Existing XP, levels, grades, unlocked tiers, and items survive migration; no forced reset or replacement maximum |

Do not wait until M6 to implement a single Override. It is both a selling point and an architectural risk. Conversely, do not build a complete ritual economy before discovering whether the oath is enjoyable.

## Agent task queue

Each row is a bounded assignment. Complete the prerequisite, implement the behavior, verify it, and record evidence before taking the next dependent task. Estimates belong at milestone level; split a task further if it stops fitting one reviewable change.

| Task | Depends on | Implementation and acceptance |
| --- | --- | --- |
| F01 | None | Inspect installed toolchain, choose exact compatible versions, record decision, create a minimal C# export and launch it |
| F02 | F01 | Create Domain/Game/Tests boundaries, BigInteger progression wrappers, string serialization and build scripts; pure tests run without Godot |
| F03 | F02 | Set pixel viewport, camera, input map, remapping storage, controller aim; both devices can enter and leave the arena |
| C01 | F03 | Add actor movement and collision with debug footprints; diagonal speed normalized and walls stop displacement |
| C02 | C01 | Add action state machine and timings; cancellation cannot duplicate hits or refunds |
| C03 | C02 | Implement Life, Focus, damage, flask, evade, and death/retry; all resource boundaries tested |
| C04 | C03 | Add pursuer, ranged caster, brute, and Bellkeeper greybox; visible hit regions match damage queries |
| O01 | C04 | Implement Momentum/Echo generation, token expiry, pause, and clear rules; wall farming and repeated-hit farming fail |
| O02 | O01 | Implement compiled binding profile, signature ordering, fallback, and atomic action commitment; OL01–OL09 and OL12 pass |
| O03 | O02 | Implement Pursuit, Afterstrike, Crossing and event provenance; descendant effects cannot generate memories |
| O04 | O03 | Add HUD prediction, binding menu and debug trace; preview has no side effects and matches accepted snapshots |
| V01 | O04 | Implement Elsewhere state machine and compatibility; OV01–OV04 and OV07–OV08 pass |
| V02 | V01 | Add oath preview and sandbox unlock transaction; interrupted writes never lose materials without ownership |
| S01 | V02 | Build persistent character save, three backups, migration fixture and recovery UI; corrupt/truncated files recover |
| S02 | S01 | Add six gear slots, comparison, inventory lock, gold/Alloy and deterministic crafting; no duplicate grant/debit |
| S03 | S02 | Add uncapped XP curve/inverse, finite foundation grants, Resonance ledger, skill technique, 12-node talents, four inscriptions and free respec; no negative or duplicate points |
| S04 | S03 | Add six total skills, Convergence/Shelter/Reprieve, hub and eight authored rooms; complete loop and boss exit work |
| S05 | S04 | Replace critical placeholders with representative art/audio, tutorial, full menu navigation and settings |
| S06 | S05 | Export external slice, run first independent playtest, fix observed control and comprehension failures |
| E01 | S06 gate | Implement the progression balance model using the same rules as the game; export XP, reference levels, damage/Life, wallet and forging tables |
| E02 | E01 | Apply Resonance and shared attunement once to combat profiles; automatic/manual allocation and bulk forge costs conserve resources |
| E03 | E02 | Implement infinite tier access, immutable per-run reward budgets and lazy tier selection UI; replay and duplicate claims cannot advance the frontier |
| E04 | E03 | Persist chapter offers, grade/frontier, chain-ready run state and exact large values; recover transaction interruptions without lost XP |
| E05 | E04 | Run EP01–EP11 where applicable, large-value benchmarks and matched/undergeared/overleveled encounter sweeps; inspect tier 9–11 playability |
| P01 | E05 | Build schemas and content reports for complete release categories; invalid content fails CI with useful paths |
| P02 | P01 | Add generator, seed capture, fallback and connectivity checks; known seeds remain reproducible within version |
| P03 | P02 | Connect the uncapped board to all three activity families, regional wallets and tier-10 proof flags; exactly budget rewards per activity |
| P04 | P03 | Implement real ritual and Trial of Contradiction; accelerated profile proves entire unlock and reload journey |
| P05 | P04 | Add Stillness and Rupture plus justified patterns; run pairwise and adversarial interaction suite |
| P06 | P05 | Author six world rules/four boss mutations, chapter compatibility and targeted Anomaly Hunts; no rule pair removes all safe routes |
| P07 | P06 | Add three-leg chains, first Sovereign Echo and last-64-run build records; EP12 and banking/reload checks pass |
| A01 | P07 | Produce Threadseer and Revenant one at a time; prove three viable builds per Frame before expanding item count |
| A02 | A01 | Complete regional content and boss roster; review each room for traversal and oath compatibility |
| A03 | A02 | Implement Red Covenant and optionally Unfinished only after dedicated design/QA spike; cut the latter if it threatens quality |
| Q01 | A03 or scope cut | Balance the campaign and endgame with ordinary gear; run long-session save/performance tests |
| Q02 | Q01 | Run accessibility, controller, screen-size and hardware matrix; fix blockers |
| R01 | Q02 | Produce release candidate, archive toolchain/content hashes, verify clean install/update and perform human release review |
| R02 | R01 and real player evidence | Choose an expansion from observed gaps; verify old high-level saves and active-run version handling before shipping more content |

## Testing strategy

Automate contracts that can corrupt a build, progression, or resources. Avoid hundreds of tests asserting a constant equals the same constant in its definition. Every test should protect a meaningful behavior, invariant, migration, or known failure.

| Layer | Coverage | Cadence |
| --- | --- | --- |
| Pure rules | Dispatch, costs, damage budget, tokens, cooldowns, qualification, reward idempotency | Every relevant change |
| Endless progression | XP inverse, milestone crossing, allocation conservation, attunement sums, tier monotonicity, values beyond 64-bit range | Progression/content/save changes |
| Generated/property cases | Valid random profiles, token conservation, no negative balances, no two active oaths, capped effect graphs | CI; preserve failing seeds |
| Content validation | References, ranges, animation bindings, localization keys, loot/progression reachability | Every content change |
| Engine integration | Collision, action timing, anchor landing, pause, actual input routing, boss state transitions | Relevant changes and milestone build |
| Save failure injection | Every transaction stage interrupted, migration fixtures, corrupt and future-version files | Save/economy changes |
| Export smoke | Launch, load arena, accept input, complete fixture, save, reload, exit cleanly | Candidate builds |
| Human playtests | Feel, readability, comprehension, build choice, repetitive tasks, accessibility | M1 onward |

For combination testing, exhaustively test the six-pattern slice against its compatible skills and Elsewhere. At release scale, enumerate valid profiles in pure rules when cheap, use pairwise coverage across Frames/techniques/patterns/oaths/items, and add explicit adversarial cases. Pairwise tests are not proof against every higher-order interaction. Fuzz resource and event boundaries and playtest the strongest discovered combinations manually.

Create a fixed encounter suite: stationary target for budget validation, moving target, three-enemy pack, mixed ranged pack, staggerable elite, Bellkeeper, and hazard arena. Record damage, time alive, resource usage, signature use, and missed opportunities. Never balance only against a stationary damage dummy.

Suggested reproducible checks after scaffolding:

```powershell
dotnet build Overload.sln -c Release
dotnet test Overload.sln -c Release --no-build
dotnet run --project src/Overload.Tools -- validate-content
```

Build scripts should resolve the pinned Godot executable and invoke headless import and export using the committed preset. The official CLI supports headless export, but a command returning zero is not enough: launch the exported executable and inspect its logs and visible behavior. [Godot command-line documentation](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html)

A custom `--smoke-test` user argument can start the integration scene and write a structured result to a temporary directory. Implement that argument explicitly; it is not a built-in Godot test command. A headless run cannot verify pixel clarity, sound, controller comfort, or render performance.

## Endless progression delivery sequence

First implement a pure balance model inside Overload.Tools that consumes the same ProgressionCurve definitions and arithmetic as the game. Proposed command: `dotnet run --project src/Overload.Tools -- simulate-progression --profile balanced --runs 1000 --seed 42`. This is a command to implement, not an existing tool. Write a JSON report and CSV tables for current/next level, tier, XP per run, estimated run duration, failed runs, grade affordability, and Override requirements. Avoid a second spreadsheet formula becoming the game's true authority.

Supply balanced, offensive, defensive, comfortable-farming, frontier-pushing, and alternate-region simulation profiles. Run durations and success rates are explicit input assumptions, later replaced with observed distributions. The first simulation cannot predict fun or player skill. Compare outcomes such as tier stagnation, resource surplus, and mandatory grind rather than reporting a false guaranteed completion time.

Next make an **endgame proving ground** using the existing Warden, room kit, and Bellkeeper. It must let developers set level and grade separately, choose any positive tier, complete tiers 9–11, allocate Resonance, forge a grade, and inspect damage calculations. No high-tier fixture may bypass the actual action resolver or use a second combat formula. Dev grants write only to isolated test saves.

Then build three saved chapter route choices and one mutated boss, followed by a three-leg chain with extraction. These make the endless model a playable product loop before new biomes are produced. Playtest a 90–120 minute session containing farming, a failed push, a build adjustment, successful frontier progress, and a route choice. Record why the player chose to continue or stop.

Finally connect the four regions and Override journey. Revalidate level-100 eligibility, the 72 qualifying-run resource arithmetic, tier-10 proof collection, and the normalized trial. Preserve the separate goals of leveling, pushing, collecting a desired item, and earning an oath. Cut optional endgame activities before cutting uncapped levels or the next-tier guarantee.

Sampled levels/tiers in tests must never become production limits. Run edge cases at million-scale levels and beyond 64-bit range, benchmark their cost, and retain gameplay profiling at realistic near-frontier values. UI lists should show a navigable window of tiers with search/jump controls, not attempt to render one row per unlocked tier.

## Performance gates

Nominate a real reference PC during M0. The initial target is 60 FPS at 1920×1080 output with a 640×360 world viewport, on a modest desktop GPU and four-core CPU. Do not publish minimum system requirements until measured on actual machines.

The stress scene contains 60 active enemies, 200 live projectiles, representative ground effects, maximum legal Overload activity, and loot. Candidate target: 95th-percentile frame time at or below 16.7 ms and 99th-percentile below 25 ms over five minutes after warmup on the reference PC. Document the hardware, graphics settings, build, and scene seed. Also test ordinary encounters, which must be clearer and smoother than the stress scene.

Initial budgets are under 1 GB working memory for the slice, under 2 GB for release, checkpoint loads under three seconds from an SSD, and no steadily growing retained memory during a one-hour expedition loop. These are goals, not claimed results.

Profile before optimizing. Likely candidates are repeated pathfinding, per-projectile allocations, collision queries, effect overdraw, UI rebuilds, and event logging. Pool projectiles/VFX if measured useful, stagger navigation updates, batch static tiles, and bound combat logs. Always preserve gameplay-critical telegraphs when reducing visual quality.

## Playtest gates and telemetry

Use local structured diagnostic logs during development. Any future remote analytics should be opt-in and minimize collected data. A session record needs a random session ID, build, test profile, input device, encounter, event type, and gameplay values; it does not need real names or arbitrary machine identifiers.

Observe input accepted, action selected, candidate rejected with reason, memory generated/consumed/expired, Strain peak, damage source, death cause, room time, loot inspected, craft transaction, proof completion, trial attempt, and oath unlock. Aggregate routine hits rather than logging unbounded per-frame data.

At M2, recruit at least five fresh testers for formative feedback. After a brief tutorial, ask them to deliberately create a requested Overload three times and explain the choice they gave up. A working gate is four of five succeeding without coaching. This small sample detects obvious problems; it is not statistical validation of the market.

At M3, compare the same boss with and without Elsewhere using equivalent gear. Testers should explain why they would choose either version. If everyone describes it as simply stronger or unusable, tune the tradeoff before producing other oaths.

At M4, test with roughly 10–15 players across ARPG familiarity and both input devices. Targets: at least 80% complete the core tutorial without intervention; at least 80% distinguish Overload from skill upgrades; no common unavoidable death or blocked progression; at least three voluntarily tried build configurations with different action sequences. Gather open-ended reasons for stopping, not just a numerical fun score.

Later, measure Overload selection share per binding, base-action share, memory wastage, deaths while expecting a different signature, item comparison time, qualification bottlenecks, and total time-to-oath. A pattern chosen by nearly every successful build is a reason to inspect its opportunity cost, not automatically to nerf it. A rarely chosen pattern may be unclear, incompatible, or weak.

Also measure XP per minute by tier-relative power, time between frontier clears, Resonance allocation, attunement spending, repeated rule combinations, chapter choice, and chain extraction. Log compact level/tier ranges and sampled exact values without casting them to 32-bit telemetry fields. A campaign completion metric is finite; endless progression has retention and voluntary stopping points rather than a final completion percentage.

Define the scope of every statistic. A 90-day oath unlock rate among campaign-completing characters does not equal ownership among all purchasers. Never tune only to a desired rarity percentage when the work is repetitive or inaccessible.

## Production estimates and cut order

Planning estimate, before any prototype measurement: M0–M4 require approximately 8–14 engineering person-weeks, plus 8–14 person-weeks of representative art, audio, design iteration, and QA. Tasks can overlap with available people, but these are not a promise of a particular calendar date or of autonomous AI speed.

A polished small release was initially estimated at roughly 50–90 engineering person-weeks and 40–80 other production person-weeks, including the slice. Budget an additional provisional 8–14 engineering person-weeks and 3–6 design/QA person-weeks for robust endless arithmetic, economy simulation, chapter generation, and the extended endgame loop. The revised planning envelope is therefore 58–104 engineering and 43–86 other production person-weeks, with overlapping art work where staffing permits. These are scope-planning assumptions, not sourced averages or a calendar promise. Re-estimate from measured throughput after M4 and M5; a solo creator can take substantially longer than a small team.

Budget explicitly for human art direction, external playtesting, asset/audio licenses, localization if selected, release administration, and contingency. No purchases or budget commitments are authorized by this plan. A practical budget formula is person-weeks by discipline multiplied by agreed rates, plus external costs and a separately visible 25–35% contingency. Do not fabricate a currency total without staffing and rate inputs.

If the slice misses its quality gate, cut scope in this order: additional Sovereign Echo encounters; Unfinished; the third Frame; extra Relics; optional room variants; fourth-region size; elaborate narrative presentation. Preserve responsive combat, the Overload chooser, one real Override, uncapped leveling and difficulty, dependable saves, and accessible controls. Reducing region size still requires equivalent varied routes for the four material sources; update the acquisition design if a region is removed entirely.

Do not expand to multiplayer to rescue a weak single-player loop. Conversely, if co-op becomes a product requirement, stop new production content long enough to test it properly. The future design would need server-authoritative input/results, ownership of memories and hit events, reliable progression transactions, latency handling, and save ownership rules. Godot's multiplayer API does not supply game-specific trust rules automatically. [Godot networking guidance](https://docs.godotengine.org/en/4.7/tutorials/networking/high_level_multiplayer.html)

## Risks and responses

| Risk | Early signal | Response |
| --- | --- | --- |
| Overload feels like an ordinary proc | Testers ignore memory choice and repeat one input | Add meaningful attack-versus-defense competition; remove automatic reward spam |
| Signature selection feels unpredictable | Frequent “wrong action” reports despite correct code | Improve shared preview, simplify priorities, lengthen memory windows, reduce concurrent bindings |
| Override is just another passive | Oath changes numbers but no decisions | Require replacing a state machine or payment/death contract |
| Override feels like tedious exclusion | One activity dominates playtime; failures erase progress | Guaranteed rewards, separate mastery proof from stockpile, shorten repetitive steps |
| Powerful combinations escape testing | Effect chains hit caps or resources cease to matter | Provenance, bounded dispatch, fuzzing, fixed encounter suite, explicit cap tests |
| Pixel art workload expands uncontrollably | New equipment requires every animation redrawn | Weapon-family presentation, limited palettes, benchmark animation costs early |
| Content quantity outruns quality | Similar rooms with no tactical difference | Author fewer distinctive encounters and validate their roles |
| Engine abstraction grows faster than game | Many interfaces but no playable improvement | Keep the next player-visible behavior as the unit of work |
| Save corruption destroys long progress | Mixed resource/unlock states after failure injection | Atomic aggregate snapshots, idempotency, backups, tested migrations |
| Endless progression becomes effectively capped | XP overflow, capped table lookup, immunity, or impractical wall at later tiers | Exact arithmetic, analytic curves, scaling invariants, large-value and frontier tests |
| Endgame becomes repetitive | Players farm one route and ignore chapter choices | Distinct tactical rule combinations, targeted rewards, optional goals, better authored encounters |
| Infinite growth erases build choices | Players eventually acquire every talent or permanent immunity | Keep choice budgets; put uncapped ranks in damage/Life channels |
| Reference-game imitation dominates identity | Pitch depends on naming other games | Lead demo with memory choices and the anchor oath; retain original world and assets |

## Instructions for the implementing agent

Read this plan, the core specification, and the endless progression specification before coding. Inspect actual repository instructions and existing changes. Uncapped character levels and difficulty are confirmed requirements; tuning numbers remain provisional. Future implementation requests establish the scope to execute.

For each assignment: state the concrete behavior being delivered; inspect relevant code; make the smallest complete change; run targeted rules and content checks; launch a representative scene when presentation or integration changes; record evidence and remaining uncertainty. Do not claim a build works because the code compiles, or claim a mechanic is enjoyable because bots can clear it.

Keep a short `docs/STATUS.md` once development begins: current milestone, completed task IDs, actual toolchain, exact build/run commands, known issues, and next task. Record architecture decisions only when made. Update the canonical rule document when behavior deliberately changes, including migration and test consequences.

Use placeholders openly. Mark generated or temporary art and debug grants. Development profiles must not ship enabled, grant real achievements, or overwrite ordinary characters. Keep test save locations separate from normal player saves.

Do not invent public APIs based on memory when a compiler or versioned reference can check them. Do not add a library before stating the problem it solves. Do not refactor unrelated systems while implementing one pattern. If the plan is inconsistent, document the narrow resolution and update the affected tests and specification together.

The first implementation request can be:

> Implement M0 and M1 of the Overload plan in this repository. Use the confirmed Windows, Godot .NET, and offline single-player baseline, with uncapped character levels and Fracture tiers as architectural requirements. Inspect and pin the toolchain, create the minimal domain/game/test boundaries and exact progression numeric types, and deliver a runnable combat arena with movement, three Warden skills, evade, flask, basic enemies, and the Bellkeeper prototype. Use explicit placeholder art. Verify clean build, exported launch, combat input, death/retry, and controller navigation. Keep the architecture ready for the documented action resolver and endless progression without implementing the whole endgame yet. Record exact commands, completed tasks, observed limitations, and next step in docs/STATUS.md.

M1 completion leads directly to the Overload proof, not to expanding the campaign. The deciding evidence is a playable sequence in which the same action takes different deliberate forms and the player understands why.
