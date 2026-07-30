# Meshy Model Workflow

## Character Construction

- Use one character mesh without held weapons or shields.
- Preserve permanent worn items, including quivers, sheaths, belts, and pouches. Removing them from
  the character usually creates more attachment and deformation work than it saves.
- Generate bow, arrow, spear, shield, sword, and other held equipment separately.
- Use a symmetrical relaxed A-pose with visible separation at armpits, wrists, legs, and accessories.
- Avoid crossed limbs, hands touching the torso, hidden feet, overlapping straps, or loose cloth that
  obscures joints.

## Generation Choice

Meshy model names and behavior can change. Check current official documentation when choosing between
models. Evaluate the resulting topology and silhouette rather than assuming the newer label wins.

- Prefer multiview generation when approved front, back, left, and right images exist. It has the best
  chance of preserving asymmetric worn equipment and rear construction.
- Prefer a controllable-polycount generator when a single-view test produces a visibly cleaner mesh
  and multiview consistency is not the limiting risk.
- Do not remesh automatically. First inspect the generated topology, triangle count, silhouette, UVs,
  and likely shoulder/hip deformation.

Starting budgets for the prototype:

| Asset | Initial target |
| --- | ---: |
| Foot soldier including worn equipment | 8,000 triangles |
| Complex foot soldier upper bound before measured justification | 12,000 triangles |
| Bow or shield | 1,000-3,000 triangles |
| Spear, sword, or arrow | 300-1,500 triangles |

These are intake targets, not immutable runtime budgets. Preserve a chunky readable silhouette before
small surface detail.

## Textures And UVs

- Keep base color, normal, metallic, roughness, and emission maps when generated.
- Do not request a separate UV unwrap merely because Meshy offers it. A textured model already has UVs.
- UV unwrap is useful before external painting or when inspection finds overlaps, stretching, or an
  unusable atlas.
- Preserve the original PBR maps even when Mixamo receives a simplified diffuse material. Reapply the
  full material in Unity during runtime integration.

## Pre-Rig Check

- One humanoid character with two arms, two legs, one head, and no detached body parts.
- Feet grounded, human scale documented, transforms clean.
- No armature or skin weights in the intended auto-rig upload.
- Worn items connected closely enough to follow the body without becoming accidental floating islands.
- Held items absent and available as separate models.
- Texture paths resolve from a fresh isolated import.
