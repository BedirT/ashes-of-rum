# Slice 000 - Bootstrap Playable Harness

## Player Outcome

Launch a native macOS build directly into the graybox Sundered Road battlefield,
inspect the blue Karasungur and red Alazhan Hisars, pan and zoom the bounded RTS
camera, read the control HUD, and quit with Escape or the HUD button.

## Acceptance

- [x] The approved Unity 6000.5.5f1 URP project exists in the repository.
- [x] Sundered Road opens on launch with both factions represented.
- [x] Keyboard, edge, middle-drag, and wheel camera controls are implemented.
- [x] EditMode and PlayMode tests protect camera and scene contracts.
- [ ] A native macOS build runs a deterministic scene smoke and exits itself.
- [ ] Evidence is keyed to the exact commit and published as `unity-local/verify`.
- [ ] A context-free review covers the exact final PR HEAD.
- [ ] The reviewed PR is squash-merged and post-merge proof passes on `main`.

## Non-Goals

Units, selection, commands, economy, construction, combat, AI, fog, minimap, final
visuals, and final audio are intentionally deferred to later vertical slices.

## Manual Play Check

1. Run the built `Ashes of Rum.app`.
2. Confirm the battlefield, two colored Hisars, road, obstacles, and HUD appear.
3. Pan with WASD and screen edges, drag with middle mouse, and zoom with the wheel.
4. Confirm camera movement remains bounded and rotation is unavailable.
5. Quit with Escape, relaunch, then quit using the HUD button.

## Evidence

Completed by the PR lifecycle. Exact SHA-keyed artifacts live under ignored
`.artifacts/verification/<sha>/` and the corresponding commit status is published to
GitHub.
