# Slice 011 - Restructure The Playable Prototype

State: In Progress

## Player-Visible Outcome

The complete current match still launches and plays from economy through Hisar victory or defeat,
with every control, formation-member behavior, AI phase, fog rule, restart, and quit path preserved.

## Value Now

The prototype's behavior is concentrated in files as large as 3,401 lines. The next gameplay slice
would be slower and riskier to change without first making the already-shipped domains navigable.

## Acceptance Checks

- Runtime and test code is grouped by existing gameplay domain without adding speculative layers.
- No hand-authored C# file exceeds 1,000 lines; cohesive files target 500-750 lines.
- The repository static check rejects a future hand-authored C# file above 1,000 lines.
- EditMode and PlayMode suites pass with the same behavioral coverage.
- The macOS Apple-silicon development build succeeds.
- Headless and graphical smoke complete the full current player path with clean logs.

## Scope

- Split the runtime composition root into responsibility-based partial files without changing its
  Unity component identity or serialized references.
- Move existing runtime types into gameplay-domain folders.
- Separate formation, formation-member, and formation-feedback types.
- Split the PlayMode test catalog by the behavior it verifies while preserving shared helpers.
- Record the agreed file-size and minimal-architecture rules in the product contract.

## Non-Goals

- No gameplay, balance, UI, art, audio, input, AI, or tuning changes.
- No new service layer, dependency-injection container, interface, factory, or package.
- No renaming public gameplay types solely for architectural aesthetics.
- No test deletion, assertion weakening, or shortened runtime proof.
