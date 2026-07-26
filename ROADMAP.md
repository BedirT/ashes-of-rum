# Ashes of Rum Prototype Roadmap

This roadmap sequences work as end-to-end vertical slices. Every completed slice must
leave `main` buildable, launchable, and playable at its current scope. Slice order may
change when playtesting exposes a stronger dependency.

## Slice Queue

| State | Slice | Player-observable outcome |
| --- | --- | --- |
| In progress | 000 - Bootstrap automated harness | Import the Unity project, run tests, build a native macOS player, launch a neutral scene, and exit through deterministic smoke. |
| Planned | 001 - Establish and command the starting economy | Launch with one Hisar and four workers, select workers, gather Supplies, and issue visible move and gather orders. |
| Planned | 002 - Build and expand population | Spend Supplies through the real economy to place and complete a House. |
| Planned | 003 - Train and counter | Train the first formations through the Hisar queue, then resolve the first readable counter fight. |
| Planned | 004 - Build the defensive line | Place a Storehouse and Tower with valid placement, construction, and destruction states. |
| Planned | 005 - Contest the road | Add Cavalry, control groups, formations, fog, and minimap information. |
| Planned | 006 - Win a complete match | Add fair scripted AI, both Hisar destruction outcomes, restart, quit, and a 10-15 minute match tune. |

Use [the slice template](docs/slices/TEMPLATE.md) for each new slice. Do not begin a
slice until its player outcome and deterministic acceptance path are explicit.
