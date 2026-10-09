# Movement and animation — 9 October 2026

Launch **Play Overload.cmd** in the repository root, or `artifacts/windows/Overload.exe` with its complete runtime folder. The older `C:\dev\games\overload` launcher points to the same current build, and its exported runtime is refreshed too.

WASD and left-stick movement now accelerate and turn over a few physics ticks, then brake promptly when released. Diagonals retain the same top speed. Collision remains authoritative; walls stop evades and permit ordinary movement to slide along them. Standard evades start with more speed and ease toward the end while retaining their authored distance, duration, contact-evasion window and cooldown. Pursuit, Crossing and Elsewhere retain their swept traversal rules.

Right click still requests a path to a point. **Hold right mouse to steer continuously.** Paths use extra corner clearance where available, skip a bend only when the full body can safely take the shortcut, and brake into a precise destination. Keyboard or stick input cancels mouse travel. Map right-click walking continues to work.

All three Frames and enemies animate from actual distance traveled. Being blocked no longer plays walking in place. Walking uses a faster distance-based cadence, eased body bob and lean, and stable directional changes. Attacks ease through anticipation, strike and recovery; evades and hit reactions blend into the body pose. Attack aim remains committed to its accepted direction, and deliberate right-stick aiming remains available while moving. A 167 ms input buffer lets a deliberate skill press near the end of an attack carry into its next legal opening.

Sprites interpolate between the 60 Hz physics updates. The camera follows that same presentation position, with a small eased look ahead, instead of stepping ahead of the drawing. Pause freezes presentation and simulation. Teleports and zone entry reset the camera immediately. Cursor projection includes camera movement and impact offset, and scrolling cannot switch controller input to mouse aiming.

The existing painted sprite atlases remain the source art. The smoothing uses motion timing and procedural pose animation; it does not add newly drawn skeletal animation frames.

## Verification

Run these commands from the canonical workspace, `C:\dev\github\bazimazi\overload`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/motion.ps1 -Rendered -Exported
```

The domain suite now has **249 passing tests**, including speed normalization, response and braking, destination arrival, directional stability, continuous attack poses and exact evade range. The isolated movement fixture exercises real input, collision and camera behavior for every Frame, including cover navigation, wall sliding, held-mouse steering, action buffering, pause/resume and controller/camera separation. A 120 FPS rendered trace verifies multiple distinct sprite positions within individual physics ticks.

The final Windows export passes **38 headless / 39 rendered movement checks** and all existing exported regression suites: arena 126, endless 89, production 156, expansion 327, experience 19, world 857, identity 52, open world 36, and RPG interactions 10. Their error logs are empty. Content validation, 216 Frame model cases and 10,000 generated map routes pass too. The local twelve-actor frontier sample at 1280 × 720 on Intel Arc 140T measures **p95 9.02 ms / p99 9.64 ms** over 30 seconds after ten seconds of warmup. This is a local measurement, not a hardware-wide guarantee.

All **198 exported files** match between the two launch folders. The older folder's executable independently passes the same 38 headless movement checks.

Logs, JSON traces, still captures and a 90-frame sequence are under `artifacts/motion/` and `artifacts/motion-*.log`. [Animated gameplay preview](../artifacts/motion/gameplay-preview.gif) and [pose contact sheet](../artifacts/motion/contact-sheet.png) make the presentation reviewable. Automated fixtures establish timing and behavior; independent playtest feedback remains a separate acceptance step.

`artifacts/LATEST-MOVEMENT.json` identifies this build and its runtime hashes. Earlier dated manifests retain evidence for their own builds.
