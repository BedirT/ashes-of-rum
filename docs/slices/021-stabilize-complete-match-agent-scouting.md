# Exceptional Refactor 021 - Stabilize Complete-Match Agent Scouting

State: Complete

## User-Approved Exception

The user directed completion of every remaining code-playability gap. Merged-main verification
then exposed a graphical complete-match failure when one selected worker remained in `Moving`
near a shared destination even though the intended west Supplies cache was already visible. This
is a narrow exceptional corrective refactor, not a gameplay slice.

## Preserved Player-Visible Paths

Normal mouse-and-keyboard gameplay, worker movement and gathering, scripted and adaptive agent
commands, complete-match victory, Restart, Quit, and every existing verification path remain
unchanged.

## Player-Observable Outcome

The code-driven complete match scouts until the target Supplies cache is visible in the same
fog-safe state available to a player, then gathers from it and completes the match. Verification no
longer depends on every worker occupying an exact shared-destination slot.

## Acceptance Checks

- The agent wait vocabulary can wait for a named Supplies cache to be currently visible.
- Visibility uses the existing fog-safe cache resolver and reveals no hidden resource state.
- Both complete-match lifecycle fixtures wait for `cache-1` visibility before gathering it.
- Focused contract coverage rejects a regression back to the unrelated all-workers-idle gate.
- The graphical Quit and headless Restart complete-match paths pass from the native Development
  player, with their immutable state/frame and telemetry evidence intact.
- The full test, ARM64 build, runtime, live-agent, conflict-mode, and log ladder remains green.

## Non-Goals

- No gameplay, movement, pathfinding, worker-slot, balance, AI, fog, input, HUD, art, audio,
  package, protocol-schema, or remote-control change.
- No retry, timeout inflation, hidden-map inspection, privileged mutation, or fixture-only cheat.
- No changes outside the minimum condition evaluator, fixtures, tests, and verification evidence.

## Evidence

- PR: [#34](https://github.com/BedirT/rts-game/pull/34)
- Final reviewed head: `1f3c105e138e5e2a5223e491aa798cc6ce36f598`
- Review: round 1 reported no blocking findings after independently running both parameterized
  fixture contract cases and inspecting exact-head lifecycle artifacts; no fixer run was needed.
- Exact-head verification: 46/46 Edit Mode, 92/92 Play Mode, macOS ARM64 Development build,
  launch-mode conflict proof, every normal/scripted headless and graphical smoke, real-economy
  Victory with both Restart and graphical Quit, both adaptive live runs, clean logs, static checks,
  and artifact/hash validation passed.
- Merge: squash-merged as `5720b32672e1f97a2c88040f865f957efc03fbbc`; the branch was deleted.
- Post-merge: `make post-merge` passed on merged `main` at that exact SHA with the same full ladder,
  including the graphical complete-match Quit path that originally exposed this defect.
