# Slice 002 - Build And Expand Population

## Player Outcome

Select a worker, spend Supplies to place a snapped House on reachable ground, watch that worker
construct it, and see population capacity increase from 12 to 20 when the House completes.

## Acceptance

- [ ] A selected worker exposes a clickable `Build House` command and the visible `H` hotkey.
- [ ] House placement snaps to the grid and rejects occupied, unreachable, out-of-bounds, or
      route-blocking positions without spending Supplies.
- [ ] Valid placement spends the full House cost, assigns one worker, and gives clear placement
      and construction feedback.
- [ ] Cancelling unfinished construction with the clickable command or `X` refunds the full cost.
- [ ] A completed House raises population capacity by 8, gives no refund, and the worker resumes
      its previous valid gathering assignment or becomes idle.
- [ ] Focused EditMode and PlayMode coverage protects spending, population, placement,
      construction, cancellation, and worker resumption.
- [ ] The native macOS development player builds a House during smoke while preserving the
      existing gather-deposit path.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

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

Pending implementation, exact-HEAD verification, context-free review, and merge.
