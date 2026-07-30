# Repository Intake

## Folder Contract

```text
SourceAssets/<Role>/
  README.md
  ANIMATION_MANIFEST.md
  References/
    Turntable/
    ScaleReference/
    AnimationPreviews/
  Meshy/
  Editable/

Assets/Art/Characters/<Role>/
  Model/
    <Role>.fbx
    Textures/
  Animations/
  Equipment/
```

`SourceAssets/` preserves approved generation references and original authoring exports without making
Unity import them. `Assets/` contains only production exports Unity must import.

Preserve an editable source, neutral gameplay-camera turntable, scale reference, and accepted-animation
preview/contact sheet when they exist. Record any missing deliverable explicitly in `README.md`; do not
invent one merely to make the folder complete.

Never move `.DS_Store`, temporary Blender files, failed uploads, rejected animations, throwaway preview
renders, or duplicate with-skin FBXs into the repository.

## Naming

- Canonical model: `<Role>.fbx`
- Animation: `<Role>_<Motion>.fbx`
- Equipment: `<Role>_<Item>.fbx`
- Texture: `<Role>_<Item?>_<Map>.png`

Preserve vendor filenames only in the manifest's provenance table.

## Unity Import Intent

Canonical humanoid model:

- Animation Type: Humanoid
- Avatar Definition: Create From This Model
- enforce T-pose when required
- import embedded material only as a temporary inspection aid
- prefer repository PBR textures and explicit URP materials during integration

Animation-only FBXs:

- Animation Type: Humanoid
- Avatar Definition: Copy From Other Avatar
- Source: canonical role Avatar
- Import Materials: off
- Import Cameras/Lights: off
- Bake root rotation, root position Y, and root position XZ into pose
- Loop Time/Loop Pose: on only for accepted loops
- one normalized clip name matching the FBX filename

PBR textures:

- `*_Normal.png`: Normal Map texture type with sRGB disabled
- `*_Metallic.png` and `*_Roughness.png`: Default texture type with sRGB disabled
- base-color and emission textures: retain color-data sRGB import semantics

Do not create an Animator Controller during archive/intake work. Controller states, layers, parameters,
events, equipment sockets, and gameplay wiring belong to the later role-integration slice.

Apply importer settings with the repository's `CharacterAssetImportSetup` Editor command and the exact
CLI invocation in `SKILL.md`. Never hand-edit generated `.meta` YAML. Treat manual Inspector setup as a
diagnostic fallback, not an accepted repository intake path.

## Verification

1. Inspect every FBX with `scripts/inspect_fbx.py` and require the same skeleton fingerprint.
2. Import through Unity and retain every generated `.meta` file.
3. Inspect the Unity import log for Avatar, clip, texture, and serialization warnings.
4. Run the exact skill-validation commands in `SKILL.md`, then `make verify` and `make pr-ready` at the
   committed HEAD.
5. Confirm the running game is unchanged until the dedicated integration slice.
