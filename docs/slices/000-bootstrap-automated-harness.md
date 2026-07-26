# Slice 000 - Bootstrap Automated Harness

## Player Outcome

Import the Unity project, run its test suites, build a native macOS player, launch a
neutral bootstrap scene, validate its required objects, and exit automatically with a
machine-readable result. This is project and delivery infrastructure, not gameplay.

## Acceptance

- [x] The approved Unity 6000.5.5f1 URP project exists in the repository.
- [x] A neutral bootstrap scene opens without gameplay content.
- [x] EditMode and PlayMode tests protect harness and scene contracts.
- [x] A native macOS build runs a deterministic scene smoke and exits itself.
- [x] Evidence is keyed to the exact commit and published as `unity-local/verify`.
- [ ] A context-free review covers the exact final PR HEAD.
- [ ] The reviewed PR is squash-merged and post-merge proof passes on `main`.

## Non-Goals

All gameplay, including the battlefield, factions, camera controls, units, selection,
economy, construction, combat, AI, fog, minimap, UI, audio, and final visuals.

## Manual Play Check

1. Run `make verify` on a clean committed branch.
2. Confirm both test reports pass and the native player is built.
3. Confirm the player launches the neutral bootstrap scene and exits itself.
4. Inspect `smoke-result.json`, `player.log`, and `summary.json` under the SHA evidence path.

## Evidence

Completed by the PR lifecycle. Exact SHA-keyed artifacts live under ignored
`.artifacts/verification/<sha>/` and the corresponding commit status is published to
GitHub.
