# Riot BuffType CC taxonomy (patch 4.17, authoritative)

Source: S4 mac decomp `src/include/Game/LoL/Spell/Buff/BuffEnums.h` -
`Spell::Buff::BuffTypeHelper` static bitmask constants, each a mask over
`(1 << BuffType)`. Decoded 2026-06-07. Our `BuffType` enum matches Riot's 1:1
(values 0-31), so these masks apply directly.

| Constant | Hex | Members |
|---|---|---|
| kCrowdControlBuffFlags | 0xf3640fa0 | Stun, Silence, Taunt, Polymorph, Slow, Snare, Sleep, Fear, Charm, Suppression, Blind, Flee, Knockup, Knockback, Disarm |
| kHardCrowdControlFlags | 0x31240120 | Stun, Taunt, Sleep, Fear, Suppression, Flee, Knockup |
| kTenacityReducibleCCFlags | 0x12640da0 | Stun, Silence, Taunt, Slow, Snare, Sleep, Fear, Charm, Blind, Flee |
| kFearFlags | 0x10200000 | Fear, Flee |
| kCharmFlags | 0x00400000 | Charm |
| kTauntFlags | 0x00000100 | Taunt |
| kMovementImpairing | 0x00400c00 | Slow, Snare, Charm |
| kMovementDisabling | 0x00400900 | Taunt, Snare, Charm |
| kAbilityDisablingFlags | 0x10000380 | Silence, Taunt, Polymorph, Flee |
| kAttackDisablingFlags | 0x90000200 | Polymorph, Flee, Disarm |
| kAttackBlindingFlags | 0x02000000 | Blind |
| kSummonerDisablingFlags | 0x01000000 | Suppression |
| kKnockupFlags | 0x20000000 | Knockup |
| kKnockbackFlags | 0x40000000 | Knockback |
| kStunOrSuppressedFlags | 0x01000020 | Stun, Suppression |
| kNegativeFlags | 0xfbec1fa8 | CombatDehancer, Stun, Silence, Taunt, Polymorph, Slow, Snare, Damage, Sleep, NearSight, Fear, Charm, Poison, Suppression, Blind, Shred, Flee, Knockup, Knockback, Disarm |

Notable rules encoded here:
- **Hard CC includes Knockup but NOT Knockback** (knockback is its own category).
- **Tenacity does NOT reduce** Polymorph, Suppression, Knockup, Knockback (correct
  4.x behavior); it DOES reduce Blind and Silence in this patch.
- Slow is movement-IMPAIRING but not movement-DISABLING; Taunt disables movement
  control but is not "impairing".
- Stun/Suppression are handled by their own combined mask, separate from
  kAbilityDisabling (Silence, Taunt, Polymorph, Flee).
- kNegativeFlags = the dispellable/"is a debuff" set (note: Damage, Poison,
  NearSight, Shred, CombatDehancer count as negative but not as CC).

`BuffTypeHelper` also provides per-type predicates (IsHardCCType,
IsTenacityReducibleCCType, IsRootType, ...) that just test these masks.
