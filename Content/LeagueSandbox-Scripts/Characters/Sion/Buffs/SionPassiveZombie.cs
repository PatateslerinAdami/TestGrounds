using System.Collections.Generic;
using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using GameServerLib.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.API;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class SionPassiveZombie: IBuffGameScript
{
    private ObjAIBase _sion;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType = BuffType.COMBAT_ENCHANCER,
        BuffAddType = BuffAddType.RENEW_EXISTING,
        PersistsThroughDeath = true,
        IsHidden = true
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell)
    {
        _sion = buff.SourceUnit;
        unit.Stats.CurrentHealth = unit.Stats.HealthPoints.Total;
        OverrideAutoAttacks(_sion, false, "SionBasicAttackPassive2", "SionBasicAttackPassive");
        unit.SetAnimStates(new Dictionary<string, string> {
            { "IDLE1", "PASSIVE_IDLE1" },
            { "IDLE1_BASE",    "PASSIVE_IDLE1"    },
            { "IDLE2_BASE",     "PASSIVE_IDLE1"     },
            { "IDLE_IN",   "PASSIVE_IDLE1"   },
            { "RUN",   "Passive_Run_Raw"   },
            { "RUN_HASTE",   "PASSIVE_RUN"   },
            { "LAUGH",   "PASSIVE_DANCE"   },
            { "DANCE",   "PASSIVE_DANCE"   },
            { "TAUNT",   "PASSIVE_DANCE"   },
            { "JOKE",   "PASSIVE_DANCE"   },
        });
        SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration, scale: 2.5f, boneName: "L_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Hand.troy", _sion, _sion, _sion, lifetime: buff.Duration, scale: 2.5f, boneName: "R_Buffbone_Glb_Hand_Loc",
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Skin.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Cas.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
        SpellEffectCreate("Sion_Base_Passive_Smoke.troy", _sion, _sion, _sion, lifetime: buff.Duration,
            flags: FXFlags.SimulateWhileOffScreen);
    }

    public void OnUpdate(Buff buff, float diff)
    {
        
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ResetCharacterVoiceOverride(buff.SourceUnit);
        _sion.RemoveOverrideAutoAttack();
        unit.SetAnimStates(new Dictionary<string, string> {
            { "ATTACK1", "" },
            { "IDLE1", "" },
            { "IDLE1_BASE",    ""    },
            { "IDLE2_BASE",     ""     },
            { "IDLE_IN",   ""   },
            { "RUN",   ""   },
            { "RUN_HASTE",   ""   },
            { "CRIT",   ""   },
            { "LAUGH",   ""   },
            { "DANCE",   ""   },
            { "TAUNT",   ""   },
            { "JOKE",   ""   },
        });
        _sion.EndZombie();
    }
}