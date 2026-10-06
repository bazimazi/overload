# 0001 — Windows combat arena foundation

Accepted 5 October 2026.

Implement M0 and M1 first, as the README requests. M2 patterns and M3 Elsewhere remain the next milestones. The initial release is an offline Windows x64 application with original geometric placeholder art.

## Toolchain

- Godot **4.7.2 stable .NET**, official build `ed1daf0bf`, Compatibility renderer. Verified against the [official archive](https://godotengine.org/download/archive/4.7.2-stable/) and the `godotengine/godot-builds` release assets.
- Matching **4.7.2.stable.mono** Windows x64 export templates. `build/setup.ps1` verifies both downloaded archives against the official SHA512 file before extraction.
- .NET SDK **10.0.401**, pinned with roll-forward disabled in `global.json`; `net10.0` for all assemblies. The machine already provides this SDK. The target framework is explicit in the Godot project because its editor inserts `net8.0` when the property exists only in a parent props file.
- Godot SDK **4.7.2** and transitive NuGet versions are locked. xUnit is used only by the engine-independent test project to exercise resource, numeric, and state-machine invariants.
- The root solution includes tools and tests. Godot also requires `Overload.Game.sln` beside its project for export. Godot's editor and export configurations have different package references, so the Game project keeps separate `packages.Debug.lock.json` and `packages.ExportRelease.lock.json`. All projects declare the Windows x64 runtime so export does not invalidate ordinary restore locks.
- Development machine: Intel Core Ultra 7 255H, Intel Arc 140T 8 GB. This is a development reference, not a published minimum specification or completed performance qualification.

## Boundaries and prototype choices

Domain owns exact numbers, fixed-tick action commitment, resource changes, damage mitigation, and stagger. Content loads and validates the named JSON balance profile. Game owns input, Godot collision evidence, encounter scheduling, and presentation. Tools exposes content validation. No domain project references Godot.

The low-resolution world is rendered separately from the UI at 640×360, using whole-number scaling with centered margins at intermediate output sizes. Pointer events use the same world-to-window transform. One pixel is 1/32 meter. The camera is fixed to show the entire first room; camera following is unnecessary for this room. Physics runs at 60 ticks per second. Authored seconds are rounded to the nearest tick, with halves rounded up; runtime JSON stores the resulting ticks (0.12 s becomes 7 ticks, 0.08 s becomes 5).

The arena uses the documented Warden rank-zero damage budgets already evaluated at 100 Attack Power. Later equipment systems must replace these cached fixture budgets with the complete budget pipeline, not multiply weapon power a second time. Health/damage are BigInteger subunits; Focus and timers remain bounded. Progression functions have no designed maximum and use logarithmic search. No character progression/save loop is exposed yet.

Three introductory encounters lead to the Bellkeeper, with full resource refills at each encounter entry. Retry repeats the current encounter; boss practice starts at its checkpoint. The boss alternates a locked-aim sweep and a seven-projectile volley, with shorter recovery below half Life. Tell durations never shorten. Ordinary enemies have distinct pursuer, caster, and brute roles. No progression rewards or persistence are fabricated for the arena.

M1's flask starts healing on acceptance, heals 35% over 60 ticks, rejects use at full Life or during an existing heal, and consumes one of three charges. All attacks spend costs at acceptance; canceling does not refund them. Movement can cancel recovery after six ticks; evade can cancel windup before the last three ticks. Hits use the same sector vertices shown by telegraphs; projectiles sweep against footprints and terrain in travel order.

`--smoke-test` uses the actual arena, physics, input map, and menus with isolated test output. `--capture` is an explicit developer screenshot fixture. Neither writes a normal character save. The prototype has no normal character-save format yet; M4 owns that work.

## Deferred evidence

Human combat feel, a physical controller, a ten-minute human attempt, sound, accessibility polish, full display/hardware coverage, and the performance soak gate remain required. Automated simulation does not establish those outcomes.
