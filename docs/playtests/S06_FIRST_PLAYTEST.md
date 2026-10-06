# S06 first independent playtest

Status: **awaiting independent tester**. The owner confirmed they will arrange someone unfamiliar with the game. Automated tests and screenshot reviews are implementation evidence; they do not establish comprehension, combat feel or the 30–45 minute target.

## Give the tester

Use the review archive identified by `artifacts/LATEST-CANDIDATE.json`. Extract it and run `windows/Overload.exe`. Keep the executable, `.pck`, and managed runtime directory together. The build includes .NET; no editor or development tools are required. Windows x64 is the tested target. This unsigned local test build has no installer or network account. The older `Overload-slice-windows.zip` remains a historical S06 build with an accelerated eight-room court.

Start at Hearth with a fresh level-1 Standard Warden and select **Begin the four-region campaign**. If an older profile is selected, use **Create separate Standard character** and **Begin Standard Warden** first. The Field guide, Controls, and Settings are available at Hearth and while paused. Use mouse wheel or D-pad/arrows to reach options below the visible menu area.

Default controls: WASD move; mouse aim; hold left click for Cleave; Q/E/R active skills; Space Traverse; F Flask; Escape pause. Controller: left/right sticks move/aim; RB Cleave; X/Y/B actives; A Traverse; LB Flask; Start pause; D-pad/A/B navigate/confirm/back.

The current campaign has sixteen checkpoints and eight bosses. Three Frames, equipment/forging, talents, inscriptions, nine shared signatures and two earned oaths are implemented. Use ordinary earned rewards for this session. Accelerated training builds are separate sandbox characters. Elsewhere can be practiced without payment; production qualification and regional Fractures are available later in progression. Human balance and completion duration remain unverified.

Room clears save rewards and the next checkpoint. Visiting Hearth or quitting preserves it. Life, Focus, flasks and temporary combat effects reset at checkpoint entry. Ordinary saves and settings live under `%APPDATA%\Godot\app_userdata\Overload — Combat Arena`. Keep that directory if a save issue occurs; do not edit the JSON during the test.

## Facilitator protocol

1. Record build label, device, resolution and ARPG familiarity. Ask the player to think aloud. Let them discover controls from the game before explaining mechanics.
2. Observe the first ten minutes without coaching. Record when and why help becomes necessary, menu dead ends, unintended actions, unreadable tells and each death.
3. Ask them to finish the campaign, return to Hearth, improve one item, choose a skill technique or talent, and change one binding. Record elapsed time rather than assuming the target duration. Record the checkpoint reached if they stop early.
4. Ask them to quit and resume between rooms; compare checkpoint, gear and wallets. If available, test controller unplug/reconnect, aiming, menus and retry on the physical device.
5. Ask them to try Elsewhere in simulation and explain what it replaces. Do not coach the answer. Record whether they understood placement has no dodge and Crossing is suspended.

## Feedback to return

- Tester alias, build label, Windows/GPU, resolution, input device and ARPG experience:
- First session length / rooms completed / boss attempts:
- First moment of confusion (exact action and what you expected):
- Any action that failed, fired unexpectedly, or was hard to aim:
- Any text, warning, enemy or HUD element that was difficult to read:
- In your words, what earns Momentum and Echo? What spends them?
- In your words, how does a pattern differ from Elsewhere?
- Did placement losing the normal dodge feel understandable? Did the practice help?
- Which skill or binding change felt worthwhile? Which felt pointless?
- Did crafting, room rewards and quit/resume behave as expected?
- Most enjoyable moment / most frustrating moment:
- Reproduction steps and optional screenshot/video for each blocker:

## Acceptance and follow-up

No results have been entered yet. S06 remains open until the first independent session is recorded and observed control/comprehension failures are addressed, with a repeat check when needed. The broader M4 gate still calls for roughly 10–15 players, tutorial and mechanic comprehension rates, representative completion time, physical-controller coverage and performance evidence. Do not mark that broader gate passed after one session.
