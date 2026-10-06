# E01-E05: endless progression foundation

6 October 2026. The owner explicitly extended implementation through E05 while the S06 independent playtest remains pending. This report records code and automated evidence; it does not close the human acceptance gates.

## Play and reproduce

Finish the eight-room court, then open **Fracture board and attunement** at Hearth. Choose an unlocked tier, complete six encounter groups and the boss, and buy earned shared grades at the forge. The board can resume an unfinished expedition. Chapter offers appear after tier 10, 20, and every later ten-tier boundary; choose one before starting another run. Previously chosen routes remain selectable without resetting the frontier.

For direct inspection without changing a normal save, launch the exported executable with `-- --fracture-practice=9`, `=10` or `=11`. Each invocation creates a separate sandbox profile at the tier's reference level with grade one behind the chosen tier. These profiles have no production oath qualification. The ordinary board never grants frontier progress from a practice profile.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1 -Engine
powershell -NoProfile -ExecutionPolicy Bypass -File build/balance.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/package-slice.ps1 -SkipExport -Variant Endless
```

The new CLI report command is `dotnet run --project src/Overload.Tools -c Release -- balance-report artifacts/balance-e05`. It exports `xp.csv`, `tiers.csv`, `forge.csv`, `encounter-sweep.csv`, `benchmarks.csv` and a methodology README. All calculations call the runtime rules; spreadsheet formulas are not a second implementation.

## Implementation boundaries

- Player damage and Life use `foundationSubunits * (50 + resonance) * (grade + 24) / 1250`, rounded once at the final combat-subunit boundary. Afterstrike splits the already-scaled root budget; it cannot reapply grade or Might.
- Enemy Life and damage use the chosen tier's squared scale. Attuned defense and tier K cancel their common denominator before mitigation. Enemy speed, warnings, projectile counts, stagger thresholds, Focus, critical chance and Strain do not scale with tier.
- Bulk forging uses an arithmetic-series quote and one save transaction. Both wallets and grade change together; permission stops at the highest cleared tier. An affordability search jumps to the highest affordable earned grade without iterating over grades.
- An expedition stores its ID, monotonic BigInteger sequence, content version, canonical-tier-derived seed, selected route, seven authored room IDs, immutable XP/wallet budgets, claims and checkpoint. Six groups receive `floor(XP budget / 10)` each. The boss pays the exact remainder plus `200T` gold and `100T` Alloy. No independent kill XP exists.
- A new run can pay again. A duplicate claim, old expedition, repeated completion, or lower-tier replay cannot increment frontier twice. Abandonment keeps previously banked group XP and forfeits the completion budget. Supplies reset on retry and checkpoint entry.
- Routes select one of four target item families and affect the seeded choice/order of authored rooms. A full bag forfeits the targeted drop; this is disclosed before entry. World rules, mutations, generated geometry, three activity families and regional Seal/proof rewards remain P02/P03/P06 work.
- Save schema 3 migrates schema 1/2 without changing XP, gear or wallets. Recovery, interrupted forge/completion/route writes, and a 4 MiB operational pre-parse file guard are tested. Unknown active-run content versions block loading rather than silently reinterpret a run. Standalone expeditions persist explicit chain ID/leg metadata as null/zero; actual three-leg gameplay and bonuses remain P07 work.
- Chapter offers are generated inside the completion transaction before display. Reload cannot reroll them. Known route storage remains bounded by the finite authored route catalog. UI abbreviations never become save values; full decimal counters are copyable in Character's ledger.

## Acceptance evidence

**143 domain/content tests**, **126 baseline arena assertions**, and **89 endless-engine assertions** pass. The engine suite enters all seven groups at tiers 9, 10 and 11, delivers actual melee/projectile damage, checks reachable spawn lanes and fixed tell budgets, retries without changing reward identity, reloads the real character store, banks exact run budgets, unlocks the next tier, forges an earned grade and reloads/selects chapter offers. Group clears are fixture-driven; this is not a bot victory claim.

| Case | Evidence and applicability |
| --- | --- |
| EP01 | 59-61, 99-101 and 999-1001 crossings retain exact XP and conserve foundation/Resonance points. |
| EP02 | Frontier completions at 9, 10, 11, 999 and 1000 unlock T+1. No final-tier branch. |
| EP03 | Lower-tier replay, repeated claims, new runs and stale claims after 70 subsequent completions are tested. |
| EP04 | Old-tier budgets and room seeds remain identical at different player levels. Enemy profiles take a tier, not a character level. |
| EP05 | Matching balanced profiles at 1, 10, 100 and 1000 match enemy scaling; grade/T defense cancellation and inherited Afterstrike budgets are tested. |
| EP06 | 60-digit tiers, 150-digit wallets, exact save reload, forge and combat values exceed signed 64-bit safely. Benchmarks extend to 300-digit tiers. |
| EP07 | A single reward crosses one million levels with bounded-search XP inversion and finite derived foundation budgets. |
| EP08 | Bulk costs equal individual costs; overspending/unearned grades fail; interrupted and duplicate commits conserve both wallets and grade. |
| EP09 | Completion/route choice interrupted at five write stages recovers the same complete budget, frontier and offered choices. Engine disk reload confirms the integration. |
| EP10 | Applicable authored assembly bounds, connected paths, fixed warnings and stagger are tested at ordinary and huge tiers. Procedural generator/rule-pair fairness tests require P02/P06 and are not claimed. |
| EP11 | A high-level Standard character cannot call the sandbox ritual or acquire an oath automatically. Production proof qualification and normalized Trial of Contradiction require P04; that portion is not claimed. |

## Balance inspection

The stationary trading model uses real action clocks, Focus, healing, damage and player defenses. It deliberately omits critical hits, movement, dodge geometry, memories and boss volley fan geometry. It cannot predict human run duration. Matched means Q(T), G=T; entry grade is T-1; undergeared is G=1; overleveled is Q(T)+40 with G=T. All use the same starter gear and no allocated skill/talent points.

| Tier | XP/run | Completion gold / Alloy | Matched boss clear | Entry-grade clear | Undergeared clear | Overleveled clear |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 9 | 9,584 | 1,800 / 900 | 25.733s | 26.467s | 34.200s | 19.533s |
| 10 | 10,376 | 2,000 / 1,000 | 25.733s | 26.467s | 35.100s | 19.967s |
| 11 | 11,200 | 2,200 / 1,100 | 25.733s | 26.467s | 36.067s | 19.967s |

The near-identical matched results support the scaling algebra. Undergeared survivors finish at about 90, 60 and 27 Life respectively, exposing the intended survivability gap. These short isolated boss samples do **not** demonstrate the proposed 12-20 minute expedition duration. Seven existing court rooms are an early balance fixture; full activity compositions and human pacing must be evaluated later. No unsupported balance changes were made just to force a duration target.

The first local warmed samples (20 iterations each) measured about 6/17/47/283 ms total for XP inversion at 20/60/120/300 digits, and 15/30/58/382 ms for checksummed save decode/validation. Combined combat-scale/forge calculations remained under 1.2 ms total in that sample. See the generated CSV for raw timings and allocations; these are hardware/run-dependent measurements, not performance guarantees. Very large save validation is intentionally outside the combat tick.

## Artifacts and open human checks

Logs: `artifacts/through-e05-validation.log`, `artifacts/e05-baseline-engine.log`, `artifacts/e05-endless-engine.log`, and the standalone export/render logs. Capture mode `-- --capture-endless` creates isolated board, forge, chapter, tier-9/10/11 boss and 10^30-tier UI screenshots under `artifacts/screenshots/endless-*.png`.

The owner still needs the independent S06 session. Follow up with actual tier-9/10/11 play to assess control, boss fairness, route clarity, willingness to repeat, deaths and time per tier. Automated model clears, synthetic inputs and screenshots do not close those gates.
