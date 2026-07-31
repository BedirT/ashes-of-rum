# Slice 024 - Establish the World Concept Kit

## Player Outcome

This is a documentation and source-asset workflow, not a gameplay slice. It provides the complete,
dimension-targeted visual reference kit needed to replace the prototype's building, resource, and
battlefield grayboxes one player-observable integration slice at a time. The concept images are not
scale proof; their declared meter targets must be enforced when models are created and imported.

## Acceptance

- [ ] Every approved building state, dedicated Supply-cache asset, and Sundered Road environment set has
      one repository-owned concept image and a declared world-space size; optional `RES-006` explicitly
      reuses `ENV-010` rather than duplicating the broken-cart concept or future model.
- [ ] Buildings contain only the building or its attached functional contents, with no people, flags,
      detached anchors, foreground clutter, labels, or scenery.
- [ ] Concepts share the approved stylized top-down RTS language and remain readable from a high
      three-quarter camera.
- [ ] A manifest and contact sheets account for all 33 unique outputs and prove that the inventory is
      complete, non-duplicative, and visually coherent; a checked-in recipe records each sheet's exact
      row-major filename order.
- [ ] A deterministic target-dimension board pairs the exact delivered complete-state images with one
      common meter grid and a 1.8 m reference, without claiming that concept perspective proves scale.
- [ ] Generated images pass file, dimension, and image-integrity validation.
- [ ] `git diff --check` passes and the existing game remains unchanged and runnable.
- [ ] A context-free review covers the exact final HEAD.
- [ ] The three ground/road panels are described only as non-seamless art-direction swatches; authoring
      and validating tileable terrain materials remains deferred to runtime integration.

## Non-Goals

- Creating or integrating runtime meshes, textures, materials, prefabs, collision, or navigation.
- Character, weapon, projectile, UI-helper, faction-marker, animation, audio, VFX, or destruction VFX.
- Animals, civilians, water, weather, multiple biomes, unique landmarks, or decorative content outside
  the approved rapid-prototype inventory.
- Generating one image per minor rotation when one modular-set sheet communicates the approved variants.

## Manual Play Check

Launch the existing game and confirm that its complete match remains unchanged. Review the generated
building, resource, and environment contact sheets at full size, checking silhouettes, camera
readability, historical tone, and absence of detached accidental geometry. Review the target-dimension
board as an implementation brief for intended proportions, not as validation of generated-image scale.
Treat the ground/road panels as palette and surface-character direction, not as tileable textures.

## Evidence

Record the PR, final reviewed HEAD, generated-image validation, contact-sheet paths, review round,
fixer count, and merge result here before closing the slice.
