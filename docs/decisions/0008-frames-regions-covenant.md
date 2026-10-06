# Frames, regional content and the Life reservoir

6 October 2026. Implements the owner's request to continue through A03.

Frames use composition and a saved identity, not player-controller subclasses. Each has eight skills, a free basic action, three equipped actives, Frame-owned rank/technique allocations and twelve connected talent nodes. The branch effects reuse the established finite foundation rules. Three equal-budget training profiles per Frame precede any item-count expansion. They are separate accelerated characters and cannot qualify for Standard rituals. Human enjoyment and build preference remain unverified.

Control casts have explicit adapters. Ember Well has one delayed ground hit; Storm Loom has three bounded pulses; Echo Order commands one fixed echo and replaces its previous command. Secondary pulses/echo bolts cannot generate memories. Defensive skills create barriers without an attack budget or critical roll. A Bone Volley shares one victim ledger across its three bolts. Direct Assault patterns require DirectDamage capabilities, so a control cast cannot silently become a cone, lance or extra summon.

The expansion catalog contains 24 skills, 24 named regional enemy families, eight bosses and 48 regional room templates. Six ordinary behavior/silhouette templates are shared across four material palettes. Ten obstacle arrangements per region plus two open boss spaces form a finite authored pool; this is not a claim of 48 unrelated handcrafted maps. The eight bosses combine fixed sweeps, volleys, movement and marked pulses. Geometry is validated before play. Every template preserves a central walking/return route and authored spawn sockets; no room requires an oath.

New Standard/training characters have a sixteen-checkpoint campaign: two ordinary groups and two bosses per region, ending with First Pattern and an ending vignette. Schema 5 migrates old characters to Warden while retaining their eight-room court, wallets, gear, progression, oath and active expedition. New regional expeditions use fracture.v3/rooms.v2; fracture.v1/v2 and rooms.v1 still validate and resume unchanged. Existing seeds are never regenerated with the new regional pool.

Red Covenant replaces the payment contract in PlayerCombat and its pure preview snapshot. A cast reserves 4 per thousand of unreserved maximum Life per base Focus point for 240 ticks, independently per accepted root. The total cannot exceed 400 per thousand or leave less than 1,000 Life subunits available. Capacity is calculated with BigInteger integer arithmetic. The reservation clamp bypasses hit resolution and produces no damage event. Expiry restores capacity without changing current Life. Healing and barriers use available capacity. Maximum-Life changes re-evaluate the same percentages and cannot invalidate the one-Life minimum. Focus payment and all Focus regeneration are inactive, including any pattern surcharge; the current authored patterns have no surcharge. Free basics, flask charges, and Traverse cooldowns retain their contracts. There is no refund subsystem; failed/stale commitments create no reservation.

The separate normalized mastery challenge requires reaching 20% reserved, healing while hurt with an active reservation, and defeating a boss with the reservoir. It retains legal Frame choices, cannot change persistent power, and grants only a permanent mastery flag. The earned ritual requires a Standard character, an already earned first oath, this mastery and 30 Seals from every region. Ownership and all four deductions commit together. Free Hearth selection permits one active oath.

## Optional Unfinished spike and cut

The design/QA audit found that path connectivity alone does not author legal soul-seal candidates. None of the new templates carries a seal-candidate set. Combat currently has alive/dead/checkpoint behavior; it lacks an isolated afterlife mode that suspends normal enemies, healing, traversal and memories. Boss marks and pending player effects can survive across a newly introduced lethal transition unless explicitly suspended. The save aggregate also has no expedition-spent afterlife charge.

| Required case | Additional work exposed by the spike |
| --- | --- |
| Simultaneous lethal hits | Idempotent lethal transition and one charge reservation before entry |
| Placement impossible | Authored candidate sets, footprint validation and visible survival fallback |
| Save/reload during trial | Atomic charge persistence before entry, checkpoint resume with charge spent |
| Boss transition | Defined suspension/cancellation of marks, projectiles and phase advancement |
| Repeated failure | Ordinary death path without re-entry or charge refund |
| Expedition restart | Charge reset tied only to a new Hearth expedition ID and discarded progress |

A03 explicitly allows cutting Unfinished when it threatens quality. This candidate cuts it, rather than shipping an incomplete resurrection/save contract. It has no selectable entry, partial mechanic or misleading unlock. The candidate has Elsewhere and Red Covenant. Reintroducing Unfinished requires a dedicated authored placement/afterlife implementation and the full QA matrix above.

The next scope is Q01 and later release verification. Neither this decision nor automated combat models closes S06, the three-enjoyable-build gate, the oath balance comparison, animation quality, duration, controller feel or hardware/performance gates.
