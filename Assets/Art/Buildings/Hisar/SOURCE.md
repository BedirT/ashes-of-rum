# Authored Hisar Assets

The four FBX models and their source PBR textures were supplied by the project owner on
2026-07-31 from `Downloads/Ashes of Rum/buildings/hisar/models/0.zip` through `3.zip`.

The numbered exports map to these game-facing states:

| Export | State |
| --- | --- |
| `1.zip` | Foundation |
| `0.zip` | Raised frame |
| `2.zip` | Canvas installation |
| `3.zip` | Complete |

Each committed mask map is derived from its supplied metallic and roughness maps: metallic is
stored in red and inverted roughness (smoothness) in alpha for URP/Lit. The hostile complete
base color is a selective red recolor of the supplied teal textile panels. Geometry and all
other texels remain unchanged. The development preview uses the three intermediate meshes;
normal matches instantiate only the complete mesh.

The first two semantic assignments intentionally follow the imported 3D geometry rather than
the numbered concept thumbnails: export 1 is the flat laid-out foundation, while export 0 has
the raised posts and working deck. This produces the physically coherent in-game sequence.
