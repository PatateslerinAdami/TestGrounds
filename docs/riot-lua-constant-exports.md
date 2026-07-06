# Riot Lua constant exports (authoritative enum values)

> There is a SECOND, larger export table in
> `herowars_clientserver/sources/newlogic/luaspellscripthelper.cpp:~15000` (103 constants:
> SPELL_* SpellDataFlags, BUFF_* BuffTypes, EFFCREATE_* FXFlags low-name forms,
> AI_* OrderTypes, DAMAGESOURCE_*, UNITSCAN_*, CHANNELINGSTOP*). Raw dump:
> `docs/riot-lua-constant-exports-spellhelper.txt`.

Source: S1 decomp `herowars_clientserver/sources/world/luabuildingblockhelper.cpp:16927-16983`
(`LuaPlus::LuaObject::SetInteger(ioGlobals, ...)`). These are the values Riot's own
spell/buff Lua scripts see — the ground truth for any script-facing GameServerCore enum.
Cross-checked 2026-06-07: ChannelingStop* values identical in the S4 mac decomp (4.17),
so this table stayed stable from S1 through S4.

## Targeting type (`TTYPE_*`)
| Name | Value |
|---|---|
| TTYPE_Invalid | -1 |
| TTYPE_Self | 0 |
| TTYPE_Target | 1 |
| TTYPE_Area | 2 |
| TTYPE_Cone | 3 |
| TTYPE_SelfAOE | 4 |
| TTYPE_TargetOrLocation | 5 |
| TTYPE_Location | 6 |
| TTYPE_Direction | 7 |

## Teams (`TEAM_*`)
| Name | Value |
|---|---|
| TEAM_UNKNOWN | 0 |
| TEAM_ORDER | 100 |
| TEAM_CHAOS | 200 |
| TEAM_NEUTRAL | 300 |
| TEAM_OWNER | 401 (script pseudo-team) |
| TEAM_CASTER | 402 (script pseudo-team) |
| TEAM_TARGET | 403 (script pseudo-team) |

## Spellbook / slots
| Name | Value |
|---|---|
| SpellSlots | 0 |
| InventorySlots | 1 |
| ExtraSlots | 2 |
| SPELLBOOK_CHAMPION | 0 |
| SPELLBOOK_SUMMONER | 1 |
| SPAWN_LOCATION | 1 |

## Buff add type (`BUFF_*`)
| Name | Value |
|---|---|
| BUFF_REPLACE_EXISTING | 0 |
| BUFF_RENEW_EXISTING | 1 |
| BUFF_STACKS_AND_RENEWS | 2 |
| BUFF_STACKS_AND_CONTINUE | 3 |
| BUFF_STACKS_AND_OVERLAPS | 4 |

## Channeling stop (verified vs mac decomp ChannelingEnums.h — identical)
| Name | Value |
|---|---|
| ChannelingStopCondition_NotCancelled | 0 |
| ChannelingStopCondition_Success | 1 |
| ChannelingStopCondition_Cancel | 2 |
| ChannelingStopSource_NotCancelled | 0 |
| ChannelingStopSource_TimeCompleted | 1 |
| ChannelingStopSource_Animation | 2 |
| ChannelingStopSource_LostTarget | 3 |
| ChannelingStopSource_StunnedOrSilencedOrTaunted | 4 |
| ChannelingStopSource_ChannelingCondition | 5 |
| ChannelingStopSource_Die | 6 |
| ChannelingStopSource_HeroReincarnate | 7 |
| ChannelingStopSource_Move | 8 |
| ChannelingStopSource_Attack | 9 |
| ChannelingStopSource_Casting | 10 |
| ChannelingStopSource_Unknown | 11 |

## Missile / forced-movement end behavior
| Name | Value |
|---|---|
| FURTHEST_WITHIN_RANGE | 0 |
| FIRST_COLLISION_HIT | 1 |
| GET_NEAREST_IN_RANGE | 2 |
| GET_NEAREST_IN_RANGE_INCLUDE_UNITS | 3 |
| FIRST_WALL_HIT | 4 |

## Order handling / facing
| Name | Value |
|---|---|
| CANCEL_ORDER | 0 |
| POSTPONE_CURRENT_ORDER | 1 |
| FACE_MOVEMENT_DIRECTION | 0 |
| KEEP_CURRENT_FACING | 1 |

## Primary ability resource (`PAR_*`) — S1 table, superseded in 4.x
| Name | Value |
|---|---|
| PAR_MANA | 0 |
| PAR_ENERGY | 1 |
| PAR_SOULS | 2 |
| PAR_SHIELD | 3 |
| PAR_OTHER | 4 |

4.17 version (mac decomp `AIHeroPrimaryAbilityResource.h:3`, authoritative for us):
MANA=0, ENERGY=1, NONE=2 (renamed from SOULS), SHIELD=3, BATTLEFURY=4, DRAGONFURY=5,
RAGE=6, HEAT=7, GNARFURY=8, FEROCITY=9, BLOODWELL=10, WIND=11, OTHER=12,
MAX=13 (sentinel), USE_CHAR_CURRENT=14 (sentinel). Our `PrimaryAbilityResourceType`
matches 0-12 exactly (verified 2026-06-07); the two sentinels are not ported.

## Misc
| Name | Value |
|---|---|
| HAS_SUNGLASSES | 0 (Riot easter egg, real export) |

## Cross-check status vs GameServerCore enums (2026-06-07)
- `BuffAddType` 0-4: exact match ✓
- `TargetingType` 0-7: exact match ✓ (our DragDirection=8 / LineTargetToCaster=9 are 4.x
  additions not in this S1 table - verify separately against S4 SpellData consumers;
  Riot TTYPE_Invalid=-1 corresponds to our byte-enum Invalid=0xFF)
- `PrimaryAbilityResourceType` 0-12: exact match vs 4.17 header ✓
- `ChannelingStopCondition` / `ChannelingStopSource`: exact match ✓ (PlayerCommand removed)
- `TeamId`: TEAM_ORDER=100/CHAOS=200/NEUTRAL=300 - pseudo-teams 401-403 (OWNER/CASTER/TARGET)
  exist only as Lua-script filter values, not real teams
