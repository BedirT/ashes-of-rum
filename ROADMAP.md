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
| In Progress | 009 - Give soldiers battlefield presence | Command formations as one unit while individual soldiers path, fight, take hits, die, and regroup naturally. |

Use [the slice template](docs/slices/TEMPLATE.md) for each new slice. Do not begin a
slice until its player outcome and deterministic acceptance path are explicit.
