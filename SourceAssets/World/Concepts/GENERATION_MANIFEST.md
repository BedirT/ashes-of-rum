# Ashes of Rum World Concept Generation Manifest

This is the complete rapid-prototype world-art inventory derived from `DESIGN.md` and
`ASSET_PLANNING.md`. Every row is required. Sizes are world-space production targets; isolated concept
framing is not evidence of scale. Runtime models must be normalized to these measurements in Unity.

## Shared Visual Contract

- High three-quarter orthographic-style presentation on a plain warm neutral background.
- Stylized low-poly, hand-painted PBR appearance with chunky gameplay-readable silhouettes.
- Dry Anatolian highland palette: cream felt, dark timber, restrained teal textile, dusty earth,
  sun-bleached vegetation, dark rock, and weathered masonry.
- Neutral static midday light and generous empty margins.
- One isolated asset or explicitly declared modular set per image.
- No people, animals, flags, text, labels, watermarks, dramatic scenery, cast-off foreground clutter,
  floating parts, or detached elements unless the row explicitly defines a debris set.

## Buildings

| ID | Required concept | Target world size | Output |
| --- | --- | --- | --- |
| BLD-001A | Hisar complete | Render mesh within 5.0 x 4.0 m; 3.4-3.8 m tall; 5.2 x 4.2 m clearance | `Buildings/Hisar_Complete.png` |
| BLD-001B | Hisar destroyed | Same 5.0 x 4.0 m footprint; collapsed below 1.6 m | `Buildings/Hisar_Destroyed.png` |
| BLD-002A | House foundation | 3.6 x 3.6 m; below 0.5 m | `Buildings/House_Foundation.png` |
| BLD-002B | House half-built | 3.6 x 3.6 m; below 2.2 m | `Buildings/House_HalfBuilt.png` |
| BLD-002C | House complete | 3.6 x 3.6 m; about 2.6 m tall | `Buildings/House_Complete.png` |
| BLD-002D | House rubble | Within 3.6 x 3.6 m; below 0.8 m | `Buildings/House_Rubble.png` |
| BLD-003A | Storehouse foundation | 3.8 x 3.8 m; below 0.5 m | `Buildings/Storehouse_Foundation.png` |
| BLD-003B | Storehouse half-built | 3.8 x 3.8 m; below 2.2 m | `Buildings/Storehouse_HalfBuilt.png` |
| BLD-003C | Storehouse complete | 3.8 x 3.8 m; about 2.8 m tall | `Buildings/Storehouse_Complete.png` |
| BLD-003D | Storehouse rubble | Within 3.8 x 3.8 m; below 0.8 m | `Buildings/Storehouse_Rubble.png` |
| BLD-004A | Watchtower foundation | 3.0 x 3.0 m; below 0.5 m | `Buildings/Watchtower_Foundation.png` |
| BLD-004B | Watchtower half-built | 3.0 x 3.0 m base; below 2.8 m | `Buildings/Watchtower_HalfBuilt.png` |
| BLD-004C | Watchtower complete | 3.0 x 3.0 m base, 3.2 m platform; about 4.0 m tall | `Buildings/Watchtower_Complete.png` |
| BLD-004D | Watchtower rubble | Within 3.2 x 3.2 m; below 0.9 m | `Buildings/Watchtower_Rubble.png` |

## Supplies

| ID | Required concept | Target world size | Output |
| --- | --- | --- | --- |
| RES-001A | Full caravan cache | Within 2.8 x 2.0 m; four clear gather sides | `Resources/Cache_Full.png` |
| RES-001B | Low caravan cache | Same footprint with visibly fewer Supplies | `Resources/Cache_Low.png` |
| RES-001C | Empty caravan cache | Same ground signature; below 0.35 m | `Resources/Cache_Empty.png` |
| RES-002 | Two supply-sack variants | Each about 0.45 x 0.35 x 0.55 m | `Resources/Supply_Sacks.png` |
| RES-003 | Compact tool bundle | About 0.8 x 0.35 x 0.25 m | `Resources/Tool_Bundle.png` |
| RES-004 | Tied timber bundle | About 1.2 x 0.55 x 0.45 m | `Resources/Timber_Bundle.png` |
| RES-005 | Trade chest | About 0.8 x 0.55 x 0.55 m | `Resources/Trade_Chest.png` |

## Battlefield Environment

| ID | Required concept/set | Target world size | Output |
| --- | --- | --- | --- |
| ENV-001 | Dusty soil, worn road, dry-grass surface set | Three non-seamless art-direction swatches | `Environment/Ground_Road_Materials.png` |
| ENV-002 | Three small dark-rock silhouettes | Each below 0.8 m diameter and 0.45 m tall | `Environment/Small_Dark_Rocks.png` |
| ENV-003 | Two large dark-rock clusters | Each 3-5 m long and below 1.8 m tall | `Environment/Large_Rock_Clusters.png` |
| ENV-004 | Three dry-scrub clumps | Each below 1.0 m diameter and 0.7 m tall | `Environment/Dry_Scrub.png` |
| ENV-005 | Three sun-bleached grass clumps/cards | Each below 0.8 m diameter and 0.45 m tall | `Environment/Dry_Grass.png` |
| ENV-006 | Two sparse highland-pine silhouettes | 3-4.5 m tall; narrow non-obscuring crowns | `Environment/Highland_Pines.png` |
| ENV-007 | Straight ruined-wall modular set | 2 m and 4 m lengths; below 1.5 m tall | `Environment/Ruined_Wall_Straight.png` |
| ENV-008 | Ruined-wall corner and broken end set | Within 2.5 m modules; below 1.5 m tall | `Environment/Ruined_Wall_Corner_End.png` |
| ENV-009 | Two fallen-masonry piles | Each below 2.0 m diameter and 0.65 m tall | `Environment/Fallen_Masonry_Piles.png` |
| ENV-010 | Broken caravan cart | Within 2.8 x 1.8 m; below 1.5 m | `Environment/Broken_Caravan_Cart.png` |
| ENV-011 | Caravan-debris modular set | Wheel, axle, cloth roll, crate; individually separated | `Environment/Caravan_Debris.png` |
| ENV-012 | Two roadside-marker/standing-stone shapes | 1.0-1.8 m tall; no writing | `Environment/Roadside_Markers.png` |

## Explicitly Engine-Generated Or Out Of Scope

Faction markers, selection rings, order markers, rally markers, placement previews, fog, minimap assets,
health bars, projectiles, characters, weapons, animals, civilians, water, weather, and unique landmarks
are not part of this concept batch.

## Completion Evidence

- Required isolated concept outputs: 33 of 33.
- Building review sheet: `Buildings_ContactSheet.png`.
- Supply review sheet: `Resources_ContactSheet.png`.
- Battlefield review sheet: `Environment_ContactSheet.png`.
- Deterministic contact-sheet recipe and row-major filename order:
  `rebuild_contact_sheets.sh`.
- Art-direction notes and provenance limits: `GENERATION_SUMMARY.md`.
- Deterministic target-dimension review: `World_Target_Dimensions.png` (rendered from
  `World_Target_Dimensions.svg`).
- The target board uses the exact four delivered complete-state images and plots their declared footprints,
  heights, and a 1.8 m reference on a common meter grid. It is an implementation brief, not proof that
  perspective concept art has physical scale. The numeric targets above remain authoritative for Meshy
  cleanup and Unity import, where scale must be validated on actual geometry.
- Re-render the board from its exact source-image links with
  `(cd SourceAssets/World/Concepts && rsvg-convert -o World_Target_Dimensions.png World_Target_Dimensions.svg)`.
- `Environment/Ground_Road_Materials.png` establishes palette, value, and surface character only. Its
  three panels are not tileable production materials. Authoring and validating genuinely seamless terrain
  textures is explicitly deferred to the later player-observable runtime integration slice.
