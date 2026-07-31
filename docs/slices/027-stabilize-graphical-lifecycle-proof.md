# Corrective workflow 027 - Stabilize graphical lifecycle proof

This is an urgent post-merge verification correction, not a gameplay slice.

## Observable outcome

The mandatory graphical complete-match Quit proof no longer lets the AI outrun the scripted
player economy under renderer load, while headless semantic sessions retain their faster speed.

## Acceptance checks

- Run the graphical complete-match Quit path repeatedly at 2x simulation speed.
- Reach Victory and invoke the shipped `Quit` listener on every repetition.
- Keep headless Restart and live-agent verification at 4x.
- Record both speeds accurately in the exact-SHA verification summary.
- Pass the full verification ladder and post-merge proof.

## Non-goals

- No gameplay, AI, economy, balance, map, or presentation changes.
- No change to normal player simulation speed.
- No return to the redundant graphical scenario matrix.
