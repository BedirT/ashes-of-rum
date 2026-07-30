# Archer Mixamo Animation Manifest

Source: user-downloaded `archer_animation_pack.zip`, 2026-07-29.

Download contract: FBX for Unity, 30 FPS, no keyframe reduction; canonical model with skin and T-pose,
all motion files without skin. All durations below were measured after Blender 5.2 FBX import. Every
motion has the canonical 49-bone skeleton fingerprint documented in `README.md`.

`ANIMATION_IMPORT.json` is the machine-readable Unity import companion to this table. Its
`loopMotions` array contains exactly the rows marked `Yes`, allowing the checked-in intake command to
reapply this mapping without manually transcribing 17 names.

Status meanings:

- **Core candidate**: needed by the current Archer role, but still requires preview with the actual bow.
- **Optional source**: preserved for later evaluation without implying a gameplay requirement.
- **Out of scope**: intentionally not eligible for current runtime integration because the gameplay
  contract has no corresponding behavior.

| Repository file | Original pack file | Seconds | Loop intent | Status | Intended review |
| --- | --- | ---: | --- | --- | --- |
| `Archer_Idle.fbx` | `standing idle 01.fbx` | 5.0333 | Yes | Core candidate | Restrained bow-ready idle. |
| `Archer_WalkForward.fbx` | `standing walk forward.fbx` | 1.2000 | Yes | Core candidate | In-place forward locomotion. |
| `Archer_RunForward.fbx` | `standing run forward.fbx` | 0.8667 | Yes | Core candidate | In-place fast locomotion. |
| `Archer_DrawArrow.fbx` | `standing draw arrow.fbx` | 1.0333 | No | Core candidate | Nock/draw phase; confirm hand path. |
| `Archer_AimOverdraw.fbx` | `standing aim overdraw.fbx` | 3.7000 | No | Core candidate | Attack/hold candidate; likely requires trimming. |
| `Archer_AimRecoil.fbx` | `standing aim recoil.fbx` | 0.7000 | No | Core candidate | Release/recovery candidate; identify release frame. |
| `Archer_HitFront.fbx` | `standing react small from front.fbx` | 1.2667 | No | Core candidate | Trim to compact non-blocking reaction if needed. |
| `Archer_DeathBackward.fbx` | `standing death backward 01.fbx` | 3.1000 | No | Core candidate | Check final footprint against formation spacing. |
| `Archer_DeathForward.fbx` | `standing death forward 01.fbx` | 3.1667 | No | Optional source | Alternate compact-death candidate. |
| `Archer_IdleLook.fbx` | `standing idle 02 looking.fbx` | 3.2000 | Yes | Optional source | Alternate idle only. |
| `Archer_IdleExamine.fbx` | `standing idle 03 examine.fbx` | 5.1000 | Yes | Optional source | Likely too busy for formation idle. |
| `Archer_UnarmedIdle.fbx` | `unarmed idle 01.fbx` | 4.9667 | Yes | Optional source | Reference/retarget fallback only. |
| `Archer_AimWalkForward.fbx` | `standing aim walk forward.fbx` | 1.2000 | Yes | Optional source | Current archers stop to fire. |
| `Archer_AimWalkBackward.fbx` | `standing aim walk back.fbx` | 1.4333 | Yes | Optional source | Current archers stop to fire. |
| `Archer_AimWalkLeft.fbx` | `standing aim walk left.fbx` | 1.2000 | Yes | Optional source | Current archers stop to fire. |
| `Archer_AimWalkRight.fbx` | `standing aim walk right.fbx` | 1.3000 | Yes | Optional source | Current archers stop to fire. |
| `Archer_WalkBackward.fbx` | `standing walk back.fbx` | 1.4667 | Yes | Optional source | Runtime usually rotates and walks forward. |
| `Archer_WalkLeft.fbx` | `standing walk left.fbx` | 1.2000 | Yes | Optional source | Blend-tree option only. |
| `Archer_WalkRight.fbx` | `standing walk right.fbx` | 1.2000 | Yes | Optional source | Blend-tree option only. |
| `Archer_RunBackward.fbx` | `standing run back.fbx` | 0.6667 | Yes | Optional source | Runtime usually rotates and runs forward. |
| `Archer_RunLeft.fbx` | `standing run left.fbx` | 0.6667 | Yes | Optional source | Blend-tree option only. |
| `Archer_RunRight.fbx` | `standing run right.fbx` | 0.7667 | Yes | Optional source | Blend-tree option only. |
| `Archer_RunForwardStop.fbx` | `standing run forward stop.fbx` | 0.9000 | No | Optional source | Current prototype does not require stop clips. |
| `Archer_TurnLeft90.fbx` | `standing turn 90 left.fbx` | 1.1667 | No | Optional source | Runtime owns the fixed 0.45-second turn. |
| `Archer_TurnRight90.fbx` | `standing turn 90 right.fbx` | 1.1000 | No | Optional source | Runtime owns the fixed 0.45-second turn. |
| `Archer_EquipBow.fbx` | `standing equip bow.fbx` | 0.8667 | No | Optional source | Bow may remain equipped throughout combat. |
| `Archer_UnequipBow.fbx` | `standing disarm bow.fbx` | 1.1000 | No | Optional source | Bow may remain equipped throughout combat. |
| `Archer_HitHeadshot.fbx` | `standing react small from headshot.fbx` | 0.9333 | No | Optional source | No headshot mechanic; possible generic hit alternative. |
| `Archer_Block.fbx` | `standing block.fbx` | 1.8000 | No | Out of scope | No block ability or stance. |
| `Archer_DiveForward.fbx` | `standing dive forward.fbx` | 1.6333 | No | Out of scope | No dive ability. |
| `Archer_DodgeBackward.fbx` | `standing dodge backward.fbx` | 1.6333 | No | Out of scope | No dodge ability. |
| `Archer_DodgeForward.fbx` | `standing dodge forward.fbx` | 1.0000 | No | Out of scope | No dodge ability. |
| `Archer_DodgeLeft.fbx` | `standing dodge left.fbx` | 0.9667 | No | Out of scope | No dodge ability. |
| `Archer_DodgeRight.fbx` | `standing dodge right.fbx` | 0.9667 | No | Out of scope | No dodge ability. |
| `Archer_Kick.fbx` | `standing melee kick.fbx` | 1.4333 | No | Out of scope | No alternate melee attack. |
| `Archer_Punch.fbx` | `standing melee punch.fbx` | 1.0000 | No | Out of scope | No alternate melee attack. |
| `Archer_FallLoop.fbx` | `fall a loop.fbx` | 1.0000 | Yes | Out of scope | No airborne/falling state. |
| `Archer_LandToIdle.fbx` | `fall a land to standing idle 01.fbx` | 0.6333 | No | Out of scope | No landing state. |
| `Archer_LandToRun.fbx` | `fall a land to run forward.fbx` | 0.9333 | No | Out of scope | No landing state. |

## Integration-Readiness Gate

Before selecting final core clips, preview them on `Model/Archer.fbx` with the approved bow attached to
the left hand and an arrow aligned to the right hand. Check foot sliding, root displacement, wrist and
shoulder deformation, hand/torso intersections, quiver clipping, neighboring formation footprint, and
the exact projectile-release frame. Availability in this archive is not acceptance for gameplay.
