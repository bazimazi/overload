# Overload research and decisions

Research was conducted on 5 October 2026 using publisher announcements, official engine and language documentation, developer documentation, and product listings. The purpose was to extract applicable design and production lessons, not to reproduce another game's content or claim knowledge of every current balance patch. Gameplay feel has not been tested in the reference games during this planning session.

The design, mechanics, item limits, milestones, performance targets, time estimates, and rarity targets in this repository are recommendations to validate. Sources support the specific observations below; they do not establish that Overload will succeed or that its proposed mechanics are globally unprecedented.

The owner subsequently required uncapped character levels and difficulty. That requirement replaces the original finite progression recommendation. The design now includes endless Resonance growth, equipment attunement, and Fracture chapters. These are original proposals for this game; the plan does not assume that all other well-known RPGs have identical or uncapped leveling systems.

## Research findings and their application

| Topic | Source observation | Decision for this project | Limits |
| --- | --- | --- | --- |
| Item readability | Loot Reborn describes reducing affixes, simplifying evaluation, and reducing overall drops | Use six slots, two affixes on ordinary advanced gear, predictable crafting, and fewer loot decisions | That announcement describes a particular Diablo IV redesign, not today's complete item system |
| Progression placement | Lord of Hatred describes moving some build-defining choices into skill branches and other passives into items | Assign an explicit responsibility to every progression system so the main mechanic does not disappear into gear | The announcement cannot prove that our proposed separation is preferable |
| Skill combinations | PoE2 0.3.0 discusses revising support restrictions and introducing elemental infusions | Treat charge-based conditional effects as established territory; distinguish Overload through character-wide typed dispatch and competing action families | Novelty needs comparison and playtesting beyond terminology |
| Pixel-art ARPG precedent | Chronicon's developer-supplied listing describes procedural dungeons, classes, crafting, and endgame systems | A pixel-art presentation can accompany an ARPG scope, but our content budget must remain much smaller | A listing and its reviews do not establish sales, team effort, or achievable production time |
| Programming analogy | Microsoft's method and override documentation distinguishes parameter-list overloading from replacing inherited implementations | Keep additive signatures and replacement contracts technically distinct | The game's runtime dispatcher is a metaphorical adaptation, not C# compile-time overload resolution |
| Pixel scaling | Godot documents integer scaling and low-resolution viewports | Prototype the world at 640×360; keep readable UI independently scaled | Rendering configuration still needs visual inspection on actual displays |
| Automated builds | Godot documents headless import/export and export-template requirements | Make a Windows export smoke check part of the first milestone | Headless success cannot verify visual quality or game feel |
| Accessibility | The guidelines prioritize remapping, readable text, redundant color/audio cues, and usable controls | Include these before the slice instead of treating them as release polish | User testing remains necessary |
| Naming | An existing commercial game is named Overload | Keep the name provisional during development and revisit public branding | This is a discoverability observation, not a legal clearance assessment |

## Primary source notes

1. **Diablo IV Loot Reborn.** Used for the stated goals behind simpler affixes, fewer drops, and recoverable Codex knowledge. The project does not adopt its full crafting or seasonal structure. [Blizzard announcement](https://news.blizzard.com/en-us/article/24077223/galvanize-your-legend-in-season-4-loot-reborn)

2. **Diablo IV Lord of Hatred.** The skill-tree section describes active-skill branches and relocation of several passives and aspects. It matters because launch-era accounts of Diablo IV are not a timeless design reference. [Blizzard announcement](https://news.blizzard.com/en-us/article/24267729/prepare-for-the-reckoning-lord-of-hatred-draws-near)

3. **Path of Exile 2 The Third Edict.** The support overhaul and Player Changes sections informed the distinction between persistent skill modifications and consumed combat context. The page also demonstrates that these systems evolve substantially between releases. Do not present its 0.3.0 rules as the latest patch. [Official Grinding Gear Games notes](https://www.pathofexile.com/forum/view-thread/3826682)

4. **Chronicon.** Its official store listing provides an adjacent pixel-art ARPG reference. Its enormous listed item and skill counts are precisely the kind of scope this plan avoids at the beginning. [Developer product listing](https://store.steampowered.com/app/375480/Chronicon/)

5. **C# method signatures.** Two methods can share a name while differing in their parameter lists; return type alone does not distinguish overloads. The game similarly uses typed memories rather than different result names to select a signature. [Microsoft methods reference](https://learn.microsoft.com/en-us/dotnet/csharp/methods)

6. **C# override.** An override changes an inherited virtual or abstract implementation while preserving the relevant member contract. The proposed oath system restricts which character contracts are replaceable. [Microsoft override reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/override)

7. **Godot release archive.** The retrieved archive lists 4.7.2 stable and a newer development branch. Pin an actual stable editor and its export templates at implementation time. [Official archive](https://godotengine.org/download/archive/)

8. **Godot C#.** The documentation requires the .NET editor and SDK, supports desktop export, and documents a web-export limitation. Some paragraphs still reference older Godot versions; verify actual compatibility with a compiled export before establishing the build baseline. [Versioned C# documentation](https://docs.godotengine.org/en/4.7/tutorials/scripting/c_sharp/c_sharp_basics.html)

9. **Godot display scaling.** Integer scaling avoids uneven pixel sizes, with letterboxing tradeoffs at some output sizes. The proposed independent UI layer needs its own layout tests. [Multiple resolutions](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html)

10. **Godot command line.** The engine supports headless operation and named export presets; export templates must be available. Use these capabilities for reproducible builds and add project-specific smoke behavior. [Command-line tutorial](https://docs.godotengine.org/en/stable/tutorials/editor/command_line_tutorial.html)

11. **Godot multiplayer.** The documentation warns against trusting client-reported gameplay state and recommends authority over important outcomes. Offline implementation boundaries help future work, but do not make multiplayer a small switch. [High-level multiplayer](https://docs.godotengine.org/en/4.7/tutorials/networking/high_level_multiplayer.html)

12. **Unity 2D workflow.** Unity provides a documented 2D production workflow covering sprites, animation, physics, audio, UI, profiling, and publishing. It is a credible alternative, not excluded because it cannot make this game. [Unity manual](https://docs.unity3d.com/6000.1/Documentation/Manual/2d-game-creation-wokflow.html)

13. **Aseprite automation.** The documented CLI can export sheets and data in batch operations, supporting reproducible sprite imports. Tool licensing and availability must be checked before choosing it. [Aseprite CLI](https://www.aseprite.org/docs/cli/)

14. **Accessibility.** The baseline recommendations include remapping, clear text, visual alternatives to sound, and alternatives to color-only information. The proposed rarity challenge should not depend on unreadable cues or inaccessible input. [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/basic/)

15. **Existing title.** Revival Productions has a released game titled Overload. Retain the project's working name for now, but resolve public naming before commissioning store branding. [Official Steam listing](https://store.steampowered.com/app/448850/Overload/)

16. **Large integer arithmetic.** .NET's BigInteger represents arbitrarily large signed integers. Use it for growing progression counters and exact scaled combat quantities, with explicit serialization and performance tests. Its availability removes fixed-width overflow as a necessary gameplay limit; it does not remove finite memory or computation costs. [Microsoft BigInteger reference](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger?view=net-10.0)

The Path of Exile 2 homepage required JavaScript in the available text reader, so the official forum patch notes were used instead. A Last Epoch homepage fetch failed; no design claim in this plan depends on that page. The research does not claim an exhaustive competitive audit.

## Alternatives considered

| Decision | Chosen proposal | Alternative | Why the proposal fits this scope |
| --- | --- | --- | --- |
| Engine | Godot .NET | Unity with C# | Both support the needed workflow; Godot suits a compact 2D desktop project, and the owner confirmed it |
| Rendering | 2D oblique top-down | True isometric geometry or 3D rendered as pixels | Keeps navigation, collisions, and asset iteration simpler while retaining a strong angled presentation |
| Multiplayer | Offline first | Online co-op from the start | Concentrates early effort on combat and the defining rules; sacrifices immediate social play |
| Overload shape | Typed contextual action selection | Transformation meter, extra talent tree, generic triggered buffs | Makes the programming analogy affect actual input behavior and build decisions |
| Override shape | One replaceable core contract | Ultimate ability with a long cooldown | Changes a persistent rule and creates a meaningful loss of ordinary behavior |
| Rarity | Guaranteed broad progression plus mastery | Extremely low drop rate, daily lockout, consumable lottery | Makes rarity understandable and earned while preventing unlucky players from losing all progress |
| Itemization | Few slots and affixes | Many currencies, sockets, conditionals, and crafting layers | Reserves cognitive complexity for action decisions |
| Progression | Uncapped level, Resonance, attunement, and Fracture tier | Finite progression endpoint | Required by the owner; formulas and repeatable authored combinations support it |
| Scope | Authored premium campaign and endless endgame | Live-service seasons and trade economy | Keeps production and operation manageable without limiting progression |

These are design judgments. They are not empirical findings from the cited documentation.

## Assumptions that still require evidence

The owner confirmed the technical starting point, but staffing, spending limits, preferred aesthetic references, release storefront, localization languages, minimum hardware, and available production time remain unspecified. None prevents a plan or the first greybox prototype. They should be resolved as their milestones approach.

The largest mechanical uncertainty is whether automatic context selection feels intentional. The M2 test must distinguish a player who deliberately spends Momentum from one who merely sees occasional bonus attacks. If that fails, reduce simultaneous contexts or change selection presentation before adding content.

The second uncertainty is whether losing the normal dodge makes Elsewhere exciting rather than frustrating. Test it early against the same encounters and offer a preview. Do not assume a clever analogy is a satisfying control scheme.

The economic uncertainty is the required time and willingness to traverse all four regions repeatedly. Fixed reward arithmetic provides predictability, but the experience depends on encounter variety and player proficiency. The 60–100 hour and 5–10% targets remain hypotheses with different denominators, not numbers derived from the reference games. Revalidate them against the new level-100 and tier-10 qualification requirements. The endless progression equations deliberately match balanced player growth with enemy growth at a reference level and grade; algebraic equality alone cannot establish playable difficulty or retention.

The production uncertainty is authored content throughput. Measure how long it takes to finish one hero's animation set, one polished enemy, one boss, and one room kit. Replace estimates with those measurements before committing to three Frames and four regions.

## Design validation experiments

1. Compare the basic arena with and without the first three Overloads using the same gear and enemy seeds. Observe deliberate setup, choice, and confusion.
2. Compare one versus three active binding rows and show exact next-action previews. Evaluate how often the outcome surprises the player.
3. Compare ordinary Traverse and Elsewhere on both a movement-heavy boss and a projectile-heavy boss. Seek different strengths rather than universal superiority.
4. Give players a realistic 12-minute expedition's loot, then time comparison and crafting decisions. Remove affixes if the simple system still consumes excessive attention.
5. Simulate ritual completion using observed activity durations and failure distributions; then run longer human tests across the actual regional content. Do not equate a spreadsheet's completion time with an enjoyable journey.
6. Add a new pattern through the intended content pipeline. If it requires editing many unrelated systems, improve the boundary before scaling to 18 patterns.
7. Run farming and pushing simulations using the same XP and scaling code as gameplay, then replace assumed run times and failure rates with observed distributions. Check whether low-tier farming or a specific allocation makes all other progression irrational.
8. Play a two-hour chapter sequence containing a failed push, build adjustment, equipment attunement, next-tier clear, and route choice. Evaluate decisions and variety separately from numerical progression.
9. Cross levels and tiers far beyond the normal playtest range with synthetic profiles. Verify exact saves, continued rewards, readable combat timing, and a next tier after every frontier clear; do not mistake a finite test range for a cap.

Each experiment should record the build, hypothesis, participant or simulation scope, observed behavior, decision, and unresolved questions. Keep rejected designs in short decision notes only when they explain the chosen implementation.
