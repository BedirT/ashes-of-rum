# Exceptional Refactor 014 - Make Formation Training Code-Playable

State: Complete

## User-Approved Exception

The user explicitly approved continuing the behavior-preserving agent-operability work after Slice
013. This is an exceptional refactor and verification-infrastructure extension, not a gameplay
slice and not a player replay system.

## Preserved Player-Visible Paths

The complete current match remains mouse-and-keyboard playable. Worker economy, construction,
Hisar selection and production, rallying, formation selection and orders, combat, fog, AI,
victory, defeat, Restart, and Quit retain their shipped behavior and presentation.

## Agent-Visible Outcome

A development ARM64 player can gather the real Supplies required for one formation, train
Spearmen through the shared Hisar production queue, wait for production to complete, observe the
new eight-member friendly formation in fog-safe structured state, and emit a named checkpoint
whose JSON state and frame manifest identify the corresponding 1920x1080 PNG.

## Acceptance Checks

- The player-view snapshot reports the friendly production queue count, active item, and progress,
  plus friendly formations with stable run-local IDs, type, selection, living member count, health,
  current order, destination state, facing, and position.
- A `train` command accepts `Spearmen` and uses the same Supply spending, population reservation,
  queue timing, completion, telemetry, feedback, and spawn path used by normal Hisar production.
- Unsupported formation types, insufficient Supplies, population blocking, and a completed match
  return stable rejection codes without spending Supplies, reserving population, or adding a queue
  item.
- A semantic `formation_ready` wait succeeds only after a matching friendly formation exists.
- The built player gathers from a visible cache, spends 400 real Supplies, reserves eight
  population, completes one eight-member Spearmen formation, and leaves the queue empty.
- Headless and graphical proof emit accepted structured responses and a passing session result.
- The graphical checkpoint pairs the exact saved state with a 1920x1080 PNG and verified SHA-256.
- Existing mouse, keyboard, HUD, economy, construction, complete-match, and earlier agent
  verification remain green.

## Scope

- Extract the minimum shared formation-training command seam from the current Hisar queue path
  without changing player behavior.
- Extend the player state projector with friendly production and formation state.
- Add one file-backed built-player Spearmen training scenario and exact-SHA harness gates.
- Add focused command, state, rejection, wait-condition, and fixture coverage.

## Non-Goals

- No gameplay, balance, input binding, HUD, AI, fog, audio, art, camera, or package changes.
- No Worker, Archer, Cavalry, cancellation, rally, formation selection, movement, combat, control
  group, AI, or complete-match agent command coverage in this increment.
- No Supply credits, spawning, instant production, time jumps, visibility overrides, or AI
  suspension.
- No hostile mobile observations, omniscient state, live socket, HTTP service, MCP dependency,
  ML-Agents package, save/load, rollback, deterministic replay, or player replay feature.
