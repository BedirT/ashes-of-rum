# Slice 023 - Integrate The Authored Archer

State: In progress

## Player Outcome

Friendly and hostile Archer formations use the authored Archer character, bow, arrow projectile, and
Humanoid animations in the running match. Individual Archers visibly idle, walk, release arrows,
react to hits, and fall on death while retaining the existing faction colors and overhead shape
markers.

## Acceptance

- [ ] Newly trained and AI-controlled Archer formations show eight authored characters at the existing
  four-wide, two-deep member positions.
- [ ] Friendly and hostile Archers remain distinguishable by blue/red character tint and the existing
  diamond/square overhead markers.
- [ ] Authored idle and walk clips follow code-owned member movement with root motion disabled.
- [ ] The release/recoil clip begins at the existing code-owned projectile release instant; projectile
  flight, attack cadence, impact timing, damage, and targeting remain unchanged.
- [ ] Nonlethal hits play the authored hit reaction while preserving flank-colored flash feedback.
- [ ] A killed Archer leaves gameplay immediately, plays the authored death presentation, and is then
  cleaned up without delaying population release or formation re-forming.
- [ ] Spearmen and Cavalry retain their current primitive/procedural presentation.
- [ ] Focused Edit Mode and Play Mode coverage protects the generated asset contract and runtime states.
- [ ] The macOS ARM64 Development build and complete-match paths remain playable with clean logs.
- [ ] `make verify` passes on the exact PR HEAD and a context-free review covers that HEAD.

## Non-Goals

- No Worker, Spearman, Cavalry, building, terrain, HUD, audio, or balance presentation changes.
- No animation-driven translation, facing, attack cadence, projectile timing, damage, casualty timing,
  collision, navigation, selection, fog, AI, or population behavior.
- No dodge, block, punch, kick, dive, turn, landing, equip, unequip, alternate idle, or aimed-locomotion
  states.
- No animation-event gameplay authority, root motion, ragdolls, blend trees, IK, LOD work, or
  production-polish pass.

## Manual Play Check

Launch the macOS Development player with `--archer-preview` to bypass the normal match and directly
spawn one friendly and one hostile Archer formation under a close camera. Confirm all sixteen models
stand on the battlefield surface, retain readable texture detail under their blue/red faction tint, and
show their authored bows and quivers without primitive member meshes obscuring them. Inspect the
generated preview result and player log before exit.

## Evidence

Record the PR, final reviewed HEAD, verification summary, review round, fixer count, merge result, and
post-merge proof here before closing the slice.
