# Validation through A03

6 October 2026. Implementation candidate for A01-A03; human acceptance remains open.

The candidate adds Threadseer and Revenant, expands Warden to eight skills, provides three equal-budget profiles per Frame, adds the four-region campaign and finite regional roster, and implements earned Red Covenant. Optional Unfinished is cut after the [design/QA spike](decisions/0008-frames-regions-covenant.md). Item counts remain unchanged pending human build evidence.

Final verification: **191 domain/content tests**, **126 arena + 89 endless + 156 production + 327 expansion checks** in source and standalone; rendered standalone expansion also passes 327 checks with empty stderr. The A03 archive verifies all 198 manifest files.

## Evidence and limits

- Build, domain/content tests and all four engine suites are reproduced by `build/test.ps1 -Engine`. The expanded catalog validates 189 entries across 25 categories, including the mechanical 24-skill/24-family/eight-boss/48-room pool.
- Domain coverage includes each Frame's legal allocations, 4,000 regional layouts, old/new saved expeditions, schema-4 migration, sixteen campaign receipts, Covenant cap/minimum/expiry/maximum-Life/healing, zero-Focus casting, rejected/stale commitments, foreign skills, single selection and every injected atomic ritual write stage.
- `frame-report artifacts/expansion-a03` writes exact runtime-derived CSV values. All 216 modeled comparisons complete; all nine base-contract profiles complete four ordinary regional samples and eight bosses. Paired Covenant wins show 0.00% median time improvement in this model. The model assumes one stationary target, all direct hits connect, no criticals/memories and avoidance of two out of three hostile attacks. It cannot establish human viability, enjoyment or the oath's approximately 15% balance guardrail.
- The expansion engine suite exercises every skill through live geometry, each saved training build, Frame-specific equipment comparison, all regional rosters/bosses, actual Elsewhere place/return in every room, the normalized Covenant challenge, ritual/selection/reload and sixteen campaign checkpoints. Progression fixtures directly defeat enemies to test receipts; they do not demonstrate human campaign or challenge victory.
- Source/export logs and rendered captures live under `artifacts/a03-*`, `artifacts/export-*-smoke*` and `artifacts/screenshots-expansion`. Final counts and archive evidence are recorded in [STATUS](STATUS.md).

## Save compatibility

Schema 5 retains exact counters, inventories, wallets, receipts and old oath ownership. Old characters remain Warden and keep the eight-room court. Active fracture.v1/v2 and rooms.v1 expeditions retain their geometry. Newly begun regional expeditions use fracture.v3/rooms.v2, selected from validated regional templates. Reservations, ground casts, echoes, barriers and challenge stages are temporary; they reset at entry checkpoints and are never serialized as a mid-combat continuation.

## Remaining gates

S06 independent feedback, three enjoyable builds per Frame, mixed real-player oath comparisons, campaign/run duration, physical controller handling, final sprite animation, navigation under dense combat, stress profiling and broader hardware/display coverage remain open. There are twelve talent nodes per Frame and nine shared signatures; the larger design budgets are not claimed as finished release content. The room pool shares authored arrangements across regional palettes, and expeditions retain the seven-chamber checkpoint-tour adapter rather than a seamless branching world.

No commits, publishing, remote operations or messages to testers are part of this implementation.
