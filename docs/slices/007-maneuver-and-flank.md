# Slice 007 - Maneuver And Flank

## Player Outcome

Use movement and attack-move orders to approach an enemy formation from the side or rear, read
both formations' facing and turn state, and gain a modest deterministic damage advantage before
the defender finishes reorienting toward the attacker.

## Acceptance

- [ ] Moving formations face their travel direction and display a readable front indicator.
- [ ] A formation in combat turns toward its target over one short fixed duration and cannot
      attack until it is facing that target.
- [ ] Formation attacks are classified deterministically as front, side, or rear from the
      defender's facing when each melee hit or Archer projectile lands.
- [ ] Side and rear hits apply tunable modest bonuses that compose with the existing counter
      multiplier without changing worker or structure damage.
- [ ] Selection and order feedback identify a formation's current facing and visible turn state.
- [ ] Focused EditMode and PlayMode coverage protects angle classification, composed damage,
      reorientation delay, projectile-time flank resolution, and the player-observable health
      advantage from a rear attack.
- [ ] The native macOS development player exercises a rear attack while preserving the complete
      gather, build, produce, scout, counter, AI, and Hisar-destruction loop.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

## Non-Goals

Manual rotation, formation-shape controls, charge, brace, volley, stances, terrain modifiers,
collision changes, new units, new AI planning, new art assets, or balance changes beyond the
minimum turn and flank tuning required for this behavior.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Select a friendly formation and confirm its front indicator and facing readout are clear.
3. Order it around an enemy formation and focus from the rear; confirm it visibly turns before
   attacking while the defender also begins its fixed reorientation.
4. Confirm rear hits receive stronger feedback and remove health faster than otherwise identical
   front hits, without overwhelming the existing type-counter relationship.
5. Move again and confirm the formation faces its travel direction, then issue Stop and
   Attack-Move to confirm the existing command flow remains responsive.
6. Complete the current economy and match paths through Hisar destruction, Restart, and Quit.
7. Inspect the player log for exceptions, missing references, assertion failures, and navigation
   errors.

## Evidence

Record the PR, final reviewed HEAD, verification summary, review round, fixer count, and merge result
before closing the slice.
