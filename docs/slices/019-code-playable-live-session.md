# Exceptional Refactor 019 - Make Agent Play Live And Adaptive

State: Complete

## User-Approved Exception

The user explicitly directed completion of all remaining code-playability work. This is an
exceptional refactor and development-verification extension, not a gameplay slice, network feature,
save system, replay system, or remote agent platform.

## Preserved Player-Visible Paths

The complete mouse-and-keyboard match and every shipped gameplay rule remain unchanged. Scripted
agent fixtures, real gathering, construction, production, fog, combat, AI, Hisar outcomes, Restart,
Quit, telemetry, and immutable frame evidence remain authoritative and playable.

## Agent-Visible Outcome

One Development player remains alive while an external local process alternates one versioned JSON
request with one immutable fog-safe JSON response, chooses later commands from the returned state,
recovers after ordinary command rejection, and optionally captures exact state-paired 1920x1080
frames. Structured state is sufficient to play; pixels remain review evidence only.

## Acceptance Checks

- Live mode is opt-in, Development-build-only, local filesystem based, and mutually exclusive with
  the existing pre-authored script mode.
- A fresh session publishes its identity and readiness, then processes only the exact next
  sequence-numbered request after atomic publication; requests and responses remain inspectable.
- Every request preserves schema version, session identity, sequence, and unique request ID. Stale,
  malformed, oversized, conflicting, duplicate, or out-of-order input cannot execute twice.
- Ordinary gameplay rejection and wait timeout return structured rejection codes without ending the
  live session, so the next command can correct the action.
- Live and scripted modes share the existing semantic command executor, fog-safe projector,
  lifecycle gates, wait conditions, capture path, stable entity IDs, and telemetry handling.
- State exposes static map bounds and a compact row-major `U`/`E`/`V` fog-cell encoding derived only
  from the public player fog map. It does not expose hidden caches, mobile actors, construction, or
  structure updates.
- Restart preserves the live session sequence while rebinding to fresh match state; Quit remains
  result-gated and uses the shipped Quit path; `end_session` remains diagnostic-only termination.
- A built-player client discovers Worker/cache/building IDs from returned observations rather than
  assuming them, branches on returned state, corrects at least one rejected command, constructs a
  real House, and captures immutable graphical state/frame evidence.
- Every prior scripted code-playability scenario, the complete real-economy lifecycle, normal
  gameplay, tests, native build, and clean-log checks remain green.

## Non-Goals

- No gameplay, balance, AI, fog rules, input, HUD, art, audio, package, or simulation changes.
- No sockets, HTTP server, cloud service, remote access, authentication system, or third-party
  transport dependency.
- No privileged spawning, Supply credits, direct damage, time jumps, visibility overrides, AI
  suspension, reflection dispatch, arbitrary method calls, or omniscient state.
- No save/load, rollback, replay, networking, multiplayer, or generalized agent framework.
- No second static complete-match fixture; Slice 018 remains the lifecycle proof.

## Verification Path

- Launch a native Development player with a fresh live mailbox and wait for `ready.json`.
- Send atomic sequence-numbered requests and wait for their matching immutable responses.
- Discover a living Worker and visible cache from the returned state, deliberately issue and recover
  from one invalid command, gather real Supplies, branch on returned Supplies, and construct a House.
- Discover the new building ID from a later response, wait for completion, center the camera, capture
  exact state/frame artifacts, validate hashes and fog rows, then terminate with `end_session`.
- Run both headless and graphical adaptive client proofs plus the complete existing verification
  ladder at the exact committed SHA.

## Evidence

Record the PR, exact reviewed HEAD, verification summary, review round, fixer count, merge, and
post-merge result here before closing the exceptional refactor.

## Implementation

- `--agent-live-dir` starts a Development-build-only, fresh local mailbox session and is rejected
  when combined with `--agent-script`. Atomic `ready.json`, numbered inbox requests, and numbered
  outbox responses preserve the full inspectable exchange. Readiness advertises the artifacts and
  final-result paths only after the match controller is available; terminal `result.json` records
  response count, checkpoint manifests, termination, and completed-match telemetry hashes.
- Live commands deserialize to the existing semantic step contract and reuse its executor,
  fog-safe projector, waits, captures, lifecycle gates, Restart rebind, and shipped Quit path.
- Player state now includes static map bounds and deterministic row-major `U`/`E`/`V` fog RLE
  projected directly from `FogOfWar.Map`; the projection participates in `stateHash`.
- `scripts/agent-live-client` adaptively discovers IDs, recovers from a deliberate rejection,
  gathers real Supplies, branches on observed state, constructs and discovers a House, centers and
  captures it, validates immutable evidence, and ends the diagnostic session.
- Truthful readiness advertises the immutable inbox, outbox, artifact, and atomic result paths only
  after the live match controller exists. The terminal result records processed responses,
  checkpoint manifests, failure reason, and completed-match telemetry hashes where available.
