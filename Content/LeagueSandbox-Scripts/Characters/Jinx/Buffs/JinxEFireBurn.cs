using GameServerCore.Enums;
using GameServerCore.Scripting.CSharp;
using LeagueSandbox.GameServer.GameObjects;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits;
using LeagueSandbox.GameServer.GameObjects.AttackableUnits.AI;
using LeagueSandbox.GameServer.GameObjects.SpellNS;
using LeagueSandbox.GameServer.GameObjects.StatsNS;
using LeagueSandbox.GameServer.Scripting.CSharp;
using static LeagueSandbox.GameServer.API.ApiFunctionManager;

namespace Buffs;

internal class JinxEFireBurn : IBuffGameScript {
    private ObjAIBase        _jinx;
    private AttackableUnit    _unit;
    private Particle _hit;
    private PeriodicTicker _periodicTicker;
    public BuffScriptMetaData BuffMetaData { get; set; } = new() {
        BuffType    = BuffType.DAMAGE,
        BuffAddType = BuffAddType.REPLACE_EXISTING
    };

    public StatsModifier StatsModifier { get; } = new();

    public void OnActivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        _jinx = ownerSpell.CastInfo.Owner;
        _unit = unit;
        _hit = _jinx.SkinID switch {
            _ => AddParticleTarget(ownerSpell.CastInfo.Owner, unit, "Jinx_E_Burning", unit, buff.Duration,
                                   bone: "BUFFBONE_GLB_GROUND_LOC")
        };
    }

    public void OnUpdate(float diff) {
        var ticks = _periodicTicker.ConsumeTicks(diff, 500f, true, 1, 3);
        if (ticks != 1) return;
        var ap  = _jinx.Stats.AbilityPower.Total;
        var dmg = (90 + 60 *(_jinx.GetSpell("JinxW").CastInfo.SpellLevel - 1) + ap)/3;
        _unit.TakeDamage(_jinx, dmg, DamageType.DAMAGE_TYPE_MAGICAL, DamageSource.DAMAGE_SOURCE_ATTACK,
                        DamageResultType.RESULT_NORMAL);
    }

    public void OnDeactivate(AttackableUnit unit, Buff buff, Spell ownerSpell) {
        RemoveParticle(_hit);
    }
}