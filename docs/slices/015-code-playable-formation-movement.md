# Exceptional Refactor 015 - Make Formation Movement Code-Playable

State: Complete

## User-Approved Exception

The user explicitly approved continuing the behavior-preserving agent-operability work after Slice
014. This is an exceptional refactor and verification-infrastructure extension, not a gameplay
slice and not a player replay system.

## Preserved Player-Visible Paths

The complete current match remains mouse-and-keyboard playable. Worker economy, construction,
Hisar production, formation selection and movement, combat, fog, AI, victory, defeat, Restart, and
Quit retain their shipped behavior and presentation.

## Agent-Visible Outcome

A development ARM64 player can train Spearmen, select that friendly formation by its stable
run-local ID, issue the shared formation move order, wait for the formation to arrive, and emit a
named checkpoint whose JSON state and frame manifest identify the corresponding 1920x1080 PNG.

## Acceptance Checks

- A `select` command accepts either friendly Worker IDs or friendly formation IDs, rejects unknown
  actors and mixed actor types with stable codes, and uses the existing selection state.
- A formation `move` command rejects a missing selection, completed match, invalid battlefield
  position, and unreachable destination without dispatching an order.
- An accepted move uses the existing player formation group-order path and projects selection,
  `Move`, destination, arrival, and final position through the stable formation ID.
- A semantic `formation_arrived` wait succeeds only when the specified friendly formation is idle,
  has no destination, and is within the documented arrival tolerance of the requested position.
- The built player gathers the real formation cost, trains one Spearmen formation, selects it,
  moves it to the scripted destination, and captures the selected formation after arrival.
- Headless and graphical proof emit accepted structured responses and a passing session result.
- The graphical checkpoint pairs the exact saved state with a 1920x1080 PNG and verified SHA-256.
- Existing mouse, keyboard, HUD, economy, construction, complete-match, and earlier agent
  verification remain green.

## Scope

- Add the minimum stable friendly-formation resolution and command validation needed to share the
  current selection and movement paths.
- Add a semantic arrival condition for an identified friendly formation.
- Add one file-backed built-player training-and-movement scenario and exact-SHA harness gates.
- Add focused command, state, rejection, arrival, and fixture coverage.

## Non-Goals

- No gameplay, balance, input binding, HUD, AI, fog, audio, art, camera, or package changes.
- No mixed Worker-and-formation selection, attack-move, stop, focus-fire, combat, hostile mobile
  observations, control groups, AI commands, or complete-match agent command coverage.
- No spawning, Supply credits, instant movement, teleports, time jumps, visibility overrides, or AI
  suspension.
- No omniscient state, live socket, HTTP service, MCP dependency, ML-Agents package, save/load,
  rollback, deterministic replay, or player replay feature.
