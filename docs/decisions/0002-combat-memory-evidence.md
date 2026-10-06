# 0002 — Combat memories from movement and hit evidence

Accepted 6 October 2026. Scope: implementation task O01, the first part of M2. Patterns, spending, Strain and the shared resolver remain O02–O04.

`CombatMemories` is an engine-independent part of `PlayerCombat`. The existing simulation clock advances it before accepting actions, so expiry at the current tick wins over selection. HUD reads do not mutate timers. A paused arena does not advance the clock.

The `arena.json` profile authors 96 pixels of locomotion in a 120-tick rolling window, 300-tick token lifetimes, and independent 120-tick generation cooldowns. The rolling window is `(tick - 120, tick]`. Each type holds one immutable token; a legal reacquisition replaces its ID, payload, and expiry. Generation resets the Momentum distance accumulator. Token IDs remain monotonic across retries; they are bounded session identifiers, not progression values.

The Godot adapter records the player's position immediately before and after their own `MoveAndSlide`, then reports the realized displacement after hit resolution. Zero intent and displacement opposite to intent receive no credit. Enemy movement, teleports and knockback are outside this measurement. Only Walk and StandardEvade with BasePlayerAction provenance qualify; overloaded roots and secondary effects fail closed. The domain also rejects duplicate movement observations within the same tick.

A 96-pixel evade was observed ending at 95.99991 pixels due to float physics positions. The distance comparison therefore permits a 0.001-pixel geometric tolerance (1/32,000 meter), with tests rejecting a materially incomplete evade. This tolerance never manufactures movement or changes the exact integer combat/progression arithmetic.

Every live hostile in the authored arena actively targets the Warden. That supplies the current combat-engagement boundary, including staggered enemies. When the last hostile dies, the encounter is complete and memories clear before the transition delay. Future multi-room AI must supply actual engagement rather than treating all scene enemies as engaged.

Echo is generated inside `ReceiveHit` only when an actual hostile collision is rejected specifically by a base Traverse's evasion window. Pressing evade, an unavoidable hit, ordinary damage, a dead actor, expired evidence, environmental sources, and overloaded/secondary traversal provenance cannot grant it. Incoming direction is the projectile's travel direction or the melee vector from attack origin toward the player.

Hostile effects carry the root attack ID and an exclusive last-possible-hit tick. All projectiles in a volley share these values. A melee hit has a one-tick lifetime; the volley deadline derives from its maximum range and speed. Receipt pruning happens only after that root can no longer hit, and expired evidence is rejected. Thus a long-lived root cannot refresh Echo after the two-second cooldown, while completed attacks do not leave an ever-growing history. Future persistent or delayed effects must preserve the original root and provide a deadline covering every child hit.

Death, encounter exit and checkpoint reset are wired to memory clearing. Equipment/build clear reasons are implemented and tested in the domain for the future legal loadout-change workflow; no equipment or build menu is implied. Root receipts, cooldowns, movement samples and tokens are transient and are never saved.

The HUD shows labeled Momentum/Echo cards, remaining lifetime, acquisition hints and a movement meter. F3 includes generation cooldowns and a bounded event log. Tokens are collected only in this milestone; no alternate attack is silently attached to them.

The engine memory fixture is appended to `--smoke-test` and uses isolated actor state, a stunned live hostile, real input, real terrain queries and real evasion collisions. `--smoke-test --capture-memories` additionally captures both stored tokens with the renderer enabled. No ordinary settings or characters are overwritten.
