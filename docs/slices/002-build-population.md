# Slice 002 - Build And Expand Population

## Player Outcome

Select a worker, spend Supplies to place a snapped House on reachable ground, watch that worker
construct it, and see population capacity increase from 12 to 20 when the House completes.

## Acceptance

- [x] A selected worker exposes a clickable `Build House` command and the visible `H` hotkey.
- [x] House placement snaps to the grid and rejects occupied, unreachable, out-of-bounds, or
      route-blocking positions without spending Supplies.
- [x] Valid placement spends the full House cost, assigns one worker, and gives clear placement
      and construction feedback.
- [x] Cancelling unfinished construction with the clickable command or `X` refunds the full cost.
- [x] A completed House raises population capacity by 8, gives no refund, and the worker resumes
      its previous valid gathering assignment or becomes idle.
- [x] Focused EditMode and PlayMode coverage protects spending, population, placement,
      construction, cancellation, and worker resumption.
- [x] The native macOS development player builds a House during smoke while preserving the
      existing gather-deposit path.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

Worker production, Storehouses, Watchtowers, completed-building demolition, multiple builders,
military formations, combat, enemy AI, fog of war, minimap, and visual polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Confirm the HUD shows `SUPPLIES 100` and `POPULATION 4 / 12`.
3. Select a worker and click `BUILD HOUSE` or press `H`.
4. Move the placement preview over occupied and open ground; confirm invalid and valid feedback.
5. Left-click valid ground; confirm Supplies fall to 0 and the assigned worker constructs the House.
6. During construction, click `CANCEL BUILD` or press `X`; confirm the House disappears and all
   100 Supplies return.
7. Place the House again and let it complete; confirm population capacity rises from 12 to 20.
8. Confirm a worker that had been gathering returns to its cache after completing construction.
9. Gather and deposit Supplies, then quit normally and inspect the player log for new runtime errors.

## Evidence

- PR: [#4](https://github.com/BedirT/rts-game/pull/4)
- Verification: `make verify` passed at final reviewed HEAD
  `f10b5b8e9bd43c3cb08b152425354453d41c378b` with 7 EditMode and 13 PlayMode tests,
  an ARM64 development build, headless smoke, graphical 1920x1080 smoke, and clean logs.
- Runtime proof: the built player gathered and deposited Supplies, spent 100 Supplies, completed
  one House, raised population capacity from 12 to 20, and resumed the worker's gathering assignment.
- Review: round 1 required fixer run 1; round 2 reported no blocking findings at the final head.
- Merge: squash-merged as `dc5ea0a962bbd374c21032eb86881eb3c68017ae`; `make post-merge`
  passed on merged `main`.
