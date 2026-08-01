# Slice 027 - Review textured building models

## Player-visible outcome

A repeatable built-player presentation shows the newly named textured House, Storehouse, and Watchtower beside one eight-member authored Archer group and the existing Hisar. The review reports the exact rendered bounds, while the live playable match continues to use its existing building presentation.

## Acceptance checks

- Preserve and name each downloaded FBX and its five supplied texture maps in its building-specific art folder.
- Import the meshes as static, Y-up URP assets with no cameras, lights, or animation.
- Apply each supplied base-color and normal map to a dedicated URP Lit material, ground every mesh, and keep scaling uniform.
- Review the House at 3.0 m, Storehouse at 3.0 m, and Watchtower at 4.0 m. The preview reports their exact footprints for the owner to compare with the current gameplay footprints before any live integration.
- Produce a 1920x1080 screenshot and machine-readable dimensions for all three models beside the existing scale references.
- Run focused Edit Mode and Play Mode coverage, import, a native Apple-silicon development build, and the built-player review.

## Non-goals

- No change to House population rules, Storehouse drop-off behavior, Watchtower combat, building construction states, or gameplay collision.
- No change to the existing live building visuals, map layout, faction marker layer, or game balance.
- No mesh stretching, texture repainting, emitted lighting, or unapproved visual-polish scope.
