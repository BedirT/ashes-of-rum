# Exceptional Refactor 013 - Make House Construction Code-Playable

State: Complete

## User-Approved Exception

The user asked to continue the approved behavior-preserving agent-operability work after slice 012.
This is an exceptional refactor and verification-infrastructure extension, not a gameplay slice and
not a player replay system.

## Preserved Player-Visible Paths

The complete current match remains mouse-and-keyboard playable. Worker placement mode, snapped
building placement, Supply spending, construction labor, population capacity, cancellation,
gathering resumption, production, formation commands, combat, fog, AI, victory, defeat, Restart,
and Quit retain their shipped behavior and presentation.

## Agent-Visible Outcome

A development ARM64 player can select a Worker, place a House at a valid visible and reachable
snapped position through the normal construction rules, wait for completion, observe population
capacity rise from 12 to 20 in fog-safe structured state, and emit a named checkpoint whose JSON
state and frame manifest identify the corresponding 1920x1080 PNG.

## Acceptance Checks

- The player-view snapshot reports friendly buildings with stable run-local IDs, type, completion,
  health, progress, position, and assigned builder IDs without exposing hostile hidden state.
- A `build` command accepts a selected available Worker, `House`, and a finite battlefield position;
  it uses the same snapping, visibility, occupancy, reachability, route-preservation, Supply, and
  construction behavior used by normal placement.
- Missing selection, unsupported building types, invalid or hidden positions, busy builders,
  insufficient Supplies, occupied ground, unreachable sites, and route-blocking sites return stable
  rejection codes without spending Supplies or creating a foundation.
- The built player spends the real starting Supplies, creates one House foundation, waits for the
  assigned Worker to complete it, and observes population capacity rise from 12 to 20.
- Headless and graphical proof emit accepted structured responses and a passing session result.
- The graphical checkpoint pairs the exact saved state with a 1920x1080 PNG and verified SHA-256.
- Existing mouse, keyboard, HUD, contextual orders, construction, complete-match, and slice-012
  verification remain green.

## Scope

- Extract the minimum shared building-placement validation and House command seam from the current
  placement path without changing its player behavior.
- Extend the existing player state projector with friendly building state.
- Add one file-backed built-player House construction scenario and its exact-SHA harness gates.
- Add focused command, fog-safety, state, and rejection coverage.

## Non-Goals

- No gameplay, balance, input binding, HUD, AI, fog, audio, art, camera, or package changes.
- No Storehouse, Watchtower, cancellation, demolition, production, rally, formation, combat, AI, or
  complete-match agent command coverage in this increment.
- No construction credits, spawning, instant completion, time jumps, visibility overrides, or AI
  suspension.
- No live socket, HTTP service, MCP dependency, ML-Agents package, cloud service, or remote telemetry.
- No omniscient observation, save/load, rollback, deterministic replay, or player replay feature.
