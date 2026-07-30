# Documentation And Asset Workflow 022 - Establish The Authored Archer Pipeline

State: Complete

## User-Approved Product Change

The user explicitly directed the completed external Archer concept, model, equipment, rig, and
Mixamo animation work into the normal repository after the exceptional refactor finished. The user
also directed the complete process to become the repository-owned character-production skill,
including animation preparation and intake.

This is a documentation-and-asset workflow change, not a gameplay slice. It approves storing and
validating authored prototype character assets and skeletal clips in the repository. Runtime use
still requires a later player-observable integration slice.

## Preserved Player-Visible Paths

The current primitive formation members, procedural movement and attack feedback, deterministic
combat timing, projectile timing, selection, formation behavior, fog, AI, and complete match remain
unchanged. No scene, prefab, Animator Controller, runtime script, tuning value, or asset reference is
replaced in this change.

## Repository Outcome

The repository contains one reviewed source of truth for producing and accepting authored units:
the actionable asset plan, the reusable concept-to-Meshy-to-Mixamo skill, and the approved Archer
model/equipment/animation package with provenance and clip intent documented.

## Acceptance Checks

- The repository-owned skill covers concept approval, consistent multiview prompts, detachable
  equipment, Meshy model preparation, Mixamo auto-rig preparation, download settings, animation
  selection, repeatable Unity Humanoid import, and acceptance review.
- The skill records the failures learned here: keep worn equipment, separate held equipment, prefer
  multiview generation before remeshing, do not assume a Blender-valid material is Mixamo-valid,
  and use legacy diffuse OBJ packaging only when an unrigged FBX is misclassified.
- The approved rigged Archer and every file from `archer_animation_pack.zip` are unpacked with
  normalized deterministic names and a manifest distinguishing required, optional, and rejected
  gameplay motions.
- The approved bow and arrow exports and their texture maps are stored beside the Archer.
- The approved concept and four model-generation views are preserved as source references outside
  Unity's runtime asset tree.
- `ASSET_PLANNING.md` becomes the repository production queue and marks the completed Archer
  preparation work truthfully without claiming runtime integration.
- Unity imports every new production asset, generates all `.meta` files, and reports no import,
  missing-reference, compilation, or serialization errors.
- Unity imports normal maps as normal-map textures and treats normal, metallic, and roughness maps as
  linear PBR data through the reusable role intake command.
- The skill passes its structural validator and a context-light forward test can recover the
  intended next-character workflow from the skill alone.
- The full tests, macOS ARM64 Development build, runtime smoke paths, log scans, and static checks
  remain green.

## Non-Goals

- No runtime Archer replacement, prefab, runtime Avatar assignment, Animator Controller, animation
  event, bow socket, arrow visibility system, projectile synchronization, or scene edit.
- No gameplay, timing, balance, movement, collision, navigation, formation, fog, AI, input, HUD,
  audio, package, or verification-protocol change.
- No import of rejected Blender animation experiments, rejected Meshy clips, failed Mixamo upload
  packages, duplicate with-skin animation meshes, or temporary previews.
- No Spearman, Worker, Cavalry, building, environment, faction-material, or production-polish work.
- No claim that all 39 downloaded Mixamo motions belong in the final runtime Animator Controller.

## Manual Play Check

Launch the current macOS Development player through the existing verification harness, complete the
normal smoke path, and inspect logs. The expected result is the same complete primitive/procedural
game with no changed presentation or controls; this proves the source-asset archive has not silently
entered the shipped runtime.

## Evidence

- PR: [#36](https://github.com/BedirT/rts-game/pull/36)
- Final reviewed head: `906143eeb3c73335d31c3d7849dc446b00b13a5d`.
- Review: round 1 found incorrect PBR data-texture semantics; fixer run 1 made normal maps
  `NormalMap`/linear, made metallic and roughness maps linear, and added regression coverage. Round 2
  found that the example importer command could clear 14 valid loop flags; fixer run 2 introduced the
  role-generic `ANIMATION_IMPORT.json` contract and a real 39-clip rerun test. Fresh round 3 reported
  no blocking findings. Two fixer runs were used.
- Asset validation: the skill validator and context-light forward test passed; Blender 5.2 confirmed
  one 4,262-vertex/7,952-polygon skinned model and 39 meshless motion FBXs sharing one 49-bone
  fingerprint; Unity confirmed one valid Humanoid Avatar, 39 copied-Avatar motion importers, all 17
  declared loop mappings, and correct PBR data-texture semantics.
- Exact-head verification: 49/49 Edit Mode, 92/92 Play Mode, macOS ARM64 Development build, every
  required headless and graphical gameplay smoke, complete-match Restart and Quit, both adaptive live
  runs, clean logs, static checks, and evidence/hash validation passed.
- Merge: squash-merged as `93be1c75d70f1281c9347f6a38bef135a2e4f453`; the feature branch was
  deleted.
- Post-merge: `make post-merge` passed on merged `main` at that exact SHA with the same complete ladder,
  proving the procedural/primitive shipped game remained unchanged and playable.
