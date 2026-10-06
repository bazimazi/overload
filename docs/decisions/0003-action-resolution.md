# O02: compiled dispatch and atomic commitment

Status: implemented, 6 October 2026.

The base-only arena gate described below records the O02 delivery boundary. [O03](0004-pattern-execution.md) now supplies and enables the three prototype pattern adapters; the dispatch/payment contract remains in use.

## Scope

O02 provides the shared selection routine and authoritative payment path. Every arena action now enters that path. O03 still owns Pursuit, Afterstrike and Crossing geometry, damage budgets, animation timing and descendant effects. O04 owns the player-facing prediction HUD and binding menu. Overrides remain V01 work; this profile uses the existing base contracts only.

The arena profile declares three prototype bindings (Pursuit, Afterstrike, Crossing) and the slice-preview Convergence definition. Convergence is not equipped in the arena. The three prototype slots are a test profile, not an implementation of progression unlocks. Two-memory bindings require slot capacity three.

The real arena preflight adapter currently enables only base execution. A matching pattern reports `Pattern execution is not available yet` and falls back without spending memories or Strain. This gate must remain until its actual O03 executor and geometry preflight are implemented together. Domain fixtures enable known pattern implementations to verify dispatch/payment; they do not establish that pattern effects work in the engine.

## Content and compilation

`ActorBehaviorProfile.Compile` creates an immutable table at actor construction or a legal binding change. It validates references, unique equipped patterns, normalized memory signatures, capability domains, priorities, costs and bounded slot counts. Implementation IDs map to a fixed whitelist; no reflection or player-authored expressions are evaluated.

Compiled order is memory count descending, saved priority ascending, then definition ID using ordinal string ordering. Input/content array order cannot affect selection. Capability flags are authored on skills and requirements on patterns. Bindings with an identical normalized signature and capability domain are rejected.

Bindings can change through the domain API only while alive, idle and outside combat. An invalid change preserves the current profile. A successful change clears memories and invalidates outstanding proposals. No menu, character save or loadout persistence is added in O02.

## Preview and authority

`PlayerCombat.Preview` copies the relevant actor values, immutable tokens and world-query results into a snapshot. `ActionResolver.Select` is pure: it has no actor, clock, RNG or world access. It checks common prerequisites, tries matching candidates in compiled order, records why higher candidates fail, and then preflights base. Costs use integer subunits. The current quote is the baseline Focus cost plus an optional authored pattern surcharge; cost Overrides are deferred.

`ActionPlan` has no public constructor or mutable fields. The authority issues it with an actor identity and actor/memory revisions. `TryCommit` rejects another actor's proposal, stale actor or token state, and repeated commitment. Revisions remain monotonic across checkpoint clock resets. An expired or refreshed token cannot be substituted into an old proposal.

Immediately before commitment, the adapter captures world evidence once again and the same selector revalidates. If the promised implementation is no longer selected, commitment fails with a reason and pays nothing. A later input may select the new fallback. If the selected implementation remains legal, execution captures the fresh world origin. Preflight adapters are trusted game code and must perform read-only queries.

The synchronous simulation authority validates all token receipts before any debit. It then consumes only those tokens, applies Focus and Strain, starts the shared skill cooldown, allocates one root ID, and publishes one execution. The execution retains its selected definition, implementation, aim, origin, consumed payloads and source classification. Canceling accepted windup does not refund anything. Spending does not reset generation cooldowns or erase hostile-root receipts.

Plans are transient, single-threaded simulation objects, not persisted commands or network messages. Execution adapters must dispatch the selected implementation exactly once; effect events must not call the resolver again.

## Strain clock

The authored profile starts at zero and caps Strain at 100, represented as 100,000 subunits. An action reaching exactly 100 is legal. Higher costs fall through to another candidate or base without deleting tokens.

A successful Overload restarts the 60-tick decay delay. Ticks 1 through 60 preserve Strain; tick 61 starts decay. At 12 points per second, each 60 Hz tick removes 200 subunits. A remainder accumulator supports other validated whole-point rates without rounding drift. Base actions and failed commitments do not restart the delay. Pause freezes the simulation clock.

Leaving combat starts a 300-tick clear timer while ordinary decay continues. Reentry cancels that exit timer. Death and checkpoint reset clear Strain immediately. Binding changes do not grant a free Strain reset.

## Evidence and remaining work

The rule suite covers OL01–OL08 and OL12 at selection/commit level, with OL09 covered by existing wall-displacement tests. It also covers read-only previews (OL11), changed geometry, refreshed/expired tokens, cross-actor/retry/duplicate proposals, capability and cost fallback, Strain boundaries and decay, lifecycle changes, canceled windup, generation receipts after spending, and 2,000 seeded action sequences checking conservation and bounds. Crossing's committed provenance cannot generate Echo or Momentum in domain tests.

Engine smoke additionally checks repeated previews against the real actor, explicit unavailable-pattern fallback, actual captured world origin, and exactly-once Focus/cooldown payment with retained memory. F3 shows the last selected implementation, Strain and first rejected-candidate reason. Full prediction/binding UI, effect provenance through actual child hits, OL10 and human pattern-choice acceptance remain O03/O04 work.
