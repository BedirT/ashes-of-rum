# Workflow 026 - Make verification headless-first

This is a repository-workflow change, not a gameplay slice.

## Observable outcome

`make verify` keeps the complete Edit Mode and Play Mode suites, native Apple-silicon build,
and built-player proof, but no longer renders every semantic agent scenario twice. Gameplay-time
inside tests is accelerated as high as 20x. Headless built semantic sessions use a NavMesh-safe 4x
speed, the graphical lifecycle gate uses a stable 2x speed, and real-time watchdogs remain intact.

## Acceptance checks

- Run every Edit Mode and Play Mode test headlessly.
- Run ordinary Play Mode gameplay waits at 20x game-time without changing production tuning,
  while frame-sensitive animation and NavMesh tests declare a lower explicit speed.
- Build the native macOS Apple-silicon Development player.
- Complete a real-economy match and shipped Restart through the semantic agent protocol in
  `-batchmode -nographics` at 4x game-time.
- Run one 1920x1080 graphical semantic complete match at 2x game-time to prove the rendered
  launch-to-result path and shipped Quit listener without duplicating every semantic scenario.
- Exercise the adaptive live-agent mailbox in `-batchmode -nographics` at 4x game-time.
- Record only gates that actually ran in the exact-SHA verification summary.
- Demonstrate a substantial wall-clock reduction compared with the previous 18-minute-plus run.

## Non-goals

- No gameplay, balance, economy, AI, or presentation changes.
- No removal of Edit Mode or Play Mode coverage.
- No acceleration in normal player launches; verification acceleration is development-only and
  requires an explicit process argument.
- No hosted CI, parallel Unity Editors, or new test framework.
- The individual scenario commands remain available for focused diagnosis; they are simply not
  all repeated by the full verification path.
