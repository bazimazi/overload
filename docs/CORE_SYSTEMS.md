# Overload and Override specification

This document makes the two defining systems implementable. It is the authority for dispatch, costs, compatibility, unlocks, and failure behavior. Values are initial tuning proposals. They require prototype testing; this document does not certify market-wide novelty or balance.

## The essential distinction

**Overload adds another valid signature for the same action. Override replaces the implementation of an allowed character contract.**

In C#, overloads can share a method name while differing in their parameter lists, and an override changes an inherited virtual or abstract member. This is the conceptual inspiration; the game uses explicit runtime data and strategies rather than trying to make a compiler infer gameplay state. [Microsoft on methods](https://learn.microsoft.com/en-us/dotnet/csharp/methods), [Microsoft on override](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/override)

| Player system | Analogy for developers | Example visible to the player |
| --- | --- | --- |
| Skill rank | Tune an existing implementation | Cleave deals more damage |
| Skill technique | Choose a persistent variant for one skill | Cleave always has a wider arc |
| Overload | Add an implementation accepting typed combat arguments | Cleave with Momentum becomes a lunge; Cleave with Echo becomes a delayed second strike |
| Override | Replace an allowed implementation behind a stable contract | Traverse no longer rolls; it creates an anchor and returns to it |

Overload operates on the character's action vocabulary. A discovered pattern can apply to several compatible skills and Frames without being a socketed support attached to one spell. Memories have competing uses across attack, movement, and recovery. The novelty hypothesis is this combination of character-wide dispatch, visible competing signatures, and late contract replacement. Conditional effects and charge consumption already exist elsewhere; do not advertise those alone as unprecedented.

## Player language and components

Use **memory** for a temporary combat argument, **pattern** for a discovered alternate action, and **binding** for an equipped pattern. A **signature** is the combination of action family and required memory types. The interface can use the word signature in advanced explanations, but its main text should read “After moving, your next assault can lunge.”

The initial action families are Assault, Traverse, and Recover. Assault is a manually initiated direct-damage skill. Traverse is the dedicated evasion input. Recover is the flask input. Pure utility casts, auto-attacks by summons, damage-over-time ticks, retaliation, and item procs do not independently dispatch Overloads.

The equipped binding belongs to the character. Its capability filter determines which actions it can implement. For example, Afterstrike requires a direct attack with a reproducible aim and origin; it does not apply to a permanent aura. The HUD names which equipped actions qualify before the player spends resources.

## Combat memories

| Memory | Intentional generation rule | Payload | Release phase |
| --- | --- | --- | --- |
| Momentum | Travel 3 meters of actual locomotion within a rolling 2-second window while in combat | Last nonzero movement direction | Prototype |
| Echo | Evade an Evadable hostile hit during an evasion window | Hostile attack identifier and incoming direction | Prototype |
| Stillness | After remaining within a 0.25-meter radius for 0.8 seconds in combat, land a base Assault | Aim and origin of that hit | After slice |
| Rupture | Cause a stagger break with a base player Assault | Broken target and position | After slice |

Memory capacity is one token per type, each expiring five seconds after generation. Reacquiring a type refreshes its expiry and replaces its payload. Maximum four stored tokens at full release. There is no memory inventory, farming outside combat, trading, or permanent hoarding.

Generation has a per-type two-second internal cooldown. Momentum counts walked or standard-evade displacement and stops counting when locomotion is blocked. It excludes teleports, knockback, and movement produced by an overloaded action. Reset the distance accumulator after generation; standing against a wall cannot generate Momentum. In combat means a live hostile has engaged the player within the current encounter; training-room enemies count but their rewards do not.

Echo uses a collision or attack-query result that would have hit but was rejected specifically by the evasion window. Simply pressing Traverse near an enemy does not count. An attack identifier awards at most one Echo even if its hitbox persists across ticks. An overloaded Traverse may evade, but cannot generate a new memory from its own execution. A base Traverse under the anchor Override may generate Echo through a valid swap evasion.

Stillness requires a successful base hit, not holding still indefinitely. Rupture requires the first break event and obeys a six-second per-target generation lockout in addition to the type cooldown. Boss stagger works; killing trash is not the only generator. Base actions under an Override can generate a memory if the chosen implementation still reports the required semantic event.

All expiration and cooldown times use simulation ticks. Pause freezes them. Clear memories on death, leaving an encounter, loading a checkpoint, changing equipment, or changing a build. Combat cannot be suspended by opening a panel and then resumed with altered bindings.

## Binding limits and discovery

The player equips one binding after the introductory quest, two at level 12, and three at level 30. A binding may require one or two distinct memory types; no duplicate token requirements. Two-memory patterns first appear with the third slot.

Patterns are guaranteed discoveries from named quests, regional bosses, or challenge rooms. A journal shows an undiscovered pattern's broad source. Repeating a completed discovery awards ordinary rewards rather than duplicate pattern items. A known pattern never needs to be releveled through repetitive use.

Only one binding may occupy an identical normalized signature and capability domain. The validator rejects indistinguishable candidates. Where two patterns could both apply with the same number of arguments, the player orders those binding rows. A signature with two required memories is more specific and takes precedence over a one-memory signature when both are executable.

Rebinding is free at Hearth. Provide three saved build loadouts once the second slot unlocks. A loadout includes skills, techniques, talents, inscriptions, bindings, priority, and chosen Override. Equipment references are optional and validated if the item no longer exists.

## Prototype and slice patterns

Damage coefficients refer to the selected skill's computed pre-defense damage budget after skill ranks, technique, and ordinary offensive statistics. Geometry can change, but do not execute the original action invisibly underneath the new one. Each pattern is one authored implementation of that action, not an extra proc appended to the original.

| Pattern | Signature | Result and tradeoff | Strain | Scope |
| --- | --- | --- | --- | --- |
| Pursuit | Assault plus Momentum | Advance up to 2 meters toward the current aim, then strike a narrow lane for 110% total damage; loses the base arc or projectile spread | 25 | First arena |
| Afterstrike | Assault plus Echo | Strike for 60%, then repeat at the original world origin and aim after 0.35 seconds for 60%; a mobile enemy may leave the second hit | 30 | First arena |
| Crossing | Traverse plus Momentum | Move up to 5 meters, pass through enemy bodies, retain the standard evasion window; terrain still blocks travel | 25 | First arena |
| Convergence | Assault plus Momentum and Echo | Draw nearby ordinary enemies inward up to 1 meter, then deal one 135% cone hit after a 0.25-second tell; bosses resist the displacement | 45 | Slice preview profile |
| Shelter | Traverse plus Echo | Execute the selected Traverse implementation and leave a fixed 2-meter field at the departure point for 2 seconds; field intercepts two ordinary hostile projectiles, not beams or boss mechanics | 30 | Slice |
| Reprieve | Recover plus Echo | Use one flask charge to heal 25% Life and grant a barrier of 15% maximum Life for 3 seconds, replacing the normal 35% heal | 25 | Slice |

Pursuit, Afterstrike, and Convergence are authored for direct melee and direct projectile skills. Each adapter has explicit targeting, hit-count, and animation metadata. Do not apply them to any future channel, summon, or ground field until an adapter has been authored and tested. A selected incompatible skill simply uses its base action; its other compatible skills still benefit from the binding.

Afterstrike's 120% is its entire two-hit budget, not two attacks each at 120%. Each victim can take each child hit once. It spends Focus only once and produces no additional memory. The delayed strike persists if the player moves but is canceled on player death or encounter unload. One original critical roll is reused across both hits to avoid two unrelated crit opportunities.

Convergence is deliberately stronger against clustered enemies but more expensive and less immediate. Its movement effect cannot drag enemies through walls, pull an invulnerable target out of its authored phase, or change boss arena boundaries.

Shelter is compatible with both standard evade and anchor swap. With an unplaced anchor it accompanies the anchor-placement phase, at the player's current location; the field is consumed even if the player never swaps. Crossing requires path traversal and is incompatible with the anchor Override. Reprieve consumes a flask charge even if some healing is overheal; the preview exposes that choice.

The other 12 release patterns are a content budget, not untested mandatory designs. Develop them only after the first six pass: four using Stillness, four using Rupture, and four cross-memory combinations. Each new pattern must create a different tactical decision and fit the same bounded dispatcher. No new trigger language just to fit a content quota.

The P05 implementation adds three justified signatures. **Focused** uses Stillness for a narrow single-target lane at 125% damage and 25 Strain, discovered through Hollow mastery. **Shatter** uses Rupture for a 75% wide cone with doubled stagger and 25 Strain, discovered through Crown mastery. **Cascade** uses Stillness and Rupture for a 160% cone after a 0.35-second commitment, at 50 Strain, discovered through the Trial of Contradiction. They replace the base geometry and inherit its once-scaled budget. Further patterns remain an authored-content decision.

## Worked combat example

A Warden equips Pursuit, Afterstrike, and Convergence in that order. They walk around the Bellkeeper's sweep, earning Momentum. Cleave's icon changes to Pursuit. They could attack now and spend it.

Instead they evade the bell volley, earning Echo. Cleave's preview now becomes Convergence because that signature requires both memories. Pressing Cleave spends both tokens, adds 45 Strain, and executes the cone implementation. A normal Cleave remains available afterward when neither token exists.

The player could instead equip Shelter in place of Convergence. With both tokens ready, Cleave would use Pursuit because Pursuit and Afterstrike both require one token and the saved priority favors Pursuit. Traverse would offer Shelter. The same encounter now poses a different question: spend Echo for damage or preserve it for shelter.

This is the central build decision. Adding passive damage to every action would remove it.

## Strain and action selection

Strain has a maximum of 100, starts at zero, and decays by 12 per second after one second without a successful Overload. A base action does not restart that delay. Leaving combat clears Strain after five seconds; death and checkpoint load clear it immediately. Values use integer subunits, so fractional decay does not depend on frame rate.

An Overload is executable only if current Strain plus its cost is at most 100. Reaching 100 is allowed. Exceeding it never damages the player, locks ordinary skills, or silently deletes memories. The proposed purpose is a pacing budget, not a second punishment system.

At each input acceptance, perform the following in a fixed order:

1. Advance expiration and cooldowns for the simulation tick, then snapshot the actor, target intent, world-query results, memories, and loadout revision.
2. Reject inputs whose common prerequisites fail: dead actor, action locked, shared action cooldown, or no legal baseline resource payment. Under a cost Override, use its payment policy instead of the baseline Focus check.
3. Find bindings for the requested family whose skill capabilities, target requirements, and memory types match. Sort by required-memory count descending, saved player priority ascending, and stable definition ID as a final invariant tie-breaker.
4. In that order, preflight each candidate's specific geometry, costs, Strain, and active Override compatibility. Select the first executable one. If a candidate fails, record its reason and try the next.
5. If no pattern is executable, preflight the base signature using the active contract implementation. A failed Overload can therefore fall back to a different valid Overload, or to base, without spending its tokens.
6. Atomically commit the selected resources, memory consumption, Strain, cooldown, and action ID. Instantiate its execution state only after successful commit.
7. Execute authored phases and emit typed results. The action never re-evaluates its signature mid-animation. Every descendant effect retains its root action ID and origin classification.

If world geometry changes between preflight and commit, revalidate once before committing. Failure returns a visible reason and costs nothing. Once windup starts, interruption spends committed resources and memories; this is consistent with ordinary skill commitment. No partial rollback after a hit occurs.

HUD previews call the same pure selection routine against a read-only snapshot. They remain predictions: a token can expire or an enemy can move before input. Show the selected pattern, expiring memory rings, and a concise fallback explanation; never maintain separate handwritten rules in UI code.

No “hold a modifier to choose correctly” input is required. Advanced players may enable a remappable one-use “base action” modifier, with a controller toggle equivalent, that bypasses all bindings for that input. It cannot bypass an active Override. This is optional after the slice, not part of the first tutorial.

## Why Override is exceptional

The player can inspect Override at level 20, see every requirement, and try a clearly labeled simulation. Acquiring the first real Override requires level 100 or higher, completion across regions and activity families at Fracture tier 10 or above, substantial guaranteed resources, and a personal trial. Level 100 is a minimum milestone in an uncapped leveling system, not a maximum. The interface is accessible; actual power is rare.

Overrides are **Sovereign Oaths**. One is active at a time across the whole character. An oath selects one allowed contract slot and replaces its base implementation. Other contracts continue normally. The replacement can remove an entire rule and install different state transitions; its power is not represented as a pile of percentage buffs.

The first three permitted slots are Traverse, PaySkillCost, and ResolveLethalDamage. No Override can replace quest completion, resource granting, the unlock validator, save integrity, loot generation, or its own qualification process. Gameplay extensibility is an authored whitelist, not arbitrary code execution.

## First Override specification

**Elsewhere** replaces Traverse. Its player text is: “Your evade becomes an anchor. Mark where you stand, then return when danger closes in.”

| State or input | Behavior |
| --- | --- |
| No anchor, Traverse pressed | Spend the action; a 0.15-second placement creates an anchor at current feet position; no displacement and no invulnerability |
| Anchor armed | It lasts 4 seconds and is visible in the world and HUD; swapping cannot begin for the first 0.25 seconds after placement |
| Armed, Traverse pressed with valid return point | Swap to the anchor, consume it, gain a 0.12-second evasion window, and start a 2.5-second traversal cooldown |
| Anchor expires | Clear it and start a 0.5-second traversal cooldown |
| Return point invalid or farther than 8 meters | Keep the anchor until expiry, spend nothing, and show why the swap failed |
| Anchor obstructed by a moving enemy | Test a deterministic nearby free landing point within 0.5 meters; otherwise treat as invalid |
| Zone transition, death, or loadout change | Remove the anchor; never carry a return point across rooms or saves |

Placement and swap are separate accepted Traverse actions. Placement has its own reactivation lock rather than the standard 1.2-second cooldown. Swapping requires the same navigable room and a valid traversable connection between the actor and anchor; closed doors, arena barriers, and no-teleport boundaries invalidate it. It can cross intervening enemy bodies, but not escape a locked encounter.

This oath rewards forecasting a safe location and enables precise returns through enemy lines. Its loss is substantial: an unprepared player cannot instantly dodge. Attack patterns must always leave a movement-only escape route, so it remains playable without a conventional roll. Teach the oath in a practice chamber before spending ritual resources.

Overload compatibility is explicit. Shelter wraps the new Traverse implementation, adding a field at the departure point. Crossing is suspended because the new implementation lacks path-traversal capability. The loadout UI identifies the suspended binding and offers replacement choices. Do not secretly restore normal rolling for incompatible patterns.

Elsewhere is the only required Override for the prototype and slice. Test the power through a debug grant immediately after Overload works. The production acquisition journey can be completed later; testing the mechanic must not require 60 hours of grinding.

## Later Override candidates

These are scoped designs for later milestones, not permission to add all three before the slice.

| Oath | Replacement | Cost and weakness | Required adversarial tests |
| --- | --- | --- | --- |
| Red Covenant | PaySkillCost stops using Focus; manually cast Focus-cost skills reserve maximum Life equal to 0.4% per base Focus point for 4 seconds | At most 40% Life can be reserved; reject casts exceeding the cap or leaving less than 1 available maximum Life; releasing a reservation restores capacity, never lost health | Haste spam, free casts, refunds, reservation expiry while hurt, max-Life changes, healing at the reduced cap |
| Unfinished | ResolveLethalDamage once per expedition replaces death with a 6-second afterlife trial requiring the destruction of three nearby authored soul seals | Cannot damage normal enemies, heal, leave the arena, or gain memories in the trial; success returns at 25% Life; failure dies normally; charge is consumed at entry | Simultaneous lethal hits, no legal seal positions, save/reload during trial, boss phase transition, repeated failure, expedition restart abuse |

Red Covenant also removes Focus-regeneration benefits while equipped and explains them as inactive in the character sheet. Reservations from separate actions expire independently. Current Life is clamped to the reduced maximum when reserving; that clamp is not damage and cannot trigger anything. A cast can therefore put the character in serious danger. Design this as a controlled reservoir, not an accidentally infinite mana source. Basic attacks, flask charges, and traversal cooldowns retain their ordinary costs.

Unfinished uses arena-authored safe candidate positions and a deterministic placement validator. If three legal positions cannot be found, use the documented fallback of a fixed survival trial with visible duration; never fail the oath because of generator geometry. This content requirement makes it the most expensive oath and the first to cut if production is constrained. Charge resets only on starting a new expedition from Hearth, where the encounter and unfinished objective progress also reset.

No oath should make a no-Override character obsolete. Target different strengths and weaknesses, with an initial balance guardrail of no more than approximately 15% median improvement across a mixed fixed encounter suite. This is a proposed tuning alarm, not a promised universal power ratio. Investigate any trivialized boss mechanic even when average damage looks acceptable.

## Acquisition and rarity

All requirements apply to the character earning the first oath. Discovery knowledge is shared across local characters, but qualifications and ritual wallets are not. The journal reveals the full journey at level 20. Regional ritual resources begin dropping only after the campaign; activity proofs begin at the listed endgame tier.

| Requirement | Exact initial proposal | Purpose |
| --- | --- | --- |
| Character mastery | Level 100 or higher and campaign completed | Substantial general play without a maximum-level requirement |
| Regional knowledge | Complete all four regional mastery quests | Visit every region and learn its mechanic |
| Breadth proofs | Complete one Hunt, Breach, and Vault at tier 10 or above in each region, for 12 distinct journal flags | Prevent one-location grinding from satisfying the journey |
| Regional resources | 120 Ash Seals, 120 Glass Seals, 120 Hollow Seals, 120 Crown Seals | Sustained play across all regions |
| Personal proof | Complete the Trial of Contradiction without an active Override | Demonstrate the base game's combat and signature understanding |
| Ritual | Commit all four resource balances at Hearth after all other requirements pass | Permanently unlock one chosen oath |

Every completed regional expedition at tier 10 or above grants six of that region's Seals. Lower tiers grant ordinary rewards and advance tier access, but no Seals. Seal quantity remains six at higher tiers; their additional incentive is XP, attunement access, ordinary rewards, and frontier progress. Finishing a region's three breadth proofs grants a one-time bonus of 12 Seals for that region. Thus each region needs 18 qualifying expedition completions: 18 × 6 + 12 = 120. Across four regions that is 72 completions. The three proof completions in each region count toward those 18; they are not extra runs. An expedition result can be claimed only once, keyed by its persistent expedition ID.

At a hypothesized 15–20 minutes per successful expedition, resource acquisition alone is 18–24 hours. Reaching level 100, unlocking tier 10, learning encounters, failed attempts, preparation, and the trial contribute additional time. These numbers do not mathematically guarantee the proposed 60–100 total hours; remeasure the full journey using the uncapped XP curve before release. Never add idle waiting simply to hit an hour target.

Seals are guaranteed completion rewards, never rare random drops. Failed attempts retain all previously banked Seals and proof flags. No Seal conversion lets one region replace another. The economy simulation should report expected completion time plus variance from failure rates and different activity durations, not pretend the fixed arithmetic models player skill.

The Trial of Contradiction has three short encounters: deliberately choose between two signatures, handle spatial hazards, then defeat a boss that combines these demands. It should not require one Frame, a rare item, perfect inputs, or a specific equipped pattern. Provide alternate valid solutions and training prompts. A failed trial costs time only; no ritual materials or entrance tickets are consumed. Completion unlocks the ritual permanently for that character.

The current trial's first encounter across all three Frames accepts landed hits from two Assault skills or two alternate signatures. This supplies a base-skill solution without requiring a particular discovery. Regional mastery quests require an actual hostile-hit evade in Ash, breaking cover in Glass, a Stillness-generating base hit in Hollow, and a base Assault boss stagger in Crown; the expedition must finish to bank the mastery flag.

For this trial only, use a separate trial actor with level-100-equivalent power, 20 Might and 20 Resolve, Attunement Grade 10, and standardized Rare equipment appropriate to the Frame. Retain the player's legal skill, talent, Codex, and signature choices; disable Override. Show this normalization before entry and permit practice with the supplied loadout. Never modify the persistent actor's XP, level, gear, or point allocation. Difficulty/accessibility settings remain available. Normalizing this explicitly identified mastery encounter prevents endless grinding from skipping the proof; ordinary campaign and Fracture enemies never secretly follow the player's level. Trial victory grants no repeatable XP, gear, or frontier access.

The first ritual selects one oath and consumes the four balances atomically. It cannot fail randomly. If a save or process failure occurs, recover to either “resources present and oath locked” or “resources deducted and oath unlocked,” never a mixture. Repeating the same transaction identifier cannot debit twice.

Later oaths require their own mastery challenge plus 30 Seals of each region. With six per completion and no repeat breadth bonus, that is five completions per region, or 20 in total. Unlocking them expands choice; it never adds a second simultaneous Override. Swapping or disabling already unlocked oaths at Hearth is free. Death never destroys an unlocked oath.

Target, as a hypothesis, roughly 5–10% of characters that finish the campaign earning an oath during an initial 90-day observation window. Define the denominator and observation window before reporting a rate. Do not promise that only a fixed percentage of all players can ever earn it. Offline save editing also prevents global enforcement. No leaderboard or scarcity claim should rely on local save ownership.

## Prototype data contract

Use stable IDs and explicit enums. Definitions are immutable at runtime. This example is a proposed authoring format, to validate against a versioned schema during M2.

```json
{
  "schemaVersion": 1,
  "id": "pattern.assault.pursuit",
  "family": "Assault",
  "requiredMemories": ["Momentum"],
  "requiredCapabilities": ["DirectDamage", "AimOrigin", "GroundAdvance"],
  "strainCost": 25,
  "implementationId": "assault.pursuit.v1",
  "parameters": {
    "advanceMeters": 2.0,
    "damageBudgetPercent": 110
  },
  "generationPolicy": "NoMemoriesFromThisAction",
  "descriptionKey": "pattern.assault.pursuit.description"
}
```

```csharp
// Contract sketch, not compiled game code.
public interface IActionResolver
{
    ActionPlan Preview(ActionIntent intent, CombatSnapshot snapshot);
}

public interface ITraverseImplementation
{
    PreflightResult Preflight(TraverseIntent intent, CombatSnapshot snapshot);
    ActionPlan Plan(TraverseIntent intent, CombatSnapshot snapshot);
}

public interface ISkillCostPolicy
{
    CostPlan Quote(SkillDefinition skill, CombatSnapshot snapshot);
}

public interface ILethalDamagePolicy
{
    LethalResult Resolve(LethalContext context);
}
```

An ActionPlan contains the chosen definition ID, snapshot revision, target intent, required capability results, payment quote, token IDs, Strain change, cooldown changes, timed effect instructions, and diagnostic reasons for rejected higher-priority candidates. A separate authoritative executor validates and commits it. UI cannot mutate a plan to bypass payment.

Compile bindings and the active oath into an ActorBehaviorProfile on loadout change. The profile contains one implementation per contract slot, a bounded list of bindings, and compatibility diagnostics. Prefer composition over generating a derived C# class for every combination.

Never evaluate player-authored code, expressions, or reflection names from saves. Implementation IDs map to known handlers in a registry. Invalid content fails validation before a build; unknown IDs in an old save use a deliberate migration or disable the affected choice with an explanation.

## Ordering and interaction rules

The pipeline is input, contract selection, signature selection, cost quote and preflight, atomic commitment, timed execution, hit resolution, post-action observations, and presentation. Contract selection supplies capabilities before signatures are filtered. Overload chooses an implementation that can call the active contract; it cannot select the superseded one.

Skill ranks and techniques define the starting attack budget and compatible capabilities. The selected Overload owns its geometry and budget split. Ordinary offensive bonuses apply once to the budget. Defense applies once to each final hit. Damage-over-time, retaliation, and child hits never dispatch new player actions.

Use RootActionId, EffectId, ParentEffectId, SourceKind, and TargetId on combat events. SourceKind distinguishes BasePlayerAction, OverloadedPlayerAction, EnemyAction, Environment, and SecondaryEffect. Base under an Override is still BasePlayerAction for permitted generation. No descendant of OverloadedPlayerAction can generate a memory, even if it produces a normal-looking hit.

Never recursively resolve effect events inline. Use a bounded queue with stable sequence numbers. Set an initial cap of 64 child effects per root action, maximum parent depth four, and a diagnostic failure in development if content exceeds that contract. Shipping should stop the offending secondary chain and log locally rather than freeze the game. Hitting the cap in valid shipped content is a release blocker.

Stat caps do not multiply independently for every system. Strain maximum, decay, and memory duration are initially unaffected by gear and talents. Add such modifiers only if evidence shows a need and the tests remain tractable. This protects the defining mechanic from becoming a solved infinite-loop build.

## Required acceptance scenarios

| ID | Setup | Expected result |
| --- | --- | --- |
| OL01 | No memories and legal Assault | Base skill executes once |
| OL02 | Momentum, Pursuit bound, Strain 0 | Pursuit executes, token spent, Strain 25 |
| OL03 | Momentum and Echo, Convergence bound and executable | Convergence wins regardless of row order; both tokens spent |
| OL04 | Both one-memory patterns match with no two-memory pattern | Saved player priority decides; unused memory remains |
| OL05 | Strain 90 and all matching patterns cost at least 25 | Base action executes; memories and Strain are unchanged by dispatch |
| OL06 | Strain 75 and Pursuit costs 25 | Pursuit is allowed, resulting Strain is 100 |
| OL07 | Top candidate unaffordable but a lower candidate legal | Lower candidate executes and UI prediction agrees |
| OL08 | Memory expires exactly at acceptance tick | Expiration happens first; expired token cannot match |
| OL09 | Blocked movement against a wall | No Momentum from intended but unrealized displacement |
| OL10 | Delayed Afterstrike hits multiple actors | Correct two-part budget; no additional payment, memory, or recursive dispatch |
| OL11 | Preview repeated without accepting an action | State, RNG streams, cooldowns, and token expiry are unchanged |
| OL12 | Commit rejected or duplicated | No partial spending and no duplicate action |
| OV01 | Elsewhere active without an anchor | Traverse places an anchor; no hidden evade or invulnerability |
| OV02 | Valid anchor swap | Position changes legally, anchor clears, cooldown starts once |
| OV03 | Closed door, invalid room, expired or distant anchor | No traversal through the boundary or unintended payment |
| OV04 | Elsewhere with Crossing and Shelter | Crossing is visibly suspended; Shelter uses the selected anchor phase |
| OV05 | Ritual interrupted during save | Resources and unlock recover in the same atomic state |
| OV06 | Trial or ritual repeated with same completion ID | No duplicate bonus and no second debit |
| OV07 | Override removed at Hearth | Contract state clears and ordinary behavior returns cleanly |
| OV08 | Any two oaths requested in a loadout | Validation rejects multiple active oaths |
| OV09 | Lethal hits arrive in the same tick under Unfinished | At most one afterlife entry and one charge consumed |
| OV10 | Red Covenant reservation expires at low health | Maximum capacity returns; current Life is not healed |
| OV11 | Level 99, 100, 101, or a very large level checks ritual qualification | Only the minimum threshold matters; no exact-level or maximum-level dependency |
| OV12 | High-level actor enters and leaves normalized trial, including crash recovery | Original progression and inventory are unchanged; trial rewards cannot be farmed |

Add property-based tests for token conservation, nonnegative resource balances, legal fallback, single implementation per action, bounded effects, and order-independent content loading. Add engine integration tests for actual collision, timing, paused clocks, anchor landing, and controller preview behavior. Unit tests alone cannot establish that the mechanic feels good.

## A03 implementation note

Red Covenant now uses authority-owned independent reservations, normalized reservoir mastery and an atomic later-oath ritual. Its contract details and optional Unfinished cut are recorded in [decision 0008](decisions/0008-frames-regions-covenant.md). The candidate implements two selectable oaths; the proposed three-oath release budget is not an achieved release gate.

## Quality/release implementation note

New ordinary profiles start as level-1 Standard characters; existing saved profiles retain their mode and resources. Settings provide 100%/125% text, high contrast, reduced hit flashes and six independently adjustable audio channels. Menus scroll and follow focus, with output-pixel UI independent of integer-scaled world rendering. Normal rendering is limited to 120 FPS around the unchanged 60 Hz simulation. Losing window focus pauses combat; disconnecting the active controller also clears held and buffered actions. Resuming is deliberate. Input remapping remains saved and conflict checked.

The 200-projectile and four-ground-cast limits stop further emission with a bounded diagnostic when saturated. Existing effects and gameplay-critical telegraphs remain visible; accepted actions do not receive refunds. Save schema remains 5. Unknown newer character content versions are preserved alongside future schema/expedition/generator versions. See [quality/release validation](Q_RELEASE_VALIDATION.md) and [decision 0009](decisions/0009-quality-release.md) for evidence and open human gates.
