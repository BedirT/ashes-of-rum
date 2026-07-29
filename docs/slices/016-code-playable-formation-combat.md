# Exceptional Refactor 016 - Make Formation Combat Code-Playable

State: Complete

## User-Approved Exception

The user explicitly directed completion of the remaining code-playability work after Slice 015.
This is an exceptional refactor and verification-infrastructure extension, not a gameplay slice or
player replay system.

## Preserved Player-Visible Paths

The complete mouse-and-keyboard match remains unchanged. Existing fog, formation orders, target
priority, deterministic combat, AI, victory, defeat, Restart, and Quit behavior remain authoritative.

## Agent-Visible Outcome

A development player can train and select Spearmen, scout currently visible hostile formations and
workers through fog-safe stable IDs, issue the shared Attack-Move, Stop, and Focus Target commands,
wait for deterministic damage, and emit state-paired combat evidence without using pixels to act.

## Acceptance Checks

- Player state includes currently visible hostile formations and workers only, with stable run-local
  IDs, health, type/activity, order, and quantized position.
- Mobile hostile actors disappear from observations and cannot be targeted when not currently visible.
- `attack_move`, `stop`, and `focus` validate match state, selection, battlefield reachability, target
  visibility, hostility, and life before sharing the normal formation command paths.
- Rejected commands do not mutate the selected formations or their current orders.
- A real built-player scenario gathers Supplies, trains Spearmen, uses Attack-Move to scout the enemy
  economy, stops, focuses a visible hostile Worker, captures the selected engaged formation and target
  in paired JSON and a 1920x1080 frame, then observes its deterministic defeat through the fog-safe
  damage wait.
- Existing gameplay and every earlier code-playable scenario remain green.

## Non-Goals

- No balance, AI, fog, input, HUD, camera, art, audio, package, or simulation changes.
- No privileged spawning, Supply credits, damage, time jumps, visibility overrides, or AI suspension.
- No hostile information outside current player vision and no omniscient agent action surface.
- No remaining production/building commands, rally, control groups, complete-match agent scenario,
  networking, save/load, rollback, or replay in this increment.
