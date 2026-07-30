# Ashes of Rum - Asset Planning

Status: Repository-owned production plan. Authored source-asset preparation was approved on 2026-07-29;
runtime integration still requires one role-sized vertical slice at a time.

Source snapshot: Repository product contract and playable implementation read on 2026-07-28 from
`/Users/bedirt/Documents/rts-game`.

This document is a one-by-one production queue for the models and animations needed by the approved
rapid prototype. A checked source-asset item means its approved files are preserved and validated; it
does not mean the running game uses that asset.

## 1. Important Contract Boundary

`DESIGN.md` now permits repository-owned prototype character, equipment, and skeletal-animation source
assets. The running game remains primitive/procedural until a player-observable integration slice
replaces one complete role safely.

Therefore:

- Models and clips may be preserved under `SourceAssets/` and imported under `Assets/Art/` for
  validation without making them live.
- Models remain stylized, simple, top-down readable, and small enough to replace the graybox without
  expanding the prototype into presentation-polish scope.
- Every runtime integration must preserve gameplay timing, movement, formation behavior, navigation,
  collision, targeting, fog, AI, and complete-match playability.
- Asset availability never authorizes a new ability. Block, dodge, dive, melee, fall, special-shot, and
  similar clips remain unused unless the gameplay contract changes separately.

## 2. Approved Visual Target

- Game: stylized 3D top-down RTS.
- Tone: mythic but physically grounded, with no magic or supernatural elements.
- Setting: a fictional dry Anatolian highland, faintly inspired by the beylik period but not presented
  as a historical reenactment.
- Camera: high three-quarter view. Strong silhouettes matter more than faces or small surface detail.
- Factions are mechanically mirrored and should share meshes wherever possible.
- Karasungur, the Black Falcons: blue cloth, dark accents, white diamond/falcon-shaped marker.
- Alazhan, the Living Flame: ember-red cloth, warm accents, white square/flame-shaped marker.
- Recognition must not rely on color alone. Preserve distinct overhead marker shapes on every mobile
  unit and faction marker shapes on major buildings.
- Neutral environment: dusty earth, sun-bleached grass, dark rock, sparse scrub or pine, weathered
  masonry, and abandoned caravan material.
- Lighting target: neutral static midday.

## 3. Technical Delivery Standard

Apply these rules to every authored asset unless an integration spike later proves a different rule is
needed.

### 3.1 Models

- Work in meters at real-world scale.
- Unity orientation: Y up, +Z forward.
- Place a character pivot at ground level between the feet. Place a mounted-unit pivot at ground level
  beneath the horse's center of mass.
- Place a building pivot at the center of its ground footprint.
- Freeze transforms before export: position zero, rotation zero, scale one.
- Use clean, non-overlapping names and no hidden render meshes.
- Prefer one mesh plus faction-swappable material slots over duplicate faction meshes.
- Keep faction cloth, neutral material, skin, leather/wood, and metal logically separable. Do not create
  many tiny material slots.
- Use URP-compatible PBR texture inputs: base color, normal, and one packed mask only if it materially
  improves readability. Avoid expensive transparency.
- Keep silhouettes chunky and details exaggerated enough to survive the gameplay camera.
- Build only one useful LOD initially. Add LODs only if a measured performance problem requires them.
- Do not bake selection rings, health bars, fog silhouettes, front indicators, or faction colors into
  the main mesh. Those are runtime feedback layers.

### 3.2 Rigs And Animation

- Use one shared humanoid skeleton for Worker, Spearman, Archer, and Cavalry Rider wherever practical.
- Keep a stable bone hierarchy and names after the first approved character export.
- Cavalry may use one export containing linked horse and rider skeletons, but keep horse and rider meshes
  separable so equipment and faction materials can be swapped.
- All locomotion and attack clips are in-place. Unity/NavMesh owns translation and facing.
- Do not use root motion, lateral displacement in hit reactions, or forward displacement in attacks.
- Animate at 30 fps or higher, then export clean key ranges with no unneeded takes.
- Looping clips must have matching first/last poses and no visible foot pop.
- Combat is deterministic. Each attack clip needs a documented contact or projectile-release time so the
  visual can be synchronized to the existing gameplay result without changing damage timing.
- Reorientation is a runtime 0.45-second turn. Do not create a gameplay-affecting turn animation. A
  neutral locomotion/idle blend may play while the runtime rotates the model.
- Attack cadence is currently 0.75 seconds. Each primary attack must read clearly and recover within that
  window.
- Archer projectile flight is currently 0.35 seconds. The arrow release pose must occur before flight
  begins.
- Hit reactions must be short and must not prevent deterministic movement or attacks.
- Death clips end in a stable pose and never move the root. Casualty removal timing will be decided during
  integration.

### 3.3 Export Package Per Asset

Each completed model should have:

- Editable source file.
- Clean export file, preferably FBX for rigged content and FBX or glTF for static content.
- Texture source and exported textures.
- One neutral turntable image from the gameplay camera angle.
- One scale-reference image beside a 1.8 m human or a meter grid.
- A short note listing scale, material slots, triangle count, and known issues.
- For animation sets, a contact sheet or preview video and a clip manifest containing frame ranges,
  looping state, duration, and event timing.

## 4. Master Inventory

The minimum prototype-ready inventory is:

| Group | Unique authored models | Faction variants | Animation sets |
| --- | ---: | ---: | ---: |
| Characters and mounts | 6 | Material/equipment swaps only | 4 gameplay sets plus shared base clips |
| Weapons and carried props | 8 | None | Driven by character bones |
| Buildings | 4 | Material and marker swaps | Static build/destroy states, no skeletal clips |
| Supply cache | 5 modular props | Neutral | Static depletion states |
| Battlefield environment | 12 modular pieces/sets | Neutral | None |
| Gameplay helper meshes | 7 simple meshes | Color/shape variants | Simple procedural motion only |

The count above treats a modular set as one production task even when it contains several closely related
pieces. Detailed tasks follow.

## 5. Character Models And Animation Sets

### CHAR-001 - Shared Humanoid Base And Rig - P0

Purpose: lock proportions, deformation, bone naming, and attachment sockets before making role variants.

Model requirements:

- Stylized adult human with readable head, torso, hands, and feet at RTS distance.
- Approximately 1.75-1.8 m tall.
- Neutral under-layer that will not visibly clip through Worker, Spearman, Archer, or Rider equipment.
- Shared humanoid skeleton with reliable shoulder, elbow, wrist, hip, knee, ankle, spine, neck, and head
  deformation.
- Required sockets/bones: right hand, left hand, back carry, right hip, overhead marker, and optional
  shield/forearm socket.
- Facial rig, fingers, cloth simulation, and hair physics are non-goals.

Shared animation clips:

| Clip | Loop | Target duration | Notes |
| --- | --- | --- | --- |
| `Humanoid_Idle` | Yes | 1.5-2.5 s | Restrained breathing and weight shift. No large weapon motion. |
| `Humanoid_Walk` | Yes | About 1.0 s | In-place, forward only, usable at 3.5-5 m/s through speed scaling. |
| `Humanoid_Hit_Front` | No | 0.20-0.30 s | Small readable recoil, root fixed. |
| `Humanoid_Hit_Left` | No | 0.20-0.30 s | Used for side-hit feedback if integration supports direction. |
| `Humanoid_Hit_Right` | No | 0.20-0.30 s | Mirror of left without root shift. |
| `Humanoid_Hit_Rear` | No | 0.20-0.30 s | Stronger shoulder/torso read for rear flanks. |
| `Humanoid_Death_Front` | No | 0.7-1.0 s | Compact fall that does not overlap multiple formation slots excessively. |
| `Humanoid_Death_Back` | No | 0.7-1.0 s | Optional second deterministic variant, selected by hit direction, not randomness. |

Acceptance check: all four role meshes can bind to this rig without renaming bones or visible deformation
failure in idle, walk, attack pose, hit, and death extremes.

### CHAR-002 - Worker - P0

Player-visible role: non-combat unit that moves, gathers Supplies, carries a visible batch, constructs one
building at a time, becomes idle, takes damage, and dies.

Model requirements:

- Shared humanoid base with simple tunic, trousers, boots, belt, and practical head covering.
- Broad, tool-carrying silhouette distinct from military units.
- Faction cloth material region large enough to read blue or red from above.
- Right-hand tool socket and back-carry socket.
- Carried Supplies prop must be visibly toggled on/off without changing the base mesh.
- White overhead faction marker remains separate and visible.

Worker-specific animation clips:

| Clip | Loop | Target duration | Gameplay mapping |
| --- | --- | --- | --- |
| `Worker_Idle` | Yes | 2.0 s | `Activity.Idle`. May reuse shared idle initially. |
| `Worker_Walk` | Yes | About 1.0 s | Moving, going to cache, returning, and going to construction. |
| `Worker_Gather` | Yes | 0.75 s | `Activity.Gathering`; repeated tool or pickup motion. Root fixed. |
| `Worker_Carry_Idle` | Yes | 1.5-2.0 s | Optional. Use only if carrying posture cannot be an upper-body layer. |
| `Worker_Carry_Walk` | Yes | About 1.0 s | Returning with Supplies. Prop stays attached with no body clipping. |
| `Worker_Construct` | Yes | 0.75-1.0 s | `Activity.Constructing`; hammer/tool contact reads from above. |
| `Worker_Hit_*` | No | 0.20-0.30 s | Reuse shared directional hit clips. |
| `Worker_Death` | No | 0.7-1.0 s | Reuse a shared death if equipment clears the ground. |

Non-goals: worker combat, repair, multiple construction tools, idle personality variants, or gender/body
variants.

Acceptance check: the role is distinguishable from all military silhouettes in blue and red, and gather,
carry, construct, idle, hit, and death states remain readable at maximum intended zoom-out.

### CHAR-003 - Spearman - P0

Player-visible role: four-wide, two-deep foot formation; counters Cavalry; individual members move, thrust,
take directional hits, die, and reform.

Model requirements:

- Shared humanoid with medium spear, helmet/cap, and compact torso protection.
- Spear length should be readable but must not create severe overlap at 1.15 m lateral and 1.35 m depth
  formation spacing.
- No large shield unless a camera test proves it does not hide neighboring members.
- Clear faction cloth panel and separate overhead marker.

Spearman-specific animation clips:

| Clip | Loop | Target duration | Gameplay mapping |
| --- | --- | --- | --- |
| `Spearman_Idle` | Yes | 1.5-2.0 s | Stable ready pose that fits the fixed grid. |
| `Spearman_Walk` | Yes | About 1.0 s | In-place formation movement and regrouping. Spear kept controlled. |
| `Spearman_Thrust` | No | 0.55-0.70 s | Primary member attack. Contact around 40-50% of clip. |
| `Spearman_StructureAttack` | No | 0.55-0.70 s | Optional; reuse thrust if it reads correctly against buildings. |
| `Spearman_Hit_*` | No | 0.20-0.30 s | Shared directional reactions with spear retained. |
| `Spearman_Death` | No | 0.7-1.0 s | Compact fall; spear may remain attached for prototype simplicity. |

Non-goals: brace, charge response, shield wall, alternate attacks, stances, or formation-shape clips.

Acceptance check: eight members can idle, walk, attack, and fall inside the fixed formation without
weapons tangling enough to obscure the formation front.

### CHAR-004 - Archer - P0

Player-visible role: four-wide, two-deep ranged formation; stops before firing visible arrows; counters
Spearmen.

Model requirements:

- Shared humanoid with bow, quiver, simpler/lighter protection, and clear faction cloth.
- Bow readable from top-down view without an excessively wide silhouette.
- Quiver does not need visibly depleting arrows.
- Arrow prop must attach cleanly to bow/hand during the firing clip and become a separate projectile at
  release.

Archer-specific animation clips:

| Clip | Loop | Target duration | Gameplay mapping |
| --- | --- | --- | --- |
| `Archer_Idle` | Yes | 1.5-2.0 s | Bow held safely inside formation footprint. |
| `Archer_Walk` | Yes | About 1.0 s | In-place. Archer must visibly stop before attack. |
| `Archer_AimDrawRelease` | No | 0.60-0.72 s | Release around 0.28-0.38 s; document exact frame. |
| `Archer_StructureShot` | No | 0.60-0.72 s | Normally reuse the standard shot. |
| `Archer_Hit_*` | No | 0.20-0.30 s | Shared directional reactions adapted to bow hand. |
| `Archer_Death` | No | 0.7-1.0 s | Compact fall; bow stays attached unless detaching is trivial. |

Non-goals: volley ability, fire arrows, reload inventory, aim randomness, kneeling, or special shots.

Acceptance check: draw and release remain readable at gameplay zoom, the release frame can synchronize to
the existing 0.35-second projectile, and eight archers do not visually collide in formation.

### CHAR-005 - Cavalry Rider - P0

Player-visible role: fast mounted formation; counters Archers; uses ordinary melee with no charge ability.

Model requirements:

- Shared humanoid adapted to a mounted pose.
- Light/medium mounted equipment with a compact saber or short spear. Prefer saber to avoid implying an
  unimplemented lance charge.
- No flowing cloth simulation.
- Strong faction-colored torso/blanket area and separate overhead marker.
- Hands align with reins and weapon throughout mounted clips.

Rider animation requirements are authored together with CHAR-006 Horse. The rider should not need a
separate independent locomotion controller for the prototype.

### CHAR-006 - Horse - P0

Model requirements:

- Stylized horse with strong body, head, and leg read from above.
- Combined horse-and-rider footprint should stay close to the existing mounted member footprint and fit
  the same four-wide, two-deep grid without persistent overlap.
- Faction-swappable saddle blanket. Horse coat can remain neutral.
- Tack should be modeled simply enough to avoid animation clipping.
- Mane/tail use rigid or bone-driven motion only. No cloth/hair simulation.

Mounted animation clips:

| Clip | Loop | Target duration | Gameplay mapping |
| --- | --- | --- | --- |
| `Cavalry_Idle` | Yes | 2.0-3.0 s | Horse and rider settle in a compact footprint. |
| `Cavalry_Run` | Yes | 0.65-0.9 s | In-place, used for 5.25 m/s movement. A trot is optional, not required. |
| `Cavalry_SaberAttack` | No | 0.55-0.70 s | Ordinary melee. Contact around 40-50%; no forward root lunge. |
| `Cavalry_StructureAttack` | No | 0.55-0.70 s | Reuse saber attack if contact direction reads against structures. |
| `Cavalry_Hit_Front` | No | 0.20-0.30 s | Small combined horse/rider response, root fixed. |
| `Cavalry_Hit_Side` | No | 0.20-0.30 s | Readable flank response without lateral displacement. |
| `Cavalry_Hit_Rear` | No | 0.20-0.30 s | Stronger rear read without bucking or gameplay interruption. |
| `Cavalry_Death` | No | 0.9-1.2 s | Compact deterministic collapse; keep within neighboring slots. |

Non-goals: charge, rear, jump, mounted archery, dismount, separate horse health, gait simulation, or rider
ragdoll.

Acceptance check: the horse and rider never separate, feet do not slide excessively at fixed movement
speed, and a formation of eight mounted members remains readable without filling the whole corridor.

## 6. Weapons And Carried Props

These are separate meshes so they can be attached, hidden, or reused.

| ID | Asset | Priority | Requirements | Animation |
| --- | --- | --- | --- | --- |
| PROP-001 | Worker hand tool | P0 | One simple mattock/hammer hybrid suitable for gather and build poses. | Driven by right hand. |
| PROP-002 | Carried Supply bundle | P0 | Compact sack/bundle that reads gold-brown and can toggle on the worker's back. | Driven by back socket. |
| PROP-003 | Spear | P0 | Medium, thick silhouette; safe length for formation spacing. | Driven by both hands/right hand. |
| PROP-004 | Bow | P0 | Simple recurve silhouette; deformation/bow-string rig only if cheap and reliable. | Driven by archer hands. |
| PROP-005 | Quiver | P0 | Static filled quiver; no depletion. | Driven by back socket. |
| PROP-006 | Arrow projectile | P0 | Thickened shaft/head for top-down visibility; pivot and +Z axis documented. | Runtime projectile arc; no clip. |
| PROP-007 | Cavalry saber | P0 | Compact curved blade; no ornate detail. | Driven by rider right hand. |
| PROP-008 | Watchtower projectile | P1 | Readable stone/bolt-sized projectile matching grounded technology. | Runtime projectile arc; no clip. |

## 7. Buildings

Both factions use the same architecture and footprints. Faction identity comes from material accents and
marker/banner shape, not mechanical-looking structural differences.

### BLD-001 - Hisar - P0

Gameplay role: starting citadel, worker/Spearman/Archer/Cavalry production source, Supply drop-off, rally
origin, and sole victory target. It is non-combat.

Model requirements:

- Ground footprint: approximately 5.2 x 4.2 m including collision clearance.
- Aim/read height: roughly 3-5 m, with a strong keep silhouette.
- One obvious front/drop-off side.
- Large faction cloth/marker socket visible from the camera.
- No weapon emplacements or defensive attack implication.
- Production happens abstractly; doors need not open and units need not emerge from an interior.

Static states:

1. `Hisar_Complete` - required.
2. `Hisar_Destroyed` - P1 compact ruins or collapsed top. The match freezes immediately on destruction,
   so an elaborate collapse animation is unnecessary.

Animation: none required. Damage flash and final destruction can remain procedural.

### BLD-002 - House - P0

Gameplay role: adds 8 population.

Model requirements:

- Approximate visual footprint: 3.6 x 3.6 m.
- Modest dwelling silhouette with a readable roof.
- No implied production, garrison, or worker entrance functionality.

Static construction/destruction states:

1. `House_Foundation` - footprint and stakes.
2. `House_HalfBuilt` - partial wall/roof structure.
3. `House_Complete` - required final mesh.
4. `House_Rubble` - compact destroyed state.

Animation: none. Integration should swap/reveal states based on construction progress. Do not author an
expensive bespoke build animation.

### BLD-003 - Storehouse - P0

Gameplay role: Supply drop-off that shortens worker gathering routes.

Model requirements:

- Approximate visual footprint: 3.8 x 3.8 m.
- Clear open storage silhouette with sacks, timber, or chests visible from above.
- One obvious drop-off side and prop placement that does not block the worker arrival point.

Static states: `Storehouse_Foundation`, `Storehouse_HalfBuilt`, `Storehouse_Complete`, and
`Storehouse_Rubble`.

Animation: none. Supplies do not need to visibly accumulate or deplete.

### BLD-004 - Watchtower - P0

Gameplay role: basic automatic supporting defense; targets one nearby hostile; cannot garrison and must
not visually promise garrison controls.

Model requirements:

- Approximate base footprint: 3 x 3 m, with platform around 3.2 x 3.2 m.
- Height around 4 m so the projectile origin is obvious.
- One clear projectile launch socket.
- No visible crew required for the prototype.

Static states: `Watchtower_Foundation`, `Watchtower_HalfBuilt`, `Watchtower_Complete`, and
`Watchtower_Rubble`.

Animation: none required. A tiny procedural recoil is optional only during integration. Do not make a
skeletal turret or targeting system.

### Shared Building Marker Set - P0

- `Marker_Karasungur_DiamondFalcon`: white/dark diamond or simplified falcon silhouette.
- `Marker_Alazhan_SquareFlame`: white/warm square or simplified flame silhouette.
- Must read in silhouette and grayscale, not only by color.
- Use the same marker language on Hisar, constructible buildings where visible, and mobile units.

## 8. Supply Cache Set

### RES-001 - Caravan Supply Cache - P0

Gameplay role: finite neutral resource node with four efficient worker gather positions. It permanently
exhausts.

The cache is assembled from:

| ID | Modular prop | Quantity/variation target |
| --- | --- | --- |
| RES-002 | Supply sack | 2 simple shapes or rotations |
| RES-003 | Tool bundle | 1 compact bundle |
| RES-004 | Timber bundle | 1 tied stack |
| RES-005 | Trade chest | 1 readable chest |
| RES-006 | Broken cart base | Optional P1 anchor for larger contested caches |

Static node states:

1. `Cache_Full` - all major props visible.
2. `Cache_Low` - fewer props, optional if depletion feedback can use one step.
3. `Cache_Empty` - scraps/empty ground marker that remains recognizable as exhausted.

Constraints:

- Keep four clear gather points around the node at roughly 1.45 m from center.
- Keep collision within approximately 2.8 x 2 m.
- No pickup animation on the cache itself.
- Avoid coins, magical crystals, or one specific raw material. The resource is abstract Supplies.

## 9. Battlefield Environment Kit

The Sundered Road is one symmetric broad corridor with short local left/right route splits. Environment
assets must support readability and navigation rather than create multiple strategic lanes or hard
chokepoints.

| ID | Asset/set | Priority | Minimum contents | Gameplay constraint |
| --- | --- | --- | --- | --- |
| ENV-001 | Ground/road material set | P0 | Dusty soil, worn road, dry grass blend | Broad readable corridor; neutral midday values. |
| ENV-002 | Dark rock small | P0 | 3 rotations/silhouettes | Decoration or soft edge, not micro-obstacles. |
| ENV-003 | Dark rock large | P0 | 2 cluster silhouettes | May define short route splits; collision must be simple. |
| ENV-004 | Dry scrub | P1 | 3 low clumps | Prefer non-blocking decoration. |
| ENV-005 | Sun-bleached grass | P1 | 3 low clumps/cards | Sparse and low contrast under units. |
| ENV-006 | Sparse highland pine | P1 | 2 silhouettes | Use sparingly; canopy must not hide formations. |
| ENV-007 | Ruined wall straight | P0 | 2-4 m modular section | Soft obstacle; never complete a wall-off. |
| ENV-008 | Ruined wall corner/end | P0 | Corner plus broken end | Compose short local route splits only. |
| ENV-009 | Fallen masonry pile | P1 | 2 piles | Low enough not to hide units. |
| ENV-010 | Broken caravan cart | P0 | One wheel/cart silhouette | Establish setting; collision kept simple. |
| ENV-011 | Caravan debris | P1 | Wheel, axle, cloth roll, crate | Decoration around caches/road edges. |
| ENV-012 | Roadside marker/standing stone | P2 | 1-2 shapes | Visual orientation only, no lore text required. |

Environment animation: none.

Non-goals: animals, civilians, neutral enemies, capturable camps, animated foliage, wind simulation,
weather, water, multiple biomes, terrain gameplay bonuses, destructible scenery, or unique landmarks that
break map symmetry.

## 10. Gameplay Helper Meshes

These are intentionally simple and may remain engine-generated. They should not block character/building
production.

| ID | Helper | Required behavior |
| --- | --- | --- |
| UI3D-001 | Formation selection ring | Flat ring under each selected member, faction/selection color. |
| UI3D-002 | Worker selection ring | Flat ring sized to worker footprint. |
| UI3D-003 | Building selection ring | Flat ring/outline sized to building footprint. |
| UI3D-004 | Formation front indicator | Small white ground marker clearly showing formation facing. |
| UI3D-005 | Move/order marker | Brief ground marker for move, gather, and hostile focus orders. |
| UI3D-006 | Rally marker | Ground marker at Hisar rally destination. |
| UI3D-007 | Placement preview | Translucent building mesh or footprint with valid/invalid color. |

Do not model world health bars, fog overlays, minimap icons, drag-selection boxes, or HUD panels as 3D
assets. They remain UI/runtime systems.

## 11. Material And Texture List

Create a compact shared library rather than unique textures for every object.

1. `MAT_FactionCloth_Karasungur` - readable blue, with dark Black Falcon accents.
2. `MAT_FactionCloth_Alazhan` - ember red, with warm Living Flame accents.
3. `MAT_FactionMarker_WhiteDark` - high-contrast overhead markers.
4. `MAT_Skin` - one neutral stylized range.
5. `MAT_LeatherWood` - shared equipment, bows, handles, tack.
6. `MAT_Metal` - restrained low-gloss weapons and helmets.
7. `MAT_Stone_Plaster` - Hisar and building walls.
8. `MAT_RoofTimber` - roofs, platforms, structural wood.
9. `MAT_Supplies` - sacks, tools, timber bundle, chest.
10. `MAT_DustRock` - road, soil, and dark highland stone variants.
11. `MAT_DryVegetation` - grass, scrub, and pine colors.

Avoid faction-specific UV layouts. The same mesh should switch between blue and red materials.

## 12. One-By-One Production Queue

Work in this order so each completed task validates assumptions needed by the next one.

### Phase A - Lock Scale And Pipeline

- [ ] 1. CHAR-001 Shared Humanoid Base And Rig.
- [ ] 2. PROP-003 Spear as the first hand/socket and scale test.
- [ ] 3. CHAR-003 Spearman model.
- [ ] 4. Spearman idle, walk, thrust, hit, and death clips.
- [ ] 5. Export an eight-member four-wide/two-deep formation preview at 1.15 m x 1.35 m spacing.

Stop and review the camera-angle preview before making the other roles. If the silhouette, scale, weapon
length, or formation density is wrong, fix the shared base now.

### Phase B - Complete The Unit Roster

- [ ] 6. CHAR-002 Worker plus PROP-001 tool and PROP-002 carried bundle.
- [ ] 7. Worker idle, walk, gather, carry-walk, construct, hit, and death clips.
- [x] 8. CHAR-004 Archer source package plus separate bow and arrow props. The quiver remains body-worn;
  see `SourceAssets/Archer/`.
- [x] 9. Archer idle, walk, release/recoil, hit, and death clips. Runtime integration uses the reviewed
  `Idle`, `WalkForward`, `AimRecoil`, `HitFront`, and `DeathBackward` subset with root motion disabled,
  a socketed bow, and projectile release remaining code-owned. The other preserved Mixamo clips remain
  source-only.
- [ ] 10. CHAR-006 Horse model and rig.
- [ ] 11. CHAR-005 Cavalry Rider plus saber.
- [ ] 12. Combined mounted idle, run, attack, hit, and death clips.
- [ ] 13. Karasungur and Alazhan material/marker variants across all four roles.
- [ ] 14. Top-down readability sheet showing both factions and every role at near and far gameplay zoom.

### Phase C - Gameplay-Critical World Models

- [ ] 15. BLD-001 Hisar complete model and both faction markers/material variants.
- [ ] 16. BLD-002 House complete model.
- [ ] 17. BLD-003 Storehouse complete model.
- [ ] 18. BLD-004 Watchtower complete model plus projectile.
- [ ] 19. Foundation, half-built, and rubble states for House, Storehouse, and Watchtower.
- [ ] 20. Hisar destroyed state.
- [ ] 21. RES-001 Supply Cache with full, low, and empty arrangements.

### Phase D - Battlefield Readability

- [ ] 22. ENV-001 ground and road material set.
- [ ] 23. Large/small dark rocks.
- [ ] 24. Ruined wall straight, corner, end, and masonry pile.
- [ ] 25. Broken caravan cart and debris.
- [ ] 26. Dry scrub and grass.
- [ ] 27. Sparse pine.
- [ ] 28. Roadside marker only if the battlefield still needs orientation cues.

### Phase E - Integration-Readiness Audit

- [ ] 29. Verify every asset's units, pivot, +Z forward, transforms, material slots, and names.
- [ ] 30. Verify every animation is in-place, loops cleanly where required, and documents contact/release
  frames.
- [ ] 31. Verify blue/red and diamond/square identification in normal view, grayscale, and fog silhouette.
- [ ] 32. Verify eight-member formations have no severe clipping in idle, movement, combat, death, and
  regrouping previews.
- [ ] 33. Verify buildings fit current approximate footprints and preserve clear worker drop-off/build
  points.
- [ ] 34. Verify environment obstacle kits can create short route splits without hiding units or producing
  hard chokepoints.
- [ ] 35. Package source, export, textures, previews, and manifests for every role. Archer source,
  production exports, and manifests are now in the repository; other roles remain pending.

## 13. Per-Asset Completion Checklist

Use this checklist for every numbered production item:

- [ ] Silhouette reads from the actual top-down camera angle.
- [ ] Scale matches the reference grid and neighboring gameplay assets.
- [ ] Pivot, axes, and transforms follow the delivery standard.
- [ ] Faction material areas are large enough to read but do not replace shape markers.
- [ ] Geometry has no obvious holes, flipped normals, duplicate internal faces, or accidental loose parts.
- [ ] UVs and textures are clean at intended screen size.
- [ ] Collision suggestion is simple and documented; render geometry is not assumed to be navigation
  geometry.
- [ ] Animation has no root translation, foot pop, prop clipping, or unintended first/last-frame jump.
- [ ] Contact/release frame is documented for attacks.
- [ ] Source, export, textures, turntable, scale image, and notes are present.
- [ ] No deferred feature is implied, such as charge, brace, volley, repair, garrison, siege, hero, magic,
  or faction asymmetry.

## 14. Explicitly Not Needed

Do not spend modeling or animation time on:

- Heroes, named leaders, officers with gameplay roles, or faction-exclusive units.
- Siege engines, ladders, rams, or improvised siege tools.
- Charge, brace, volley, stance, morale, routing, reinforcement, or formation-shape animations.
- Worker combat or building repair animations.
- Hisar combat weapons.
- Garrison interiors or enter/exit animations.
- Neutral civilians, animals, wildlife, hostile creatures, or capturable camps.
- Multiple maps, biomes, seasons, weather, water, or night lighting.
- Main-menu models, cinematic props, cutscenes, dialogue portraits, lip sync, or facial animation.
- Production-quality destruction simulation, ragdolls, cloth, hair, or physics-driven accessories.
- Music, voices, or licensed art/audio dependencies.

## 15. Future Integration Slice Boundary

When the refactor is merged and the user wants these assets in the game, integration should be split into
small player-observable slices rather than one art dump. A safe sequence is:

1. Use the approved repository source-asset contract in `DESIGN.md`; do not treat asset intake as runtime
   integration.
2. Integrate one shared rig plus Spearman model/animations while preserving formation behavior, selection,
   hit feedback, casualties, fog, tests, build, and complete-match playability.
3. Integrate Worker states and verify gather/carry/build behavior end to end.
4. Integrate Archer and projectile-release synchronization.
5. Integrate Cavalry horse/rider while preserving its only mechanical difference: faster movement and
   the counter triangle.
6. Integrate buildings and cache depletion states without changing footprints, NavMesh behavior, combat,
   placement, or economy rules.
7. Integrate the minimal environment kit and re-prove path preservation, camera readability, fog, and the
   complete match.

Each integration slice should leave the entire current game runnable and playable. Art replacement must
not change combat timing, collision, navigation, population, targeting, visibility, or AI unless that
behavioral change is separately approved.
