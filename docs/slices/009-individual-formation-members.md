# Slice 009 - Give Soldiers Battlefield Presence

## Player Outcome

Command an eight-soldier formation as one unit while its individual soldiers navigate around
obstacles and one another, turn and fight at their own positions, receive visibly connected hits,
die individually, and naturally regroup into the formation afterward.

## Acceptance

- [x] One click, control-group entry, and order still select and command the complete formation;
      individual soldiers cannot be selected or ordered separately.
- [x] Each living soldier owns a persistent world position, facing, health value, movement path,
      and formation slot rather than inheriting every anchor translation and rotation rigidly.
- [x] Soldiers follow individual NavMesh paths toward their assigned slots, separate around an
      obstacle or neighboring soldier when needed, and close ranks smoothly without teleporting.
- [x] Every Archer projectile visibly tracks one deterministic living enemy soldier and damages
      only that soldier on arrival rather than resolving against the formation center.
- [x] Melee soldiers close on and damage individual reachable enemy soldiers while the formation
      remains the combat command and counter-resolution boundary.
- [x] A lethal hit removes only its targeted soldier at that soldier's position, immediately frees
      one population, and causes survivors to close ranks smoothly.
- [x] Existing counter modifiers, member-count combat output, projectile timing, formation facing,
      flank bonuses, frontline blocking, fog, health bars, AI orders, and structure combat remain
      deterministic and playable.
- [x] Focused EditMode and PlayMode coverage protects member slot assignment, individual movement,
      projectile targeting, member-specific damage, casualty position, and regrouping.
- [x] The native macOS player visibly exercises obstacle separation, member-targeted Archer fire,
      an individual casualty, and survivor regrouping while preserving the complete match loop.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

Individual soldier selection or commands, reinforcement or formation merging, selectable shapes,
manual facing, siege units, airborne launch or knockback physics, ragdolls, skeletal animation,
terrain-derived combat modifiers, or production-art polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Train a formation and move it past a building or road obstacle; confirm members take distinct
   paths and rejoin the four-wide, two-deep block without snapping or becoming stuck.
3. Order the formation through a turn; confirm soldiers face and flow through the turn individually
   rather than rotating as one rigid visual block.
4. Engage Cavalry with Archers and confirm each arrow visibly follows a soldier, lands on that
   soldier, and produces damage or death only there.
5. Observe a casualty and confirm it disappears at its own battlefield position while the remaining
   soldiers move naturally into the open slot and population falls by one.
6. Re-check counter combat, front/side/rear damage, direct frontline blocking, lateral release,
   fog loss, AI attacks, Hisar result, Restart, and Quit.
7. Inspect the player log for exceptions, missing references, assertion failures, navigation errors,
   or soldiers stranded away from their living formation.

## Evidence

- Gameplay PR: [#18](https://github.com/BedirT/rts-game/pull/18)
- Final reviewed head: `a64741d57b11b98a626083b8a6d2de968dd67c0a`
- Verification: 26/26 EditMode tests, 76/76 PlayMode tests, native ARM64 development build,
  45/45 headless smoke checks, and 48/48 graphical smoke checks passed at the final head.
- Review: round 3 reported zero blocking findings after two fixer runs.
- Merge: squash commit `6e68327ce02b61cb454814cb4392cd32159167ba`
- Merged-main proof: `make post-merge` passed at `6e68327ce02b61cb454814cb4392cd32159167ba`;
  SHA-keyed evidence is stored under `.artifacts/verification/6e68327ce02b61cb454814cb4392cd32159167ba/`.
