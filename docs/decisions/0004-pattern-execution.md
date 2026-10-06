# O03: authored pattern execution

Status: implemented, 6 October 2026. Extends [O02 dispatch](0003-action-resolution.md) with real arena adapters for Pursuit, Afterstrike and Crossing. Convergence remains a domain selection fixture and future slice effect, not an enabled arena implementation.

## Execution contracts

The three prototype bindings are equipped by the arena profile. Pursuit wins over Afterstrike when both one-memory signatures are executable, using their authored priorities. Traverse competes for the same Momentum token through Crossing. Dispatch still commits one cost, cooldown and root ID; the effect adapter never dispatches a second action.

Preflight now tests actual terrain/body geometry with the player's circular footprint. A movement candidate with less than one pixel of legal advance fails with a visible reason, allowing another pattern or base to execute without spending that candidate's token. The world query returns a destination which the authority captures after its final revalidation. Movement remains swept every tick, so a preflight result does not grant permission to cross later obstacles.

The versioned adapters in `PatternExecution` define geometry, ranges, hit caps, movement distance, damage split and stagger/push behavior. They are code-authored v1 rules, not a general expression language or animation editor. One world meter is 32 pixels.

## Pursuit

- Advance up to 64 pixels along accepted aim during the selected skill's windup. Enemy bodies and solid terrain stop the advance; movement does not slide sideways around obstacles.
- On the active phase, strike a fixed narrow rectangle from the actual post-advance position, using the skill's original reach and an eight-pixel half-width. Walls occlude hits. This replaces the base sector or projectile entirely.
- Apply 110% of the once-rolled damage budget. Cleave/Pulse have the existing 64-query victim bound; Lance retains its three-victim limit, ordered by distance along the lane and actor ID. Each victim is hit once during the active window.
- Preserve the skill's stagger budget and remove Pulse's base push. The narrower shape and displacement are the authored tradeoff.

## Afterstrike

- The first active frame captures the strike's world origin and accepted aim. Cleave/Pulse use their sector adapter and Lance uses its moving projectile adapter. Each part has its own victim receipts and the same authored hit limit.
- One critical roll establishes the total damage budget. The first part deals 60%; a second part emits 21 simulation ticks later from the captured origin and aim. Integer rounding preserves the full 120% total, with at most one subunit difference between parts.
- Each part uses 60% of the base stagger and push, avoiding two full-strength control effects. Stagger rounds down to an integer per part.
- Movement, recovery cancellation or a later action cannot relocate the repeat. Targets can leave or enter the second hit. Pause freezes the delay. Death and encounter unload cancel waiting repeats and in-flight player projectiles, including death earlier in the same projectile batch.
- The waiting strike has a violet world outline. It is a location cue, not a promise that a moving target will still be hit.

## Crossing

- Travel up to 160 pixels over the standard 18-tick Traverse duration, retaining exactly the first seven ticks of evasion. No damage effect is added.
- Player movement temporarily ignores enemy bodies, while solid terrain still blocks the swept footprint. The hostile-hit layer remains present, so evasion is tested by real hostile collision evidence.
- Preflight searches backward along the terrain-clipped path for an unoccupied endpoint. Execution checks that endpoint again while moving. A newly occupied endpoint shortens the path. A final overlap check can retreat along the traversable segment before restoring body collision.
- The normal collision mask is restored at completion, death, retry and encounter exit. The entire action retains `OverloadedPlayerAction`, so neither its movement nor its evasion generates a replacement memory. The engine fixture makes Momentum generation available before Crossing to test this independently of the generation cooldown.

## Effects and provenance

Primary effects use effect ID zero. The Afterstrike repeat receives a unique child ID, parent ID zero, and the same root ID and source classification. Projectiles retain that receipt through every hit. The event trace is capped at 64 entries. Descendants never invoke action selection or resource payment.

Each live root permits at most 64 children and depth four. The delayed timeline holds at most 256 entries, ordered by due tick then monotonic insertion sequence; it returns a batch before execution so newly queued work cannot recurse inline. Active strikes and projectiles also have bounded storage. Exceeding a budget reports an engine error and stops the offending effect; valid content reaching a cap is a defect, not a tuning mechanism.

## Evidence and remaining work

Unit tests cover authored coefficients, huge/indivisible budgets, skill adapters, unsupported implementations, immutable root provenance, forged-parent rejection, count/depth bounds, queue capacity and stable non-recursive delivery.

Engine fixtures grant the required tokens, then exercise the real input, authority, physics and hit paths. They cover all three Assault skills under Pursuit and Afterstrike, exact damage and victim counts, the repeat delay and fixed origin, one critical roll/payment, walls, occupied/moving Crossing endpoints, standard evasion timing, memory exclusion, pause, death, unload and same-tick lethal cancellation. Earlier O01 fixtures still cover earning tokens from actual movement and evaded hostile hits.

The HUD shows Strain and the pattern currently executing; movement trails and the Afterstrike outline distinguish the effects. O04 still owns the full per-action prediction HUD, binding menu and expanded diagnostics. Human testing of deliberate pattern choice, readability and feel remains open, as do physical-controller testing and performance targets. These checks do not complete the M2 acceptance gate.
