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

internal class IreliaHitenStyleCharged : IBuffGameScript {
    private ObjAIBase _irelia;
    private Spell _mainSpell;
    private Particle  _activeGlow, _activate;

    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING,
        MaxStacks   = 1
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _irelia = ownerSpell.CastInfo.Owner;
        _mainSpell = ownerSpell;
        unit.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "Attack1c" },
            { "attack2", "Attack2c" },
            { "crit",    "Critc"    },
            { "run",     "Runc"     },
            { "idle1",   "Idle1c"   }
        });

        ApiEventManager.OnHitUnit.AddListener(this, ownerSpell.CastInfo.Owner, OnHit);
        _activate = AddParticleTarget(_irelia, _irelia, "irelia_hitenStyle_activate", _irelia,
                                      bone: "BUFFBONE_GLB_WEAPON_1");
        _activeGlow = AddParticleTarget(_irelia, _irelia, "irelia_hitenStyle_active_glow", _irelia,
                                        bone: "BUFFBONE_GLB_WEAPON_1");
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        ApiEventManager.RemoveAllListenersForOwner(this);
        RemoveParticle(_activate);
        RemoveParticle(_activeGlow);
        unit.SetAnimStates(new Dictionary<string, string> {
            { "attack1", "" },
            { "attack2", "" },
            { "crit",    "" },
            { "run",     "" },
            { "idle1",   "" }
        });
        AddBuff("IreliaHitenStyle", 999999f, 1, ownerSpell, unit, ownerSpell.CastInfo.Owner, true);
    }

    private void OnHit(DamageData data) {
        var dmg  = 15f + 15f * (_mainSpell.CastInfo.SpellLevel - 1);
        var heal = (3f  + 3f  * (_mainSpell.CastInfo.SpellLevel - 1)) * 2f;

        _irelia.TakeHeal(_irelia, heal, HealType.SelfHeal);
        data.Target.TakeDamage(_irelia, dmg, DamageType.DAMAGE_TYPE_TRUE, DamageSource.DAMAGE_SOURCE_PROC,
                               DamageResultType.RESULT_NORMAL);
    }
}
