# Slice 025 - Preserve authored buildings and review scale

## Player-visible outcome

A repeatable built-player preview shows all four proportion-preserving Hisar construction states.
The downloaded untextured tent-style House and complete Hisar are also shown beside one authored
Archer group so the House scale can be approved before a paid texture pass. All accepted source
assets are stored in the repository instead of Downloads or disposable worktrees.

## Acceptance checks

- Import the newly downloaded untextured Meshy T2 House with all required metadata and no errors.
- Normalize the House uniformly to 3.0 m height without stretching its footprint.
- Reuse the existing textured complete Hisar at a uniform 3.25 m height without stretching or
  squeezing it, and give the complete state an independent 10.6 x 6.6 m gameplay footprint. All
  construction states use the complete model's same uniform scale.
- Preserve foundation, raised-frame, canvas-installation, and complete models in a repeatable
  staged development preview.
- Place both in a repeatable in-game presentation beside one eight-member authored Archer group
  and capture a clean 1920x1080 screenshot.
- Run focused tests, the complete verification ladder, and inspect runtime logs.

## Non-goals

- No House texture generation or Meshy credit spend before scale approval.
- No other building, resource, prop, or rubble-state integration.
- No replacement of House gameplay rules, costs, population behavior, or construction timing.
- No replacement of the live starting Hisars or expansion of the compact prototype map. The
  approved visual and its larger planned footprint require a later map-layout integration slice.
- No runtime frontage flags or final environment dressing.
- No speculative shared world-asset framework beyond what this one reachable presentation
  needs.
