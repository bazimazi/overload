# ARPG combat and playability overhaul — 9 October 2026

Build: `arpg.2026-10-09`. Run **Play Overload.cmd** in either `C:\dev\github\bazimazi\overload` or `C:\dev\games\overload`. Keep the complete Windows runtime folder together. `artifacts/LATEST-ARPG.json` identifies the exact runtime, source hashes and checks.

## Play with familiar action RPG controls

Left click clear ground to walk. Hold left mouse to steer. Click an enemy to approach and attack; hold on that enemy to repeat attacks. A light ring and arrow identify the selected target. Click a discovered NPC, exit, waypoint or device to approach and interact. Click an equipment label to collect it when close enough. Movement respects the player's collision footprint and paths around scenery.

**Alt + left mouse** attacks toward your cursor while standing your ground. **WASD** and **right mouse** remain available for movement; keyboard or controller movement cancels a mouse order. On a controller, left stick moves, right stick aims and **RB** attacks. Input bindings can be changed in settings.

**Q / E / R** use the three equipped skills. Free basic attacks generate pressure while Focus regenerates. **Space** evades; **F** heals. Both defensive actions can immediately interrupt a basic attack. You retain 80% movement speed during basic swings, and travel 40% faster outside combat in adventure landscapes. Existing acceleration, braking, sprite interpolation and camera smoothing remain active.

Begin at Hearth, click Mara and accept the patrol quest. Take the west road, clear the patrol, collect Mara's Weapon and equip it with **I**. **K** trains skills and talents; **J** shows the current quest. **Tab** opens the local map; **M** opens the world atlas. Follow the gold route through the campaign's four regions, restoring two guarded conduits in each dungeon before its ruler. Each region has an enforcer and a ruler.

**P** channels a town portal for three seconds. Movement, attacking or damage interrupts it. Hearth restores supplies, offers forging and salvage, and lets you change difficulty. Press P in Hearth to return to your departure point. The journal offers repeating frontier contracts after the introductory training. The frontier opens new generated reaches rather than reusing a single combat room.

## Combat and equipment have stronger consequences

Adventure patrols now contain four or six monsters with complementary melee and ranged roles. Ordinary monsters have 65% of their former Life and 60% of their former damage; the larger packs create more targets and skill opportunities. Boss stats retain their authored balance. At most sixteen adventure enemies are alive at once, and a pack pays its fixed reward only after every defender falls.

Selected optional encounters have an elite leader with triple Life. **Volatile** marks a pulse around the leader; **Stormbound** marks your location. Both lock their position and show an amber circle for one second before striking. Move outside the circle or evade the contact. The leader's name, ring and hazard message distinguish it from ordinary monsters. These encounters guarantee Rare or better equipment.

The first enforcer guarantees a Legendary. Warden and Revenant receive Deathburst; Threadseer receives Split Volley. The first completed frontier contract provides a complementary power. Ordinary Legendary rewards can roll any of seven powers. Duplicate copies of the same power do not stack.

| Power | Gameplay effect |
| --- | --- |
| Reaving | Basic melee attacks reach 18 pixels farther and sweep 60 degrees wider, up to 180 degrees. Projectile basics pierce two more enemies with a wider collision radius. |
| Split Volley | Direct projectile attacks fire three fan bolts at 65% damage, sharing three times the original victim budget. An enemy takes one hit per volley. Secondary projectiles do not generate additional fans. |
| Deathburst | The first kill per player attack explodes within 78 pixels for 50% of that attack's rolled damage. Walls block hits. Secondary explosions cannot trigger more explosions or generate memories. |
| Fleet | Movement speed +6%. |
| Flow | Focus regeneration +4 per second. |
| Guard | Maximum Life +12%. |
| Tempo | Active skill cooldowns reduced by 10%. |

Item comparison puts basic damage, maximum Life and armor before/after values above the equipment cards and next to **Equip item**. In Hearth, **Salvage spare Common / Magic items** removes only unequipped, unlocked low-rarity gear and pays its materials once. Rare and Legendary items remain for individual review. Inventory sorts spare high-rarity upgrades first. Field menus pause combat and preserve resources when you change gear or skills.

Outdoor ground now uses regional dirt, grass, wetland, ruined stone and cracked earth rather than repeated indoor floor tiles. Roads, architecture and collision remain synchronized with the map.

Save schema 8 migrates schemas 1–7 while preserving exact gear, quests, ground loot, contracts and portal return points. Older builds reject these newer saves. Returning to title is explicitly labeled **Save and return to title**; use P to actually travel to Hearth.

## Reference and validation

The comparison uses the exploration, enemy roles, active combat, meaningful equipment and town-service loop described in Blizzard's [Diablo IV feature overview](https://news.blizzard.com/en-us/article/23189677/diablo-iv-feature-overview). Blizzard's [combat readability update](https://news.blizzard.com/en-us/article/23746639/diablo-iv-quarterly-updatedecember-2021) guides the warning colors and visual priority. These are design references, not a claim that Overload matches Diablo IV's production quality or current balance. Overload retains its original setting, Frames, art and memory mechanics.

`build/campaign.ps1` drives ordinary movement, basic attacks, affordable equipped skills, evades and flasks on a fresh Standard Warden. It completes all four regions and eight bosses, restores every conduit, selects the ending, unlocks the endgame, completes a frontier contract, equips its reward, opens a second reach and reloads the save. It spends only earned skill/talent points and uses gear actually collected. It neither grants extra Life nor directly kills enemies. Headless and rendered reports disclose combat time, hits, flasks, skills, Life remaining and legendary activations. Automation is not independent human testing.

`build/adventure.ps1` additionally covers all three Frames' starter quests and enforcers, loot-label travel, the earned Split Volley, field build edits and interrupted/completed portals. Both reviews check 960×540 menus with 125% text. The isolated combat regression separately tests full-circle collision, nonrecursive Deathburst damage and shared projectile victim limits. Protected geometry and stress fixtures remain separate from normal-stat combat.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/campaign.ps1 -Rendered -Exported
powershell -NoProfile -ExecutionPolicy Bypass -File build/adventure.ps1 -Rendered -Exported
powershell -NoProfile -ExecutionPolicy Bypass -File build/world.ps1 -Performance -Exported -Seconds 30
```

The final checks pass **283 domain tests**, **216/216 Frame cases** and **10,000 generated physical maps with no unreachable routes**. The standalone build has zero build warnings/errors and empty engine-suite error logs. Campaign review passes **432 headless / 443 rendered checks**; adventure review passes **197 / 150**. Movement passes **38 / 39**, and RPG interaction passes **10 in each mode**. The final equipped-item focus correction has later standalone adventure/UI coverage; the full rendered campaign used the same combat rules before that UI correction. `LATEST-ARPG.json` discloses both runtime hashes.

The final sixteen-actor frontier stress sample on Intel Arc 140T at 1280×720, Compatibility rendering, measures **p95 9.80 ms / p99 11.17 ms** across 3,436 frames over thirty measured seconds after ten seconds of warmup. It passes the 16.7 ms / 25 ms target. This isolated stress fixture has protected stats and measures local rendering performance, separately from normal-stat combat.

See the [rendered campaign preview](../artifacts/campaign/contact-sheet.png) and the [equipped-item comparison with larger text](../artifacts/adventure/11-worn-compare-960-large-text.png).

Final counters, the rendered preview and the local sixteen-actor performance sample are recorded in `LATEST-ARPG.json`. Independent human/controller acceptance, additional hardware, more encounter variety and production animation coverage remain open. This is an original playable ARPG development build; it does not establish Diablo IV's content scale or release polish.
