# Ashes of Rum - Approved Rapid Prototype Contract

Status: Approved shared understanding, 2026-07-25

Implementation status at approval: Intentionally deferred

Canonical scope: This document governs future planning and implementation unless the user
explicitly changes a decision.

## 1. Purpose And Success Criterion

`Ashes of Rum` is a rapid gameplay prototype, not a production-ready game or a
presentation-polished vertical slice. Development is delivered through small end-to-end
vertical slices, each of which must leave the complete current game playable.

The prototype succeeds when repeated solo playtests demonstrate that its core loop creates
meaningful choices. The player should be able to gather, build, produce, scout, counter,
maneuver, and destroy the enemy Hisar while recognizing consequential decisions they would
change in a subsequent match.

The user is the prototype's sole planned tester. Exact health, damage, sight radius, gather
rate, build time, and similar balance values are tunable implementation parameters rather
than frozen product commitments.

Current development prioritizes a complete and coherent playable loop, responsive
controls, gameplay clarity, reliable AI, and repeatable verification. Story depth, historical
detail, and visual polish are secondary and should receive only the effort required to make
the prototype understandable and enjoyable.

## 2. Game Identity

- Working title: `Ashes of Rum`.
- The setting is fictional and only faintly inspired by the rise of Anatolian beyliks after
  the Seljuk order.
- Do not present the game as a historical reenactment or make historical-accuracy claims.
- The tone is mythic but physically grounded. There are no magic systems or supernatural
  units.
- The fixed player faction is Karasungur, the blue Black Falcons.
- The fixed AI faction is Alazhan, the ember-red Living Flame.
- The factions are mechanically mirrored.
- There are no named individual leaders or battlefield hero units.
- Use plain English terminology with selective regional flavor. The faction citadel and
  victory structure is called the `Hisar`; other approved labels remain immediately readable
  English terms.

## 3. Presentation

- Stylized 3D top-down presentation.
- Readable graybox fidelity using an in-engine primitive kit rather than external art packs.
- Simple procedural motion communicates movement and combat: movement bob, facing, attack
  anticipation, projectile arcs, hit feedback, and casualty removal. Do not introduce
  skeletal-animation scope.
- Friendly and hostile forces use blue and red materials plus shape markers so recognition
  does not rely on color alone.
- Use functional audio feedback for selection, orders, construction, production, attacks,
  hits, warnings, victory, and defeat. Do not add music or voice acting. Avoid licensed
  external audio dependencies.
- The single battlefield is `The Sundered Road`, a dry Anatolian highland with dusty soil,
  sun-bleached grass, dark rock, sparse scrub or pines, ruined masonry, and caravan remains.
- Lighting is neutral static midday light.
- Terrain elevation may add visual depth but has no effect on movement, sight, range, or
  damage.

## 4. Match Contract

- One single-player match against one calibrated AI opponent.
- Target duration: 10-15 minutes for a player who understands the controls.
- There is no hard time limit or score-based fallback. The match continues until a Hisar is
  destroyed.
- Destroying the enemy Hisar is the only victory condition.
- On Hisar destruction, freeze simulation immediately and show victory or defeat, elapsed
  time, and only `Restart` and `Quit` actions.
- Launch directly into the match. Do not build a main menu or skirmish setup screen.
- There is no pause, tactical pause, save, resume, replay, or simulation-speed control.
- There is no separate tutorial or guided onboarding sequence. Normal labels, tooltips, and
  visible hotkey hints are still required UI, not a tutorial.

## 5. Map And Visibility

### 5.1 Layout

- The Sundered Road is symmetric.
- It has one broad overall corridor between bases.
- Soft obstacles create short local left/right route splits within that corridor.
- The terrain should support local flanking without becoming a set of separate strategic
  lanes or hard chokepoints.
- Camera zoom-out should show roughly one-third of the map length, not the entire battlefield.
- Construction placement must preserve at least one navigable route between bases. Buildings
  physically block movement but cannot complete a total wall-off.

### 5.2 Resources

- About one-third of each side's likely match economy is available safely near its starting
  Hisar.
- The remaining supply caches pull workers and armies toward contested central ground.
- Resource nodes are scattered caravan caches containing sacks, tools, timber bundles, and
  trade chests.
- The map contains no neutral civilians, animals, hostile creatures, or capturable camps.

### 5.3 Fog Of War

- Implement full RTS fog with three states: unexplored, explored but not currently visible,
  and currently visible.
- Visibility is radius-based only. Terrain and buildings do not occlude sight.
- Use one shared sight radius rather than role-specific ranges.
- Previously explored terrain remains visible.
- Previously seen enemy buildings remain as dim, stale silhouettes at their last known state.
- Mobile enemy units disappear immediately when they leave current vision.
- The AI knows the map layout and starting locations but obeys current vision for moving
  units and newly constructed buildings.

## 6. Economy

- The prototype has one abstract resource: `Supplies`.
- Design the resource model cleanly enough that Supplies can later be replaced by three or
  more resources, but do not implement unused multi-resource generality now.
- Caravan caches are finite and permanently exhaustible.
- Each cache exposes four efficient worker gathering positions.
- Workers gather a fixed visible batch, carry it to the nearest Hisar or Storehouse, deposit
  it, and return automatically.
- When a cache is depleted, a worker seeks another known and visible cache within a limited
  nearby radius. If none is available, it becomes idle and notifies the player.
- Workers are targetable and killable but have no attack.
- The starting state is one Hisar and four workers, with no starting military formation.
- Starting Supplies pay for exactly one first-tier choice: either one Worker or one House.
- Initial relative cost tiers are:
  - Worker: 1
  - House: 1
  - Storehouse: 2
  - Watchtower: 3
  - Any military formation: 4
- These are ratios, not final numeric prices.

## 7. Population

- Workers consume one population each.
- Each visible living soldier consumes one population.
- Starting population cap: 12.
- Each House adds 8 population capacity.
- Hard cap per side: 60.
- A produced formation begins with eight soldiers and therefore consumes eight population.
- Each casualty immediately frees one population slot.
- Damaged formations cannot reinforce or merge; freed partial capacity may be used toward a
  newly trained full formation once enough capacity is available.

## 8. Buildings And Construction

### 8.1 Hisar

- The Hisar is the starting citadel and victory target.
- It is non-combat and has no defensive attack.
- It trains Workers, Spearmen, Archers, and Cavalry.
- All four types compete for one shared production queue.
- A contextual rally point sends new formations to terrain or sends new workers directly to
  a caravan cache to begin gathering.

### 8.2 Constructible Buildings

- House: raises population cap by 8.
- Storehouse: accepts worker Supply drop-offs and shortens gathering routes.
- Watchtower: automatically attacks one nearby hostile target. It is a basic supporting
  defense, cannot garrison units, and must not dominate assaults.

### 8.3 Placement And Labor

- Building placement is free placement with grid snapping.
- A worker may place a building anywhere currently visible and reachable.
- Placement validation must preserve at least one path between bases.
- One assigned worker constructs a structure. Additional workers cannot accelerate it.
- After completion, the worker resumes its previous valid gathering assignment; otherwise it
  becomes idle.
- Cancelling queued production or in-progress training returns the full Supply cost.
- Cancelling unfinished construction returns the full cost.
- Enemy destruction of unfinished or completed construction gives no refund.
- Completed friendly buildings may be deliberately demolished after confirmation for no
  refund.
- Workers cannot repair buildings.

## 9. Army Model

### 9.1 Formation Roster

The roster is a three-way explicit counter triangle:

- Spearmen counter Cavalry.
- Archers counter Spearmen.
- Cavalry counter Archers.

Counters should be strong but recoverable: at equal Supply value, the counter wins clearly
with meaningful survivors, but superior numbers or a successful flank can overturn the
matchup.

Counter relationships use damage modifiers only. Do not implement charge, brace, volley, or
other automatic or player-triggered special abilities.

### 9.2 Formation Representation

- Every produced formation contains eight visible members.
- Every type uses the same fixed four-wide, two-deep grid.
- The player selects and commands the formation as one object.
- The formation uses shared anchor movement while visible members attack, take damage, and
  disappear as casualties.
- Survivors smoothly re-form into a compact front-to-back block.
- Damaged formations cannot replace members or merge.
- Cavalry moves clearly faster. Spearmen and Archers share one foot speed.

### 9.3 Movement And Collision

- Allied formations use soft avoidance and may compress or overlap temporarily rather than
  deadlock.
- Opposing formations block movement and create a frontline.
- A multi-formation move preserves the group's rough relative layout rather than converging
  every formation on one point or automatically arranging roles.
- Formations face their movement direction and then turn toward their combat target.
- There is no manual rotate command.
- Reorientation takes a short visible fixed duration so flanking has a meaningful window.

## 10. Combat

- Approved combat commands: Move, Attack-Move, Focus Target, and Stop.
- There are no aggression stances.
- Attack-Move and autonomous combat choose the nearest valid hostile formation, then workers,
  then buildings. Manual focus commands override this behavior.
- A focus command targets an entire formation. Member attacks distribute automatically among
  reachable enemy members.
- Combat is fully deterministic.
- Units and buildings use health only. Do not add armor, critical hits, misses, randomized
  damage, morale, or friendly fire.
- Archers must stop to fire.
- Arrows are visible deterministic projectiles and apply damage to their chosen target on
  arrival.
- Formation facing matters. Side and rear attacks receive a modest flank bonus.
- Every military formation can damage structures at the same standardized reduced structural
  rate.
- There are no siege units or improvised siege tools.

## 11. AI Contract

- One difficulty setting: one calibrated opponent.
- The AI obeys the same economy, Supply costs, finite caches, population rules, production
  queue, construction rules, and combat rules as the player.
- The AI receives no income bonus, hidden resource grants, spawned emergency defenders, or
  omniscient targeting.
- Strategy is a fixed build-and-attack script rather than a reactive planner.
- Expected timing targets:
  - Around minute 3: a Cavalry scout and probe reaches the player's side.
  - Around minute 6: a mixed pressure force leaves the AI base.
  - Around minute 10: surviving forces commit to the player's Hisar.
- On an early player attack, the AI recalls nearby available formations to defend its workers,
  buildings, or Hisar, then resumes the script if the threat ends.
- It replaces lost workers up to a scripted workforce target through the real shared queue.
- It rebuilds economy-critical Houses when population-blocked and a Storehouse when gathering
  routes fail.
- It does not endlessly replace destroyed Watchtowers.

## 12. Input And Camera

### 12.1 Selection And Orders

- Target input is desktop mouse and keyboard.
- Standard RTS selection: left-click, drag selection box, Shift selection modification, and
  numeric control groups.
- Double-click selects visible friendly units or formations of the same type within the
  current camera view.
- Production and commands are available through both clickable HUD buttons and visible fixed
  hotkeys.
- Keybindings are not remappable in the prototype.
- Mixed worker and military selections perform their type-valid contextual actions.
- Contextual right-click handles movement, gathering, rally points, and focus targets.

### 12.2 Camera

- Keyboard movement with WASD or arrow keys.
- Screen-edge panning.
- Middle-mouse drag.
- Mouse-wheel zoom.
- No camera rotation.
- Clamp movement to the playable map with a small margin so edge entities can be centered.
- The maximum zoom-out shows about one-third of the map length.

## 13. HUD And Feedback

- Fixed 1920x1080 game window.
- Compact standard RTS HUD:
  - Supplies and population at the top.
  - Selection details and contextual commands at the bottom.
  - Production queue adjacent to contextual commands.
  - Fog-aware minimap in a lower corner.
- The minimap shows explored terrain and currently visible friendly and enemy markers and
  supports click-to-move-camera.
- Health bars appear when an entity is selected, hovered, or damaged.
- Under-attack events produce a throttled audio cue and minimap ping but never steal the
  camera.
- Notify only blocked or idle economy states, such as idle workers, exhausted caches, and
  population-blocked production.
- Do not expose developer debug panels as player HUD.

## 14. Telemetry And Diagnostics

- Persist a local JSON match summary and event log. Do not transmit data over a network.
- Capture at least elapsed time, outcome, Supplies gathered, entities produced and lost,
  buildings constructed and destroyed, first-contact timing, major AI attack timings, and
  Hisar destruction.
- There is no replay system.
- Development-only diagnostics and test controls may exist but must remain outside the player
  HUD and shipped interaction contract.

## 15. Technical Direction

- Use the installed Unity `6000.5.5f1` Editor.
- Create a 3D Universal Render Pipeline project.
- Target a native macOS Apple-silicon development build only.
- Use ordinary GameObjects and MonoBehaviours rather than DOTS/ECS.
- Use ScriptableObjects for unit, building, economy, timing, and AI tuning data.
- Use Unity's Input System.
- Use AI Navigation/NavMesh for movement and navigation, including runtime building
  obstruction and path validation.
- Use Canvas-based runtime UI.
- Use a custom bounded RTS camera rather than adding Cinemachine.
- Do not add networking, cloud services, accounts, monetization, analytics services,
  Addressables, or third-party runtime assets.
- Use the existing Git repository. Git LFS is not required for the primitive graybox asset
  set.
- Avoid speculative abstractions. Only the future replacement of Supplies with multiple
  resources warrants a clean data boundary now; it does not warrant implementing unused
  systems.

## 16. Verification Contract

Implementation is not complete until there is evidence for all of the following:

- Edit-mode tests cover economy arithmetic, population and casualty behavior, production and
  cancellation, counter modifiers, deterministic damage, visibility state, and AI phase
  transitions.
- Play-mode tests cover the gather-deposit loop, building placement and completion, production
  and rallying, formation movement and casualties, combat, fog reveal and memory, AI attacks,
  and Hisar victory or defeat.
- The full relevant test suite passes headlessly.
- A macOS Apple-silicon development build succeeds.
- A live complete match proves the launch-to-result path, including Restart and Quit.
- Match logs are produced and contain the required summary fields.
- Generated folders such as `Library`, `Temp`, `obj`, and builds are excluded from version
  control, while every committed Unity asset is accompanied by its `.meta` file.

## 17. Explicitly Deferred

The rapid prototype does not include:

- Multiple resources.
- Technology tiers or upgrade trees.
- Specialized military production buildings.
- Faction asymmetry.
- Additional factions, maps, biomes, or procedural maps.
- Multiplayer, networking, matchmaking, or local versus play.
- Mobile, Windows, Linux, WebGL, or console targets.
- Magic, supernatural units, hero units, or named leaders.
- Active abilities or automatic unit traits such as charge, brace, or volley.
- Morale, routing, detailed armor classes, accuracy, or friendly fire.
- Reinforcement or merging of damaged formations.
- Selectable formation shapes, combat stances, or manual facing commands.
- Siege units or repair systems.
- Neutral agents, camps, wildlife, or capturable settlements.
- Main menu, skirmish setup, guided tutorial, pause, save, replay, game-speed controls,
  settings, or key remapping.
- Music, voice acting, production-quality art, or public-playtest distribution.
- Monetization, ads, in-app purchases, live operations, accounts, cloud saves, or remote
  services.

## 18. Change Control

Future agents must treat this document as the approved contract, not as a suggestion. If a
new request conflicts with it, identify the affected decision and update this document only
after the user explicitly approves the change.
