# Ashes of Rum Prototype Roadmap

This roadmap sequences work as end-to-end vertical slices. Every completed slice must
leave `main` buildable, launchable, and playable at its current scope. Slice order may
change when playtesting exposes a stronger dependency.

## Slice Queue

| State | Slice | Player-observable outcome |
| --- | --- | --- |
| Complete | 000 - Bootstrap automated harness | Import the Unity project, run tests, build a native macOS player, launch a neutral scene, and exit through deterministic smoke. |
| Complete | 001 - Establish and command the starting economy | Launch with one Hisar and four workers, select workers, gather Supplies, and issue visible move and gather orders. |
| Complete | 002 - Build and expand population | Spend Supplies through the real economy to place and complete a House. |
| Complete | 003 - Train and counter | Train the first formations through the Hisar queue, then resolve the first readable counter fight. |
| Complete | 004 - Build the defensive line | Place a Storehouse and Tower with valid placement, construction, and destruction states. |
| Complete | 005 - Contest the road | Add Cavalry, control groups, formations, fog, and minimap information. |
| Complete | 006 - Win a complete match | Add fair scripted AI, both Hisar destruction outcomes, restart, quit, and a 10-15 minute match tune. |
| Complete | 007 - Maneuver and flank | Read formation facing, exploit fixed reorientation, and gain a modest deterministic side or rear damage advantage. |
| Complete | 008 - Hold the frontline | Opposing formations halt direct movement through each other while leaving room to maneuver around a flank. |
| Complete | 009 - Give soldiers battlefield presence | Command formations as one unit while individual soldiers path, fight, take hits, die, and regroup naturally. |
| Complete | 010 - Unblock formation slots | Soldiers settle at reachable positions when structures cover their ideal slots, then reform when space opens. |
| Complete | Exceptional refactor 011 - Restructure the playable prototype | User-approved behavior-preserving restructure that keeps the complete current match playable while making each gameplay domain and test area small enough to understand and change safely. |
| Complete | Exceptional refactor 012 - Make the starting economy code-playable | Run a development player from versioned Worker move/gather commands, inspect fog-safe JSON state, and review a state-paired rendered checkpoint. |
| Complete | Exceptional refactor 013 - Make House construction code-playable | Place and complete a House through a versioned Worker command, observe population capacity rise in fog-safe JSON state, and review the paired rendered checkpoint. |
| Complete | Exceptional refactor 014 - Make formation training code-playable | Gather the real formation cost, train Spearmen through the shared Hisar queue, inspect production and friendly formation JSON state, and review the paired rendered checkpoint. |
| Complete | Exceptional refactor 015 - Make formation movement code-playable | Select trained Spearmen by stable ID, issue the shared formation move order, wait for arrival, and inspect the moved formation in paired JSON and rendered evidence. |
| Complete | Exceptional refactor 016 - Make formation combat code-playable | Scout fog-safe hostile actors, issue shared Attack-Move, Stop, and Focus commands, and inspect deterministic combat in paired JSON and rendered evidence. |
| Complete | Exceptional refactor 017 - Make economy and production code-playable | Operate every match-relevant building, production, cancellation, demolition, and rally command through structured player-authority actions and inspect the Hisar and queue state. |
| Complete | Exceptional refactor 018 - Make the complete match lifecycle code-playable | Win a real-economy built match through structured player-authority commands, inspect immutable state/frame checkpoints and telemetry, then exercise result-gated Restart and Quit. |
| Complete | Exceptional refactor 019 - Make agent play live and adaptive | Keep one Development player alive while an external agent observes fog-safe JSON state, submits one semantic command at a time, corrects rejected actions, and reviews exact state-paired frames. |
| Complete | Exceptional refactor 020 - Isolate verification authority | Fail closed before launch when privileged smoke automation is combined with either player-authority agent mode, so test-only mutations can never contaminate agent observations. |
| Complete | Exceptional refactor 021 - Stabilize complete-match agent scouting | Wait for the west Supplies cache to become observable before gathering it, rather than requiring every selected worker to settle into an exact movement slot. |
| In Progress | Documentation and asset workflow 022 - Establish the authored Archer pipeline | Store and validate the approved Archer source assets and reusable concept-to-Meshy-to-Mixamo workflow without changing the running game. |

Use [the slice template](docs/slices/TEMPLATE.md) for each new slice. Do not begin a
slice until its player outcome and deterministic acceptance path are explicit.
