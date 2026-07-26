# Slice 004 - Build The Defensive Line

## Player Outcome

Select a worker, spend Supplies to place and complete either a Storehouse or Watchtower, use the
Storehouse as a shorter Supply drop-off, and watch the completed Watchtower automatically fire on
the nearest hostile formation in range.

## Acceptance

- [x] A selected worker exposes clickable House, Storehouse, and Watchtower commands with visible
      fixed hotkeys and the approved 1:2:3 relative Supply costs.
- [x] Storehouse and Watchtower placement uses the existing snapped, reachable, unoccupied,
      in-bounds, route-preserving construction path.
- [x] One assigned worker completes each building, resumes its prior gathering assignment, and can
      cancel unfinished construction for a full refund.
- [x] A completed Storehouse becomes an available drop-off and workers carrying Supplies choose it
      when it is closer than the Hisar.
- [x] A completed Watchtower automatically targets the nearest hostile formation in range, fires a
      visible deterministic projectile, and supports the defense without resolving a moving assault
      by itself.
- [x] Completed friendly buildings can be selected and deliberately demolished only after visible
      confirmation, with no refund.
- [x] Focused EditMode and PlayMode coverage protects building costs, placement, completion,
      cancellation, nearest drop-off selection, tower targeting, damage, and demolition.
- [x] The native macOS development player exercises both new buildings while preserving gathering,
      House construction, formation training, and counter combat.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

Enemy building construction, worker training, Storehouse or Watchtower rebuilding AI, enemy attacks
against structures, structure-focused formation orders, fog of war,
minimap, control groups, complete AI, victory or defeat, audio, and visual polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Gather enough Supplies, select one worker, and confirm House `[H]`, Storehouse `[R]`, and
   Watchtower `[T]` commands show costs of 100, 200, and 300.
3. Place a Storehouse near a cache; confirm snapped placement, invalid placement feedback,
   Supply spending, one-worker construction, cancellation with a full refund, and completion.
4. Send a worker to that cache and confirm its carried batch deposits at the nearer completed
   Storehouse rather than returning to the Hisar.
5. Place and complete a Watchtower near the road, then train Archers so the hostile Spearmen arrive.
6. Confirm the Watchtower selects the nearest hostile in range, fires visible projectiles, causes
   deterministic casualties, and does not prevent the existing Archer counter fight.
7. Select a completed building, click `DEMOLISH`, confirm the warning, then click `CONFIRM DEMOLISH`;
   confirm the building disappears and Supplies do not change.
8. Build a House, gather and deposit Supplies, train a formation, win the counter fight, and quit
   normally.
9. Inspect the player log for exceptions, missing references, assertion failures, and navigation
   errors.

## Evidence

- PR: [#8](https://github.com/BedirT/rts-game/pull/8)
- Verification: `make verify` passed at final reviewed HEAD
  `1bdc35999b87219974c5a1e44dabc6866fe49698` with 14 EditMode and 22 PlayMode tests,
  an ARM64 development build, headless smoke, graphical 1920x1080 smoke, and clean logs.
- Runtime proof: the exact-HEAD built player constructed and cancelled a Storehouse and Watchtower,
  deposited Supplies at the nearer Storehouse, used supporting Watchtower fire against a moving
  formation, deliberately demolished a completed building, and preserved gathering, House
  construction, formation training, and counter combat.
- Review: round 1 reported three blocking findings and required fixer run 1; round 2 reported no
  blocking findings at the final head.
- Merge: squash-merged as `45f5dabb529690d33fc78875ea55a2b7ff6cfd8e`; `make post-merge`
  passed on merged `main` with the same test, build, smoke, and log checks.
