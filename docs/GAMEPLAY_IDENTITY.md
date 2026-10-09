# Gameplay and identity pass — 8 October 2026

The owner's feedback was that combat felt generic, the world lacked character, Overload/Override were hard to experience, and the UI was too basic. This pass changes playable behavior and presentation. It does not certify player enjoyment or a world-class quality bar.

## Play this build

Launch `artifacts/windows/Overload.exe` in `C:\dev\github\bazimazi\overload`. At Hearth choose **Learn to rewrite**. The Memory Chamber uses real movement, hits, memory consumption, Crossing and Elsewhere placement/return across all three Frames. It temporarily supplies three bindings, grants no rewards or oath ownership, and restores the previous combat profile on exit. The character snapshot remains unchanged.

Then choose **Explore the world**. Hold **Shift / left trigger** while acting to preserve memories and execute the base action. Release it to spend a memory through an equipped binding. Override still governs the base contract. **Tab / View** opens a tactical map and stops simulation time; Escape/Back closes it. Click a revealed landmark to track it, or use **T / D-pad Left** to cycle known landmarks. **M / D-pad Right** opens regional travel.

## Delivered behavior

- Memory preservation is part of the authority-issued intent and snapshot. Prediction, revalidation and commitment share the selection rule. Normal costs, cooldowns, geometry, oath payment and anchor phases still apply.
- Practice observes actual base-hit provenance, walking-generated Momentum, selected Pursuit/Crossing and real anchor phases. It does not advance by a timer or grant memories. Retry and exit restore the ordinary character contract.
- A compact combat deck replaces the full-width bottom panel and permanent memory header boxes. Life/Focus reservoirs, Frame-specific glyphs, selected signature names, memory lifetimes, Strain, boss openings and dispatch announcements give each part a distinct role. Tooltips disclose costs and fallback reasons. Instructions wrap at smaller/enlarged text settings.
- A fog-aware minimap shows nearby revealed geometry, landmarks and defenders. The destination arrow follows the next navigation bend; distance is straight-line meters. The local map draws only revealed route segments. Tracking is session-local and creates no save schema changes.
- World packs remain idle until a member is damaged or has sight of the player within 210 pixels. The existing three-windup limit still applies. Memories and combat audio follow engaged hostiles; long-distance pack retirement remains intact.
- Road surfaces follow cached footprint-aware navigation around actual terrain. Planked gantries, quieter service routes, signposts, raised structure faces, existing pixel props, regional ground detail and refuge shelters clarify the geography. Decoration adds no collision or rewards.
- Fixed a swept-query bug: `IntersectShape` checks the stationary origin before `CastMotion` tests travel. Crossing aimed beyond a wall now advances to a safe clipped endpoint instead of rejecting the entire move.

## Verification

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/test.ps1 -Engine
powershell -NoProfile -ExecutionPolicy Bypass -File build/export.ps1 -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File build/identity.ps1 -Exported -Rendered
powershell -NoProfile -ExecutionPolicy Bypass -File build/quality.ps1 -Mode Matrix -Exported
powershell -NoProfile -ExecutionPolicy Bypass -File build/world.ps1 -Exported -Review
```

The identity suite exercises all three Standard Frames through practice with real input. It checks the entire unchanged save, contract restoration, clipped Crossing, hidden-landmark rejection, map clock freezing and world engagement. Pure resolver tests cover preservation costs/tokens/read-only prediction and attempts to bypass blocked geometry or Elsewhere. Existing domain/content, campaign, endgame, compatibility and engine tests remain required.

Rendered identity review produces 39 captures, including combat/local maps at 960×540, 1280×720 and 1920×1080 with 100%/125% text, plus gantries, reclaimed refuges and bosses in all four regions. It verifies that painted roads have real segments and enough navigable width. The existing menu matrix covers 80 cases. Evidence is under `artifacts/identity*`, `artifacts/identity`, `artifacts/world` and `artifacts/quality`; `artifacts/LATEST-IDENTITY.json` identifies the verified runtime. Earlier release/world manifests remain historical.

## Remaining quality work

This improves agency, comprehension, navigation and scene readability. The campaign still has seventeen authored zones, finite enemy behaviors and existing generated poses. Human combat/route playtests, stronger encounter set pieces, denser authored environments and artist-directed animation are needed before a commercial quality claim is justified. Physical-controller feel and broad hardware acceptance remain open.
