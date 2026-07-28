# Slice 010 - Unblock Formation Slots

## Player Outcome

Move or fight near a structure without individual soldiers freezing around its footprint. A soldier
whose ideal formation slot is carved out of the NavMesh settles at the nearest reachable position,
keeps trying as the formation moves, and returns to its exact slot once open ground is available.

## Acceptance

- [x] A member whose ideal slot overlaps a live carved structure chooses a complete reachable path
      to nearby walkable ground instead of stopping indefinitely.
- [x] The member considers the reachable projected point arrived rather than continually pursuing
      the invalid point inside the obstacle.
- [x] All member movement remains constrained to the NavMesh and outside the structure footprint.
- [x] After the formation anchor moves clear of the structure, every survivor reforms into its
      exact four-wide, two-deep slot without teleporting.
- [x] Existing individual obstacle detours, member targeting, casualties, formation commands, and
      the complete match remain playable.
- [x] Focused PlayMode coverage reproduces the obstructed-slot stall and protects recovery.
- [x] The built macOS player exercises member navigation and the complete match path.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

New formation shapes, individual selection, structure avoidance for non-formation actors, combat
tuning, siege knockback, animation, or changes to building placement rules.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Order a formation beside an enemy or friendly structure so part of its four-wide block would
   overlap the structure footprint.
3. Confirm every soldier continues around the structure and settles on nearby walkable ground;
   none remains stranded well away from both the formation and the obstacle boundary.
4. Move the formation into open terrain and confirm all living soldiers smoothly regain their
   exact compact slots.
5. Complete the normal combat and match-result path, then inspect logs for navigation errors,
   exceptions, assertions, or missing references.

## Evidence

- Gameplay PR: [#20](https://github.com/BedirT/rts-game/pull/20)
- Final reviewed head: `278f160585a9fe78b11b8ced03732c36093ebc4d`
- Verification: 26/26 EditMode tests, 79/79 PlayMode tests, native ARM64 development build,
  46/46 headless smoke checks, and 49/49 graphical smoke checks passed at the final head.
- Runtime proof: both smoke modes carved an ideal member slot, proved same-side reachable settling,
  removed the obstruction, and proved exact regrouping; focused tests also covered cumulative slot
  drift, swept obstacle safety, speed bounds, and separation-pressure progress.
- Review: round 3 reported zero blocking findings after two fixer runs.
- Merge: squash commit `e3148e2aa9c530d55a7e214b66cf8b8f98f0f063`
- Merged-main proof: `make post-merge` passed at `e3148e2aa9c530d55a7e214b66cf8b8f98f0f063`;
  SHA-keyed evidence is stored under `.artifacts/verification/e3148e2aa9c530d55a7e214b66cf8b8f98f0f063/`.
