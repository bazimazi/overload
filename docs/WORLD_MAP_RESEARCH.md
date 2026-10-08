# World and map research

Researched 8 October 2026. **Research and repository audit, before gameplay implementation.** Read alongside [the world proposal and delivery plan](WORLD_MAP_DESIGN.md).

## Main finding

Overload needs places that players can understand, choose between, explore, and change. The current regional artwork and combat systems provide useful foundations, but a sequence of differently dressed arenas does not supply that experience. The recommended direction is a persistent authored regional geography, explorable zones assembled from deliberate encounter spaces, and a separate procedural Fracture layer with visible destinations and goals.

This recommendation is an inference from the references and the actual project, not a claim that combining their features guarantees a world-class game. The quality bar is meaningful exploration, coherent fiction, readable combat, and repeatable decisions. Map area and content count are production inputs, not measures of success.

## Method, versions, and limits

Reviewed Blizzard and Grinding Gear Games developer articles, official game descriptions, expansion pages, and patch notes. The comparison covers Diablo II, Diablo III, Diablo IV, Path of Exile 1, and Path of Exile 2. Historical designs are explicitly separated from subsequent changes. Recent editions examined include PoE 1 **3.28.0 Mirage**, PoE 2 **0.5.0 Return of the Ancients**, and Diablo IV's **Lord of Hatred / 3.0.0 announcement**. These are dated reference editions, not a claim that every statement describes the latest live patch on the research date.

The local audit used this checkout, `C:\dev\github\bazimazi\overload`, at baseline commit `4682f59` (`improve graphics`), its current design/status documents, source code, content JSON, art prompts, and the existing `artifacts/experience/first-room.png` and `hearth.png` captures. The other IDE tab at `C:\dev\games\overload\docs\STATUS.md` belongs to a separate directory and is older; this report does not treat it as current implementation evidence.

No hands-on reference-game sessions, video-transcript analysis, new Overload gameplay session, or player study was performed. Existing Overload captures were visually inspected. Search results and community complaints were discovery aids; the conclusions below rely on publisher/developer sources. The PoE 2 expansion landing page required JavaScript, so its official patch notes were used for substantive claims. Numerical targets in the companion proposal are original hypotheses.

## What the reference games teach

### 1. Geography can stay understandable while local layouts change

Diablo II's official guide distinguishes randomized areas from fixed areas and describes discovered waypoints persisting with the character. This separates finding one's way through a particular layout from retaining useful knowledge of the journey. The guide also exposes unavailable waypoint destinations and explains quest restrictions on travel. [S01: Arreat Summit basics](https://classic.battle.net/diablo2exp/basics/)

**Application:** keep the relationships between Hearth and the four regions stable. Let dungeon interiors vary. Discovery should improve the player's future travel options; it should not disappear merely because a combat instance is regenerated. A destination's geography, its unlock condition, and its encounter seed need separate identities.

### 2. Repeatable combat maps need consistency as well as variety

Diablo III's 2.4.3 notes revised Greater Rift floor size, monster distribution, entry safety, and repeated tileset behavior. They also moved a waypoint and adjusted bounty placement to shorten access. Patch 2.5.0 subsequently changed tileset frequencies, reducing some enclosed environments and increasing some open ones. These are concrete examples of tuning the distribution of generated experiences rather than merely adding more rooms. [S02: 2.4.3 notes](https://news.blizzard.com/en-gb/article/20426802/patch-2-4-3-now-live), [S03: 2.5.0 first look](https://news.blizzard.com/en-us/article/20597130/first-look-patch-2-5-0)

**Application:** measure how far players travel, how much usable fighting space each role receives, and how often the same topology appears. A narrow map cannot be considered equivalent to an open map just because both contain the same enemy budget. Fracture fairness needs geometry and encounter-distribution evidence.

### 3. A region is a society and an ecology, not just a palette

Diablo IV's 2019 feature overview described contiguous regions, regional monster ecologies, towns, and randomized dungeons. The March 2022 environment article goes further: coast settlements reflect fishing and available building materials, damaged architecture communicates history, and weather and lighting reinforce local character. Its dungeon examples use thematic dressing and transitions between compatible kits while protecting gameplay readability. These are development descriptions, not promises about every current location. [S04: feature overview](https://news.blizzard.com/en-us/article/23189677/diablo-iv-feature-overview), [S06: March 2022 environment update](https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022)

**Application:** define how people survived in Ash Foundry, what its infrastructure did, how the catastrophe damaged it, and why its enemies occupy particular spaces. Use furnaces, worker housing, cooling channels, transport routes, and a recognizable skyline to express those answers. Preserve quiet terrain under attacks and characters.

### 4. Exploration becomes consequential when a place changes

Blizzard's June 2020 development update described hostile Camps becoming outposts with NPCs and waypoints, often telling their stories visually. The 2022 announcement described Strongholds producing permanent local changes and sometimes exposing further content. The former also documents grayboxing as a cheap way to test an area before final art. [S05: June 2020 update](https://news.blizzard.com/en-us/article/23463858/diablo-iv-quarterly-updatejune-2020), [S07: 2022 world announcement](https://news.blizzard.com/en-us/article/23816540/all-hell-breaks-loose-in-2023diablo-iv-is-coming)

**Application:** reclaim one meaningful Foundry location that opens a shortcut, refuge, and waypoint. Show the result in the scene and on the map. Test the route and encounter first with simple geometry. A progress percentage in Hearth alone cannot communicate a repaired world.

### 5. Navigation and objectives can undermine excellent environments

Diablo IV's beta retrospective explicitly addressed backtracking and tedious dungeon objectives. Changes included moving structure objectives toward main paths, helping straggler completion, reducing interaction delays, and improving door information on the minimap. The source documents particular problems and responses; it does not establish that every dungeon later solved them. [S08: beta retrospective](https://news.blizzard.com/en-us/article/23938289/diablo-iv-open-beta-retrospective-transforming-feedback-into-change)

**Application:** give optional branches a purpose, reconnect long detours, and avoid a mandatory hunt for the last ordinary enemy. A door, quest marker, and reward state should explain themselves. An objective must change an action or decision, rather than simply require another lap through cleared corridors.

### 6. Endgame structure should support the player's intent

The Lord of Hatred announcement describes an endgame hub and War Plans: players select activities, chain them, travel between them, and pursue activity customization and rewards. It also describes map overlay and pathfinding additions. These features are presented in a specific expansion/update context. [S09: Lord of Hatred announcement](https://news.blizzard.com/en-us/article/24267729/prepare-for-the-reckoning-lord-of-hatred-draws-near)

**Application:** turn Overload's existing route and chain systems into a legible spatial selection experience. Show reward, duration estimate, hazards, and next unlock before entry. Let comfortable farming, new discoveries, and frontier pushing coexist. The existing offline game does not need shared-world scheduling to offer these choices.

### 7. Procedural generation is authored design with controlled combinations

PoE's developer level-design interview describes interchangeable encounter spaces and set pieces, restrictions against poor combinations, and generating thousands of levels to assess monster/chest counts and density. It discusses the tradeoff between procedural variation and precise curation, and the extra control needed for trap sequences. PoE's game overview also describes instanced, randomized outdoor areas and modifiable endgame maps. Its other economy descriptions are historical and are not used as current rules here. [S10: game overview](https://www.pathofexile.com/game), [S11: level-design interview](https://www.pathofexile.com/forum/view-thread/1654235)

**Application:** author encounter chunks with ports, footprints, sightlines, pacing roles, and compatibility tags. Generate a playable graph, embed it in geometry, then populate and decorate. Verify several actors' traversal and actual socket connections. Randomly shifting two obstacles inside a fixed rectangle supplies little spatial variety.

### 8. Revisiting a place can advance the story

The Fall of Oriath expansion describes returning through recognizable and new locations and witnessing the consequences of previous actions. Familiar geography therefore supports a changed situation instead of merely serving as reused scenery. [S12: Fall of Oriath](https://www.pathofexile.com/oriath)

**Application:** the Palimpsest already makes changed versions of places plausible. Return to a cooled Foundry route, a cleared Marsh passage, or an opened Archive wing. Change inhabitants, a usable route, and an environmental detail together. Keep the state comprehensible and avoid making every story choice a separate campaign branch.

### 9. An Atlas connects short runs to longer goals

PoE's 2016 Atlas notes tied connected destinations, completion, map acquisition, and guardian progression together. Siege of the Atlas later described a global specialization tree and paths through influenced maps toward major bosses. These are different historical systems: Shaper's Orbs, regional trees, and later trees must not be combined into an invented timeless ruleset. [S13: original Atlas mechanics](https://www.pathofexile.com/forum/view-thread/1714099), [S14: Siege of the Atlas](https://www.pathofexile.com/siege)

**Application:** each run should contribute to a visible frontier, regional proof, or boss pursuit. Destination familiarity and a long-term goal can make repeated maps meaningful. Overload's uncapped tiers should remain separate from its finite authored geography; adding a giant character-power tree is unnecessary.

### 10. Recent Atlas revisions make navigation and goals especially relevant

PoE 1's 3.28.0 notes decouple ordinary map items from a particular destination, place device-accessed content on the Atlas, introduce quadrant-based influence progression, and preserve some special entry requirements. GGG's follow-up explicitly identifies visibility of unfinished destinations and Atlas interaction improvements as work items. [S15: Mirage notes](https://www.pathofexile.com/forum/view-thread/3913392), [S16: Mirage follow-up](https://www.pathofexile.com/forum/view-thread/3916921/filter-account-type/staff)

PoE 2's 0.5.0 notes introduce fixed Atlas points of interest, guided activity storylines, deterministic quest versions of pinnacle encounters alongside repeatable versions, and additional Atlas navigation tools. Campaign changes add environmental direction clues, shorten some areas, and streamline some interactions. This is a May 2026 reference edition; the launch-era description of an unguided infinite Atlas is insufficient for this comparison. [S17: Return of the Ancients notes](https://www.pathofexile.com/forum/view-thread/3932540)

**Application:** a good map needs destination intent and clear completion states, not just a web of icons. Required story progress should be obtainable deliberately. Uncapped repeat play can exist beyond that conclusion. A trail, industrial pipe, or landmark should help navigate without constantly opening a menu.

## Comparative synthesis

This table is a design interpretation of the sourced observations above, rather than a ranking or measured player preference.

| Reference | Most useful strength for Overload | Tradeoff to account for | Adaptation |
| --- | --- | --- | --- |
| Diablo II | Persistent travel knowledge within mixed fixed/random spaces | Rediscovery and travel friction need deliberate handling | Stable regional connections and earned waypoints |
| Diablo III | Short repeatable sessions and generator-distribution tuning | Combat efficiency can overshadow exploration | A distinct Fracture mode with reliable entry safety and bounded runs |
| Diablo IV | Place identity, regional ecology, and reclaimed locations | Scale and objective chores can create empty travel or repetition | Compact explorable regions with visible local consequences |
| PoE 1 | Authored procedural spaces, destination progression, and specialization | Stacked systems demand considerable explanation and maintenance | Small compatible pools, disclosed rules, targeted rewards |
| PoE 2 0.5 | Explicit geographic goals and guided endgame arcs | More content still needs efficient navigation and pacing | Fixed milestones with repeatable play after the story conclusion |

The shared lesson is a hierarchy of decisions: **where to go, which local route to take, how to fight there, and why to return**. Overload currently offers several decisions about builds and difficulty; it offers much less of the middle two spatial layers.

## Audit of the current game

### What already exists

- Four region identities, 24 regional ordinary enemy families, eight regional bosses, three Frames, and a sixteen-checkpoint campaign.
- 48 regional templates: 12 per region. Direct content inspection finds **0–2 obstacle rectangles per template and 41 distinct obstacle arrays**. Distinct arrays should not be mistaken for 41 distinct explorable maps.
- Seven-group expeditions, six world laws, disclosed route/chapter rules, activity families, chains, exact reward budgets, and atomic save transactions.
- Footprint-aware tactical navigation, readable tells, low-resolution world rendering, independently scaled UI, and original regional background art.

These are source/content observations. The historical test counts in STATUS are not newly rerun results from this research pass.

### Gaps that matter to the feature

| Finding | Concrete evidence | Consequence |
| --- | --- | --- |
| Campaign movement is sequential | [JourneySession](../src/Overload.Game/Scripts/JourneySession.cs) chooses region from `room / 4` and advances from `CheckpointRoom`; [JourneyRules](../src/Overload.Domain/JourneyRules.cs) grants rewards and increments the checkpoint | A new region arrives after another clear, rather than through an understood journey |
| Branches are metadata, not playable routes | [ExpeditionGenerator](../src/Overload.Domain/ExpeditionGenerator.cs) creates the same nine-node graph; [FractureSession](../src/Overload.Game/Scripts/FractureSession.cs) indexes encounters with `NextGroup`; source search finds no gameplay traversal of `Layout.Nodes` | Current graph-connectivity checks do not prove players can choose a branch |
| World extent equals one combat arena | [WorldView](../src/Overload.Game/Scripts/WorldView.cs) uses fixed perimeter walls; [Arena](../src/Overload.Game/Scripts/Arena.cs) creates a fixed camera at `(320,180)` | Multi-screen exploration needs level bounds and a following camera, not a larger background alone |
| Geometry assumptions are distributed | [TacticalNavigation](../src/Overload.Domain/TacticalNavigation.cs), [WorldQueries](../src/Overload.Game/Scripts/WorldQueries.cs), [WorldLaws](../src/Overload.Domain/WorldLaws.cs), and [EndgameWorld](../src/Overload.Game/Scripts/EndgameWorld.cs) contain fixed floor bounds, hazard areas, or objective positions | New geometry must drive movement, hazards, return landing, and visuals consistently |
| Art was authored for the existing arena | [Generation prompts](../assets/source/pixel-v2/generation.json) require a clear rectangular floor with detail around its perimeter; WorldView stretches one background across the scene | These assets are useful references and legacy scenes, but cannot function as a modular landscape kit |
| Persistent state assumes a linear journey | [CharacterState](../src/Overload.Domain/CharacterState.cs) requires contiguous claimed rooms equal to the checkpoint and has no world discovery/waypoint/quest phase aggregate | Branches, return travel, and world changes need explicit state and migration semantics |
| Scenario is mainly brief room text | [EncounterExperience](../src/Overload.Game/Scripts/EncounterExperience.cs) supplies regional introductions; the campaign ending appears in JourneySession | The fiction needs causal objectives, inhabitants, and consequences enacted in locations |
| Hearth mainly exposes menu destinations | Existing Hearth capture and [ArenaHud](../src/Overload.Game/Scripts/ArenaHud.cs) | Its strong atmosphere can become a spatial hub with understandable exits and inhabitants |

The inspected first-room capture supports the geometry finding: a single enclosed court, a regional name, three hostiles, and a common floor composition. The Hearth capture supports the menu-hub finding. These captures cannot establish combat enjoyment, campaign duration, or navigation usability.

## Recommended feature boundary

Build a **connected regional world**, rather than committing immediately to a seamless continent. The player walks across a zone, recognizes its landmarks, chooses an optional route, discovers a refuge, and enters a dungeon. Explicit zone transitions are acceptable. Hearth remains the anchor. The Fracture Atlas represents unstable expeditions rooted in those places and retains uncapped tiers.

The Palimpsest gives the maps an original purpose: discarded versions of inhabited places reappear, while the First Pattern restores stability by erasing what does not fit. The player should see both the need for stability and its human cost. A repair should change a route and a community, not merely increase a counter.

The first proof should cover Ash Foundry only: a walkable Hearth approach, one multi-screen exploration zone with a genuine fork and reconnecting route, one discoverable reclaimed refuge, one small dungeon, one regional boss consequence, persistent discovery, and a Fracture replay of familiar terrain. The companion proposal defines the scenario, architecture, and acceptance evidence.

## Questions to answer through the prototype

| Uncertainty | Evidence to collect | Design response if it fails |
| --- | --- | --- |
| Players understand the journey | Observe whether they can identify the Foundry, their current task, and a route back | Improve exits, landmarks, and map hierarchy before adding zones |
| Exploration changes decisions | Observe route selection and optional discoveries across different builds | Change branch affordances and rewards rather than add acreage |
| Larger zones preserve Overload readability | Compare cramped, open, and mixed encounters with the existing arena | Revise spacing, sightlines, camera, and hazard placement |
| Revisited terrain remains interesting | Compare campaign and Fracture versions of the same location | Change topology/objective combinations while retaining recognizable landmarks |
| The scenario is understood | Ask what the First Pattern does and what reclaiming the refuge changed | Add a visible cause/consequence and shorten exposition |
| Production is sustainable | Record actual effort for one playable zone and one finished kit | Reduce final location count or reuse kits with distinct layouts |

## Source ledger

All sources accessed 8 October 2026. Staff posts, rather than forum replies, are the evidence for GGG threads. This ledger identifies the edition and relevant section; the linked findings above contain the substantive notes.

| ID | Primary source | Edition / section used |
| --- | --- | --- |
| S01 | [Blizzard, Arreat Summit basics](https://classic.battle.net/diablo2exp/basics/) | Diablo II legacy guide: waypoints, travel, map layouts |
| S02 | [Blizzard, Patch 2.4.3](https://news.blizzard.com/en-gb/article/20426802/patch-2-4-3-now-live) | Diablo III: Adventure Mode, Rifts, Zones |
| S03 | [Blizzard, Patch 2.5.0 first look](https://news.blizzard.com/en-us/article/20597130/first-look-patch-2-5-0) | Diablo III: Adventure Mode Updates |
| S04 | [Blizzard, Feature Overview](https://news.blizzard.com/en-us/article/23189677/diablo-iv-feature-overview) | Diablo IV, 2019 development overview: world and dungeons |
| S05 | [Blizzard, June 2020 update](https://news.blizzard.com/en-us/article/23463858/diablo-iv-quarterly-updatejune-2020) | Diablo IV: Blockout, Open World, Camps |
| S06 | [Blizzard, March 2022 update](https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022) | Diablo IV: environments, culture kits, dungeon transitions |
| S07 | [Blizzard, 2022 launch announcement](https://news.blizzard.com/en-us/article/23816540/all-hell-breaks-loose-in-2023diablo-iv-is-coming) | Diablo IV: exploration and Strongholds |
| S08 | [Blizzard, Open Beta Retrospective](https://news.blizzard.com/en-us/article/23938289/diablo-iv-open-beta-retrospective-transforming-feedback-into-change) | Diablo IV, 2023: dungeon layouts and objectives |
| S09 | [Blizzard, Lord of Hatred announcement](https://news.blizzard.com/en-us/article/24267729/prepare-for-the-reckoning-lord-of-hatred-draws-near) | Diablo IV, 3.0.0 announcement: hub, War Plans, navigation |
| S10 | [GGG, game overview](https://www.pathofexile.com/game) | PoE 1: instanced world and Map item concept; historical overview |
| S11 | [GGG, level-design interview](https://www.pathofexile.com/forum/view-thread/1654235) | PoE 1, 2016: generation, density, Labyrinth |
| S12 | [GGG, Fall of Oriath](https://www.pathofexile.com/oriath) | PoE 1, 3.0: changed and revisited campaign locations |
| S13 | [GGG, Detailed Atlas Mechanics](https://www.pathofexile.com/forum/view-thread/1714099) | PoE 1, 2016: adjacency, completion, guardian progression |
| S14 | [GGG, Siege of the Atlas](https://www.pathofexile.com/siege) | PoE 1, 3.17: Atlas progression and specialization |
| S15 | [GGG, Mirage patch notes](https://www.pathofexile.com/forum/view-thread/3913392) | PoE 1, 3.28.0, posted 26 February 2026: Endgame Changes |
| S16 | [GGG, Mirage follow-up](https://www.pathofexile.com/forum/view-thread/3916921/filter-account-type/staff) | PoE 1, 8 March 2026: Atlas visibility and interaction issues |
| S17 | [GGG, Return of the Ancients patch notes](https://www.pathofexile.com/forum/view-thread/3932540) | PoE 2, 0.5.0, posted 22 May 2026: Endgame, navigation, Campaign Replayability |

The research phase establishes enough direction for a bounded world prototype. It does not settle final production scope, certify a commercial quality level, or replace observing people playing the new maps.
