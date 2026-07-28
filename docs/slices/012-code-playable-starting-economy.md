# Exceptional Refactor 012 - Make The Starting Economy Code-Playable

State: Complete

## User-Approved Exception

The user explicitly approved a behavior-preserving agent-operability seam so development agents can
play from structured code-visible state while rendered frames remain optional input and reviewable
evidence. This is an exceptional refactor and verification-infrastructure change, not a gameplay
slice and not a player replay system.

## Preserved Player-Visible Paths

The complete current match remains mouse-and-keyboard playable. Worker selection, movement,
gathering, deposit, construction, production, formation commands, fog, AI, victory, defeat, Restart,
and Quit retain their shipped behavior and presentation.

## Agent-Visible Outcome

A development ARM64 player can execute the starting Worker move and gather-deposit loop from a
versioned JSON script, observe fog-safe structured state after every step, and emit a named
checkpoint whose JSON state and frame manifest identify the corresponding 1920x1080 PNG.

## Acceptance Checks

- Agent mode is development-only, local-only, opt-in at process launch, and absent from the HUD.
- The script schema and every response carry a version and stable request or sequence identity.
- A player-view snapshot reports economy, outcome, camera, initial friendly Workers, and currently
  visible Supply caches with stable run-local IDs and deterministic ordering.
- The protocol can select a friendly Worker, issue a reachable move, and gather from a visible cache
  through the same semantic command validation used by normal contextual input.
- Invalid, hidden, unknown, busy, or unreachable commands return stable rejection codes without
  privileged mutation.
- The built player completes a real gather-deposit loop without Supply credits, spawning, direct
  damage, time jumps, visibility overrides, or disabling the opponent.
- Headless proof emits structured responses and a passing session result.
- Graphical proof emits the same structured path plus a 1920x1080 PNG, checkpoint state, manifest,
  and verified screenshot SHA-256.
- Existing real Input System tests continue to prove mouse, keyboard, HUD, and contextual orders.
- Full tests, ARM64 build, complete-match headless/graphical smoke, hands-on smoke, and logs remain
  green.

## Scope

- Extract the minimum shared Worker selection, movement, and gathering command validation from the
  current input path.
- Add a read-only player-perspective state projector for the starting economy.
- Add a deterministic file-backed JSON script runner and JSONL response trace to the development
  player.
- Extend exact-SHA harness evidence with headless and graphical agent scenarios.

## Non-Goals

- No gameplay, balance, input binding, HUD, AI, fog, audio, art, or camera behavior changes.
- No live network server, MCP dependency, ML-Agents package, cloud service, or remote telemetry.
- No arbitrary C# execution, GameObject hierarchy dump, or agent access to private Unity objects.
- No construction, production, military, AI, or complete-match agent command coverage yet.
- No omniscient observation supplied to the acting agent.
- No save/load, rollback, simulation rewrite, deterministic-replay claim, or player replay feature.
- No pixel-perfect golden suite, segmentation pipeline, VLM merge gate, or Graphics Test Framework
  package.
