# Slice 008 - Hold The Frontline

## Player Outcome

Order a formation directly through a visible opposing formation and see it hold at a readable
frontline instead of passing through the enemy, then maneuver laterally around that line to keep
the existing flanking path available.

## Acceptance

- [x] A normal move order cannot carry a formation through a visible opposing formation.
- [x] The selected-formation HUD identifies when an enemy frontline is blocking movement.
- [x] A lateral order lets the blocked formation disengage and move around the opponent.
- [x] Allied formations retain their existing soft avoidance and temporary compression behavior.
- [x] Focus, attack-move, reorientation, and side or rear damage keep their existing behavior.
- [x] Focused EditMode and PlayMode coverage protects deterministic blocking, release, and
      same-faction non-blocking behavior.
- [x] The native macOS development player exercises both a blocked direct move and a successful
      lateral release while preserving the complete match loop.
- [x] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [x] A context-free review covers the exact final HEAD.
- [x] The merged game remains playable at its current scope.

## Non-Goals

Rigid allied collision, physics-driven pushing, formation-shape controls, manual facing, new
pathfinding, terrain modifiers, combat balance changes, additional unit types, or AI planning
changes.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080.
2. Select a friendly formation, reveal a hostile formation, and issue a normal move order beyond
   it; confirm the friendly formation stops on its own side of the enemy and the HUD reads
   `FRONTLINE BLOCKED`.
3. Issue a lateral move away from the opposing formation; confirm the selected formation moves and
   the blocked readout clears.
4. Route around the enemy and focus it from the side or rear; confirm reorientation and flank
   feedback still work.
5. Exercise gathering, construction, production, AI pressure, Hisar destruction, Restart, and Quit.
6. Inspect the player log for exceptions, missing references, assertion failures, navigation
   errors, or repeated blocked-state churn.

## Evidence

- Gameplay PR: [#15](https://github.com/BedirT/rts-game/pull/15)
- Corrective verification PR: [#16](https://github.com/BedirT/rts-game/pull/16)
- Verification: `make verify` passed on final gameplay HEAD
  `1d4129c7b9272b2ae81aa64badb492cc6cdbcd0e` and corrective HEAD
  `622750b8d33eef2c401ce69cd3381644b9380700`, each with 25 EditMode and 71
  PlayMode tests, an ARM64 development build, headless smoke, graphical 1920x1080 smoke, the
  frontline block and lateral-release assertion, and clean logs.
- Runtime proof: the built player issued a normal move through a visible opposing formation,
  stopped on its own side with `FRONTLINE BLOCKED` selection feedback, moved laterally to clear
  the line, and preserved attack-move, counter combat, flank damage, both scripted-AI Hisar
  outcomes, Restart, and Quit. Graphical captures were visually inspected.
- Review: context-free round 1 on both PRs reported no blocking findings at each final head; no
  fixer runs were required.
- Merge: gameplay was squash-merged as `793d2934f4a1f8e19651ca9c1e9163df4c397c85`.
  Its first post-merge graphical smoke exposed a setup dependency in the isolated Hisar proof:
  surviving AI formations correctly blocked the runner's direct structure path. Corrective PR
  #16 made that test setup deterministic without changing gameplay and was squash-merged as
  `cad4b62427030fa8e9b7393bb0578e5892634b3c`; `make post-merge` then passed on merged `main` with
  the same 25 EditMode and 71 PlayMode tests, ARM64 build, both smokes, and clean logs.
