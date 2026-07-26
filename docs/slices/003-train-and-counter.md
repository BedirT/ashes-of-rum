# Slice 003 - Train And Counter

## Player Outcome

Gather enough Supplies, select the Hisar, train a full eight-member formation through its shared
queue, then select that formation and focus an enemy formation to resolve a readable counter fight.

## Acceptance

- [x] Selecting the Hisar exposes Spearman, Archer, and Cavalry training commands with visible
      costs and hotkeys.
- [x] All formation types use one queue, spend 400 Supplies, reserve eight population, complete
      after a visible deterministic timer, and rally as four-wide, two-deep formations.
- [x] Population-blocked or unaffordable orders do not enter the queue or spend resources.
- [x] Cancelling the active queue item refunds its full Supply cost and releases its reserved
      population.
- [x] A completed friendly formation can be selected and right-clicked onto the first enemy
      formation to issue a visible focus-target order.
- [x] Eight visible members fight deterministically; casualties disappear and immediately release
      population, while Archer damage clearly counters Spearmen.
- [x] Focused EditMode and PlayMode coverage protects queue arithmetic, cancellation, population,
      counter modifiers, deterministic casualties, training, selection, and combat resolution.
- [x] The native macOS development player trains Archers and wins the counter fight during smoke
      while preserving gathering and House construction.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

Worker training, a complete enemy economy or AI build script, Attack-Move, Stop, formation-group
movement, flanking, Cavalry speed tuning, structure damage, Storehouses, Watchtowers, fog of war,
minimap, victory or defeat, audio, and visual polish.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Send all four workers to gather until at least 400 Supplies are available.
3. Select the Hisar and confirm all three formation commands, their 400-Supply cost, and queue state.
4. Train Archers; confirm Supplies and available population fall immediately and queue progress is visible.
5. Cancel once; confirm all 400 Supplies and eight population return, then train Archers again.
6. Confirm eight blue diamond-marked Archers rally in a four-wide, two-deep block and red
   square-marked Spearmen arrive as first contact.
7. Select the Archers and right-click the Spearmen; confirm focus feedback, visible arrow travel,
   hit feedback, casualties, compact re-forming, and population release.
8. Confirm the Archers win clearly with meaningful survivors, then gather, build a House, issue a
   worker move order, and quit normally.
9. Inspect the player log for exceptions, missing references, assertion failures, and navigation errors.

## Evidence

- PR: [#6](https://github.com/BedirT/rts-game/pull/6)
- Verification: `make verify` passed at final reviewed HEAD
  `2dde0765ce9d1e432dee0a71992c11e179fca357` with 10 EditMode and 16 PlayMode tests,
  an ARM64 development build, headless smoke, graphical 1920x1080 smoke, and clean logs.
- Runtime proof: the exact-HEAD built player gathered and deposited Supplies, constructed a House,
  trained eight Archers through the shared queue, rendered supported blue/white/cyan formation
  materials, fired eight visible arrows, displayed nonlethal hit feedback, defeated eight Spearmen,
  and retained all eight Archers. The graphical capture was inspected for HUD and formation clarity.
- Review: round 1 reported two blocking findings and required fixer run 1; round 2 reported no
  blocking findings at the final head.
- Merge: squash-merged as `dcd54bb749731c821e447fd135605da4520a4adc`; `make post-merge`
  passed on merged `main` with the same 10 EditMode and 16 PlayMode tests, build, smokes, and log checks.
