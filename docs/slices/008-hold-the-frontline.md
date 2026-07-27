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
- [ ] The native macOS development player exercises both a blocked direct move and a successful
      lateral release while preserving the complete match loop.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

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

Pending implementation, verification, review, and merge.
