# Exceptional Refactor 020 - Isolate Verification Authority

State: Complete

## User-Approved Exception

The user directed completion of every remaining code-playability gap. The final independent audit
found that privileged smoke automation and player-authority agent modes can be enabled together.
This is a narrow exceptional corrective refactor, not a gameplay slice.

## Preserved Player-Visible Paths

Normal mouse-and-keyboard gameplay, privileged smoke verification when launched alone, scripted
agent verification when launched alone, and adaptive live agent play when launched alone remain
unchanged.

## Authority Outcome

A Development player fails closed before any verification runner starts when `--smoke-test` is
combined with `--agent-script` or `--agent-live-dir`. Privileged spawning, credits, time control,
AI control, hidden-cache mutation, or direct damage can therefore never affect an agent observation
or action session.

## Acceptance Checks

- One shared launch-mode validator recognizes smoke, scripted-agent, and live-agent modes.
- The validator permits no mode and exactly one mode, but rejects every multi-mode combination.
- Both runtime initialization entry points consult the validator before creating a runner or
  mutating the match.
- A native Development player launched with smoke plus live-agent flags exits nonzero, emits one
  explicit conflict marker, and creates no smoke result, live readiness, response, or result.
- Normal smoke, scripted agent, live adaptive agent, complete-match lifecycle, tests, build, and
  log checks remain green.
- Slice 019 records its exact PR, review/fixer, merge, and post-merge evidence.

## Non-Goals

- No gameplay, balance, AI, fog, input, HUD, art, audio, package, or protocol behavior changes.
- No new verification mode, privileged command, remote endpoint, or security/authentication layer.
- No redesign of existing smoke or agent runners beyond the shared fail-closed launch guard.

## Evidence

- PR: [#33](https://github.com/BedirT/rts-game/pull/33)
- Final reviewed head: `5f9c536825196359c074871a32362961a94ecb83`
- Review: round 1 reported no blocking findings; no fixer run was needed. The reviewer also launched
  all four invalid built-player mode combinations and confirmed each exited with status 2, emitted
  exactly one conflict marker, and created no verification outputs.
- Exact-head verification: 46/46 Edit Mode, 92/92 Play Mode, macOS ARM64 Development build,
  launch-mode conflict proof, every normal/scripted headless and graphical smoke, both
  complete-match lifecycle runs, both adaptive live runs, clean logs, static checks, and
  artifact/hash validation passed.
- Merge: squash-merged as `9d691889c6fd5ba2739e170941716b5c08a12712`; the branch was deleted.
- Post-merge: the first merged-main ladder exposed an unrelated complete-match scouting predicate
  defect, which corrective Slice 021 fixed. `make post-merge` then passed the full ladder on the
  merged-main descendant `5720b32672e1f97a2c88040f865f957efc03fbbc`, including the launch-mode
  conflict proof and every isolated verification mode.

## Implementation

- `VerificationLaunchModeValidator` classifies smoke, scripted-agent, and live-agent launch flags
  before either runtime runner is created. It permits zero or one mode and rejects every multi-mode
  combination with one `VERIFICATION_LAUNCH_CONFLICT` marker before exiting nonzero.
- Focused Edit Mode cases cover all eight zero, single, pair, and triple flag combinations.
- `make verify` launches the built Development player with smoke plus live-agent flags and requires
  a prompt nonzero exit, exactly one conflict marker, and no smoke or live-session artifacts.
