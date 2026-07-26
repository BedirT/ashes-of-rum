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
- [x] Local evidence is keyed to the exact commit and recorded in the PR.
- [ ] A context-free review covers the exact final PR HEAD.
- [ ] The reviewed PR is squash-merged and post-merge proof passes on `main`.

## Non-Goals

All gameplay, including the battlefield, factions, camera controls, units, selection,
economy, construction, combat, AI, fog, minimap, UI, audio, and final visuals.

## Automated Verification

1. Run `make verify` on a clean committed branch.
2. Confirm both test reports pass and the native ARM64 development player is built.
3. Confirm deterministic headless smoke passes and exits itself.
4. Confirm the normal graphical player launches at 1920x1080, renders the neutral scene to
   `graphical-smoke.png`, and exits itself.
5. Parse `smoke-result.json`, `graphical-smoke-result.json`, and `summary.json` under the SHA
   evidence path and inspect the associated logs.

## Hands-On Play Check

1. Launch the exact-HEAD `.app` without `-batchmode` or `-nographics`.
2. Observe the neutral ground, camera, and lighting in a normal 1920x1080 game window.
3. Confirm there is no gameplay or player HUD in this harness-only slice.
4. Close the window and inspect the graphical player log for new runtime errors.

## Evidence

Completed by the PR lifecycle. Exact SHA-keyed artifacts live under ignored
`.artifacts/verification/<sha>/`. The PR records the exact SHA and local command results;
hosted CI and commit-status publication are intentionally excluded.
