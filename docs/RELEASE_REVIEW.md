# Candidate release review

This is a local review candidate. Human review, independent playtesting and a nominated hardware matrix are pending. No store submission, distribution announcement or public release is authorized by this document.

## Reviewer session

Record the archive SHA256, build manifest, machine/display/input device and duration. Use a fresh Standard character; training profiles are separate sandboxes. Complete the campaign, inspect an earned item, change bindings, retry a death, resume a checkpoint after quitting, and play an endgame session containing a failed push and a build adjustment. Record observed failures and why you continued or stopped. Follow `S06_FIRST_PLAYTEST.md` for the first unfamiliar-player session.

Review these items before signing off:

- Combat tells, action predictions and memory choices are understood without coaching; all three Frames offer useful alternatives.
- Keyboard/mouse and a physical controller support combat, long menus, pause, Back and retry. Unplug/reconnect and window-focus changes leave the game recoverable.
- Text size, contrast, reduced flashes, room hints and six audio channels remain usable at the chosen display size. Essential attack information is visible with audio muted.
- Fresh installation has level-1 Standard resources. Updating retains characters, exact progression, items and unfinished runs; a future-version save is preserved.
- Campaign difficulty/duration, ordinary gear and oath tradeoffs meet the intended player experience. Synthetic clear fixtures do not establish these results.
- Review original asset provenance and presentation. Current static SVG animation/room variants and the unchanged small item catalog are disclosed scope limits. Final art direction, store copy and store asset approval remain open.
- Read the runtime/performance evidence and the hardware table in `Q_RELEASE_VALIDATION.md`; nominate and test the reference PC and additional devices. No minimum hardware requirements are published.

Record reviewer/date/build, failures with reproduction steps, accepted scope cuts, and a clear approve/request-changes decision in a new file under `docs/playtests`. This checklist is not a signed approval.

## Installation and recovery

Unzip into a new writable folder and launch `windows/Overload.exe`. Keep the executable, `.pck` and runtime directory together. Development SDKs are unnecessary. The default renderer is Mobile/Vulkan; use `Overload.exe --rendering-method gl_compatibility --rendering-driver opengl3` to select Compatibility on another GPU. Compatibility missed the performance target on the development machine. To update, close the game and extract the next candidate into a new folder; launch it only after verifying its archive hash. User saves remain outside the installation directory. Retain the previous install and a copy of the entire character folder until the update is reviewed.

Normal characters live under Godot `user://characters`, beneath `%APPDATA%/Godot/app_userdata/<project name>`. `profile.cfg` selects the active character; `controls.cfg` and `presentation.cfg` hold settings. Test commands write only `user://tests/session-*` profiles. A save is a checksummed aggregate; never hand-edit its payload or copy individual counters between snapshots.

On load, the game checks the current file, verified temporary file and three backups, then recovers the newest complete revision. It preserves unsupported future-version files. If every snapshot is invalid, use the recovery menu to create a separate profile or restore a copied whole folder while the game is closed. Keep the original folder for investigation; do not overwrite it with an older snapshot while the game is running.

For a crash report, collect the candidate hash/build manifest, reproduction steps, chosen Frame/activity, save-version message and Godot logs. Include a **copy** of an affected character folder only when willing to share it. Remove personal filesystem paths from logs before sharing. Reports are local/manual; this build sends no remote analytics. The owner must name a support contact before public release.

## Expansion evidence gate (R02)

The earlier independent tester has been arranged; no report has been received. An expansion has not been selected. Record each observed gap, its reproduction/session evidence, how often it occurred, and whether a fix to existing content would address it. Then select one bounded expansion with a player-facing acceptance check. Additional rooms, mutations or oath choices require evidence of demand and a reviewed baseline.

Before shipping added content, run the archived-baseline update check and future-version preservation tests. Keep `fracture.v1/v2/v3` and `rooms.v1/v2` definitions immutable for active runs. New geometry or reward rules need a new version; resume old runs with their original seed, geometry and budget. Never reset high-level progression or replace it with a maximum level/tier.
