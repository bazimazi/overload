# Overload game design

The [connected-world implementation](WORLD_IMPLEMENTATION.md) now supplies Hearth and four regional arcs through seventeen explorable zones, physical travel and fog, reclaimed refuges, eight placed bosses, convergent endings, and seeded objective-driven Fractures. Older room campaigns remain compatible. The original research/design assumptions below still require player validation.

The recommended game combines an immediately satisfying action RPG combat loop with deliberate positioning, skill combinations, and expressive character builds. Its identity is a character who can carry several answers to the same situation. Overload adds those answers; Override eventually changes what a fundamental action means.

This is a production proposal for a small team supported by AI development agents. Every uncited mechanic and numerical target below is an original design proposal, not a claim about an existing game or proven player preference. The first release scope is a ceiling to reassess after the vertical slice.

## Player promise and design pillars

The player-facing pitch is: **Fight to remember. Change how you fight. Earn the power to rewrite yourself.**

The player does not write code. Programming supplies the underlying distinction between additional signatures and replacement implementations. The fiction, interface, and tutorials express that distinction through embodied memories, altered actions, and forbidden oaths.

| Pillar | Concrete requirement | Validation |
| --- | --- | --- |
| Responsive, readable combat | Movement and aim remain predictable; enemy attacks have recognizable windups and recovery | Players explain why a death occurred from a short replay |
| Builds change decisions | A build changes positioning, timing, or which action to choose | Three viable builds follow visibly different combat patterns |
| Overload is the main attraction | Unlock it within the first 15 minutes; use it throughout campaign and endgame | Players intentionally set up a signature without tutorial prompts |
| Override is earned authorship | Replace a core rule after a long, visible mastery journey | Players can explain both the advantage and the lost base behavior |
| Simple equipment | Few affixes, few currencies, predictable upgrades | Most loot comparisons take less than ten seconds in usability sessions |
| Bounded production | Validate one hero and biome before expanding | No content expansion until core mechanic and export gates pass |

## What to learn from the references

Blizzard's Loot Reborn update explicitly aimed to simplify item evaluation through fewer affixes and fewer drops. Adopt that readability goal, while giving Overload responsibility for most conditional complexity. Do not reproduce the subsequent crafting layers wholesale. [Blizzard on Loot Reborn](https://news.blizzard.com/en-us/article/24077223/galvanize-your-legend-in-season-4-loot-reborn)

Path of Exile 2's 0.3.0 notes discuss support combinations, removed restrictions, and elemental infusions. These demonstrate both expressive customization and the difficulty of keeping combinations useful. The proposed response is a small set of visible, character-wide signatures with explicit conflict rules. This is a design inference, not evidence that the proposed system will be fun. [Grinding Gear Games on The Third Edict](https://www.pathofexile.com/forum/view-thread/3826682)

Diablo IV's later Lord of Hatred announcement describes moving build-defining choices between items and skill trees. Treat the reference games as evolving examples, rather than freezing a launch-era description of their progression. [Blizzard on skill tree changes](https://news.blizzard.com/en-us/article/24267729/prepare-for-the-reckoning-lord-of-hatred-draws-near)

The intended blend is accessible pacing and feedback with meaningful attack commitment and boss learning. It should not alternate between helplessly slow opening hours and unreadable endgame effects. Tune a stable combat language that survives progression.

## World and narrative

The [connected world proposal](WORLD_MAP_DESIGN.md) expands this setting into regional geography, a causal Foundry scenario, discoverable refuges, world changes, and a first playable exploration slice. Its basis is the [world and map research](WORLD_MAP_RESEARCH.md). This is proposed next work; the existing sixteen-checkpoint implementation remains the current campaign.

The setting is the **Palimpsest**, a civilization built over successive versions of reality. Ancient laws assign every creature a role: a guardian holds, a hunter pursues, the dead remain dead. A catastrophe has exposed discarded versions of those laws.

The player is a **Manyborn**, someone whose body can remember lives it never lived. An Overload is a usable alternate response drawn from those memories. An Override is a forbidden act of sovereignty: the Manyborn rewrites an inherited law of their own existence.

Three factions provide a small, coherent conflict. The Wardens preserve rules because ordinary people depend on them. The Unbound want freedom even when rewriting causes disasters. The Archivists preserve lost possibilities without agreeing who should use them. NPCs argue about consequences rather than lecture about programming.

The campaign antagonist, the **First Pattern**, removes contradictions to stabilize the world. The player must defeat its regional enforcers, then decide how the world should remember the catastrophe. Narrative choices change dialogue and an ending vignette; branching campaigns are outside first-release scope.

The four proposed regions are Ash Foundry, Glass Marsh, Hollow Archive, and Crown Scar. Each has its own silhouettes, hazard language, enemy family, signature discoveries, and endgame material. The hub, Hearth, visibly repairs itself as the campaign advances.

## Session and combat loops

The immediate loop is read an enemy tell, move or counter, generate a memory, choose how to spend it, collect a useful reward. An encounter should last roughly 20–60 seconds for an ordinary pack, with short breathing room afterward. Normal enemies initially survive two to four basic hits; elites require approximately 10–25 seconds of effective pressure. These are prototype encounter targets, not fixed formulas.

A typical 15–25 minute session is: choose a destination and desired reward, traverse several authored combat rooms, encounter an optional challenge, defeat an elite or boss, inspect a few drops, return to Hearth, and adjust one build choice. Avoid mandatory town visits after every small room.

The long-term loop is discover skills and signatures, explore different builds, complete regional challenges, enter harder expeditions, earn Override, then keep advancing through an endless sequence of Fracture tiers. Character level, Resonance ranks, equipment attunement, and difficulty have no designed maximum. The campaign has an ending; character growth and the endgame do not. Players choose when to push their frontier, farm a comfortable tier, or try another build.

## Combat rules

Use a 2D oblique top-down camera with eight visual facings and continuous movement and aim. Square navigation cells sit underneath the art; do not introduce true isometric geometry for the first release. World distances use meters with 32 world pixels per meter, independent of display scaling.

WASD plus mouse aim and a twin-stick controller are first-class inputs. Six ability slots contain a free basic attack, three chosen active skills, a dedicated Traverse action, and a healing flask. The four offensive/utility skills share the same input abstraction on keyboard and controller. A click-to-move scheme is a later option after pathing and input-priority tests, not a blocker for the slice.

Attacks have windup, active, and recovery phases. Movement can cancel recovery after the authored commitment point; cancellation never produces a free hit or returns already-spent resources. Dodge may interrupt cancelable attacks, but cannot cancel an explicitly committed heavy strike before its hit window. Communicate that commitment through animation and audio.

Initial movement speed is 4 meters per second. Standard Traverse is a 3-meter evade lasting 0.30 seconds, with a 1.2-second cooldown starting at acceptance and an initial 0.12-second evasion window. These values require playtesting. The evade avoids explicitly Evadable hits; floor hazards and attacks labeled Unavoidable still apply. Never rely on color alone to distinguish them.

Major enemy windups start around 0.6–1.0 seconds; ordinary attacks can be faster after their patterns are taught. Aim for player attacks that respond visibly on the next rendered frame, with a 0.10-second input buffer. Network latency is not part of the baseline.

All heroes use Life, Focus, and Strain. Focus pays for skills, starts at 100, and regenerates at an initial 8 per second; basic attacks remain usable at zero. Strain only limits Overload frequency and is specified separately. Flask starts with three charges, heals 35% maximum Life over one second, and refills at checkpoints and boss resets. Do not require consumable shopping for routine attempts.

Physical, Ember, Storm, and Void are the initial damage types. Physical uses Armor; the three supernatural types use one shared Resistance statistic. The combat sheet exposes reduction against the current enemy tier. Do not add separate elemental resistance caps, accuracy, random evasion, penetration, and several overlapping defenses in the first release.

Stagger is a visible control meter. Ordinary enemies can be interrupted by designated attacks. Bosses accumulate stagger and enter short authored vulnerability windows, with temporary stagger immunity afterward. Every boss remains beatable without a stagger-focused build. Root, slow, and knockback have explicit boss equivalents rather than silently failing.

## Heroes and skills

Heroes are **Frames**, each with a different starting identity, basic action, skill collection, and talent layout. Overload signatures are shared where their capability requirements fit. Classes do not map directly to C# inheritance trees.

| Frame | Fantasy and combat identity | Initial skills |
| --- | --- | --- |
| Warden | Close-range pressure, counterplay, short control windows | Cleave, Shield Pulse, Chain Lance, Faultline |
| Threadseer | Spatial spellcasting and delayed area control | Needle, Ember Well, Tether, Storm Loom |
| Revenant | Mobile ranged pressure and a single commanded echo | Shard Shot, Reap, Echo Order, Veil |

Build Warden first. The first arena needs only Cleave, Shield Pulse, and Chain Lance plus Traverse and flask. Add Faultline and two alternative Warden skills for the slice. The other Frames remain design stubs until one Frame supports three enjoyable builds.

The full release budget is eight skills per Frame, four equipped at once including the basic. Each skill has five rank upgrades and one mutually exclusive technique choice from two options. Rank primarily improves magnitude; techniques change that skill's persistent geometry or role. A technique applies on every eligible use, while an Overload chooses among contextual action implementations and consumes memories.

Example: Cleave's technique can make its arc wider or its reach longer. A Cleave Overload can spend Momentum to turn the next use into a lunging cut. A traversal Override changes the meaning of the character's Traverse action regardless of which skills are equipped.

## First arena tuning sheet

The following values make the first greybox implementation concrete. Store them as a named prototype balance profile, not constants spread through code. Begin the Warden at 200 Life, 100 Focus, 100 weapon Attack Power, 40 Armor, 20 Resistance, 5% critical chance, and a 1.5 critical multiplier. Skill rank is zero initially. The shared damage formula is in the implementation plan.

| Skill | Role and geometry | Base damage budget before bonuses | Focus and cooldown | Windup, active, recovery |
| --- | --- | --- | --- | --- |
| Cleave | Basic Assault, 100-degree arc with 1.5-meter reach | 10 + 4 per rank + 0.4 × Attack Power | Free; no separate cooldown beyond the action cycle | 0.12 s, 0.08 s, 0.20 s |
| Shield Pulse | Assault and crowd control, 80-degree cone with 2-meter reach, 0.5-meter ordinary-enemy push | 5 + 4 per rank + 0.25 × Attack Power | 25 Focus; 6-second cooldown | 0.20 s, 0.10 s, 0.25 s |
| Chain Lance | Piercing Assault, 6-meter projectile path, 0.25-meter radius, maximum three victims | 20 + 8 per rank + 0.8 × Attack Power per victim | 24 Focus; 2-second cooldown | 0.25 s, projectile launch, 0.25 s recovery |

Each victim can be hit once by a base action. Chain Lance travels at 15 meters per second, ends at terrain or maximum range, and uses swept collision queries. Its per-victim budget is not divided by the number of targets; piercing is its geometry advantage. Shield Pulse deals 30 stagger units, Chain Lance 15, and Cleave 5; an initial ordinary stagger threshold is 40, boss threshold 200. Displacement and boss stagger still follow the shared resistance rules.

Allow recovery cancellation after its first 0.10 seconds. Windup can be canceled by evade until the final 0.05 seconds before the active phase; once an action commits its active phase, finish that phase before canceling. Already committed costs remain spent. Tune these boundaries with real input latency and animations.

Initial targets are ordinary enemies with 120–160 Life, an elite with 900, and Bellkeeper with 6,000. Start ordinary enemy hits at 12–20 pre-defense damage and clearly telegraphed boss heavy hits at 55–80. These fixtures establish a repeatable comparison; change them if measured kill times or survival contradict the combat goals. Patterns that replace geometry use their own authored timeline but retain the selected skill's Focus payment and cooldown.

## Three Warden build hypotheses

These are playtest fixtures, not finished optimal builds. Test with equal equipment budgets and appropriate unlocked bindings.

| Build | Skill and talent emphasis | Signature loadout | Intended decisions |
| --- | --- | --- | --- |
| Pursuer | Cleave reach, Chain Lance, movement and Focus talents | Pursuit, Crossing, Reprieve | Spend Momentum closing distance or reserve it for escape; save Echo for recovery |
| Counterfighter | Shield Pulse control, wider Cleave, defensive talents | Afterstrike, Shelter, Reprieve | Earn Echo from enemy pressure, then decide between a delayed attack, projectile cover, or emergency recovery |
| Convergent | Faultline area control, sustained Focus, modest durability | Pursuit, Afterstrike, Convergence | Hold one memory until the second arrives, manipulate pack position, then commit a stronger combined action |

The Counterfighter intentionally has several competing uses for one memory. If that makes it starved rather than thoughtful, adjust generation opportunity or the loadout before adding passive token-generation gear. Add Elsewhere to one fixture at a time and measure how it changes positioning and failure modes.

## Progression responsibilities

| System | Player question | Source and limit | Excluded responsibility |
| --- | --- | --- | --- |
| Character level | How far has this hero developed? | XP with no maximum level; foundation milestones through 60 | Does not automatically scale every enemy to the player |
| Resonance | How does this hero keep growing? | One point per level after 60; uncapped Might and Resolve ranks | Cannot buy extra bindings, oaths, invulnerability, or unlimited speed |
| Skills | What actions do I bring? | Skill unlocks, 24 total skill points by level 60 | Cannot globally rewrite healing or traversal |
| Talents | What strengths and tradeoffs define this Frame? | 30 points, 36 authored nodes per Frame | Cannot add signature dispatch or earn Override |
| Codex | What knowledge have I recovered? | Exploration and challenge records; equip two minor inscriptions | No second legendary-item power library |
| Equipment | How strong and resilient am I? | Six slots, small affix pool, targeted crafting | No hidden access to the main mechanic |
| Overload | How can the same action answer different situations? | Discover signature patterns, equip up to three bindings | No universal permanent damage multiplier |
| Override | Which inherited rule do I replace? | Endgame ritual and mastery proof; one active contract | No random loot drop or ordinary respec node |

The skill-point schedule is 20 points at even levels 2–40 plus four from campaign milestones. Talent points arrive at even levels 2–60. A basic skill works at rank zero. Skill rank costs one point per rank, technique costs one point, and four fully invested skills therefore use the 24-point budget. Techniques require rank three. Unspent choices are legal.

Those skill and talent budgets preserve a build's choices; they do not stop leveling. Level 61 and every later level grants a Resonance point, freely assigned at Hearth to uncapped Might or Resolve. These continuously improve damage or maximum Life. The same character-level counter continues through 100, 1,000, and beyond; there is no prestige reset or replacement “real level.” Exact formulas and reward rules are in [the endless progression specification](ENDLESS_PROGRESSION.md).

Talents form three short connected branches per Frame. Small nodes cost one point and have one rank; six of the 36 nodes are larger tradeoffs. A character equips at most two of those larger nodes and must satisfy path prerequisites. The slice needs only 12 nodes and eight available talent points in its accelerated test profile. Do not build a continent-sized passive tree.

Codex inscriptions are modest exploration rewards such as greater material pickup radius or a small out-of-combat recovery benefit. They never generate memories, remove Strain, or waive Override requirements. Entries also explain enemy tells, discovered sources, and signature examples. First release: 12 inscriptions, two equipped, no rank grinding.

Respeccing skills and talents is free at Hearth. Overload bindings and unlocked Override choices can also be rearranged there. Provide named loadouts and a preview showing incompatible choices. There are no paid respecs. Account-wide discoveries reveal sources and recipes to alternate characters; levels, skill points, and Override qualification remain character-specific.

## Onboarding and pacing

| Point in journey | Introduce | Reason |
| --- | --- | --- |
| First five minutes | Move, aim, attack, evade, flask | Establish combat without menus |
| Minutes 5–15, roughly level 3 | Momentum and one guided Overload | Demonstrate the selling point early |
| Levels 5–12 | One technique, first talents, first Codex entry | Establish distinctions through examples |
| Level 12 | Second Overload binding and Echo memory | Add competition for the same input |
| Level 20 | Override preview chamber and visible requirements | Give a long-term ambition before commitment |
| Level 30 | Third binding and two-memory signatures | Enable full signature selection |
| Levels 35–45 | Campaign conclusion and introductory expeditions | Begin exploring the endgame while foundational progression continues |
| Level 60, then 61 onward | Foundation complete, then one Resonance point every level | Transition into endless numerical growth while retaining build choices |
| Level 100 or higher | Override qualification can be completed with tier 10 proofs and other requirements | A demanding milestone that never depends on reaching a maximum level |

A campaign target of 8–12 hours and a broad first Override target of 60–100 total character hours are hypotheses. Validate campaign pacing before expanding the world. Accelerated development profiles must be unmistakably labeled and excluded from economy measurements.

The tutorial first shows the same button producing a normal Cleave, then a lunging Cleave after movement. It then asks the player to use the memory defensively instead. This teaches choice, not simply waiting for a buff icon. A training room allows repeatable enemy tells, fixed gear, and unlimited safe experimentation.

## Equipment and economy

Six equipment slots: weapon, head, body, hands, boots, and charm. No separate offhand, ring pair, belt, jewels, or socket grid. A Frame's shield or focus object is part of its weapon presentation. Each slot has a clear primary value: weapon Attack Power, armor-piece Armor, and charm maximum Focus.

Three rarities suffice: Common with its base value, Rare with two affixes, and Relic with two affixes plus one fixed distinctive property. Rare equipment must complete all campaign and required endgame content. A Relic's property is a modest always-on utility or skill adjustment; it cannot imitate signature routing or replace a core contract.

Initial affix pool: maximum Life, Focus regeneration, Attack Power, critical chance, critical bonus, Armor, Resistance, movement speed, skill haste, and flask recovery. Slot eligibility narrows the pool, and no item rolls the same affix twice. Start the slice with only six of these. Show exact before/after changes, including caps, in equipment comparison.

Gear has five authored foundation bands unlocked during campaign and introductory expeditions. Each has a narrow base-stat range; earlier pieces can be upgraded into an unlocked band while retaining their affixes. Each item also permits three predictable quality upgrades of 4% to its base value. After the campaign, a shared equipment Attunement Grade continues upward without a maximum, using the same gold and Alloy. It improves damage, Life, and defense ratings without adding affix lines or multiplying movement speed and cooldown reduction. All equipped items inherit the character's grade, so keeping a favorite item or changing a build does not require six separate endless upgrade chores. Grade 1 is granted initially; higher grades require clearing at least that Fracture tier. See [attunement rules](ENDLESS_PROGRESSION.md) for the exact scaling and costs.

Crafting uses gold and a single common salvage material, Alloy. Players can replace one chosen affix with a selected legal affix at a fixed, previewed cost; afterward only that affix slot remains replaceable. There is no destruction chance. Relic recipes have guaranteed targeted sources. Ordinary gear uses inventory space; crafting and ritual resources use separate wallets.

The full economy has two general-purpose resources and four regional Override resources. Proofs of mastery are journal flags, not more currencies. The rare ritual stockpile cannot be spent accidentally at a general crafting vendor. Each regional activity also grants ordinary gear and gold so pursuing Override does not halt all other progression.

Initial reward target: one meaningful equipment candidate per completed combat room or objective, with fewer low-value drops as progression advances. A 12-minute expedition should produce roughly four to eight equipment candidates, not dozens per minute. Currency and materials auto-pick up in range. Compare, mark, salvage, and lock gear with either input device; never automatically salvage a locked item.

Do not let Overload or Override multiply ritual rewards. Rarer access should reflect play breadth and demonstrated competence, not an exponential advantage for the first characters who unlock it.

## Encounters and world structure

The [world design requirements](WORLD_MAP_DESIGN.md#geometry-generation-and-content-production) are implemented through shared geometry, physical travel and graph-driven Fractures; see [current behavior and evidence](WORLD_IMPLEMENTATION.md). Older journeys retain their original traversal.

Use authored rooms assembled into constrained graphs. The campaign uses mostly authored sequences; expeditions remix compatible rooms, enemy groups, and optional objectives. Generate the graph first, validate required paths and exits, then populate encounters, then decorate. Keep bosses and story spaces handcrafted.

The slice enemy roster is a pursuer, shieldbearer, ranged caster, charging brute, summoner, and area-denial creature. Roles have different shapes and movement rhythms. Packs combine two or three roles; limit simultaneous major windups through a combat director so threat comes from interaction rather than unavoidable overlap.

Each biome introduces one environmental interaction useful to several builds. Examples include cooling vents in Ash Foundry and movable cover in Glass Marsh. Terrain can create tactical opportunities, but no signature should require a rare terrain type to function in ordinary content.

The first boss, the **Bellkeeper**, teaches the core system in three patterns: a wide sweep that rewards repositioning, a bell volley that can be evaded for Echo, and an exposed recovery that rewards a prepared attack. At low health it alternates these patterns faster without adding unreadable particles. In a special Override trial version, the arena tests planning a return location before a delayed hazard.

Boss deaths reset the encounter and flask charges, with a checkpoint directly outside. Campaign death loses no XP, items, or ritual materials. Expedition death ends the current optional bonus chain but preserves already banked rewards. Hardcore and permanent death are deferred.

## Endgame and Override journey

The **Fracture Atlas** is a compact board of region routes with tiers 1, 2, 3, and onward without a final tier. Each route declares its reward, enemy strength, and encounter rules before entry. Clearing the highest available tier opens the next; there is no random map-item supply requirement. Older tiers remain available and never scale automatically to the character. Introduce one route rule initially, then at most two compatible rules and one boss mutation per expedition. Bounded simultaneous complexity preserves readable combat while numerical difficulty keeps increasing.

Every ten tiers form a **Palimpsest Chapter**. Chapter numbers continue indefinitely, recombining region layouts, enemy roles, environmental laws, and boss behaviors from validated authored pools. The chapter boss offers three next-chapter routes with different stated rules and targeted rewards. Examples include bell volleys that leave delayed echoes, vents that alter safe movement lanes, and a boss that places its own return anchor. No combination may require owning Override, a specific memory, or a rare item. Repetition remains possible; endless progression does not promise infinitely many handcrafted mechanics.

Add two optional goals alongside tier pushing: **Anomaly Hunts**, selecting a disclosed boss mutation for targeted loot, and **Sovereign Echoes**, encounters that teach enemies' versions of oath mechanics and award visual variants or records. Neither grants a second active Override or exclusive mandatory stat multipliers. **Expedition Chains** connect three regions with a reward bank after each leg and an optional continuation bonus. A chain failure preserves banked XP, items, Seals, and frontier access. These systems and their delivery order are detailed in [the endless progression specification](ENDLESS_PROGRESSION.md).

Three activity families reuse tested room and enemy systems: Hunts emphasize priority targets and bosses; Breaches emphasize wave defense and movement; Vaults emphasize exploration, route choices, and optional combat objectives. None requires stealth, a second combat game, or a separate simulation engine.

Override resources come from all four regions and mastery proofs require all three activity families at tier 10 or higher. Level 100 is a minimum qualification milestone, not a maximum. Targets, resource arithmetic, retries, and activation semantics are defined in [the core specification](CORE_SYSTEMS.md). The trial uses a disclosed normalized power profile so an extremely high-level character must still demonstrate the mechanic. Ordinary endgame remains viable without Override. Optional sovereign challenges can recognize an earned Override without becoming a mandatory power gate.

Do not use login streaks, daily lockouts, season resets, or consumable trial tickets to manufacture scarcity. If a small fraction of players earns Override, it should be because the journey is demanding. No honest design can guarantee a fixed percentage of lifetime owners, especially in an offline game.

## Pixel art and audio direction

Use dark mineral environments, worn fabric, sharp metal highlights, and bright spectral memories. Overload has a distinct split silhouette and short afterimage; Override adds a permanent small visual alteration and an action-specific effect. Keep hostile telegraphs higher contrast than friendly damage effects.

The initial world viewport is 640×360 with nearest-neighbor integer scaling and a fixed combat field of view. Render scalable UI separately at output resolution. Godot documents integer scaling as a way to avoid uneven pixel display; the two-layer world/UI arrangement is the proposed implementation. [Godot on multiple resolutions](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html)

Use 32×32 environment tiles, approximately 32×48 character bodies on 64×64 animation canvases, and eight-direction hero animations. Prototype four directions first. Ordinary enemies can use four directions if silhouettes remain clear; bosses use authored facings. Avoid equipment paper-doll permutations: weapon families and a few Frame palettes provide visual progression.

The art pipeline stores editable source, exported atlas, metadata, pivot, collision-footprint notes, animation tags, and provenance. Automate sprite-sheet export; Aseprite exposes batch and sheet operations suitable for this. It is an optional licensed tool, not a purchase made by this plan. [Aseprite CLI documentation](https://www.aseprite.org/docs/cli/)

The slice art budget is one hero, six enemy families, one boss, a hub kit, a dungeon kit, roughly 20 essential VFX, and roughly 40 UI icons. A hero with eight facings, ten animation states, and an average of six frames already requires about 480 frames before weapon variants. Pixel art is a substantial production task; placeholder art must not be mistaken for a solved animation pipeline.

Audio needs separate cues for memory gained, signature ready, signature spent, Strain rejection, major enemy windup, and Override invocation. Rate-limit repeated cues. Couple every essential sound with a visual cue. Initial audio budget: 35–50 gameplay sounds, three music loops or stems, and ambience for the slice; no voiced campaign requirement.

## Interface and accessibility

HUD: Life and Focus, four skill slots, Traverse and flask, a compact memory rack, Strain, and a small icon beside each eligible action showing its currently predicted implementation. Memory shapes and labels must work without color. Avoid a screen of independent buff timers.

The Overload panel has three binding rows written in plain language: action, required memories, result, cost, priority, and preview. The Override panel has available contracts, lost behavior, new behavior, compatibility, regional progress, and a route to the next requirement. It is visible long before activation is possible.

Support full remapping, keyboard-only menus, controller-only menus, independent UI scale, readable fonts, hold/toggle alternatives, subtitles, separate audio sliders, adjustable shake and flashes, and aim assistance. These priorities follow the basic accessibility guidance; their exact implementation remains project design work. [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/basic/)

Provide a pauseable offline game. Pausing freezes combat, memory expiration, and cooldowns. Offer relaxed, standard, and severe combat tuning with identical story and reward access. Mastery can have a clearly described optional standard-rules badge; access to ordinary Override should not depend on refusing accessibility settings. Keep the mastery challenge demanding through decision quality and encounter knowledge.

Localize through stable string keys from the beginning. English is the first authored language; text should expand by 30% without clipping. Do not assume a translation language from the developer's location. Provide a high-contrast non-pixel font option for tooltips.

## Scope boundaries

| Deliverable | Content budget | Purpose |
| --- | --- | --- |
| Mechanic arena | One Frame, three skills, three signatures, one boss, one Override behind a debug switch | Test the defining interaction |
| External vertical slice | One Frame, six skills, six signatures, two memory types, 12 talent nodes, four Codex entries, six enemy families, one boss, eight combat rooms, hub, 12 gear bases, two Relics | Prove a complete 30–45 minute loop |
| Small release candidate | Three Frames, 24 skills, 18 shared signatures, four memory types, three Overrides, four regions, 24 enemy families, eight bosses, 48 room templates, 36 gear bases, 12 Relics | An authored campaign, uncapped levels, and endlessly scaling endgame |

The slice targets a level 1–15 onboarding session and uses separate preview profiles for later mechanics. Its leveling service has no maximum, and automated profiles must already cross levels 60, 100, and 1,000. Content ends at the slice's exit, not because XP stops working. It does not claim to validate a 60–100 hour Override journey or long-term endgame pacing; those require economy tools and extended playtests.

Online co-op, trade, PvP, seasons, battle passes, procedural story generation, arbitrary player scripting, pets, mounts, housing, and a public mod API are outside the baseline. Multiplayer is a major product decision that must be revisited before production if required. Offline saves cannot establish trustworthy global rarity or competitive rankings.

A premium release with optional future content expansions is the working business model. No price or sales forecast is assumed. Validate demand with a representative demo, playtest feedback, and a clear gameplay trailer. Set production spending only after the slice establishes actual content costs and the owner's available resources.

The name remains provisional. A released game already uses Overload, which creates a practical discoverability concern before public branding. [Existing Overload Steam listing](https://store.steampowered.com/app/448850/Overload/)
