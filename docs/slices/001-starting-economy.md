# Slice 001 - Establish And Command The Starting Economy

## Player Outcome

Launch directly into The Sundered Road with one Karasungur Hisar, four workers, and finite
caravan caches. Select workers, issue visible move and gather orders, and watch carried
Supplies return to the Hisar and increase the HUD total.

## Acceptance

- [ ] A clean launch creates one blue Hisar, four friendly workers, and finite Supply caches.
- [ ] Click or drag-select workers, with Shift modification and visible selection feedback.
- [ ] Right-clicking terrain moves selected workers through NavMesh navigation.
- [ ] Right-clicking a cache gathers a fixed batch, carries it to the Hisar, deposits it, and
      automatically returns while the cache has Supplies.
- [ ] The Canvas HUD shows Supplies, selected-worker state, controls, and visible order feedback.
- [ ] Focused EditMode and PlayMode coverage protect economy arithmetic and the gather-deposit loop.
- [ ] The native macOS development player exercises movement and a real deposit during smoke.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

## Non-Goals

Worker production, Houses, Storehouses, construction, population spending, military formations,
combat, enemy AI, fog of war, minimap, audio, match completion, and final visual polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Confirm one blue Hisar, four workers, two caravan caches, the Supplies HUD, and control hints.
3. Click one worker and drag-select several workers; confirm blue selection rings and the selection panel.
4. Right-click open terrain; confirm an order marker, responsive movement, and updated worker state.
5. Right-click a caravan cache; confirm workers travel to it, visibly carry a batch home, deposit,
   increase the Supplies total, and return automatically.
6. Pan with WASD, edge pan, or middle-mouse drag and zoom with the mouse wheel.
7. Quit normally and inspect the player log for new runtime errors.

## Evidence

Record the PR, final reviewed HEAD, verification summary, review round, fixer count, and merge
result here before closing the slice.
