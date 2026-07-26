# Slice 005 - Contest The Road

## Player Outcome

Train a visibly faster Cavalry formation, combine it with other formations in a numbered control
group, preserve the group's rough layout while moving or attack-moving into the central road, and
use three-state fog plus the minimap to understand which hostile forces are currently known.

## Acceptance

- [ ] Cavalry trains through the existing shared Hisar queue, remains an eight-member formation,
      moves clearly faster than foot formations, and wins its approved counter fight against Archers.
- [ ] Drag and Shift selection can hold multiple friendly formations, while double-click selects
      visible friendly formations of the same type.
- [ ] `Ctrl+1` through `Ctrl+9` assigns the selected workers and formations to a control group;
      `1` through `9` recalls the surviving members with visible confirmation.
- [ ] Contextual move and focus orders apply to every selected formation, group movement preserves
      rough relative layout, `Attack-Move [F]` acquires the nearest currently visible hostile, and
      `Stop [G]` visibly halts the group without conflicting with WASD camera input.
- [ ] The battlefield exposes unexplored, explored, and currently visible fog states using one
      shared sight radius; explored ground remains known and hostile mobile formations disappear
      immediately outside current vision.
- [ ] Hostile static-target memory supports dim stale silhouettes at the last known state without
      making them currently targetable.
- [ ] A lower-corner minimap shows explored terrain, friendly markers, and only currently visible
      hostile mobile markers; clicking explored minimap ground recenters the bounded RTS camera.
- [ ] Focused EditMode and PlayMode coverage protects fog transitions, Cavalry speed and countering,
      group layout, control-group recall, attack-move visibility, Stop, minimap filtering, and camera
      navigation.
- [ ] The native macOS development player trains Cavalry, creates and recalls a multi-formation
      group, contests the revealed road, loses contact under fog, and preserves every prior economy,
      building, Watchtower, training, and counter-combat path.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

## Non-Goals

Enemy economy or construction, production AI, Hisar damage or match outcomes, complete scripted AI
phases, worker training, formation-to-building combat, under-attack pings, match telemetry, audio,
Restart or Quit result actions, 10-15 minute balance, and visual polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Confirm most of the road begins unexplored, friendly units reveal nearby ground, explored ground
   remains dim after units leave, and hostile formations render only in current vision.
3. Gather Supplies, build the required House, train Archers, then gather and train Cavalry.
4. Drag-select both formations, assign them with `Ctrl+1`, clear selection, and recall with `1`.
5. Right-click the road and confirm both formations move while preserving their relative spacing;
   press `G` and confirm both stop.
6. Use `Attack-Move [F]` toward the visible hostile Archers, confirm the group acquires them, and
   confirm Cavalry movement is clearly faster and Cavalry wins the direct counter fight.
7. Move away until surviving hostile mobile units leave vision and disappear from both battlefield
   and minimap while the explored road remains visible.
8. Click explored minimap ground and confirm the camera recenters without leaving its map bounds.
9. Re-check gathering, House, Storehouse, Watchtower, demolition, formation training, and the Archer
   versus Spearman counter path, then quit normally.
10. Inspect the player log for exceptions, missing references, assertion failures, and navigation
    errors.

## Evidence

Record the PR, final reviewed HEAD, verification summary, review round, fixer count, and merge result
before closing the slice.
