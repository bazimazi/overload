# Production foundation through P07

6 October 2026. The owner requested implementation through P07. The independent S06 session remains pending; this implementation does not close the human acceptance gates.

## Delivered behavior

| Task | Behavior and evidence |
| --- | --- |
| P01 | Versioned release catalog and JSON schema cover 25 categories. The validator reports definition paths for missing references, duplicate IDs, prerequisite cycles, localization and animation bindings, and unsafe world-rule budgets. Reports list 63 representative entries. This is schema coverage, not a complete release roster; additional Frames and regions remain A tasks. |
| P02 | `rooms.v1` selects a seven-chamber tour, seeded pillar geometry and a bounded branch/checkpoint/boss graph from the existing court kit. Conservative footprint flood checks verify entrance/exit sockets and spawn lanes. Generation retries at most four times and has an open authored fallback. Seed, version, selected rooms, graph and geometry persist in each new activity. The report checks 10,000 seeds and retains 24 examples. Rooms currently transition through checkpoint menus rather than seamless exploration. |
| P03 | Hunt uses marked elites; Breach uses mixed wardens and environmental pressure; Vault requires touching a guarded keystone after defeating each room's guardians. All four regions offer every family at any unlocked tier. Each activity pays the same immutable XP and wallet budget. Tier 10+ awards six regional Seals and a distinct family proof; completing the three proofs pays one 12-Seal breadth bonus. |
| P04 | Separate Standard profiles begin at level 1 without synthetic currency. Four mastery quests require an actual evade, cover break, Stillness hit and boss stagger. The earned ritual requires level 100+, campaign completion, all four masteries, 12 proofs, 120 Seals per region and the trial. Ownership and all four debits are one transaction. The three-stage trial creates a separate normalized actor with level-100 power, 20 Might/Resolve, grade 10 and Rare gear; legal build choices remain, Override is disabled, and persistent power is untouched. |
| P05 | Stillness requires 48 ticks within an 8px radius followed by a base hit. Rupture requires a base stagger break, a 120-tick type cooldown and a 360-tick target lockout. Overloaded descendants cannot generate either. Focused trades area for a 125% narrow single-target lane; Shatter trades damage for a 75% wide hit with doubled stagger; Cascade spends both for a delayed 160% cone at 50 Strain. Hollow mastery, Crown mastery and the trial guarantee their respective discoveries. The content quota of twelve additional patterns has not been filled speculatively. |
| P06 | Six rules use authored, visible rectangles or breakable cover, with warnings of at least 60 ticks and a common movement-only safe lane. Early chapters use one rule; later chapters use two. Four Bellkeeper mutations add a return mark, wider/slower five-bolt fan, longer preparation, or lateral procession. Anomaly Hunts disclose a chosen mutation and target the selected route's loot family, with ordinary rewards. Heavy attack reservations cap concurrency at two. |
| P07 | Three regional legs bank normal rewards and frontier independently. Leg two adds 10% completion gold/Alloy; leg three adds 20%. Death spends the current leg's bonus eligibility before retry; extraction retains banked XP/wallets/proofs/Seals. Neither bonus multiplies XP or Seals. The Remembering Sovereign uses the return-mark boss behavior and awards an anchor-halo visual, Codex text and a local record. Last-64 records retain exact tier, time, assistance classification and a compact build including gear stats. |

The graph and regions reuse the court art kit and Warden/Bellkeeper roster. These are functional production foundations; they do not substitute for A01/A02 regional art, encounter variety or the remaining Frames. The six world rules are deliberately bounded first implementations, not the entire future environmental simulation.

## Saves and reward conservation

Schema 4 preserves existing XP, equipment, wallets, frontier and active `fracture.v1` expeditions. New regional runs use `fracture.v2` and store generated layouts. Unknown run/generator versions stop loading and preserve files. Current chapter offers remain saved; route screens now disclose their laws. Save transactions validate proof sets, chain region/order/leg, run identities and bounded records.

The Seal arithmetic test completes 18 qualifying expeditions in each region: `18 * 6 + 12 = 120`. The three breadth completions count within the eighteen. It checks all 72 completions, 12 flags, four masteries, one-time bonuses and the 64-record bound. This proves arithmetic and idempotency, not human acquisition time. Anomaly and chain results cannot create extra proof identities or multiplied Seals.

Ritual and second-leg payout tests interrupt all five write stages. Trial-proof interruption preserves original progression and gear. Engine checks reload real disk snapshots after activities, trial, ritual and chain legs, including death's lost bonus eligibility. A trial restart uses a fresh temporary actor and never writes its normalized equipment to the character.

Records measure simulation time banked at successful checkpoints and deaths. Mid-combat exit resumes the entry checkpoint; unsaved time is not a trusted competitive record. Best times displayed at the selected tier are drawn from the last 64 stored runs, separately by standard/assisted settings. Hazard assistance halves environmental damage and is retained by a saved active run.

## Verification and reproduction

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1 -Engine
dotnet run --project src/Overload.Tools -c Release --no-build -- content-report artifacts/production-p07
dotnet run --project src/Overload.Tools -c Release --no-build -- generator-report artifacts/production-p07
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/package-slice.ps1 -SkipExport -Variant Production
```

**172 domain/content tests**, **126 baseline**, **89 endless**, and **156 production engine assertions** pass.

Domain/content checks cover qualification thresholds, normalized gear replacement, activity budgets, all allowed rule pairs, new-pattern/Assault/Elsewhere combinations, provenance, stale claims, extraction and interrupted writes. The retained baseline suites protect earlier Overload and E05 behavior. Production engine fixtures exercise actual base-hit memory generation, real collision/spawn paths, Vault objectives, all families, trial hazard transitions, oath debit/reload, chain death/bonuses and four mutations. Fixture kills accelerate progression; they are not claims of player victories.

Current evidence is under `artifacts/through-p07-validation.log`, `artifacts/p07-engine.log`, `artifacts/p07-direct-export.log`, `artifacts/export-production-smoke.log` and rendered smoke/capture logs. Screenshots are under `artifacts/screenshots-production`. Generated category and layout reports are under `artifacts/production-p07`. The final status file records verified counts and package location.

The Windows export helper now launches the actual editor with redirected logs and isolates its MSBuild workers. This resolves console-helper shutdown and intermittent publish-worker stalls observed during candidate preparation.

The independent S06 session, physical controller feel, actual tier/chain durations, oath balance, regional variety, broad hardware performance and a 90-120 minute mixed endgame session still require human evidence. The generator's connectivity and shared safe-lane tests establish structural limits, not encounter enjoyment or every possible enemy/hazard timing interaction.
