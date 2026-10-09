# Adventure gameplay overhaul — 9 October 2026

Build: `adventure.2026-10-09`. Launch **Play Overload.cmd** from the repository root. The complete Windows runtime is in `artifacts/windows`; `artifacts/LATEST-ADVENTURE.json` records the source, runtime hashes and verification evidence. The older `C:\dev\games\overload` launcher receives the same verified runtime.

## Play a complete adventure loop

1. Begin in safe Hearth Junction. Follow the gold minimap marker to **Mara** and press **G** to accept the first quest.
2. Take the west road to Cinderroad. Clear the first patrol using your free basic attack and active skills. This battle awards **1,100 total XP, 75 gold and a guaranteed Rare weapon** on a fresh character, reaching level 2.
3. Press **G** near the glowing weapon, or click its label to walk over and collect it. Open **I**, select it, compare the stats, and use the prominent **Equip item** action.
4. Open **K** and spend your first skill point. Character, inventory, skills and talents can be used while exploring; their menus pause combat. Equipment and training changes preserve current Life, Focus, flasks, cooldowns and the action clock.
5. Follow the gold route to the Ash enforcer. **Amber shapes warn of attacks and floor hazards; teal marks recovery or broken guard. Space evades and F heals.** Basic attacks cost no Focus; Focus regenerates automatically. Boss floor hazards stop when the boss dies.
6. Press **P** to channel a town portal for three seconds. Movement, attacks and damage cancel the channel. Hearth restores supplies and offers forging and salvage in the inventory. Press **P** in Hearth to return to the saved departure position.
7. Continue the campaign by restoring both dungeon conduits and defeating the regional ruler. Quest text tracks conduit counts, boss objectives and restored regions. **J** opens the complete journal.

The local map (**Tab**), world atlas (**M**) and minimap remain available. NPCs and discovered equipment drops appear among map targets. Keyboard/mouse movement and aiming are separate; a loot or menu click is suppressed until release so it cannot accidentally start an attack.

## Repeatable frontier contracts

After the first skill training, open the journal in Hearth or speak to Mara. A Trailkeeper caravan takes you to a fresh generated reach. Defeat three marked patrols along the central road, clear the camp's guards, and activate Trailkeeper Camp. The tracker reports four completed objectives and routes each step.

Return to Hearth and **Claim legendary contract reward** in the journal. Each completed survey grants a Legendary item, XP, 100+ gold and 10 Alloy. A completed contract pays once; the next contract starts in a new reach. Partial objectives survive saves. Abandoning a contract grants no reward. Unclaimed rewards remain available when the inventory is full.

Legendary powers have readable effects:

| Power | Effect |
| --- | --- |
| Fleet | Movement speed +6% |
| Flow | Focus regeneration +4 per second |
| Guard | Maximum Life +12% |
| Tempo | Active skill cooldowns reduced by 10% |

Duplicate copies of the same power do not stack. Compare an item before equipping it: its ordinary stats and power both matter. Contract rewards are delivered to inventory; encounter gear appears physically on the ground.

Story, Adventurer and Veteran difficulty can be chosen in Hearth's journal. Story reduces enemy and hazard damage and lengthens enemy warnings. Adventurer preserves the standard balance. Veteran increases enemy health and damage. XP and rewards are the same at every difficulty.

## Persistence and compatibility

Save schema 7 preserves exact progression, campaign claims, inventory and equipped gear from schemas 1–6. Existing adventurers skip the introductory training sequence; level-1 characters who only rested at the Hearth waypoint still receive the first quest. Unknown future schemas or adventure versions stop loading rather than overwriting newer files.

Encounter receipts own their rewards before pickup. Drops retain their zone, original position and item identity across travel and reload. Claimed encounters and repeated pickups cannot duplicate XP, currency or equipment. Pending loot is bounded to 64 entries, with room reserved for the first quest weapon; excess ordinary rewards convert to gold and Alloy. Inventory holds 36 items. Survey history remains bounded as described in [the open-world overhaul](OPEN_WORLD_OVERHAUL.md).

## Comparison and verification

The benchmark is Diablo IV's exploration–combat–loot–build loop, connected wilderness and town services. Blizzard describes those pillars in its [feature overview](https://news.blizzard.com/en-us/article/23189677/diablo-iv-feature-overview); its [combat readability update](https://news.blizzard.com/en-us/article/23746639/diablo-iv-quarterly-updatedecember-2021) emphasizes clear enemy warnings, distinct effects and visual priority. Those sources guide the comparison; they do not certify this game's quality. Overload retains its original world, characters, artwork, memory mechanics and three Frames.

`AdventureReview` drives normal movement, basic attacks, equipped skills, healing and evades with ordinary character and enemy stats. It fights the first patrol and enforcer with all three Frames, exercises field equipment and training, tests portal interruption and return, completes a frontier contract, equips its Legendary reward and opens a second reach. It also checks loot-label movement/pickup, reloads, a paused journal, and 960×540 comparison navigation with 125% text. It never grants extra Life or kills enemies directly. Combat reports disclose elapsed simulation time, hits, flask use and remaining Life.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/adventure.ps1 -Rendered -Exported
powershell -NoProfile -ExecutionPolicy Bypass -File build/world.ps1 -Performance -Exported -Seconds 30
```

Review artifacts are under `artifacts/adventure`; final counters and local performance results are in `LATEST-ADVENTURE.json`. Earlier route suites intentionally clear defenders and use protected fixture characters to check geometry and persistence. They do not measure combat difficulty; the new adventure review does.

The final source pass has 270 passing domain tests, 216/216 Frame cases and 10,000 generated physical maps with no unreachable routes. Exported regression suites pass with empty error logs. The adventure review passes 179 headless and 149 rendered checks, movement passes 38 headless and 39 rendered checks, and RPG interaction passes ten checks in each mode. The rendered review fights the Warden enforcer and completes its contract; the headless review fights all three Frames' enforcers. See the [rendered preview](../artifacts/adventure/contact-sheet.png).

The final local performance sample uses twelve active frontier enemies, 1280×720 Compatibility rendering and Intel Arc 140T. Over 30 measured seconds after ten seconds of warmup, p95 is 11.14 ms and p99 is 14.78 ms, passing the 16.7 ms / 25 ms target. This is one local stress sample with the adventure HUD active.

This is a playable ARPG prototype with a substantially stronger core loop. Independent human comprehension and controller testing, full-campaign pacing, more encounter/quest variety, production animation coverage and wider hardware validation remain open. Automated runs and local rendered checks do not establish Diablo IV's content scale or release polish.
