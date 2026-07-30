# Mixamo Rig And Animation Workflow

## Upload Order

1. Try the textured unrigged FBX.
2. If Mixamo reports that it cannot map an existing skeleton, verify the file truly has no armature,
   skin groups, or modifiers.
3. If a verified unrigged FBX is still misclassified, upload an OBJ, MTL, and diffuse texture ZIP.

OBJ cannot carry a skeleton, so the ZIP forces the marker-based auto-rig path. The ZIP must contain the
OBJ, MTL, and referenced texture at its root.

## Legacy Diffuse Fallback

Mixamo's material reader is older than Blender's current PBR exporter. A model can look correct in
Blender and still appear glossy white in Mixamo. Use matching lowercase names such as `archer.obj`,
`archer.mtl`, and `archer.jpg`. Convert the diffuse image to an 8-bit sRGB JPEG without alpha or embedded
profiles. Use a minimal MTL:

```mtl
newmtl archer
Ka 1.000000 1.000000 1.000000
Kd 1.000000 1.000000 1.000000
Ks 0.000000 0.000000 0.000000
Ns 1.000000
d 1.000000
illum 2
map_Kd archer.jpg
```

The OBJ must contain both `mtllib archer.mtl` and `usemtl archer`. Do not include normal, metallic,
roughness, emission, absolute paths, spaces, or modern material extensions in the fallback package.

## Marker Placement

- Chin: centered below the jaw.
- Wrists and elbows: anatomical joint centers, not glove or sleeve ends.
- Knees: joint centers visible from the front.
- Groin: body center at the hip crease.
- Prefer no finger bones for this RTS prototype.

## Download Settings

Canonical character download:

- `Format: FBX for Unity (.fbx)`
- `Pose: T-pose`
- `Frames per Second: 30`
- `Keyframe Reduction: None`
- with skin

Every motion download:

- `Format: FBX for Unity (.fbx)`
- without skin
- `Frames per Second: 30`
- `Keyframe Reduction: None`
- `In Place` enabled when the motion exposes it

Keep one with-skin character. Repeated with-skin downloads duplicate mesh and materials and complicate
Avatar reuse.

## Role Review

Use the role's clip table and non-goals in `ASSET_PLANNING.md`. The following checks are the Archer
example, not a universal animation list:

- Idle: restrained and loopable with the left hand able to hold the bow.
- Walk/run: in place, stable torso, no bow-arm swing that crosses the body.
- Attack: left hand stable, right hand reaches/draws/releases, and release frame identifiable.
- Hit: compact, no root displacement, bow hand remains plausible.
- Death: ends in a stable compact pose that does not cover adjacent formation slots excessively.

The animation service does not know the actual detached bow or arrow geometry. Judge the motion again on
the real character with equipment attached.
