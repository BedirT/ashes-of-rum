# Exceptional Refactor 011 - Restructure The Playable Prototype

State: Complete

## User-Approved Exception

The user explicitly requested a complete codebase restructure that adopts Ponytail's
maintainability guidance because existing files exceeded 2,000 lines. This work is an exceptional
behavior-preserving architectural refactor under `AGENTS.md`, not a gameplay slice and not a
precedent for agent-initiated horizontal work.

## Preserved Player-Visible Paths

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
- No exemption from the full automated, build, runtime, PR, context-free review, merge, or
  post-merge proof required by `AGENTS.md`.

## Completion Evidence

- Implementation PR: [#22](https://github.com/BedirT/rts-game/pull/22).
- Final reviewed implementation head: `6d39bc31d4cc36342c4933fbafa6b7e7369c184a`.
- Squash merge on `main`: `74aae99795531a8d45d62ed908a3b47f67be05f9`.
- Review rounds: three. Round 1 recorded the exceptional-refactor contract, round 2 fixed the
  no-trailing-newline line-count boundary, and round 3 reported zero blocking findings.
- Fixer runs: two, both with exact-head verification and PR-posted evidence.
- Final branch verification: `make verify` passed with 26 EditMode tests, 79 PlayMode tests, a
  macOS Apple-silicon Development build, built-player headless smoke, graphical 1920x1080 smoke,
  clean log scans, and `git diff --check`.
- Merged-main proof: `make post-merge` passed at
  `74aae99795531a8d45d62ed908a3b47f67be05f9` with the same complete-match runtime path and clean
  logs.
