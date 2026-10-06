# Quality and release validation

6 October 2026. The request extends through R02. Quality tooling, accessibility fixes, a review candidate and save compatibility checks are implemented. Human balance/accessibility acceptance, additional hardware, release approval and evidence-based expansion selection remain open. No public release or expansion content is claimed.

## Changes

- Runtime-backed ordinary-gear campaign and fixed endgame encounter reports; each comparison includes time, damage, Life, Focus, flasks, signatures and unused-memory opportunities.
- Repeatable rendered performance, accelerated expedition/save soak, eight-menu display matrix and isolated clean-install checks in `build/quality.ps1`.
- Responsive scrolling menus, 100%/125% text, high contrast, accessible names on wrapping buttons, and pause on window focus loss/controller disconnect. UI uses output pixels; the world retains independent integer scaling at 640x360. Minimum window remains 960x540.
- Bounded projectile/ground-effect saturation stops further emissions and records a bounded diagnostic instead of reporting an engine failure. Saturation does not refund accepted action costs or remove existing effects. Critical telegraphs are retained.
- Measured hot paths prompted native query/result disposal, one redraw request per render update after physics, a bounded 128-entry effect-text layout cache and three projectile drawing batches. Normal rendering is capped at 120 FPS around the unchanged 60 Hz simulation. Attack warnings and damage geometry remain intact. Subsystem timing and failed initial measurements are retained.
- Fresh ordinary installs now create a level-1 Standard Warden without synthetic currency; existing profiles load unchanged. Separate Frame selection and accelerated training profiles remain available.
- Unknown character content versions now receive the same preserve-and-upgrade treatment as future schema, expedition and generator versions, preventing fallback to an older backup.
- Candidate archives contain the complete runtime, dirty working-source snapshot, dependency locks, toolchain/content/source/file hashes, validation evidence, asset provenance and human review/recovery instructions. Older archives are preserved.

## Automated evidence

The solution builds without warnings/errors. **204 domain/content tests pass.** Thirteen added cases cover schemas 1–5 at enormous saved values, partially claimed v1/v2/v3 runs, and byte-preservation of every save/backup file when a future schema/content/generator is encountered. Existing save-failure injection, Covenant, migration, exact progression and reward tests also pass.

All four suites pass in the current standalone export: **126 arena, 89 endless, 156 production and 327 expansion checks**. Clean Standard-profile installation checks create each Frame, bank the first campaign checkpoint and reload it. Those checks intentionally defeat enemies with fixture commands to validate persistence; they do not establish human victories.

The ordinary-gear report contains **306 successful modeled comparisons**: all sixteen campaign checkpoints per Frame with earned XP/ordinary deterministic drops, and seven endgame samples per Frame at tiers 1, 10, 11, 1,000 and 10^30. Samples cover stationary/moving targets, a three-enemy pack, mixed ranged pack, staggerable elite, Bellkeeper and hazard arena. Both all-attacks-connect and two-of-three-attacks-avoided assumptions are reported. No item quality upgrades, rare affixes or oath power are required by these fixtures.

The model uses actual authority/math but assumes hit geometry, simplified hostile cadence and movement. It excludes critical rolls and real boss navigation/fan/mark behavior. Modeled campaign combat totals are only roughly two minutes, excluding travel, menus, missed attacks and learning. This is a pacing warning to investigate with players, not a measured campaign duration or a justification to add repetitive grind. No balance numbers were changed merely to force a desired model result. Details are in `artifacts/quality/BALANCE.md` and `ordinary-gear.csv`.

The display matrix renders **80 cases**: Settings, Bindings, Inventory, Character, Fractures, Frame chooser, Controls and Tutorial at 960x540, 1280x720, 1920x1080, 2560x1440 and 1920x800, each with 100%/125% text. It verifies actual window size, menu containment and visible focus; captures are retained. Small-window settings, inventory and bindings were visually inspected. Synthetic controller routing/disconnect, held-input release and presentation toggles pass. This covers windows on one machine, not five physical displays or a screen-reader usability study.

## Runtime measurements

Final standalone soak and performance measurements are recorded in `artifacts/quality/soak.json`, `performance.json` and `ordinary-performance.json`. The full default-renderer stress passes on this development machine; broader hardware acceptance remains open.

| Measurement | Result |
| --- | --- |
| Expedition/save loop | 3,600 simulated seconds in 116.04 wall seconds; 120 commanded checkpoints committed/reloaded, 26 bounded run records and 64 receipts |
| Retained managed memory / working memory | Managed 2.53 to 2.59 MiB; last 20 retained samples span 16.9 KiB. Peak working set 160.71 MiB; last 20 working samples 146.0–153.8 MiB; nodes 49–58 |
| Rendered 5-minute stress, 30-second warmup | Mobile/Vulkan, 35,819 measured frames; p95 10.68 ms / p99 14.56 ms, passes both targets; 89 Overload actions; peak working set 497.05 MiB |
| Ordinary rendered sample, 30-second warmup | Mobile/Vulkan, 60 measured seconds / 7,200 frames; p95 8.90 ms / p99 9.13 ms, passes both targets |

The soak accelerates physics callbacks with a 32x clock while retaining 1/60-second physics deltas. Every commanded checkpoint is flushed, committed atomically and reloaded. It also executes live movement, enemy scheduling and effects. The recorded simulation hour is not a wall-clock hour; a real-time endurance session remains open. Expected persisted inventory/records grow within existing finite limits; inspect memory distribution alongside that growth.

Stress uses 60 active enemies, the actual 200-projectile list, four ground effects, an echo and authority-constrained signature dispatch. Synthetic supplies/memory evidence keep the stress scene populated. Ground and echo effects are combined to stress both implementations; this is a heavier mixed fixture than one normal Frame can equip. Loot is banked at checkpoints; there are no live floor pickups in this implementation. Frame intervals use Stopwatch wall time with VSync disabled, the normal 120 FPS rendering limit and no fixed-FPS override. The full quality sequence leaves 125% text selected before measuring performance. Headless soak timings are not renderer measurements.

The first uncapped full stress failed at p95 141.54 ms / p99 184.73 ms. Query disposal, redraw scheduling and text caching improved it to 29.37 / 37.92 ms, still a failure. Projectile batching cut measured effect-draw CPU time substantially; bounding redundant rendering passed a short diagnostic but the full Compatibility run failed at 32.32 / 59.66 ms, including late frames in ordinary encounters (24.60 / 47.86 ms). A 30-second Mobile/Vulkan diagnostic passed at 14.75 / 18.73 ms. The final five-minute default-renderer result above determines the local gate. Failed reports remain in the evidence archive. `stress.png` was visually inspected for projectile shapes, tails, cores and attack warnings.

The measured machine is a **Core Ultra 7 255H, 16 cores, Intel Arc 140T, driver 32.0.101.8132, Windows 10 Enterprise 10.0.19045, approximately 15.4 GiB visible RAM**. The candidate defaults to Mobile/Vulkan after Compatibility/OpenGL missed the sustained target; Compatibility remains an explicit launch option. Hardware metadata contains no machine IDs. This is the development reference, not a measured modest four-core minimum. Other GPU/CPU/display/DPI cases and physical controller feel/reconnect remain pending; no minimum system requirements are published.

## Install/update and archive

`release-update` loads the actual SHA256-verified A03 baseline assemblies into a separate context. They decode/re-encode three synthetic high-level active saves; the current code opens the resulting files, preserves exact counters/geometry/budgets and commits their remaining four groups. Tested tier is `10^40 + 123`, with wallets up to 90 digits. Cold fixture load times were approximately 11–81 ms on this machine, below the three-second checkpoint budget. This is local synthetic-save evidence, not a survey of players' saves.

`build/release.ps1` creates a uniquely named ZIP and `artifacts/LATEST-CANDIDATE.json`. It requires a completed standalone quality run and verifies the tested runtime and report hashes before packaging. It archives the actual working files, including untracked authored content; a base commit and dirty flag alone would not reconstruct this workspace. Toolchain packages are identified by hashes and remain in `.tools`; the package records reinstall/rebuild instructions. Godot and the bundled .NET runtime license/third-party notices are included. Rebuilding byte-identical binaries has not been established.

`build/verify-candidate.ps1` checks the ZIP hash, every packaged file and every archived source file, extracts into a fresh directory containing spaces, then launches all four standalone suites and the Standard-profile installation check. Its result is `artifacts/clean-install-verification.json`. It does not touch personal character folders.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1 -Engine
dotnet run --project src/Overload.Tools -c Release --no-build -- quality-report
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/quality.ps1 -Exported
# Historical runtime directory comes from the verified A03 ZIP:
dotnet run --project src/Overload.Tools -c Release --no-build -- release-update <baseline-runtime-directory> artifacts/quality/update
powershell -NoProfile -ExecutionPolicy Bypass -File build/release.ps1 -SkipExport
powershell -NoProfile -ExecutionPolicy Bypass -File build/verify-candidate.ps1 -Archive <candidate-zip>
```

## Remaining task gates

| Task | Delivered | Still required |
| --- | --- | --- |
| Q01 | Ordinary-gear model, live accelerated save/runtime loop and rendered performance evidence | Independent campaign/endgame pacing and balance; real-time hour; compare oath tradeoffs with players |
| Q02 | Accessibility/input interruption fixes, 80 rendered menu cases, synthetic input checks, one-machine measurements | Physical controller, accessibility usability, DPI/fullscreen and additional reference/minimum hardware |
| R01 | Standalone review candidate, exact source/toolchain/evidence archive, clean install and archived-baseline update checks, recovery/reporting process | Human release review/signoff; support contact; final art/store asset acceptance; remaining Q gates |
| R02 | High-level and active-run compatibility; future-version preservation; expansion evidence intake/checklist | Reviewed baseline and real player evidence to choose an expansion before shipping content |

The owner previously arranged an independent tester; no report has arrived. [Release review and expansion checklist](RELEASE_REVIEW.md) records the concrete next human steps. An expansion has not been selected and no extra content was shipped under R02.
