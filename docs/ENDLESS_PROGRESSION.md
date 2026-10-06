# Overload endless progression specification

Character levels, repeatable power growth, equipment attunement, and Fracture difficulty have no designed maximum. This is a confirmed product requirement. The game must continue awarding XP, increasing the same character-level counter, and offering a harder tier after any completed frontier tier. This specification replaces the original finite progression model.

All equations are initial balancing proposals. They define a coherent implementation and test model, not measured player pacing. Level 60 completes the foundation, level 100 is an Override qualification milestone, and tier 10 begins its resource journey. None is an endpoint.

## Progress that continues indefinitely

| Track | How it advances | Ongoing benefit |
| --- | --- | --- |
| Character level | Earn XP from combat and objectives | Visible level increases forever; no forced reset |
| Resonance | Every level after 60 grants one point | Uncapped Might or Resolve ranks increase damage or Life |
| Equipment attunement | Spend gold and Alloy up to the highest tier cleared | One shared grade scales equipment power without more affixes |
| Fracture frontier | Clear the highest available tier | Unlock the next difficulty and its rewards |
| Palimpsest Chapters | Complete successive groups of ten tiers | Choose new combinations of encounter rules and targeted rewards |
| Personal mastery | Complete optional route and oath challenges | Records, cosmetic variants, and more ways to test builds |

Keep the skill and talent budgets, equipment slots, memory capacity, three active bindings, and one active Override. These are simultaneous choice limits. They prevent an old character from eventually equipping every build at once, while the level and power tracks continue growing. Maintain limits on speed, avoidance, effect recursion, and concurrent enemies to keep the combat playable.

## Character levels and XP

There is one level counter starting at 1. Use this provisional cost to advance from level L to L + 1:

```text
X(L) = 1,000 + 50L + 5L²

TotalXPForLevel(L), with n = L - 1:
  1,000n + 25n(n + 1) + 5n(n + 1)(2n + 1) / 6
```

Evaluate the sum using exact integer arithmetic; the last term is integral when calculated as a complete numerator before division. Do not divide its factors separately. Level 1 requires zero total XP. XP is never discarded at 60, 100, a chapter boundary, or a UI display threshold.

| Current level | XP to next level | Meaning |
| --- | --- | --- |
| 15 | 2,875 | Early progression continues beyond a slice session |
| 60 | 22,000 | The next level grants the first Resonance point |
| 100 | 56,000 | Override eligibility does not stop leveling |
| 1,000 | 5,051,000 | Same level and XP systems continue |

Campaign quests and enemy rewards need their own authored budgets to reach roughly level 35–45 at the campaign's end. Validate those totals against this curve; do not infer campaign pacing from the formula alone. No level drain on death, no mandatory prestige reset, and no daily XP limit.

For endgame balancing, define reference level `Q(T) = 60 + 4(T - 1)` for tier T. This is a recommendation used to calculate rewards and evaluate balance, not a minimum entry requirement and not automatic enemy scaling to the player. A full expedition's baseline XP is `floor(X(Q(T)) / 5)`.

Allocate 60% of that budget across six mandatory encounter/objective groups and 40% to final completion. Assign each mandatory group `floor(budget / 10)` and give the final completion the remaining amount, conserving the exact total. Alternate activity templates must conserve the same declared budget. Summoned, endlessly respawning, or resurrected enemies have no independent repeat XP. Each group reward is a saved one-time claim for the expedition instance; restarting an already-paid room does not repay it.

Optional rooms take their rewards from a separately disclosed authored bonus budget. No award is a percentage of the player's own next-level requirement: repeatedly farming easy content must not keep accelerating just because the character levels up. Older content always gives its fixed positive XP, so a player can still make slow progress while farming comfortably. Starting a new expedition intentionally allows earning its rewards again.

At the reference level, an expedition initially supplies about one fifth of a level's XP; at a fixed lower tier, leveling gradually takes longer. Run duration, failure, optional objectives, and build performance determine actual time. Track XP per minute for farming and pushing; the formula is a baseline to tune, not a mandate that every five runs always grant a level.

## Resonance allocation

The available point total is `max(0, Level - 60)`. One point buys one rank of either Might or Resolve, and each track has no maximum rank. Free reassignment at Hearth enables experimentation. Unspent points are retained exactly. Offer an opt-in auto-allocation rule: balanced, offense-first ratio, defense-first ratio, or manual. New profiles default to a clearly displayed balanced rule, alternating Might first and Resolve second; disabling it returns control to the player.

```text
DamageResonance = 1 + Might / 50
LifeResonance   = 1 + Resolve / 50
Might + Resolve + Unspent = max(0, Level - 60)
```

Each rank adds two percentage points of the foundational value. Ten ranks mean 1.20 times that value, not 1.02 raised to the tenth power. Marginal percentage gains diminish relative to an already strong character, but absolute power does not approach a ceiling. Both tracks are useful at every level.

Might scales the final offensive budget of attacks and damage effects once; descendants inherit it without reapplying it. Resolve scales maximum Life once. Percentage-based healing and barriers use the resulting maximum Life; any flat heal must explicitly use the Life scaling channel. Stagger generation, Focus, Strain, token duration, skill slots, critical probability, speed, and invulnerability do not grow from Resonance.

Do not spend Resonance points inside the existing talent tree, which would eventually erase its tradeoffs. Do not grant an extra oath slot at level 1,000 or reduce ritual requirements at a high level. Numerical advancement and authored behavioral choices remain different systems.

## Equipment attunement

Equipment keeps its six slots, two ordinary affixes, three quality upgrades, and one replaceable affix slot. Five foundation bands establish base-item variety; no infinite sequence of rarities or added affix lines is needed.

At the campaign's conclusion, unlock **Attunement Grade 1** at the forge. The character's equipped arsenal shares this grade, including subsequently equipped items. Its growth is an equipment progression channel applied to the complete calculated combat values; it is not a per-item roll. The UI displays the shared grade beside the loadout and includes its contribution in comparisons.

```text
S(x) = 1 + (x - 1) / 25, for integer x >= 1

Attunement damage multiplier = S(G)
Attunement Life multiplier   = S(G)
Attunement Armor multiplier  = S(G)
Attunement Resistance multiplier = S(G)
```

G is the current shared grade. Grade 1 is the baseline grant; every higher grade requires clearing at least that Fracture tier. The invariant is `1 <= G <= max(1, HighestClearedTier)`. Clearing a tier authorizes a grade, but does not automatically pay for it. A single upgrade from grade G - 1 to G costs `25G Alloy` and `50G gold`, for G >= 2. Bulk upgrades use the exact sum of those costs and one atomic transaction; grade cannot skip past the earned frontier.

Baseline expedition completion at tier T grants `100T Alloy` and `200T gold`, in addition to authored loot and small encounter drops. The fixed crafting costs for changing an affix and upgrading item quality remain separate. This starting schedule makes attunement affordable near the frontier; tune it so a player is not required to repeat dozens of runs just to synchronize six slots. Large stockpiles cannot buy unearned tiers.

Gear band, quality, and affixes first determine foundational values. Apply shared attunement once afterward. Do not scale weapon Attack Power by grade and then scale its resulting attack by grade again. Focus capacity, Focus regeneration, movement speed, critical chance, skill haste, and fixed Relic behaviors retain their established limits. A flat damage proc uses the damage channel once; a percentage-of-Life effect uses the already-scaled Life value.

Loot remains useful for alternate builds, better permitted rolls, new Relic choices, and salvage. A perfectly rolled favorite item can remain useful as its shared grade rises; the endless chase does not require finding that identical item again at every tier. Targeted rewards improve the chance of the requested item family, not the number of mandatory affixes.

## Unlimited difficulty scaling

Tiers begin at 1 and continue indefinitely. A new endgame character has highest unlocked tier 1 and highest cleared tier 0. Successful completion of tier T sets highest cleared to at least T and unlocks at least T + 1. Completion and unlock use the same saved transaction as the reward. Replaying a lower tier cannot increment the frontier. The initial design advances one tier at a time, with no gear-based access gate or timed completion requirement.

For an enemy whose authored tier-1 template has Life H and damage D:

```text
EnemyLife(T)   = H * S(T)²
EnemyDamage(T) = D * S(T)²

PlayerDamage = foundationalAttackBudget * DamageResonance * S(G)
PlayerLife   = foundationalMaximumLife * LifeResonance * S(G)
```

Use the same player growth factors for all skills and their Overloads. Enemy templates, encounter composition, and optional modifiers still change tactical difficulty. Multipliers are calculated from the chosen tier; they never inspect player level or equipment at runtime.

A balanced character at `Level = Q(T)`, with `Might = Resolve = 2(T - 1)` and `G = T`, has the same overall numerical scaling factor `S(T)²` as the tier's enemies. This gives a coherent reference for continuing progression without an eventual mathematical wall. Entry to the next uncleared tier usually has grade one behind; the small gap is intentional. A skilled or specialized build can push ahead of the reference, and a farming character can overpower earlier tiers.

| Tier | Reference level | Enemy Life and damage multiplier | Balanced player multiplier at matching grade |
| --- | --- | --- | --- |
| 1 | 60 | 1.0000 | 1.0000 |
| 10 | 96 | 1.8496 | 1.8496 |
| 100 | 456 | 24.6016 | 24.6016 |
| 1,000 | 4,056 | 1,677.7216 | 1,677.7216 |

This is an algebra check, not proof of real balance. Skill uptime, damage distribution, stagger, defense, and player execution require actual encounters. A character investing everything into offense deliberately falls behind on survivability; allow respec and communicate that tradeoff instead of silently flattening enemy damage.

For defense, retain the initial maximum reduction rules but replace the old capped-tier constant with `K(T) = 120 * S(T)`. Armor and Resistance scale with `S(G)`, so comparable equipment keeps comparable mitigation at comparable tiers. Physical reduction is `min(0.65, Armor / (Armor + K(T)))`; supernatural reduction is `min(0.60, Resistance / (Resistance + K(T)))`. During the campaign, use each encounter's authored K value. Do not reinterpret campaign difficulty bands as maximum Fracture tiers.

Boss stagger is normalized: authored action stagger values and boss thresholds remain in a stable scale, with bounded authored modifiers. Reaching a higher tier does not make staggering mathematically impossible. Percentage damage against enemies is either excluded or explicitly budgeted and capped per action; arbitrary percent-health attacks must not bypass all scaling.

Enemy movement speed, tell duration, attack concurrency, projectile count, and effect recursion do not rise indefinitely. Challenge continues through uncapped power requirements and changing combinations within a readable combat language. Never shorten a warning to zero, require impossible inputs, or increase monster count until hardware fails.

## Palimpsest Chapters and changing encounters

`Chapter(T) = 1 + floor((T - 1) / 10)` and `ChapterStep(T) = 1 + ((T - 1) mod 10)`. The chapter index is uncapped; the step is only a position within a repeating structure. There is no final chapter.

At the end of each chapter, offer three seeded next-chapter routes. Each proposal states region emphasis, target loot family, a world rule, and its drawback. Save the offered choices before displaying them so reload cannot reroll them. Picking one changes the player's chapter route, not permanent character statistics. Allow returning to Hearth and selecting a known earlier route without losing frontier progress.

Author six world rules and four boss mutations for the first complete endgame. Start early chapters with one rule; later chapters select at most two compatible rules and one boss mutation. A compatibility graph rejects combinations that remove every escape route, require a particular build, or cover the arena continuously. Prefer unseen recent combinations, but permit repetition when the authored pool is exhausted. Do not promise infinite novel art, bosses, or mechanics.

| Example rule or mutation | Decision it adds | Fairness constraint |
| --- | --- | --- |
| Reverberation | A major attack repeats at its previous location after a visible delay | The repeat has its own outline and cannot overlap every safe exit |
| Glass Shelter | Breakable cover intercepts projectiles but is destroyed by heavy hits | Every Frame can break it; no room can trap the actor |
| Migrating Vents | Safe lanes move between announced environmental pulses | Fixed minimum warning and a reachable safe lane |
| Anchored Bellkeeper | Boss places a visible return anchor before a volley | Destroying the anchor or moving away both work; player Override is unnecessary |

The world fiction is that deeper layers remember different versions of the same places. A room can have new sight lines, cover arrangements, enemy-role combinations, and law effects while retaining its readable tile kit. Artist-authored variants establish coherence; generation selects legal assemblies.

## Additional endgame goals

**Anomaly Hunts** let the player choose a named boss, one disclosed mutation, and a targeted loot family at an unlocked tier. They use the ordinary tier scaling and reward budget. They do not create a separate superior source of XP or mandatory new currency.

**Expedition Chains** join three regional expeditions. Each leg banks its normal rewards and frontier update. Continuing risks only an unbanked ordinary gold/Alloy bonus: 10% extra after leg two and 20% after leg three, calculated from that leg's baseline completion wallet reward. No bonus XP, additional proof flags, or multiplied Seals. Leaving early preserves banked gains. Save the chain position and bonus terms so disconnecting or reloading cannot duplicate a leg.

**Sovereign Echoes** are optional encounters with enemies demonstrating authored oath-like behavior. Clearing them awards a visual variant, a Codex account of the encounter, and a personal record. They never add an active oath slot, waive the qualification trial, or grant a required exclusive power multiplier. All Frames have a base-mechanic solution.

**Build records** keep highest tier, best completion time at a chosen tier, and a compact loadout snapshot. Distinguish standard and assisted settings in local records without removing progression rewards. Offline records are personal achievements, not trusted global rankings.

The P07 foundation implements chains with saved region order and three leg identities. A death forfeits that leg's unbanked optional wallet bonus; retry can still bank its ordinary rewards. Successful second and third legs pay their 10%/20% bonuses atomically with completion. Extraction preserves previously banked group XP and completed-leg rewards. The last 64 local records include exact tier, checkpoint/death simulation time, assistance label and compact skill/technique/talent/Codex/gear stats. Displayed best times are drawn from that retained window; unsaved mid-combat time is not a trusted competitive record.

The initial Sovereign Echo is the Remembering Sovereign, a Bellkeeper that places a visible return mark before moving and firing a volley. Moving away from the mark is a base-mechanic solution. Its reward is an anchor halo, a Codex description and a local record, without XP, Seals, frontier or oath qualification.

These systems extend play goals without requiring seasonal resets or live operations. A future content update can expand the rule pool and room kits while existing characters keep their levels and frontier. Record content version with seeds; do not silently reinterpret an active saved expedition using a changed generator.

## Override within endless progression

Follow the full [Override specification](CORE_SYSTEMS.md). The first oath requires level 100 or higher, 12 regional activity proofs at tier 10 or higher, four resource balances of 120, and the normalized Trial of Contradiction. A character can keep gaining levels and tiers throughout the journey and afterward.

Seals remain six per eligible expedition, regardless of how deep the player progresses. This preserves the journey's breadth; higher tiers already offer more XP, ordinary resources, attunement access, and records. Every qualifying expedition can advance both ordinary progression and ritual progress, so the rare system is not a separate pause in development.

Numerical power in the personal trial is explicitly normalized. Its temporary actor never overwrites the real character. Ordinary Fracture runs use the character's actual power. Reaching level 1,000 does not grant Override automatically, and possessing Override does not become mandatory at tier 1,000.

## Numerical representation and persistence

Use `System.Numerics.BigInteger` domain values for total XP, level, Resonance ranks, grade, frontier, gold, Alloy, and growing combat fixed-point quantities. Serialize them as canonical decimal strings rather than JSON numbers that another reader might round. BigInteger represents arbitrarily large integers; memory and compute remain finite, so this removes fixed-width gameplay overflow rather than promising literal infinite runtime. [Microsoft BigInteger documentation](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger?view=net-10.0)

Keep multipliers as exact rational/fixed-point calculations. For example, `S(T) = (T + 24) / 25`, so `S(T)² = (T + 24)² / 625`; do not cast T to a floating-point value first. Health and damage can use 1,000 integer subunits per displayed point, round at the documented final boundary, and cache computed profile factors. Physics coordinates, small bounded percentages, and animation times keep their ordinary engine representations.

For a health-bar fill, calculate a bounded ratio such as `currentLife * 10,000 / maximumLife` using integer arithmetic, then convert that small result for rendering. Scientific notation for labels is presentation only; full exact values remain available in detail views. Do not use formatted text as a save value or round a level up to abbreviate it.

Derive level from total XP using the cumulative polynomial with exponential-bound search and binary search. A large reward must not loop once per gained level. Derive Resonance point totals arithmetically, and enumerate only the finite foundational unlock milestones crossed by the reward. Validate duplicate reward IDs before adding XP. Bulk attunement uses arithmetic-series costs, not a loop for every grade.

Derive a generator seed by hashing the canonical tier string with run identity, route selection, and content version into the engine's supported seed size. A finite seed space may repeat layouts, which is acceptable; never truncate the actual tier or use the seed as its stored identity. Separate unbounded progression values from finite template indices and bounded runtime handles.

Store highest unlocked/cleared tiers as scalars, a sparse set of named challenge flags, and the last 64 detailed runs. Do not allocate one row or scene per tier, store every level-up event forever, or generate rooms for all future tiers. Archive additional diagnostic history separately if enabled. Persist the currently offered chapter routes and only generated active rooms. Compact completed-run reward receipts after committing an immutable run-end checkpoint, retaining transaction sequence and active-run receipts to prevent replaying stale claims.

Validate save format, sign, allocation conservation, grade/frontier relationships, and file size before parsing untrusted values. A malformed gigantic file must fail safely with recovery options, not freeze. An operational file-size guard is not a designed level cap. Benchmark very large legitimate values and document real costs.

No data definition may contain a final maximum character level or tier, clamp XP at a foundational milestone, or depend on a fixed array indexed by level. A finite list of templates supplies repeated content; formulas supply progression outside the observed playtest range.

## Acceptance and balancing gates

| ID | Scenario | Required result |
| --- | --- | --- |
| EP01 | XP crosses levels 59–61, 99–101, and 999–1,001 | Same level counter advances, XP retained, exactly correct points granted |
| EP02 | Successful frontier runs at tiers 9, 10, 11, 999, and 1,000 | T + 1 unlocked every time; no final-tier branch |
| EP03 | Replay a lower tier or resubmit a completion ID | Normal new runs pay once; duplicate claims cannot pay or advance twice |
| EP04 | Fixed old content visited at different player levels | Enemy stats and raw XP remain identical for the same content version |
| EP05 | Compare matching balanced profiles at tiers 1, 10, 100, 1,000 | Scaling matches the specified reference algebra; mechanics still require real playtests |
| EP06 | Huge values beyond signed 64-bit range, save/load, allocation and grant | Exact round-trip, no overflow, negative balances, lost XP, or NaN |
| EP07 | One reward crosses one million levels | Bounded-search calculation, no per-level reward loop, all finite unlocks processed once |
| EP08 | Forge upgrades after clearing a frontier | Cannot buy past it; bulk cost equals individual costs; duplicate commit cannot spend twice |
| EP09 | Chapter boundary and crash while choosing a route | Same choices return; route state and frontier recover consistently |
| EP10 | High-tier generator and all allowed rule pairs | Entity budgets and minimum warning times remain intact; legal safe paths exist |
| EP11 | Level above 100 attempts Override | Minimum qualification works; temporary trial normalization leaves persistent power intact |
| EP12 | Death, early extraction, or reload in a chain | Banked rewards retained; optional bonus and leg rewards cannot duplicate |

Run property tests over positive tiers and levels, exact arithmetic checks across numeric boundaries, and a stratified encounter sweep. Test ordinary play around levels 40–120 and tiers 1–20, then synthetic profiles at tiers 100, 1,000, and very large values. A finite stress range is a validation sample, never an implementation ceiling.

Measure time per level, time per new tier, attunement affordability, build viability, percent of deaths with readable causes, and willingness to repeat chapter routes. Start with a 12–20 minute expedition target near appropriate power, inspect large deviations, and tune the curve and rewards together. Formula tests cannot establish that an endlessly repeated activity remains enjoyable; add new authored combinations only when existing decisions are worth repeating.
