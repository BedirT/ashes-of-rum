# Slice 006 - Win A Complete Match

## Player Outcome

Launch directly into The Sundered Road, build an economy and army against a fair Alazhan opponent,
survive its visible probe and attack phases, destroy the enemy Hisar or lose the Karasungur Hisar,
then see the frozen match result and either restart or quit.

## Acceptance

- [ ] A clean launch creates mirrored Karasungur and Alazhan Hisars, workers, finite Supplies,
      population, construction, and shared production queues without hidden income, units, or
      emergency defenders.
- [ ] The Alazhan script uses the real economy to build and train, sends a Cavalry probe around
      minute 3, mixed pressure around minute 6, and a final Hisar assault around minute 10; nearby
      surviving formations defend an early player attack before resuming the script. A failed
      gathering route spends the real Storehouse cost, assigns one Worker to construction, uses
      the completed drop-off, and can recover again after enemy destruction without a refund.
- [ ] Selecting the Hisar and right-clicking terrain creates a visible rally point for new
      formations; right-clicking a currently visible neutral cache sends each newly trained Worker
      directly into the gather-deposit loop without accepting unseen cache targets.
- [ ] Local procedural cues cover selection, orders, construction, production, attacks, hits,
      warnings, victory, and defeat. Friendly under-attack events emit a throttled, fog-aware
      minimap ping without moving the camera; no music, voice, or external audio asset is added.
- [ ] Friendly and hostile military formations can focus visible buildings and damage every
      structure at one standardized reduced structural rate; formations remain the first automatic
      combat priority and the Hisar remains non-combat.
- [ ] Destroying either Hisar immediately freezes gameplay, clears active input and combat, and
      presents Victory or Defeat with elapsed time plus only working `Restart` and `Quit` actions.
- [ ] Restart creates a fresh deterministic match without retaining economy, fog, units, buildings,
      result state, or telemetry counters from the previous match.
- [ ] Neutral Supply caches obey three-state fog: unexplored caches and depletion changes stay
      hidden, explored caches retain their last-seen state, and caches create no hostile minimap
      marker or first-contact telemetry.
- [ ] A local JSON event log and match summary record elapsed time, outcome, Supplies gathered,
      entities produced and lost, buildings constructed and destroyed, first contact, AI attack
      timings, and Hisar destruction without transmitting data.
- [ ] Default tuning targets a 10-15 minute understood-player match, while tests and smoke use an
      explicit development-only clock override rather than changing shipped timings or granting
      player-facing resources.
- [ ] Focused EditMode coverage protects AI phase transitions, fair spending/population rules,
      deterministic structural damage, result state, and telemetry serialization.
- [ ] Focused PlayMode coverage protects AI gathering/building/training, each attack phase, early
      defense, visible structure focus, both Hisar outcomes, frozen simulation, Restart, and clean
      match-state reset.
- [ ] The native macOS development player exercises a deterministic launch-to-result path, Restart,
      the opposite result, and Quit while preserving every previously merged economy, building,
      formation, counter, control-group, fog, and minimap path.
- [ ] `make verify` passes on the exact PR HEAD and its local SHA-keyed evidence is recorded in the PR.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The merged game remains playable at its current scope.

## Non-Goals

Reactive or learning AI, multiple difficulties, faction asymmetry, additional maps or units, siege
systems, reinforcement, repair, technology, replay, save or pause, tutorial content, music, voice,
external assets, networking, production art, and presentation polish beyond readable match-state
feedback.

## Manual Play Check

1. Launch the exact-HEAD macOS development build at 1920x1080 and confirm the match begins directly
   with the existing Karasungur economy and every unexplored Alazhan-side Supply cache hidden by fog.
2. Gather Supplies, construct a House and defensive building, train a counter force, and scout the
   road without using development-only controls.
3. Observe the Alazhan Cavalry probe, mixed pressure, and final assault leaving its real economy at
   the configured phase times; verify enemies disappear when player vision is lost.
4. Focus a currently visible hostile building, confirm member attacks visibly reduce structure
   health, and destroy the Alazhan Hisar.
5. Confirm simulation and orders stop immediately, Victory and elapsed time appear, and only
   `Restart` and `Quit` remain interactive.
6. Choose `Restart`, confirm fresh Supplies, population, caches, fog, units, buildings, clock, and
   telemetry, then use the deterministic test path to destroy the Karasungur Hisar and verify Defeat.
7. Choose `Quit`, inspect the local match summary and event log for all required fields, and confirm
   the player log contains no exceptions, assertions, missing references, or navigation errors.

## Evidence

Record the PR, final reviewed HEAD, verification summary, review round, fixer count, merge result,
and merged-main smoke result before closing the slice.
