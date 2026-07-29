# Exceptional Refactor 018 - Make The Complete Match Lifecycle Code-Playable

State: Complete

## User-Approved Exception

The user explicitly directed completion of all remaining code-playability work after Slice 017.
This is an exceptional refactor and verification-infrastructure extension, not a gameplay slice,
save system, or replay system.

## Preserved Player-Visible Paths

The complete mouse-and-keyboard match remains unchanged. Real gathering, construction, production,
fog, combat, AI, Hisar victory and defeat, frozen result overlay, Restart, Quit, and local telemetry
remain authoritative.

## Agent-Visible Outcome

A development player can operate from launch through a real Hisar result without pixels, inspect
the frozen outcome and exact telemetry, invoke the shipped result-gated Restart and Quit paths, and
review multiple immutable state-paired 1920x1080 frames centered on relevant visible scenes.

## Acceptance Checks

- Friendly Hisar state includes health, maximum health, attackability/destruction, and result-action
  availability; terminal observation remains safe after either Hisar is destroyed.
- A fog-safe `outcome_is` wait observes `Victory` or `Defeat` without an omniscient oracle.
- Camera centering uses the shipped bounded camera/minimap semantic seam, changes no gameplay or fog
  authority, and cannot target hidden mobile or unknown entities.
- Every named capture writes a distinct immutable PNG, state JSON, and manifest; every manifest hash
  continues to match its own files after later captures.
- Session results contain existing local match-summary and event-log paths plus SHA-256 hashes, and
  the files contain the required elapsed time, outcome, economy, production/loss, construction/
  destruction, contact, attack, and Hisar fields.
- `restart` rejects before a result; after a result it invokes the shipped Restart path, rebinds the
  persistent runner to a fresh controller/projector/executor, and observes a fresh in-progress match.
- `quit` rejects before a result and invokes the shipped Quit path only after a result. Privileged
  fixture termination is renamed `end_session` and remains development-runner-only.
- A built-player scenario gathers and spends real Supplies, constructs required population/economy,
  trains real formations, scouts and destroys the hostile Hisar through normal selection and combat
  orders, captures the frozen result, and proves Restart and Quit across built lifecycle runs without
  Supply credits, spawning, damage, time jumps, visibility overrides, or AI suspension.
- Existing normal gameplay and every earlier code-playable scenario remain green.

## Non-Goals

- No balance, AI, fog, input, HUD, art, audio, package, or simulation changes.
- No privileged spawning, Supply credits, direct damage, time jumps, visibility overrides, or AI
  suspension in the built lifecycle proof.
- No network transport or live adaptive observation/action loop; that remains Slice 019.
- No save/load, rollback, replay, networking, cloud service, or generalized agent platform.

## Implemented Verification Path

- The deterministic real-economy route spends the initial 100 Supplies on one House, gathers the
  two 400-Supply safe caches through fog-visible player orders, trains Spearmen and Archers through
  the shared Hisar queue, scouts with Attack-Move, and focuses the newly visible hostile Hisar.
- `agent-complete-match-restart.json` captures the assault and frozen Victory, invokes the shipped
  Restart listener, proves a freshly rebound in-progress match, captures it, then uses the
  development-runner-only `end_session` action.
- `agent-complete-match-quit.json` captures distinct 1920x1080 assault and frozen-Victory frames,
  then invokes the shipped result-gated Quit listener. Each state and PNG has its own path and
  SHA-256 in its immutable checkpoint manifest.
- The harness verifies the persisted match summary and event log by exact paths and SHA-256 hashes,
  including outcome, elapsed time, gathered Supplies, production/loss, construction/destruction,
  first contact, all AI attack timing fields, and Hisar destruction.
