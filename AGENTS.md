# Agent Operating Contract

This file defines the required delivery workflow for every agent working in this repository.
The words **must**, **always**, and **never** are intentional.

## 1. Read The Product Contract First

Before planning, designing, reviewing, or implementing anything, read the repository-root
[`DESIGN.md`](DESIGN.md) in full. It is the canonical product contract.

- Preserve its locked decisions and explicit deferrals unless the user explicitly changes
  them.
- Treat values identified as tunable as implementation parameters, not product commitments.
- Update `DESIGN.md` in the same PR whenever the user approves a product decision change.
- Do not silently expand the rapid prototype into production or presentation-polish scope.

## 2. Optimize For A Fun, Complete, Playable Prototype

Current priorities, in order:

1. A complete core loop that is genuinely playable and fun.
2. Correct, responsive controls and clear gameplay feedback.
3. Reliable simulation, AI, tests, builds, and iteration tooling.
4. Only the story and visuals required to make gameplay readable.

Story depth, historical detail, decorative content, and visual polish are not current goals.
Never trade a working gameplay loop for more lore, art, animation, effects, or architecture.

## 3. Deliver Only Vertical Slices

Every change must be framed and delivered as the smallest useful **vertical slice**: an
end-to-end player-observable improvement that starts and ends with a runnable game.

A valid slice must:

- add or improve a behavior a player can reach in the running game;
- include the minimum data, runtime logic, input, feedback, and integration needed for that
  behavior to work end to end;
- include focused automated coverage for its rules and integration seams;
- preserve every previously playable path; and
- leave the game runnable and playable when merged.

Do not create horizontal infrastructure-only PRs, disconnected framework layers, placeholder
APIs, or batches of unintegrated systems. Infrastructure is allowed only when it is the
minimum necessary part of the same slice and is exercised by that slice.

Before editing, write down the slice's player-visible outcome, acceptance checks, and explicit
non-goals. Keep the slice small enough to complete, review, fix, and merge as one PR.

Documentation-only and repository-workflow changes may be delivered without a player-visible
gameplay behavior, but they must not claim to be gameplay slices and must still follow the PR,
review, verification, and merge process below.

## 4. Unity CLI Is The Primary Development Interface

Use the repository's Unity CLI skill at
[`/.agents/skills/unity-cli/SKILL.md`](.agents/skills/unity-cli/SKILL.md) as the authoritative
command reference. Read the relevant reference file before using a command group.

- Prefer `unity` CLI commands for Editor discovery, project creation, opening, importing,
  running, testing, building, logs, diagnostics, and Editor integration.
- Use machine-readable `--format json` output when a command result must be parsed.
- Use `unity run`, `unity test`, and `unity build` rather than inventing shell wrappers around
  the Editor binary.
- Invoke the Editor binary directly only when the repository skill documents a required
  exception, such as asynchronous Unity Package Manager operations that cannot run with
  `-quit`.
- Manage Unity packages through `UnityEditor.PackageManager.Client` as documented in
  [`/.agents/skills/unity-package-management/SKILL.md`](.agents/skills/unity-package-management/SKILL.md).
  Never hand-edit `Packages/manifest.json` to install or remove packages.
- Do not substitute manual Unity Editor clicking for a repeatable CLI operation. Use the GUI
  only for visual inspection, hands-on play, or an operation the CLI genuinely cannot perform.

## 5. The Game Must Run After Every Change

**A change is not complete, stageable, committable, or publishable unless the current game
runs properly after that change.**

After every coherent code, asset, scene, package, or configuration change:

1. Import or refresh through Unity so scripts compile and all required `.meta` files exist.
2. Run the most focused relevant automated tests.
3. Launch the affected playable path through Unity CLI and verify the player-visible behavior.
4. Check logs for new exceptions, assertion failures, missing references, import failures, or
   navigation errors.
5. Fix any regression before starting another slice step.

Do not defer runtime integration until the end of a PR. Compilation alone is not runtime
proof, and tests alone are not playability proof.

Before opening or updating a PR, run the full applicable verification ladder:

1. Unity import and compilation.
2. Focused Edit Mode tests.
3. Focused Play Mode tests.
4. The full relevant test suite headlessly.
5. A macOS Apple-silicon development build.
6. A hands-on smoke test of the complete current game, including all previously merged
   slices and the new behavior.
7. A clean log inspection and `git diff --check`.

When a Unity project does not exist yet, run every verification step that is actually
available and state which Unity gates are structurally unavailable. The project-bootstrap PR
must create the first runnable scene and establish the full run/test/build loop.

## 6. Branch And Commit Discipline

- Start each slice from an up-to-date `main` unless the user explicitly specifies another
  base.
- Create a dedicated branch named `agent/<short-slice-name>`.
- Never develop a slice directly on `main`.
- Inspect `git status`, the diff, and the active branch before staging.
- Stage only files belonging to the slice. Never absorb unrelated user changes.
- Commit Unity assets together with their `.meta` files.
- Never commit `Library`, `Temp`, `obj`, logs, or build output.
- Use concise commits that describe the working slice, not implementation activity.

## 7. Every Slice Requires A Proper Pull Request

After the slice is complete and the full verification ladder is green:

1. Push the branch to `origin`.
2. Open a **ready-for-review**, non-draft PR targeting `main`.
3. Do not ask the user to create, review, merge, or close the PR.

The PR body must contain:

- the player-visible outcome;
- why this slice is valuable now;
- the implementation scope and explicit non-goals;
- automated test commands and results;
- build command and result;
- hands-on runtime proof;
- known risks or consciously deferred follow-ups; and
- confirmation that the game remains playable at the branch head.

Never open a PR for a branch that does not run properly.

## 8. Mandatory Context-Free PR Review

Immediately after opening the PR, the primary agent must spawn a review sub-agent with **no
prior conversation context**. Use a no-history fork such as `fork_turns="none"`.

Give the reviewer only:

- the repository path;
- the PR URL or number;
- the base and head branches;
- an instruction to read `AGENTS.md` and `DESIGN.md` from the repository;
- an instruction to independently inspect the complete diff and run proportionate checks;
  and
- an instruction to post its review directly on the PR.

Do not give the reviewer the implementation rationale, expected verdict, prior debugging
history, or a summary that could bias the review.

The reviewer must:

- act only as a reviewer and not edit the branch;
- prioritize correctness, regressions, playability, contract violations, missing tests, and
  unsafe assumptions;
- cite actionable findings with file and line references;
- distinguish blocking findings from non-blocking suggestions;
- post the full review to the PR using `gh pr review --comment`, `gh pr comment`, or equivalent
  GitHub tooling; and
- post an explicit `no blocking findings` verdict when clean.

A review that exists only in agent chat does not count. It must land on the PR itself.

## 9. Review-Fix Loop: Maximum Three Fixer Runs

The primary agent orchestrates the loop. A **round** consists of one context-free PR review
followed, when the review has actionable findings, by one fixing sub-agent run. Never invoke a
fixing agent more than three times for one PR.

Spawn the fixing sub-agent with the repository path, PR URL or number, current round number,
and an instruction to read `AGENTS.md`, `DESIGN.md`, and the posted PR review. The fixing agent
must work only on the PR branch, address every valid in-scope finding, add regression coverage,
run the game and required checks, commit, push, and post its response and verification evidence
to the PR. It must not broaden the slice.

For each round:

1. Read the review from the PR, not from hidden agent context.
2. If there are actionable findings, invoke one fixing sub-agent for the round.
3. Inspect the fixer's diff, commits, PR response, and verification evidence.
4. Independently run the game after the fixes and repeat the full applicable verification
   ladder.
5. If another review is warranted, spawn a **fresh context-free reviewer** for the next round.

Stop early when a review reports no blocking findings and all required gates are green. Never
run more than three fixing-agent invocations for one PR.

After the third fixer run, do not invoke a fourth fixer. The primary agent must perform the
final green-gate audit and decide the terminal action. It may request one final context-free
review only if that review cannot trigger a fourth fixer run; otherwise proceed directly to
the terminal decision using the posted findings and green-gate evidence.

## 10. Merge Is The Required Normal Outcome

Slice sizing and implementation quality must make merge achievable within at most three
fixing-agent runs. The expected terminal state of every completed slice PR is a squash merge,
not an indefinitely open or abandoned PR.

Merge the PR only when:

- all known blocking review findings are resolved;
- required checks and the full applicable verification ladder are green;
- the branch is current enough with `main` to merge safely; and
- the branch head has hands-on proof that the complete game remains playable.

Use the repository's available GitHub tooling to squash-merge and delete the branch. A merged
PR is closed by GitHub and satisfies the close requirement.

Never merge known broken code merely to satisfy the expected merge outcome. If an external
blocker, platform outage, inaccessible credential, or unresolved correctness defect makes a
safe merge impossible after three rounds:

1. post the exact blocker and evidence on the PR;
2. preserve `main` as the playable branch;
3. close the PR without merge; and
4. report the exception clearly to the user.

This is an exceptional failure path, not an alternative delivery strategy.

## 11. Post-Merge Proof

After merging:

1. switch to `main` and fast-forward from `origin/main`;
2. confirm the PR is merged and the remote branch is deleted;
3. run the applicable smoke test from merged `main`;
4. inspect logs and repository status; and
5. report the PR URL, review rounds, merge commit, tests, build, and runtime proof.

If merged `main` fails its smoke test, treat it as an urgent regression and immediately create
the smallest corrective vertical-slice PR through this same workflow.

## 12. Definition Of Done

A slice is done only when all of the following are true:

- its player-visible acceptance checks pass;
- every previously merged playable path still works;
- automated tests and the required build are green;
- runtime logs are clean of new errors;
- the PR contains the verification evidence;
- at least one context-free review is posted on the PR;
- all known blocking findings are resolved;
- the PR is merged, or the documented exceptional failure path was followed; and
- merged `main` is confirmed playable.
