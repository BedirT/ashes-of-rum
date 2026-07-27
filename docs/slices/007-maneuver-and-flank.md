# Slice 007 - Maneuver And Flank

## Player Outcome

Use movement and attack-move orders to approach an enemy formation from the side or rear, read
both formations' facing and turn state, and gain a modest deterministic damage advantage before
the defender finishes reorienting toward the attacker.

## Acceptance

- [x] Moving formations face their travel direction and display a readable front indicator.
- [x] A formation in combat turns toward its target over one short fixed duration and cannot
      attack until it is facing that target.
- [x] Formation attacks are classified deterministically as front, side, or rear from the
      defender's facing when each melee hit or Archer projectile lands.
- [x] Side and rear hits apply tunable modest bonuses that compose with the existing counter
      multiplier without changing worker or structure damage.
- [x] Selection and order feedback identify a formation's current facing and visible turn state.
- [x] Focused EditMode and PlayMode coverage protects angle classification, composed damage,
      reorientation delay, projectile-time flank resolution, and the player-observable health
      advantage from a rear attack.
- [x] The native macOS development player exercises a rear attack while preserving the complete
      gather, build, produce, scout, counter, AI, and Hisar-destruction loop.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

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

- PR: [#13](https://github.com/BedirT/rts-game/pull/13)
- Verification: `make verify` passed at final reviewed HEAD
  `8c8628dd47113f2807857dec6012dfbf8c90c996` with 24 EditMode and 69 PlayMode tests,
  an ARM64 development build, headless smoke, graphical 1920x1080 smoke, and clean logs.
- Runtime proof: the exact-HEAD built player completed the existing economy, construction,
  production, scouting, counter-combat, scripted-AI, Hisar-result, Restart, and Quit paths. It
  displayed a casualty-safe formation front marker and live `FACING N 15 deg | READY` selection
  feedback, then performed matched front and rear volleys and confirmed the rear volley removed
  more health with a stronger hit reaction. Both graphical captures were visually inspected.
- Review: context-free round 1 reported no blocking findings at the final head; no fixer run was
  required.
- Merge: squash-merged as `7a145ff4254b35eecd2dc320a06678c56e9551d5`; `make post-merge`
  passed on merged `main` with the same 24 EditMode and 69 PlayMode tests, ARM64 build, smokes,
  rear-attack assertion, and clean logs.
