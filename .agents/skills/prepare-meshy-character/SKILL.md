---
name: prepare-meshy-character
description: Create and approve an Ashes of Rum character concept, produce consistent Firefly multiview and equipment references, generate and validate Meshy models, prepare a clean Mixamo rig, select and download animation clips, and package approved source assets into the Unity repository. Use for designing the next unit, generating Meshy-ready views, separating weapons, choosing Meshy generation or remesh settings, fixing Mixamo uploads or textures, collecting a character animation set, or preparing authored character assets for a later runtime integration slice.
---

# Prepare A Meshy Character

Turn one unit role into an approved, traceable character package. Preserve player control over art
direction and paid generation. Stop before runtime integration unless the user explicitly approves a
separate player-observable integration slice.

## 1. Recover The Contract

1. Read repository `AGENTS.md`, `DESIGN.md`, `ASSET_PLANNING.md`, and the current role implementation.
2. Identify the unit's existing gameplay silhouette, formation footprint, movement speed, attack
   timing, held equipment, projectile behavior, and explicit non-features.
3. State the role-specific asset outcome, acceptance checks, and non-goals before generating or
   moving files.
4. Keep production-quality polish subordinate to top-down readability and a playable prototype.

## 2. Approve The Concept

1. Gather only role, silhouette, clothing/armor, palette, worn equipment, held equipment, handedness,
   and placement details not already settled by project references.
2. Generate one full-body concept with the image-generation tool. When a style reference exists,
   require its exact visual language instead of describing a competing style.
3. Show the concept and remain at the approval gate until the user explicitly says `approved`,
   `perfect`, `use this one`, or equivalent.
4. Regenerate when the user identifies a flaw. Do not treat partial praise as approval.
5. After approval, record exact proportions, restrained face/eye treatment, palette, materials,
   clothing, armor, every asymmetric item, and handedness.

## 3. Prepare Production References

Read [references/prompt-patterns.md](references/prompt-patterns.md), then produce one independent
copy-ready prompt for each of these:

- front A-pose;
- back A-pose;
- character-left A-pose;
- character-right A-pose;
- each detachable asset by itself.

Keep body-worn attachments such as quivers, scabbards, sheaths, belts, pouches, and straps on the
character. Remove held equipment such as bows, arrows, spears, shields, and swords and generate each
as a separate asset. Use identical white-background, neutral-lighting, no-shadow, full-body framing
for all character views.

Do not request a collage or turntable sheet. Generate and approve each view separately when the user
asks Codex to make the production images.

## 4. Generate And Validate Meshy Assets

Read [references/meshy-model-workflow.md](references/meshy-model-workflow.md).

1. Confirm the credit cost immediately before any paid Meshy tool call and obtain an explicit `go
   ahead`, `generate`, or equivalent authorization for that quoted cost.
2. Decide required output formats before generation.
3. Prefer the current Meshy multiview model when the four approved views are available. Use a
   controllable-polycount model only when its tested mesh is visibly better for this character; do
   not choose solely from marketing labels.
4. Target approximately 8,000 triangles for one foot soldier, then judge silhouette and deformation.
   Remesh only when the result materially exceeds the target or deforms poorly.
5. Do not run UV unwrap on an already textured, correctly mapped result. Run it only when a fresh UV
   layout is required for external texturing.
6. Download the textured unrigged character and every detachable asset. Keep the original Meshy
   source export and PBR texture maps outside Unity's `Assets/` tree.
7. Check scale, Y-up orientation, ground pivot, UVs, material references, geometry, and detached-item
   completeness before rigging.

## 5. Rig In Mixamo

Read [references/mixamo-animation-workflow.md](references/mixamo-animation-workflow.md).

1. Upload only the character. Keep bow, arrow, spear, shield, and other held assets separate.
2. Try the clean unrigged textured FBX first.
3. If Mixamo incorrectly enters existing-skeleton mapping, prepare an OBJ, MTL, and diffuse texture
   ZIP. Use matching lowercase names and a strict legacy diffuse material; Blender successfully
   loading a modern MTL is not proof Mixamo will load it.
4. Place Mixamo's chin, wrist, elbow, knee, and groin markers at anatomical joints and use no finger
   bones unless gameplay demonstrably requires fingers.
5. Download the canonical character once with `FBX for Unity`, `T-pose`, `30 FPS`, and `Keyframe
   Reduction: None`. This is the only with-skin file.
6. Download later motions without skin at 30 FPS with no keyframe reduction. Enable `In Place` for
   locomotion when offered.

## 6. Select The Role's Animation Set

Read the role-specific table and non-goals in `ASSET_PLANNING.md`. Collect the smallest set that maps to
the role's current runtime states, usually idle, locomotion, primary action, compact hit, and compact
death. Record unavailable or unsuitable motions instead of substituting a clip that implies a new
ability. Keep alternatives as optional source only when they plausibly serve an existing state.

For Archer, this means a restrained bow-ready idle, forward walk, forward run if runtime needs it,
draw/aim/release with a clear release frame, compact hit, and compact death. The left hand holds the bow
and the right hand draws. Keep the bow and temporary nocked arrow outside the animation FBX. Gameplay
owns bow attachment, arrow visibility, projectile spawn, damage timing, root translation, and facing.

For other roles, replace the Archer example with their planned equipment, handedness, contact timing,
and existing state mapping. Reject dodge, dive, block, falling, traversal, alternate attacks, or stances
unless that role's gameplay contract already contains them. Do not let an available animation imply a
new ability.

Preview every accepted clip on the actual character with its equipment attached. Reject foot sliding,
hand drift, broken wrists, torso intersections, bow/quiver clipping, an unclear release, excessive root
motion, or a footprint-breaking fall.

## 7. Package The Repository Intake

Read [references/repository-intake.md](references/repository-intake.md).

1. Keep approved concepts, multiview references, original model exports, and authoring textures under
   `SourceAssets/<Role>/`.
2. Put only Unity-importable production exports under `Assets/Art/Characters/<Role>/`.
3. Normalize filenames to `<Role>_<Motion>.fbx`; never keep vendor names such as `mixamo.com` as the
   repository clip identity.
4. Record original filenames, source service, skeleton fingerprint, duration, loop status, intended
   gameplay use, optional status, and rejection reason in the character manifest.
5. Run the bundled Blender inspection script against the canonical model and all animations. Resolve
   Blender from `PATH`, falling back to the standard macOS application location:

   ```bash
   BLENDER="$(command -v blender || printf '%s' /Applications/Blender.app/Contents/MacOS/Blender)"
   "$BLENDER" --background --factory-startup \
     --python .agents/skills/prepare-meshy-character/scripts/inspect_fbx.py -- \
     Assets/Art/Characters/<Role>/Model/<Role>.fbx \
     Assets/Art/Characters/<Role>/Animations/*.fbx
   ```

6. Require one skinned model, animation-only motion FBXs, and one identical skeleton fingerprint within
   the role package. Before reusing clips across roles, separately test Avatar compatibility and
   deformation; independent Mixamo auto-rigs are not automatically a shared production skeleton.
7. Import through Unity so all `.meta` files exist, then run the repository importer with the exact
   comma-separated loop names from the role manifest:

   ```bash
   ROLE="Archer"
   LOOP_MOTIONS="Idle,WalkForward,RunForward"
   unity run "$PWD" --timeout 600 -- \
     -executeMethod AshesOfRum.Editor.CharacterAssetImportSetup.Configure \
     -characterRole "$ROLE" \
     -loopMotions "$LOOP_MOTIONS" \
     -logFile "Logs/${ROLE}AssetImportSetup.log"
   ```

   The command configures the model as Humanoid with a T-pose Avatar and every motion as Humanoid using
   that Avatar, with root rotation/height/XZ baked into pose and loop flags set only for the supplied
   names. Treat a nonzero exit or an unknown loop name as an intake failure.
8. Run the repository's focused checks and complete verification ladder. Inspect import and runtime
   logs for missing textures, avatar errors, clip warnings, or serialization failures.

Validate this skill after edits with:

```bash
python3 "${CODEX_HOME:-$HOME/.codex}/skills/.system/skill-creator/scripts/quick_validate.py" \
  .agents/skills/prepare-meshy-character
PYTHONPYCACHEPREFIX=/tmp/ashes-of-rum-pycache python3 -m py_compile \
  .agents/skills/prepare-meshy-character/scripts/inspect_fbx.py
```

## 8. Runtime Integration Boundary

Repository intake does not make the character live. Runtime integration needs its own small vertical
slice that wires one role end to end: prefab/model, materials, sockets, Animator, gameplay-state
parameters, attack event timing, hit/death feedback, tests, build, and hands-on play proof.

Do not alter collision, navigation, formation spacing, movement speed, attack cadence, projectile
flight, damage, targeting, fog, AI, or population merely to accommodate an asset. Fix the asset or
author a separately approved gameplay change.

## Approval Gates

- Concept approval before production views.
- Production-view approval before paid 3D generation.
- Mesh and texture approval before rigging.
- Rig/deformation approval before purchasing or collecting a large animation set.
- Actual-character animation review before marking any clip integration-ready.
- Explicit integration-slice approval before changing the running game.
