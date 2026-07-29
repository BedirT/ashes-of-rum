# Exceptional Refactor 017 - Make Economy And Production Code-Playable

State: Complete

## User-Approved Exception

The user explicitly directed completion of all remaining code-playability work after Slice 016.
This is an exceptional refactor and verification-infrastructure extension, not a gameplay slice or
player replay system.

## Preserved Player-Visible Paths

The complete mouse-and-keyboard match remains unchanged. Existing selection, gathering, building,
construction cancellation, demolition confirmation, Hisar production, rallying, combat, AI,
victory, defeat, Restart, and Quit behavior remain authoritative.

## Agent-Visible Outcome

A development player can use stable structured state and the same semantic command seams as the
normal HUD and input paths to build each building type, train Workers and every formation type,
cancel construction or production, confirm demolition, and set a terrain or visible-cache Hisar
rally without using pixels to act.

## Acceptance Checks

- Player state includes the friendly Hisar's stable ID, health, selection, rally destination, and
  visible rally-cache ID, plus the active production item, count, and progress.
- `build` accepts House, Storehouse, and Watchtower through the shared placement rules and rejects
  unknown types without mutation.
- `train` accepts Worker, Spearmen, Archers, and Cavalry through the shared Hisar queue rules and
  rejects unknown types without mutation.
- `cancel_construction`, `cancel_production`, `request_demolition`, and `confirm_demolition` preserve
  the shipped refund, confirmation, selection, and match-state rules.
- `set_rally` accepts battlefield terrain under the shipped Hisar ground-rally rules or a currently visible non-exhausted cache and
  rejects hidden, exhausted, unknown, or invalid targets without changing the prior rally.
- A real built-player scenario gathers and spends real Supplies, cancels and replaces construction,
  completes a Storehouse, sets a visible-cache rally, queues and cancels production, trains a Worker
  that begins gathering at that rally, demolishes the completed Storehouse with confirmation, and
  captures paired structured state and a 1920x1080 frame.
- Existing gameplay and every earlier code-playable scenario remain green.

## Scope

- Extend the player snapshot with one stable friendly Hisar record and its fog-safe rally and
  shared production-queue state.
- Share the existing placement, queue, refund, rally, selection, and two-step demolition paths
  behind semantic development-player commands with stable rejection codes.
- Add one file-backed real-economy built-player scenario with paired headless and 1920x1080
  graphical checkpoint validation.
- Add focused parsing, state, accepted-command, rejected-command, refund, confirmation, and
  no-mutation coverage plus exact harness assertions for the action trace.

## Non-Goals

- No balance, AI, fog, input, HUD, camera, art, audio, package, or simulation changes.
- No privileged spawning, Supply credits, damage, time jumps, visibility overrides, or AI suspension.
- No control-group command surface because stable-ID selection provides equivalent gameplay
  authority; control groups remain a player convenience rather than a complete-loop dependency.
- No result-gated Restart or Quit, complete-match agent scenario, telemetry references, networking,
  save/load, rollback, or replay in this increment.
