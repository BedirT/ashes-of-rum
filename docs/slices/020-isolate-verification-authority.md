# Exceptional Refactor 020 - Isolate Verification Authority

State: In Progress

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

Record the PR, final reviewed head, verification, review/fixer count, merge, and post-merge proof
before marking this corrective refactor complete.

## Implementation

- `VerificationLaunchModeValidator` classifies smoke, scripted-agent, and live-agent launch flags
  before either runtime runner is created. It permits zero or one mode and rejects every multi-mode
  combination with one `VERIFICATION_LAUNCH_CONFLICT` marker before exiting nonzero.
- Focused Edit Mode cases cover all eight zero, single, pair, and triple flag combinations.
- `make verify` launches the built Development player with smoke plus live-agent flags and requires
  a prompt nonzero exit, exactly one conflict marker, and no smoke or live-session artifacts.
